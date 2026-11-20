using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MkvRenameWizard.Helpers;
using MkvRenameWizard.Models.Rail;
using MkvRenameWizard.ViewModels;

namespace MkvRenameWizard.Views;

public partial class ContentSelectView : UserControl
{
    private const double DragThreshold = 4;
    private const double AutoScrollEdge = 40;
    private const double AutoScrollMaxSpeed = 14;

    private ContentSelectViewModel? _subscribedVm;
    private Border? _capturedGrip;
    private IPointer? _capturedPointer;
    private Point _pressStartInGrip;
    private RailReorderDragData? _pendingDrag;
    private RailReorderDragData? _activeDrag;
    private Border? _dragSourceRow;
    private int _lastTargetIndex = -1;
    private DispatcherTimer? _autoScrollTimer;
    private double _autoScrollDirection;
    private Point? _lastDragPointInOverlay;

    public ContentSelectView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnExternalFileDragOver, RoutingStrategies.Bubble);
        AddHandler(DragDrop.DropEvent, OnExternalFileDrop, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Tunnel);
    }

    private ScrollViewer? MatchRailsScrollViewer => MatchRailsList?.FindDescendantOfType<ScrollViewer>();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_subscribedVm is not null)
        {
            _subscribedVm.RowSettled -= OnRowSettled;
            _subscribedVm = null;
        }

        if (DataContext is ContentSelectViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.RowSettled += OnRowSettled;
        }
    }

    private void OnRowSettled(int targetIndex, int direction, RailSettleType settleType)
    {
        if (MatchRailsList?.ContainerFromIndex(targetIndex) is not Control container)
        {
            return;
        }

        var rowBorder = container.FindDescendantOfType<Border>();
        if (rowBorder is not null && rowBorder.Classes.Contains(Constants.AxamlClasses.BorderMatchRow))
        {
            RailSettleAnimator.RunIfNeeded(rowBorder, settleType, direction);
        }
    }

    // --- Pointer-capture internal rail reordering (zero OS drag-drop round-trips) ---

    private void OnGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border grip || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var rowVm = FindRowViewModel(grip);
        var rowBorder = FindRowContainer(grip);
        if (rowVm == null || rowBorder == null || DataContext is not ContentSelectViewModel vm)
        {
            return;
        }

        var side = RailGrip.GetSide(grip);
        var isMoveLinked = vm.LinkRailReorder || (e.KeyModifiers & KeyModifiers.Shift) != 0;

        _capturedGrip = grip;
        _capturedPointer = e.Pointer;
        _pressStartInGrip = e.GetPosition(grip);
        _pendingDrag = new RailReorderDragData(side, rowVm.ZeroBasedIndex, isMoveLinked);
        _dragSourceRow = rowBorder;

        grip.PointerMoved += OnGripPointerMoved;
        grip.PointerReleased += OnGripPointerReleased;
        grip.PointerCaptureLost += OnGripPointerCaptureLost;
        e.Pointer.Capture(grip);
        e.Handled = true;
    }

    private void OnGripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_capturedGrip is null || DataContext is not ContentSelectViewModel vm)
        {
            return;
        }

        if (_activeDrag is null)
        {
            if (_pendingDrag is null || _dragSourceRow is null)
            {
                return;
            }

            var currentInGrip = e.GetPosition(_capturedGrip);
            if (Math.Abs(currentInGrip.X - _pressStartInGrip.X) < DragThreshold &&
                Math.Abs(currentInGrip.Y - _pressStartInGrip.Y) < DragThreshold)
            {
                return;
            }

            var rowVm = FindRowViewModel(_dragSourceRow);
            if (rowVm is null)
            {
                ReleaseGripCapture(commit: false);
                return;
            }

            _activeDrag = _pendingDrag;
            _lastTargetIndex = _activeDrag.SourceIndex;
            vm.BeginRailDrag(_activeDrag);
            BeginRailDragUi(_dragSourceRow, rowVm, _activeDrag);
        }

        UpdateDragFeedback(e.GetPosition(RailDragOverlay), vm);
        e.Handled = true;
    }

    private void OnGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ReleaseGripCapture(commit: _activeDrag is not null);
        e.Handled = true;
    }

    private void OnGripPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ReleaseGripCapture(commit: false);
    }

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _activeDrag is not null)
        {
            ReleaseGripCapture(commit: false);
            e.Handled = true;
        }
    }

    private void ReleaseGripCapture(bool commit)
    {
        if (_capturedGrip is not null)
        {
            _capturedGrip.PointerMoved -= OnGripPointerMoved;
            _capturedGrip.PointerReleased -= OnGripPointerReleased;
            _capturedGrip.PointerCaptureLost -= OnGripPointerCaptureLost;
        }

        if (_capturedPointer is not null && ReferenceEquals(_capturedPointer.Captured, _capturedGrip))
        {
            _capturedPointer.Capture(null);
        }

        _capturedGrip = null;
        _capturedPointer = null;
        _pendingDrag = null;

        if (_activeDrag is not null && DataContext is ContentSelectViewModel vm)
        {
            if (commit) vm.CommitRailDrag();
            else vm.CancelRailDrag();
        }

        ClearRailDragUi();
    }

    private void BeginRailDragUi(Border sourceRow, RailMatchRowViewModel rowVm, RailReorderDragData dragData)
    {
        sourceRow.Classes.Add(Constants.AxamlClasses.DraggingClass);

        var isEpisodeSideDragged = dragData.IsMoveLinked ? rowVm.HasEpisode : dragData.ReorderSide == RailReorderSide.Episode;

        if (DragGhostIndex != null)
        {
            DragGhostIndex.Text = rowVm.IndexLabel;
        }

        if (DragGhostLabel != null)
        {
            DragGhostLabel.Text = isEpisodeSideDragged
                ? $"{rowVm.EpisodeCode}  {rowVm.EpisodeTitle}"
                : rowVm.FileDisplayName;
        }

        if (DragGhostDetail != null)
        {
            DragGhostDetail.Text = isEpisodeSideDragged
                ? rowVm.EpisodeRunTimeLabel
                : rowVm.FileSizeLabel;
        }

        if (DragGhost != null)
        {
            DragGhost.IsVisible = true;
        }

        if (DragInsertLine != null)
        {
            DragInsertLine.IsVisible = true;
        }
    }

    private void ClearRailDragUi()
    {
        StopAutoScroll();

        _dragSourceRow?.Classes.Remove(Constants.AxamlClasses.DraggingClass);
        _dragSourceRow = null;
        _activeDrag = null;
        _lastTargetIndex = -1;
        _lastDragPointInOverlay = null;

        if (DragGhost != null) DragGhost.IsVisible = false;
        if (DragInsertLine != null) DragInsertLine.IsVisible = false;
    }

    private void UpdateDragFeedback(Point pointerInOverlay, ContentSelectViewModel vm)
    {
        var overlay = RailDragOverlay;
        var scroll = MatchRailsScrollViewer;
        if (overlay == null || _activeDrag is null)
        {
            return;
        }

        _lastDragPointInOverlay = pointerInOverlay;
        if (scroll is not null)
        {
            UpdateAutoScrollPoint(pointerInOverlay, scroll);
        }

        var targetIndex = ResolveTargetRowIndex(pointerInOverlay, vm);
        if (targetIndex >= 0 && targetIndex != _lastTargetIndex)
        {
            _lastTargetIndex = targetIndex;
            vm.PreviewDragReorder(_activeDrag, targetIndex);
        }

        PositionInsertLine(_activeDrag, overlay, targetIndex);
        PositionGhost(pointerInOverlay, overlay);
    }

    private int ResolveTargetRowIndex(Point pointerInOverlay, ContentSelectViewModel vm)
    {
        if (MatchRailsList == null || RailDragOverlay == null || vm.MatchRows.Count == 0)
        {
            return 0;
        }

        var closestIndex = -1;
        var closestDist = double.MaxValue;

        foreach (var container in MatchRailsList.GetRealizedContainers())
        {
            if (container.DataContext is not RailMatchRowViewModel rowVm)
            {
                continue;
            }

            var topLeft = container.TranslatePoint(new Point(0, 0), RailDragOverlay);
            if (topLeft is null)
            {
                continue;
            }

            var top = topLeft.Value.Y;
            var height = container.Bounds.Height;
            if (pointerInOverlay.Y >= top && pointerInOverlay.Y <= top + height)
            {
                return rowVm.ZeroBasedIndex;
            }

            var centerDist = Math.Abs(pointerInOverlay.Y - (top + height / 2));
            if (centerDist < closestDist)
            {
                closestDist = centerDist;
                closestIndex = rowVm.ZeroBasedIndex;
            }
        }

        return closestIndex >= 0 ? closestIndex : 0;
    }

    private void PositionInsertLine(RailReorderDragData dragData, Canvas overlay, int targetIndex)
    {
        var line = DragInsertLine;
        if (line == null || MatchRailsList == null)
        {
            return;
        }

        var container = MatchRailsList.ContainerFromIndex(targetIndex);
        if (container is null)
        {
            line.IsVisible = false;
            return;
        }

        var topLeft = container.TranslatePoint(new Point(0, 0), overlay);
        if (topLeft is null)
        {
            line.IsVisible = false;
            return;
        }

        var lineY = topLeft.Value.Y;
        var lineLeft = 0.0;
        var lineWidth = overlay.Bounds.Width;

        if (!dragData.IsMoveLinked)
        {
            // Columns are "*, 48, *": Episode is left half, File is right half
            var halfWidth = Math.Max(0, (overlay.Bounds.Width - 48) / 2);
            if (dragData.ReorderSide == RailReorderSide.Episode)
            {
                lineLeft = 0;
                lineWidth = halfWidth;
            }
            else
            {
                lineLeft = halfWidth + 48;
                lineWidth = halfWidth;
            }
        }

        Canvas.SetLeft(line, lineLeft);
        Canvas.SetTop(line, lineY - 1);
        line.Width = Math.Max(lineWidth, 1);
        line.IsVisible = true;
    }

    private void PositionGhost(Point pointerInOverlay, Canvas overlay)
    {
        var ghost = DragGhost;
        if (ghost == null)
        {
            return;
        }

        var x = pointerInOverlay.X + 12;
        var y = pointerInOverlay.Y + 10;

        if (overlay.Bounds.Width > 0)
        {
            x = Math.Clamp(x, 0, Math.Max(0, overlay.Bounds.Width - 200));
        }

        Canvas.SetLeft(ghost, x);
        Canvas.SetTop(ghost, y);
    }

    private void UpdateAutoScrollPoint(Point pointerInOverlay, ScrollViewer scroll)
    {
        var height = scroll.Viewport.Height;
        if (height <= 0)
        {
            StopAutoScroll();
            return;
        }

        if (pointerInOverlay.Y < AutoScrollEdge)
        {
            var intensity = Math.Clamp(1 - pointerInOverlay.Y / AutoScrollEdge, 0, 1);
            StartAutoScroll(-AutoScrollMaxSpeed * intensity, scroll);
        }
        else if (pointerInOverlay.Y > height - AutoScrollEdge)
        {
            var dist = pointerInOverlay.Y - (height - AutoScrollEdge);
            var intensity = Math.Clamp(dist / AutoScrollEdge, 0, 1);
            StartAutoScroll(AutoScrollMaxSpeed * intensity, scroll);
        }
        else
        {
            StopAutoScroll();
        }
    }

    private void StartAutoScroll(double direction, ScrollViewer scroll)
    {
        _autoScrollDirection = direction;
        if (_autoScrollTimer != null)
        {
            return;
        }

        _autoScrollTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var offset = scroll.Offset;
            var maxY = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            var newY = Math.Clamp(offset.Y + _autoScrollDirection, 0, maxY);
            scroll.Offset = new Vector(offset.X, newY);

            if (_activeDrag is not null && DataContext is ContentSelectViewModel vm &&
                _lastDragPointInOverlay is { } lastPoint)
            {
                UpdateDragFeedback(lastPoint, vm);
            }
        });
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        _autoScrollTimer?.Stop();
        _autoScrollTimer = null;
        _autoScrollDirection = 0;
    }

    // --- External OS file drag-and-drop (.mkv import) ---

    private void OnExternalFileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasImportableFiles(e.DataTransfer) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnExternalFileDrop(object? sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        await HandleFileImportDropAsync(e);
    }

    private void OnDropZoneDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasImportableFiles(e.DataTransfer) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDropZoneDrop(object? sender, DragEventArgs e)
    {
        await HandleFileImportDropAsync(e);
    }

    private async Task HandleFileImportDropAsync(DragEventArgs e)
    {
        var paths = StorageItemPaths.GetExisingLocalPathsFromTransfer(e.DataTransfer);
        if (paths.Count == 0)
        {
            return;
        }

        e.Handled = true;
        if (DataContext is ContentSelectViewModel vm)
        {
            await vm.ImportFromPathsAsync(paths);
        }
    }

    private static bool HasImportableFiles(IDataTransfer transfer)
    {
        var files = transfer.TryGetFiles();
        return files is not null && files.Any() && StorageItemPaths.GetExisingLocalPathsFromTransfer(transfer).Count > 0;
    }

    private static RailMatchRowViewModel? FindRowViewModel(Control control)
    {
        var current = control;
        while (current != null)
        {
            if (current.DataContext is RailMatchRowViewModel vm)
            {
                return vm;
            }
            current = current.Parent as Control;
        }
        return null;
    }

    private static Border? FindRowContainer(Control control)
    {
        var current = control;
        while (current != null)
        {
            if (current is Border { Classes.Count: > 0 } border &&
                border.Classes.Contains(Constants.AxamlClasses.BorderMatchRow))
            {
                return border;
            }
            current = current.Parent as Control;
        }
        return null;
    }
}
