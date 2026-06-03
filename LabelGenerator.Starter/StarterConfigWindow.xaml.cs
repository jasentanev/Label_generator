using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Services.Configuration;

namespace LabelGenerator.Starter;

public partial class StarterConfigWindow : Window
{
    private readonly JsonConfigurationStore configurationStore;
    private readonly LabelGeneratorConfiguration configuration;
    private readonly ObservableCollection<LabelStarterProfile> profiles = [];
    private bool isLoading;
    private LabelStarterProfile? selectedProfile;

    public StarterConfigWindow(
        JsonConfigurationStore configurationStore,
        LabelGeneratorConfiguration configuration,
        string? selectedProfileId)
    {
        InitializeComponent();
        this.configurationStore = configurationStore;
        this.configuration = configuration;

        ProfilesListBox.ItemsSource = profiles;
        DataSourceComboBox.ItemsSource = configuration.DataSources;
        LabelComboBox.ItemsSource = configuration.LabelTemplates;
        ActionComboBox.ItemsSource = Enum.GetValues<LabelStarterActionMode>();
        DataSourceComboBox.SelectionChanged += (_, _) => RefreshCommandPreview();
        LabelComboBox.SelectionChanged += (_, _) => RefreshCommandPreview();
        ActionComboBox.SelectionChanged += (_, _) => RefreshCommandPreview();
        UsersCheckBox.Checked += (_, _) => RefreshCommandPreview();
        UsersCheckBox.Unchecked += (_, _) => RefreshCommandPreview();

        foreach (var profile in configuration.Application.LabelStarters)
        {
            profiles.Add(CloneProfile(profile));
        }

        selectedProfile = profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, selectedProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault();
        ProfilesListBox.SelectedItem = selectedProfile;
        LoadProfileEditor(selectedProfile);
    }

    public string SelectedProfileId { get; private set; } = string.Empty;

    private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        selectedProfile = ProfilesListBox.SelectedItem as LabelStarterProfile;
        LoadProfileEditor(selectedProfile);
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
        }
        finally
        {
            isLoading = false;
            RefreshCommandPreview();
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

        profiles.Add(profile);
        ProfilesListBox.SelectedItem = profile;
        StatusTextBlock.Text = "New starter profile created.";
    }

    private void ApplyProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            NewProfileButton_Click(sender, e);
            selectedProfile = ProfilesListBox.SelectedItem as LabelStarterProfile;
        }

        if (selectedProfile is null)
        {
            return;
        }

        ApplyEditorToProfile(selectedProfile);
        ProfilesListBox.Items.Refresh();
        RefreshCommandPreview();
        StatusTextBlock.Text = "Starter profile applied.";
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete starter '{selectedProfile.DisplayName}'?",
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
        StatusTextBlock.Text = "Starter profile deleted.";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is not null)
        {
            ApplyEditorToProfile(selectedProfile);
        }

        configuration.Application.LabelStarters = profiles
            .Select(CloneProfile)
            .ToList();
        SelectedProfileId = selectedProfile?.Id ?? string.Empty;
        await configurationStore.SaveAsync(configuration);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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
            MainWindow.BuildArguments(profile).Select(MainWindow.QuoteArgument));
    }

    private static LabelStarterProfile CloneProfile(LabelStarterProfile profile) =>
        new()
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            DataSourceId = profile.DataSourceId,
            LabelTemplateId = profile.LabelTemplateId,
            ActionMode = profile.ActionMode,
            UserMode = profile.UserMode,
            IsEnabled = profile.IsEnabled
        };
}
