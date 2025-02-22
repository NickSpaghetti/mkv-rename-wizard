using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class CheckboxOption : ViewModelBase
{
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }

    private string _label;
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    public CheckboxOption(string label)
    {
        Label = label;
    }
}