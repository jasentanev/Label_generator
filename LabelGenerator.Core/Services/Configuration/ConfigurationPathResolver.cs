namespace LabelGenerator.Core.Services.Configuration;

public static class ConfigurationPathResolver
{
    private const string EnvironmentVariableName = "LABEL_GENERATOR_CONFIG";

    public static string ResolveSharedConfigurationPath(string bundledConfigurationPath)
    {
        var overridePath = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            EnsureParentDirectory(overridePath);
            SeedIfMissing(overridePath, bundledConfigurationPath);
            return overridePath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var sharedPath = Path.Combine(localAppData, "LabelGenerator", "appsettings.json");
        EnsureParentDirectory(sharedPath);
        SeedIfMissing(sharedPath, bundledConfigurationPath);
        return sharedPath;
    }

    private static void SeedIfMissing(string targetPath, string bundledConfigurationPath)
    {
        if (File.Exists(targetPath) || !File.Exists(bundledConfigurationPath))
        {
            return;
        }

        File.Copy(bundledConfigurationPath, targetPath);
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
