using LabelGenerator.Core.Models;

namespace LabelGenerator.App.Printing;

public interface IPrinterService
{
    IReadOnlyList<string> GetPrinterNames();

    void ShowPreview(
        LabelTemplateProfile template,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        PrintRequest request);

    PrintResult Print(
        LabelTemplateProfile template,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        PrintRequest request);
}
