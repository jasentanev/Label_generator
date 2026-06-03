using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Configuration;
using ShapeLine = System.Windows.Shapes.Line;
using ShapeRectangle = System.Windows.Shapes.Rectangle;

namespace LabelGenerator.Starter;

public partial class MainWindow : Window
{
    private readonly string configurationPath;
    private readonly JsonConfigurationStore configurationStore;
    private readonly ObservableCollection<StarterProfileView> profiles = [];

    private LabelGeneratorConfiguration configuration = new();
    private StarterProfileView? selectedProfile;
    private string selectedProfileId = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        var baseDirectory = AppContext.BaseDirectory;
        var bundledConfigurationPath = Path.Combine(baseDirectory, "Config", "appsettings.json");
        configurationPath = ConfigurationPathResolver.ResolveSharedConfigurationPath(bundledConfigurationPath);
        configurationStore = new JsonConfigurationStore(configurationPath);

        ProfilesListBox.ItemsSource = profiles;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        LoadBranding();
        await LoadConfigurationAsync();
    }

    private async Task LoadConfigurationAsync()
    {
        try
        {
            configuration = await configurationStore.LoadAsync();
            configuration.Application.LabelStarters ??= [];

            profiles.Clear();
            foreach (var profile in BuildProfiles(configuration))
            {
                profiles.Add(new StarterProfileView(profile));
            }

            selectedProfile = profiles.FirstOrDefault(profile =>
                    string.Equals(profile.Profile.Id, selectedProfileId, StringComparison.OrdinalIgnoreCase))
                ?? profiles.FirstOrDefault();
            ProfilesListBox.SelectedItem = selectedProfile;
            RenderSelectedPreview();
            SetStatus($"Loaded {profiles.Count} starter profile(s). Config: {configurationPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Configuration load failed: {ex.Message}");
        }
    }

    private IEnumerable<LabelStarterProfile> BuildProfiles(LabelGeneratorConfiguration value)
    {
        if (value.Application.LabelStarters.Count > 0)
        {
            return value.Application.LabelStarters.Where(profile => profile.IsEnabled);
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
        selectedProfile = ProfilesListBox.SelectedItem as StarterProfileView;
        selectedProfileId = selectedProfile?.Profile.Id ?? string.Empty;
        RenderSelectedPreview();
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadConfigurationAsync();
    }

    private async void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new StarterConfigWindow(configurationStore, configuration, selectedProfile?.Profile.Id)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            selectedProfileId = window.SelectedProfileId;
            await LoadConfigurationAsync();
        }
    }

    private void OpenSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
        {
            SetStatus("Select a starter profile first.");
            return;
        }

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

    private void RenderSelectedPreview()
    {
        var profile = selectedProfile?.Profile;
        var template = profile is null
            ? null
            : configuration.LabelTemplates.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, profile.LabelTemplateId, StringComparison.OrdinalIgnoreCase));

        CommandPreviewTextBox.Text = profile is null
            ? string.Empty
            : "LabelGenerator.App.exe " + string.Join(" ", BuildArguments(profile).Select(QuoteArgument));

        if (profile is null || template is null)
        {
            PreviewTitleTextBlock.Text = "No label selected";
            PreviewSummaryTextBlock.Text = string.Empty;
            PreviewCanvas.Children.Clear();
            return;
        }

        var dataSource = configuration.DataSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profile.DataSourceId, StringComparison.OrdinalIgnoreCase));

        PreviewTitleTextBlock.Text = template.DisplayName;
        PreviewSummaryTextBlock.Text =
            $"Datasource: {dataSource?.DisplayName ?? profile.DataSourceId} | Action: {profile.ActionMode} | Users: {profile.UserMode}";
        RenderTemplatePreview(template);
    }

    private void RenderTemplatePreview(LabelTemplateProfile template)
    {
        PreviewCanvas.Children.Clear();
        var labelWidth = ToDip(template.Sheet.LabelWidthMillimeters);
        var labelHeight = ToDip(template.Sheet.LabelHeightMillimeters);
        PreviewCanvas.Width = Math.Max(160, labelWidth);
        PreviewCanvas.Height = Math.Max(100, labelHeight);

        PreviewCanvas.Children.Add(new Border
        {
            Width = labelWidth,
            Height = labelHeight,
            BorderBrush = template.Design.ShowBorder ? Brushes.LightGray : Brushes.Transparent,
            BorderThickness = template.Design.ShowBorder ? new Thickness(1) : new Thickness(0),
            Background = Brushes.White
        });

        foreach (var element in template.Design.Elements)
        {
            var visual = CreateElementPreview(element);
            visual.Width = ToDip(element.WidthMillimeters);
            visual.Height = ToDip(element.HeightMillimeters);
            Canvas.SetLeft(visual, ToDip(element.XMillimeters));
            Canvas.SetTop(visual, ToDip(element.YMillimeters));
            PreviewCanvas.Children.Add(visual);
        }
    }

    private static FrameworkElement CreateElementPreview(LabelDesignElement element) =>
        element.Type switch
        {
            LabelElementType.Text => CreateTextPreview(element, element.Text),
            LabelElementType.Field => CreateTextPreview(element, "{" + element.FieldName + "}"),
            LabelElementType.Barcode => CreateBarcodePlaceholder(element),
            LabelElementType.Image => CreateTextPreview(element, "Image"),
            LabelElementType.Rectangle => CreateRectanglePreview(element),
            LabelElementType.Line => CreateLinePreview(element),
            _ => CreateTextPreview(element, string.Empty)
        };

    private static TextBlock CreateTextPreview(LabelDesignElement element, string text) =>
        new()
        {
            Text = text,
            FontSize = Math.Max(4, element.FontSize),
            FontWeight = element.IsBold ? FontWeights.SemiBold : FontWeights.Normal,
            FontStyle = element.IsItalic ? FontStyles.Italic : FontStyles.Normal,
            TextDecorations = element.IsStrikethrough ? TextDecorations.Strikethrough : null,
            Foreground = ParseBrush(element.Foreground, Brushes.Black),
            Background = ParseBrush(element.Background, Brushes.Transparent),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = element.TextAlignment switch
            {
                LabelTextAlignment.Center => TextAlignment.Center,
                LabelTextAlignment.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            }
        };

    private static FrameworkElement CreateBarcodePlaceholder(LabelDesignElement element) =>
        new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Child = new TextBlock
            {
                Text = "||||||||||||",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ParseBrush(element.Foreground, Brushes.Black)
            }
        };

    private static FrameworkElement CreateRectanglePreview(LabelDesignElement element) =>
        new ShapeRectangle
        {
            Stroke = ParseBrush(element.Foreground, Brushes.Black),
            StrokeThickness = ToDip(Math.Max(0.1, element.LineThicknessMillimeters)),
            StrokeDashArray = CreateStrokeDashArray(element.LineStyle),
            Fill = ParseBrush(element.Background, Brushes.Transparent)
        };

    private static FrameworkElement CreateLinePreview(LabelDesignElement element) =>
        new ShapeLine
        {
            X1 = 0,
            Y1 = ToDip(element.HeightMillimeters) / 2,
            X2 = ToDip(element.WidthMillimeters),
            Y2 = ToDip(element.HeightMillimeters) / 2,
            Stroke = ParseBrush(element.Foreground, Brushes.Black),
            StrokeThickness = ToDip(Math.Max(0.1, element.LineThicknessMillimeters)),
            StrokeDashArray = CreateStrokeDashArray(element.LineStyle),
            Stretch = Stretch.Fill
        };

    private static DoubleCollection? CreateStrokeDashArray(LabelLineStyle style) =>
        style switch
        {
            LabelLineStyle.Dashed => [4, 2],
            LabelLineStyle.Dotted => [1, 2],
            _ => null
        };

    private static string ResolveMainAppPath()
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "LabelGenerator.App.exe");
        return File.Exists(executablePath) ? executablePath : string.Empty;
    }

    public static IReadOnlyList<string> BuildArguments(LabelStarterProfile profile)
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

        if (profile.ActionMode == LabelStarterActionMode.Load)
        {
            arguments.Add("-Load");
        }
        else if (profile.ActionMode == LabelStarterActionMode.Preview)
        {
            arguments.Add("-Preview");
        }
        else if (profile.ActionMode == LabelStarterActionMode.Print)
        {
            arguments.Add("-Print");
        }

        return arguments;
    }

    public static string QuoteArgument(string argument) =>
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

    private static Brush ParseBrush(string value, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return (Brush)(new BrushConverter().ConvertFromString(value) ?? fallback);
        }
        catch
        {
            return fallback;
        }
    }

    private void SetStatus(string message) => StatusTextBlock.Text = message;

    private static double ToDip(double millimeters) => millimeters * 96.0 / 25.4;

    private sealed class StarterProfileView(LabelStarterProfile profile)
    {
        public LabelStarterProfile Profile { get; } = profile;

        public string DisplayName => Profile.DisplayName;

        public string Description => Profile.Description;

        public string Summary =>
            $"Datasource: {Profile.DataSourceId} | Label: {Profile.LabelTemplateId} | Action: {Profile.ActionMode} | Users: {Profile.UserMode}";
    }
}
