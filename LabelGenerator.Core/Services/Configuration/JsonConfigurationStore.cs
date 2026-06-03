using System.Text.Json;
using System.Text.Json.Serialization;
using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.Configuration;

public sealed class JsonConfigurationStore(string configurationPath) : IConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<LabelGeneratorConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configurationPath))
        {
            return CreateDefaultConfiguration();
        }

        await using var stream = File.OpenRead(configurationPath);
        var configuration = await JsonSerializer.DeserializeAsync<LabelGeneratorConfiguration>(
            stream,
            SerializerOptions,
            cancellationToken);

        return configuration ?? CreateDefaultConfiguration();
    }

    public async Task SaveAsync(LabelGeneratorConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directory = Path.GetDirectoryName(configurationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(configurationPath);
        await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions, cancellationToken);
    }

    private static LabelGeneratorConfiguration CreateDefaultConfiguration() =>
        new()
        {
            Application = new ApplicationSettings
            {
                LabelStarters =
                [
                    new LabelStarterProfile
                    {
                        Id = "demo-open",
                        DisplayName = "Demo labels",
                        Description = "Open the label generator with the demo data source and product label.",
                        DataSourceId = "demo",
                        LabelTemplateId = "a4-2x7-demo",
                        ActionMode = LabelStarterActionMode.Open,
                        UserMode = true
                    },
                    new LabelStarterProfile
                    {
                        Id = "demo-load",
                        DisplayName = "Demo load",
                        Description = "Open the demo label workflow and load the master view.",
                        DataSourceId = "demo",
                        LabelTemplateId = "a4-2x7-demo",
                        ActionMode = LabelStarterActionMode.Load,
                        UserMode = true
                    },
                    new LabelStarterProfile
                    {
                        Id = "demo-preview",
                        DisplayName = "Demo preview",
                        Description = "Load the demo data source and open preview for the product label.",
                        DataSourceId = "demo",
                        LabelTemplateId = "a4-2x7-demo",
                        ActionMode = LabelStarterActionMode.Preview,
                        UserMode = true
                    }
                ]
            },
            DataSources =
            [
                new DataSourceProfile
                {
                    Id = "demo",
                    DisplayName = "Demo inventory views",
                    ProviderInvariantName = "Demo",
                    PrimaryView = "vw_label_candidates",
                    DetailView = "vw_label_details",
                    KeyColumn = "ProductCode",
                    MaxRows = 500,
                    CommandTimeoutSeconds = 30,
                    VisibleColumns = ["ProductCode", "ProductName", "Category", "BatchNo", "Status"]
                }
            ],
            LabelTemplates =
            [
                new LabelTemplateProfile
                {
                    Id = "a4-2x7-demo",
                    DisplayName = "A4 2 x 7 product labels",
                    TemplateFilePath = "Templates/demo-a4-2x7.label.json",
                    ExpectedFields = ["ProductCode", "ProductName", "BatchNo", "ExpiryDate", "Barcode"],
                    MasterFilters = [],
                    LabelCount = new LabelCountSettings(),
                    Sheet = new LabelSheetDefinition(),
                    Design = LabelTemplateDesign.CreateDefaultProductDesign()
                },
                new LabelTemplateProfile
                {
                    Id = "a4-1x1-shipping",
                    DisplayName = "A4 shipping label",
                    TemplateFilePath = "Templates/demo-shipping.label.json",
                    ExpectedFields = ["ProductCode", "ProductName", "BatchNo", "Barcode"],
                    MasterFilters = [],
                    LabelCount = new LabelCountSettings(),
                    Sheet = new LabelSheetDefinition
                    {
                        MarginLeftMillimeters = 15,
                        MarginTopMillimeters = 20,
                        LabelWidthMillimeters = 180,
                        LabelHeightMillimeters = 120,
                        GapXMillimeters = 0,
                        GapYMillimeters = 0,
                        Columns = 1,
                        Rows = 1
                    },
                    Design = LabelTemplateDesign.CreateDefaultShippingDesign()
                }
            ]
        };
}
