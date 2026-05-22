using System.Collections.Frozen;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Models.Rail;
using MkvRenameWizard.Services;
using MkvRenameWizard.ViewModels;

namespace MkvRenameWizard.Views;

public partial class ContentSelectView : UserControl
{
    private const double DragThreshold = 4;
    private const double AutoScrollEdge = 40;
    private const double AutoMaxSpeed = 14;

    private RailReorderDragData? _activeDrag;
    private Border? _dragSourceRow;
    private RailReorderSide? _dragSide;
    private int _lastInsertIndex = -1;
    private DispatcherTimer _autoScrollTimer;
    private double _autoScrollDirection;
    private Point? _lastDRagpointInScroll;
        

    public ContentSelectView()
    {
        InitializeComponent();
        
        AddHandler(DragDrop.DragOverEvent, OnRootDragOver, RoutingStrategies.Bubble);
        AddHandler(DragDrop.DropEvent, OnRootDrop, RoutingStrategies.Bubble);
        AddHandler(DragDrop.DragLeaveEvent, OnRootDragLeave, RoutingStrategies.Bubble);
    }

    private ScrollViewer? MatchRailsScrollViewer => MatchRailsScroll;
    private Canvas? DragOverlay => RailDragOverlay;

    private void OnMatchRowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border row || row.DataContext is not RailMatchRowViewModel rowVm)
        {
            return;
        }

        RailSettleAnimator.RunIfNeeded(row, rowVm.SettleType, rowVm.SettleDirection);
    }

    private void OnRootDragOver(object? sender, DragEventArgs e)
    {
        if (RailReorderDragFormats.TryGet(e.DataTransfer, out var railReorderDragData))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;

            if (DataContext is ContentSelectViewModel vm)
            {
                UpdateRailDragFeedback(e, railReorderDragData, vm);
            }
            
            return;
        }

        if (HasImportableFiles(e.DataTransfer))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }
        
        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnRootDragLeave(object? sender, RoutedEventArgs e)
    {
        if (!RailReorderDragFormats.TryGet(e.DataTransfer, out _))
        {
            return;
        }

        if (DragInsertLine != null)
        {
            DragInsertLine.IsVisible = false;
        }

        StopAutoScroll();
    }

    private async void OnRootDrop(object? sender, DragEventArgs e)
    {
        if (RailReorderDragFormats.TryGet(e.DataTransfer, out var railReorderDragData))
        {
            if (DataContext is ContentSelectViewModel vm)
            {
                var insertIndex = ResolveInsertIndex(e);
                vm.CommitRailDrag(railReorderDragData, insertIndex);
            }
            e.Handled = true;
            return;
        }

        if (e.Handled)
        {
            return;
        }

        await HandelFileImportDropAsync(e);
    }
    
    private async void OpenFileButton_Clicked(object sender, RoutedEventArgs args)
    {
        
    }
}