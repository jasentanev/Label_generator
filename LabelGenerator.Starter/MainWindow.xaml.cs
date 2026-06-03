using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Configuration;

namespace LabelGenerator.Starter;

public partial class MainWindow : Window
{
    private readonly string configurationPath;
    private readonly JsonConfigurationStore configurationStore;
    private readonly ObservableCollection<StarterProfileView> profiles = [];

    private LabelGeneratorConfiguration configuration = new();
    private StarterProfileView? selectedProfile;
    private bool isLoading;

    public MainWindow()
    {
        InitializeComponent();

        var baseDirectory = AppContext.BaseDirectory;
        var bundledConfigurationPath = Path.Combine(baseDirectory, "Config", "appsettings.json");
        configurationPath = ConfigurationPathResolver.ResolveSharedConfigurationPath(bundledConfigurationPath);
        configurationStore = new JsonConfigurationStore(configurationPath);

        ProfilesListBox.ItemsSource = profiles;
        ActionComboBox.ItemsSource = Enum.GetValues<LabelStarterActionMode>();

        Loaded += MainWindow_Loaded;
        DataSourceComboBox.SelectionChanged += (_, _) => RefreshCommandPreview();
        LabelComboBox.SelectionChanged += (_, _) => RefreshCommandPreview();
        ActionComboBox.SelectionChanged += (_, _) => RefreshCommandPreview();
        UsersCheckBox.Checked += (_, _) => RefreshCommandPreview();
        UsersCheckBox.Unchecked += (_, _) => RefreshCommandPreview();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        LoadBranding();
        await LoadConfigurationAsync();
    }

    private async Task LoadConfigurationAsync()
    {
        isLoading = true;
        try
        {
            configuration = await configurationStore.LoadAsync();
            configuration.Application.LabelStarters ??= [];

            DataSourceComboBox.ItemsSource = configuration.DataSources;
            LabelComboBox.ItemsSource = configuration.LabelTemplates;

            profiles.Clear();
            foreach (var profile in BuildProfiles(configuration))
            {
                profiles.Add(new StarterProfileView(profile));
            }

            selectedProfile = profiles.FirstOrDefault();
            ProfilesListBox.SelectedItem = selectedProfile;
            LoadProfileEditor(selectedProfile?.Profile);
            SetStatus($"Loaded {profiles.Count} starter profile(s). Config: {configurationPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Configuration load failed: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            RefreshCommandPreview();
        }
    }

    private IEnumerable<LabelStarterProfile> BuildProfiles(LabelGeneratorConfiguration value)
    {
        if (value.Application.LabelStarters.Count > 0)
        {
            return value.Application.LabelStarters;
        }

        var dataSource = value.DataSources.FirstOrDefault();
        var template = value.LabelTemplates.FirstOrDefault();
        if (dataSource is null || template is null)
        {
            return [];
        }

        return
        [
            new LabelStarterProfile
            {
                Id = "default-open",
                DisplayName = "Default label",
                Description = "Open the first configured data source and label.",
                DataSourceId = dataSource.Id,
                LabelTemplateId = template.Id,
                ActionMode = LabelStarterActionMode.Open,
                UserMode = true,
                IsEnabled = true
            }
        ];
    }

    private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        selectedProfile = ProfilesListBox.SelectedItem as StarterProfileView;
        LoadProfileEditor(selectedProfile?.Profile);
    }

    private void LoadProfileEditor(LabelStarterProfile? profile)
    {
        isLoading = true;
        try
        {
            NameTextBox.Text = profile?.DisplayName ?? string.Empty;
            DescriptionTextBox.Text = profile?.Description ?? string.Empty;
            DataSourceComboBox.SelectedValue = profile?.DataSourceId ?? string.Empty;
            LabelComboBox.SelectedValue = profile?.LabelTemplateId ?? string.Empty;
            ActionComboBox.SelectedItem = profile?.ActionMode ?? LabelStarterActionMode.Open;
            UsersCheckBox.IsChecked = profile?.UserMode ?? true;
            EnabledCheckBox.IsChecked = profile?.IsEnabled ?? true;
            RefreshCommandPreview();
        }
        finally
        {
            isLoading = false;
        }
    }

    private void NewProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = new LabelStarterProfile
        {
            Id = $"starter-{DateTime.Now:yyyyMMddHHmmss}",
            DisplayName = "New starter",
            Description = "Prepared label workflow.",
            DataSourceId = configuration.DataSources.FirstOrDefault()?.Id ?? string.Empty,
            LabelTemplateId = configuration.LabelTemplates.FirstOrDefault()?.Id ?? string.Empty,
            ActionMode = LabelStarterActionMode.Open,
            UserMode = true,
            IsEnabled = true
        };

        var view = new StarterProfileView(profile);
        profiles.Add(view);
        ProfilesListBox.SelectedItem = view;
        SetStatus("New starter profile created. Apply and Save to persist.");
    }

    private void ApplyProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            NewProfileButton_Click(sender, e);
            selectedProfile = ProfilesListBox.SelectedItem as StarterProfileView;
        }

        if (selectedProfile is null)
        {
            return;
        }

        ApplyEditorToProfile(selectedProfile.Profile);
        selectedProfile.Refresh();
        ProfilesListBox.Items.Refresh();
        RefreshCommandPreview();
        SetStatus("Starter profile applied. Save to persist.");
    }

    private void ApplyEditorToProfile(LabelStarterProfile profile)
    {
        profile.DisplayName = string.IsNullOrWhiteSpace(NameTextBox.Text)
            ? profile.DisplayName
            : NameTextBox.Text.Trim();
        profile.Description = DescriptionTextBox.Text.Trim();
        profile.DataSourceId = DataSourceComboBox.SelectedValue as string ?? string.Empty;
        profile.LabelTemplateId = LabelComboBox.SelectedValue as string ?? string.Empty;
        profile.ActionMode = ActionComboBox.SelectedItem is LabelStarterActionMode mode
            ? mode
            : LabelStarterActionMode.Open;
        profile.UserMode = UsersCheckBox.IsChecked == true;
        profile.IsEnabled = EnabledCheckBox.IsChecked == true;
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete starter '{selectedProfile.Profile.DisplayName}'?",
            "Delete starter",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        profiles.Remove(selectedProfile);
        selectedProfile = profiles.FirstOrDefault();
        ProfilesListBox.SelectedItem = selectedProfile;
        SetStatus("Starter profile deleted. Save to persist.");
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is not null)
        {
            ApplyEditorToProfile(selectedProfile.Profile);
            selectedProfile.Refresh();
        }

        configuration.Application.LabelStarters = profiles
            .Select(profile => profile.Profile)
            .ToList();

        await configurationStore.SaveAsync(configuration);
        ProfilesListBox.Items.Refresh();
        SetStatus($"Saved starter profiles. Config: {configurationPath}");
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadConfigurationAsync();
    }

    private void OpenSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            SetStatus("Select a starter profile first.");
            return;
        }

        ApplyEditorToProfile(selectedProfile.Profile);
        selectedProfile.Refresh();
        LaunchProfile(selectedProfile.Profile);
    }

    private void LaunchProfile(LabelStarterProfile profile)
    {
        if (!profile.IsEnabled)
        {
            SetStatus("Selected starter profile is disabled.");
            return;
        }

        var executablePath = ResolveMainAppPath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            SetStatus("LabelGenerator.App.exe was not found in the starter folder.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false
            };

            foreach (var argument in BuildArguments(profile))
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo);
            SetStatus($"Started: {profile.DisplayName}");
        }
        catch (Exception ex)
        {
            SetStatus($"Start failed: {ex.Message}");
        }
    }

    private static string ResolveMainAppPath()
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "LabelGenerator.App.exe");
        return File.Exists(executablePath) ? executablePath : string.Empty;
    }

    private static IReadOnlyList<string> BuildArguments(LabelStarterProfile profile)
    {
        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.DataSourceId))
        {
            arguments.Add("-datasource");
            arguments.Add(profile.DataSourceId);
        }

        if (!string.IsNullOrWhiteSpace(profile.LabelTemplateId))
        {
            arguments.Add("-label");
            arguments.Add(profile.LabelTemplateId);
        }

        if (profile.UserMode)
        {
            arguments.Add("-Users");
        }

        if (profile.ActionMode == LabelStarterActionMode.Preview)
        {
            arguments.Add("-Preview");
        }
        else if (profile.ActionMode == LabelStarterActionMode.Print)
        {
            arguments.Add("-Print");
        }

        return arguments;
    }

    private void RefreshCommandPreview()
    {
        if (isLoading)
        {
            return;
        }

        var profile = new LabelStarterProfile();
        ApplyEditorToProfile(profile);
        CommandPreviewTextBox.Text = "LabelGenerator.App.exe " + string.Join(
            " ",
            BuildArguments(profile).Select(QuoteArgument));
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ') ? $"\"{argument}\"" : argument;

    private void LoadBranding()
    {
        var logoPath = ResolveLogoPath();
        if (!string.IsNullOrWhiteSpace(logoPath))
        {
            LogoImage.Source = LoadBitmap(logoPath);
        }

        var infoPath = Path.Combine(AppContext.BaseDirectory, "info.txt");
        if (File.Exists(infoPath))
        {
            InfoTextBlock.Text = File.ReadAllText(infoPath).Trim();
        }
    }

    private static string ResolveLogoPath()
    {
        var preferred = Path.Combine(AppContext.BaseDirectory, "log.jpg");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        var alternate = Path.Combine(AppContext.BaseDirectory, "logo.jpg");
        return File.Exists(alternate) ? alternate : string.Empty;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void SetStatus(string message) => StatusTextBlock.Text = message;

    private sealed class StarterProfileView(LabelStarterProfile profile)
    {
        public LabelStarterProfile Profile { get; } = profile;

        public string DisplayName => Profile.DisplayName;

        public string Description => Profile.Description;

        public string Summary =>
            $"Datasource: {Profile.DataSourceId} | Label: {Profile.LabelTemplateId} | Action: {Profile.ActionMode} | Users: {Profile.UserMode}";

        public void Refresh()
        {
        }
    }
}
