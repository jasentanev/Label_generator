using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Printing;
using ZXing;
using ZXing.Common;
using ShapeLine = System.Windows.Shapes.Line;
using ShapeRectangle = System.Windows.Shapes.Rectangle;

namespace LabelGenerator.App.Printing;

public sealed class LabelDocumentFactory(string assetBaseDirectory)
{
    public FixedDocument Create(
        LabelTemplateProfile template,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        PrintRequest request)
    {
        var sheet = template.Sheet;
        var pageWidth = ToDip(sheet.PageWidthMillimeters);
        var pageHeight = ToDip(sheet.PageHeightMillimeters);
        var labelsPerPage = sheet.LabelsPerPage;
        var startLabelIndex = Math.Clamp(request.StartLabelPosition, 1, labelsPerPage) - 1;
        var expandedRows = LabelQuantityResolver.ExpandRows(template, rows, request.Copies);

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

        FixedPage? currentPage = null;
        var labelIndex = startLabelIndex;

        foreach (var row in expandedRows)
        {
            if (currentPage is null || labelIndex >= labelsPerPage)
            {
                currentPage = CreatePage(document, pageWidth, pageHeight);
                labelIndex = 0;
            }

            AddLabel(currentPage, template, row, labelIndex);
            labelIndex++;
        }

        if (expandedRows.Count == 0)
        {
            CreatePage(document, pageWidth, pageHeight);
        }

        return document;
    }

    private static FixedPage CreatePage(FixedDocument document, double pageWidth, double pageHeight)
    {
        var page = new FixedPage
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White
        };

        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);
        document.Pages.Add(pageContent);
        return page;
    }

    private void AddLabel(
        FixedPage page,
        LabelTemplateProfile template,
        IReadOnlyDictionary<string, object?> row,
        int labelIndex)
    {
        var sheet = template.Sheet;
        var column = labelIndex % sheet.Columns;
        var rowIndex = labelIndex / sheet.Columns;

        var left = ToDip(sheet.MarginLeftMillimeters + template.CalibrationOffsets.XMillimeters)
            + column * ToDip(sheet.LabelWidthMillimeters + sheet.GapXMillimeters);
        var top = ToDip(sheet.MarginTopMillimeters + template.CalibrationOffsets.YMillimeters)
            + rowIndex * ToDip(sheet.LabelHeightMillimeters + sheet.GapYMillimeters);

        var label = CreateLabelVisual(template, row);
        label.Width = ToDip(sheet.LabelWidthMillimeters);
        label.Height = ToDip(sheet.LabelHeightMillimeters);

        FixedPage.SetLeft(label, left);
        FixedPage.SetTop(label, top);
        page.Children.Add(label);
    }

    private Border CreateLabelVisual(
        LabelTemplateProfile template,
        IReadOnlyDictionary<string, object?> row)
    {
        var canvas = new Canvas
        {
            Width = ToDip(template.Sheet.LabelWidthMillimeters),
            Height = ToDip(template.Sheet.LabelHeightMillimeters),
            ClipToBounds = true
        };

        var elements = template.Design.Elements.Count > 0
            ? template.Design.Elements
            : LabelTemplateDesign.CreateDefaultProductDesign().Elements;

        foreach (var element in elements)
        {
            var visual = CreateElementVisual(element, row);
            visual.Width = ToDip(element.WidthMillimeters);
            visual.Height = ToDip(element.HeightMillimeters);

            Canvas.SetLeft(visual, ToDip(element.XMillimeters));
            Canvas.SetTop(visual, ToDip(element.YMillimeters));
            canvas.Children.Add(visual);
        }

        return new Border
        {
            BorderBrush = template.Design.ShowBorder ? Brushes.LightGray : Brushes.Transparent,
            BorderThickness = template.Design.ShowBorder ? new Thickness(0.5) : new Thickness(0),
            Background = Brushes.White,
            Child = canvas
        };
    }

    private FrameworkElement CreateElementVisual(
        LabelDesignElement element,
        IReadOnlyDictionary<string, object?> row)
    {
        return element.Type switch
        {
            LabelElementType.Text => CreateTextBlock(element, element.Text),
            LabelElementType.Field => CreateTextBlock(element, GetValue(row, element.FieldName, string.Empty)),
            LabelElementType.Barcode => CreateBarcodeVisual(element, GetValue(row, element.FieldName, element.Text)),
            LabelElementType.Image => CreateImageVisual(element),
            LabelElementType.Rectangle => CreateRectangleVisual(element),
            LabelElementType.Line => CreateLineVisual(element),
            _ => CreateTextBlock(element, string.Empty)
        };
    }

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

    private static FrameworkElement CreateBarcodeVisual(LabelDesignElement element, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CreateTextBlock(element, string.Empty);
        }

        try
        {
            var width = Math.Max(80, (int)ToDip(element.WidthMillimeters));
            var height = Math.Max(40, (int)ToDip(element.HeightMillimeters));
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
                    Width = width,
                    Height = height,
                    Margin = 1,
                    PureBarcode = !element.ShowHumanReadableText
                }
            };

            var pixelData = writer.Write(value);
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

            return new Image
            {
                Source = source,
                Stretch = Stretch.Fill
            };
        }
        catch
        {
            return CreateTextBlock(element, value);
        }
    }

    private FrameworkElement CreateImageVisual(LabelDesignElement element)
    {
        var path = ResolveAssetPath(element.ImagePath);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return CreateTextBlock(element, "Image not found");
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        return new Image
        {
            Source = image,
            Stretch = Stretch.Uniform
        };
    }

    private static FrameworkElement CreateRectangleVisual(LabelDesignElement element) =>
        new ShapeRectangle
        {
            Stroke = ParseBrush(element.Foreground, Brushes.Black),
            StrokeThickness = ToDip(Math.Max(0.1, element.LineThicknessMillimeters)),
            StrokeDashArray = CreateStrokeDashArray(element.LineStyle),
            Fill = ParseBrush(element.Background, Brushes.Transparent)
        };

    private static FrameworkElement CreateLineVisual(LabelDesignElement element) =>
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

    private static Brush ParseBrush(string value, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            var converter = new BrushConverter();
            return (Brush)(converter.ConvertFromString(value) ?? fallback);
        }
        catch
        {
            return fallback;
        }
    }

    private string ResolveAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(assetBaseDirectory, path);
    }

    private static string GetValue(IReadOnlyDictionary<string, object?> row, string preferredField, string fallbackField)
    {
        if (row.TryGetValue(preferredField, out var preferredValue) && preferredValue is not null)
        {
            return FormatValue(preferredValue);
        }

        if (!string.IsNullOrWhiteSpace(fallbackField)
            && row.TryGetValue(fallbackField, out var fallbackValue)
            && fallbackValue is not null)
        {
            return FormatValue(fallbackValue);
        }

        return string.Empty;
    }

    private static string FormatValue(object value) =>
        value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
        };

    private static double ToDip(double millimeters) => millimeters * 96.0 / 25.4;
}
