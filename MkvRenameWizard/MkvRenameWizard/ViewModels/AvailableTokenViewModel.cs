using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public sealed class AvailableTokenViewModel(string name, string description, string exampleValue, bool isUsedInPattern)
    : ViewModelBase
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string ExampleValue { get; } = exampleValue;

    public bool IsUsedInPattern
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = isUsedInPattern;
}
