using System.Text.Json.Serialization;

namespace LabelGenerator.Core.Models;

public sealed class ColumnFilter
{
    public string ColumnName { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public bool IsCaseSensitive { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsActive => IsEnabled && !string.IsNullOrWhiteSpace(Pattern);

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var state = IsEnabled ? "On" : "Off";
            var sensitivity = IsCaseSensitive ? "case" : "ignore case";
            return $"{state}: {ColumnName} =~ {Pattern} ({sensitivity})";
        }
    }
}
