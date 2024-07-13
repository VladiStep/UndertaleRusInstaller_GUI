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
using UndertaleModLib.Scripting;
using static UndertaleRusInstallerGUI.Core;

namespace UndertaleRusInstallerGUI.Views
{
    public partial class InstallModView : UserControl
    {
        private readonly MainWindow mainWindow;
        private bool isFirstLine;
        private static readonly ImmutableSolidColorBrush warningBrush = new(Colors.Orange);
        private static readonly ImmutableSolidColorBrush errorBrush = new(Colors.OrangeRed);

        public InstallModView() // For the designer preview
        {
            InitializeComponent();
        }
        public InstallModView(MainWindow mainWindow)
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

            InstallProgressBar.Value = 0;
            InstallProgressBar.Maximum = 100;
            InstallLogText.Inlines.Clear();
            isFirstLine = true;

            try
            {
                await ExtractNewData(OnInstallMessage);
            }
            catch (ScriptException scrEx)
            {
                mainWindow.ChangeBackButtonState(true, 3); // 3 = go straight to the archive path choosing stage
                OnInstallError(scrEx.Message, false);
                mainWindow.ScriptError("В процессе распаковки архива с данными возникла ошибка.\n" +
                                       "Текст ошибки смотрите в журнале загрузки.");
                ZipIsValid = false;
                return;
            }
            catch (Exception ex)
            {
                mainWindow.ChangeBackButtonState(true, 3); // 3 = go straight to the archive path choosing stage
                OnInstallError($"В процессе распаковки архива с данными возникла ошибка:\n{ex}", false);
                mainWindow.ScriptError("В процессе распаковки архива с данными возникла ошибка.\n" +
                                       "Текст ошибки смотрите в журнале загрузки.");
                ZipIsValid = false;
                return;
            }

            if (!await MakeDataBackup(OnInstallMessage, (text) => OnInstallError(text, false)))
            {
                mainWindow.ChangeBackButtonState(true);
                OnInstallMessage("Резервная копия не была сделана, установка прервана.", false);
                mainWindow.ScriptError("Резервная копия не была сделана, подробности смотрите в журнале загрузки.");
                return;
            }

            bool installed = false;
            try
            {
                installed = await InstallMod(OnInstallMessage, (text) => OnInstallError(text, false), OnInstallWarning,
                                             SetProgressStatus, (max) => { SetupProgress(0, max); },
                                             IncrementProgressValue);
            }
            catch (ScriptException scrEx)
            {
                mainWindow.ChangeBackButtonState(true);
                OnInstallError(scrEx.Message, false);
                mainWindow.ScriptError("В процессе установки возникла ошибка.\n" +
                                       "Текст ошибки смотрите в журнале загрузки.");
                return;
            }
            catch (Exception ex)
            {
                mainWindow.ChangeBackButtonState(true);
                OnInstallError($"В процессе установки возникла ошибка:\n{ex}", false);
                mainWindow.ScriptError("В процессе установки возникла ошибка.\n" +
                                       "Текст ошибки смотрите в журнале загрузки.");
                return;
            }

            if (!installed)
            {
                mainWindow.ChangeBackButtonState(true);
                OnInstallError("Установка прервана.", false);
                return;
            }

            try
            {
                await DeleteTempData(OnInstallMessage);
            }
            catch
            {
                OnInstallError("Не удалось удалить остаточные файлы.\n" + 
                               $"Попробуйте удалить их самостоятельно, путь папки - \"{TempDirPath}\".", false);
            }

            OnInstallMessage("Установка завершена.", false);
            mainWindow.ChangeNextButtonState(true);
        }

        private void OnInstallMessage(string text, bool setStatus)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Run run = new(isFirstLine ? text : '\n' + text);
                InstallLogText.Inlines.Add(run);

                InstallLogScroll.ScrollToEnd();
                InstallProgressText.Text = setStatus ? text : "-";
                InstallProgressValText.Text = null;
                InstallProgressBar.IsIndeterminate = setStatus;

                isFirstLine = false;
            }, DispatcherPriority.Render);
        }
        private void OnInstallWarning(string text)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Run run = new()
                {
                    Foreground = warningBrush,
                    Text = isFirstLine ? text : '\n' + text
                };
                InstallLogText.Inlines.Add(run);

                InstallLogScroll.ScrollToEnd();

                isFirstLine = false;
            }, DispatcherPriority.Render);
        }
        private void OnInstallError(string text, bool scrollToEnd = true)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Run run = new()
                {
                    Foreground = errorBrush,
                    Text = isFirstLine ? text : '\n' + text
                };
                InstallLogText.Inlines.Add(run);
                
                if (scrollToEnd)
                    InstallLogScroll.ScrollToEnd();
                else
                    InstallLogScroll.Offset += new Vector(0, 12 * 10);

                InstallProgressBar.Value = 0;
                InstallProgressText.Text = "-";
                InstallProgressValText.Text = null;

                isFirstLine = false;
            }, DispatcherPriority.Render);
        }

        private void IncrementProgressValue()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                InstallProgressBar.Value++;
                InstallProgressValText.Text = $"{InstallProgressBar.Value}/{InstallProgressBar.Maximum}";
            }, DispatcherPriority.Render);
        }
        private void SetProgressValue(double value)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                InstallProgressBar.Value = value;
                InstallProgressValText.Text = $"{InstallProgressBar.Value}/{InstallProgressBar.Maximum}";
            }, DispatcherPriority.Render);
        }
        private void SetProgressStatus(string text)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                InstallProgressText.Text = text;
                InstallProgressValText.Text = $"{InstallProgressBar.Value}/{InstallProgressBar.Maximum}";
            }, DispatcherPriority.Render);
        }
        private void SetupProgress(double value, double max)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                InstallProgressBar.Value = value;

                if (max == 0)
                {
                    InstallProgressBar.Maximum = 100;
                    InstallProgressBar.IsIndeterminate = true;
                }
                else
                {
                    InstallProgressBar.Maximum = max;
                    InstallProgressBar.IsIndeterminate = false;
                }
            }, DispatcherPriority.Render);
        }

        private void ScrollViewer_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            // Ported from "SelectableTextBlock.cs"

            if (InstallLogText.Text?.Length == 0)
                return;

            PointerPoint pointer = e.GetCurrentPoint(InstallLogText);
            if (!pointer.Properties.IsLeftButtonPressed)
                return;

            Thickness padding = InstallLogText.Padding;
            Point point = e.GetPosition(InstallLogText) - new Point(padding.Left, padding.Top);
            int textPos = InstallLogText.TextLayout.HitTestPoint(in point).TextPosition;
            InstallLogText.SelectionStart = textPos;
            InstallLogText.SelectionEnd = textPos;

            e.Pointer.Capture(InstallLogText);
        }
        private void ScrollViewer_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
           if (e.Pointer.Captured == InstallLogText)
                e.Pointer.Capture(null);
        }
    }
}
