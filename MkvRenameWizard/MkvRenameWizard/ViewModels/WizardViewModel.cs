using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Models.TvMaze;
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

    public bool CanGoBack => CurrentPageIndex > 0;
    public bool CanGoForward => CurrentPageIndex < Pages.Count - 1;
    public bool IsLastPage => CurrentPageIndex == Pages.Count - 1;

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
        1 => $"{_contentSelectViewModel.MkvFilesList.Count} MKV files selected",
        2 => $"{_outputFileConfigurationViewModel.PreviewList.Count} Rename preview files",
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

    public string PrimaryButtonText => IsLastPage ? "Finish" : "Continue";

    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> FinishCommand { get; }
    public ReactiveCommand<Unit, Unit> PrimaryCommand { get; }

    private readonly ContentSearchViewModel _contentSearchViewModel;
    private readonly ContentSelectViewModel _contentSelectViewModel;
    private readonly OutputFileConfigurationViewModel _outputFileConfigurationViewModel;
    
    private readonly ITvMazeService _tvMazeService;

    public WizardViewModel()
    {
        _contentSearchViewModel = new ContentSearchViewModel();
        _contentSelectViewModel = new ContentSelectViewModel(null, new ObservableCollection<CheckboxOption<Season>>());
        _outputFileConfigurationViewModel = new OutputFileConfigurationViewModel(new Dictionary<string, MkvFile>());

        _tvMazeService = new TvMazeService(new TvMazeDataAccess());
        
        _contentSearchViewModel
            .WhenAnyValue(x => x.SelectedSeasonCount, x => x.SelectedEpisodeCount, x => x.SelectedShow, x => x.CanContinue)
            .Subscribe(checkboxOptions =>
            {
                RaiseWizardChromeProperties();
            });
        
        _contentSearchViewModel
            .WhenAnyValue(x => x.SelectedShow)
            .Subscribe(_ =>
            {
                UpdateContentSelectViewModel();
            });

        Pages = new ObservableCollection<ViewModelBase>
        {
            _contentSearchViewModel,
            _contentSelectViewModel,
            _outputFileConfigurationViewModel,
        };

        var canGoBack = this.WhenAnyValue(x => x.CurrentPageIndex).Select(_ => CanGoBack);
        var canGoForward = this.WhenAnyValue(x => x.CurrentPageIndex).Select(_ => CanGoForward);
        var canFinish = this.WhenAnyValue(x => x.CurrentPageIndex).Select(_ => IsLastPage);
        var canUsePrimary = this.WhenAnyValue(x => x.CurrentPageIndex)
            .CombineLatest(
            _contentSearchViewModel.WhenAnyValue(x => x.CanContinue),
            (pageIndex, canContinueSearch) => pageIndex switch
            {
                0 => canContinueSearch,
                _ => CanGoBack || IsLastPage
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

    private void Previous() => CurrentPageIndex--;
    private void Next() => CurrentPageIndex++;

    private void Finish() { /* Implement finish logic */ }

    private async Task ExecutePrimaryCommand()
    {
        if (IsLastPage)
        {
            Finish();
            return;
        }
        Next();
        await UpdateContentSelectViewModel();
        UpdateFileOutputConfigurationViewModel();
    }

    private async Task UpdateContentSelectViewModel()
    {
        _contentSelectViewModel.SelectedShow = _contentSearchViewModel.SelectedShow;
        _contentSelectViewModel.ContentList.Clear();
        if (_contentSelectViewModel.SelectedShow == null)
        {
            return;
        }
        
        foreach (var option in _contentSearchViewModel.SeasonsCheckBoxes)
        {
            if (option.IsChecked)
            {
                var episodes = await _tvMazeService.ListEpisodesBySeasonAsync(option.Value.Id);
                //TODO: change to send Episodes instead of just the name
                _contentSelectViewModel.ContentList.AddRange(episodes.Select(e => e.Name));
            }
                
        }
    }
    
    private void UpdateFileOutputConfigurationViewModel()
    {
        _outputFileConfigurationViewModel.PreviewList.Clear();
        _outputFileConfigurationViewModel.FileContentMap.Clear();
        
        var maxCount = _contentSelectViewModel.MkvFilesList.Count;
        if (_contentSelectViewModel.ContentList.Count < _contentSelectViewModel.MkvFilesList.Count)
        {
            maxCount = _contentSelectViewModel.ContentList.Count;
        }

        for (var i = 0; i < maxCount; i++)
        {
            var content = _contentSelectViewModel.ContentList[i];
            var mkvFile = _contentSelectViewModel.MkvFilesList[i];
            _outputFileConfigurationViewModel.FileContentMap.Add(content, mkvFile);
        }
    }

    private void RaiseWizardChromeProperties()
    {
        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(CanGoForward));
        this.RaisePropertyChanged(nameof(IsLastPage));
        this.RaisePropertyChanged(nameof(StepSubtitle));
        this.RaisePropertyChanged(nameof(FooterStatusLine));
        this.RaisePropertyChanged(nameof(FooterDetailLine));
        this.RaisePropertyChanged(nameof(PrimaryButtonText));
    }
}
