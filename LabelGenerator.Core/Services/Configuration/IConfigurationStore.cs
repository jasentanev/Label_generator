using LabelGenerator.Core.Configuration;

namespace LabelGenerator.Core.Services.Configuration;

public interface IConfigurationStore
{
    Task<LabelGeneratorConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LabelGeneratorConfiguration configuration, CancellationToken cancellationToken = default);
}
