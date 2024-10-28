using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UndertaleModLib.Models;
using UndertaleModLib.Scripting;
using UndertaleModLib;
using static UndertaleModLib.UndertaleReader;
using static UndertaleRusInstallerGUI.OSMethods;
using UndertaleRusInstallerGUI.Views;
using System.Collections;
using System.Drawing;
using System.IO.Compression;
using System.Text.RegularExpressions;
using InfoFlags = UndertaleModLib.Models.UndertaleGeneralInfo.InfoFlags;
using FuncClassif = UndertaleModLib.Models.UndertaleGeneralInfo.FunctionClassification;
using OptionsFlags = UndertaleModLib.Models.UndertaleOptions.OptionsFlags;
using System.Collections.Immutable;

namespace UndertaleRusInstallerGUI;

public static class Core
{
    public enum GameType : ushort
    {
        None,
        Undertale,
        XBOXTALE
    }
    private enum BackupResult : ushort
    {
        SourceNotFound,
        Error,
        Success
    }
    public enum FileStatus : ushort
    {
        NotFound,
        Empty,
        OK
    }

    public delegate void MsgDelegate(string text, bool setStatus);

    public static MainWindow MainWindow { get; set; }

    private const string utWinLocation = "{0}Program Files{1}\\Steam\\steamapps\\common\\Undertale\\";
    private const string utMacLocation = "{0}/Library/Application Support/Steam/steamapps/common/Undertale/UNDERTALE.app/";
    private static readonly string[] utLinuxLocations = { "{0}/.steam/steam/steamapps/common/Undertale/", "{0}/.local/share/steam/steamapps/common/Undertale/",
                                                          "{0}/.steam/Steam/steamapps/common/Undertale/", "{0}/.local/share/Steam/steamapps/common/Undertale/" };
    private const string utWinFileLoc = "data.win";
    private const string utMacFileLoc = "Contents/Resources/game.ios";
    private const string utLinuxFileLoc = "assets/game.unx";

    private static readonly Regex utWinLocRegex = new(@"(.+\\)[^\\]+\.win$", RegexOptions.Compiled);
    private static readonly Regex utLinuxLocRegex = new(@"(.+/)assets/[^/]+\.unx$", RegexOptions.Compiled);

    public static readonly string CurrDirPath = Path.GetDirectoryName(Environment.ProcessPath) + Path.DirectorySeparatorChar;
    public static readonly string TempDirPath = Path.Combine(Path.GetTempPath(), "UndertaleRusInstaller") + Path.DirectorySeparatorChar;
    public static readonly string NewDataDirPath = Path.Combine(TempDirPath, "data") + Path.DirectorySeparatorChar;
    public const string ZipName = "ru_data.zip";
    private static string gameDirLocation, gamePrefix;
    public static readonly string[] ValidDataExtensions = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                                                            ? new[] { ".app", ".win", ".ios", ".unx" }
                                                            : new[] { ".win", ".ios", ".unx" };
    private static readonly (string Path, string Name) xboxtaleExePath = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) // This won't be used on Windows
                                                                         ? ("runner", "runner") : ("Contents/MacOS/Mac_Runner", "Mac_Runner");
    private const string demonxWin = "demonx = \"...\"";
    private const string demonxNonWin = "demonx = \"Part of this game's charm is the mystery of how many options or secrets there are. If you are reading this, " +
                                        "please don't post this message or this information anywhere. Or doing secrets will become pointless.\"";

    private static GameType _selectedGame;
    public static GameType SelectedGame
    {
        get => _selectedGame;
        set
        {
            _selectedGame = value;
            gamePrefix = value.ToString().ToLowerInvariant();
        }
    }
    public static UndertaleData Data { get; set; }
    public static string DataPath { get; set; }
    public static bool ReplaceXBOXTALEExe { get; set; }
    public static string XBOXTALEExePath => gameDirLocation + xboxtaleExePath.Path;

    public static string ZipPath { get; set; }
    public static bool ZipIsValid { get; set; } = true; 

    public static long GetFileSize(string path)
    {
        FileInfo info = new(path);
        if (info.LinkTarget is not null)
            info = new(info.LinkTarget);

        return info.Length;
    }

    public static IEnumerable<T> ReverseList<T>(this IList<T> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            yield return list[i];
        }
    }

    // Thanks, ChatGPT-4o.
    public static void ExtractOneDirectory(this ZipArchive zipArchive, string directoryName, string destDirPath,
                                           bool overwriteFiles = true)
    {
        if (!directoryName.EndsWith('/'))
            directoryName += '/';

        if (!Directory.Exists(destDirPath))
            Directory.CreateDirectory(destDirPath);

        bool found = false;
        foreach (var entry in zipArchive.Entries.Where(e => e.FullName.StartsWith(directoryName, StringComparison.InvariantCulture)))
        {
            if (entry.FullName == directoryName)
                continue;

            string relPath = entry.FullName[directoryName.Length..];
            string destPath = Path.Combine(destDirPath, relPath);

            if (relPath.EndsWith('/'))
                Directory.CreateDirectory(destPath);
            else
                entry.ExtractToFile(destPath, overwrite: overwriteFiles);

            found = true;
        }

        if (!found)
            throw new DirectoryNotFoundException($"В архиве нет папки \"{directoryName}\".");
    }

    public static FileStatus IsZipPathValid(string zipPath, bool checkName = true)
    {
        FileStatus res = FileStatus.OK;
        if ((checkName && !zipPath.EndsWith(ZipName, StringComparison.InvariantCulture))
            || !File.Exists(zipPath))
        {
            res = FileStatus.NotFound;
        }
        else if (GetFileSize(zipPath) == 0)
        {
            res = FileStatus.Empty;
        }

        ZipIsValid = (res == FileStatus.OK);

        return res;
    }
    public static string ChooseZipPath()
    {
        string[] types = { "*.zip" };
        string desc = $"Архив с данными русификатора (*.zip)";
        string zipPath = OpenFileDialog($"Выберите архив с данными русификатора", ZipName, 1, types, desc, 0);

        return zipPath;
    }

    public static bool IsDataPathValid(string dataPath)
    {
        return dataPath?.Length >= 5 // "a.win"
               && File.Exists(dataPath)
               && ValidDataExtensions.Any(x => dataPath.EndsWith(x, StringComparison.InvariantCulture));
    }
    public static string ChooseDataPath()
    {
        string[] types = ValidDataExtensions.Select(x => '*' + x).ToArray(); // ".win" -> "*.win"
        string desc = $"Данные игры ({String.Join(", ", types)})";
        string dataPath = OpenFileDialog($"Выберите файл данных {SelectedGame}", "", types.Length, types, desc, 0);
        if (dataPath is null)
            return null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (dataPath.EndsWith(".app/", StringComparison.InvariantCulture)) // Файл приложения MacOS 
            {
                if (SelectedGame == GameType.XBOXTALE)
                    gameDirLocation = dataPath;
                dataPath += utMacFileLoc;
            }
        }
        else if (SelectedGame == GameType.XBOXTALE)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Match linuxDir = utLinuxLocRegex.Match(dataPath);
                if (linuxDir.Success)
                    gameDirLocation = linuxDir.Groups[1].Value;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Match windowsDir = utWinLocRegex.Match(dataPath);
                if (windowsDir.Success)
                    gameDirLocation = windowsDir.Groups[1].Value;
            }
        }
        if (!Directory.Exists(gameDirLocation))
            gameDirLocation = null;

        return dataPath;
    }
    public static string GetDefaultUTFilePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var drive in DriveInfo.GetDrives().Select(d => d.Name))
            {
                string dirPath = String.Format(utWinLocation, drive, " (x86)");
                string path = dirPath + utWinFileLoc;
                if (File.Exists(path))
                {
                    gameDirLocation = dirPath;
                    return path;
                }

                dirPath = String.Format(utWinLocation, drive, "");
                path = dirPath + utWinFileLoc;
                if (File.Exists(path))
                {
                    gameDirLocation = dirPath;
                    return path;
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dirPath = String.Format(utMacLocation, userDir);
            string path = dirPath + utMacFileLoc;
            if (File.Exists(path))
            {
                gameDirLocation = dirPath;
                return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string pathFormat in utLinuxLocations)
            {
                string dirPath = String.Format(pathFormat, userDir);
                string path = dirPath + utLinuxFileLoc;
                if (File.Exists(path))
                {
                    gameDirLocation = dirPath;
                    return path;
                }
            }
        }

        return null;
    }

    public static async Task<bool> LoadDataFile(WarningHandlerDelegate warnDelegate, MessageHandlerDelegate msgDelegate)
    {
        UndertaleData data = null;

        bool res = await Task.Run(() =>
        {
            try
            {
                using (var stream = new FileStream(DataPath, FileMode.Open, FileAccess.Read))
                    data = UndertaleIO.Read(stream, warnDelegate, msgDelegate);
            }
            catch
            {
                MainWindow.ScriptError("Во время загрузки файла возникла ошибка.\n" +
                                       "Текст ошибки смотрите в журнале загрузки.");

                throw;
            }

            Data = data;

            return true;
        });

        return res;
    }

    public static GameType CheckSelectedDataFile()
    {
        if (Data.GeneralInfo.FileName.Content != "UNDERTALE")
            return GameType.None;

        if (Data.IsGameMaker2())
        {
            if (Data.GameObjects.ByName("obj_mewmew_npc") is null)
                return GameType.None;
            else
                return GameType.XBOXTALE;
        }
        else
        {
            var menuDraw = Data.Code.ByName("gml_Object_obj_intromenu_Draw_0");
            // `version = "1.08"`
            if (menuDraw?.Instructions.Any(i => i.Type1 == UndertaleInstruction.DataType.String
                                                && i.Value is UndertaleResourceById<UndertaleString, UndertaleChunkSTRG> strRef
                                                && strRef.Resource.Content == "1.08") != true)
            {
                return GameType.None;
            }
        }

        return GameType.Undertale;
    }

    private static string GetFolder(string path)
    {
        return Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
    }

    // null = not all texture pages were deleted
    // true = all were deleted
    // false = no Russian textures were found
    public static bool? DeleteOldRussianTexturePages(Action<string> warnDelegate)
    {
        if (Data.EmbeddedTextures.Count < 4)
            return false;

        bool? CompareAndDeleteItems(HashSet<UndertaleTexturePageItem> remainingItems)
        {
            var remainingItemsArr = remainingItems.ToArray();
            int i = remainingItems.Count - 1;
            int lastRemItemIndex = Data.TexturePageItems.IndexOf(remainingItemsArr[i]);
            if (lastRemItemIndex == -1)
            {
                warnDelegate($"\"{nameof(CompareAndDeleteItems)}()\" - lastRemItemIndex == -1?");
                return null;
            }

            // I wanted to limit `textPageItems` to the `embedSkipAmount`, but I gave up
            int removeCount = Data.TexturePageItems.Count - lastRemItemIndex - 1;
            var textPageItems = Data.TexturePageItems.ReverseList().Skip(removeCount);

            // Check if there is no unmatched page items in the end
            // or in between the embed. textures
            foreach (var srcItem in textPageItems)
            {
                if (i < 0)
                {
                    warnDelegate($"\"{nameof(CompareAndDeleteItems)}()\" - i < 0?");
                    return null;
                }

                if (srcItem != remainingItemsArr[i--])
                    return false;
            }

            for (i = 0; i < removeCount; i++)
                Data.TexturePageItems.RemoveAt(Data.TexturePageItems.Count - 1);

            return true;
        }

        int embedSkipAmount = (SelectedGame == GameType.Undertale) ? 20 : 35;
        int embedTakeAmount = Data.EmbeddedTextures.Count - embedSkipAmount;
        var embedTextures = Data.EmbeddedTextures.ReverseList().Take(embedTakeAmount);
        var notJaFonts = Data.Fonts.Where(x => !x.Name.Content.StartsWith("fnt_ja", StringComparison.InvariantCulture))
                                   .ToHashSet();
        var pageItems = Data.TexturePageItems.ToHashSet();
        ImmutableDictionary<UndertaleTexturePageItem, int> pageItemsIndexDict = null;
        byte deletedUsedRuTextures = 0;

        foreach (var texture in embedTextures)
        {
            int matchingItemsCount = 0;
            int totalItemsCount = 0;

            foreach (var item in pageItems.Where(x => x.TexturePage == texture))
            {
                var sprite = Data.Sprites.FirstOrDefault(s => s.Textures.Any(x => x.Texture == item));
                if (sprite?.Name?.Content is null)
                {
                    // Deleted all the texture page items that were used
                    if (deletedUsedRuTextures == 2)
                    {
                        matchingItemsCount++;
                        totalItemsCount++;
                        pageItems.Remove(item);

                        continue;
                    }

                    foreach (var font in notJaFonts)
                    {
                        if (font.Texture == item)
                        {
                            notJaFonts.Remove(font);

                            if (font.Glyphs.Any(g => g.Character == 'А')) // Cyrillic
                            {
                                matchingItemsCount++;
                                pageItems.Remove(item);
                            }
                            totalItemsCount++;

                            continue;
                        }
                    }

                    continue;
                }

                if (sprite.Name.Content.EndsWith("_ru", StringComparison.InvariantCulture))
                    matchingItemsCount++;
                totalItemsCount++;

                // Remove other sprite frames, so it
                // won't check the each frame of the same sprite again.
                // Though, it also removes this frame.
                foreach (var frame in sprite.Textures)
                    pageItems.Remove(frame.Texture);
            }

            if (matchingItemsCount == totalItemsCount)
            {
                pageItemsIndexDict ??= Data.TexturePageItems.Zip(Enumerable.Range(0, Data.TexturePageItems.Count))
                                                            .ToImmutableDictionary(x => x.First, x => x.Second);

                bool? res = CompareAndDeleteItems(pageItems);
                if (res != true)
                    return res;

                Data.EmbeddedTextures.RemoveAt(Data.EmbeddedTextures.Count - 1);

                if (deletedUsedRuTextures < 2)
                    deletedUsedRuTextures++;
            }
            else
            {
                break;
            }
        }

        // If it's 3+ installation (there are 4+ RU textures), then we can't decide
        // whether all unused textures are the Russian ones, but this should be sufficient.
        return notJaFonts.Count == 0;
    }

    public static async Task<bool> MakeDataBackup(MsgDelegate msgDelegate, Action<string> errorDelegate)
    {
        bool res = await Task.Run(() =>
        {
            string dataFolder = GetFolder(DataPath);
            string dataName = Path.GetFileName(DataPath);

            msgDelegate($"Создание резервной копии файла данных {SelectedGame}...", true);
            string backupFolder = dataFolder + "backup";
            string backupPath = Path.Combine(backupFolder, dataName);
            if (File.Exists(backupPath))
            {
                bool rewrite = MainWindow.ScriptQuestion($"Резервная копия уже существует (путь - `{backupPath}`).\n" +
                                                         "Заменить её?");
                if (rewrite)
                {
                    try
                    {
                        File.Delete(backupPath);
                    }
                    catch (Exception ex)
                    {
                        errorDelegate($"Не удалось удалить существующую резервную копию.\nОшибка - {ex}");
                        return false;
                    }
                }
                else
                    return false;
            }
            else
            {
                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);
            }

            try
            {
                File.Copy(DataPath, backupPath);
            }
            catch (Exception ex)
            {
                errorDelegate($"Не удалось создать резервную копию.\nОшибка - {ex}");
                return false;
            }
            msgDelegate($"Резервная копия файла создана, путь:\n\"{backupPath}\".", false);

            return true;
        });

        return res;
    }

    public static async Task ExtractNewData(MsgDelegate msgDelegate)
    {
        if (!File.Exists(ZipPath))
            throw new ScriptException($"Ошибка - не найден архив с данными \"{ZipPath}\".");

        await Task.Run(() =>
        {
            msgDelegate("Распаковка архива с данными...", false);

            using ZipArchive archive = ZipFile.OpenRead(ZipPath);
            archive.ExtractOneDirectory("common", NewDataDirPath);
            archive.ExtractOneDirectory(gamePrefix, NewDataDirPath);
        });
    }
    public static async Task DeleteTempData(MsgDelegate msgDelegate)
    {
        if (!Directory.Exists(TempDirPath))
            return;

        await Task.Run(() =>
        {
            msgDelegate("Удаление остаточных файлов данных...", false);
            Directory.Delete(TempDirPath, true);
        });
    }
    public static async Task<bool> InstallMod(MsgDelegate msgDelegate, Action<string> errorDelegate, Action<string> warnDelegate,
                                              Action<string> statusMsgDeleg, Action<double> statusMaxDeleg,
                                              Action valueIncrDeleg)
    {
        bool res = await Task.Run(() =>
        {
            bool? deletedOldRusTextures = DeleteOldRussianTexturePages(warnDelegate);
            if (deletedOldRusTextures is bool res)
            {
                if (res)
                    msgDelegate("Обнаружены и успешно удалены предыдущие установленные текстуры русификатора.", false);
            }
            else
            {
                warnDelegate("Внимание - обнаружены предыдущие установленные текстуры русификатора, но не удалось удалить их все.");
            }

            MainWindow.ImportGMLString("gml_Script_textdata_ru", "");

            string packDir = TempDirPath + "Packager";
            if (!Directory.Exists(NewDataDirPath))
                throw new ScriptException($"Ошибка - не найдена папка \"{NewDataDirPath}\".");

            string fontsDir = Path.Combine(NewDataDirPath, "ru_fonts") + Path.DirectorySeparatorChar;
            string spritesDir = Path.Combine(NewDataDirPath, "ru_sprites") + Path.DirectorySeparatorChar;
            string codeDir = Path.Combine(NewDataDirPath, "code") + Path.DirectorySeparatorChar;
            if (!Directory.Exists(fontsDir))
                throw new ScriptException($"Ошибка - не найдена папка \"{fontsDir}\".");
            if (!Directory.Exists(spritesDir))
                throw new ScriptException($"Ошибка - не найдена папка \"{spritesDir}\".");
            if (!Directory.Exists(codeDir))
                throw new ScriptException($"Ошибка - не найдена папка \"{codeDir}\".");

            #region Game specific operations
            if (SelectedGame == GameType.Undertale)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        string codePath = Path.Combine(codeDir, "gml_Script_scr_namingscreen_check.gml");
                        string gmlText = File.ReadAllText(codePath);
                        gmlText = gmlText.Replace(demonxNonWin, demonxWin);
                        File.WriteAllText(codePath, gmlText);
                    }
                    catch (Exception ex)
                    {
                        warnDelegate($"Не удалось заменить строку с \"demonx\" - {ex.Message}.");
                    }
                }
            }
            else if (SelectedGame == GameType.XBOXTALE)
            {
                if (!InstallToXBOXTALEPart(msgDelegate, errorDelegate, warnDelegate))
                    return false;
            }
            #endregion

            #region Font import 
            msgDelegate("Замена шрифтов...", false);
            statusMsgDeleg("Импорт текстур шрифтов: ");
            statusMaxDeleg(0);

            Directory.CreateDirectory(packDir);
            string sourcePath = fontsDir;
            string searchPattern = "*.png";
            string outName = Path.Combine(packDir, "atlas.txt");
            int textureSize = 2048;
            int border = 2;
            bool debug = false;
            Packer packer = new();
            double max = 0;
            try
            {
                packer.Process(sourcePath, searchPattern, textureSize, border, debug, valueIncrDeleg, statusMaxDeleg);
                max = packer.Atlasses.SelectMany(x => x.Nodes).Count(x => x.Texture is not null);
                statusMaxDeleg(max);
                packer.SaveAtlasses(outName, valueIncrDeleg);
            }
            catch (Exception ex)
            {
                if (ex.Message[0] == '.')
                    throw new ScriptException($"Ошибка при импорте текстуры шрифта \"{Path.GetFileName(ex.Message[1..])}\":\n{ex}");
                else
                    throw new ScriptException($"Ошибка при замене шрифтов:\n{ex}");
            }

            statusMsgDeleg("Замена текстур шрифтов: ");
            statusMaxDeleg(max);

            int lastTextPage = Data.EmbeddedTextures.Count - 1;
            int lastTextPageItem = Data.TexturePageItems.Count - 1;

            string prefix = outName.Replace(Path.GetExtension(outName), "");
            int atlasCount = 0;
            foreach (Atlas atlas in packer.Atlasses)
            {
                string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);
                UndertaleEmbeddedTexture texture = new();
                texture.Name = new UndertaleString("Texture " + ++lastTextPage);
                texture.TextureData.TextureBlob = File.ReadAllBytes(atlasName);
                Data.EmbeddedTextures.Add(texture);
                foreach (Node n in atlas.Nodes)
                {
                    if (n.Texture != null)
                    {
                        string stripped = Path.GetFileNameWithoutExtension(n.Texture.Source);
                        valueIncrDeleg();

                        UndertaleTexturePageItem texturePageItem = new();
                        lastTextPageItem++;
                        texturePageItem.SourceX = (ushort)n.Bounds.X;
                        texturePageItem.SourceY = (ushort)n.Bounds.Y;
                        texturePageItem.SourceWidth = (ushort)n.Bounds.Width;
                        texturePageItem.SourceHeight = (ushort)n.Bounds.Height;
                        texturePageItem.TargetX = 0;
                        texturePageItem.TargetY = 0;
                        texturePageItem.TargetWidth = (ushort)n.Bounds.Width;
                        texturePageItem.TargetHeight = (ushort)n.Bounds.Height;
                        texturePageItem.BoundingWidth = (ushort)n.Bounds.Width;
                        texturePageItem.BoundingHeight = (ushort)n.Bounds.Height;
                        texturePageItem.TexturePage = texture;
                        Data.TexturePageItems.Add(texturePageItem);


                        UndertaleFont font = null;
                        font = Data.Fonts.ByName(stripped);

                        if (font == null)
                        {
                            UndertaleString fontUTString = Data.Strings.MakeString(stripped);
                            UndertaleFont newFont = new();
                            newFont.Name = fontUTString;

                            FontUpdate(sourcePath, ref newFont);
                            newFont.Texture = texturePageItem;
                            Data.Fonts.Add(newFont);
                            continue;
                        }

                        FontUpdate(sourcePath, ref font);
                        font.Texture = texturePageItem;
                        UndertaleSprite.TextureEntry texentry = new();
                        texentry.Texture = texturePageItem;
                    }
                }
                atlasCount++;
            }

            Directory.Delete(packDir, true);
            #endregion

            #region Sprite import
            msgDelegate("Замена спрайтов...", false);
            statusMsgDeleg("Импорт текстур спрайтов: ");
            statusMaxDeleg(0);

            Directory.CreateDirectory(packDir);
            sourcePath = spritesDir;
            packer = new Packer();
            try
            {
                packer.Process(sourcePath, searchPattern, textureSize, border, debug, valueIncrDeleg, statusMaxDeleg);
                max = packer.Atlasses.SelectMany(x => x.Nodes).Count(x => x.Texture is not null);
                statusMaxDeleg(max);
                packer.SaveAtlasses(outName, valueIncrDeleg);
            }
            catch (Exception ex)
            {
                if (ex.Message[0] == '.')
                    throw new ScriptException($"Ошибка при импорте спрайта \"{Path.GetFileName(ex.Message[1..])}\":\n{ex}");
                else
                    throw new ScriptException($"Ошибка при замене спрайтов:\n{ex}");
            }

            string[] newMasks = { "spr_wordfall1_ru_0", "spr_wordfall2_ru_0", "spr_wordfall3_ru_0", "spr_wordfall4_ru_0", "spr_wordfall5_ru_0", "spr_wordfall6_ru_0", "spr_wordfall7_ru_0", "spr_talkbt_hollow_ru_0", "spr_quittingmessage_ru_0", "spr_pressz_press_ru_0", "spr_oolbone_ru_0", "spr_itembt_hollow_ru_0", "spr_hpname_ru_0", "spr_fightbt_hollow_ru_0", "spr_egggraph_ru_0", "spr_dmgmiss_o_ru_0", "spr_dbone_ru_0", "spr_cbone_ru_0", "spr_bulletNapstaSad_ru_0", "spr_barktry_ru_0", "spr_actbt_center_hole_ru_0", "spr_6hope_ru_0" };

            statusMsgDeleg("Замена текстур спрайтов: ");
            statusMaxDeleg(max);

            lastTextPage = Data.EmbeddedTextures.Count - 1;
            lastTextPageItem = Data.TexturePageItems.Count - 1;

            // Import everything into UMT
            atlasCount = 0;
            foreach (Atlas atlas in packer.Atlasses)
            {
                string atlasName = Path.Combine(packDir, String.Format(prefix + "{0:000}" + ".png", atlasCount));
                Bitmap atlasBitmap = new(atlasName);
                UndertaleEmbeddedTexture texture = new();
                texture.Name = new UndertaleString("Texture " + ++lastTextPage);
                texture.TextureData.TextureBlob = File.ReadAllBytes(atlasName);
                Data.EmbeddedTextures.Add(texture);
                foreach (Node n in atlas.Nodes)
                {
                    if (n.Texture != null)
                    {
                        string stripped = Path.GetFileNameWithoutExtension(n.Texture.Source);
                        valueIncrDeleg();

                        // Initalize values of this texture
                        UndertaleTexturePageItem texturePageItem = new();
                        lastTextPageItem++;
                        texturePageItem.SourceX = (ushort)n.Bounds.X;
                        texturePageItem.SourceY = (ushort)n.Bounds.Y;
                        texturePageItem.SourceWidth = (ushort)n.Bounds.Width;
                        texturePageItem.SourceHeight = (ushort)n.Bounds.Height;
                        texturePageItem.TargetX = 0;
                        texturePageItem.TargetY = 0;
                        texturePageItem.TargetWidth = (ushort)n.Bounds.Width;
                        texturePageItem.TargetHeight = (ushort)n.Bounds.Height;
                        texturePageItem.BoundingWidth = (ushort)n.Bounds.Width;
                        texturePageItem.BoundingHeight = (ushort)n.Bounds.Height;
                        texturePageItem.TexturePage = texture;

                        // Add this texture to UMT
                        Data.TexturePageItems.Add(texturePageItem);

                        SpriteType spriteType = GetSpriteType(n.Texture.Source);

                        SetTextureTargetBounds(ref texturePageItem, n);

                        if (spriteType == SpriteType.Background)
                        {
                            UndertaleBackground background = Data.Backgrounds.ByName(stripped);
                            if (background != null)
                            {
                                background.Texture = texturePageItem;
                            }
                            else
                            {
                                // No background found, let's make one
                                UndertaleString backgroundUTString = Data.Strings.MakeString(stripped);
                                UndertaleBackground newBackground = new();
                                newBackground.Name = backgroundUTString;
                                newBackground.Transparent = false;
                                newBackground.Preload = false;
                                newBackground.Texture = texturePageItem;
                                Data.Backgrounds.Add(newBackground);
                            }
                        }
                        else if (spriteType == SpriteType.Sprite)
                        {
                            // Get sprite to add this texture to
                            string spriteName;
                            int lastUnderscore, frame;
                            try
                            {
                                lastUnderscore = stripped.LastIndexOf('_');
                                spriteName = stripped[..lastUnderscore];
                                frame = Int32.Parse(stripped[(lastUnderscore + 1)..]);
                            }
                            catch
                            {
                                throw new ScriptException("Изображение " + stripped + " имеет неправильное имя.");
                            }
                            UndertaleSprite sprite = null;
                            sprite = Data.Sprites.ByName(spriteName);

                            // Create TextureEntry object
                            UndertaleSprite.TextureEntry texentry = new();
                            texentry.Texture = texturePageItem;

                            UndertaleSprite origSprite = Data.Sprites.ByName(spriteName[..spriteName.LastIndexOf('_')]);

                            // Set values for new sprites
                            if (sprite == null)
                            {
                                UndertaleString spriteUTString = Data.Strings.MakeString(spriteName);
                                UndertaleSprite newSprite = new();
                                newSprite.Name = spriteUTString;
                                newSprite.Width = (uint)n.Bounds.Width;
                                newSprite.Height = (uint)n.Bounds.Height;
                                if (origSprite != null)
                                {
                                    newSprite.MarginLeft = origSprite.MarginLeft;
                                    newSprite.MarginRight = origSprite.MarginRight;
                                    newSprite.MarginTop = origSprite.MarginTop;
                                    newSprite.MarginBottom = origSprite.MarginBottom;
                                    newSprite.OriginX = origSprite.OriginX;
                                    newSprite.OriginY = origSprite.OriginY;
                                }
                                else
                                {
                                    newSprite.MarginLeft = 0;
                                    newSprite.MarginRight = n.Bounds.Width - 1;
                                    newSprite.MarginTop = 0;
                                    newSprite.MarginBottom = n.Bounds.Height - 1;
                                    newSprite.OriginX = 0;
                                    newSprite.OriginY = 0;
                                }
                                if (origSprite != null)
                                {
                                    newSprite.OriginX = origSprite.OriginX;
                                    newSprite.OriginY = origSprite.OriginY;
                                }
                                if (frame > 0)
                                {
                                    for (int i = 0; i < frame; i++)
                                        newSprite.Textures.Add(null);
                                }

                                newSprite.CollisionMasks.Add(newSprite.NewMaskEntry());
                                if (origSprite != null && !newMasks.Contains(stripped)
                                    && newSprite.CollisionMasks[0].Data.Length == origSprite.CollisionMasks[0].Data.Length)
                                {
                                    for (int i = 0; i < origSprite.CollisionMasks[0].Data.Length; i++)
                                        newSprite.CollisionMasks[0].Data[i] = origSprite.CollisionMasks[0].Data[i];
                                }
                                else
                                {
                                    Rectangle bmpRect = new(n.Bounds.X, n.Bounds.Y, n.Bounds.Width, n.Bounds.Height);
                                    System.Drawing.Imaging.PixelFormat format = atlasBitmap.PixelFormat;
                                    Bitmap cloneBitmap = atlasBitmap.Clone(bmpRect, format);
                                    int width = ((n.Bounds.Width + 7) / 8) * 8;
                                    BitArray maskingBitArray = new(width * n.Bounds.Height);
                                    for (int y = 0; y < n.Bounds.Height; y++)
                                    {
                                        for (int x = 0; x < n.Bounds.Width; x++)
                                        {
                                            Color pixelColor = cloneBitmap.GetPixel(x, y);
                                            maskingBitArray[y * width + x] = (pixelColor.A > 0);
                                        }
                                    }
                                    BitArray tempBitArray = new(width * n.Bounds.Height);
                                    for (int i = 0; i < maskingBitArray.Length; i += 8)
                                    {
                                        for (int j = 0; j < 8; j++)
                                        {
                                            tempBitArray[j + i] = maskingBitArray[-(j - 7) + i];
                                        }
                                    }
                                    int numBytes;
                                    numBytes = maskingBitArray.Length / 8;
                                    byte[] bytes = new byte[numBytes];
                                    tempBitArray.CopyTo(bytes, 0);
                                    for (int i = 0; i < bytes.Length; i++)
                                        newSprite.CollisionMasks[0].Data[i] = bytes[i];
                                }
                                newSprite.Textures.Add(texentry);
                                Data.Sprites.Add(newSprite);
                                continue;
                            }
                            if (frame > sprite.Textures.Count - 1)
                            {
                                while (frame > sprite.Textures.Count - 1)
                                    sprite.Textures.Add(texentry);

                                continue;
                            }
                            sprite.Textures[frame] = texentry;
                        }
                    }
                }
                // Increment atlas
                atlasCount++;
                atlasBitmap.Dispose();
            }

            Directory.Delete(packDir, true);
            #endregion

            #region Code replacement
            string[] codeFiles = Directory.GetFiles(codeDir, "*.gml").OrderBy(x => x).ToArray();

            msgDelegate("Замена кода...", false);
            statusMsgDeleg("Импорт и замена кода: ");
            statusMaxDeleg(codeFiles.Length);
            foreach (var file in codeFiles)
            {
                valueIncrDeleg();

                int lastIndexOfSep = file.LastIndexOf(Path.DirectorySeparatorChar);
                try
                {
                    MainWindow.ImportGMLString(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
                }
                catch (Exception ex)
                {
                    throw new ScriptException($"Ошибка при импорте файла кода \"{Path.GetFileName(file)}\":\n{ex}");
                }
            }
            #endregion

            #region Saving the game data file
            msgDelegate("Сохранение файла игровых данных...", true);
            string tempFilePath = DataPath + "temp";
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                    UndertaleIO.Write(stream, Data);

                if (File.Exists(DataPath))
                    File.Delete(DataPath);
                File.Move(tempFilePath, DataPath);
            }
            catch (Exception ex)
            {
                errorDelegate($"Произошла ошибка при сохранении файла игровых данных - {ex}");
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);

                return false;
            }
            #endregion

            return true;
        });

        return res;
    }
    private static bool InstallToXBOXTALEPart(MsgDelegate msgDelegate, Action<string> errorDelegate, Action<string> warnDelegate)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!ReplaceXBOXTALEExe)
                return true;

            BackupResult backupRes = MakeExecutableBackup(msgDelegate, errorDelegate, warnDelegate);
            if (backupRes == BackupResult.Error)
            {
                bool proceed = MainWindow.ScriptQuestion("Исполняемый файл игры не будет заменён, так как не получилось сделать его резервную копию.\n" +
                                                         "Всё равно устанавливать русификатор?");
                if (!proceed)
                    return false;
            }
            if (!ReplaceExecutable(backupRes, msgDelegate, errorDelegate))
            {
                bool proceed = MainWindow.ScriptQuestion("Не получилось заменить исполняемый файл игры.\nВсё равно продолжить установку?");
                if (!proceed)
                    return false;
            }
        }

        // TODO: find out what flags are not necessary, and
        // should it replace them only for MacOS full screen support?
        #region Flags
        Data.GeneralInfo.Info = InfoFlags.SyncVertex1 | InfoFlags.Scale | InfoFlags.ShowCursor | InfoFlags.ScreenKey
                                | InfoFlags.SyncVertex3 | InfoFlags.StudioVersionB3;

        Data.GeneralInfo.FunctionClassifications = FuncClassif.Joystick | FuncClassif.Gamepad | FuncClassif.Screengrab
                                                   | FuncClassif.Math | FuncClassif.Action | FuncClassif.MatrixD3D
                                                   | FuncClassif.DataStructures | FuncClassif.File | FuncClassif.INI
                                                   | FuncClassif.Directory | FuncClassif.Encoding | FuncClassif.UIDialog
                                                   | FuncClassif.MotionPlanning | FuncClassif.ShapeCollision | FuncClassif.Instance 
                                                   | FuncClassif.Room | FuncClassif.Game | FuncClassif.Window | FuncClassif.DrawColor 
                                                   | FuncClassif.Texture | FuncClassif.String | FuncClassif.Tiles
                                                   | FuncClassif.Surface | FuncClassif.IO | FuncClassif.Variables 
                                                   | FuncClassif.Array | FuncClassif.Date | FuncClassif.Sprite | FuncClassif.Audio 
                                                   | FuncClassif.Event | FuncClassif.FreeType | FuncClassif.OS | FuncClassif.Console
                                                   | FuncClassif.Buffer | FuncClassif.Steam;

        Data.Options.Info = OptionsFlags.UseNewAudio | OptionsFlags.ScreenKey | OptionsFlags.QuitKey | OptionsFlags.SaveKey
                            | OptionsFlags.ScreenShotKey | OptionsFlags.CloseSec | OptionsFlags.ScaleProgress
                            | OptionsFlags.DisplayErrors | OptionsFlags.VariableErrors | OptionsFlags.CreationEventOrder;
        #endregion

        return true;
    }
    private static BackupResult MakeExecutableBackup(MsgDelegate msgDelegate, Action<string> errorDelegate, Action<string> warnDelegate)
    {
        msgDelegate("Создание резервной копии исполняемого файла игры...", true);

        if (gameDirLocation is null)
        {
            warnDelegate("Внимание - не удалось найти путь папки с игрой.");
            return BackupResult.Error;
        }

        string exePath = gameDirLocation + xboxtaleExePath.Path;
        if (!File.Exists(exePath))
        {
            warnDelegate("Внимание - не найден исполняемый файл игры.");
            return BackupResult.SourceNotFound;
        }

        string exeName = Path.GetFileName(exePath);
        string backupFolder = GetFolder(exePath) + "backup";
        string backupPath = Path.Combine(backupFolder, exeName);
        if (File.Exists(backupPath))
        {
            bool rewrite = MainWindow.ScriptQuestion($"Резервная копия уже существует (путь - \"{backupPath}\").\n" +
                                                     "Заменить её?");
            if (rewrite)
            {
                try
                {
                    File.Delete(backupPath);
                }
                catch (Exception ex)
                {
                    errorDelegate($"Не удалось удалить существующую резервную копию.\nОшибка - {ex.Message}");
                    return BackupResult.Error;
                }
            }
            else
                return BackupResult.Error;
        }
        else
        {
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);
        }

        try
        {
            File.Copy(exePath, backupPath);
        }
        catch (Exception ex)
        {
            errorDelegate($"Не удалось создать резервную копию.\nОшибка - {ex.Message}");
            return BackupResult.Error;
        }
        msgDelegate($"Резервная копия исполняемого файла игры создана, путь - \"{backupPath}\"\n", false);

        return BackupResult.Success;
    }
    private static bool ReplaceExecutable(BackupResult backupRes, MsgDelegate msgDelegate, Action<string> errorDelegate)
    {
        if (backupRes == BackupResult.Success)
            msgDelegate("Замена исполняемого файла игры...", true);
        else if (backupRes == BackupResult.SourceNotFound)
            msgDelegate("Копирование исполняемого файла игры...", true);
        else
            return true;

        string exePath = gameDirLocation + xboxtaleExePath.Path;
        string srcExePath = Path.Combine(NewDataDirPath, xboxtaleExePath.Name);
        if (!File.Exists(srcExePath))
        {
            errorDelegate($"Ошибка - не найдена замена для исполняемого файла игры, путь - \"{srcExePath}\".");
            return false;
        }

        try
        {
            if (File.Exists(exePath))
                File.Delete(exePath);
            File.Copy(srcExePath, exePath);
        }
        catch (Exception ex)
        {
            MainWindow.ScriptMessage($"Произошла ошибка при замене исполняемого файла игры - {ex}");
            return false;
        }

        return true;
    }

    private static void FontUpdate(string sourcePath, ref UndertaleFont newFont)
    {
        using (StreamReader reader = new(sourcePath + "glyphs_" + newFont.Name.Content + ".csv"))
        {
            newFont.Glyphs.Clear();
            string line;
            int head = 0;
            while ((line = reader.ReadLine()) != null)
            {
                string[] s = line.Split(';');

                if (head == 1)
                {
                    newFont.RangeStart = UInt16.Parse(s[0]);
                    head++;
                }

                if (head == 0)
                {
                    string name = s[0].Replace("\"", "");
                    newFont.DisplayName = Data.Strings.MakeString(name);
                    newFont.EmSize = UInt16.Parse(s[1]);
                    newFont.Bold = Boolean.Parse(s[2]);
                    newFont.Italic = Boolean.Parse(s[3]);
                    newFont.Charset = Byte.Parse(s[4]);
                    newFont.AntiAliasing = Byte.Parse(s[5]);
                    newFont.ScaleX = UInt16.Parse(s[6]);
                    newFont.ScaleY = UInt16.Parse(s[7]);
                    head++;
                }

                if (head > 1)
                {
                    newFont.Glyphs.Add(new UndertaleFont.Glyph()
                    {
                        Character = UInt16.Parse(s[0]),
                        SourceX = UInt16.Parse(s[1]),
                        SourceY = UInt16.Parse(s[2]),
                        SourceWidth = UInt16.Parse(s[3]),
                        SourceHeight = UInt16.Parse(s[4]),
                        Shift = Int16.Parse(s[5]),
                        Offset = Int16.Parse(s[6]),
                    });
                    newFont.RangeEnd = UInt32.Parse(s[0]);
                }
            }
        }
    }
    private static void SetTextureTargetBounds(ref UndertaleTexturePageItem tex, Node n)
    {
        tex.TargetX = 0;
        tex.TargetY = 0;
        tex.TargetWidth = (ushort)n.Bounds.Width;
        tex.TargetHeight = (ushort)n.Bounds.Height;
    }
    private enum SpriteType
    {
        Sprite,
        Background,
        Font,
        Unknown
    }
    private static SpriteType GetSpriteType(string path)
    {
        string folderPath = Path.GetDirectoryName(path);
        string folderName = new DirectoryInfo(folderPath).Name;
        string lowerName = folderName.ToLower();

        if (lowerName == "backgrounds" || lowerName == "background")
            return SpriteType.Background;
        else if (lowerName == "fonts" || lowerName == "font")
            return SpriteType.Font;
        else if (lowerName == "sprites" || lowerName == "sprite")
            return SpriteType.Sprite;

        return SpriteType.Unknown;
    }
}
