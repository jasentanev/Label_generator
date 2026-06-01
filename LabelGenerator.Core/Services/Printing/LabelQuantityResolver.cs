using System.Globalization;
using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.Printing;

public static class LabelQuantityResolver
{
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> ExpandRows(
        LabelTemplateProfile template,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        int copies)
    {
        var effectiveCopies = Math.Max(1, copies);
        var expandedRows = new List<IReadOnlyDictionary<string, object?>>();

        foreach (var row in rows)
        {
            var labelCount = GetLabelCount(template, row);
            for (var i = 0; i < labelCount * effectiveCopies; i++)
            {
                expandedRows.Add(row);
            }
        }

        return expandedRows;
    }

    public static int GetTotalLabelCount(
        LabelTemplateProfile template,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        int copies) =>
        rows.Sum(row => GetLabelCount(template, row)) * Math.Max(1, copies);

    public static int GetLabelCount(
        LabelTemplateProfile template,
        IReadOnlyDictionary<string, object?> row)
    {
        var settings = template.LabelCount ?? new LabelCountSettings();
        var defaultCount = Math.Max(0, settings.DefaultCount);

        if (!settings.IsEnabled || string.IsNullOrWhiteSpace(settings.ColumnName))
        {
            return defaultCount;
        }

        if (!row.TryGetValue(settings.ColumnName, out var rawValue) || rawValue is null || rawValue is DBNull)
        {
            return defaultCount;
        }

        var count = TryReadCount(rawValue, out var parsedCount)
            ? parsedCount
            : defaultCount;

        count = Math.Max(0, count);
        return settings.MaxCountPerRow > 0
            ? Math.Min(count, settings.MaxCountPerRow)
            : count;
    }

    private static bool TryReadCount(object value, out int count)
    {
        switch (value)
        {
            case byte byteValue:
                count = byteValue;
                return true;
            case short shortValue:
                count = shortValue;
                return true;
            case int intValue:
                count = intValue;
                return true;
            case long longValue:
                count = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
                return true;
            case decimal decimalValue:
                count = decimalValue > int.MaxValue ? int.MaxValue : (int)Math.Floor(decimalValue);
                return true;
            case double doubleValue when !double.IsNaN(doubleValue):
                count = doubleValue > int.MaxValue ? int.MaxValue : (int)Math.Floor(doubleValue);
                return true;
            case float floatValue when !float.IsNaN(floatValue):
                count = floatValue > int.MaxValue ? int.MaxValue : (int)Math.Floor(floatValue);
                return true;
            default:
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                {
                    return true;
                }

                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedDecimal))
                {
                    count = parsedDecimal > int.MaxValue ? int.MaxValue : (int)Math.Floor(parsedDecimal);
                    return true;
                }

                count = 0;
                return false;
        }
    }
}
