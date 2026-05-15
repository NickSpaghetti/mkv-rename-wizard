using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class CheckboxOption<T> : ViewModelBase
{
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            this.RaiseAndSetIfChanged(ref _isChecked, value);
            this.RaisePropertyChanged(nameof(IsNotChecked));
        }
    }
    public bool IsNotChecked => !IsChecked;

    public string Label
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
    
    public T Value { get; set; }

    public CheckboxOption(string label, T value)
    {
        Label = label;
        Value = value;
    }
}