using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using MkvRenameWizard.Models.Renaming;
using MkvRenameWizard.Services;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class WizardViewModel : ViewModelBase
{
    public int CurrentPageIndex
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RaiseWizardChromeProperties();
        }
    }

    public ObservableCollection<ViewModelBase> Pages { get; }
    
    public bool ShowRenameResult { get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RaiseWizardChromeProperties();
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

    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> FinishCommand { get; }
    public ReactiveCommand<Unit, Unit> PrimaryCommand { get; }

    private readonly ContentSearchViewModel _contentSearchViewModel;
    private readonly ContentSelectViewModel _contentSelectViewModel;
    private readonly RenameResultViewModel _renameResultViewModel;
    private readonly OutputFileConfigurationViewModel _outputFileConfigurationViewModel;
    private readonly IFileRenameOperationService _fileRenameOperationService;
    private readonly ITvMazeService _tvMazeService;
    private readonly ILogger<WizardViewModel> _logger;

    public WizardViewModel(ContentSearchViewModel contentSearchViewModel, ContentSelectViewModel contentSelectViewModel,
        OutputFileConfigurationViewModel outputFileConfigurationViewModel, RenameResultViewModel renameResultViewModel,
        ITvMazeService tvMazeService, IFileRenameOperationService fileRenameOperationService,
        ILogger<WizardViewModel> logger)
    {
        _contentSearchViewModel = contentSearchViewModel;
        _contentSelectViewModel = contentSelectViewModel;
        _outputFileConfigurationViewModel = outputFileConfigurationViewModel;
        _renameResultViewModel = renameResultViewModel;
        
        _fileRenameOperationService = fileRenameOperationService;
        _tvMazeService = tvMazeService;
        _logger = logger;

        _renameResultViewModel.ResetRequested += () =>
        {
            ResetWizard();
            CurrentPageIndex = 0;
            ShowRenameResult = false;
        };
        _renameResultViewModel.RetryFailedRequested += async () => await ExecuteRenameAsync(retryFailedOnly: true);
        _renameResultViewModel.GoBackRequested += () =>
        {
            _outputFileConfigurationViewModel.ApplyRenameResults(_renameResultViewModel.OperationResults);
            ShowRenameResult = false;
        };
        
        _contentSearchViewModel
            .WhenAnyValue(x => x.SelectedSeasonCount,
                          x => x.SelectedEpisodeCount, 
                         x => x.SelectedShow,
                         x => x.CanContinue)
            .Subscribe(checkboxOptions =>
            {
                RaiseWizardChromeProperties();
            });
        
        _contentSearchViewModel
            .WhenAnyValue(x => x.SelectedShow)
            .Subscribe(async void (_) =>
            {
                try
                {
                    await UpdateContentSelectViewModel();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, e.Message);
                }
            });
        _outputFileConfigurationViewModel
            .WhenAnyValue(x => x.IsPatternValid, x => x.ReadyCount)
            .Subscribe(_ => RaiseWizardChromeProperties());

        Pages = new ObservableCollection<ViewModelBase>
        {
            _contentSearchViewModel,
            _contentSelectViewModel,
            _outputFileConfigurationViewModel,
        };

        var canGoBack = this.WhenAnyValue(x => x.CurrentPageIndex, x => x.ShowRenameResult).Select(_ => CanGoBack);
        var canGoForward = this.WhenAnyValue(x => x.CurrentPageIndex, x => x.ShowRenameResult).Select(_ => CanGoForward);
        var canFinish = this.WhenAnyValue(x => x.CurrentPageIndex,x => x.ShowRenameResult).Select(_ => IsLastPage);
       
        var canUsePrimary = this.WhenAnyValue(x => x.CurrentPageIndex,x => x.ShowRenameResult)
            .CombineLatest(
            _contentSearchViewModel.WhenAnyValue(x => x.CanContinue),
            _outputFileConfigurationViewModel.WhenAnyValue(x => x.IsPatternValid),
            _outputFileConfigurationViewModel.WhenAnyValue(x => x.ReadyCount),
            (pageAndResult, canSearch, patternOk, readyCount) =>
            {
                var (pageIndex, hasShowResult) = pageAndResult;
                if (hasShowResult)
                {
                    return false;
                }

                return pageIndex switch
                {
                    0 => canSearch,
                    2 => patternOk && readyCount > 0,
                    _ => true
                };
            }
        );
        
        PreviousCommand = ReactiveCommand.Create(Previous, canGoBack);
        NextCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                Next();
                await UpdateContentSelectViewModel();
                UpdateFileOutputConfigurationViewModel();
            },
            canGoForward);

        FinishCommand = ReactiveCommand.Create(Finish, canFinish);
        PrimaryCommand = ReactiveCommand.CreateFromTask(ExecutePrimaryCommand, canUsePrimary);
    }

    private void Previous()
    {
        if (CurrentPageIndex == 2)
        {
            _contentSelectViewModel.ClearErrors();
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
        IEnumerable<RenameFileOperation> operations;

        if (retryFailedOnly)
        {
            var failedOperations = _renameResultViewModel.OperationResults
                .Where(r => !r.IsSuccessful)
                .Select(r => r.RenameOperation.OperationId)
                .ToHashSet();

            operations = _outputFileConfigurationViewModel.BuildRenameOperations()
                .Where(j => failedOperations.Contains(j.OperationId));
        }
        else
        {
            operations = _outputFileConfigurationViewModel.BuildRenameOperations();
        }
        
        var results = await _fileRenameOperationService.ExecuteAsync(operations);

        if (retryFailedOnly)
        {
            var merged = _renameResultViewModel.OperationResults
                .Select(result => results.ToDictionary(r =>
                    r.RenameOperation.OperationId).GetValueOrDefault(result.RenameOperation.OperationId, result)).ToList();
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

    private async Task UpdateContentSelectViewModel()
    {
        _contentSelectViewModel.Episodes.Clear();
        if (_contentSearchViewModel.SelectedShow == null)
        {
            _contentSelectViewModel.ShowName =string.Empty;
            return;
        }
        _contentSelectViewModel.ShowName = _contentSearchViewModel.SelectedShow.Name;
        foreach (var seasonsCheckBox in _contentSearchViewModel.SeasonsCheckBoxes)
        {
            if (!seasonsCheckBox.IsChecked)
            {
                continue;
            }
            var episodes = await _tvMazeService.ListEpisodesBySeasonAsync(seasonsCheckBox.Value.Id);
            _contentSelectViewModel.Episodes.AddRange(episodes);
        }
    }
    
    private void UpdateFileOutputConfigurationViewModel()
    {
        
        var maxCount = Math.Min(_contentSelectViewModel.ImportedFileCount, _contentSelectViewModel.EpisodeCount);
        
        var entries = new List<RenameEntity>(maxCount);
        for (var i = 0; i < maxCount; i++)
        {
            entries.Add(new RenameEntity(Episode: _contentSelectViewModel.Episodes[i],
                MkvFile: _contentSelectViewModel.MkvFiles[i]));
        }
        
        _outputFileConfigurationViewModel.CurrentShowName = _contentSearchViewModel.SelectedShow?.Name ?? string.Empty;
        _outputFileConfigurationViewModel.RenameEntities = entries;
    }

    private void RaiseWizardChromeProperties()
    {
        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(CanGoForward));
        this.RaisePropertyChanged(nameof(IsLastPage));
        this.RaisePropertyChanged(nameof(StepSubtitle));
        this.RaisePropertyChanged(nameof(FooterStatusLine));
        this.RaisePropertyChanged(nameof(FooterDetailLine));
        this.RaisePropertyChanged(nameof(FooterLabel));
        this.RaisePropertyChanged(nameof(PrimaryButtonText));
    }
}
