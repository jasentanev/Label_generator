namespace LabelGenerator.App.ViewModels;

public sealed class QuickMarkPrintResult
{
    public QuickMarkPrintStatus Status { get; set; }

    public string ScanValue { get; set; } = string.Empty;

    public List<string> Keys { get; set; } = [];

    public int DetailRowCount { get; set; }

    public int PrintedLabelCount { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsSuccess => Status == QuickMarkPrintStatus.Success;

    public static QuickMarkPrintResult Failed(
        QuickMarkPrintStatus status,
        string scanValue,
        string message) =>
        new()
        {
            Status = status,
            ScanValue = scanValue,
            Message = message
        };
}

public enum QuickMarkPrintStatus
{
    Success,
    NoScanValue,
    NoKey,
    NotInFilteredMaster,
    NoDetailRows,
    TemplateInvalid,
    PrinterError
}
