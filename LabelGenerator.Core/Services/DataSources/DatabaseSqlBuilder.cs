using System.Text.RegularExpressions;
using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.DataSources;

public static partial class DatabaseSqlBuilder
{
    public static string BuildPrimaryQuery(DataSourceProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.PrimarySql))
        {
            return RequireSelectStatement(profile.PrimarySql, nameof(profile.PrimarySql));
        }

        var viewName = RequireSafeIdentifierPath(profile.PrimaryView, nameof(profile.PrimaryView));
        var maxRows = Math.Clamp(profile.MaxRows, 1, 100_000);

        return profile.ProviderInvariantName switch
        {
            "Microsoft.Data.SqlClient" or "System.Data.SqlClient" =>
                $"select top ({maxRows}) * from {viewName}",
            "Npgsql" or "MySqlConnector" => $"select * from {viewName} limit {maxRows}",
            _ => $"select * from {viewName}"
        };
    }

    public static string BuildDetailQuery(DataSourceProfile profile, IReadOnlyList<string> keyParameterPlaceholders)
    {
        if (keyParameterPlaceholders.Count == 0)
        {
            throw new ArgumentException("At least one key parameter is required.", nameof(keyParameterPlaceholders));
        }

        var keyPlaceholders = string.Join(", ", keyParameterPlaceholders);

        if (!string.IsNullOrWhiteSpace(profile.DetailSql))
        {
            var sql = RequireSelectStatement(profile.DetailSql, nameof(profile.DetailSql));
            if (!sql.Contains("{Keys}", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("DetailSql must contain a {Keys} token for selected key values.", nameof(profile.DetailSql));
            }

            return sql.Replace("{Keys}", keyPlaceholders, StringComparison.OrdinalIgnoreCase);
        }

        var viewName = RequireSafeIdentifierPath(profile.DetailView, nameof(profile.DetailView));
        var keyColumn = RequireSafeIdentifierPath(profile.KeyColumn, nameof(profile.KeyColumn));

        return $"select * from {viewName} where {keyColumn} in ({keyPlaceholders})";
    }

    public static IReadOnlyList<string> BuildKeyParameterPlaceholders(DataSourceProfile profile, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var isOdbc = string.Equals(profile.ProviderInvariantName, "System.Data.Odbc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile.ProviderInvariantName, "Odbc", StringComparison.OrdinalIgnoreCase);

        return Enumerable.Range(0, count)
            .Select(index => isOdbc ? "?" : $"@p{index}")
            .ToList();
    }

    public static IReadOnlyList<string> BuildKeyParameterNames(int count) =>
        Enumerable.Range(0, Math.Max(0, count))
            .Select(index => $"p{index}")
            .ToList();

    private static string RequireSelectStatement(string value, string parameterName)
    {
        var sql = value.Trim();
        if (sql.EndsWith(';'))
        {
            sql = sql[..^1].Trim();
        }

        if (sql.Contains(';'))
        {
            throw new ArgumentException("Only one SELECT statement is allowed.", parameterName);
        }

        if (!sql.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            && !sql.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only SELECT statements are allowed.", parameterName);
        }

        return sql;
    }

    private static string RequireSafeIdentifierPath(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || !SafeIdentifierPathRegex().IsMatch(value))
        {
            throw new ArgumentException(
                "Only simple schema/view/column identifiers are allowed. Use views to encapsulate complex SQL.",
                parameterName);
        }

        return value;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$]*(\.[A-Za-z_][A-Za-z0-9_$]*)*$")]
    private static partial Regex SafeIdentifierPathRegex();
}
