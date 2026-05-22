using System;
using System.Collections.Generic;
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

    private RailReorderDragData? _activeDrag;
    private Border? _dragSourceRow;
    private RailReorderSide? _dragSide;
    private int _lastInsertIndex = -1;
    private DispatcherTimer _autoScrollTimer;
    private double _autoScrollDirection;
    private Point? _lastDragPointInScroll;
        

    public ContentSelectView(Point? lastDragPointInScroll)
    {
        _lastDragPointInScroll = lastDragPointInScroll;
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

    private void OnRootDragLeave(object? sender, DragEventArgs e)
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

    private async void OnGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border grip || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var rowVm = FindRowViewModel(grip);
        if (rowVm == null || DataContext is not ContentSelectViewModel vm)
        {
            return;
        }
        
        var rowBorder = FindRowContainer(grip);
        if (rowBorder == null)
        {
            return;
        }

        var side = grip.Classes.Contains(Constants.AxamlClasses.EpisodeGripClass) ? RailReorderSide.Episode : RailReorderSide.File;
        var isMoveLinked = vm.LinkRailReorder || (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var dragData = new RailReorderDragData(side, rowVm.ZeroBasedIndex, isMoveLinked);
        var startPoint = e.GetPosition(grip);

        e.Handled = true;

        var hasMoved = await WaitForDragThresholdAsync(grip, e, startPoint);
        if (!hasMoved)
        {
            return;
        }
        
        _activeDrag = dragData;
        _dragSourceRow = rowBorder;
        _dragSide = side;
        _lastInsertIndex = -1;
        
        vm.BeginRailDrag(dragData);
        BeginRailDragUi(rowBorder, rowVm, isMoveLinked);

        var dataTransfer = RailReorderDragFormats.CreateTransfer(dragData);
        var result = DragDropEffects.None;

        try
        {
            result = await DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Move);
        }
        finally
        {
            if (result != DragDropEffects.Move && DataContext is ContentSelectViewModel cancelVm)
            {
                cancelVm.CancelRailDrag();
            }
            else if (DataContext is ContentSelectViewModel endVm)
            {
                endVm.EndRailDrag();
            }

            ClearRailDragUi();

        }

    }

    private static async Task<bool> WaitForDragThresholdAsync(Border grip, PointerPressedEventArgs pointerPressedEvent,
        Point startPoint)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>();

        void OnRelease(object? sender, PointerReleasedEventArgs releasedEventArgs)
        {
            grip.PointerReleased -= OnRelease;
            grip.PointerMoved -= OnMoved;
            taskCompletionSource.TrySetResult(true);
        }

        void OnMoved(object? sender, PointerEventArgs pointerEventArgs)
        {
            var point = pointerEventArgs.GetPosition(grip);
            if (Math.Abs(point.X - startPoint.X) < DragThreshold && Math.Abs(point.Y - startPoint.Y) < DragThreshold)
            {
                return;
            }
            
            grip.PointerReleased -= OnRelease;
            grip.PointerMoved -= OnMoved;
            taskCompletionSource.TrySetResult(true);
        }
        
        grip.PointerMoved += OnMoved;
        grip.PointerReleased += OnRelease;

        return await taskCompletionSource.Task;
    }

    private void BeginRailDragUi(Border sourceRow, RailMatchRowViewModel rowVm, bool isMoveLinked)
    {
        sourceRow.Classes.Add("dragging");

        if (DragGhostLabel != null)
        {
            DragGhostLabel.Text = isMoveLinked
                ? (rowVm.HasEpisode ? $"{rowVm.EpisodeCode}  {rowVm.EpisodeTitle}" : rowVm.FileDisplayName)
                : _dragSide == RailReorderSide.Episode
                    ? $"{rowVm.EpisodeCode}  {rowVm.EpisodeTitle}"
                    : rowVm.FileDisplayName;
        }

        if (DragGhost != null)
        {
            DragGhost.IsVisible = true;
        }

        if (DragInsertLine.IsVisible != null)
        {
            DragInsertLine.IsVisible = true;
        }
    }

    private void ClearRailDragUi()
    {
        StopAutoScroll();

        if (_dragSourceRow != null)
        {
            _dragSourceRow.Classes.Remove(Constants.AxamlClasses.DraggingClass);
        }
        
        _dragSourceRow = null;
        _activeDrag = null;
        _dragSide = null;
        _lastInsertIndex = -1;
        _lastDragPointInScroll = null;

        if (DragGhost != null)
        {
            DragGhost.IsVisible = false;
        }

        if (DragInsertLine != null)
        {
            DragInsertLine.IsVisible = false;
        }
    }

    private void UpdateRailDragFeedback(DragEventArgs e, RailReorderDragData dragData,
        ContentSelectViewModel contentSelectViewModel)
    {
        var scroll = MatchRailsScrollViewer;
        var overlay = DragOverlay;

        if (scroll == null || overlay == null)
        {
            return;
        }

        var pointerInScroll = e.GetPosition(scroll);
        _lastDragPointInScroll = pointerInScroll;
        UpdateAutoScrollPoint(pointerInScroll, scroll);

        var insertIndex = ResolveInsertIndex(e);
        if (_lastInsertIndex != insertIndex)
        {
            _lastInsertIndex = insertIndex;
            contentSelectViewModel.PreviewDragReorder(dragData, insertIndex);
        }
        
        PositionInsertLine(e, dragData, scroll, overlay, insertIndex);
        PositionGhost(pointerInScroll, overlay);
    }
    

    private void PositionInsertLine(DragEventArgs e, RailReorderDragData dragData, ScrollViewer scrollViewer,
        Canvas overlay, int insertIndex)
    {
        var line = DragInsertLine;
        if (line == null)
        {
            return;
        }

        var rows = GetMatchRows();
        double lineY;
        double lineLeft;
        double lineWidth;

        if (rows.Count == 0)
        {
            lineY = 0;
            lineLeft = 0;
            lineWidth = overlay.Bounds.Width;
        }
        else if (insertIndex >= rows.Count)
        {
            var last = rows.LastOrDefault();
            if (last == null)
            {
                return;
            }
            var topLeft = last.TranslatePoint(new Point(0, last.Bounds.Height), overlay);
            lineY = topLeft?.Y ?? 0;
            lineLeft = 0;
            lineWidth = overlay.Bounds.Width;
        }
        else
        {
            var row = rows[insertIndex];
            var topLeft = row.TranslatePoint(new Point(0, 0), overlay);
            lineY = topLeft?.Y ?? 0;
            lineLeft = 0;
            lineWidth = overlay.Bounds.Width;

            if (!dragData.IsMoveLinked)
            {
                var cell = FindPanelCell(row, dragData.ReorderSide);
                if (cell != null)
                {
                    var cellTopLeft = cell.TranslatePoint(new Point(0, 0), overlay);
                    if (cellTopLeft != null)
                    {
                        lineLeft = cellTopLeft.Value.X;
                        lineWidth = cell.Bounds.Width;
                    }
                }
            }
        }
        
        Canvas.SetLeft(line, lineLeft);
        Canvas.SetTop(line, lineY - 1);
        line.Width = Math.Max(lineWidth, 1);
        line.IsVisible = true;
    }

    private void PositionGhost(Point pointerInScroll, Canvas overlay)
    {
        var ghost = DragGhost;
        if (ghost == null)
        {
            return;
        }

        const double offSetX = 12;
        const double offSetY = 10;
        
        var x = pointerInScroll.X + offSetX;
        var y = pointerInScroll.Y + offSetY;

        if (overlay.Bounds.Width > 0)
        {
            x = Math.Clamp(x,0,Math.Max(0, overlay.Bounds.Width - 200));
        }
        
        Canvas.SetLeft(ghost, x);
        Canvas.SetTop(ghost, y);
    }

    private int ResolveInsertIndex(DragEventArgs e)
    {
        if (e.Source is Control source && FindRowContainer(source) is { } rowBorder)
        {
            return ComputeInsertIndex(rowBorder, e);
        }

        var scroll = MatchRailsScrollViewer;
        var overlay = DragOverlay;
        if (scroll == null || overlay == null)
        {
            return 0;
        }
        
        var position = e.GetPosition(scroll);
        var rows = GetMatchRows();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var topLeft = row.TranslatePoint(new Point(0, 0), scroll);
            if (topLeft == null)
            {
                continue;
            }

            var bounds = new Rect(topLeft.Value, row.Bounds.Size);
            if (!bounds.Contains(position))
            {
                continue;
            }
            
            return position.Y > bounds.Y + bounds.Height / 2 ? i + 1 : i;
        }

        return rows.Count;
    }

    private void UpdateAutoScrollPoint(Point pointerInScroll, ScrollViewer scroll)
    {
        var height = scroll.Viewport.Height;
        if (height <= 0)
        {
            StopAutoScroll();
            return;
        }

        if (pointerInScroll.Y < AutoScrollEdge)
        {
            var intensity = 1 - pointerInScroll.Y / AutoScrollEdge;
            StartAutoScroll(-AutoScrollMaxSpeed * intensity, scroll);
        }
        else if (pointerInScroll.Y > height - AutoScrollEdge)
        {
            var dist = pointerInScroll.Y - (height - AutoScrollEdge);
            var intensity = Math.Min(1, dist / AutoScrollEdge);
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
            var offSet = scroll.Offset;
            var maxY = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            var newY = Math.Clamp(offSet.Y + _autoScrollDirection, 0, maxY);
            scroll.Offset = new Vector(offSet.X, newY);

            if (_activeDrag is { } dragData && DataContext is ContentSelectViewModel contentSelectViewModel &&
                _lastDragPointInScroll is { } lastDragPoint)
            {
                var insertIndex = HitTestInsertIndex(scroll, lastDragPoint);
                if (_lastInsertIndex != insertIndex)
                {
                    _lastInsertIndex = insertIndex;
                    contentSelectViewModel.PreviewDragReorder(dragData,insertIndex);
                }
                
                var overlay = DragOverlay;
                if (overlay != null)
                {
                    PositionInsertLineForIndex(dragData,scroll,overlay,insertIndex);
                    PositionGhost(lastDragPoint, overlay);
                }
            }
        });
        _autoScrollTimer.Start();
    }

    private void PositionInsertLineForIndex(RailReorderDragData dragData, ScrollViewer scroll, Canvas overlay,
        int insertIndex)
    {
        var line = DragInsertLine;
        if (line == null)
        {
            return;
        }
        var rows = GetMatchRows();
        double lineY;
        double lineLeft;
        double lineWidth;

        if (rows.Count == 0 || insertIndex >= rows.Count)
        {
            if (rows.Count > 0)
            {
                var last = rows.LastOrDefault();
                if (last == null)
                {
                    return;
                }

                var topLeft = last.TranslatePoint(new Point(0, last.Bounds.Height), overlay);
                lineY = topLeft?.Y ?? 0;
            }
            else
            {
                lineY = 0;
            }

            lineLeft = 0;
            lineWidth = overlay.Bounds.Width;
        }
        else
        {
            if (insertIndex < 0)
            {
                return;
            }
            var row =  rows[insertIndex];
            var topLeft = row.TranslatePoint(new Point(0, 0), overlay);
            lineY = topLeft?.Y ?? 0;
            lineLeft = 0;
            lineWidth = overlay.Bounds.Width;

            if (dragData.IsMoveLinked)
            {
                return;
            }

            var cell = FindPanelCell(row, dragData.ReorderSide);
            if (cell == null)
            {
                return;
            }
            var cellTopLeft = cell.TranslatePoint(new Point(0, 0), overlay);
            if (cellTopLeft == null)
            {
                return;
            }
            lineLeft = cellTopLeft.Value.X;
            lineWidth = cell.Bounds.Width;

        }
        Canvas.SetLeft(line, lineLeft);
        Canvas.SetTop(line,lineY - 1);
        line.Width = Math.Max(lineWidth, 1);
        line.IsVisible = true;

    }

    private static int HitTestInsertIndex(ScrollViewer scroll, Point positionInScroll)
    {
        var rows = GetMatchRowsFrom(scroll);
        for(var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var topLeft = row.TranslatePoint(new Point(0, 0), scroll);
            if (topLeft is null)
            {
                continue;
            }

            var bounds = new Rect(topLeft.Value, row.Bounds.Size);
            if (!bounds.Contains(positionInScroll))
            {
                continue;
            }

            return positionInScroll.Y > bounds.Y + bounds.Height / 2 ? i + 1 : i;
        }

        return rows.Count;
    }

    private void StopAutoScroll()
    {
        _autoScrollTimer.Stop();
        _autoScrollTimer = null;
        _autoScrollDirection = 0;
    }

    private List<Border> GetMatchRows()
    {
        if (MatchRailsScrollViewer == null)
        {
            return new List<Border>();
        }
        return GetMatchRowsFrom(MatchRailsScrollViewer);
    }

    private static List<Border> GetMatchRowsFrom(Visual root)
    {
        var rows = new List<Border>();
        CollectMatchRows(root, rows);
        var scroll = root as ScrollViewer ?? root;
        rows.Sort((a, b) =>
        {
            var ay = a.TranslatePoint(new Point(0, 0), scroll)?.Y ?? 0;
            var by = b.TranslatePoint(new Point(0, 0), scroll)?.Y ?? 0;
            return ay.CompareTo(by);
        });
        return rows;
    }

    private static void CollectMatchRows(Visual visual, List<Border> rows)
    {
        if (visual is Border { Classes.Count: > 0 } border &&
            border.Classes.Contains(Constants.AxamlClasses.BorderMatchRow))
        {
            rows.Add(border);
            return;
        }

        if (visual is Control control)
        {
            foreach (var child in control.GetVisualChildren())
            {
                CollectMatchRows(child, rows);
            }
        }
    }

    private static Border? FindPanelCell(Border row, RailReorderSide railReorderSide)
    {
        Border? found = null;
        Visit(row, c =>
        {
            if (found != null)
            {
                return;
            }

            if (c is Border { Classes.Count: > 0 } border &&
                border.Classes.Contains(Constants.AxamlClasses.BorderRailPaneCell))
            {
                var grid = border.Child as Grid;
                if (grid == null)
                {
                    return;
                }

                var isEpisode = grid.Children.OfType<Border>()
                    .Any(g => g.Classes.Contains(Constants.AxamlClasses.EpisodeGripClass));
                if (railReorderSide == RailReorderSide.Episode && isEpisode)
                {
                    found = border;
                }
                else if (railReorderSide == RailReorderSide.File && !isEpisode)
                {
                    found = border;
                }
            }
        });
        return found;
    }

    private static void Visit(Visual visual, Action<Visual> action)
    {
        action(visual);
        if (visual is Control control)
        {
            foreach (var child in control.GetVisualChildren())
            {
                if (child is Visual childVisual)
                {
                    Visit(childVisual, action);   
                }
            }
        }
    }

    private async Task OnDropZoneDrop(object? sender, DragEventArgs e)
    {
        if (RailReorderDragFormats.TryGet(e.DataTransfer, out _))
        {
            e.Handled = true;
            return;
        }

        await HandelFileImportDropAsync(e);
    }

    private async Task HandelFileImportDropAsync(DragEventArgs e)
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

    private Border? FindRowContainer(Control control)
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

    private static int ComputeInsertIndex(Border rowBorder, DragEventArgs e)
    {
        if (rowBorder.DataContext is not RailMatchRowViewModel railMatchRowViewModel)
        {
            return 0;
        }

        var position = e.GetPosition(rowBorder);
        return position.Y > rowBorder.Bounds.Height / 2
            ? railMatchRowViewModel.ZeroBasedIndex + 1
            : railMatchRowViewModel.ZeroBasedIndex;
    }
}