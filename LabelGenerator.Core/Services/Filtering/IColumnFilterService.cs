using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.Filtering;

public interface IColumnFilterService
{
    FilterValidationResult Validate(IEnumerable<ColumnFilter> filters);

    IReadOnlyList<IReadOnlyDictionary<string, object?>> Apply(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        IEnumerable<ColumnFilter> filters);
}
