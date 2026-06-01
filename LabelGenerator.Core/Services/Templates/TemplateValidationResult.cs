namespace LabelGenerator.Core.Services.Templates;

public sealed class TemplateValidationResult(IEnumerable<string> missingFields)
{
    public IReadOnlyList<string> MissingFields { get; } = missingFields.ToList();

    public bool IsValid => MissingFields.Count == 0;
}
