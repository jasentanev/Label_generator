using System.Printing;
using System.Windows.Controls;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Printing;

namespace LabelGenerator.App.Printing;

public sealed class WpfPrinterService(string assetBaseDirectory) : IPrinterService
{
    private readonly LabelDocumentFactory documentFactory = new(assetBaseDirectory);

    public IReadOnlyList<string> GetPrinterNames()
    {
        try
        {
            using var server = new LocalPrintServer();
            var defaultPrinter = server.DefaultPrintQueue?.Name;
            var printerNames = server
                .GetPrintQueues()
                .Select(queue => queue.Name)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(defaultPrinter)
                && printerNames.Remove(defaultPrinter))
            {
                printerNames.Insert(0, defaultPrinter);
            }

            return printerNames;
        }
        catch
        {
            return [];
        }
    }

    public void ShowPreview(
        LabelTemplateProfile template,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        PrintRequest request)
    {
        var document = documentFactory.Create(template, rows, request);
        var preview = new PreviewWindow(document);
        preview.ShowDialog();
    }

    public PrintResult Print(
        LabelTemplateProfile template,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        PrintRequest request)
    {
        try
        {
            var document = documentFactory.Create(template, rows, request);
            var printDialog = new PrintDialog();

            if (!string.IsNullOrWhiteSpace(request.PrinterName))
            {
                using var server = new LocalPrintServer();
                var queue = server
                    .GetPrintQueues()
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.Name,
                        request.PrinterName,
                        StringComparison.CurrentCultureIgnoreCase));

                if (queue is null)
                {
                    return PrintResult.Failed($"Printer '{request.PrinterName}' was not found.", request.SelectedKeys);
                }

                printDialog.PrintQueue = queue;
                printDialog.PrintTicket = queue.DefaultPrintTicket;
            }

            printDialog.PrintDocument(document.DocumentPaginator, $"Label Generator - {template.DisplayName}");
            return PrintResult.Success(LabelQuantityResolver.GetTotalLabelCount(template, rows, request.Copies));
        }
        catch (Exception ex)
        {
            return PrintResult.Failed(ex.Message, request.SelectedKeys);
        }
    }
}
