namespace LabelGenerator.Core.Configuration;

public sealed class LabelStarterProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    public string LabelTemplateId { get; set; } = string.Empty;

    public LabelStarterActionMode ActionMode { get; set; } = LabelStarterActionMode.Open;

    public bool UserMode { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
}

public enum LabelStarterActionMode
{
    Open,
    Load,
    Preview,
    Print
}
