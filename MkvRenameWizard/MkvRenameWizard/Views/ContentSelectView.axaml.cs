using System.Collections.Frozen;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Services;
namespace MkvRenameWizard.Views;

public partial class ContentSelectView : UserControl
{
    private readonly IMkvFinderService _mkvFinderService;
    public IDictionary<string, FrozenSet<MkvFile>> FileDict = new Dictionary<string, FrozenSet<MkvFile>>();
    public ContentSelectView()
    {
        InitializeComponent();
        _mkvFinderService = new MkvFinderService();
    }

    private async void OpenFileButton_Clicked(object sender, RoutedEventArgs args)
    {
        FileDict = await _mkvFinderService.OpenMkvFiles(TopLevel.GetTopLevel(this));
    }
}