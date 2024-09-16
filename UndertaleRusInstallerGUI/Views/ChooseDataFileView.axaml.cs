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
        private readonly MainWindow mainWindow;
        private GameType lastSelectedGame;
        private bool cleanResultText = true;

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
            DataPath = DataPathBox.Text;
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
            if (mainWindow is null)
                return;

            bool nextButtonState = IsDataPathValid(DataPathBox.Text);
            mainWindow.ChangeNextButtonState(nextButtonState);

            if (cleanResultText)
                ChangeResultText(null);
        }
    }
}
