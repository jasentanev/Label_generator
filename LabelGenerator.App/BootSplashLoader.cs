using System.IO;

namespace LabelGenerator.App;

public static class BootSplashLoader
{
    private const string LogoFileName = "log.jpg";
    private const string AlternateLogoFileName = "logo.jpg";
    private const string InfoFileName = "info.txt";

    public static BootSplashWindow? TryCreate(string baseDirectory)
    {
        try
        {
            var logoPath = ResolveLogoPath(baseDirectory);
            var infoPath = Path.Combine(baseDirectory, InfoFileName);
            if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(infoPath))
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

    private static string ResolveLogoPath(string baseDirectory)
    {
        var preferredPath = Path.Combine(baseDirectory, LogoFileName);
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var alternatePath = Path.Combine(baseDirectory, AlternateLogoFileName);
        return File.Exists(alternatePath) ? alternatePath : string.Empty;
    }
}
