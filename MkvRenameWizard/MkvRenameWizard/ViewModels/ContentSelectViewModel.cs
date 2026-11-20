using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
    private readonly IFileLoggerService _fileLoggerService;

    private bool _deferMatchRefresh;
    private RailReorderDragData? _activeDrag;
    private int _dragCurrentIndex = -1;

    public event Action<int, int, RailSettleType>? RowSettled;

    public ContentSelectViewModel(
        IMkvFinderService mkvFinderService,
        IFileLoggerService fileLoggerService,
        ILogger<ContentSelectViewModel> logger)
    {
        _mkvFinderService = mkvFinderService;
        _logger = logger;
        _fileLoggerService = fileLoggerService;

        MoveEpisodeUpCommand = ReactiveCommand.Create<int>(i => MoveChevron(RailReorderSide.Episode, i, delta: -1));
        MoveEpisodeDownCommand = ReactiveCommand.Create<int>(i => MoveChevron(RailReorderSide.Episode, i, delta: 1));
        MoveMkvFileUpCommand = ReactiveCommand.Create<int>(i => MoveChevron(RailReorderSide.File, i, delta: -1));
        MoveMkvFileDownCommand = ReactiveCommand.Create<int>(i => MoveChevron(RailReorderSide.File, i, delta: 1));
        OpenFilesCommand = ReactiveCommand.CreateFromTask(ExecuteOpenFilesCommand);
        OpenFolderCommand = ReactiveCommand.CreateFromTask(ExecuteOpenFolderCommand);
        OpenLogFolderCommand = ReactiveCommand.Create(() => _fileLoggerService.OpenLogDirectory());
        ClearFilesCommand = ReactiveCommand.Create(ClearImport);
        OpenSystemSettingsCommand = ReactiveCommand.Create(TryOpenPermissionsSetting);
        PrimaryErrorCommand = ReactiveCommand.CreateFromTask(ExecutePrimaryErrorCommand);
        SecondaryErrorCommand = ReactiveCommand.CreateFromTask(ExecuteSecondaryErrorCommand);
        TogglePartialFailureDetailsCommand = ReactiveCommand.Create(() => { ShowPartialFailureDetails = !ShowPartialFailureDetails; });
        SetLinkedReorderCommand = ReactiveCommand.Create(() => { LinkRailReorder = true; });
        SetIndependentReorderCommand = ReactiveCommand.Create(() => { LinkRailReorder = false; });

        Episodes.CollectionChanged += (_, _) => OnListChanged();
        MkvFiles.CollectionChanged += (_, _) => OnListChanged();
    }

    public Os CurrentOs { get; } = ResolveCurrentOs();
    public ObservableCollection<Episode> Episodes { get; } = new();
    public ObservableCollection<MkvFile> MkvFiles { get; } = new();
    public ObservableCollection<FileImportIssueViewModel> ImportIssues { get; } = new();
    public ObservableCollection<RailMatchRowViewModel> MatchRows { get; } = new();

    public ReactiveCommand<int, Unit> MoveEpisodeUpCommand { get; }
    public ReactiveCommand<int, Unit> MoveEpisodeDownCommand { get; }
    public ReactiveCommand<int, Unit> MoveMkvFileUpCommand { get; }
    public ReactiveCommand<int, Unit> MoveMkvFileDownCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenLogFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSystemSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> PrimaryErrorCommand { get; }
    public ReactiveCommand<Unit, Unit> SecondaryErrorCommand { get; }
    public ReactiveCommand<Unit, Unit> TogglePartialFailureDetailsCommand { get; }
    public ReactiveCommand<Unit, Unit> SetLinkedReorderCommand { get; }
    public ReactiveCommand<Unit, Unit> SetIndependentReorderCommand { get; }

    public bool IsRailDragging { get; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public int LinkAnimationVersion { get; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool HasImportedAttempted { get; private set => this.RaiseAndSetIfChanged(ref field, value); }

    public bool ShowPartialFailureDetails
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PartialFailureDetailsToggleText));
        }
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
    public bool IsShiftLinkHintVisible => LinkRailReorder;

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
    public bool HasImportedFiles => ImportedFileCount > 0;
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
        ? EpisodeCount == 1 ? "1 episode" : $"{EpisodeCount} episodes"
        : $"{EpisodeCount} from {ShowName}";

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
        var maxCount = GetRailCount(dragData.ReorderSide, dragData.IsMoveLinked);
        if (dragData.SourceIndex < 0 || dragData.SourceIndex >= maxCount)
        {
            return;
        }

        _activeDrag = dragData;
        _dragCurrentIndex = dragData.SourceIndex;
        IsRailDragging = true;
    }

    public void PreviewDragReorder(RailReorderDragData dragData, int targetIndex)
    {
        if (_activeDrag is null || _dragCurrentIndex < 0)
        {
            return;
        }

        var maxCount = GetRailCount(_activeDrag.ReorderSide, _activeDrag.IsMoveLinked);
        if (maxCount <= 1)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, maxCount - 1);
        if (targetIndex == _dragCurrentIndex)
        {
            return;
        }

        MoveRail(_activeDrag.ReorderSide, _activeDrag.IsMoveLinked, _dragCurrentIndex, targetIndex);
        _dragCurrentIndex = targetIndex;
    }

    public void CommitRailDrag()
    {
        if (_activeDrag is { } drag && _dragCurrentIndex >= 0 && _dragCurrentIndex != drag.SourceIndex)
        {
            NotifyRowSettled(_dragCurrentIndex, Math.Sign(_dragCurrentIndex - drag.SourceIndex), drag.IsMoveLinked);
        }

        EndRailDrag();
    }

    public void CancelRailDrag()
    {
        if (_activeDrag is { } drag && _dragCurrentIndex >= 0 && _dragCurrentIndex != drag.SourceIndex)
        {
            MoveRail(drag.ReorderSide, drag.IsMoveLinked, _dragCurrentIndex, drag.SourceIndex);
        }

        EndRailDrag();
    }

    public void EndRailDrag()
    {
        _activeDrag = null;
        _dragCurrentIndex = -1;
        IsRailDragging = false;
    }

    private void MoveChevron(RailReorderSide side, int fromIndex, int delta)
    {
        var linked = LinkRailReorder;
        var count = GetRailCount(side, linked);
        if (count <= 1 || fromIndex < 0 || fromIndex >= count)
        {
            return;
        }

        var targetIndex = (fromIndex + delta + count) % count;
        if (targetIndex == fromIndex)
        {
            return;
        }

        MoveRail(side, linked, fromIndex, targetIndex);
        NotifyRowSettled(targetIndex, Math.Sign(targetIndex - fromIndex), linked);
    }

    private void MoveRail(RailReorderSide side, bool linked, int fromIndex, int toIndex)
    {
        _deferMatchRefresh = true;
        try
        {
            if (linked)
            {
                MoveCollectionItem(Episodes, fromIndex, toIndex);
                MoveCollectionItem(MkvFiles, fromIndex, toIndex);
            }
            else if (side == RailReorderSide.Episode)
            {
                MoveCollectionItem(Episodes, fromIndex, toIndex);
            }
            else
            {
                MoveCollectionItem(MkvFiles, fromIndex, toIndex);
            }
        }
        finally
        {
            _deferMatchRefresh = false;
        }

        // Only update the slice of MatchRows whose Episode or MkvFile index actually changed
        var start = Math.Max(0, Math.Min(fromIndex, toIndex));
        var end = Math.Min(MatchRows.Count - 1, Math.Max(fromIndex, toIndex));
        for (var i = start; i <= end; i++)
        {
            var episode = i < EpisodeCount ? Episodes[i] : null;
            var mkvFile = i < ImportedFileCount ? MkvFiles[i] : null;
            MatchRows[i].Update(i + 1, episode, mkvFile, RailSettleType.None, 0);
        }
    }

    private static void MoveCollectionItem<T>(ObservableCollection<T> list, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= list.Count)
        {
            return;
        }

        toIndex = Math.Clamp(toIndex, 0, list.Count - 1);
        if (fromIndex != toIndex)
        {
            list.Move(fromIndex, toIndex);
        }
    }

    private int GetRailCount(RailReorderSide side, bool linked)
    {
        if (linked)
        {
            return Math.Max(EpisodeCount, ImportedFileCount);
        }

        return side == RailReorderSide.Episode ? EpisodeCount : ImportedFileCount;
    }

    private void NotifyRowSettled(int targetIndex, int direction, bool isLinked)
    {
        if (direction == 0)
        {
            return;
        }

        if (isLinked)
        {
            LinkAnimationVersion++;
        }

        RowSettled?.Invoke(targetIndex, direction, isLinked ? RailSettleType.Linked : RailSettleType.Independent);
    }

    private void OnListChanged()
    {
        if (_deferMatchRefresh)
        {
            return;
        }

        RefreshMatchState();
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
        ApplyImportResult(result, merge: true);
    }

    public void SetEpisodes(IReadOnlyList<Episode> episodes)
    {
        _deferMatchRefresh = true;
        try
        {
            Episodes.Clear();
            for (var i = 0; i < episodes.Count; i++)
            {
                Episodes.Add(episodes[i]);
            }
        }
        finally
        {
            _deferMatchRefresh = false;
        }
        RefreshMatchState();
    }

    public void AddMkvFiles(IReadOnlyList<MkvFile> files)
    {
        _deferMatchRefresh = true;
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                MkvFiles.Add(files[i]);
            }
        }
        finally
        {
            _deferMatchRefresh = false;
        }
        RefreshMatchState();
    }

    internal async Task ImportFromPathsAsync(IReadOnlyList<string> paths)
    {
        var result = await _mkvFinderService.ImportFromPathsAsync(paths);
        ApplyImportResult(result, merge: true);
    }

    private void ApplyImportResult(FileImportResult result, bool merge = false)
    {
        HasImportedAttempted = HasImportedAttempted || result.IsEmpty;
        ShowPartialFailureDetails = false;

        _deferMatchRefresh = true;
        try
        {
            if (!merge)
            {
                MkvFiles.Clear();
                ImportIssues.Clear();
            }

            var existingPaths = merge
                ? new HashSet<string>(MkvFiles.Where(f => f.FullPath is not null).Select(f => f.FullPath!), StringComparer.OrdinalIgnoreCase)
                : null;
            foreach (var mkvFile in result.ImportedFiles)
            {
                if (merge && existingPaths != null && mkvFile.FullPath is not null && existingPaths.Contains(mkvFile.FullPath))
                {
                    continue;
                }

                MkvFiles.Add(mkvFile);
                if (mkvFile.FullPath is not null)
                {
                    existingPaths?.Add(mkvFile.FullPath);
                }
            }

            foreach (var fileImportIssue in result.FileImportIssues)
            {
                ImportIssues.Add(new FileImportIssueViewModel(fileImportIssue));
            }
        }
        finally
        {
            _deferMatchRefresh = false;
        }

        RefreshMatchState();
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
        MkvFiles.Clear();
        ClearErrors();
    }

    public void ClearErrors()
    {
        HasImportedAttempted = false;
        ImportIssues.Clear();
        RaiseImportStateProperties();
    }

    private async Task ExecutePrimaryErrorCommand()
    {
        if (HasPermissionDenied && !OperatingSystem.IsLinux())
        {
            TryOpenPermissionsSetting();
            return;
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
        if (OperatingSystem.IsMacOS()) return Os.MacOs;
        if (OperatingSystem.IsLinux()) return Os.Linux;
        if (OperatingSystem.IsWindows()) return Os.Windows;
        return Os.Unkown;
    }

    private static string GetPermissionDeniedDescription()
    {
        return ResolveCurrentOs() switch
        {
            Os.Windows =>
                "Windows blocked access to the folder you picked.  Check the folder permissions, Controlled folder access, or copy the files to a location your user account can read.",
            Os.MacOs => $"macOS bocked access to the folder you picked.  Grant full disk Access to {Constants.Metadata.AppName}, or move the files somewhere readable.",
            Os.Linux => "Linux blocked access to the folder you picked.  Check the file ownership and permissions, or if you have installed via sandbox container grant access to that folder and try again.",
            _ => $"{Constants.Metadata.AppName} could not read the folder you picked.  Move the file somewhere readable or fox permissions on your user account."
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to open permissions for {Os}", CurrentOs);
        }
    }

    private void RefreshMatchState()
    {
        var rowCount = Math.Max(EpisodeCount, ImportedFileCount);
        var existingCount = MatchRows.Count;
        var overlap = Math.Min(existingCount, rowCount);

        for (var i = 0; i < overlap; i++)
        {
            var episode = i < EpisodeCount ? Episodes[i] : null;
            var mkvFile = i < ImportedFileCount ? MkvFiles[i] : null;
            MatchRows[i].Update(i + 1, episode, mkvFile, RailSettleType.None, 0);
        }

        for (var i = overlap; i < rowCount; i++)
        {
            var episode = i < EpisodeCount ? Episodes[i] : null;
            var mkvFile = i < ImportedFileCount ? MkvFiles[i] : null;
            MatchRows.Add(new RailMatchRowViewModel(i + 1, episode, mkvFile));
        }

        for (var i = existingCount - 1; i >= rowCount; i--)
        {
            MatchRows.RemoveAt(i);
        }

        RaiseImportStateProperties();
    }

    public void Reset()
    {
        EndRailDrag();

        _deferMatchRefresh = true;
        try
        {
            Episodes.Clear();
            ShowName = string.Empty;
            ShowPartialFailureDetails = false;
            LinkRailReorder = false;
            MkvFiles.Clear();
            HasImportedAttempted = false;
            ImportIssues.Clear();
        }
        finally
        {
            _deferMatchRefresh = false;
        }
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
        this.RaisePropertyChanged(nameof(ShowPartialFailureBanner));
        this.RaisePropertyChanged(nameof(SecondaryErrorActionText));
        this.RaisePropertyChanged(nameof(PartialFailureTitle));
        this.RaisePropertyChanged(nameof(PartialFailureSummary));
    }
}
