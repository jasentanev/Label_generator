using System.Diagnostics;
using System.IO;
using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.Templates;

namespace LabelGenerator.App.Designing;

public sealed class DesignerAppLauncher(string appBaseDirectory) : IDesignerLauncher
{
    public void Open(LabelTemplateProfile template)
    {
        var executablePath = FindDesignerExecutable();
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
            return;
        }

        var projectPath = FindDesignerProject();
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            Process.Start(new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        throw new FileNotFoundException("LabelGenerator.Designer executable or project was not found.");
    }

    private string? FindDesignerExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(appBaseDirectory, "LabelGenerator.Designer.exe"),
            Path.Combine(GetRepositoryRoot(), "LabelGenerator.Designer", "bin", "Debug", "net10.0-windows", "LabelGenerator.Designer.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private string? FindDesignerProject()
    {
        var projectPath = Path.Combine(GetRepositoryRoot(), "LabelGenerator.Designer", "LabelGenerator.Designer.csproj");
        return File.Exists(projectPath) ? projectPath : null;
    }

    private string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(appBaseDirectory);
        for (var i = 0; i < 4 && directory.Parent is not null; i++)
        {
            directory = directory.Parent;
        }

        return directory.FullName;
    }
}
