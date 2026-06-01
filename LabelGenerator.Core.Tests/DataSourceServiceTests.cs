using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.DataSources;

namespace LabelGenerator.Core.Tests;

public sealed class DataSourceServiceTests
{
    [Fact]
    public async Task DemoProvider_LoadsPrimaryAndDetailRows()
    {
        var service = new DataSourceService();
        var profile = CreateDemoProfile();

        var primaryRows = await service.LoadPrimaryRowsAsync(profile);
        var detailRows = await service.LoadDetailRowsAsync(profile, ["BG-1001"]);

        Assert.NotEmpty(primaryRows);
        Assert.Single(detailRows);
        Assert.Equal("BG-1001", detailRows[0]["ProductCode"]);
        Assert.True(detailRows[0].ContainsKey("Barcode"));
        Assert.True(detailRows[0].ContainsKey("ExpiryDate"));
        Assert.True(detailRows[0].ContainsKey("LabelCount"));
    }

    [Fact]
    public async Task DemoProvider_RespectsMaxRows()
    {
        var service = new DataSourceService();
        var profile = CreateDemoProfile();
        profile.MaxRows = 2;

        var primaryRows = await service.LoadPrimaryRowsAsync(profile);

        Assert.Equal(2, primaryRows.Count);
    }

    private static DataSourceProfile CreateDemoProfile() =>
        new()
        {
            Id = "demo",
            DisplayName = "Demo",
            ProviderInvariantName = "Demo",
            PrimaryView = "vw_label_candidates",
            DetailView = "vw_label_details",
            KeyColumn = "ProductCode",
            MaxRows = 500
        };
}
