using System.Text.Json.Serialization;

namespace LabelGenerator.Core.Models;

public sealed class LabelDesignElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public LabelElementType Type { get; set; } = LabelElementType.Text;

    public string Text { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public double XMillimeters { get; set; }

    public double YMillimeters { get; set; }

    public double WidthMillimeters { get; set; } = 30;

    public double HeightMillimeters { get; set; } = 8;

    public double FontSize { get; set; } = 10;

    public bool IsBold { get; set; }

    public LabelTextAlignment TextAlignment { get; set; } = LabelTextAlignment.Left;

    public string Foreground { get; set; } = "#000000";

    public string Background { get; set; } = "Transparent";

    public BarcodeSymbology BarcodeSymbology { get; set; } = BarcodeSymbology.Code128;

    public bool ShowHumanReadableText { get; set; } = true;

    public string ImagePath { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName =>
        Type switch
        {
            LabelElementType.Text => $"Text: {TrimForDisplay(Text)}",
            LabelElementType.Field => $"Field: {FieldName}",
            LabelElementType.Barcode => $"Barcode: {FieldName}",
            LabelElementType.Image => $"Image: {Path.GetFileName(ImagePath)}",
            LabelElementType.Rectangle => "Rectangle",
            _ => Type.ToString()
        };

    private static string TrimForDisplay(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        return value.Length <= 24 ? value : value[..24] + "...";
    }
}
