namespace LabelGenerator.App;

public sealed class StartupOptions
{
    public bool UserMode { get; init; }

    public string? Label { get; init; }

    public string? DataSource { get; init; }

    public StartupActionMode ActionMode { get; init; }

    public bool ShowConfigureButton => !UserMode && string.IsNullOrWhiteSpace(DataSource);

    public bool ShowDesignerButton => !UserMode && string.IsNullOrWhiteSpace(Label);

    public bool ShowDataSourceSelector => string.IsNullOrWhiteSpace(DataSource);

    public bool ShowTemplateSelector => string.IsNullOrWhiteSpace(Label);

    public static StartupOptions Parse(IEnumerable<string> args)
    {
        var userMode = false;
        string? label = null;
        string? dataSource = null;
        var actionMode = StartupActionMode.None;

        var items = args.ToList();
        for (var index = 0; index < items.Count; index++)
        {
            var current = items[index];
            if (string.Equals(current, "-Users", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "--Users", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "/Users", StringComparison.OrdinalIgnoreCase))
            {
                userMode = true;
                continue;
            }

            if (string.Equals(current, "-Preview", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "--Preview", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "/Preview", StringComparison.OrdinalIgnoreCase))
            {
                actionMode = StartupActionMode.Preview;
                continue;
            }

            if (string.Equals(current, "-Print", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "--Print", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "/Print", StringComparison.OrdinalIgnoreCase))
            {
                actionMode = StartupActionMode.Print;
                continue;
            }

            if (IsOption(current, "label"))
            {
                label = ReadOptionValue(current, "label", items, ref index);
                continue;
            }

            if (IsOption(current, "datasource") || IsOption(current, "dataSource"))
            {
                dataSource = ReadOptionValue(current, "datasource", items, ref index);
            }
        }

        return new StartupOptions
        {
            UserMode = userMode,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            DataSource = string.IsNullOrWhiteSpace(dataSource) ? null : dataSource.Trim(),
            ActionMode = actionMode
        };
    }

    private static bool IsOption(string value, string optionName) =>
        value.Equals($"-{optionName}", StringComparison.OrdinalIgnoreCase)
        || value.Equals($"--{optionName}", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith($"-{optionName}=", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith($"--{optionName}=", StringComparison.OrdinalIgnoreCase)
        || value.Equals($"/{optionName}", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith($"/{optionName}=", StringComparison.OrdinalIgnoreCase);

    private static string? ReadOptionValue(string current, string optionName, IReadOnlyList<string> args, ref int index)
    {
        var equalsIndex = current.IndexOf('=');
        if (equalsIndex >= 0 && equalsIndex < current.Length - 1)
        {
            return current[(equalsIndex + 1)..];
        }

        if (index + 1 >= args.Count || args[index + 1].StartsWith('-') || args[index + 1].StartsWith('/'))
        {
            return null;
        }

        index++;
        return args[index];
    }
}
