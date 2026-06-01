using System.Windows;
using System.Data;
using System.IO;
using System.Windows.Input;
using LabelGenerator.App.Designing;
using LabelGenerator.App.Printing;
using LabelGenerator.App.ViewModels;
using LabelGenerator.Core.Services.Audit;
using LabelGenerator.Core.Services.Configuration;
using LabelGenerator.Core.Services.DataSources;
using LabelGenerator.Core.Services.Filtering;
using LabelGenerator.Core.Services.Templates;

namespace LabelGenerator.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly JsonConfigurationStore configurationStore;
    private readonly DataSourceService dataSourceService;

    public MainWindow()
    {
        InitializeComponent();

        var baseDirectory = AppContext.BaseDirectory;
        var bundledConfigurationPath = Path.Combine(baseDirectory, "Config", "appsettings.json");
        var configurationPath = ConfigurationPathResolver.ResolveSharedConfigurationPath(bundledConfigurationPath);
        var assetBaseDirectory = Path.GetDirectoryName(configurationPath) ?? baseDirectory;
        var auditPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelGenerator",
            "audit.log");
        configurationStore = new JsonConfigurationStore(configurationPath);
        dataSourceService = new DataSourceService();

        viewModel = new MainViewModel(
            configurationStore,
            dataSourceService,
            new RegexColumnFilterService(),
            new WpfPrinterService(assetBaseDirectory),
            new DesignerAppLauncher(baseDirectory),
            new FileAuditLogger(auditPath));

        DataContext = viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await viewModel.InitializeAsync();
    }

    private void PrimaryDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        viewModel.SetSelectedPrimaryRows(PrimaryDataGrid.SelectedItems);
    }

    private async void ScanTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        await MarkScanValueAsync();
        e.Handled = true;
    }

    private async void MarkScanButton_Click(object sender, RoutedEventArgs e)
    {
        await MarkScanValueAsync();
    }

    private async Task MarkScanValueAsync()
    {
        var keys = await viewModel.LookupScanKeysAsync(ScanTextBox.Text);
        if (keys.Count == 0)
        {
            return;
        }

        var selectedCount = SelectRowsByKeys(keys);
        viewModel.SetSelectedPrimaryRows(PrimaryDataGrid.SelectedItems);
        viewModel.SetScanStatus(keys.Count, selectedCount);

        if (selectedCount > 0)
        {
            ScanTextBox.Clear();
        }
    }

    private void OnlySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.SetSelectedPrimaryRows(PrimaryDataGrid.SelectedItems);
        var selectedKeys = viewModel.GetSelectedKeyStrings();
        if (!viewModel.ShowOnlySelectedRows())
        {
            return;
        }

        SelectRowsByKeys(selectedKeys);
        viewModel.SetSelectedPrimaryRows(PrimaryDataGrid.SelectedItems);
    }

    private void ShowFilteredRowsButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedKeys = viewModel.GetSelectedKeyStrings();
        viewModel.ShowFilteredRows();
        SelectRowsByKeys(selectedKeys);
        viewModel.SetSelectedPrimaryRows(PrimaryDataGrid.SelectedItems);
    }

    private int SelectRowsByKeys(IReadOnlyCollection<string> keys)
    {
        var keyColumn = viewModel.SelectedDataSource?.KeyColumn;
        if (string.IsNullOrWhiteSpace(keyColumn))
        {
            return 0;
        }

        var keySet = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedCount = 0;
        DataRowView? firstSelected = null;

        foreach (var item in PrimaryDataGrid.Items.OfType<DataRowView>())
        {
            if (!item.Row.Table.Columns.Contains(keyColumn))
            {
                continue;
            }

            var value = item.Row[keyColumn];
            var text = value is null or DBNull ? string.Empty : Convert.ToString(value);
            if (string.IsNullOrWhiteSpace(text) || !keySet.Contains(text))
            {
                continue;
            }

            if (!PrimaryDataGrid.SelectedItems.Contains(item))
            {
                PrimaryDataGrid.SelectedItems.Add(item);
            }

            firstSelected ??= item;
            selectedCount++;
        }

        if (firstSelected is not null)
        {
            PrimaryDataGrid.ScrollIntoView(firstSelected);
        }

        return selectedCount;
    }

    private async void ConfigureDataSourcesButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new DataSourceConfigWindow(configurationStore, dataSourceService)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            await viewModel.InitializeAsync();
        }
    }
}
