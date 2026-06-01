using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.Templates;

public sealed class TemplateRepository(IEnumerable<LabelTemplateProfile> templates) : ITemplateRepository
{
    private readonly List<LabelTemplateProfile> templates = templates.ToList();

    public IReadOnlyList<LabelTemplateProfile> GetTemplates() => templates;

    public LabelTemplateProfile? FindById(string templateId) =>
        templates.FirstOrDefault(template => string.Equals(template.Id, templateId, StringComparison.OrdinalIgnoreCase));

    public TemplateValidationResult ValidateFields(
        LabelTemplateProfile template,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        var availableFields = rows
            .SelectMany(row => row.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingFields = template.ExpectedFields
            .Where(field => !availableFields.Contains(field))
            .ToList();

        return new TemplateValidationResult(missingFields);
    }
}
