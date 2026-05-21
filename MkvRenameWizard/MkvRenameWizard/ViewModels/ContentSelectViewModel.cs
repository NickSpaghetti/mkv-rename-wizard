using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.FileImport;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Models.Rail;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Services;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class ContentSelectViewModel : ViewModelBase
{
    private readonly ILogger<ContentSelectViewModel> _logger;
    private readonly IMkvFinderService _mkvFinderService;

    public ContentSelectViewModel(IMkvFinderService mkvFinderService, ILogger<ContentSelectViewModel> logger)
    {
        _mkvFinderService =  mkvFinderService;
        _logger = logger;

        MoveEpisodeUpCommand = ReactiveCommand.Create<int>(i => MoveChevronUp(Episodes, i));
        MoveEpisodeDownCommand = ReactiveCommand.Create<int>(i => MoveChevronDown(Episodes, i));
        MoveMkvFileUpCommand = ReactiveCommand.Create<int>(i => MoveChevronUp(MkvFiles, i));
        MoveMkvFileDownCommand = ReactiveCommand.Create<int>(i => MoveChevronDown(MkvFiles, i));
        OpenFilesCommand = ReactiveCommand.CreateFromTask(ExecuteOpenFilesCommand);
        OpenFolderCommand = ReactiveCommand.CreateFromTask(ExecuteOpenFolderCommand);
        ClearFilesCommand = ReactiveCommand.Create(ClearImport);
        OpenSystemSettingsCommand = ReactiveCommand.Create(TryOpenPermissionsSetting);
        PrimaryErrorCommand = ReactiveCommand.CreateFromTask(ExecutePrimaryErrorCommand);
        SecondaryErrorCommand = ReactiveCommand.CreateFromTask(ExecuteSecondaryErrorCommand);
        TogglePartialFailureDetailscommand = ReactiveCommand.Create(() => {ShowPartialFailureDetails = !ShowPartialFailureDetails;});
        SetLinkedReorderCommand = ReactiveCommand.Create(() => { LinkRailReorder = true; });
        SetIndependentReorderCommand = ReactiveCommand.Create(() => { LinkRailReorder = false; });
    }

    public Os CurrentOs { get; } = ResolveCurrentOs();
    public ObservableCollection<Episode> Episodes { get; } = new();
    public ObservableCollection<MkvFile> MkvFiles { get; } = new();
    public ObservableCollection<FileImportIssueViewModel> ImportIssues { get; } = new();
    public ObservableCollection<RailMatchRowViewModel> MatchRows { get; } = new();
    
    public ReactiveCommand<int, Unit> MoveEpisodeUpCommand { get;  }
    public ReactiveCommand<int, Unit> MoveEpisodeDownCommand { get;  }
    public ReactiveCommand<int, Unit> MoveMkvFileUpCommand { get;  }
    public ReactiveCommand<int, Unit> MoveMkvFileDownCommand { get;  }
    
    public ReactiveCommand<Unit, Unit> OpenFilesCommand { get;  }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get;  }
    public ReactiveCommand<Unit, Unit> ClearFilesCommand { get;  }
    public ReactiveCommand<Unit, Unit> OpenSystemSettingsCommand { get;  }
    public ReactiveCommand<Unit, Unit> PrimaryErrorCommand { get;  }
    public ReactiveCommand<Unit, Unit> SecondaryErrorCommand { get; }
    public ReactiveCommand<Unit, Unit> TogglePartialFailureDetailscommand { get;  }
    public ReactiveCommand<Unit, Unit> SetLinkedReorderCommand { get; }
    public ReactiveCommand<Unit, Unit> SetIndependentReorderCommand { get;  }

    private RailSettleHint? _lastSettle;
    private bool _deferMatchRefresh;
    private bool _suppressSettle;
    private List<Episode>? _dragEpisodeSnapshot;
    private List<MkvFile>? _dragFileSnapshot;
    private int _dragPreviewInsertIndex = -1;
    private DispatcherTimer? _settleClearTimer;

    public bool IsRailDragging { get; private set => this.RaiseAndSetIfChanged(ref field, value); }
    
    public int LinkAnimationVersion { get; private set => this.RaiseAndSetIfChanged(ref field, value); }
    
    public bool HasImportedAttempted { get; private set => this.RaiseAndSetIfChanged(ref field, value); }
    
    public bool ShowPartialFailureDetails
    {
        get;
        set { this.RaiseAndSetIfChanged(ref field, value); this.RaisePropertyChanged(nameof(PartialFailureDetailsToggleText)); }
    }

    public bool LinkRailReorder
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsLinkedModelSelected));
            this.RaisePropertyChanged(nameof(IsIndependentModeSelected));
            this.RaisePropertyChanged(nameof(IsShiftLinkHintVisible));
        }
    } = false;
    
    public bool IsLinkedModelSelected => LinkRailReorder;
    public bool IsIndependentModeSelected => !LinkRailReorder;
    public bool IsShiftLinkHintVisible => !LinkRailReorder;

    public string ShowName
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(EpisodeColumnSubtitle));
        }
    } = string.Empty;
    
    public int EpisodeCount => Episodes.Count;
    public int ImportedFileCount => MkvFiles.Count;
    public int IssueCount => ImportIssues.Count;
    public int PairedCount => Math.Min(Episodes.Count, MkvFiles.Count);
    public int SkippedCount => Math.Abs(EpisodeCount - ImportedFileCount) + IssueCount;
    public bool HasImportedFiles => ImportIssues.Count > 0;
    public bool HasIssues => ImportIssues.Count > 0;
    public bool HasPermissionDenied => ImportIssues.Any(issue => issue.Type == FileImportIssueType.PermissionDenied);
    public bool HasInvalidMkvIssues => ImportIssues.Any(issue => issue.Type == FileImportIssueType.InvalidContainer);
    public bool HasNoMkvIssues => ImportIssues.Any(issue => issue.Type == FileImportIssueType.NoSupportedFilesFound);
    public bool ShowEmptyImportPrompt => !HasImportedFiles && !HasNoMkvIssues;
    public bool ShowErrorPanel => HasImportedAttempted && !HasImportedFiles && HasIssues;
    public bool ShowPartialFailureBanner => HasImportedFiles && HasIssues;
    public bool ShowMatchRails => HasImportedFiles;
    public bool ShowImportPanel => !ShowMatchRails;
    public bool CanContinue => PairedCount > 0;
    public string PairedChipText => PairedCount == 1 ? "1 pared" : $"{PairedCount} pared";
    public string SkippedChipText => SkippedCount == 1 ? "1 skipped" : $"{SkippedCount} skipped";
    public string EpisodeColumnSubtitle => string.IsNullOrWhiteSpace(ShowName) 
        ? EpisodeCount == 1 ? "1 episode" : $"{EpisodeCount} episodes" :  $"{EpisodeCount} from {ShowName}";

    public string FileColumnSubtitle => HasImportedFiles ? $"{ImportedFileCount} mkv" : "Drop mkv file(s) here";
    public string EpisodeHeaderText => EpisodeColumnSubtitle;
    public string FileHeaderText => HasImportedFiles ? FileColumnSubtitle : "Drop mkv file(s) here";
    public string ImportButtonText => HasImportedFiles ? "Reimport" : "+ Import mkv files";

    public string ImportStatusText => HasImportedFiles ? FileColumnSubtitle :
        ShowErrorPanel ? ErrorEyebrowText : "Awaiting mkv files";

    public string FooterStatusText => HasImportedFiles ? $"{ImportedFileCount} file(s) imported" :
        ShowErrorPanel ? "Fix the issue(s) above to continue" : $"{ImportedFileCount} file(s) imported";
    
    public string ErrorEyebrowText => HasPermissionDenied ? "Permission denied" : HasInvalidMkvIssues ? $"{IssueCount} of {ImportedFileCount + IssueCount} files couldn't be parsed" : string.Empty;

    public string ErrorTitle => HasPermissionDenied ? "Can't read these files" :
        HasInvalidMkvIssues ? "These don't look like mkv files" : "No mkv files in this folder";

    public string ErrorDescription => HasPermissionDenied
        ? GetPermissionDeniedDescription()
            : HasInvalidMkvIssues
                ? "Files imported either hae the wrong container or corrupted."
                : "The folder you selected contains no .mkv files";

    public string PrimaryErrorActionText =>
        HasPermissionDenied ? GetPermissionDeniedDescription() : "Pick another folder";
    
    public bool ShowPrimaryErrorButton => !(HasPermissionDenied && OperatingSystem.IsLinux());

    public string SecondaryErrorActionText => HasPermissionDenied ? "Try a different folder" :
        HasInvalidMkvIssues ? "Skip these and continue" : "Show what's in there";
    public string PartialFailureTitle => $"{IssueCount} of  {ImportedFileCount + IssueCount} file(s) couldn't be imported";
    public string PartialFailureSummary => string.Join(" · ", ImportIssues.Take(3).Select(issue => $"{issue.DisplayName} - {issue.Reason}"));
    public string PartialFailureDetailsToggleText => ShowPartialFailureDetails ? "Hide details" : "Show details";

    public void BeginRailDrag(RailReorderDragData dragData)
    {
        IsRailDragging = true;
        _dragEpisodeSnapshot = Episodes.ToList();
        _dragFileSnapshot = MkvFiles.ToList();
        _dragPreviewInsertIndex = -1;
    }

    public void PreviewDragReorder(RailReorderDragData dragData, int insertIndex)
    {
        if (_dragEpisodeSnapshot == null)
        {
            return;
        }

        if (!IsValidDragSourceIndex(dragData, _dragEpisodeSnapshot.Count, _dragFileSnapshot?.Count ?? 0))
        {
            return;
        }
        
        insertIndex = Math.Clamp(insertIndex, 0, Math.Max(EpisodeCount, ImportedFileCount));
        if (insertIndex == _dragPreviewInsertIndex)
        {
            return;
        }
        
        _dragPreviewInsertIndex = insertIndex;
        _suppressSettle = true;

        RestoreListFromSnapshot();
        ApplyDragMove(dragData, insertIndex);
        RefreshMatchState();
    }

    public void CommitRailDrag(RailReorderDragData dragData, int insertIndex)
    {
        _suppressSettle = false;

        if (_dragEpisodeSnapshot == null)
        {
            ReorderFromDrag(dragData, insertIndex);
            EndRailDrag();
        }
        
        if (!IsValidDragSourceIndex(dragData, _dragEpisodeSnapshot?.Count ?? 0, _dragFileSnapshot?.Count ?? 0))
        {
            ClearDragSnapshot();
            IsRailDragging = false;
            RefreshMatchState();
            return;
        }
        
        insertIndex = Math.Clamp(insertIndex, 0, Math.Max(EpisodeCount, ImportedFileCount));
        if (insertIndex != _dragPreviewInsertIndex)
        {
            RestoreListFromSnapshot();
            ApplyDragMove(dragData, insertIndex);
        }

        var finalTo = FindDraggedItemFinalIndex(dragData);
        if (finalTo != dragData.SourceIndex)
        {
            PrepareSettle(finalTo, dragData.SourceIndex, dragData.IsMoveLinked);
        }

        ClearDragSnapshot();
        IsRailDragging = false;
        RefreshMatchState();
        ScheduleClearSettle();
    }

    private void ApplyDragMove(RailReorderDragData dragData, int insertIndex)
    {
        if (!IsValidDragSourceIndex(dragData, EpisodeCount, ImportedFileCount))
        {
            return;
        }
        
        _deferMatchRefresh = true;

        if (dragData.IsMoveLinked)
        {
            MoveItem(Episodes, dragData.SourceIndex, insertIndex);
            if (dragData.SourceIndex < ImportedFileCount)
            {
                MoveItem(MkvFiles, dragData.SourceIndex, insertIndex);
            }
        } 
        else if (dragData.ReorderSide == RailReorderSide.Episode)
        {
            MoveItem(Episodes, dragData.SourceIndex,insertIndex);
        }
        else
        {
            MoveItem(MkvFiles, dragData.SourceIndex, insertIndex);
        }
        
        _deferMatchRefresh = false;
    }

    public void CancelRailDrag()
    {
        RestoreListFromSnapshot();
        ClearDragSnapshot();
        IsRailDragging = false;
        _suppressSettle = false;
        RefreshMatchState();
    }

    public void EndRailDrag()
    {
        ClearDragSnapshot();
        IsRailDragging = false;
    }

    public void ReorderFromDrag(RailReorderDragData dragData, int targetIndex)
    {
        if (!IsValidDragSourceIndex(dragData, EpisodeCount, ImportedFileCount))
        {
            return;
        }
        
        if (dragData.IsMoveLinked)
        {
            MoveLinkedWithSettle(dragData.SourceIndex, targetIndex);
        }
        else if (dragData.ReorderSide == RailReorderSide.Episode)
        {
            MoveItemWithSettle(Episodes, dragData.SourceIndex, targetIndex, isLinked: false);
        }
        else
        {
            MoveItemWithSettle(MkvFiles, dragData.SourceIndex, targetIndex, isLinked: false);
        }
    }
    
    public void ReorderEpisode(int fromIndex, int toIndex) => MoveItemWithSettle(Episodes, fromIndex, toIndex, isLinked: false);
    public void ReorderMkvFile(int fromIndex, int toIndex) => MoveItemWithSettle(MkvFiles, fromIndex, toIndex, isLinked: false);
    public void ReorderLinkedRow(int fromIndex, int toIndex) => MoveLinkedWithSettle(fromIndex, toIndex);

    private void MoveLinkedWithSettle(int fromIndex, int toIndex)
    {
        var finalTo = TryComputeFinalIndex(Episodes, fromIndex, toIndex);
        if (finalTo == null)
        {
            return;
        }
        
        PrepareSettle(finalTo.Value, fromIndex, isLinked: true);
        _deferMatchRefresh = true;
        MoveItem(Episodes, fromIndex, toIndex);
        if (fromIndex < ImportedFileCount)
        {
            MoveItem(Episodes, fromIndex, toIndex);
        }
        _deferMatchRefresh = false;
        RefreshMatchState();
        ScheduleClearSettle();
    }

    private void MoveChevronUp<T>(ObservableCollection<T> rails, int fromIndex)
    {
        if (rails.Count <= 1)
        {
            return;
        }
        
        var targetIndex = fromIndex <= 0 ? rails.Count - 1 : fromIndex - 1;
        MoveItemToTargetWithSettle(rails, fromIndex, targetIndex, isLinked: false);
    }
    
    private void MoveChevronDown<T>(ObservableCollection<T> rails, int fromIndex)
    {
        if (rails.Count <= 1)
        {
            return;
        }
        
        var targetIndex = fromIndex >= rails.Count -1 ? 0: fromIndex + 1;
        MoveItemToTargetWithSettle(rails, fromIndex, targetIndex, isLinked: false);
    }

    private void MoveItemToTargetWithSettle<T>(ObservableCollection<T> rails, int fromIndex, int targetIndex,
        bool isLinked)
    {
        if (fromIndex < 0 || fromIndex >= rails.Count || fromIndex == targetIndex)
        {
            return;
        }
        
        PrepareSettle(targetIndex, fromIndex, isLinked);
        MoveItemToTarget(rails, fromIndex, targetIndex);
        ScheduleClearSettle();
    }
    
    private void MoveItemWithSettle<T>(ObservableCollection<T> rails, int fromIndex, int insertIndex,
        bool isLinked)
    {
        var finalTo = TryComputeFinalIndex(rails, fromIndex, insertIndex);
        if (finalTo == null)
        {
            return;
        }
        
        PrepareSettle(finalTo.Value, fromIndex, isLinked);
        MoveItemToTarget(rails, fromIndex, insertIndex);
        ScheduleClearSettle();
    }

    private static void MoveItemToTarget<T>(ObservableCollection<T> rails, int fromIndex, int targetIndex)
    {
        if (fromIndex < 0 || fromIndex >= rails.Count)
        {
            return;
        }
        
        targetIndex = Math.Clamp(targetIndex, 0, rails.Count - 1);
        if (targetIndex == fromIndex)
        {
            return;
        }
        
        var rail  = rails[fromIndex];
        rails.RemoveAt(fromIndex);
        rails.Insert(targetIndex, rail);
    }

    private void PrepareSettle(int toIndex, int fromIndex, bool isLinked)
    {
        if (_suppressSettle)
        {
            return;
        }
        
        var direction = Math.Sign(toIndex - fromIndex);
        if (direction == 0)
        {
            return;
        }
        
        _lastSettle = new RailSettleHint(toIndex, fromIndex, isLinked ? RailSettleType.Linked : RailSettleType.Independent);
        if (isLinked)
        {
            LinkAnimationVersion++;
        }
    }

    private void ScheduleClearSettle()
    {
        _settleClearTimer?.Stop();
        _settleClearTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(540)};
        _settleClearTimer?.Tick += (_, _) =>
        {
            _settleClearTimer?.Stop();
            _lastSettle = null;
        };
        
        _settleClearTimer?.Start();
    }

    private int FindDraggedItemFinalIndex(RailReorderDragData dragData)
    {
        if (_dragEpisodeSnapshot == null)
        {
            return dragData.SourceIndex;
        }

        if (dragData.IsMoveLinked)
        {
            var rowCount = LinkedRowCount(_dragEpisodeSnapshot.Count, _dragEpisodeSnapshot.Count);
            if (!IsRailInRange(dragData.SourceIndex, rowCount))
            {
                return dragData.SourceIndex;
            }

            if (IsRailInRange(dragData.SourceIndex, _dragEpisodeSnapshot.Count))
            {
                var episodeIndex = Episodes.IndexOf(_dragEpisodeSnapshot[dragData.SourceIndex]);
                if (episodeIndex >= 0)
                {
                    return episodeIndex;
                }
            }
            
            if (_dragFileSnapshot is not null && IsRailInRange(dragData.SourceIndex, _dragFileSnapshot.Count))
            {
                var fileIndex = MkvFiles.IndexOf(_dragFileSnapshot[dragData.SourceIndex]);
                if (fileIndex >= 0)
                {
                    return fileIndex;
                }
            }

            return dragData.SourceIndex;
        }

        return dragData.ReorderSide switch
        {
            RailReorderSide.Episode when IsRailInRange(dragData.SourceIndex, _dragEpisodeSnapshot.Count)
                => ResolveEpisodeFinalIndex(_dragEpisodeSnapshot[dragData.SourceIndex], dragData.SourceIndex),
            RailReorderSide.File when _dragFileSnapshot is not null &&
                                      IsRailInRange(dragData.SourceIndex, _dragFileSnapshot.Count) =>
                ResolveFileFinalIndex(_dragFileSnapshot[dragData.SourceIndex], dragData.SourceIndex),
            _ => dragData.SourceIndex
        };
    }

    private static bool IsRailInRange(int index, int count) => index >= 0 && index < count;
    private static int LinkedRowCount(int episodeCount, int mkvFileCount) => Math.Max(episodeCount, mkvFileCount);

    private static bool IsValidDragSourceIndex(RailReorderDragData dragData, int episodeCount, int mkvFileCount)
    {
        if (dragData.IsMoveLinked)
        {
            return IsRailInRange(dragData.SourceIndex,LinkedRowCount(episodeCount, mkvFileCount));
        }

        return dragData.ReorderSide switch
        {
            RailReorderSide.Episode => IsRailInRange(dragData.SourceIndex, episodeCount),
            RailReorderSide.File => IsRailInRange(dragData.SourceIndex, mkvFileCount),
            _ => false
        };
    }
    
    private int ResolveEpisodeFinalIndex(Episode episode, int fallbackIndex)
    {
        var index = Episodes.IndexOf(episode);
        return index >= 0  ? index : fallbackIndex;
    }
    
    private int ResolveFileFinalIndex(MkvFile mkvFile, int fallbackIndex)
    {
        var index = MkvFiles.IndexOf(mkvFile);
        return index >= 0  ? index : fallbackIndex;
    }

    private static int? TryComputeLinkedFinalIndex<TEpisode, TFile>(int fromIndex, int insertIndex,
        IList<TEpisode> episodes, IList<TFile> files)
    {
        if (IsRailInRange(fromIndex, episodes.Count))
        {
            return TryComputeFinalIndex(episodes, fromIndex, insertIndex);
        }

        if (IsRailInRange(fromIndex, files.Count))
        {
            return TryComputeFinalIndex(files,fromIndex,insertIndex);
        }
        
        return null;
    }

    private void RestoreListFromSnapshot()
    {
        if (_dragEpisodeSnapshot == null || _dragFileSnapshot == null)
        {
            return;
        }

        _deferMatchRefresh = true;
        Episodes.Clear();
        foreach (var episode in _dragEpisodeSnapshot)
        {
            Episodes.Add(episode);
        }
        
        MkvFiles.Clear();
        foreach (var file in _dragFileSnapshot)
        {
            MkvFiles.Add(file);
        }
        
        _deferMatchRefresh = false;
    }

    private void ClearDragSnapshot()
    {
        _dragEpisodeSnapshot = null;
        _dragFileSnapshot = null;
        _dragPreviewInsertIndex = -1;
    }

    private void OnListChanged()
    {
        if (_deferMatchRefresh)
        {
            return;
        }

        RefreshMatchState();
    }

    private static int? TryComputeFinalIndex<T>(IList<T> list, int fromIndex, int insertIndex)
    {
        if (fromIndex < 0 || fromIndex >= list.Count)
        {
            return null;
        }
        
        insertIndex = Math.Clamp(insertIndex, 0, list.Count);
        if (fromIndex == insertIndex || (fromIndex + 1 == insertIndex && insertIndex < list.Count))
        {
            return null;
        }
        
        var finalIndex = insertIndex;
        if (insertIndex > fromIndex)
        {
            finalIndex--;
        }

        if (finalIndex == fromIndex)
        {
            return null;
        }
        
        return Math.Clamp(finalIndex, 0, list.Count - 1);
    }

    private static void MoveItem<T>(ObservableCollection<T> list, int fromIndex,int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= list.Count)
        {
            return;
        }
        
        toIndex = Math.Clamp(toIndex, 0, list.Count);
        if (fromIndex == toIndex || (fromIndex + 1 == toIndex && toIndex == list.Count))
        {
            return;
        }
        
        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        if (toIndex > fromIndex)
        {
            toIndex--;
        }
        toIndex = Math.Clamp(toIndex, 0, list.Count);
        list.Insert(toIndex, item);
    }

    private async Task ExecuteOpenFilesCommand()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            return;
        }
        var result = await _mkvFinderService.OpenMkvFilesAsync(topLevel);
        ApplyImportResult(result, merge: HasImportedFiles);
    }

    private async Task ExecuteOpenFolderCommand()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            return;
        }
        var result = await _mkvFinderService.OpenMkvFoldersAsync(topLevel);
        ApplyImportResult(result, merge:true);
    }

    private async Task ImportFromPathsAsync(IReadOnlyList<string> paths)
    {
        var result = await _mkvFinderService.ImportFromPathsAsync(paths);
        ApplyImportResult(result, merge: true);
    }

    private void ApplyImportResult(FileImportResult result, bool merge = false)
    {
        HasImportedAttempted = HasImportedAttempted || result.IsEmpty;
        ShowPartialFailureDetails = false;

        if (!merge)
        {
            MkvFiles.Clear();
            ImportIssues.Clear();
        }

        var existingPaths = merge
            ? new HashSet<string>(MkvFiles.Select(f => f.FullPath), StringComparer.OrdinalIgnoreCase)
            : null;
        foreach (var mkvFile in result.ImportedFiles)
        {
            if (merge && existingPaths != null && existingPaths.Contains(mkvFile.FullPath))
            {
                continue;
            }
            
            MkvFiles.Add(mkvFile);
            existingPaths?.Add(mkvFile.FullPath);
        }

        foreach (var fileImportIssue in result.FileImportIssues)
        {
            ImportIssues.Add(new FileImportIssueViewModel(fileImportIssue));
        }

        RaiseImportStateProperties();
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime app ||
            app.MainWindow == null)
        {
            return null;
        }
        
        return TopLevel.GetTopLevel(app.MainWindow);
    }

    private void ClearImport()
    {
        HasImportedAttempted = false;
        MkvFiles.Clear();
        ImportIssues.Clear();
        RaiseImportStateProperties();
    }

    private async Task ExecutePrimaryErrorCommand()
    {
        if (HasPermissionDenied)
        {
            if (!OperatingSystem.IsLinux())
            {
                TryOpenPermissionsSetting();
                return;
            }
        }

        await ExecuteOpenFilesCommand();
    }

    private async Task ExecuteSecondaryErrorCommand()
    {
        if (HasPermissionDenied && OperatingSystem.IsLinux())
        {
            await ExecuteOpenFolderCommand();
        }
        else
        {
            await ExecuteOpenFilesCommand();
        }
    }

    private static Os ResolveCurrentOs()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Os.MacOs;
        }
        else if (OperatingSystem.IsLinux())
        {
            return Os.Linux;
        }
        else if (OperatingSystem.IsWindows())
        {
            return Os.Windows;
        }
        else
        {
            return Os.Unkown;
        }
    }

    private static string GetPermissionDeniedDescription()
    {
        return ResolveCurrentOs() switch
        {
            Os.Windows =>
                "Windows blocked access to the folder you picked.  Check the folder permissions, Controlled folder access, or copy the files to a location your user account can read.",
            Os.MacOs => $"macOS bocked access to the folder you picked.  Grant full disk Access to {Constants.AppName}, or move the files somewhere readable.",
            Os.Linux => "Linux blocked access to the folder you picked.  Check the file ownership and permissions, or if you have installed via sandbox container grant access to that folder and try again.",
            _ or Os.Unkown=> $"{Constants.AppName} could not read the folder you picked.  Move the file somewhere readable or fox permissions on your user account."
        };
    }

    private static string GetPermissionDeniedPrimaryActionTest()
    {
        return ResolveCurrentOs() switch
        {
            Os.Windows => "Open System Settings",
            Os.MacOs => "Open Privacy & Security",
            _ or Os.Linux or Os.Unkown => "Check your file permissions."
        };
    }
    

    private void TryOpenPermissionsSetting()
    {
        try
        {
            if (CurrentOs == Os.Windows)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:privacy",
                    UseShellExecute = true
                });
            }

            if (CurrentOs == Os.MacOs)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { "x-apple.systempreferences:com.apple.preferences.security?Privacy_AllFiles" }
                });
            }
        }
        catch(Exception exception)
        {
            _logger.LogError(exception, $"Failed to open permissions for {CurrentOs:G}");
        }
    }
    

    private void RefreshMatchState()
    {
        MatchRows.Clear();
        var rowCount = Math.Max(EpisodeCount, ImportedFileCount);
        for (var i = 0; i < rowCount; i++)
        {
            var episode = i < EpisodeCount ? Episodes[i] : null;
            var mkvFile = i < ImportedFileCount ? MkvFiles[i] : null;
            
            RailSettleType settleType = RailSettleType.None;
            var settleDirection = 0;
            if (_lastSettle is { Targetindex: 1 } hint)
            {
                settleType = hint.SettleType;
                settleDirection = hint.Direction;
            }
            
            MatchRows.Add(new RailMatchRowViewModel(i + 1, episode, mkvFile, settleType, settleDirection));
        }

        RaiseImportStateProperties();
    }

    public void Reset()
    {
        _settleClearTimer?.Stop();
        _settleClearTimer = null;
        _lastSettle = null;
        _suppressSettle = false;
        ClearDragSnapshot();
        IsRailDragging = false;
        
        Episodes.Clear();
        ShowName = string.Empty;
        ShowPartialFailureDetails = false;
        LinkRailReorder = false;
        ClearImport();
        RefreshMatchState();
    }

    private void RaiseImportStateProperties()
    {
        this.RaisePropertyChanged(nameof(EpisodeCount));
        this.RaisePropertyChanged(nameof(ImportedFileCount));
        this.RaisePropertyChanged(nameof(IssueCount));
        this.RaisePropertyChanged(nameof(PairedCount));
        this.RaisePropertyChanged(nameof(SkippedCount));
        this.RaisePropertyChanged(nameof(HasImportedAttempted));
        this.RaisePropertyChanged(nameof(HasImportedFiles));
        this.RaisePropertyChanged(nameof(HasIssues));
        this.RaisePropertyChanged(nameof(HasPermissionDenied));
        this.RaisePropertyChanged(nameof(HasInvalidMkvIssues));
        this.RaisePropertyChanged(nameof(HasNoMkvIssues));
        this.RaisePropertyChanged(nameof(ShowEmptyImportPrompt));
        this.RaisePropertyChanged(nameof(ShowMatchRails));
        this.RaisePropertyChanged(nameof(ShowImportPanel));
        this.RaisePropertyChanged(nameof(CanContinue));
        this.RaisePropertyChanged(nameof(PairedChipText));
        this.RaisePropertyChanged(nameof(SkippedChipText));
        this.RaisePropertyChanged(nameof(EpisodeColumnSubtitle));
        this.RaisePropertyChanged(nameof(FileColumnSubtitle));
        this.RaisePropertyChanged(nameof(EpisodeHeaderText));
        this.RaisePropertyChanged(nameof(FileHeaderText));
        this.RaisePropertyChanged(nameof(ImportButtonText));
        this.RaisePropertyChanged(nameof(ImportStatusText));
        this.RaisePropertyChanged(nameof(FooterStatusText));
        this.RaisePropertyChanged(nameof(ErrorEyebrowText));
        this.RaisePropertyChanged(nameof(ErrorTitle));
        this.RaisePropertyChanged(nameof(ErrorDescription));
        this.RaisePropertyChanged(nameof(PrimaryErrorActionText));
        this.RaisePropertyChanged(nameof(ShowPrimaryErrorButton));
        this.RaisePropertyChanged(nameof(SecondaryErrorActionText));
        this.RaisePropertyChanged(nameof(PartialFailureTitle));
        this.RaisePropertyChanged(nameof(PartialFailureSummary));
    }
    








}

