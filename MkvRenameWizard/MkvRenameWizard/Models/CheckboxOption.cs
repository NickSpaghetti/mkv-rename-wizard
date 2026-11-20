namespace MkvRenameWizard.ViewModels;

public class CheckboxOption<T> : ViewModelBase
{
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                OnPropertyChanged(nameof(IsNotChecked));
            }
        }
    }

    public bool IsNotChecked => !IsChecked;

    public string Label
    {
        get;
        set => SetProperty(ref field, value);
    }

    public T Value { get; set; }

    public CheckboxOption(string label, T value)
    {
        Label = label;
        Value = value;
    }
}
