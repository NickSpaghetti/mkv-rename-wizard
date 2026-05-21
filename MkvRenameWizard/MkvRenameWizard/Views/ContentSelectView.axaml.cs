using System.Collections.Frozen;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Services;
namespace MkvRenameWizard.Views;

public partial class ContentSelectView : UserControl
{
    private readonly IMkvFinderService _mkvFinderService;
    private readonly ILogger<ContentSelectView> _logger;
    private const double DragThreshold = 4;
    private const double AutoScrollEdge = 40;
    private const double AutoMaxSpeed = 14;
    public IDictionary<string, FrozenSet<MkvFile>> FileDict = new Dictionary<string, FrozenSet<MkvFile>>();

    private async void OpenFileButton_Clicked(object sender, RoutedEventArgs args)
    {
        FileDict = new Dictionary<string, FrozenSet<MkvFile>>();
    }
}