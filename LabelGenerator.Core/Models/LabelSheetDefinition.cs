namespace LabelGenerator.Core.Models;

public sealed class LabelSheetDefinition
{
    public double PageWidthMillimeters { get; set; } = 210;

    public double PageHeightMillimeters { get; set; } = 297;

    public double MarginLeftMillimeters { get; set; } = 4.8;

    public double MarginTopMillimeters { get; set; } = 13.5;

    public double LabelWidthMillimeters { get; set; } = 99.1;

    public double LabelHeightMillimeters { get; set; } = 38.1;

    public double GapXMillimeters { get; set; } = 2.5;

    public double GapYMillimeters { get; set; }

    public int Columns { get; set; } = 2;

    public int Rows { get; set; } = 7;

    public int LabelsPerPage => Math.Max(1, Columns * Rows);
}
