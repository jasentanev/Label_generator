using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LabelGenerator.App.ViewModels;

namespace LabelGenerator.App;

public partial class QuickMarkPrintWindow : Window
{
    private readonly MainViewModel viewModel;
    private bool isProcessing;

    public QuickMarkPrintWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public event EventHandler? PrintedSuccessfully;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FocusScanBox();
    }

    private async void ScanTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ProcessScanAsync();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        await ProcessScanAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task ProcessScanAsync()
    {
        if (isProcessing)
        {
            return;
        }

        isProcessing = true;
        ScanButton.IsEnabled = false;
        ScanTextBox.IsEnabled = false;
        StatusTextBlock.Foreground = Brushes.DimGray;
        StatusTextBlock.Text = "Loading details and printing...";

        try
        {
            var result = await viewModel.QuickMarkAndPrintAsync(ScanTextBox.Text);
            ShowResult(result);

            if (result.IsSuccess)
            {
                PlaySuccessSound();
                PrintedSuccessfully?.Invoke(this, EventArgs.Empty);
                ScanTextBox.Clear();
            }
            else
            {
                PlayFailureSound();
                ScanTextBox.SelectAll();
            }
        }
        finally
        {
            isProcessing = false;
            ScanButton.IsEnabled = true;
            ScanTextBox.IsEnabled = true;
            FocusScanBox();
        }
    }

    private void ShowResult(QuickMarkPrintResult result)
    {
        KeysTextBlock.Text = result.Keys.Count == 0
            ? "-"
            : string.Join(", ", result.Keys);
        DetailRowsTextBlock.Text = result.DetailRowCount.ToString();
        LabelsTextBlock.Text = result.PrintedLabelCount.ToString();

        StatusTextBlock.Foreground = result.IsSuccess
            ? new SolidColorBrush(Color.FromRgb(25, 107, 36))
            : new SolidColorBrush(Color.FromRgb(164, 38, 44));
        StatusTextBlock.Text = result.Message;
    }

    private void FocusScanBox()
    {
        ScanTextBox.Focus();
        Keyboard.Focus(ScanTextBox);
    }

    private static void PlaySuccessSound()
    {
        _ = Task.Run(() =>
        {
            try
            {
                Console.Beep(880, 90);
            }
            catch
            {
                System.Media.SystemSounds.Beep.Play();
            }
        });
    }

    private static void PlayFailureSound()
    {
        _ = Task.Run(() =>
        {
            try
            {
                Console.Beep(220, 140);
                Console.Beep(180, 140);
            }
            catch
            {
                System.Media.SystemSounds.Hand.Play();
            }
        });
    }
}
