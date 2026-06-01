namespace LabelGenerator.Core.Models;

public sealed class PrintRequest
{
    public List<string> SelectedKeys { get; set; } = [];

    public string TemplateId { get; set; } = string.Empty;

    public string PrinterName { get; set; } = string.Empty;

    public int Copies { get; set; } = 1;

    public int StartLabelPosition { get; set; } = 1;

    public PrintMode Mode { get; set; } = PrintMode.Preview;
}
