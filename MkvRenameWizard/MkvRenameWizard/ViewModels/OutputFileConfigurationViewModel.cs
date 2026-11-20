using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Helpers;
using MkvRenameWizard.Models.Renaming;
using Path = System.IO.Path;

namespace MkvRenameWizard.ViewModels;

public class OutputFileConfigurationViewModel : ViewModelBase
{
    private IReadOnlyList<PatternSegment> _parsedSegments = Array.Empty<PatternSegment>();
    private string _sanitizedShowName = string.Empty;
    private bool _suppressRebuild;

    public List<RenameEntity> RenameEntities
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            var firstFullPath = value.Count > 0 ? value[0].MkvFile.FullPath : null;
            var sourceDirectory = firstFullPath is not null ? Path.GetDirectoryName(firstFullPath) : null;

            TargetFolder = string.IsNullOrEmpty(sourceDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : sourceDirectory;

            if (!_suppressRebuild)
            {
                RebuildPreview();
            }
        }
    } = new();

    public string CurrentShowName
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }
            SetProperty(ref field, value);
            _sanitizedShowName = FilePatternHelper.SanitizeToken(value.AsSpan());
            if (!_suppressRebuild)
            {
                RebuildPreview();
            }
        }
    } = string.Empty;

    private const string DefaultFileNamePattern = "{S##E##} {Title}";

    public string FileNamePattern
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RevalidatePattern();
                if (!_suppressRebuild)
                {
                    RebuildPreview();
                }
            }
        }
    } = DefaultFileNamePattern;

    public bool IsPatternValid
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ExecuteRenameCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public string? TargetFolder { get; set => SetProperty(ref field, value); }

    public ObservableCollection<PatternError> PatternErrors { get; } = new();
    public ObservableCollection<PatternSegmentViewModel> PatternSegments { get; } = new();
    public ObservableCollection<AvailableTokenViewModel> ActiveTokens { get; } = new();

    public bool IsTokenTableExpanded { get; set => SetProperty(ref field, value); }

    public string Prefix
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && !_suppressRebuild)
            {
                RebuildPreview();
            }
        }
    } = string.Empty;

    public CaseStyle SelectedCaseStyle
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsCaseDefault));
                OnPropertyChanged(nameof(IsCaseSnake));
                OnPropertyChanged(nameof(IsCasePascal));
                OnPropertyChanged(nameof(IsCaseCamel));
                if (!_suppressRebuild)
                {
                    RebuildPreview();
                }
            }
        }
    } = CaseStyle.Default;

    public bool IsCaseDefault
    {
        get => SelectedCaseStyle == CaseStyle.Default;
        set { if (value) SelectedCaseStyle = CaseStyle.Default; }
    }

    public bool IsCaseSnake
    {
        get => SelectedCaseStyle == CaseStyle.SnakeCase;
        set { if (value) SelectedCaseStyle = CaseStyle.SnakeCase; }
    }

    public bool IsCasePascal
    {
        get => SelectedCaseStyle == CaseStyle.PascalCase;
        set { if (value) SelectedCaseStyle = CaseStyle.PascalCase; }
    }

    public bool IsCaseCamel
    {
        get => SelectedCaseStyle == CaseStyle.CamelCase;
        set { if (value) SelectedCaseStyle = CaseStyle.CamelCase; }
    }

    public Func<Task<string?>>? PickFolderAsync { get; set; }

    public ObservableCollection<RenamePreviewRowViewModel> PreviewItems { get; } = new();

    public int ReadyCount
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                ExecuteRenameCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public int ConflictCount { get; private set => SetProperty(ref field, value); }
    public int SkippedCount { get; private set => SetProperty(ref field, value); }

    public IRelayCommand ShowTokenTableCommand { get; }
    public IRelayCommand ResetPatternCommand { get; }
    public IRelayCommand<PatternError> ApplySuggestionCommand { get; set; }
    public IRelayCommand<string> InsertTokenCommand { get; }
    public IAsyncRelayCommand PickTargetFolderCommand { get; }
    public IRelayCommand ExecuteRenameCommand { get; }

    private readonly ILogger<OutputFileConfigurationViewModel> _logger;

    public OutputFileConfigurationViewModel(ILogger<OutputFileConfigurationViewModel> logger)
    {
        _logger = logger;

        foreach (var token in FilePatternHelper.ValidTokens)
        {
            ActiveTokens.Add(new AvailableTokenViewModel(token.Name, token.Description, token.ExampleValue, false));
        }

        ShowTokenTableCommand = new RelayCommand(() => IsTokenTableExpanded = !IsTokenTableExpanded);
        ResetPatternCommand = new RelayCommand(() => FileNamePattern = string.Empty);
        ApplySuggestionCommand = new RelayCommand<PatternError>(ApplySuggestion);
        PickTargetFolderCommand = new AsyncRelayCommand(PickTargetFolder);
        InsertTokenCommand = new RelayCommand<string>(name =>
        {
            if (!string.IsNullOrEmpty(name))
            {
                FileNamePattern += $"{{{name}}}";
            }
        });
        ExecuteRenameCommand = new RelayCommand(() => { }, () => IsPatternValid && ReadyCount > 0);

        RevalidatePattern();
    }

    public void Configure(string showName, List<RenameEntity> entities)
    {
        _suppressRebuild = true;
        try
        {
            CurrentShowName = showName;
            RenameEntities = entities;
        }
        finally
        {
            _suppressRebuild = false;
        }
        RebuildPreview();
    }

    private void RevalidatePattern()
    {
        var (segments, errors) = FilePatternHelper.ParseAndValidate(FileNamePattern);
        _parsedSegments = segments;

        if (PatternErrors.Count > 0 || errors.Count > 0)
        {
            PatternErrors.Clear();
            for (var i = 0; i < errors.Count; i++)
            {
                PatternErrors.Add(errors[i]);
            }
        }

        var targetSegmentCount = segments.Count + 1;
        var existingSegCount = PatternSegments.Count;
        var segOverlap = Math.Min(existingSegCount, segments.Count);

        for (var i = 0; i < segOverlap; i++)
        {
            var seg = segments[i];
            if (PatternSegments[i].Text != seg.Text || PatternSegments[i].SegmentType != seg.SegmentType)
            {
                PatternSegments[i] = new PatternSegmentViewModel { Text = seg.Text, SegmentType = seg.SegmentType };
            }
        }

        for (var i = segOverlap; i < segments.Count; i++)
        {
            PatternSegments.Add(new PatternSegmentViewModel { Text = segments[i].Text, SegmentType = segments[i].SegmentType });
        }

        if (PatternSegments.Count > segments.Count)
        {
            if (PatternSegments[segments.Count].Text != ".mkv" || PatternSegments[segments.Count].SegmentType != PatternSegmentType.Literal)
            {
                PatternSegments[segments.Count] = new PatternSegmentViewModel { Text = ".mkv", SegmentType = PatternSegmentType.Literal };
            }
            for (var i = PatternSegments.Count - 1; i >= targetSegmentCount; i--)
            {
                PatternSegments.RemoveAt(i);
            }
        }
        else
        {
            PatternSegments.Add(new PatternSegmentViewModel { Text = ".mkv", SegmentType = PatternSegmentType.Literal });
        }

        IsPatternValid = errors.Count == 0;
        if (!IsPatternValid)
        {
            IsTokenTableExpanded = true;
        }

        RebuildAvailableTokenStatus(segments);
    }

    private void RebuildAvailableTokenStatus(IReadOnlyList<PatternSegment> segments)
    {
        for (var i = 0; i < ActiveTokens.Count; i++)
        {
            var tokenVm = ActiveTokens[i];
            var isUsed = false;
            for (var j = 0; j < segments.Count; j++)
            {
                if (segments[j].SegmentType == PatternSegmentType.ValidToken &&
                    string.Equals(segments[j].Text, tokenVm.Name, StringComparison.Ordinal))
                {
                    isUsed = true;
                    break;
                }
            }
            tokenVm.IsUsedInPattern = isUsed;
        }
    }

    private void RebuildPreview()
    {
        var entitiesSpan = CollectionsMarshal.AsSpan(RenameEntities);
        var entityCount = entitiesSpan.Length;

        if (!IsPatternValid)
        {
            var existingCount = PreviewItems.Count;
            var overlap = Math.Min(existingCount, entityCount);

            for (var i = 0; i < overlap; i++)
            {
                var sourceFilePath = entitiesSpan[i].MkvFile.FullPath ?? string.Empty;
                var sourceFileName = Path.GetFileName(sourceFilePath);
                PreviewItems[i].Update(i + 1, sourceFilePath, sourceFileName, null, RenamePreviewStatus.PatternError);
            }

            for (var i = overlap; i < entityCount; i++)
            {
                var sourceFilePath = entitiesSpan[i].MkvFile.FullPath ?? string.Empty;
                var sourceFileName = Path.GetFileName(sourceFilePath);
                PreviewItems.Add(new RenamePreviewRowViewModel(i + 1, sourceFilePath, sourceFileName, null, RenamePreviewStatus.PatternError));
            }

            for (var i = existingCount - 1; i >= entityCount; i--)
            {
                PreviewItems.RemoveAt(i);
            }

            ReadyCount = 0;
            ConflictCount = 0;
            SkippedCount = 0;
            return;
        }

        var targets = entityCount > 0 ? new string?[entityCount] : Array.Empty<string?>();
        var statuses = entityCount > 0 ? new RenamePreviewStatus[entityCount] : Array.Empty<RenamePreviewStatus>();
        var sourceNames = entityCount > 0 ? new string[entityCount] : Array.Empty<string>();
        var targetCounts = new Dictionary<string, int>(entityCount, StringComparer.OrdinalIgnoreCase);

        var showNameSpan = _sanitizedShowName.AsSpan();
        var prefixSpan = Prefix.AsSpan();

        for (var i = 0; i < entityCount; i++)
        {
            ref readonly var entry = ref entitiesSpan[i];
            var sourceFilePath = entry.MkvFile.FullPath ?? string.Empty;
            var sourceFileName = Path.GetFileName(sourceFilePath);
            sourceNames[i] = sourceFileName;

            if (entry.Episode.EpisodeNumber == null)
            {
                statuses[i] = RenamePreviewStatus.Skipped;
                continue;
            }

            var extSpan = Path.GetExtension(sourceFilePath.AsSpan());
            var runTimeStr = entry.Episode.RunTime > 0
                ? LabelFormaterHelper.FormatRunTime(entry.Episode.RunTime)
                : string.Empty;

            var target = FilePatternHelper.ApplyTokenValues(
                _parsedSegments,
                entry.Episode,
                showNameSpan,
                prefixSpan,
                extSpan,
                runTimeStr.AsSpan(),
                SelectedCaseStyle,
                appendExtension: extSpan);

            targets[i] = target;

            if (string.Equals(sourceFileName, target, StringComparison.OrdinalIgnoreCase))
            {
                statuses[i] = RenamePreviewStatus.Done;
            }
            else
            {
                statuses[i] = RenamePreviewStatus.Ready;
                targetCounts.TryGetValue(target, out var count);
                targetCounts[target] = count + 1;
            }
        }

        var readyCount = 0;
        var conflictCount = 0;
        var skippedCount = 0;
        var existingPreviewCount = PreviewItems.Count;
        var previewOverlap = Math.Min(existingPreviewCount, entityCount);

        for (var i = 0; i < entityCount; i++)
        {
            var finalStatus = statuses[i];
            var target = targets[i];
            if (finalStatus == RenamePreviewStatus.Ready && target is not null && targetCounts[target] > 1)
            {
                finalStatus = RenamePreviewStatus.Conflict;
            }

            if (finalStatus == RenamePreviewStatus.Ready) readyCount++;
            else if (finalStatus == RenamePreviewStatus.Conflict) conflictCount++;
            else if (finalStatus == RenamePreviewStatus.Skipped) skippedCount++;

            var sourcePath = entitiesSpan[i].MkvFile.FullPath ?? string.Empty;
            if (i < previewOverlap)
            {
                PreviewItems[i].Update(i + 1, sourcePath, sourceNames[i], target, finalStatus);
            }
            else
            {
                PreviewItems.Add(new RenamePreviewRowViewModel(i + 1, sourcePath, sourceNames[i], target, finalStatus));
            }
        }

        for (var i = existingPreviewCount - 1; i >= entityCount; i--)
        {
            PreviewItems.RemoveAt(i);
        }

        ReadyCount = readyCount;
        ConflictCount = conflictCount;
        SkippedCount = skippedCount;
    }

    private async Task PickTargetFolder()
    {
        if (PickFolderAsync is null)
        {
            return;
        }

        var chosen = await PickFolderAsync();
        if (chosen is not null)
        {
            TargetFolder = chosen;
        }
    }

    private void ApplySuggestion(PatternError? error)
    {
        if (error?.Suggestion is null)
        {
            return;
        }

        FileNamePattern = FileNamePattern.Replace($"{{{error.TokenName}}}", $"{{{error.Suggestion}}}");
    }

    public void ApplyRenameResults(IEnumerable<RenameOperationResult<RenameFileOperation>> results)
    {
        var renamePaths = results
            .Where(r => r.IsSuccessful && r.RenameOperation.TargetPath != null)
            .ToDictionary(r => r.RenameOperation.SourcePath, r => r.RenameOperation.TargetPath, StringComparer.OrdinalIgnoreCase);

        foreach (var entity in RenameEntities)
        {
            if (entity.MkvFile.FullPath != null && renamePaths.TryGetValue(entity.MkvFile.FullPath, out var renamePath))
            {
                entity.MkvFile.FullPath = renamePath;
            }
        }
        RebuildPreview();
    }

    public IEnumerable<RenameFileOperation> BuildRenameOperations()
    {
        if (TargetFolder == null)
        {
            yield break;
        }

        for (var i = 0; i < PreviewItems.Count; i++)
        {
            var item = PreviewItems[i];
            if (item.RenamePreviewStatus != RenamePreviewStatus.Ready || item.TargetPath is null)
            {
                continue;
            }

            yield return new RenameFileOperation(
                item.OperationId,
                item.SourcePath,
                Path.Combine(TargetFolder, item.TargetPath));
        }
    }

    public void Reset()
    {
        _suppressRebuild = true;
        try
        {
            RenameEntities = new List<RenameEntity>();
            CurrentShowName = string.Empty;
            FileNamePattern = DefaultFileNamePattern;
            Prefix = string.Empty;
            SelectedCaseStyle = CaseStyle.Default;
            TargetFolder = string.Empty;
            PreviewItems.Clear();
            PatternErrors.Clear();
            PatternSegments.Clear();
            ReadyCount = 0;
            ConflictCount = 0;
            SkippedCount = 0;
        }
        finally
        {
            _suppressRebuild = false;
        }
        RevalidatePattern();
    }
}
