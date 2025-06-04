using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace UndertaleRusInstallerGUI.Views;

public partial class MainWindow : Window
{
    public UserControl[] Parts { get; set; }
    private short currPartIndex = 0;
    private readonly short lastPartIndex = -1;
    private readonly Thickness copyrightMargin = new(0, 0, 5, 0);
    private ushort? tempGoBackAmount = null;
    private ushort? tempGoNextAmount = null;

    public MainWindow()
    {
        Parts = new UserControl[]
        {
            new StartView(this),
            new ChooseGameView(this),
            new ChooseDataFileView(this),
            new ChooseArchivePathView(this),
            new LoadGameView(this),
            new ConfirmInstallView(this),
            new InstallModView(this),
            new FinishView(this)
        };
        lastPartIndex = (short)(Parts.Length - 1);

        InitializeComponent();

        Core.MainWindow = this;

        bool hasError = CheckForGDIError(ref lastPartIndex);
    }
    

    public void ChangeBackButtonState(bool state, ushort amount = 1)
    {
        BackButton.IsEnabled = state;
        if (amount != 1)
            tempGoBackAmount = amount;
    }
    public void ChangeNextButtonState(bool state, ushort amount = 1)
    {
        NextButton.IsEnabled = state;
        if (amount != 1)
            tempGoNextAmount = amount;
    }
    public void ChangeCopyrightState(bool state)
    {
        CopyrightText.IsVisible = state;
        SeparatorRect.Margin = state ? copyrightMargin : default;
    }
    public void GoBack(ushort amount = 1)
    {
        if (currPartIndex == 0)
            return;

        if (tempGoBackAmount is not null)
        {
            amount = (ushort)tempGoBackAmount;
            tempGoBackAmount = null;
        }

        currPartIndex -= (short)amount;
        if (Parts[currPartIndex] is LoadGameView)
            currPartIndex--; // Skip "LoadGameView"

        RefreshCurrentPart();
    }
    public void GoForward(ushort amount = 1)
    {
        if (NextButton.Classes.Contains("finish"))
        {
            OnFinish();
            return;
        }

        if (currPartIndex == lastPartIndex)
            return;

        if (tempGoNextAmount is not null)
        {
            amount = (ushort)tempGoNextAmount;
            tempGoNextAmount = null;
        }

        currPartIndex += (short)amount;

        RefreshCurrentPart();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        GoBack();
    }
    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        GoForward();
    }
    private void NextButton_Click_GDIError(object sender, RoutedEventArgs e)
    {
        currPartIndex = lastPartIndex;
        RefreshCurrentPart();

        NextButton.Click -= NextButton_Click_GDIError;
        NextButton.Click += NextButton_Click;
    }

    private void RefreshCurrentPart()
    {
        CurrentPartControl.Content = Parts[currPartIndex];
        if (Parts[currPartIndex] is ConfirmInstallView)
            NextButton.Classes.Add("install");
        else
            NextButton.Classes.Remove("install");

        if (currPartIndex == lastPartIndex)
        {
            BackButton.IsVisible = false;
            CancelButton.IsEnabled = false;
            NextButton.Classes.Add("finish");
            return;
        }

        BackButton.IsVisible = currPartIndex != 0;
    }

    /// <summary>
    /// Checks if "libgdiplus" is available on the system.
    /// </summary>
    /// <param name="lastPartIndex"></param>
    /// <returns><see langword="true" /> if there is an error (no "libgdiplus").</returns>
    private bool CheckForGDIError(ref short lastPartIndex)
    {
        void ActivateGDIErrorPart(ref short lastPartIndex, GDIErrorType errorType)
        {
            Parts = Parts.Append(new GDIErrorView(this, errorType)).ToArray();
            lastPartIndex++;

            NextButton.Click -= NextButton_Click;
            NextButton.Click += NextButton_Click_GDIError;
        }

        try
        {
            // Only Linux at the time.
            // TODO: Implement all of this for MacOS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                /// If "libfontconfig" is installed, then the bundled version can conflict with the current config file,
                /// which leads to a lot of "fontconfig" warnings.
                /// So we "discard" it by renaming it before the "libgdiplus" loads.
                /// If it was already "discarded", then we shouldn't do anything.

                string libfontconfigDiscarded = $"(discarded){OSMethods.libfontconfig}";
                string destPath = Path.Combine(Core.CurrDirPath, libfontconfigDiscarded);
                if (!File.Exists(destPath))
                {
                    string srcPath = Path.Combine(Core.CurrDirPath, OSMethods.libfontconfig);
                    if (File.Exists(srcPath))
                    {
                        if (OSMethods.IsUNIXLibraryInstalled(OSMethods.libfontconfig) == true)
                        {
                            File.Move(srcPath, destPath);

                            OSMethods.Replace_libfontconfig_name(libfontconfigDiscarded);
                        }
                    }
                }
            }

            // An empty 10x10 PNG image (152 bytes)
            byte[] samplePNGBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv" +
                                                             "8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAPSURBVChTYxgFgxIwMAAAAZoAAXcn7CgAAAAASUVORK5CYII=");
            using MemoryStream ms = new(samplePNGBytes);
            using System.Drawing.Image img = System.Drawing.Image.FromStream(ms);

            return false;
        }
        catch (Exception ex)
        {
            if (ex is ArgumentException or OutOfMemoryException) // The current "libgdiplus" version is broken
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ScriptMessage($"Произошла непредвиденная ошибка при проверке наличия и доступности библиотеки \"System.Drawing\": {ex.Message}.\n" +
                                  "Вы можете попытаться продолжить, но, скорее всего, будет ошибка.\n" +
                                  "Сообщите об этой ошибке автору русификатора.");

                    return false;
                }

                GDIErrorType reason;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string libgdiplus = "libgdiplus.so";
                    string libgdiplusPath = Path.Combine(Core.CurrDirPath, libgdiplus);
                    reason = File.Exists(libgdiplus)
                             ? GDIErrorType.BundledIsBroken
                             : GDIErrorType.InstalledIsBroken;
                }
                else
                {
                    // MacOS
                    reason = GDIErrorType.BundledIsBroken;
                }
                ActivateGDIErrorPart(ref lastPartIndex, reason);

                return true;
            }

            // Some obscure error related to single file publishing or trimming for Windows
            // (the installer still works correctly)
            if (ex is TypeLoadException && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            string excText = ex.ToString();
            if (excText.Contains("libgdiplus")) // (A compatible version of) "libgdiplus" is missing
            {
                ActivateGDIErrorPart(ref lastPartIndex, GDIErrorType.CompatibleIsMissing);

                return true;
            }
            else
            {
                string libgdiplus = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                    ? "System.Drawing"
                                    : "libgdiplus";
                ScriptMessage($"Произошла непредвиденная ошибка при проверке наличия и доступности библиотеки \"{libgdiplus}\":\n{ex.Message}\n\n" +
                              "Вы можете попытаться продолжить, но, скорее всего, будет ошибка.\n" +
                              "Сообщите об этой ошибке автору русификатора.");
            }

            return false;
        }
    }

    private bool OnCancel()
    {
        if (ScriptQuestion("Вы действительно хотите отменить установку русификатора?"))
        {
            Closing -= Window_Closing;
            Close();
            return true;
        }

        return false;
    }
    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        e.Cancel = true;

        if (OnCancel())
            e.Cancel = false;
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        OnCancel();
    }
    
    private void OnFinish()
    {
        Closing -= Window_Closing;
        Close();
    }
}
