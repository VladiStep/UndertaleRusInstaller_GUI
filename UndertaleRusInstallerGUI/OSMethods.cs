using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UndertaleRusInstallerGUI.Views;

namespace UndertaleRusInstallerGUI
{
    public static class OSMethods
    {
        public const string libfontconfig = "libfontconfig.so.1";
        private static string ldconfigOutput = null;
        private static readonly HashSet<string> libgdiplusFiles = new()
        {
            "libjbig.so.0", "libjpeg.so.8", "libpixman-1.so.0", "libpng12.so.0", "libtiff.so.5", "libcairo.so.2",
            "libexif.so.12", "libfreetype.so.6", "libgdiplus.so", "libgif.so.7", "libglib-2.0.so.0", libfontconfig
        };


        // Windows
        [DllImport("tinyfiledialogs", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tinyfd_messageBoxW(string aTitle, string aMessage, string aDialogType, string aIconType, int aDefaultButton);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_openFileDialogW(string aTitle, string aDefaultPathAndFile, int aNumOfFilterPatterns, string[] aFilterPatterns, string aSingleFilterDescription, int aAllowMultipleSelects);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_selectFolderDialogW(string aTitle, string aDefaultPath = "");

        // Linux/MacOS
        [DllImport("tinyfiledialogs", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tinyfd_messageBox(string aTitle, string aMessage, string aDialogType, string aIconType, int aDefaultButton);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_openFileDialog(string aTitle, string aDefaultPathAndFile, int aNumOfFilterPatterns, string[] aFilterPatterns, string aSingleFilterDescription, int aAllowMultipleSelects);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_selectFolderDialog(string aTitle, string aDefaultPath = "");

        public static int MessageBox(string title, string message, string dialogType, string iconType, int defaultButton)
        {
            message = message.Replace('"', '“').Replace('\'', '‘').Replace('`', '‘');

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return tinyfd_messageBoxW(title, message, dialogType, iconType, defaultButton);
            }
            else
            {
                message = message.Replace("\\", "\\\\\\\\");
                return tinyfd_messageBox(title, message, dialogType, iconType, defaultButton);
            }
        }

        public static string OpenFileDialog(string title, string defaultPathAndFile, int numOfFilterPatterns, string[] filterPatterns, string singleFilterDescription, int allowMultipleSelects)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Marshal.PtrToStringUni(tinyfd_openFileDialogW(title, defaultPathAndFile, numOfFilterPatterns, filterPatterns, singleFilterDescription, allowMultipleSelects));
            }
            else
            {
                return Marshal.PtrToStringAnsi(tinyfd_openFileDialog(title, defaultPathAndFile, numOfFilterPatterns, filterPatterns, singleFilterDescription, allowMultipleSelects));
            }
        }

        public static string SelectFolderDialog(string title, string defaultPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Marshal.PtrToStringUni(tinyfd_selectFolderDialogW(title, defaultPath));
            }
            else
            {
                return Marshal.PtrToStringAnsi(tinyfd_selectFolderDialog(title, defaultPath));
            }
        }


        public static bool OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                try
                {
                    // hack because of this: https://github.com/dotnet/corefx/issues/10361
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        url = url.Replace("&", "^&");
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start("xdg-open", url);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Process.Start("open", url);
                    }
                    else
                    {
                        throw;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        public static bool? IsUNIXLibraryInstalled(string libraryName, bool dontShowWarning = false, MainWindow mainWindow = null)
        {
            if (ldconfigOutput is not null)
                return ldconfigOutput.Contains(libraryName, StringComparison.OrdinalIgnoreCase);

            try
            {
                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ldconfig",
                        Arguments = "-p",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                ldconfigOutput = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // If "ldconfig" failed
                if (process.ExitCode != 0)
                {
                    ldconfigOutput = null;
                    return null;
                }

                return ldconfigOutput.Contains(libraryName, StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception ex)
            {
                ldconfigOutput = null;

                if (!dontShowWarning)
                    mainWindow?.ScriptMessage($"Произошла ошибка при проверке наличия библиотеки \"{libraryName}\":\n{ex.Message}\n\n" +
                                              "Возможно, это ни на что не повлияет.");

                return null; // Error occurred
            }
        }

        public static void Discard_libgdiplus_Files(MainWindow mainWindow)
        {
            // Only Linux at the moment
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

            try
            {
                string destDir = Path.Combine(Core.CurrDirPath, "discarded_libgdiplus_files");
                Directory.CreateDirectory(destDir);

                var filePaths = Directory.EnumerateFiles(Core.CurrDirPath, "*lib*.so*");
                foreach (string filePath in filePaths)
                {
                    string fileName = filePath.Split('/')[^1];
                    if (fileName.Length == 0) continue;

                    if (libgdiplusFiles.Contains(fileName))
                    {
                        string destPath = Path.Combine(destDir, fileName);
                        File.Move(filePath, destPath, overwrite: true);

                        libgdiplusFiles.Remove(fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                mainWindow.ScriptMessage($"Произошла ошибка при перемещении файлов \"libgdiplus\" в папку \"discarded_libgdiplus_files\":\n{ex.Message}\n\n" +
                                         "Закройте программу и переместите перечисленные файлы вручную:\n" +
                                         String.Join(", ", libgdiplusFiles));
            }
        }
        public static void Replace_libfontconfig_name(string newName)
        {
            libgdiplusFiles.Remove(libfontconfig);
            libgdiplusFiles.Add(newName);
        }
    }
}
