using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Configuration;

public sealed class LabelGeneratorConfiguration
{
    public List<DataSourceProfile> DataSources { get; set; } = [];

    public List<LabelTemplateProfile> LabelTemplates { get; set; } = [];
}
