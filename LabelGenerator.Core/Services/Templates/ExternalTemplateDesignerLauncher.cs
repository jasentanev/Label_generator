using System.Diagnostics;
using LabelGenerator.Core.Models;

namespace LabelGenerator.Core.Services.Templates;

public sealed class ExternalTemplateDesignerLauncher(string baseDirectory) : IDesignerLauncher
{
    public void Open(LabelTemplateProfile template)
    {
        var templatePath = ResolvePath(template.TemplateFilePath);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Template file was not found.", templatePath);
        }

        var startInfo = string.IsNullOrWhiteSpace(template.DesignerExecutablePath)
            ? new ProcessStartInfo(templatePath) { UseShellExecute = true }
            : new ProcessStartInfo(template.DesignerExecutablePath, $"\"{templatePath}\"") { UseShellExecute = true };

        Process.Start(startInfo);
    }

    private string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path);
}
