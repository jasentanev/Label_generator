using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Printing;

namespace LabelGenerator.Core.Tests;

public sealed class LabelQuantityResolverTests
{
    [Fact]
    public void ExpandRows_UsesLabelCountColumnWhenPresent()
    {
        var template = new LabelTemplateProfile();
        var rows = new[]
        {
            Row(("ProductCode", "A"), ("LabelCount", 2)),
            Row(("ProductCode", "B"), ("LabelCount", 3))
        };

        var expanded = LabelQuantityResolver.ExpandRows(template, rows, copies: 1);

        Assert.Equal(5, expanded.Count);
        Assert.Equal("A", expanded[0]["ProductCode"]);
        Assert.Equal("A", expanded[1]["ProductCode"]);
        Assert.Equal("B", expanded[2]["ProductCode"]);
    }

    [Fact]
    public void ExpandRows_MultipliesLabelCountByCopies()
    {
        var template = new LabelTemplateProfile();
        var rows = new[] { Row(("ProductCode", "A"), ("LabelCount", 2)) };

        var expanded = LabelQuantityResolver.ExpandRows(template, rows, copies: 3);

        Assert.Equal(6, expanded.Count);
    }

    [Fact]
    public void GetLabelCount_MissingColumnUsesDefaultCount()
    {
        var template = new LabelTemplateProfile();
        var row = Row(("ProductCode", "A"));

        var count = LabelQuantityResolver.GetLabelCount(template, row);

        Assert.Equal(1, count);
    }

    [Fact]
    public void GetLabelCount_ZeroSuppressesRow()
    {
        var template = new LabelTemplateProfile();
        var row = Row(("ProductCode", "A"), ("LabelCount", 0));

        var count = LabelQuantityResolver.GetLabelCount(template, row);

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetLabelCount_CanBeDisabledPerTemplate()
    {
        var template = new LabelTemplateProfile
        {
            LabelCount = new LabelCountSettings { IsEnabled = false, DefaultCount = 1 }
        };
        var row = Row(("ProductCode", "A"), ("LabelCount", 5));

        var count = LabelQuantityResolver.GetLabelCount(template, row);

        Assert.Equal(1, count);
    }

    [Fact]
    public void GetLabelCount_UsesConfiguredColumnName()
    {
        var template = new LabelTemplateProfile
        {
            LabelCount = new LabelCountSettings { ColumnName = "QtyLabels" }
        };
        var row = Row(("ProductCode", "A"), ("QtyLabels", "4"));

        var count = LabelQuantityResolver.GetLabelCount(template, row);

        Assert.Equal(4, count);
    }

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
}
