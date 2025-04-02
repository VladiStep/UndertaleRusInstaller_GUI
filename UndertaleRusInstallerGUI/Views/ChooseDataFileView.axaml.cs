using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static UndertaleRusInstallerGUI.Core;

namespace UndertaleRusInstallerGUI.Views
{
    public partial class ChooseDataFileView : UserControl
    {
        private static readonly bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private readonly MainWindow mainWindow;
        private GameType lastSelectedGame;
        private bool cleanResultText = true;
        private bool ignoreTextChange = false;

        public ChooseDataFileView() // For the designer preview
        {
            InitializeComponent();
        }
        public ChooseDataFileView(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
        }

        private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (lastSelectedGame != SelectedGame)
            {
                if (SelectedGame == GameType.Undertale)
                {
                    string dataPath = GetDefaultUTFilePath(); // + gameDirLocation
                    if (dataPath is null)
                        ChangeResultText("Не удалось найти файл данных Undertale, выберите его вручную.");
                    else
                        ChangeResultText("Файл данных Undertale найден.\n" +
                                         "Если вы хотите установить русификатор в другую копию Undertale, то выберите другой путь.");

                    cleanResultText = false;
                    ChangeDataPathText(dataPath);
                    cleanResultText = true;
                }
                else
                {
                    ChangeResultText(null);

                    if (DataPathBox.Text is null)
                        DataPathBox_TextChanging(null, null);
                    else
                        ChangeDataPathText(null);
                }
            }
            else
            {
                cleanResultText = false;
                DataPathBox_TextChanging(null, null);
                cleanResultText = true;
            }

            lastSelectedGame = SelectedGame;
            DataPathHeader.Header = $"Путь файла данных {SelectedGame}";
        }
        private void UserControl_DetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            DataPath = ProcessDataPath(DataPathBox.Text);
            ZipIsValid = true;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string dataPath = ChooseDataPath();
            if (String.IsNullOrEmpty(dataPath))
                return;

            ChangeDataPathText(dataPath);
        }

        
        private void ChangeResultText(string text)
        {
            CheckResultText.IsVisible = !String.IsNullOrEmpty(text);
            CheckResultText.Text = text;
        }
        private void ChangeDataPathText(string text, bool triggerEvent = true)
        {
            if (!triggerEvent)
                DataPathBox.TextChanging -= DataPathBox_TextChanging;

            if (text == DataPathBox.Text)
                DataPathBox_TextChanging(null, null);
            else
                DataPathBox.Text = text;

            if (!triggerEvent)
                DataPathBox.TextChanging += DataPathBox_TextChanging;
        }
        private void DataPathBox_TextChanging(object sender, TextChangingEventArgs e)
        {
            if (mainWindow is null || ignoreTextChange)
                return;

            string dataPath = DataPathBox.Text;
            bool nextButtonState = IsDataPathValid(dataPath);
            if (!nextButtonState && isMacOS) // e.g. "../UNDERTALE.app/"
            {
                dataPath = ProcessDataPath(dataPath);

                nextButtonState = IsDataPathValid(dataPath);
                if (nextButtonState)
                {
                    ignoreTextChange = true;
                    DataPathBox.Text = dataPath;
                    ignoreTextChange = false;
                }
            }
            mainWindow.ChangeNextButtonState(nextButtonState);

            if (cleanResultText)
                ChangeResultText(null);
        }
    }
}
