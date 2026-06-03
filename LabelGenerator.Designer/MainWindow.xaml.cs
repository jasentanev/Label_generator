using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabelGenerator.Core.Configuration;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Configuration;
using Microsoft.Win32;
using ZXing;
using ZXing.Common;

namespace LabelGenerator.Designer;

public partial class MainWindow : Window
{
    private readonly string configurationPath;
    private readonly string assetBaseDirectory;
    private readonly JsonConfigurationStore configurationStore;

    private LabelGeneratorConfiguration configuration = new();
    private LabelTemplateProfile? selectedTemplate;
    private LabelDesignElement? selectedElement;
    private ColumnFilter? selectedMasterFilter;
    private FrameworkElement? draggingVisual;
    private Point dragStart;
    private bool isDragging;
    private bool isLoading;

    public MainWindow()
    {
        InitializeComponent();

        var baseDirectory = AppContext.BaseDirectory;
        var bundledConfigurationPath = Path.Combine(baseDirectory, "Config", "appsettings.json");
        configurationPath = ConfigurationPathResolver.ResolveSharedConfigurationPath(bundledConfigurationPath);
        assetBaseDirectory = Path.GetDirectoryName(configurationPath) ?? baseDirectory;
        configurationStore = new JsonConfigurationStore(configurationPath);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        ElementTypeComboBox.ItemsSource = Enum.GetValues<LabelElementType>();
        NewElementTypeComboBox.ItemsSource = Enum.GetValues<LabelElementType>();
        NewElementTypeComboBox.SelectedItem = LabelElementType.Text;
        TextAlignmentComboBox.ItemsSource = Enum.GetValues<LabelTextAlignment>();
        BarcodeSymbologyComboBox.ItemsSource = Enum.GetValues<BarcodeSymbology>();

        await LoadConfigurationAsync();
    }

    private async Task LoadConfigurationAsync()
    {
        configuration = await configurationStore.LoadAsync();
        RefreshTemplateList();

        TemplateComboBox.SelectedItem = configuration.LabelTemplates.FirstOrDefault();
        StatusTextBlock.Text = $"Configuration: {configurationPath}";
    }

    private void RefreshTemplateList()
    {
        isLoading = true;
        TemplateComboBox.ItemsSource = null;
        TemplateComboBox.ItemsSource = configuration.LabelTemplates;
        isLoading = false;
    }

    private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        selectedTemplate = TemplateComboBox.SelectedItem as LabelTemplateProfile;
        selectedElement = null;
        selectedMasterFilter = null;
        LoadTemplateProperties();
        RefreshFieldNames();
        RefreshMasterFilters();
        RefreshElements();
        RenderCanvas();
    }

    private void LoadTemplateProperties()
    {
        if (selectedTemplate is null)
        {
            return;
        }

        selectedTemplate.LabelCount ??= new LabelCountSettings();
        TemplateIdTextBox.Text = selectedTemplate.Id;
        TemplateNameTextBox.Text = selectedTemplate.DisplayName;
        TemplatePathTextBox.Text = selectedTemplate.TemplateFilePath;
        ShowBorderCheckBox.IsChecked = selectedTemplate.Design.ShowBorder;
        UseLabelCountCheckBox.IsChecked = selectedTemplate.LabelCount.IsEnabled;
        LabelCountColumnTextBox.Text = selectedTemplate.LabelCount.ColumnName;
        MaxLabelCountTextBox.Text = selectedTemplate.LabelCount.MaxCountPerRow.ToString(CultureInfo.InvariantCulture);
        LabelWidthTextBox.Text = FormatNumber(selectedTemplate.Sheet.LabelWidthMillimeters);
        LabelHeightTextBox.Text = FormatNumber(selectedTemplate.Sheet.LabelHeightMillimeters);
        ColumnsTextBox.Text = selectedTemplate.Sheet.Columns.ToString(CultureInfo.InvariantCulture);
        RowsTextBox.Text = selectedTemplate.Sheet.Rows.ToString(CultureInfo.InvariantCulture);
        MarginTopTextBox.Text = FormatNumber(selectedTemplate.Sheet.MarginTopMillimeters);
        MarginBottomTextBox.Text = FormatNumber(selectedTemplate.Sheet.MarginBottomMillimeters);
        MarginLeftTextBox.Text = FormatNumber(selectedTemplate.Sheet.MarginLeftMillimeters);
        MarginRightTextBox.Text = FormatNumber(selectedTemplate.Sheet.MarginRightMillimeters);
        GapXTextBox.Text = FormatNumber(selectedTemplate.Sheet.GapXMillimeters);
        GapYTextBox.Text = FormatNumber(selectedTemplate.Sheet.GapYMillimeters);
    }

    private void ApplyTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTemplateProperties();
        RefreshTemplateListPreservingSelection();
        RenderCanvas();
        SetStatus("Template properties applied.");
    }

    private void ApplyTemplateProperties()
    {
        if (selectedTemplate is null)
        {
            return;
        }

        selectedTemplate.LabelCount ??= new LabelCountSettings();
        selectedTemplate.Id = string.IsNullOrWhiteSpace(TemplateIdTextBox.Text)
            ? selectedTemplate.Id
            : TemplateIdTextBox.Text.Trim();
        selectedTemplate.DisplayName = string.IsNullOrWhiteSpace(TemplateNameTextBox.Text)
            ? selectedTemplate.DisplayName
            : TemplateNameTextBox.Text.Trim();
        selectedTemplate.TemplateFilePath = TemplatePathTextBox.Text.Trim();
        selectedTemplate.Design.ShowBorder = ShowBorderCheckBox.IsChecked == true;
        selectedTemplate.LabelCount.IsEnabled = UseLabelCountCheckBox.IsChecked == true;
        selectedTemplate.LabelCount.ColumnName = string.IsNullOrWhiteSpace(LabelCountColumnTextBox.Text)
            ? "LabelCount"
            : LabelCountColumnTextBox.Text.Trim();
        selectedTemplate.LabelCount.MaxCountPerRow = Math.Max(1, ParseInt(MaxLabelCountTextBox.Text, selectedTemplate.LabelCount.MaxCountPerRow));
        selectedTemplate.Sheet.LabelWidthMillimeters = ParseDouble(LabelWidthTextBox.Text, selectedTemplate.Sheet.LabelWidthMillimeters);
        selectedTemplate.Sheet.LabelHeightMillimeters = ParseDouble(LabelHeightTextBox.Text, selectedTemplate.Sheet.LabelHeightMillimeters);
        selectedTemplate.Sheet.Columns = Math.Max(1, ParseInt(ColumnsTextBox.Text, selectedTemplate.Sheet.Columns));
        selectedTemplate.Sheet.Rows = Math.Max(1, ParseInt(RowsTextBox.Text, selectedTemplate.Sheet.Rows));
        selectedTemplate.Sheet.MarginTopMillimeters = Math.Max(0, ParseDouble(MarginTopTextBox.Text, selectedTemplate.Sheet.MarginTopMillimeters));
        selectedTemplate.Sheet.MarginBottomMillimeters = Math.Max(0, ParseDouble(MarginBottomTextBox.Text, selectedTemplate.Sheet.MarginBottomMillimeters));
        selectedTemplate.Sheet.MarginLeftMillimeters = Math.Max(0, ParseDouble(MarginLeftTextBox.Text, selectedTemplate.Sheet.MarginLeftMillimeters));
        selectedTemplate.Sheet.MarginRightMillimeters = Math.Max(0, ParseDouble(MarginRightTextBox.Text, selectedTemplate.Sheet.MarginRightMillimeters));
        selectedTemplate.Sheet.GapXMillimeters = Math.Max(0, ParseDouble(GapXTextBox.Text, selectedTemplate.Sheet.GapXMillimeters));
        selectedTemplate.Sheet.GapYMillimeters = Math.Max(0, ParseDouble(GapYTextBox.Text, selectedTemplate.Sheet.GapYMillimeters));
    }

    private void RefreshTemplateListPreservingSelection()
    {
        var current = selectedTemplate;
        isLoading = true;
        TemplateComboBox.ItemsSource = null;
        TemplateComboBox.ItemsSource = configuration.LabelTemplates;
        TemplateComboBox.SelectedItem = current;
        isLoading = false;
    }

    private void RefreshFieldNames()
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataSource in configuration.DataSources)
        {
            foreach (var column in dataSource.VisibleColumns)
            {
                fields.Add(column);
            }
        }

        if (selectedTemplate is not null)
        {
            foreach (var field in selectedTemplate.ExpectedFields)
            {
                fields.Add(field);
            }

            foreach (var field in selectedTemplate.Design.Elements
                         .Where(element => element.Type is LabelElementType.Field or LabelElementType.Barcode)
                         .Select(element => element.FieldName)
                         .Where(field => !string.IsNullOrWhiteSpace(field)))
            {
                fields.Add(field);
            }
        }

        var fieldList = fields.OrderBy(field => field).ToList();
        FieldNameComboBox.ItemsSource = fieldList;
        MasterFilterColumnComboBox.ItemsSource = fieldList;
    }

    private void RefreshMasterFilters()
    {
        MasterFiltersListBox.SelectionChanged -= MasterFiltersListBox_SelectionChanged;
        MasterFiltersListBox.ItemsSource = null;
        MasterFiltersListBox.ItemsSource = selectedTemplate?.MasterFilters;
        MasterFiltersListBox.SelectedItem = selectedMasterFilter;
        MasterFiltersListBox.SelectionChanged += MasterFiltersListBox_SelectionChanged;
    }

    private void MasterFiltersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedMasterFilter = MasterFiltersListBox.SelectedItem as ColumnFilter;
        LoadMasterFilterProperties();
    }

    private void LoadMasterFilterProperties()
    {
        if (selectedMasterFilter is null)
        {
            MasterFilterColumnComboBox.Text = string.Empty;
            MasterFilterPatternTextBox.Text = string.Empty;
            MasterFilterEnabledCheckBox.IsChecked = true;
            MasterFilterCaseCheckBox.IsChecked = false;
            return;
        }

        MasterFilterColumnComboBox.Text = selectedMasterFilter.ColumnName;
        MasterFilterPatternTextBox.Text = selectedMasterFilter.Pattern;
        MasterFilterEnabledCheckBox.IsChecked = selectedMasterFilter.IsEnabled;
        MasterFilterCaseCheckBox.IsChecked = selectedMasterFilter.IsCaseSensitive;
    }

    private void AddMasterFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate is null)
        {
            SetStatus("Create or select a template first.");
            return;
        }

        var filter = ReadMasterFilterFromEditor();
        selectedTemplate.MasterFilters.Add(filter);
        selectedMasterFilter = filter;
        RefreshMasterFilters();
        MasterFiltersListBox.SelectedItem = filter;
        SetStatus("Master view regex filter added. Save to persist.");
    }

    private void ApplyMasterFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedMasterFilter is null)
        {
            AddMasterFilterButton_Click(sender, e);
            return;
        }

        var edited = ReadMasterFilterFromEditor();
        selectedMasterFilter.ColumnName = edited.ColumnName;
        selectedMasterFilter.Pattern = edited.Pattern;
        selectedMasterFilter.IsEnabled = edited.IsEnabled;
        selectedMasterFilter.IsCaseSensitive = edited.IsCaseSensitive;
        RefreshMasterFilters();
        MasterFiltersListBox.SelectedItem = selectedMasterFilter;
        SetStatus("Master view regex filter applied. Save to persist.");
    }

    private void DeleteMasterFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate is null || selectedMasterFilter is null)
        {
            return;
        }

        selectedTemplate.MasterFilters.Remove(selectedMasterFilter);
        selectedMasterFilter = null;
        RefreshMasterFilters();
        LoadMasterFilterProperties();
        SetStatus("Master view regex filter deleted. Save to persist.");
    }

    private ColumnFilter ReadMasterFilterFromEditor() =>
        new()
        {
            ColumnName = string.IsNullOrWhiteSpace(MasterFilterColumnComboBox.Text)
                ? string.Empty
                : MasterFilterColumnComboBox.Text.Trim(),
            Pattern = MasterFilterPatternTextBox.Text,
            IsEnabled = MasterFilterEnabledCheckBox.IsChecked == true,
            IsCaseSensitive = MasterFilterCaseCheckBox.IsChecked == true
        };

    private void RefreshElements()
    {
        ElementsListBox.SelectionChanged -= ElementsListBox_SelectionChanged;
        ElementsListBox.ItemsSource = null;
        ElementsListBox.ItemsSource = selectedTemplate?.Design.Elements;
        ElementsListBox.SelectedItem = selectedElement;
        ElementsListBox.SelectionChanged += ElementsListBox_SelectionChanged;
    }

    private void ElementsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedElement = ElementsListBox.SelectedItem as LabelDesignElement;
        LoadElementProperties();
        RenderCanvas();
    }

    private void LoadElementProperties()
    {
        isLoading = true;

        if (selectedElement is null)
        {
            ElementTypeComboBox.SelectedItem = null;
            ElementTextTextBox.Text = string.Empty;
            FieldNameComboBox.Text = string.Empty;
            ElementXTextBox.Text = string.Empty;
            ElementYTextBox.Text = string.Empty;
            ElementWidthTextBox.Text = string.Empty;
            ElementHeightTextBox.Text = string.Empty;
            FontSizeTextBox.Text = string.Empty;
            TextAlignmentComboBox.SelectedItem = null;
            BoldCheckBox.IsChecked = false;
            ItalicCheckBox.IsChecked = false;
            StrikethroughCheckBox.IsChecked = false;
            ForegroundTextBox.Text = string.Empty;
            BackgroundTextBox.Text = string.Empty;
            BarcodeSymbologyComboBox.SelectedItem = null;
            HumanReadableCheckBox.IsChecked = false;
            ImagePathTextBox.Text = string.Empty;
            isLoading = false;
            return;
        }

        ElementTypeComboBox.SelectedItem = selectedElement.Type;
        ElementTextTextBox.Text = selectedElement.Text;
        FieldNameComboBox.Text = selectedElement.FieldName;
        ElementXTextBox.Text = FormatNumber(selectedElement.XMillimeters);
        ElementYTextBox.Text = FormatNumber(selectedElement.YMillimeters);
        ElementWidthTextBox.Text = FormatNumber(selectedElement.WidthMillimeters);
        ElementHeightTextBox.Text = FormatNumber(selectedElement.HeightMillimeters);
        FontSizeTextBox.Text = FormatNumber(selectedElement.FontSize);
        TextAlignmentComboBox.SelectedItem = selectedElement.TextAlignment;
        BoldCheckBox.IsChecked = selectedElement.IsBold;
        ItalicCheckBox.IsChecked = selectedElement.IsItalic;
        StrikethroughCheckBox.IsChecked = selectedElement.IsStrikethrough;
        ForegroundTextBox.Text = selectedElement.Foreground;
        BackgroundTextBox.Text = selectedElement.Background;
        BarcodeSymbologyComboBox.SelectedItem = selectedElement.BarcodeSymbology;
        HumanReadableCheckBox.IsChecked = selectedElement.ShowHumanReadableText;
        ImagePathTextBox.Text = selectedElement.ImagePath;

        isLoading = false;
    }

    private void ApplyElementButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedElement is null)
        {
            return;
        }

        ApplyElementProperties(selectedElement);
        RefreshElements();
        RenderCanvas();
        SetStatus("Element properties applied.");
    }

    private void ApplyElementProperties(LabelDesignElement element)
    {
        element.Type = ElementTypeComboBox.SelectedItem is LabelElementType type ? type : element.Type;
        element.Text = ElementTextTextBox.Text;
        element.FieldName = FieldNameComboBox.Text.Trim();
        element.XMillimeters = Math.Max(0, ParseDouble(ElementXTextBox.Text, element.XMillimeters));
        element.YMillimeters = Math.Max(0, ParseDouble(ElementYTextBox.Text, element.YMillimeters));
        element.WidthMillimeters = Math.Max(1, ParseDouble(ElementWidthTextBox.Text, element.WidthMillimeters));
        element.HeightMillimeters = Math.Max(1, ParseDouble(ElementHeightTextBox.Text, element.HeightMillimeters));
        element.FontSize = Math.Max(1, ParseDouble(FontSizeTextBox.Text, element.FontSize));
        element.TextAlignment = TextAlignmentComboBox.SelectedItem is LabelTextAlignment alignment ? alignment : element.TextAlignment;
        element.IsBold = BoldCheckBox.IsChecked == true;
        element.IsItalic = ItalicCheckBox.IsChecked == true;
        element.IsStrikethrough = StrikethroughCheckBox.IsChecked == true;
        element.Foreground = string.IsNullOrWhiteSpace(ForegroundTextBox.Text) ? "#000000" : ForegroundTextBox.Text.Trim();
        element.Background = string.IsNullOrWhiteSpace(BackgroundTextBox.Text) ? "Transparent" : BackgroundTextBox.Text.Trim();
        element.BarcodeSymbology = BarcodeSymbologyComboBox.SelectedItem is BarcodeSymbology symbology
            ? symbology
            : element.BarcodeSymbology;
        element.ShowHumanReadableText = HumanReadableCheckBox.IsChecked == true;
        element.ImagePath = ImagePathTextBox.Text.Trim();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate is not null)
        {
            ApplyTemplateProperties();
            if (selectedElement is not null)
            {
                ApplyElementProperties(selectedElement);
            }
        }

        UpdateExpectedFields();
        await configurationStore.SaveAsync(configuration);
        RefreshTemplateListPreservingSelection();
        RefreshMasterFilters();
        RefreshElements();
        RenderCanvas();
        SetStatus($"Saved configuration: {configurationPath}");
    }

    private void UpdateExpectedFields()
    {
        foreach (var template in configuration.LabelTemplates)
        {
            template.ExpectedFields = template.Design.Elements
                .Where(element => element.Type is LabelElementType.Field or LabelElementType.Barcode)
                .Select(element => element.FieldName)
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private void NewTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var id = $"label-{DateTime.Now:yyyyMMddHHmmss}";
        var template = new LabelTemplateProfile
        {
            Id = id,
            DisplayName = "New label",
            TemplateFilePath = $"Templates/{id}.label.json",
            ExpectedFields = [],
            MasterFilters = [],
            LabelCount = new LabelCountSettings(),
            Sheet = new LabelSheetDefinition
            {
                LabelWidthMillimeters = 99.1,
                LabelHeightMillimeters = 38.1,
                Columns = 2,
                Rows = 7
            },
            Design = new LabelTemplateDesign { ShowBorder = true }
        };

        configuration.LabelTemplates.Add(template);
        selectedTemplate = template;
        RefreshTemplateList();
        TemplateComboBox.SelectedItem = template;
        SetStatus("New template created. Add elements and save.");
    }

    private void CopyTemplateAsNewButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate is null)
        {
            SetStatus("Select a template first.");
            return;
        }

        ApplyTemplateProperties();
        if (selectedElement is not null)
        {
            ApplyElementProperties(selectedElement);
        }

        var copy = CloneTemplate(selectedTemplate);
        var id = $"label-{DateTime.Now:yyyyMMddHHmmss}";
        copy.Id = id;
        copy.DisplayName = $"{selectedTemplate.DisplayName} copy";
        copy.TemplateFilePath = $"Templates/{id}.label.json";
        foreach (var element in copy.Design.Elements)
        {
            element.Id = Guid.NewGuid().ToString("N");
        }

        configuration.LabelTemplates.Add(copy);
        selectedTemplate = copy;
        selectedElement = null;
        selectedMasterFilter = null;
        RefreshTemplateList();
        TemplateComboBox.SelectedItem = copy;
        SetStatus("Template copied as new. Save to persist.");
    }

    private void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete template '{selectedTemplate.DisplayName}'?",
            "Delete template",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        configuration.LabelTemplates.Remove(selectedTemplate);
        selectedTemplate = configuration.LabelTemplates.FirstOrDefault();
        selectedElement = null;
        selectedMasterFilter = null;
        RefreshTemplateList();
        TemplateComboBox.SelectedItem = selectedTemplate;
        SetStatus("Template deleted. Save to persist the change.");
    }

    private void NewElementButton_Click(object sender, RoutedEventArgs e)
    {
        var type = NewElementTypeComboBox.SelectedItem is LabelElementType selectedType
            ? selectedType
            : LabelElementType.Text;

        if (type == LabelElementType.Image)
        {
            AddImageElement();
            return;
        }

        AddElement(CreateDefaultElement(type));
    }

    private LabelDesignElement CreateDefaultElement(LabelElementType type) =>
        type switch
        {
            LabelElementType.Field => new LabelDesignElement
            {
                Type = LabelElementType.Field,
                FieldName = FieldNameComboBox.Text.Trim().Length > 0 ? FieldNameComboBox.Text.Trim() : string.Empty,
                XMillimeters = 5,
                YMillimeters = 15,
                WidthMillimeters = 50,
                HeightMillimeters = 9,
                FontSize = 10
            },
            LabelElementType.Barcode => new LabelDesignElement
            {
                Type = LabelElementType.Barcode,
                FieldName = FieldNameComboBox.Text.Trim().Length > 0 ? FieldNameComboBox.Text.Trim() : string.Empty,
                XMillimeters = 5,
                YMillimeters = 25,
                WidthMillimeters = 50,
                HeightMillimeters = 18,
                BarcodeSymbology = BarcodeSymbology.Code128,
                ShowHumanReadableText = true
            },
            LabelElementType.Rectangle => new LabelDesignElement
            {
                Type = LabelElementType.Rectangle,
                XMillimeters = 4,
                YMillimeters = 4,
                WidthMillimeters = 40,
                HeightMillimeters = 20,
                Foreground = "#000000",
                Background = "Transparent"
            },
            _ => new LabelDesignElement
            {
                Type = LabelElementType.Text,
                Text = "Text",
                XMillimeters = 5,
                YMillimeters = 5,
                WidthMillimeters = 30,
                HeightMillimeters = 8,
                FontSize = 10
            }
        };

    private void AddTextButton_Click(object sender, RoutedEventArgs e) =>
        AddElement(new LabelDesignElement
        {
            Type = LabelElementType.Text,
            Text = "Text",
            XMillimeters = 5,
            YMillimeters = 5,
            WidthMillimeters = 30,
            HeightMillimeters = 8,
            FontSize = 10
        });

    private void AddFieldButton_Click(object sender, RoutedEventArgs e) =>
        AddElement(new LabelDesignElement
        {
            Type = LabelElementType.Field,
            FieldName = FieldNameComboBox.Text.Trim().Length > 0 ? FieldNameComboBox.Text.Trim() : "ProductName",
            XMillimeters = 5,
            YMillimeters = 15,
            WidthMillimeters = 50,
            HeightMillimeters = 9,
            FontSize = 10
        });

    private void AddBarcodeButton_Click(object sender, RoutedEventArgs e) =>
        AddElement(new LabelDesignElement
        {
            Type = LabelElementType.Barcode,
            FieldName = FieldNameComboBox.Text.Trim().Length > 0 ? FieldNameComboBox.Text.Trim() : "Barcode",
            XMillimeters = 5,
            YMillimeters = 25,
            WidthMillimeters = 50,
            HeightMillimeters = 18,
            BarcodeSymbology = BarcodeSymbology.Code128,
            ShowHumanReadableText = true
        });

    private void AddRectangleButton_Click(object sender, RoutedEventArgs e) =>
        AddElement(new LabelDesignElement
        {
            Type = LabelElementType.Rectangle,
            XMillimeters = 4,
            YMillimeters = 4,
            WidthMillimeters = 40,
            HeightMillimeters = 20,
            Foreground = "#000000",
            Background = "Transparent"
        });

    private void AddImageButton_Click(object sender, RoutedEventArgs e)
    {
        AddImageElement();
    }

    private void AddImageElement()
    {
        var dialog = CreateImageDialog();
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        AddElement(new LabelDesignElement
        {
            Type = LabelElementType.Image,
            ImagePath = ToStoredAssetPath(dialog.FileName),
            XMillimeters = 5,
            YMillimeters = 5,
            WidthMillimeters = 25,
            HeightMillimeters = 20
        });
    }

    private static LabelTemplateProfile CloneTemplate(LabelTemplateProfile template)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(template, options);
        return JsonSerializer.Deserialize<LabelTemplateProfile>(json, options)
            ?? throw new InvalidOperationException("Template clone failed.");
    }

    private void AddElement(LabelDesignElement element)
    {
        if (selectedTemplate is null)
        {
            SetStatus("Create or select a template first.");
            return;
        }

        selectedTemplate.Design.Elements.Add(element);
        selectedElement = element;
        RefreshElements();
        ElementsListBox.SelectedItem = element;
        LoadElementProperties();
        RenderCanvas();
        SetStatus($"{element.Type} element added. Save to persist.");
    }

    private void DeleteElementButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate is null || selectedElement is null)
        {
            return;
        }

        selectedTemplate.Design.Elements.Remove(selectedElement);
        selectedElement = null;
        RefreshElements();
        LoadElementProperties();
        RenderCanvas();
        SetStatus("Element deleted. Save to persist.");
    }

    private void DeleteSelectedComponentButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteElementButton_Click(sender, e);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || e.OriginalSource is TextBox or ComboBox)
        {
            return;
        }

        DeleteElementButton_Click(sender, e);
        e.Handled = true;
    }

    private void MoveElementUpButton_Click(object sender, RoutedEventArgs e) => MoveSelectedElement(-1);

    private void MoveElementDownButton_Click(object sender, RoutedEventArgs e) => MoveSelectedElement(1);

    private void MoveSelectedElement(int offset)
    {
        if (selectedTemplate is null || selectedElement is null)
        {
            return;
        }

        var elements = selectedTemplate.Design.Elements;
        var index = elements.IndexOf(selectedElement);
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= elements.Count)
        {
            return;
        }

        elements.RemoveAt(index);
        elements.Insert(targetIndex, selectedElement);
        RefreshElements();
        ElementsListBox.SelectedItem = selectedElement;
        RenderCanvas();
    }

    private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateImageDialog();
        if (dialog.ShowDialog(this) == true)
        {
            ImagePathTextBox.Text = ToStoredAssetPath(dialog.FileName);
        }
    }

    private static OpenFileDialog CreateImageDialog() =>
        new()
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            CheckFileExists = true
        };

    private void RenderCanvas()
    {
        DesignCanvas.Children.Clear();

        if (selectedTemplate is null)
        {
            return;
        }

        var width = ToDip(selectedTemplate.Sheet.LabelWidthMillimeters);
        var height = ToDip(selectedTemplate.Sheet.LabelHeightMillimeters);
        DesignCanvas.Width = width;
        DesignCanvas.Height = height;
        DesignCanvas.LayoutTransform = new ScaleTransform(ZoomSlider.Value, ZoomSlider.Value);
        CanvasSizeTextBlock.Text = $"{FormatNumber(selectedTemplate.Sheet.LabelWidthMillimeters)} x {FormatNumber(selectedTemplate.Sheet.LabelHeightMillimeters)} mm";

        if (selectedTemplate.Design.ShowBorder)
        {
            DesignCanvas.Children.Add(new Border
            {
                Width = width,
                Height = height,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            });
        }

        foreach (var element in selectedTemplate.Design.Elements)
        {
            var visual = CreateElementVisual(element);
            Canvas.SetLeft(visual, ToDip(element.XMillimeters));
            Canvas.SetTop(visual, ToDip(element.YMillimeters));
            DesignCanvas.Children.Add(visual);
        }
    }

    private FrameworkElement CreateElementVisual(LabelDesignElement element)
    {
        var content = CreateElementContent(element);
        var border = new Border
        {
            Width = ToDip(element.WidthMillimeters),
            Height = ToDip(element.HeightMillimeters),
            BorderBrush = ReferenceEquals(element, selectedElement) ? Brushes.DodgerBlue : Brushes.Gray,
            BorderThickness = ReferenceEquals(element, selectedElement) ? new Thickness(1.5) : new Thickness(0.5),
            Background = Brushes.Transparent,
            Child = content,
            Tag = element,
            Cursor = Cursors.SizeAll
        };

        border.MouseLeftButtonDown += Element_MouseLeftButtonDown;
        border.MouseMove += Element_MouseMove;
        border.MouseLeftButtonUp += Element_MouseLeftButtonUp;
        return border;
    }

    private FrameworkElement CreateElementContent(LabelDesignElement element) =>
        element.Type switch
        {
            LabelElementType.Text => CreateTextBlock(element, element.Text),
            LabelElementType.Field => CreateTextBlock(element, "{" + element.FieldName + "}"),
            LabelElementType.Barcode => CreateBarcodePreview(element),
            LabelElementType.Image => CreateImagePreview(element),
            LabelElementType.Rectangle => new Border
            {
                BorderBrush = ParseBrush(element.Foreground, Brushes.Black),
                BorderThickness = new Thickness(1),
                Background = ParseBrush(element.Background, Brushes.Transparent)
            },
            _ => CreateTextBlock(element, string.Empty)
        };

    private static TextBlock CreateTextBlock(LabelDesignElement element, string text) =>
        new()
        {
            Text = text,
            FontSize = element.FontSize,
            FontWeight = element.IsBold ? FontWeights.SemiBold : FontWeights.Normal,
            FontStyle = element.IsItalic ? FontStyles.Italic : FontStyles.Normal,
            TextDecorations = element.IsStrikethrough ? TextDecorations.Strikethrough : null,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = ParseBrush(element.Foreground, Brushes.Black),
            Background = ParseBrush(element.Background, Brushes.Transparent),
            TextAlignment = element.TextAlignment switch
            {
                LabelTextAlignment.Center => TextAlignment.Center,
                LabelTextAlignment.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            }
        };

    private static FrameworkElement CreateBarcodePreview(LabelDesignElement element)
    {
        try
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = element.BarcodeSymbology switch
                {
                    BarcodeSymbology.QrCode => BarcodeFormat.QR_CODE,
                    BarcodeSymbology.Ean13 => BarcodeFormat.EAN_13,
                    _ => BarcodeFormat.CODE_128
                },
                Options = new EncodingOptions
                {
                    Width = Math.Max(80, (int)ToDip(element.WidthMillimeters)),
                    Height = Math.Max(40, (int)ToDip(element.HeightMillimeters)),
                    Margin = 1,
                    PureBarcode = !element.ShowHumanReadableText
                }
            };

            var sample = element.BarcodeSymbology == BarcodeSymbology.Ean13 ? "5901234123457" : "123456789012";
            var pixelData = writer.Write(sample);
            var source = BitmapSource.Create(
                pixelData.Width,
                pixelData.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelData.Pixels,
                pixelData.Width * 4);
            source.Freeze();

            return new Image { Source = source, Stretch = Stretch.Fill };
        }
        catch
        {
            return CreateTextBlock(element, "Barcode");
        }
    }

    private FrameworkElement CreateImagePreview(LabelDesignElement element)
    {
        var path = ResolveAssetPath(element.ImagePath);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return CreateTextBlock(element, "Image");
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return new Image { Source = bitmap, Stretch = Stretch.Uniform };
        }
        catch
        {
            return CreateTextBlock(element, "Image error");
        }
    }

    private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement visual || visual.Tag is not LabelDesignElement element)
        {
            return;
        }

        selectedElement = element;
        ElementsListBox.SelectionChanged -= ElementsListBox_SelectionChanged;
        ElementsListBox.SelectedItem = element;
        ElementsListBox.SelectionChanged += ElementsListBox_SelectionChanged;
        LoadElementProperties();
        if (visual is Border border)
        {
            border.BorderBrush = Brushes.DodgerBlue;
            border.BorderThickness = new Thickness(1.5);
        }

        draggingVisual = visual;
        dragStart = e.GetPosition(DesignCanvas);
        isDragging = true;
        visual.CaptureMouse();
        e.Handled = true;
    }

    private void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging || draggingVisual?.Tag is not LabelDesignElement element || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(DesignCanvas);
        var deltaX = current.X - dragStart.X;
        var deltaY = current.Y - dragStart.Y;

        element.XMillimeters = Math.Max(0, element.XMillimeters + ToMillimeters(deltaX));
        element.YMillimeters = Math.Max(0, element.YMillimeters + ToMillimeters(deltaY));
        dragStart = current;

        Canvas.SetLeft(draggingVisual, ToDip(element.XMillimeters));
        Canvas.SetTop(draggingVisual, ToDip(element.YMillimeters));
        ElementXTextBox.Text = FormatNumber(element.XMillimeters);
        ElementYTextBox.Text = FormatNumber(element.YMillimeters);
    }

    private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (draggingVisual is not null)
        {
            draggingVisual.ReleaseMouseCapture();
        }

        draggingVisual = null;
        isDragging = false;
    }

    private void DesignCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Source == DesignCanvas)
        {
            selectedElement = null;
            ElementsListBox.SelectedItem = null;
            LoadElementProperties();
            RenderCanvas();
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DesignCanvas is not null)
        {
            DesignCanvas.LayoutTransform = new ScaleTransform(ZoomSlider.Value, ZoomSlider.Value);
        }
    }

    private string ResolveAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(assetBaseDirectory, path);
    }

    private string ToStoredAssetPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var basePath = Path.GetFullPath(assetBaseDirectory);

        return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(basePath, fullPath)
            : fullPath;
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

    private void SetStatus(string message) => StatusTextBlock.Text = $"{message}  ({configurationPath})";

    private static double ParseDouble(string value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentCultureValue))
        {
            return currentCultureValue;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue)
            ? invariantValue
            : fallback;
    }

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var result) ? result : fallback;

    private static string FormatNumber(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static double ToDip(double millimeters) => millimeters * 96.0 / 25.4;

    private static double ToMillimeters(double dip) => dip * 25.4 / 96.0;
}
