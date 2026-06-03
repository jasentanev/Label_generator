using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using LabelGenerator.App.Localization;
using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Localization;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Configuration;
using LabelGenerator.Core.Services.DataSources;

namespace LabelGenerator.App;

public partial class DataSourceConfigWindow : Window
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IConfigurationStore configurationStore;
    private readonly IDataSourceService dataSourceService;
    private LabelGeneratorConfiguration configuration = new();
    private DataSourceProfile? selectedProfile;
    private bool isLoading;

    public DataSourceConfigWindow(IConfigurationStore configurationStore, IDataSourceService dataSourceService)
    {
        InitializeComponent();
        this.configurationStore = configurationStore;
        this.dataSourceService = dataSourceService;

        ProviderComboBox.ItemsSource = new[]
        {
            "Demo",
            "Microsoft.Data.SqlClient",
            "Npgsql",
            "MySqlConnector",
            "System.Data.Odbc"
        };
        LanguageComboBox.ItemsSource = new[]
        {
            new LanguageOption("en", "English"),
            new LanguageOption("bg", "Български")
        };
        LanguageComboBox.DisplayMemberPath = nameof(LanguageOption.DisplayName);
        LanguageComboBox.SelectedValuePath = nameof(LanguageOption.Code);

        Loaded += DataSourceConfigWindow_Loaded;
    }

    private async void DataSourceConfigWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= DataSourceConfigWindow_Loaded;
        var loadedConfiguration = await configurationStore.LoadAsync();
        configuration = CloneConfiguration(loadedConfiguration);
        LanguageComboBox.SelectedValue = UiTextLocalizer.NormalizeLanguage(configuration.Application.Language);
        RefreshList(configuration.DataSources.FirstOrDefault());
        StatusTextBlock.Text = "Data source configuration loaded.";
        WpfLocalization.Apply(this, configuration.Application.Language);
    }

    private static LabelGeneratorConfiguration CloneConfiguration(LabelGeneratorConfiguration source)
    {
        var json = JsonSerializer.Serialize(source, CloneOptions);
        return JsonSerializer.Deserialize<LabelGeneratorConfiguration>(json, CloneOptions) ?? new LabelGeneratorConfiguration();
    }

    private void RefreshList(DataSourceProfile? profileToSelect)
    {
        isLoading = true;
        DataSourcesListBox.ItemsSource = null;
        DataSourcesListBox.ItemsSource = configuration.DataSources;
        DataSourcesListBox.SelectedItem = profileToSelect;
        isLoading = false;

        selectedProfile = profileToSelect;
        LoadProfile(profileToSelect);
    }

    private void DataSourcesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        selectedProfile = DataSourcesListBox.SelectedItem as DataSourceProfile;
        LoadProfile(selectedProfile);
    }

    private void LoadProfile(DataSourceProfile? profile)
    {
        IdTextBox.Text = profile?.Id ?? string.Empty;
        DisplayNameTextBox.Text = profile?.DisplayName ?? string.Empty;
        ProviderComboBox.Text = profile?.ProviderInvariantName ?? "Microsoft.Data.SqlClient";
        ConnectionStringTextBox.Text = profile?.ConnectionSecret ?? string.Empty;
        PrimaryViewTextBox.Text = profile?.PrimaryView ?? string.Empty;
        DetailViewTextBox.Text = profile?.DetailView ?? string.Empty;
        PrimarySqlTextBox.Text = profile?.PrimarySql ?? string.Empty;
        DetailSqlTextBox.Text = profile?.DetailSql ?? string.Empty;
        LookupSqlTextBox.Text = profile?.LookupSql ?? string.Empty;
        LookupKeyColumnTextBox.Text = profile?.LookupKeyColumn ?? string.Empty;
        KeyColumnTextBox.Text = profile?.KeyColumn ?? "ProductCode";
        MaxRowsTextBox.Text = (profile?.MaxRows ?? 500).ToString();
        TimeoutTextBox.Text = (profile?.CommandTimeoutSeconds ?? 30).ToString();
        VisibleColumnsTextBox.Text = string.Join(Environment.NewLine, profile?.VisibleColumns ?? []);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            selectedProfile = CreateDefaultProfile("source");
            configuration.DataSources.Add(selectedProfile);
        }

        ApplyEditorToProfile(selectedProfile);
        RefreshList(selectedProfile);
        StatusTextBlock.Text = "Changes applied. Save to write them to configuration.";
    }

    private void ApplyEditorToProfile(DataSourceProfile profile)
    {
        profile.Id = string.IsNullOrWhiteSpace(IdTextBox.Text) ? profile.Id : IdTextBox.Text.Trim();
        profile.DisplayName = string.IsNullOrWhiteSpace(DisplayNameTextBox.Text) ? profile.Id : DisplayNameTextBox.Text.Trim();
        profile.ProviderInvariantName = string.IsNullOrWhiteSpace(ProviderComboBox.Text) ? "Microsoft.Data.SqlClient" : ProviderComboBox.Text.Trim();
        profile.ConnectionSecret = ConnectionStringTextBox.Text.Trim();
        profile.PrimaryView = PrimaryViewTextBox.Text.Trim();
        profile.DetailView = DetailViewTextBox.Text.Trim();
        profile.PrimarySql = PrimarySqlTextBox.Text.Trim();
        profile.DetailSql = DetailSqlTextBox.Text.Trim();
        profile.LookupSql = LookupSqlTextBox.Text.Trim();
        profile.LookupKeyColumn = LookupKeyColumnTextBox.Text.Trim();
        profile.KeyColumn = string.IsNullOrWhiteSpace(KeyColumnTextBox.Text) ? "ProductCode" : KeyColumnTextBox.Text.Trim();
        profile.MaxRows = Math.Max(1, ParseInt(MaxRowsTextBox.Text, profile.MaxRows));
        profile.CommandTimeoutSeconds = Math.Max(1, ParseInt(TimeoutTextBox.Text, profile.CommandTimeoutSeconds));
        profile.VisibleColumns = ParseVisibleColumns(VisibleColumnsTextBox.Text);
    }

    private static List<string> ParseVisibleColumns(string value) =>
        value.Split([Environment.NewLine, ",", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = CreateDefaultProfile("sql-source");
        configuration.DataSources.Add(profile);
        RefreshList(profile);
        StatusTextBlock.Text = "New SQL data source created. Edit and save.";
    }

    private void NewOdbcButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = CreateDefaultProfile("odbc-source");
        profile.ProviderInvariantName = "System.Data.Odbc";
        profile.ConnectionSecret = "DSN=LabelsDsn;Uid=user;Pwd=password;";
        profile.PrimarySql = "select ProductCode, ProductName, Status from vw_label_candidates";
        profile.DetailSql = "select * from vw_label_details where ProductCode in ({Keys})";
        profile.LookupSql = "select ProductCode from vw_label_lookup where Barcode = {Scan}";
        profile.LookupKeyColumn = "ProductCode";
        configuration.DataSources.Add(profile);
        RefreshList(profile);
        StatusTextBlock.Text = "New ODBC data source created. Edit and save.";
    }

    private static DataSourceProfile CreateDefaultProfile(string prefix)
    {
        var id = $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}";
        return new DataSourceProfile
        {
            Id = id,
            DisplayName = id,
            ProviderInvariantName = "Microsoft.Data.SqlClient",
            PrimaryView = "dbo.vw_label_candidates",
            DetailView = "dbo.vw_label_details",
            KeyColumn = "ProductCode",
            LookupKeyColumn = "ProductCode",
            MaxRows = 500,
            CommandTimeoutSeconds = 30,
            VisibleColumns = ["ProductCode", "ProductName", "Status"]
        };
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete data source '{selectedProfile.DisplayName}'?",
            "Delete data source",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        configuration.DataSources.Remove(selectedProfile);
        RefreshList(configuration.DataSources.FirstOrDefault());
        StatusTextBlock.Text = "Data source deleted. Save to persist.";
    }

    private async void TestPrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = new DataSourceProfile();
        ApplyEditorToProfile(profile);

        try
        {
            StatusTextBlock.Text = "Testing primary query...";
            var rows = await dataSourceService.LoadPrimaryRowsAsync(profile);
            StatusTextBlock.Text = $"Test OK: loaded {rows.Count} row(s).";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Test failed: {ex.Message}";
        }
    }

    private async void TestLookupButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = new DataSourceProfile();
        ApplyEditorToProfile(profile);

        try
        {
            StatusTextBlock.Text = "Testing lookup query...";
            var keys = await dataSourceService.LookupKeysAsync(profile, LookupTestValueTextBox.Text);
            StatusTextBlock.Text = keys.Count == 0
                ? "Lookup OK: no keys returned."
                : $"Lookup OK: {string.Join(", ", keys)}";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Lookup failed: {ex.Message}";
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is not null)
        {
            ApplyEditorToProfile(selectedProfile);
        }

        configuration.Application.Language = LanguageComboBox.SelectedValue as string ?? "en";
        await configurationStore.SaveAsync(configuration);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value, out var result) ? result : fallback;

    private sealed record LanguageOption(string Code, string DisplayName);
}
