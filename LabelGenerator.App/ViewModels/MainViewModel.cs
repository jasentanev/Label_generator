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
using LabelGenerator.Core.Localization;

namespace LabelGenerator.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IConfigurationStore configurationStore;
    private readonly IDataSourceService dataSourceService;
    private readonly IColumnFilterService filterService;
    private readonly IPrinterService printerService;
    private readonly IDesignerLauncher designerLauncher;
    private readonly IAuditLogger auditLogger;
    private readonly StartupOptions startupOptions;

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
    private string applicationLanguage = "en";
    private int selectedPrimaryCount;
    private bool isShowingOnlySelectedRows;

    public MainViewModel(
        IConfigurationStore configurationStore,
        IDataSourceService dataSourceService,
        IColumnFilterService filterService,
        IPrinterService printerService,
        IDesignerLauncher designerLauncher,
        IAuditLogger auditLogger,
        StartupOptions? startupOptions = null)
    {
        this.configurationStore = configurationStore;
        this.dataSourceService = dataSourceService;
        this.filterService = filterService;
        this.printerService = printerService;
        this.designerLauncher = designerLauncher;
        this.auditLogger = auditLogger;
        this.startupOptions = startupOptions ?? new StartupOptions();

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

    public bool ShowConfigureButton => startupOptions.ShowConfigureButton;

    public bool ShowDesignerButton => startupOptions.ShowDesignerButton;

    public bool ShowDataSourceSelector => startupOptions.ShowDataSourceSelector;

    public bool ShowTemplateSelector => startupOptions.ShowTemplateSelector;

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

    public string ApplicationLanguage
    {
        get => applicationLanguage;
        private set => SetProperty(ref applicationLanguage, UiTextLocalizer.NormalizeLanguage(value));
    }

    public int SelectedPrimaryCount
    {
        get => selectedPrimaryCount;
        private set => SetProperty(ref selectedPrimaryCount, value);
    }

    public bool IsShowingOnlySelectedRows => isShowingOnlySelectedRows;

    public async Task InitializeAsync()
    {
        App.WriteStartupLog("MainViewModel.InitializeAsync start");
        IsBusy = true;
        try
        {
            var configuration = await configurationStore.LoadAsync();
            App.WriteStartupLog("MainViewModel configuration loaded");
            ApplicationLanguage = configuration.Application.Language;

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

            var printerLoadStatus = await LoadPrintersWithTimeoutAsync();

            var startupMessages = new List<string>();
            var dataSourceRequested = !string.IsNullOrWhiteSpace(startupOptions.DataSource);
            var labelRequested = !string.IsNullOrWhiteSpace(startupOptions.Label);
            var resolvedDataSource = ResolveDataSource(startupOptions.DataSource);
            var resolvedTemplate = ResolveTemplate(startupOptions.Label);

            if (dataSourceRequested && resolvedDataSource is null)
            {
                startupMessages.Add($"Data source '{startupOptions.DataSource}' was not found.");
            }

            if (labelRequested && resolvedTemplate is null)
            {
                startupMessages.Add($"Label '{startupOptions.Label}' was not found.");
            }

            SelectedDataSource = dataSourceRequested ? resolvedDataSource : DataSources.FirstOrDefault();
            SelectedTemplate = labelRequested ? resolvedTemplate : Templates.FirstOrDefault();
            SelectedPrinter = Printers.FirstOrDefault() ?? string.Empty;
            SelectTemplatePrinter();
            var startupSummary = BuildStartupSummary();
            StatusMessage = string.Join(" ", new[]
            {
                "Configuration loaded.",
                string.Join(" ", startupMessages),
                startupSummary,
                printerLoadStatus
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
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

    public async Task ExecuteStartupActionAsync()
    {
        if (startupOptions.ActionMode == StartupActionMode.None)
        {
            return;
        }

        if (SelectedDataSource is null || SelectedTemplate is null)
        {
            StatusMessage = "Startup action skipped: data source or label template was not found.";
            return;
        }

        if (Printers.Count > 0)
        {
            SelectedPrinter = Printers[0];
        }

        await LoadPrimaryAsync();
        if (primaryRows.Count == 0)
        {
            StatusMessage = "Startup action skipped: primary view returned no rows.";
            return;
        }

        if (startupOptions.ActionMode == StartupActionMode.Load)
        {
            StatusMessage = $"Startup load completed: {primaryRows.Count} primary row(s).";
            return;
        }

        if (startupOptions.ActionMode == StartupActionMode.Preview)
        {
            await PreviewAsync();
            return;
        }

        await PrintAsync();
    }

    private async Task<string> LoadPrintersWithTimeoutAsync()
    {
        IReadOnlyList<string> printers;
        try
        {
            App.WriteStartupLog("MainViewModel printer load start");
            printers = await Task
                .Run(printerService.GetPrinterNames)
                .WaitAsync(TimeSpan.FromSeconds(5));
            App.WriteStartupLog($"MainViewModel printer load completed count={printers.Count}");
        }
        catch (TimeoutException)
        {
            App.WriteStartupLog("MainViewModel printer load timeout");
            Printers.Clear();
            return "Printer list timed out; preview still works and print can use the default dialog.";
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"MainViewModel printer load failed: {ex}");
            Printers.Clear();
            return $"Printer list failed: {ex.Message}";
        }

        Printers.Clear();
        foreach (var printer in printers)
        {
            Printers.Add(printer);
        }

        return printers.Count == 0
            ? "No Windows printers were found."
            : string.Empty;
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

    public IReadOnlyList<string> GetSelectedKeyStrings() =>
        selectedKeyValues
            .Select(Convert.ToString)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void ClearSelectedPrimaryRows()
    {
        selectedKeyValues.Clear();
        SelectedPrimaryCount = 0;
        detailKeysSignature = string.Empty;
        RaiseCommandStates();
    }

    public bool ShowOnlySelectedRows()
    {
        if (SelectedDataSource is null)
        {
            StatusMessage = "Select a data source first.";
            return false;
        }

        var selectedKeys = GetSelectedKeyStrings();
        if (selectedKeys.Count == 0)
        {
            StatusMessage = "Select master rows first.";
            return false;
        }

        isShowingOnlySelectedRows = true;
        var selectedRowCount = SetPrimaryViewToSelectedRows(selectedKeys);
        StatusMessage = $"Showing {selectedRowCount} selected row(s) from {filteredRows.Count} filtered rows.";
        RaiseCommandStates();
        return true;
    }

    public void ShowFilteredRows()
    {
        isShowingOnlySelectedRows = false;
        PrimaryRowsView = TabularDataBuilder.ToDataTable(filteredRows).DefaultView;
        StatusMessage = $"Showing all {filteredRows.Count} filtered row(s).";
        RaiseCommandStates();
    }

    public IReadOnlyList<string> AddMarkedKeysToSelection(IReadOnlyCollection<string> keys, out int matchedCount)
    {
        matchedCount = 0;
        if (SelectedDataSource is null || keys.Count == 0)
        {
            return GetSelectedKeyStrings();
        }

        var lookupKeySet = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedKeys = filteredRows
            .Select(row => row.TryGetValue(SelectedDataSource.KeyColumn, out var value) ? Convert.ToString(value) : null)
            .Where(value => !string.IsNullOrWhiteSpace(value) && lookupKeySet.Contains(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        matchedCount = matchedKeys.Count;
        if (matchedKeys.Count == 0)
        {
            return GetSelectedKeyStrings();
        }

        var selectedKeySet = GetSelectedKeyStrings().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in matchedKeys)
        {
            if (selectedKeySet.Add(key))
            {
                selectedKeyValues.Add(key);
            }
        }

        SelectedPrimaryCount = selectedKeyValues.Count;
        detailKeysSignature = string.Empty;
        var selectedKeys = GetSelectedKeyStrings();
        if (isShowingOnlySelectedRows)
        {
            SetPrimaryViewToSelectedRows(selectedKeys);
        }

        RaiseCommandStates();
        return selectedKeys;
    }

    public async Task<IReadOnlyList<string>> LookupScanKeysAsync(string scanValue)
    {
        if (SelectedDataSource is null)
        {
            StatusMessage = "Select a data source first.";
            return [];
        }

        if (primaryRows.Count == 0)
        {
            StatusMessage = "Load the primary view before scanning.";
            return [];
        }

        if (string.IsNullOrWhiteSpace(scanValue))
        {
            StatusMessage = "Enter or scan a value first.";
            return [];
        }

        try
        {
            var keys = await dataSourceService.LookupKeysAsync(SelectedDataSource, scanValue);
            StatusMessage = keys.Count == 0
                ? $"Scan '{scanValue}' returned no keys."
                : $"Scan '{scanValue}' returned {keys.Count} key(s).";
            return keys;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan lookup failed: {ex.Message}";
            return [];
        }
    }

    public async Task<QuickMarkPrintResult> QuickMarkAndPrintAsync(string scanValue)
    {
        var normalizedScanValue = scanValue.Trim();
        if (string.IsNullOrWhiteSpace(normalizedScanValue))
        {
            StatusMessage = "Enter or scan a value first.";
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.NoScanValue,
                normalizedScanValue,
                StatusMessage);
        }

        if (SelectedDataSource is null)
        {
            StatusMessage = "Select a data source first.";
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.NoKey,
                normalizedScanValue,
                StatusMessage);
        }

        if (SelectedTemplate is null)
        {
            StatusMessage = "Select a template first.";
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.TemplateInvalid,
                normalizedScanValue,
                StatusMessage);
        }

        if (primaryRows.Count == 0)
        {
            await LoadPrimaryAsync();
        }

        if (primaryRows.Count == 0 || filteredRows.Count == 0)
        {
            StatusMessage = "Primary view returned no rows for the current filters.";
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.NoKey,
                normalizedScanValue,
                StatusMessage);
        }

        var lookupKeys = await LookupScanKeysAsync(normalizedScanValue);
        if (lookupKeys.Count == 0)
        {
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.NoKey,
                normalizedScanValue,
                StatusMessage);
        }

        var matchedKeys = MatchKeysInFilteredMaster(lookupKeys);
        if (matchedKeys.Count == 0)
        {
            StatusMessage = $"Scan '{normalizedScanValue}' returned {lookupKeys.Count} key(s), but none are in the filtered master view.";
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.NotInFilteredMaster,
                normalizedScanValue,
                StatusMessage);
        }

        await LoadDetailsForKeysAsync(matchedKeys.Cast<object?>().ToList());
        if (detailRows.Count == 0)
        {
            StatusMessage = $"No detail rows were loaded for scan '{normalizedScanValue}'.";
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.NoDetailRows,
                normalizedScanValue,
                StatusMessage);
        }

        var validation = templateRepository.ValidateFields(SelectedTemplate, detailRows);
        if (!validation.IsValid)
        {
            StatusMessage = "Template fields missing: " + string.Join(", ", validation.MissingFields);
            return QuickMarkPrintResult.Failed(
                QuickMarkPrintStatus.TemplateInvalid,
                normalizedScanValue,
                StatusMessage);
        }

        var request = new PrintRequest
        {
            SelectedKeys = matchedKeys,
            TemplateId = SelectedTemplate.Id,
            PrinterName = !string.IsNullOrWhiteSpace(SelectedPrinter)
                ? SelectedPrinter
                : SelectedTemplate.DefaultPrinter,
            Copies = 1,
            StartLabelPosition = 1,
            Mode = PrintMode.DirectPrint
        };

        var printResult = printerService.Print(SelectedTemplate, detailRows, request);
        if (printResult.Status != PrintStatus.Success)
        {
            StatusMessage = $"Quick print failed: {printResult.ErrorMessage}";
            await auditLogger.WriteAsync(
                $"QuickMarkPrint status={printResult.Status}; scan={normalizedScanValue}; template={request.TemplateId}; printer={request.PrinterName}; rows={detailRows.Count}; error={printResult.ErrorMessage}");
            return new QuickMarkPrintResult
            {
                Status = QuickMarkPrintStatus.PrinterError,
                ScanValue = normalizedScanValue,
                Keys = matchedKeys,
                DetailRowCount = detailRows.Count,
                PrinterName = request.PrinterName,
                Message = StatusMessage
            };
        }

        await auditLogger.WriteAsync(
            $"QuickMarkPrint status={printResult.Status}; scan={normalizedScanValue}; template={request.TemplateId}; printer={request.PrinterName}; rows={detailRows.Count}; labels={printResult.PrintedCount}; copies=1; labelCountColumn={SelectedTemplate.LabelCount.ColumnName}");

        ClearSelectedPrimaryRows();
        StatusMessage = $"Quick printed {printResult.PrintedCount} labels from {detailRows.Count} detail rows.";
        return new QuickMarkPrintResult
        {
            Status = QuickMarkPrintStatus.Success,
            ScanValue = normalizedScanValue,
            Keys = matchedKeys,
            DetailRowCount = detailRows.Count,
            PrintedLabelCount = printResult.PrintedCount,
            PrinterName = request.PrinterName,
            Message = StatusMessage
        };
    }

    public void SetScanStatus(int keyCount, int selectedCount)
    {
        StatusMessage = selectedCount == 0
            ? $"Scan returned {keyCount} key(s), but none are visible in the loaded primary view."
            : $"Marked {selectedCount} row(s) from {keyCount} lookup key(s).";
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
        isShowingOnlySelectedRows = false;
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
        if (!await EnsureDetailsForCurrentKeysAsync(forceReload: true))
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

    private DataSourceProfile? ResolveDataSource(string? requestedDataSource)
    {
        if (string.IsNullOrWhiteSpace(requestedDataSource))
        {
            return null;
        }

        return DataSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, requestedDataSource, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.DisplayName, requestedDataSource, StringComparison.CurrentCultureIgnoreCase));
    }

    private LabelTemplateProfile? ResolveTemplate(string? requestedLabel)
    {
        if (string.IsNullOrWhiteSpace(requestedLabel))
        {
            return null;
        }

        return Templates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, requestedLabel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.DisplayName, requestedLabel, StringComparison.CurrentCultureIgnoreCase));
    }

    private string BuildStartupSummary()
    {
        var parts = new List<string>();
        if (startupOptions.UserMode)
        {
            parts.Add("User mode.");
        }

        if (!string.IsNullOrWhiteSpace(startupOptions.DataSource))
        {
            parts.Add($"Data source fixed: {SelectedDataSource?.DisplayName ?? startupOptions.DataSource}.");
        }

        if (!string.IsNullOrWhiteSpace(startupOptions.Label))
        {
            parts.Add($"Label fixed: {SelectedTemplate?.DisplayName ?? startupOptions.Label}.");
        }

        if (startupOptions.ActionMode != StartupActionMode.None)
        {
            parts.Add($"Startup action: {startupOptions.ActionMode}.");
        }

        return string.Join(" ", parts);
    }

    private async Task<bool> EnsureDetailsForCurrentKeysAsync(bool forceReload = false)
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
        if (forceReload || detailRows.Count == 0 || !string.Equals(signature, detailKeysSignature, StringComparison.Ordinal))
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
        detailRows.Clear();
        detailKeysSignature = string.Empty;
        DetailRowsView = null;
        try
        {
            var rows = await dataSourceService.LoadDetailRowsAsync(SelectedDataSource, keys);
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

    private List<string> MatchKeysInFilteredMaster(IReadOnlyList<string> lookupKeys)
    {
        if (SelectedDataSource is null)
        {
            return [];
        }

        var filteredKeySet = filteredRows
            .Select(row => row.TryGetValue(SelectedDataSource.KeyColumn, out var value) ? Convert.ToString(value) : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return lookupKeys
            .Where(key => !string.IsNullOrWhiteSpace(key) && filteredKeySet.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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

    private int SetPrimaryViewToSelectedRows(IReadOnlyCollection<string> selectedKeys)
    {
        if (SelectedDataSource is null)
        {
            PrimaryRowsView = TabularDataBuilder.ToDataTable(Array.Empty<IReadOnlyDictionary<string, object?>>()).DefaultView;
            return 0;
        }

        var keySet = selectedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedRows = filteredRows
            .Where(row => row.TryGetValue(SelectedDataSource.KeyColumn, out var value)
                && value is not null
                && keySet.Contains(Convert.ToString(value) ?? string.Empty))
            .ToList();

        PrimaryRowsView = TabularDataBuilder.ToDataTable(selectedRows).DefaultView;
        return selectedRows.Count;
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
        isShowingOnlySelectedRows = false;
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
