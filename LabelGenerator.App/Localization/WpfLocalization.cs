using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LabelGenerator.Core.Localization;

namespace LabelGenerator.App.Localization;

public static class WpfLocalization
{
    private static readonly DependencyProperty OriginalTextProperty = DependencyProperty.RegisterAttached(
        "OriginalText",
        typeof(string),
        typeof(WpfLocalization),
        new PropertyMetadata(null));

    public static void Apply(DependencyObject root, string? language)
    {
        try
        {
            if (root is Window window)
            {
                window.Title = Translate(window, window.Title, language);
            }

            ApplyToObject(root, language);

            var childrenCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childrenCount; index++)
            {
                Apply(VisualTreeHelper.GetChild(root, index), language);
            }
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Localization skipped for {root.GetType().Name}: {ex.Message}");
        }
    }

    private static void ApplyToObject(DependencyObject value, string? language)
    {
        switch (value)
        {
            case TextBlock textBlock:
                if (textBlock.Inlines.Count == 0)
                {
                    if (textBlock.ReadLocalValue(TextBlock.TextProperty) is string)
                    {
                        textBlock.Text = Translate(textBlock, textBlock.Text, language);
                    }

                    break;
                }

                foreach (var run in textBlock.Inlines.OfType<Run>().Where(run => run.ReadLocalValue(Run.TextProperty) is string))
                {
                    run.Text = Translate(run, run.Text, language);
                }

                break;
            case ContentControl { Content: string content } contentControl:
                contentControl.Content = Translate(contentControl, content, language);
                break;
            case HeaderedContentControl { Header: string header } headeredContentControl:
                headeredContentControl.Header = Translate(headeredContentControl, header, language);
                break;
            case DataGrid dataGrid:
                foreach (var column in dataGrid.Columns)
                {
                    if (column.Header is string columnHeader)
                    {
                        column.Header = Translate(column, columnHeader, language);
                    }
                }

                break;
        }
    }

    private static string Translate(DependencyObject owner, string currentValue, string? language)
    {
        var original = owner.GetValue(OriginalTextProperty) as string;
        if (original is null)
        {
            original = currentValue;
            owner.SetValue(OriginalTextProperty, original);
        }

        return UiTextLocalizer.Translate(original, language);
    }
}
