using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using static UndertaleRusInstallerGUI.Core;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UndertaleRusInstallerGUI.Views
{
    public partial class ChooseArchivePathView : UserControl
    {
        private readonly MainWindow mainWindow;

        public ChooseArchivePathView() // For the designer preview
        {
            InitializeComponent();
        }
        public ChooseArchivePathView(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
        }

        private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (ZipPath is null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ChangeResultText("Из-за ограничений MacOS невозможно автоматически получить путь к архиву с данными русификатора," +
                                     "поэтому вам нужно выбрать его самостоятельно.");

                    ZipIsValid = false;
                    mainWindow.ChangeNextButtonState(false);

                    return;
                }

                ZipPath = Path.Combine(CurrDirPath, ZipName);
            }

            // Can be `false` only on an extraction error
            if (!ZipIsValid)
            {
                ChangeResultText("Архив с данными русификатора повреждён.\n" +
                                 "Попробуйте скачать/распаковать архив с установщиком заново, либо выберите другой путь.");
            }
            else
            {
                FileStatus zipStatus = IsZipPathValid(ZipPath, false);
                if (zipStatus == FileStatus.NotFound)
                {
                    ChangeResultText("Архив с данными русификатора не найден, выберите путь вручную.");
                }
                else if (zipStatus == FileStatus.Empty)
                {
                    ChangeResultText("Архив с данными русификатора пуст.\n" +
                                     "Попробуйте скачать/распаковать архив с установщиком заново, либо выберите другой путь.");
                }
                else
                {
                    ChangeResultText("Архив с данными русификатора найден, нажмите \"Далее\".");
                }

                ChangeZipPathText(ZipPath, false);
            }

            mainWindow.ChangeNextButtonState(ZipIsValid);
        }
        private void UserControl_DetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            ZipPath = ZipPathBox.Text;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string zipPath = ChooseZipPath();
            if (String.IsNullOrEmpty(zipPath))
                return;

            ChangeZipPathText(zipPath);
        }


        private void ChangeResultText(string text)
        {
            CheckResultText.IsVisible = !String.IsNullOrEmpty(text);
            CheckResultText.Text = text;
        }
        private void ChangeZipPathText(string text, bool triggerEvent = true)
        {
            if (!triggerEvent)
                ZipPathBox.TextChanging -= ZipPathBox_TextChanging;

            ZipPathBox.Text = text;

            if (!triggerEvent)
                ZipPathBox.TextChanging += ZipPathBox_TextChanging;
        }
        private void ZipPathBox_TextChanging(object sender, TextChangingEventArgs e)
        {
            if (mainWindow is null)
                return;

            _ = IsZipPathValid(ZipPathBox.Text); // Changes "ZipIsValid"
            mainWindow.ChangeNextButtonState(ZipIsValid);

            ChangeResultText(null);
        }
    }
}
