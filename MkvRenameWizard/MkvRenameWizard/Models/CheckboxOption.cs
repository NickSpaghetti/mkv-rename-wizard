using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class CheckboxOption<T> : ViewModelBase
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
    
    public T Value { get; set; }

    public CheckboxOption(string label, T value)
    {
        Label = label;
        Value = value;
    }
}