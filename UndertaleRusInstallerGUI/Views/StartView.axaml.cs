using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using static UndertaleRusInstallerGUI.OSMethods;

namespace UndertaleRusInstallerGUI.Views;

public partial class StartView : UserControl
{
    private readonly MainWindow mainWindow;

    public StartView() // For the designer preview
    {
        InitializeComponent();
    }
    public StartView(MainWindow mainWindow)
    {
        InitializeComponent();

        this.mainWindow = mainWindow;
    }

    private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (mainWindow is not null)
        {
            mainWindow.ChangeNextButtonState(true);
            mainWindow.ChangeCopyrightState(false);
        }
    }

    private void NXTLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Content is not string url)
            return;

        if (!OpenUrl(url))
        {
            mainWindow.ScriptError("Не удалось открыть ссылку.\n" +
                                   "Вы можете скопировать ссылку (нажав по ней правой кнопкой мыши) и перейти по ней самостоятельно.");
        }
    }
    private void NXTLinkButton_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (sender is not Button button
            || button.Content is not string url
            || mainWindow is null)
            return;

        if (mainWindow.ScriptQuestion("Скопировать ссылку в буфер обмена?"))
            mainWindow.Clipboard.SetTextAsync(url);
    }
}
