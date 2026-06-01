namespace LabelGenerator.Core.Services.Audit;

public interface IAuditLogger
{
    Task WriteAsync(string message, CancellationToken cancellationToken = default);
}
