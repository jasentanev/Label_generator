using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.DataSources;

public interface IDataSourceService
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> LoadPrimaryRowsAsync(
        DataSourceProfile profile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> LoadDetailRowsAsync(
        DataSourceProfile profile,
        IEnumerable<object?> keys,
        CancellationToken cancellationToken = default);
}
