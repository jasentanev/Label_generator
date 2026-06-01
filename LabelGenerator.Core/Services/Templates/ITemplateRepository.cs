using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.Templates;

public interface ITemplateRepository
{
    IReadOnlyList<LabelTemplateProfile> GetTemplates();

    LabelTemplateProfile? FindById(string templateId);

    TemplateValidationResult ValidateFields(
        LabelTemplateProfile template,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows);
}
