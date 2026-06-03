using System.IO;

namespace LabelGenerator.App;

public static class BootSplashLoader
{
    private const string LogoFileName = "log.jpg";
    private const string InfoFileName = "info.txt";

    public static BootSplashWindow? TryCreate(string baseDirectory)
    {
        try
        {
            var logoPath = Path.Combine(baseDirectory, LogoFileName);
            var infoPath = Path.Combine(baseDirectory, InfoFileName);
            if (!File.Exists(logoPath) || !File.Exists(infoPath))
            {
                return null;
            }

            var infoText = File.ReadAllText(infoPath).Trim();
            if (string.IsNullOrWhiteSpace(infoText))
            {
                return null;
            }

            return new BootSplashWindow(logoPath, infoText);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Boot splash skipped: {ex}");
            return null;
        }
    }
}
