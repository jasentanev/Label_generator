using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LabelGenerator.App;

public partial class BootSplashWindow : Window
{
    public BootSplashWindow(string logoPath, string infoText)
    {
        InitializeComponent();

        LogoImage.Source = LoadBitmap(logoPath);
        InfoTextBlock.Text = infoText;
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
}
