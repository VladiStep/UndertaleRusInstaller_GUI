using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using static UndertaleRusInstallerGUI.OSMethods;

namespace UndertaleRusInstallerGUI.Views;

public partial class StartView : UserControl
{
    private readonly MainWindow mainWindow;
    private const string floweyPathPrefix = "avares://UndertaleRusInstallerGUI/Assets/flowey/";
    private readonly Bitmap[] floweyFrames;
    private bool floweyWasShown;

    public StartView() // For the designer preview
    {
        InitializeComponent();
    }
    public StartView(MainWindow mainWindow)
    {
        InitializeComponent();

        this.mainWindow = mainWindow;
        try
        {
            floweyFrames = new[]
            {
                new Bitmap(AssetLoader.Open(new Uri(floweyPathPrefix + "1.png"))),
                new Bitmap(AssetLoader.Open(new Uri(floweyPathPrefix + "2.png"))),
                new Bitmap(AssetLoader.Open(new Uri(floweyPathPrefix + "3.png"))),
                new Bitmap(AssetLoader.Open(new Uri(floweyPathPrefix + "4.png"))),
                new Bitmap(AssetLoader.Open(new Uri(floweyPathPrefix + "5.png")))
            };
        }
        catch { }
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

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!floweyWasShown && e.HeightChanged && e.NewSize.Height > 520)
        {
            floweyWasShown = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    foreach (var frame in floweyFrames)
                    {
                        await Task.Delay(100);
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            FloweyImage.Source = frame;
                            FloweyImage.UpdateLayout();
                        }, DispatcherPriority.Render);
                    }
                    
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        FloweyImage.Source = null;
                        foreach (var img in floweyFrames)
                            img.Dispose();
                    });
                }
                catch { }
            });
        }
    }
}
