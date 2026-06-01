using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Templates;

namespace LabelGenerator.Core.Tests;

public sealed class TemplateRepositoryTests
{
    [Fact]
    public void ValidateFields_ReportsMissingFields()
    {
        var template = new LabelTemplateProfile
        {
            Id = "test",
            ExpectedFields = ["ProductCode", "Barcode"]
        };
        var repository = new TemplateRepository([template]);
        var rows = new[]
        {
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductCode"] = "BG-1001"
            }
        };

        var result = repository.ValidateFields(template, rows);

        Assert.False(result.IsValid);
        Assert.Equal(["Barcode"], result.MissingFields);
    }
}
