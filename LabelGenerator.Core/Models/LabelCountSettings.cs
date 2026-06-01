namespace LabelGenerator.Core.Models;

public sealed class LabelCountSettings
{
    public bool IsEnabled { get; set; } = true;

    public string ColumnName { get; set; } = "LabelCount";

    public int DefaultCount { get; set; } = 1;

    public int MaxCountPerRow { get; set; } = 1000;
}
