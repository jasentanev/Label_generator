namespace LabelGenerator.Core.Services.Audit;

public sealed class FileAuditLogger(string logFilePath) : IAuditLogger
{
    public async Task WriteAsync(string message, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}";
        await File.AppendAllTextAsync(logFilePath, line, cancellationToken);
    }
}
