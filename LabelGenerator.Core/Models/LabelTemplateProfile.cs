namespace LabelGenerator.Core.Models;

public sealed class LabelTemplateProfile
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string TemplateFilePath { get; set; } = string.Empty;

    public List<string> ExpectedFields { get; set; } = [];

    public List<ColumnFilter> MasterFilters { get; set; } = [];

    public LabelCountSettings LabelCount { get; set; } = new();

    public string DefaultPrinter { get; set; } = string.Empty;

    public CalibrationOffsets CalibrationOffsets { get; set; } = new();

    public LabelSheetDefinition Sheet { get; set; } = new();

    public LabelTemplateDesign Design { get; set; } = LabelTemplateDesign.CreateDefaultProductDesign();

    public string DesignerExecutablePath { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
}
