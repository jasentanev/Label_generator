namespace LabelGenerator.Core.Configuration;

public sealed class ApplicationSettings
{
    public string Language { get; set; } = "en";

    public List<LabelStarterProfile> LabelStarters { get; set; } = [];
}
