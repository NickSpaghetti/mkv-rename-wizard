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
    private int _currentPageIndex;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set => this.RaiseAndSetIfChanged(ref _currentPageIndex, value);
    }

    public ObservableCollection<ViewModelBase> Pages { get; }

    public bool CanGoBack => CurrentPageIndex > 0;
    public bool CanGoForward => CurrentPageIndex < Pages.Count - 1;
    public bool IsLastPage => CurrentPageIndex == Pages.Count - 1;

    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> FinishCommand { get; }

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
            .WhenAnyValue(x => x.SeasonsCheckBoxs)
            .Subscribe(checkboxOptions =>
            {
                UpdateContentSelectViewModel();
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

        PreviousCommand = ReactiveCommand.Create(Previous, this.WhenAnyValue(x => x.CanGoBack));

        
        NextCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                Next();
                await UpdateContentSelectViewModel();
                UpdateFileOutputConfigurationViewModel();
            },
            this.WhenAnyValue(x => x.CanGoForward));

        FinishCommand = ReactiveCommand.Create(Finish, this.WhenAnyValue(x => x.IsLastPage));
    }

    private void Previous() => CurrentPageIndex--;
    private void Next() => CurrentPageIndex++;

    private void Finish() { /* Implement finish logic */ }

    private async Task UpdateContentSelectViewModel()
    {
        _contentSelectViewModel.SelectedShow = _contentSearchViewModel.SelectedShow;
        if (_contentSelectViewModel.SelectedShow == null)
        {
            return;
        }

        //_contentSelectViewModel.ContentList.Clear();
        //_contentSelectViewModel.MkvFilesList.Clear();
        foreach (var option in _contentSearchViewModel.SeasonsCheckBoxs)
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
}
