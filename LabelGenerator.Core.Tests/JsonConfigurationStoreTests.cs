using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Configuration;

namespace LabelGenerator.Core.Tests;

public sealed class JsonConfigurationStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsDesignedTemplates()
    {
        var path = Path.Combine(Path.GetTempPath(), "LabelGeneratorTests", $"{Guid.NewGuid():N}.json");
        var store = new JsonConfigurationStore(path);
        var configuration = new LabelGeneratorConfiguration
        {
            DataSources =
            [
                new DataSourceProfile
                {
                    Id = "demo",
                    DisplayName = "Demo",
                    ProviderInvariantName = "Demo",
                    PrimaryView = "vw_primary",
                    DetailView = "vw_detail",
                    KeyColumn = "ProductCode"
                }
            ],
            LabelTemplates =
            [
                new LabelTemplateProfile
                {
                    Id = "custom",
                    DisplayName = "Custom",
                    TemplateFilePath = "Templates/custom.label.json",
                    ExpectedFields = ["Barcode"],
                    MasterFilters =
                    [
                        new ColumnFilter
                        {
                            ColumnName = "Status",
                            Pattern = "Promo|Damage",
                            IsEnabled = true
                        }
                    ],
                    Design = new LabelTemplateDesign
                    {
                        Elements =
                        [
                            new LabelDesignElement
                            {
                                Type = LabelElementType.Barcode,
                                FieldName = "Barcode",
                                BarcodeSymbology = BarcodeSymbology.QrCode,
                                WidthMillimeters = 20,
                                HeightMillimeters = 20
                            },
                            new LabelDesignElement
                            {
                                Type = LabelElementType.Image,
                                ImagePath = "Images/logo.png",
                                WidthMillimeters = 15,
                                HeightMillimeters = 10
                            }
                        ]
                    }
                }
            ]
        };

        await store.SaveAsync(configuration);
        var loaded = await store.LoadAsync();

        Assert.Single(loaded.LabelTemplates);
        Assert.Single(loaded.LabelTemplates[0].MasterFilters);
        Assert.Equal("Status", loaded.LabelTemplates[0].MasterFilters[0].ColumnName);
        Assert.Equal(2, loaded.LabelTemplates[0].Design.Elements.Count);
        Assert.Equal(LabelElementType.Barcode, loaded.LabelTemplates[0].Design.Elements[0].Type);
        Assert.Equal(BarcodeSymbology.QrCode, loaded.LabelTemplates[0].Design.Elements[0].BarcodeSymbology);
        Assert.Equal("Images/logo.png", loaded.LabelTemplates[0].Design.Elements[1].ImagePath);
    }
}
