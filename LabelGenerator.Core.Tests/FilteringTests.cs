using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Filtering;

namespace LabelGenerator.Core.Tests;

public sealed class FilteringTests
{
    private readonly RegexColumnFilterService service = new();

    [Fact]
    public void Apply_MatchesBulgarianText()
    {
        var rows = new[]
        {
            Row(("ProductName", "Кисело мляко 3.6%")),
            Row(("ProductName", "Honey 450 g"))
        };

        var filters = new[]
        {
            new ColumnFilter
            {
                ColumnName = "ProductName",
                Pattern = "мляко",
                IsEnabled = true
            }
        };

        var result = service.Apply(rows, filters);

        Assert.Single(result);
        Assert.Equal("Кисело мляко 3.6%", result[0]["ProductName"]);
    }

    [Fact]
    public void Apply_RespectsCaseSensitivity()
    {
        var rows = new[] { Row(("ProductName", "Honey 450 g")) };
        var filters = new[]
        {
            new ColumnFilter
            {
                ColumnName = "ProductName",
                Pattern = "honey",
                IsEnabled = true,
                IsCaseSensitive = true
            }
        };

        var result = service.Apply(rows, filters);

        Assert.Empty(result);
    }

    [Fact]
    public void Validate_ReturnsErrorsForInvalidRegex()
    {
        var filters = new[]
        {
            new ColumnFilter
            {
                ColumnName = "ProductName",
                Pattern = "[",
                IsEnabled = true
            }
        };

        var result = service.Validate(filters);

        Assert.False(result.IsValid);
        Assert.Contains("ProductName", result.Errors[0]);
    }

    [Fact]
    public void Apply_TreatsNullAsEmptyString()
    {
        var rows = new[] { Row(("ProductName", null)) };
        var filters = new[]
        {
            new ColumnFilter
            {
                ColumnName = "ProductName",
                Pattern = "^$",
                IsEnabled = true
            }
        };

        var result = service.Apply(rows, filters);

        Assert.Single(result);
    }

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
}
