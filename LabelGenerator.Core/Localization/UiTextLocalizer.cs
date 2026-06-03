using System.Text.Json;

namespace LabelGenerator.Core.Localization;

public static class UiTextLocalizer
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> TranslationCache = new(StringComparer.OrdinalIgnoreCase);

    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en";
        }

        var normalized = language.Trim();
        var separatorIndex = normalized.IndexOfAny(['-', '_']);
        if (separatorIndex > 0)
        {
            normalized = normalized[..separatorIndex];
        }

        return normalized.ToLowerInvariant();
    }

    public static string Translate(string value, string? language)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var normalizedLanguage = NormalizeLanguage(language);
        if (string.Equals(normalizedLanguage, "en", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var translations = LoadTranslations(normalizedLanguage);
        return translations.TryGetValue(value, out var translated) && !string.IsNullOrEmpty(translated)
            ? translated
            : value;
    }

    private static IReadOnlyDictionary<string, string> LoadTranslations(string language)
    {
        lock (TranslationCache)
        {
            if (TranslationCache.TryGetValue(language, out var cached))
            {
                return cached;
            }

            var loaded = LoadTranslationFile(language);
            TranslationCache[language] = loaded;
            return loaded;
        }
    }

    private static IReadOnlyDictionary<string, string> LoadTranslationFile(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "lng", $"{language}.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var json = File.ReadAllText(path);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
            return new Dictionary<string, string>(values, StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
