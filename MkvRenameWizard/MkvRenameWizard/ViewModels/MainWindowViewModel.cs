namespace MkvRenameWizard.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public WizardViewModel WizardViewModel { get; }

    public MainWindowViewModel(WizardViewModel wizardViewModel)
    {
        WizardViewModel = wizardViewModel;
    }

    public MainWindowViewModel()
    {
        throw new System.NotImplementedException();
    }
}