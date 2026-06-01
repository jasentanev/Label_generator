using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using LabelGenerator.App.Printing;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Audit;
using LabelGenerator.Core.Services.Configuration;
using LabelGenerator.Core.Services.DataSources;
using LabelGenerator.Core.Services.Filtering;
using LabelGenerator.Core.Services.Printing;
using LabelGenerator.Core.Services.Templates;
using LabelGenerator.Core.Utilities;

namespace LabelGenerator.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IConfigurationStore configurationStore;
    private readonly IDataSourceService dataSourceService;
    private readonly IColumnFilterService filterService;
    private readonly IPrinterService printerService;
    private readonly IDesignerLauncher designerLauncher;
    private readonly IAuditLogger auditLogger;

    private readonly List<IReadOnlyDictionary<string, object?>> primaryRows = [];
    private readonly List<IReadOnlyDictionary<string, object?>> filteredRows = [];
    private readonly List<IReadOnlyDictionary<string, object?>> detailRows = [];
    private readonly List<object?> selectedKeyValues = [];
    private ITemplateRepository templateRepository = new TemplateRepository([]);
    private string detailKeysSignature = string.Empty;

    private DataSourceProfile? selectedDataSource;
    private LabelTemplateProfile? selectedTemplate;
    private DataView? primaryRowsView;
    private DataView? detailRowsView;
    private string selectedPrinter = string.Empty;
    private int copies = 1;
    private int startLabelPosition = 1;
    private bool isBusy;
    private string statusMessage = "Load a data source to start.";
    private int selectedPrimaryCount;

    public MainViewModel(
        IConfigurationStore configurationStore,
        IDataSourceService dataSourceService,
        IColumnFilterService filterService,
        IPrinterService printerService,
        IDesignerLauncher designerLauncher,
        IAuditLogger auditLogger)
    {
        this.configurationStore = configurationStore;
        this.dataSourceService = dataSourceService;
        this.filterService = filterService;
        this.printerService = printerService;
        this.designerLauncher = designerLauncher;
        this.auditLogger = auditLogger;

        LoadPrimaryCommand = new AsyncRelayCommand(LoadPrimaryAsync, CanLoadPrimary);
        LoadDetailsCommand = new AsyncRelayCommand(LoadDetailsAsync, CanUseRows);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, CanUseRows);
        PrintCommand = new AsyncRelayCommand(PrintAsync, CanUseRows);
        ApplyFiltersCommand = new RelayCommand(ApplyFilters, () => primaryRows.Count > 0 && !IsBusy);
        ClearFiltersCommand = new RelayCommand(ClearFilters, () => Filters.Count > 0 && !IsBusy);
        OpenDesignerCommand = new RelayCommand(OpenDesigner, () => SelectedTemplate is not null && !IsBusy);
    }

    public ObservableCollection<DataSourceProfile> DataSources { get; } = [];

    public ObservableCollection<LabelTemplateProfile> Templates { get; } = [];

    public ObservableCollection<ColumnFilterViewModel> Filters { get; } = [];

    public ObservableCollection<string> Printers { get; } = [];

    public AsyncRelayCommand LoadPrimaryCommand { get; }

    public AsyncRelayCommand LoadDetailsCommand { get; }

    public AsyncRelayCommand PreviewCommand { get; }

    public AsyncRelayCommand PrintCommand { get; }

    public RelayCommand ApplyFiltersCommand { get; }

    public RelayCommand ClearFiltersCommand { get; }

    public RelayCommand OpenDesignerCommand { get; }

    public DataSourceProfile? SelectedDataSource
    {
        get => selectedDataSource;
        set
        {
            if (SetProperty(ref selectedDataSource, value))
            {
                ClearRows();
                RaiseCommandStates();
            }
        }
    }

    public LabelTemplateProfile? SelectedTemplate
    {
        get => selectedTemplate;
        set
        {
            if (SetProperty(ref selectedTemplate, value))
            {
                StartLabelPosition = 1;
                SelectTemplatePrinter();
                if (primaryRows.Count > 0)
                {
                    ApplyCurrentFilters("Template changed; saved master filters applied.");
                }
                else
                {
                    RaiseCommandStates();
                }
            }
        }
    }

    public DataView? PrimaryRowsView
    {
        get => primaryRowsView;
        private set => SetProperty(ref primaryRowsView, value);
    }

    public DataView? DetailRowsView
    {
        get => detailRowsView;
        private set => SetProperty(ref detailRowsView, value);
    }

    public string SelectedPrinter
    {
        get => selectedPrinter;
        set => SetProperty(ref selectedPrinter, value);
    }

    public int Copies
    {
        get => copies;
        set => SetProperty(ref copies, Math.Max(1, value));
    }

    public int StartLabelPosition
    {
        get => startLabelPosition;
        set
        {
            var labelsPerPage = SelectedTemplate?.Sheet.LabelsPerPage ?? 1;
            SetProperty(ref startLabelPosition, Math.Clamp(value, 1, labelsPerPage));
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public int SelectedPrimaryCount
    {
        get => selectedPrimaryCount;
        private set => SetProperty(ref selectedPrimaryCount, value);
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var configuration = await configurationStore.LoadAsync();

            DataSources.Clear();
            foreach (var profile in configuration.DataSources)
            {
                DataSources.Add(profile);
            }

            Templates.Clear();
            foreach (var template in configuration.LabelTemplates)
            {
                Templates.Add(template);
            }

            templateRepository = new TemplateRepository(configuration.LabelTemplates);

            Printers.Clear();
            foreach (var printer in printerService.GetPrinterNames())
            {
                Printers.Add(printer);
            }

            SelectedDataSource = DataSources.FirstOrDefault();
            SelectedTemplate = Templates.FirstOrDefault();
            SelectedPrinter = Printers.FirstOrDefault() ?? string.Empty;
            SelectTemplatePrinter();
            StatusMessage = "Configuration loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Configuration error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetSelectedPrimaryRows(IList selectedItems)
    {
        selectedKeyValues.Clear();

        if (SelectedDataSource is not null)
        {
            foreach (var key in selectedItems
                         .OfType<DataRowView>()
                         .Select(row => ReadRowValue(row, SelectedDataSource.KeyColumn))
                         .Where(value => value is not null && value is not DBNull))
            {
                selectedKeyValues.Add(key);
            }
        }

        SelectedPrimaryCount = selectedKeyValues.Count;
        detailKeysSignature = string.Empty;
        RaiseCommandStates();
    }

    private async Task LoadPrimaryAsync()
    {
        if (SelectedDataSource is null)
        {
            StatusMessage = "Select a data source first.";
            return;
        }

        IsBusy = true;
        try
        {
            var rows = await dataSourceService.LoadPrimaryRowsAsync(SelectedDataSource);
            primaryRows.Clear();
            primaryRows.AddRange(rows);
            detailRows.Clear();
            detailKeysSignature = string.Empty;
            DetailRowsView = null;
            SelectedPrimaryCount = 0;

            RebuildFilters();
            ApplyCurrentFilters($"Loaded {primaryRows.Count} primary rows from {SelectedDataSource.DisplayName}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDetailsAsync()
    {
        var keys = GetCurrentKeys();
        if (keys.Count == 0)
        {
            StatusMessage = "Select rows, or load a primary view with at least one row.";
            return;
        }

        await LoadDetailsForKeysAsync(keys);
    }

    private void ApplyFilters()
    {
        ApplyCurrentFilters();
    }

    private bool ApplyCurrentFilters(string? successPrefix = null)
    {
        var filterModels = GetCombinedFilters();
        var validation = filterService.Validate(filterModels);
        if (!validation.IsValid)
        {
            StatusMessage = string.Join(" | ", validation.Errors);
            return false;
        }

        filteredRows.Clear();
        filteredRows.AddRange(filterService.Apply(primaryRows, filterModels));
        PrimaryRowsView = TabularDataBuilder.ToDataTable(filteredRows).DefaultView;
        selectedKeyValues.Clear();
        SelectedPrimaryCount = 0;
        detailRows.Clear();
        detailKeysSignature = string.Empty;
        DetailRowsView = null;
        var templateFilterCount = SelectedTemplate?.MasterFilters.Count(filter => filter.IsActive) ?? 0;
        var manualFilterCount = Filters.Count(filter => filter.ToModel().IsActive);
        var filterSummary = templateFilterCount + manualFilterCount > 0
            ? $" ({templateFilterCount} template + {manualFilterCount} manual filters)"
            : string.Empty;
        StatusMessage = $"{successPrefix ?? "Filter applied"}: {filteredRows.Count} of {primaryRows.Count} rows{filterSummary}.";
        RaiseCommandStates();
        return true;
    }

    private void ClearFilters()
    {
        foreach (var filter in Filters)
        {
            filter.Clear();
        }

        ApplyCurrentFilters("Manual filters cleared");
    }

    private List<ColumnFilter> GetCombinedFilters()
    {
        var filters = new List<ColumnFilter>();

        if (SelectedTemplate is not null)
        {
            filters.AddRange(SelectedTemplate.MasterFilters);
        }

        filters.AddRange(Filters.Select(filter => filter.ToModel()));
        return filters;
    }

    private async Task PreviewAsync()
    {
        if (!await EnsureDetailsForCurrentKeysAsync())
        {
            return;
        }

        var request = BuildPrintRequest(PrintMode.Preview);
        printerService.ShowPreview(SelectedTemplate!, detailRows, request);
        StatusMessage = $"Preview generated for {GetTotalLabelCount()} labels from {detailRows.Count} detail rows.";
    }

    private async Task PrintAsync()
    {
        if (!await EnsureDetailsForCurrentKeysAsync())
        {
            return;
        }

        var request = BuildPrintRequest(PrintMode.DirectPrint);
        var result = printerService.Print(SelectedTemplate!, detailRows, request);

        await auditLogger.WriteAsync(
            $"Print status={result.Status}; template={request.TemplateId}; printer={request.PrinterName}; rows={detailRows.Count}; labels={result.PrintedCount}; copies={request.Copies}; labelCountColumn={SelectedTemplate?.LabelCount.ColumnName}");

        StatusMessage = result.Status == PrintStatus.Success
            ? $"Printed {result.PrintedCount} labels."
            : $"Print failed: {result.ErrorMessage}";
    }

    private void OpenDesigner()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        try
        {
            designerLauncher.Open(SelectedTemplate);
            StatusMessage = $"Opened designer for {SelectedTemplate.DisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Designer failed: {ex.Message}";
        }
    }

    private async Task<bool> EnsureDetailsForCurrentKeysAsync()
    {
        if (SelectedTemplate is null)
        {
            StatusMessage = "Select a template first.";
            return false;
        }

        var keys = GetCurrentKeys();
        if (keys.Count == 0)
        {
            StatusMessage = "No rows selected for printing.";
            return false;
        }

        var signature = BuildKeySignature(keys);
        if (detailRows.Count == 0 || !string.Equals(signature, detailKeysSignature, StringComparison.Ordinal))
        {
            await LoadDetailsForKeysAsync(keys);
        }

        var validation = templateRepository.ValidateFields(SelectedTemplate, detailRows);
        if (!validation.IsValid)
        {
            StatusMessage = "Template fields missing: " + string.Join(", ", validation.MissingFields);
            return false;
        }

        return detailRows.Count > 0;
    }

    private async Task LoadDetailsForKeysAsync(IReadOnlyList<object?> keys)
    {
        if (SelectedDataSource is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var rows = await dataSourceService.LoadDetailRowsAsync(SelectedDataSource, keys);
            detailRows.Clear();
            detailRows.AddRange(rows);
            detailKeysSignature = BuildKeySignature(keys);
            DetailRowsView = TabularDataBuilder.ToDataTable(detailRows).DefaultView;
            StatusMessage = $"Loaded {detailRows.Count} detail rows for {keys.Count} key(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Detail load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private PrintRequest BuildPrintRequest(PrintMode mode)
    {
        var keys = GetCurrentKeys();
        return new PrintRequest
        {
            SelectedKeys = keys
                .Select(Convert.ToString)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList(),
            TemplateId = SelectedTemplate?.Id ?? string.Empty,
            PrinterName = SelectedPrinter,
            Copies = Copies,
            StartLabelPosition = StartLabelPosition,
            Mode = mode
        };
    }

    private int GetTotalLabelCount() =>
        SelectedTemplate is null
            ? 0
            : LabelQuantityResolver.GetTotalLabelCount(SelectedTemplate, detailRows, Copies);

    private IReadOnlyList<object?> GetCurrentKeys()
    {
        if (selectedKeyValues.Count > 0)
        {
            return selectedKeyValues.ToList();
        }

        if (SelectedDataSource is null)
        {
            return [];
        }

        return filteredRows
            .Select(row => row.TryGetValue(SelectedDataSource.KeyColumn, out var value) ? value : null)
            .Where(value => value is not null)
            .ToList();
    }

    private static string BuildKeySignature(IEnumerable<object?> keys) =>
        string.Join('\u001F', keys.Select(Convert.ToString));

    private static object? ReadRowValue(DataRowView row, string columnName) =>
        row.Row.Table.Columns.Contains(columnName) ? row.Row[columnName] : null;

    private void RebuildFilters()
    {
        Filters.Clear();

        var columns = SelectedDataSource?.VisibleColumns.Count > 0
            ? SelectedDataSource.VisibleColumns
            : primaryRows.SelectMany(row => row.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var column in columns)
        {
            Filters.Add(new ColumnFilterViewModel(column));
        }
    }

    private void SelectTemplatePrinter()
    {
        if (SelectedTemplate is null || string.IsNullOrWhiteSpace(SelectedTemplate.DefaultPrinter))
        {
            return;
        }

        var configuredPrinter = Printers.FirstOrDefault(printer =>
            string.Equals(printer, SelectedTemplate.DefaultPrinter, StringComparison.CurrentCultureIgnoreCase));

        if (!string.IsNullOrWhiteSpace(configuredPrinter))
        {
            SelectedPrinter = configuredPrinter;
        }
    }

    private void ClearRows()
    {
        primaryRows.Clear();
        filteredRows.Clear();
        detailRows.Clear();
        selectedKeyValues.Clear();
        detailKeysSignature = string.Empty;
        Filters.Clear();
        PrimaryRowsView = null;
        DetailRowsView = null;
        SelectedPrimaryCount = 0;
    }

    private bool CanLoadPrimary() => SelectedDataSource is not null && !IsBusy;

    private bool CanUseRows() => SelectedDataSource is not null
        && SelectedTemplate is not null
        && primaryRows.Count > 0
        && !IsBusy;

    private void RaiseCommandStates()
    {
        LoadPrimaryCommand.RaiseCanExecuteChanged();
        LoadDetailsCommand.RaiseCanExecuteChanged();
        PreviewCommand.RaiseCanExecuteChanged();
        PrintCommand.RaiseCanExecuteChanged();
        ApplyFiltersCommand.RaiseCanExecuteChanged();
        ClearFiltersCommand.RaiseCanExecuteChanged();
        OpenDesignerCommand.RaiseCanExecuteChanged();
    }
}
