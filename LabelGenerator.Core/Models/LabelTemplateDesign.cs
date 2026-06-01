namespace LabelGenerator.Core.Models;

public sealed class LabelTemplateDesign
{
    public bool ShowBorder { get; set; } = true;

    public List<LabelDesignElement> Elements { get; set; } = [];

    public static LabelTemplateDesign CreateDefaultProductDesign() =>
        new()
        {
            ShowBorder = true,
            Elements =
            [
                new LabelDesignElement
                {
                    Type = LabelElementType.Field,
                    FieldName = "ProductName",
                    XMillimeters = 4,
                    YMillimeters = 3,
                    WidthMillimeters = 88,
                    HeightMillimeters = 10,
                    FontSize = 13,
                    IsBold = true
                },
                new LabelDesignElement
                {
                    Type = LabelElementType.Field,
                    FieldName = "ProductCode",
                    XMillimeters = 4,
                    YMillimeters = 14,
                    WidthMillimeters = 42,
                    HeightMillimeters = 7,
                    FontSize = 10
                },
                new LabelDesignElement
                {
                    Type = LabelElementType.Text,
                    Text = "Batch:",
                    XMillimeters = 4,
                    YMillimeters = 22,
                    WidthMillimeters = 16,
                    HeightMillimeters = 6,
                    FontSize = 8
                },
                new LabelDesignElement
                {
                    Type = LabelElementType.Field,
                    FieldName = "BatchNo",
                    XMillimeters = 19,
                    YMillimeters = 22,
                    WidthMillimeters = 26,
                    HeightMillimeters = 6,
                    FontSize = 8
                },
                new LabelDesignElement
                {
                    Type = LabelElementType.Barcode,
                    FieldName = "Barcode",
                    XMillimeters = 47,
                    YMillimeters = 15,
                    WidthMillimeters = 47,
                    HeightMillimeters = 18,
                    FontSize = 8,
                    BarcodeSymbology = BarcodeSymbology.Code128,
                    ShowHumanReadableText = true
                }
            ]
        };

    public static LabelTemplateDesign CreateDefaultShippingDesign() =>
        new()
        {
            ShowBorder = true,
            Elements =
            [
                new LabelDesignElement
                {
                    Type = LabelElementType.Text,
                    Text = "SHIPMENT LABEL",
                    XMillimeters = 8,
                    YMillimeters = 8,
                    WidthMillimeters = 90,
                    HeightMillimeters = 10,
                    FontSize = 18,
                    IsBold = true
                },
                new LabelDesignElement
                {
                    Type = LabelElementType.Field,
                    FieldName = "ProductName",
                    XMillimeters = 8,
                    YMillimeters = 25,
                    WidthMillimeters = 130,
                    HeightMillimeters = 14,
                    FontSize = 16,
                    IsBold = true
                },
                new LabelDesignElement
                {
                    Type = LabelElementType.Field,
                    FieldName = "BatchNo",
                    XMillimeters = 8,
                    YMillimeters = 43,
                    WidthMillimeters = 70,
                    HeightMillimeters = 10,
                    FontSize = 12
                },
                new LabelDesignElement
                {
                    Type = LabelElementType.Barcode,
                    FieldName = "Barcode",
                    XMillimeters = 8,
                    YMillimeters = 63,
                    WidthMillimeters = 150,
                    HeightMillimeters = 35,
                    BarcodeSymbology = BarcodeSymbology.Code128,
                    ShowHumanReadableText = true
                }
            ]
        };
}
