using LabelGenerator.Core.Models;

namespace LabelGenerator.App.ViewModels;

public sealed class ColumnFilterViewModel(string columnName) : ViewModelBase
{
    private string pattern = string.Empty;
    private bool isCaseSensitive;
    private bool isEnabled;

    public string ColumnName { get; } = columnName;

    public string Pattern
    {
        get => pattern;
        set
        {
            if (SetProperty(ref pattern, value))
            {
                IsEnabled = !string.IsNullOrWhiteSpace(value);
            }
        }
    }

    public bool IsCaseSensitive
    {
        get => isCaseSensitive;
        set => SetProperty(ref isCaseSensitive, value);
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public ColumnFilter ToModel() =>
        new()
        {
            ColumnName = ColumnName,
            Pattern = Pattern,
            IsCaseSensitive = IsCaseSensitive,
            IsEnabled = IsEnabled
        };

    public void Clear()
    {
        Pattern = string.Empty;
        IsEnabled = false;
        IsCaseSensitive = false;
    }
}
