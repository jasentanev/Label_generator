namespace LabelGenerator.Core.Services.Configuration;

public static class ConfigurationPathResolver
{
    private const string EnvironmentVariableName = "LABEL_GENERATOR_CONFIG";
    private const string ApplicationDirectoryName = "LabelGenerator";
    private const string ConfigurationFileName = "appsettings.json";

    public static string ResolveSharedConfigurationPath(string bundledConfigurationPath)
    {
        var overridePath = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            EnsureParentDirectory(overridePath);
            SeedIfMissing(overridePath, bundledConfigurationPath);
            return overridePath;
        }

        var localPath = Path.Combine(
            Path.GetDirectoryName(bundledConfigurationPath) ?? AppContext.BaseDirectory,
            ConfigurationFileName);
        EnsureParentDirectory(localPath);
        var legacyPath = ResolveLegacyConfigurationPath();
        MigrateLegacyConfigurationIfNewer(localPath, legacyPath);
        SeedIfMissing(localPath, legacyPath, bundledConfigurationPath);
        return localPath;
    }

    private static void SeedIfMissing(string targetPath, params string[] sourcePaths)
    {
        if (File.Exists(targetPath))
        {
            return;
        }

        foreach (var sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }

            File.Copy(sourcePath, targetPath);
            return;
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ResolveLegacyConfigurationPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, ApplicationDirectoryName, ConfigurationFileName);
    }

    private static void MigrateLegacyConfigurationIfNewer(string targetPath, string legacyPath)
    {
        if (!File.Exists(targetPath) || !File.Exists(legacyPath))
        {
            return;
        }

        var targetInfo = new FileInfo(targetPath);
        var legacyInfo = new FileInfo(legacyPath);
        if (legacyInfo.LastWriteTimeUtc <= targetInfo.LastWriteTimeUtc)
        {
            return;
        }

        File.Copy(legacyPath, targetPath, overwrite: true);
    }
}
