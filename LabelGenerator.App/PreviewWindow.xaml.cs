using System.Windows;
using System.Windows.Documents;

namespace LabelGenerator.App;

public partial class PreviewWindow : Window
{
    public PreviewWindow(FixedDocument document)
    {
        InitializeComponent();
        Viewer.Document = document;
    }
}
