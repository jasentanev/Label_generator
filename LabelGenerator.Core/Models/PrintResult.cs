namespace LabelGenerator.Core.Models;

public sealed class PrintResult
{
    public PrintStatus Status { get; set; }

    public int PrintedCount { get; set; }

    public List<string> FailedKeys { get; set; } = [];

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public static PrintResult Success(int printedCount) =>
        new() { Status = PrintStatus.Success, PrintedCount = printedCount };

    public static PrintResult Failed(string message, IEnumerable<string>? failedKeys = null) =>
        new()
        {
            Status = PrintStatus.Failed,
            ErrorMessage = message,
            FailedKeys = failedKeys?.ToList() ?? []
        };
}

public enum PrintStatus
{
    Success,
    Partial,
    Failed,
    Cancelled
}
