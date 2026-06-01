using System.Data.Common;
using System.Data.Odbc;
using LabelGenerator.Core.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace LabelGenerator.Core.Services.DataSources;

public sealed class DataSourceService : IDataSourceService
{
    private static readonly Lazy<bool> ProviderRegistration = new(RegisterProviderFactories);

    private readonly IReadOnlyList<Dictionary<string, object?>> demoPrimaryRows =
    [
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = "BG-1001",
            ["ProductName"] = "Кисело мляко 3.6%",
            ["Category"] = "Dairy",
            ["BatchNo"] = "L240601",
            ["Status"] = "Ready"
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = "BG-1002",
            ["ProductName"] = "Бяло сирене",
            ["Category"] = "Dairy",
            ["BatchNo"] = "L240602",
            ["Status"] = "Hold"
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = "BG-2001",
            ["ProductName"] = "Лютеница домашна",
            ["Category"] = "Canned",
            ["BatchNo"] = "L240603",
            ["Status"] = "Ready"
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = "BG-3001",
            ["ProductName"] = "Honey 450 g",
            ["Category"] = "Grocery",
            ["BatchNo"] = "L240604",
            ["Status"] = "Ready"
        }
    ];

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> LoadPrimaryRowsAsync(
        DataSourceProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.IsDemo)
        {
            await Task.Yield();
            return demoPrimaryRows
                .Take(Math.Max(1, profile.MaxRows))
                .Select(CloneReadOnly)
                .ToList();
        }

        ProviderRegistration.Value.Equals(true);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(
            connection,
            DatabaseSqlBuilder.BuildPrimaryQuery(profile),
            profile.CommandTimeoutSeconds);

        return await ReadRowsAsync(command, profile.MaxRows, cancellationToken);
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> LoadDetailRowsAsync(
        DataSourceProfile profile,
        IEnumerable<object?> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var keyList = keys
            .Where(key => key is not null && !string.IsNullOrWhiteSpace(Convert.ToString(key)))
            .Distinct()
            .ToArray();

        if (keyList.Length == 0)
        {
            return [];
        }

        if (profile.IsDemo)
        {
            await Task.Yield();
            return BuildDemoDetailRows(keyList)
                .Select(CloneReadOnly)
                .ToList();
        }

        ProviderRegistration.Value.Equals(true);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken);

        var parameterNames = DatabaseSqlBuilder.BuildKeyParameterNames(keyList.Length);
        var parameterPlaceholders = DatabaseSqlBuilder.BuildKeyParameterPlaceholders(profile, keyList.Length);
        await using var command = CreateCommand(
            connection,
            DatabaseSqlBuilder.BuildDetailQuery(profile, parameterPlaceholders),
            profile.CommandTimeoutSeconds);

        for (var i = 0; i < keyList.Length; i++)
        {
            AddParameter(command, parameterNames[i], keyList[i]);
        }

        return await ReadRowsAsync(command, null, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> LookupKeysAsync(
        DataSourceProfile profile,
        string scanValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(scanValue))
        {
            return [];
        }

        if (profile.IsDemo || string.IsNullOrWhiteSpace(profile.LookupSql))
        {
            await Task.Yield();
            return [scanValue.Trim()];
        }

        ProviderRegistration.Value.Equals(true);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(
            connection,
            DatabaseSqlBuilder.BuildLookupQuery(profile, DatabaseSqlBuilder.BuildScanParameterPlaceholder(profile)),
            profile.CommandTimeoutSeconds);

        AddParameter(command, "scan", scanValue.Trim());

        var rows = await ReadRowsAsync(command, null, cancellationToken);
        var keyColumn = string.IsNullOrWhiteSpace(profile.LookupKeyColumn)
            ? profile.KeyColumn
            : profile.LookupKeyColumn;

        return rows
            .Select(row => ReadLookupKey(row, keyColumn))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    private static DbConnection CreateConnection(DataSourceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ProviderInvariantName))
        {
            throw new InvalidOperationException("ProviderInvariantName is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.ConnectionSecret))
        {
            throw new InvalidOperationException("ConnectionSecret is required for database providers.");
        }

        var factory = DbProviderFactories.GetFactory(profile.ProviderInvariantName);
        var connection = factory.CreateConnection()
            ?? throw new InvalidOperationException($"Provider '{profile.ProviderInvariantName}' did not create a connection.");

        connection.ConnectionString = profile.ConnectionSecret;
        return connection;
    }

    private static bool RegisterProviderFactories()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("System.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("System.Data.Odbc", OdbcFactory.Instance);
        DbProviderFactories.RegisterFactory("Odbc", OdbcFactory.Instance);
        return true;
    }

    private static DbCommand CreateCommand(DbConnection connection, string commandText, int timeoutSeconds)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = timeoutSeconds;
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        DbCommand command,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
            if (maxRows is > 0 && rows.Count >= maxRows.Value)
            {
                break;
            }
        }

        return rows;
    }

    private static IReadOnlyDictionary<string, object?> CloneReadOnly(Dictionary<string, object?> row) =>
        new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);

    private static string? ReadLookupKey(IReadOnlyDictionary<string, object?> row, string keyColumn)
    {
        if (!string.IsNullOrWhiteSpace(keyColumn)
            && row.TryGetValue(keyColumn, out var configuredValue)
            && configuredValue is not null)
        {
            return Convert.ToString(configuredValue);
        }

        return row.Values.Any() ? Convert.ToString(row.Values.First()) : null;
    }

    private IEnumerable<Dictionary<string, object?>> BuildDemoDetailRows(IEnumerable<object?> keys)
    {
        foreach (var key in keys.Select(Convert.ToString))
        {
            var primary = demoPrimaryRows.FirstOrDefault(row =>
                string.Equals(Convert.ToString(row["ProductCode"]), key, StringComparison.OrdinalIgnoreCase));

            if (primary is null)
            {
                continue;
            }

            yield return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductCode"] = primary["ProductCode"],
                ["ProductName"] = primary["ProductName"],
                ["Category"] = primary["Category"],
                ["BatchNo"] = primary["BatchNo"],
                ["ExpiryDate"] = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
                ["NetWeight"] = primary["Category"]?.ToString() == "Dairy" ? "400 g" : "450 g",
                ["Barcode"] = $"380{Math.Abs(primary["ProductCode"]!.GetHashCode()):000000000}",
                ["LabelCount"] = primary["Status"]?.ToString() == "Hold" ? 0 : primary["Category"]?.ToString() == "Dairy" ? 2 : 1,
                ["Country"] = "BG",
                ["Storage"] = primary["Category"]?.ToString() == "Dairy" ? "2-6 C" : "Dry"
            };
        }
    }
}
