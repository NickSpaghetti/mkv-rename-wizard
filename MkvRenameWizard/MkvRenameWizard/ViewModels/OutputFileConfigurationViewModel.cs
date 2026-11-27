using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Core.Services.Aggregations;
using MkvRenameWizard.Models.Renaming;
using CoreCaseStyle = MkvRenameWizard.Core.Models.Renaming.CaseStyle;
using CoreRenameCandidate = MkvRenameWizard.Core.Models.Renaming.RenameCandidate;
using CoreRenamePlanRequest = MkvRenameWizard.Core.Models.Renaming.RenamePlanRequest;
using CoreTokenNames = MkvRenameWizard.Core.Models.Renaming.TokenNames;
using Path = System.IO.Path;

namespace MkvRenameWizard.ViewModels;

public class OutputFileConfigurationViewModel : ViewModelBase
{
    private readonly IMkvRenameWizardService _coreService;
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
            if (!_suppressRebuild)
            {
                RebuildPreview();
            }
        }
    } = string.Empty;

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
    } = CoreTokenNames.DefaultPattern;

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

    public OutputFileConfigurationViewModel(
        IMkvRenameWizardService coreService,
        ILogger<OutputFileConfigurationViewModel> logger)
    {
        _coreService = coreService;
        _logger = logger;

        foreach (var token in _coreService.RetrieveAvailableTokens())
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
        var validation = _coreService.ValidatePattern(FileNamePattern ?? string.Empty);
        var segments = new PatternSegment[validation.Segments.Count];
        for (var i = 0; i < validation.Segments.Count; i++)
        {
            var seg = validation.Segments[i];
            segments[i] = new PatternSegment(seg.Text, (PatternSegmentType)(int)seg.SegmentType);
        }

        if (PatternErrors.Count > 0 || validation.Errors.Count > 0)
        {
            PatternErrors.Clear();
            for (var i = 0; i < validation.Errors.Count; i++)
            {
                var err = validation.Errors[i];
                PatternErrors.Add(new PatternError(err.Column, err.TokenName, err.Suggestion));
            }
        }

        var targetSegmentCount = segments.Length + 1;
        var existingSegCount = PatternSegments.Count;
        var segOverlap = Math.Min(existingSegCount, segments.Length);

        for (var i = 0; i < segOverlap; i++)
        {
            var seg = segments[i];
            if (PatternSegments[i].Text != seg.Text || PatternSegments[i].SegmentType != seg.SegmentType)
            {
                PatternSegments[i] = new PatternSegmentViewModel { Text = seg.Text, SegmentType = seg.SegmentType };
            }
        }

        for (var i = segOverlap; i < segments.Length; i++)
        {
            PatternSegments.Add(new PatternSegmentViewModel { Text = segments[i].Text, SegmentType = segments[i].SegmentType });
        }

        if (PatternSegments.Count > segments.Length)
        {
            if (PatternSegments[segments.Length].Text != ".mkv" || PatternSegments[segments.Length].SegmentType != PatternSegmentType.Literal)
            {
                PatternSegments[segments.Length] = new PatternSegmentViewModel { Text = ".mkv", SegmentType = PatternSegmentType.Literal };
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

        IsPatternValid = validation.IsValid;
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

        var candidates = new CoreRenameCandidate[entityCount];
        for (var i = 0; i < entityCount; i++)
        {
            ref readonly var entry = ref entitiesSpan[i];
            candidates[i] = new CoreRenameCandidate(
                SourcePath: entry.MkvFile.FullPath ?? string.Empty,
                Season: entry.Episode.Season,
                EpisodeNumber: entry.Episode.EpisodeNumber,
                EpisodeName: entry.Episode.Name,
                RunTime: entry.Episode.RunTime,
                AirDate: entry.Episode.AirDate);
        }

        var plan = _coreService.BuildRenamePlan(new CoreRenamePlanRequest(
            ShowName: CurrentShowName ?? string.Empty,
            Pattern: FileNamePattern ?? string.Empty,
            Prefix: Prefix ?? string.Empty,
            CaseStyle: (CoreCaseStyle)(int)SelectedCaseStyle,
            TargetDirectory: null,
            Candidates: candidates));

        var existingPreviewCount = PreviewItems.Count;
        var previewOverlap = Math.Min(existingPreviewCount, plan.Items.Count);

        for (var i = 0; i < plan.Items.Count; i++)
        {
            var item = plan.Items[i];
            var status = (RenamePreviewStatus)(int)item.Status;
            if (i < previewOverlap)
            {
                PreviewItems[i].Update(item.OperationId, item.SourcePath, item.SourceFileName, item.TargetFileName, status);
            }
            else
            {
                PreviewItems.Add(new RenamePreviewRowViewModel(item.OperationId, item.SourcePath, item.SourceFileName, item.TargetFileName, status));
            }
        }

        for (var i = existingPreviewCount - 1; i >= plan.Items.Count; i--)
        {
            PreviewItems.RemoveAt(i);
        }

        ReadyCount = plan.ReadyCount;
        ConflictCount = plan.ConflictCount;
        SkippedCount = plan.SkippedCount;
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
            FileNamePattern = CoreTokenNames.DefaultPattern;
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
