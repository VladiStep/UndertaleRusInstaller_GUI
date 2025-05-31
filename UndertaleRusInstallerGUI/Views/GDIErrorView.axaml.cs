using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace UndertaleRusInstallerGUI.Views;

public enum GDIErrorType
{
    Unknown,
    CompatibleIsMissing,
    BundledIsBroken,
    InstalledIsBroken
}

public partial class GDIErrorView : UserControl
{
    private readonly MainWindow mainWindow;

    private GDIErrorType ErrorType { get; }

    private readonly string _installCmdText
        = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
          ? "sudo apt-get update\nsudo apt-get install libc6-dev libgdiplus"
          : "brew install mono-libgdiplus";
    private string InstallCmdText => _installCmdText;
        

    public GDIErrorView()  // For the designer preview
    {
        InitializeComponent();
    }

    public GDIErrorView(MainWindow mainWindow, GDIErrorType errorType)
    {
        InitializeComponent();

        this.mainWindow = mainWindow;
        ErrorType = errorType;
    }

    private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (mainWindow is null)
            return;

        if (ErrorType == GDIErrorType.BundledIsBroken)
        {
            _ = Task.Run(() =>
            {
                OSMethods.Discard_libgdiplus_Files(mainWindow);
            });
        }

        
        var mainContentTemp = MainContent.Resources[ErrorType] as DataTemplate;
        MainContent.Content = mainContentTemp?.Build(null);

        mainWindow.Clipboard.SetTextAsync(InstallCmdText);
    }
}