using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using static UndertaleRusInstallerGUI.OSMethods;

[assembly: Guid("B473613D-06DD-4C0D-AA8C-87154BBAFD9B")]
namespace UndertaleRusInstallerGUI.Desktop;

class Program
{
    private static readonly Regex macOSDirRegex = new(@"(.+/)[^/]+\.app/Contents/MacOS/$", RegexOptions.Compiled);
    public static string GetExecutableDirectory()
    {
        string procDir = Path.GetDirectoryName(Environment.ProcessPath) + Path.DirectorySeparatorChar;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var match = macOSDirRegex.Match(procDir);
            if (match.Success)
                procDir = match.Groups[1].Value;
            else
                procDir = null;
        }

        return procDir;
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            ProcessException(ex, "crash.txt");
        }
    }
    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
            return;

        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;

        ProcessException(ex, "crash1.txt");
    }
    private static void ProcessException(Exception ex, string fileName)
    {
        string procDir = GetExecutableDirectory();
        if (procDir is null || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string targetSiteStr = ex.StackTrace.Split(Environment.NewLine)[0];
            int inIndex = targetSiteStr.IndexOf(" in ");
            if (inIndex != -1)
                targetSiteStr = targetSiteStr.Insert(inIndex, "\n  ");
            string msg = $"Ошибка - {ex.Message}\n{targetSiteStr}";
            
            MessageBox("Установщик русификатора Undertale/XBOXTALE", msg, "ok", "error", 0);
        }
        else
        {
            string msg = $"Ошибка - {ex.Message}.\nПодробности смотрите в файле \"{fileName}\".";
            try
            {
                File.WriteAllText(procDir + fileName, ex.ToString());
            }
            catch { }

            MessageBox("Установщик русификатора Undertale/XBOXTALE", msg, "ok", "error", 0);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

}
