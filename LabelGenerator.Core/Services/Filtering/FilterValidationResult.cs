namespace LabelGenerator.Core.Services.Filtering;

public sealed class FilterValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; } = [];
}
