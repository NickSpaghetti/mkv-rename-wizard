using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MkvRenameWizard.ViewModels;
using MkvRenameWizard.Views;

namespace MkvRenameWizard;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        ContentSearchViewModel => new ContentSearchView(),
        ContentSelectViewModel => new ContentSelectView(),
        OutputFileConfigurationViewModel => new OutputFileConfigurationView(),
        RenameResultViewModel => new RenameResultView(),
        WizardViewModel => new WizardView(),
        null => null,
        _ => new TextBlock { Text = "Not Found: " + param.GetType().FullName }
    };

    public bool Match(object? data) => data is ViewModelBase;
}
