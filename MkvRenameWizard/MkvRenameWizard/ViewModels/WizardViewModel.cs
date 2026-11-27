using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Renaming;
using MkvRenameWizard.Services;

namespace MkvRenameWizard.ViewModels;

public class WizardViewModel : ViewModelBase
{
    public int CurrentPageIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaiseWizardChromeProperties();
            }
        }
    }

    public ObservableCollection<ViewModelBase> Pages { get; }

    public bool ShowRenameResult
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                RaiseWizardChromeProperties();
            }
        }
    }

    public RenameResultViewModel RenameResultViewModel => _renameResultViewModel;

    public bool CanGoBack => !ShowRenameResult && CurrentPageIndex > 0;
    public bool CanGoForward => !ShowRenameResult && CurrentPageIndex < Pages.Count - 1;
    public bool IsLastPage => !ShowRenameResult && CurrentPageIndex == Pages.Count - 1;

    public string StepSubtitle => CurrentPageIndex switch
    {
        0 => "Step 1 of 3 - Find your show",
        1 => "Step 2 of 3 - Select your files",
        2 => "Step 3 of 3 - Configure output",
        _ => string.Empty
    };

    public string FooterStatusLine => CurrentPageIndex switch
    {
        0 => _contentSearchViewModel.SelectionSummary,
        1 => $"{_contentSelectViewModel.MkvFiles.Count} MKV files selected",
        2 when !_outputFileConfigurationViewModel.IsPatternValid =>
            $"{_outputFileConfigurationViewModel.PatternErrors.Count} pattern error(s).  the filename pattern uses an unknown token",
        2 => $"{_outputFileConfigurationViewModel.ReadyCount} files will be rename in place",
        _ => string.Empty
    };

    public string FooterDetailLine => CurrentPageIndex switch
    {
        0 => _contentSearchViewModel.HasNoSelectedShow
            ? "Search TVMaze, then chose at least one season."
            : _contentSearchViewModel.EpisodeSummary,
        1 => "Match files to the selected episodes.",
        2 => "Review and finish the rename preview.",
        _ => string.Empty
    };

    public string FooterLabel => CurrentPageIndex switch
    {
        2 when !_outputFileConfigurationViewModel.IsPatternValid => "FIX PATTERN ERRORS TO CONTINUE",
        2 => "READY TO RENAME",
        _ => string.Empty,
    };

    public string PrimaryButtonText => CurrentPageIndex switch
    {
        2 => _outputFileConfigurationViewModel is { IsPatternValid: true, ReadyCount: > 0 }
            ? $"{_outputFileConfigurationViewModel.ReadyCount} files(s)"
            : "Rename 0 files",
        _ => IsLastPage ? "Finish" : "Continue"
    };

    public IRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IRelayCommand FinishCommand { get; }
    public IAsyncRelayCommand PrimaryCommand { get; }

    private readonly ContentSearchViewModel _contentSearchViewModel;
    private readonly ContentSelectViewModel _contentSelectViewModel;
    private readonly RenameResultViewModel _renameResultViewModel;
    private readonly OutputFileConfigurationViewModel _outputFileConfigurationViewModel;
    private readonly IFileRenameOperationService _fileRenameOperationService;
    private readonly ITvMazeService _tvMazeService;
    private readonly ILogger<WizardViewModel> _logger;

    public WizardViewModel(
        ContentSearchViewModel contentSearchViewModel,
        ContentSelectViewModel contentSelectViewModel,
        OutputFileConfigurationViewModel outputFileConfigurationViewModel,
        RenameResultViewModel renameResultViewModel,
        ITvMazeService tvMazeService,
        IFileRenameOperationService fileRenameOperationService,
        ILogger<WizardViewModel> logger)
    {
        _contentSearchViewModel = contentSearchViewModel;
        _contentSelectViewModel = contentSelectViewModel;
        _outputFileConfigurationViewModel = outputFileConfigurationViewModel;
        _renameResultViewModel = renameResultViewModel;

        _fileRenameOperationService = fileRenameOperationService;
        _tvMazeService = tvMazeService;
        _logger = logger;

        Pages = new ObservableCollection<ViewModelBase>
        {
            _contentSearchViewModel,
            _contentSelectViewModel,
            _outputFileConfigurationViewModel,
        };

        PreviousCommand = new RelayCommand(Previous, () => CanGoBack);
        NextCommand = new AsyncRelayCommand(async () =>
        {
            Next();
            await UpdateContentSelectViewModel();
            UpdateFileOutputConfigurationViewModel();
        }, () => CanGoForward);
        FinishCommand = new RelayCommand(Finish, () => IsLastPage);
        PrimaryCommand = new AsyncRelayCommand(ExecutePrimaryCommand, CanUsePrimary);

        _renameResultViewModel.ResetRequested += () =>
        {
            ResetWizard();
            CurrentPageIndex = 0;
            ShowRenameResult = false;
        };
        _renameResultViewModel.RetryFailedRequested += async () => await ExecuteRenameAsync(retryFailedOnly: true);
        _renameResultViewModel.UndoRequested += async () => await UndoRenameAsync();
        _renameResultViewModel.GoBackRequested += () =>
        {
            _outputFileConfigurationViewModel.ApplyRenameResults(_renameResultViewModel.OperationResults);
            ShowRenameResult = false;
        };

        _contentSearchViewModel.PropertyChanged += OnContentSearchPropertyChanged;
        _outputFileConfigurationViewModel.PropertyChanged += OnOutputConfigPropertyChanged;
    }

    private void OnContentSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ContentSearchViewModel.SelectedSeasonCount):
            case nameof(ContentSearchViewModel.SelectedEpisodeCount):
            case nameof(ContentSearchViewModel.CanContinue):
                RaiseWizardChromeProperties();
                break;

            case nameof(ContentSearchViewModel.SelectedShow):
                RaiseWizardChromeProperties();
                _ = UpdateContentSelectSafeAsync();
                break;
        }
    }

    private async Task UpdateContentSelectSafeAsync()
    {
        try
        {
            await UpdateContentSelectViewModel();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update episodes for selected show");
        }
    }

    private void OnOutputConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OutputFileConfigurationViewModel.IsPatternValid)
            or nameof(OutputFileConfigurationViewModel.ReadyCount))
        {
            RaiseWizardChromeProperties();
        }
    }

    private bool CanUsePrimary()
    {
        if (ShowRenameResult)
        {
            return false;
        }

        return CurrentPageIndex switch
        {
            0 => _contentSearchViewModel.CanContinue,
            2 => _outputFileConfigurationViewModel.IsPatternValid && _outputFileConfigurationViewModel.ReadyCount > 0,
            _ => true
        };
    }

    private void Previous()
    {
        if (CurrentPageIndex == 2)
        {
            _contentSelectViewModel.ClearErrors();
        }
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
        }
    }

    private void Next() => CurrentPageIndex++;

    private void Finish()
    {
        ResetWizard();
        CurrentPageIndex = 0;
    }

    private void ResetWizard()
    {
        _contentSearchViewModel.Reset();
        _contentSelectViewModel.Reset();
        _outputFileConfigurationViewModel.Reset();
    }

    private async Task ExecutePrimaryCommand()
    {
        if (CurrentPageIndex == 2)
        {
            await ExecuteRenameAsync(retryFailedOnly: false);
            return;
        }
        if (IsLastPage)
        {
            Finish();
            return;
        }
        Next();
        await UpdateContentSelectViewModel();
        UpdateFileOutputConfigurationViewModel();
    }

    private async Task ExecuteRenameAsync(bool retryFailedOnly)
    {
        List<RenameFileOperation> operations;

        if (retryFailedOnly)
        {
            var failedOperations = _renameResultViewModel.OperationResults
                .Where(r => !r.IsSuccessful)
                .Select(r => r.RenameOperation.OperationId)
                .ToHashSet();

            operations = _outputFileConfigurationViewModel.BuildRenameOperations()
                .Where(j => failedOperations.Contains(j.OperationId))
                .ToList();
        }
        else
        {
            operations = _outputFileConfigurationViewModel.BuildRenameOperations().ToList();
        }

        var results = await Task.Run(() => _fileRenameOperationService.Execute(operations));

        if (retryFailedOnly)
        {
            var resultsById = results.ToDictionary(r => r.RenameOperation.OperationId);
            var merged = new List<RenameOperationResult<RenameFileOperation>>(_renameResultViewModel.OperationResults.Count);
            foreach (var result in _renameResultViewModel.OperationResults)
            {
                merged.Add(resultsById.GetValueOrDefault(result.RenameOperation.OperationId, result));
            }
            results = merged;
        }

        var seasonNumbers = _contentSelectViewModel.Episodes
            .Select(e => e.Season)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var seasonLabel = seasonNumbers.Count == 1
            ? $"Season {seasonNumbers[0]}"
            : $"Seasons {string.Join(", ", seasonNumbers)}";

        var showSeasonLabel = _contentSearchViewModel.SelectedShow is not null
            ? $"{_contentSearchViewModel.SelectedShow.Name} - {seasonLabel}"
            : string.Empty;

        if (_outputFileConfigurationViewModel.TargetFolder != null)
        {
            _renameResultViewModel.RePopulateViewModel(
                results,
                showSeasonLabel,
                _outputFileConfigurationViewModel.TargetFolder);

            ShowRenameResult = true;
        }
    }

    private async Task UndoRenameAsync()
    {
        var previousResults = _renameResultViewModel.OperationResults.ToList();
        if (previousResults.Count == 0)
        {
            return;
        }

        var undoResults = await Task.Run(() => _fileRenameOperationService.Undo(previousResults));
        _outputFileConfigurationViewModel.ApplyRenameResults(undoResults);

        _renameResultViewModel.RePopulateViewModel(
            undoResults,
            _renameResultViewModel.ShowSeasonLabel ?? string.Empty,
            _renameResultViewModel.TargetFolder ?? string.Empty,
            isUndoResult: true);
    }

    private async Task UpdateContentSelectViewModel()
    {
        if (_contentSearchViewModel.SelectedShow == null)
        {
            _contentSelectViewModel.SetEpisodes(Array.Empty<Models.TvMaze.Episode>());
            _contentSelectViewModel.ShowName = string.Empty;
            return;
        }

        _contentSelectViewModel.ShowName = _contentSearchViewModel.SelectedShow.Name;
        var checkedSeasonIds = new List<long>();
        foreach (var seasonsCheckBox in _contentSearchViewModel.SeasonsCheckBoxes)
        {
            if (seasonsCheckBox.IsChecked)
            {
                checkedSeasonIds.Add(seasonsCheckBox.Value.Id);
            }
        }

        if (checkedSeasonIds.Count == 0)
        {
            _contentSelectViewModel.SetEpisodes(Array.Empty<Models.TvMaze.Episode>());
            return;
        }

        var fetchTasks = new Task<List<Models.TvMaze.Episode>>[checkedSeasonIds.Count];
        for (var i = 0; i < checkedSeasonIds.Count; i++)
        {
            fetchTasks[i] = _tvMazeService.ListEpisodesBySeasonAsync(checkedSeasonIds[i]);
        }

        var seasonEpisodeLists = await Task.WhenAll(fetchTasks);
        var totalCount = 0;
        for (var i = 0; i < seasonEpisodeLists.Length; i++)
        {
            totalCount += seasonEpisodeLists[i].Count;
        }

        var allEpisodes = new List<Models.TvMaze.Episode>(totalCount);
        for (var i = 0; i < seasonEpisodeLists.Length; i++)
        {
            allEpisodes.AddRange(seasonEpisodeLists[i]);
        }

        _contentSelectViewModel.SetEpisodes(allEpisodes);
    }

    private void UpdateFileOutputConfigurationViewModel()
    {
        var maxCount = Math.Min(_contentSelectViewModel.ImportedFileCount, _contentSelectViewModel.EpisodeCount);

        var entries = new List<RenameEntity>(maxCount);
        for (var i = 0; i < maxCount; i++)
        {
            entries.Add(new RenameEntity(
                Episode: _contentSelectViewModel.Episodes[i],
                MkvFile: _contentSelectViewModel.MkvFiles[i]));
        }

        _outputFileConfigurationViewModel.Configure(
            _contentSearchViewModel.SelectedShow?.Name ?? string.Empty,
            entries);
    }

    private void RaiseWizardChromeProperties()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(IsLastPage));
        OnPropertyChanged(nameof(StepSubtitle));
        OnPropertyChanged(nameof(FooterStatusLine));
        OnPropertyChanged(nameof(FooterDetailLine));
        OnPropertyChanged(nameof(FooterLabel));
        OnPropertyChanged(nameof(PrimaryButtonText));

        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
        PrimaryCommand.NotifyCanExecuteChanged();
    }
}
