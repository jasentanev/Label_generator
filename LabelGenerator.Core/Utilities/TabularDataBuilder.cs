using System.Data;

namespace LabelGenerator.Core.Utilities;

public static class TabularDataBuilder
{
    public static DataTable ToDataTable(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        var table = new DataTable();
        var materializedRows = rows.ToList();
        var columns = materializedRows
            .SelectMany(row => row.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var column in columns)
        {
            table.Columns.Add(column, typeof(object));
        }

        foreach (var row in materializedRows)
        {
            var dataRow = table.NewRow();
            foreach (var column in columns)
            {
                dataRow[column] = row.TryGetValue(column, out var value) && value is not null
                    ? value
                    : DBNull.Value;
            }

            table.Rows.Add(dataRow);
        }

        return table;
    }
}
