namespace MkvRenameWizard.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public WizardViewModel WizardViewModel { get; }

    public MainWindowViewModel()
    {
        WizardViewModel = new WizardViewModel();
    }
}