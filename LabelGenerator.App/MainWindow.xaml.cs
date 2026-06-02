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
    private readonly StartupOptions startupOptions;

    public MainWindow()
    {
        App.WriteStartupLog("MainWindow.ctor before InitializeComponent");
        InitializeComponent();
        App.WriteStartupLog("MainWindow.ctor after InitializeComponent");
        startupOptions = StartupOptions.Parse(Environment.GetCommandLineArgs().Skip(1));
        App.WriteStartupLog($"Startup options: users={startupOptions.UserMode}; label={startupOptions.Label}; datasource={startupOptions.DataSource}; action={startupOptions.ActionMode}");

        var baseDirectory = AppContext.BaseDirectory;
        var bundledConfigurationPath = Path.Combine(baseDirectory, "Config", "appsettings.json");
        var configurationPath = ConfigurationPathResolver.ResolveSharedConfigurationPath(bundledConfigurationPath);
        App.WriteStartupLog($"MainWindow configurationPath={configurationPath}");
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
            new FileAuditLogger(auditPath),
            startupOptions);

        DataContext = viewModel;
        ContentRendered += MainWindow_ContentRendered;
        App.WriteStartupLog("MainWindow.ctor completed");
    }

    private async void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= MainWindow_ContentRendered;
        App.WriteStartupLog("MainWindow.ContentRendered");
        await Task.Yield();
        await viewModel.InitializeAsync();
        await viewModel.ExecuteStartupActionAsync();
        App.WriteStartupLog("MainWindow.InitializeAsync completed");
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
            PlayMarkFailureSound();
            return;
        }

        int selectedCount;
        if (viewModel.IsShowingOnlySelectedRows)
        {
            var selectedKeys = viewModel.AddMarkedKeysToSelection(keys, out selectedCount);
            if (selectedCount > 0)
            {
                SelectRowsByKeys(selectedKeys);
                viewModel.SetSelectedPrimaryRows(PrimaryDataGrid.SelectedItems);
            }
        }
        else
        {
            selectedCount = SelectRowsByKeys(keys);
            viewModel.SetSelectedPrimaryRows(PrimaryDataGrid.SelectedItems);
        }

        viewModel.SetScanStatus(keys.Count, selectedCount);

        if (selectedCount > 0)
        {
            PlayMarkSuccessSound();
            ScanTextBox.Clear();
            return;
        }

        PlayMarkFailureSound();
    }

    private static void PlayMarkSuccessSound()
    {
        _ = Task.Run(() =>
        {
            try
            {
                Console.Beep(880, 90);
            }
            catch
            {
                System.Media.SystemSounds.Beep.Play();
            }
        });
    }

    private static void PlayMarkFailureSound()
    {
        _ = Task.Run(() =>
        {
            try
            {
                Console.Beep(220, 140);
                Console.Beep(180, 140);
            }
            catch
            {
                System.Media.SystemSounds.Hand.Play();
            }
        });
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
