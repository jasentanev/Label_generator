using System.Windows;
using System.IO;
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

        viewModel = new MainViewModel(
            new JsonConfigurationStore(configurationPath),
            new DataSourceService(),
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
}
