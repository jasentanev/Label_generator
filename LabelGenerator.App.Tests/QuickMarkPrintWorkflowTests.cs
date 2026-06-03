using LabelGenerator.App.Printing;
using LabelGenerator.App.ViewModels;
using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Audit;
using LabelGenerator.Core.Services.Configuration;
using LabelGenerator.Core.Services.DataSources;
using LabelGenerator.Core.Services.Filtering;
using LabelGenerator.Core.Services.Printing;
using LabelGenerator.Core.Services.Templates;

namespace LabelGenerator.App.Tests;

public sealed class QuickMarkPrintWorkflowTests
{
    [Fact]
    public async Task QuickMarkAndPrint_PrintsFilteredLookupKeyWithLabelCountAndCopiesOne()
    {
        var dataSource = CreateDataSource();
        var template = CreateTemplate();
        var dataSourceService = new FakeDataSourceService(
            PrimaryRows(),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["scan-promo"] = ["100"]
            },
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["100"] = [Row(("ItemId", "100"), ("ArtName", "Promo item"), ("LabelCount", 3))]
            });
        var printerService = new FakePrinterService();
        var viewModel = CreateViewModel(dataSource, template, dataSourceService, printerService);

        await viewModel.InitializeAsync();
        viewModel.Copies = 5;

        var result = await viewModel.QuickMarkAndPrintAsync("scan-promo");

        Assert.Equal(QuickMarkPrintStatus.Success, result.Status);
        Assert.Equal(["100"], result.Keys);
        Assert.Equal(1, result.DetailRowCount);
        Assert.Equal(3, result.PrintedLabelCount);
        Assert.Equal(1, printerService.PrintCallCount);
        Assert.NotNull(printerService.LastRequest);
        Assert.Equal(1, printerService.LastRequest!.Copies);
        Assert.Equal(1, printerService.LastRequest.StartLabelPosition);
        Assert.Equal(PrintMode.DirectPrint, printerService.LastRequest.Mode);
    }

    [Fact]
    public async Task QuickMarkAndPrint_DoesNotPrintLookupKeyOutsideFilteredMaster()
    {
        var dataSource = CreateDataSource();
        var template = CreateTemplate();
        var dataSourceService = new FakeDataSourceService(
            PrimaryRows(),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["scan-regular"] = ["200"]
            },
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["200"] = [Row(("ItemId", "200"), ("ArtName", "Regular item"), ("LabelCount", 1))]
            });
        var printerService = new FakePrinterService();
        var viewModel = CreateViewModel(dataSource, template, dataSourceService, printerService);

        await viewModel.InitializeAsync();

        var result = await viewModel.QuickMarkAndPrintAsync("scan-regular");

        Assert.Equal(QuickMarkPrintStatus.NotInFilteredMaster, result.Status);
        Assert.Equal(0, printerService.PrintCallCount);
    }

    [Fact]
    public async Task QuickMarkAndPrint_DoesNotPrintWhenTemplateFieldsAreMissing()
    {
        var dataSource = CreateDataSource();
        var template = CreateTemplate();
        var dataSourceService = new FakeDataSourceService(
            PrimaryRows(),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["scan-promo"] = ["100"]
            },
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["100"] = [Row(("ItemId", "100"), ("LabelCount", 1))]
            });
        var printerService = new FakePrinterService();
        var viewModel = CreateViewModel(dataSource, template, dataSourceService, printerService);

        await viewModel.InitializeAsync();

        var result = await viewModel.QuickMarkAndPrintAsync("scan-promo");

        Assert.Equal(QuickMarkPrintStatus.TemplateInvalid, result.Status);
        Assert.Equal(0, printerService.PrintCallCount);
    }

    private static MainViewModel CreateViewModel(
        DataSourceProfile dataSource,
        LabelTemplateProfile template,
        IDataSourceService dataSourceService,
        FakePrinterService printerService) =>
        new(
            new FakeConfigurationStore(dataSource, template),
            dataSourceService,
            new RegexColumnFilterService(),
            printerService,
            new NoopDesignerLauncher(),
            new FakeAuditLogger());

    private static DataSourceProfile CreateDataSource() =>
        new()
        {
            Id = "demo",
            DisplayName = "Demo",
            ProviderInvariantName = "Demo",
            KeyColumn = "ItemId"
        };

    private static LabelTemplateProfile CreateTemplate() =>
        new()
        {
            Id = "promo-label",
            DisplayName = "Promo label",
            ExpectedFields = ["ArtName"],
            DefaultPrinter = "Test Printer",
            MasterFilters =
            [
                new ColumnFilter
                {
                    ColumnName = "Category",
                    Pattern = "^Promo$",
                    IsEnabled = true
                }
            ],
            LabelCount = new LabelCountSettings
            {
                IsEnabled = true,
                ColumnName = "LabelCount",
                DefaultCount = 1
            }
        };

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> PrimaryRows() =>
    [
        Row(("ItemId", "100"), ("Category", "Promo")),
        Row(("ItemId", "200"), ("Category", "Regular"))
    ];

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);

    private sealed class FakeConfigurationStore(
        DataSourceProfile dataSource,
        LabelTemplateProfile template) : IConfigurationStore
    {
        public Task<LabelGeneratorConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LabelGeneratorConfiguration
            {
                DataSources = [dataSource],
                LabelTemplates = [template]
            });

        public Task SaveAsync(LabelGeneratorConfiguration configuration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeDataSourceService(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> primaryRows,
        IReadOnlyDictionary<string, IReadOnlyList<string>> lookupKeys,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> detailRows) : IDataSourceService
    {
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> LoadPrimaryRowsAsync(
            DataSourceProfile profile,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(primaryRows);

        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> LoadDetailRowsAsync(
            DataSourceProfile profile,
            IEnumerable<object?> keys,
            CancellationToken cancellationToken = default)
        {
            var rows = keys
                .Select(Convert.ToString)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .SelectMany(key => detailRows.TryGetValue(key!, out var value)
                    ? value
                    : [])
                .ToList();

            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(rows);
        }

        public Task<IReadOnlyList<string>> LookupKeysAsync(
            DataSourceProfile profile,
            string scanValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(lookupKeys.TryGetValue(scanValue, out var keys)
                ? keys
                : []);
    }

    private sealed class FakePrinterService : IPrinterService
    {
        public int PrintCallCount { get; private set; }

        public PrintRequest? LastRequest { get; private set; }

        public IReadOnlyList<string> GetPrinterNames() => ["Test Printer"];

        public void ShowPreview(
            LabelTemplateProfile template,
            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
            PrintRequest request)
        {
        }

        public PrintResult Print(
            LabelTemplateProfile template,
            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
            PrintRequest request)
        {
            PrintCallCount++;
            LastRequest = request;
            return PrintResult.Success(LabelQuantityResolver.GetTotalLabelCount(template, rows, request.Copies));
        }
    }

    private sealed class NoopDesignerLauncher : IDesignerLauncher
    {
        public void Open(LabelTemplateProfile template)
        {
        }
    }

    private sealed class FakeAuditLogger : IAuditLogger
    {
        public Task WriteAsync(string message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
