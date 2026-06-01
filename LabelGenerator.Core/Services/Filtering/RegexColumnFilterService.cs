using System.Globalization;
using System.Text.RegularExpressions;
using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.Filtering;

public sealed class RegexColumnFilterService : IColumnFilterService
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    public FilterValidationResult Validate(IEnumerable<ColumnFilter> filters)
    {
        var result = new FilterValidationResult();

        foreach (var filter in filters.Where(filter => filter.IsActive))
        {
            try
            {
                _ = CreateRegex(filter);
            }
            catch (ArgumentException ex)
            {
                result.Errors.Add($"{filter.ColumnName}: {ex.Message}");
            }
        }

        return result;
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Apply(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        IEnumerable<ColumnFilter> filters)
    {
        var activeFilters = filters
            .Where(filter => filter.IsActive)
            .Select(filter => (Filter: filter, Regex: CreateRegex(filter)))
            .ToList();

        if (activeFilters.Count == 0)
        {
            return rows.ToList();
        }

        return rows
            .Where(row => MatchesAll(row, activeFilters))
            .ToList();
    }

    private static bool MatchesAll(
        IReadOnlyDictionary<string, object?> row,
        IEnumerable<(ColumnFilter Filter, Regex Regex)> filters)
    {
        foreach (var (filter, regex) in filters)
        {
            row.TryGetValue(filter.ColumnName, out var value);
            var text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;

            if (!regex.IsMatch(text))
            {
                return false;
            }
        }

        return true;
    }

    private static Regex CreateRegex(ColumnFilter filter)
    {
        var options = RegexOptions.Compiled;
        if (!filter.IsCaseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(filter.Pattern, options, MatchTimeout);
    }
}
