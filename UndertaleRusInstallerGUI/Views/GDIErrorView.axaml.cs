using Avalonia;
using Avalonia.Controls;
using System.Runtime.InteropServices;

namespace UndertaleRusInstallerGUI.Views;

public partial class GDIErrorView : UserControl
{
    private readonly MainWindow mainWindow;

    public GDIErrorView()  // For the designer preview
    {
        InitializeComponent();
    }

    public GDIErrorView(MainWindow mainWindow)
    {
        InitializeComponent();

        this.mainWindow = mainWindow;
    }

    private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (mainWindow is null)
            return;

        string cmdText; 
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            cmdText = "sudo apt-get update\n" +
                      "sudo apt-get install libc6-dev libgdiplus";
        }
        else
        {
            cmdText = "brew install mono-libgdiplus";
        }

        GDIInstallCmdText.Text = cmdText;
        mainWindow.Clipboard.SetTextAsync(cmdText);
    }
}