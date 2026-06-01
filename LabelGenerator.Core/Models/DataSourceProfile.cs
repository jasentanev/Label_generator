namespace LabelGenerator.Core.Models;

public sealed class DataSourceProfile
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ProviderInvariantName { get; set; } = string.Empty;

    public string ConnectionSecret { get; set; } = string.Empty;

    public string PrimaryView { get; set; } = string.Empty;

    public string DetailView { get; set; } = string.Empty;

    public string PrimarySql { get; set; } = string.Empty;

    public string DetailSql { get; set; } = string.Empty;

    public string LookupSql { get; set; } = string.Empty;

    public string LookupKeyColumn { get; set; } = string.Empty;

    public string KeyColumn { get; set; } = string.Empty;

    public int MaxRows { get; set; } = 500;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public List<string> VisibleColumns { get; set; } = [];

    public bool IsDemo => string.Equals(ProviderInvariantName, "Demo", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
}
