using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Threading.Tasks;
using static UndertaleRusInstallerGUI.Core;

namespace UndertaleRusInstallerGUI.Views
{
    public partial class LoadGameView : UserControl
    {
        private readonly MainWindow mainWindow;
        private bool isFirstLine = true;
        private static readonly ImmutableSolidColorBrush warningBrush = new(Colors.Orange);
        private static readonly ImmutableSolidColorBrush errorBrush = new(Colors.OrangeRed);

        public LoadGameView() // For the designer preview
        {
            InitializeComponent();
        }
        public LoadGameView(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
        }

        private async void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (mainWindow is null)
                return;

            mainWindow.ChangeBackButtonState(false);
            mainWindow.ChangeNextButtonState(false);

            LoadingLogText.Inlines.Clear();
            isFirstLine = true;

            try
            {
                await LoadDataFile(OnLoadingWarning, OnLoadingMessage);
            }
            catch (Exception ex)
            {
                OnLoadingError(ex.ToString(), false);
                // The message is already displayed in "LoadDataFile()"

                return;
            }

            mainWindow.ChangeBackButtonState(true);

            GameType gameType = CheckSelectedDataFile();
            if (gameType == GameType.None)
            {
                bool ignore = mainWindow.ScriptQuestion("Кажется, что загруженная игра - это не Undertale v1.08 и не NXTale.\n" +
                                                        "Всё равно продолжить?");
                if (!ignore)
                    mainWindow.GoBack(2); // Skip "ChooseArchivePathView"
                else
                    mainWindow.GoForward();    
            }
            else if (gameType != SelectedGame)
            {
                mainWindow.ScriptError($"Загруженная игра - это не {SelectedGame}.\n" +
                                       "Выберите другой файл.");
                mainWindow.GoBack(2); // Skip "ChooseArchivePathView"
                return;
            }
            else
                mainWindow.GoForward();
        }

        private string TranslateLoadingMessage(string line)
        {
            if (line.StartsWith("Counting objects"))
                return "Подсчёт объектов блока данных " + line[^4..];
            else if (line.StartsWith("Reading chunk"))
                return "Чтение блока данных " + line[^4..];
            else if (line == "Resolving resource IDs...")
                return "Преобразование идентификаторов ресурсов...";
            else
                return line;
        }
        private void OnLoadingMessage(string text)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                string translatedLine = TranslateLoadingMessage(text);
                Run run = new(isFirstLine ? translatedLine : '\n' + translatedLine);
                LoadingLogText.Inlines.Add(run);

                LoadingLogScroll.ScrollToEnd();

                isFirstLine = false;
            }, DispatcherPriority.Render);
        }
        private void OnLoadingWarning(string text)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Run run = new()
                {
                    Foreground = warningBrush,
                    Text = isFirstLine ? text : '\n' + text
                };
                LoadingLogText.Inlines.Add(run);

                LoadingLogScroll.ScrollToEnd();

                isFirstLine = false;
            }, DispatcherPriority.Render);
        }
        private void OnLoadingError(string text, bool scrollToEnd = true)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Run run = new()
                {
                    Foreground = errorBrush,
                    Text = isFirstLine ? text : '\n' + text
                };
                LoadingLogText.Inlines.Add(run);
                
                if (scrollToEnd)
                    LoadingLogScroll.ScrollToEnd();
                else
                    LoadingLogScroll.Offset += new Vector(0, 12 * 10);

                isFirstLine = false;
            }, DispatcherPriority.Render);
        }

        private void ScrollViewer_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            // Ported from "SelectableTextBlock.cs"

            if (LoadingLogText.Text?.Length == 0)
                return;

            PointerPoint pointer = e.GetCurrentPoint(LoadingLogText);
            if (!pointer.Properties.IsLeftButtonPressed)
                return;

            Thickness padding = LoadingLogText.Padding;
            Point point = e.GetPosition(LoadingLogText) - new Point(padding.Left, padding.Top);
            int textPos = LoadingLogText.TextLayout.HitTestPoint(in point).TextPosition;
            LoadingLogText.SelectionStart = textPos;
            LoadingLogText.SelectionEnd = textPos;

            e.Pointer.Capture(LoadingLogText);
        }
        private void ScrollViewer_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
           if (e.Pointer.Captured == LoadingLogText)
                e.Pointer.Capture(null);
        }
    }
}
