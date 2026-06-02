using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace LabelGenerator.App;

public partial class App : Application
{
    private static readonly string StartupLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LabelGenerator",
        "startup.log");

    public static void WriteStartupLog(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(StartupLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                StartupLogPath,
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Startup diagnostics must never prevent the app from starting.
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        WriteStartupLog("App.OnStartup");
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        base.OnStartup(e);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteStartupLog($"DispatcherUnhandledException: {e.Exception}");
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteStartupLog($"UnhandledException: {e.ExceptionObject}");
    }
}
