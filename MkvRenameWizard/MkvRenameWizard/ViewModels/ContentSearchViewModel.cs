using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Services;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class ContentSearchViewModel : ViewModelBase
{
    private string _searchText;
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
    
    private ObservableCollection<Show> _searchResults;
    public ObservableCollection<Show> SearchResults
    {
        get => _searchResults;
        set => this.RaiseAndSetIfChanged(ref _searchResults, value);
    }
    
    private Show _selectedShow;
    public Show SelectedShow
    {
        get => _selectedShow;
        set => this.RaiseAndSetIfChanged(ref _selectedShow, value);
    }

    private ObservableCollection<CheckboxOption<Season>> _seasonsCheckBoxs;
    public ObservableCollection<CheckboxOption<Season>> SeasonsCheckBoxs
    {
        get => _seasonsCheckBoxs;
        set => this.RaiseAndSetIfChanged(ref _seasonsCheckBoxs, value);
    }
    public ReactiveCommand<Unit, Unit> UpdateCheckboxOptionsCommand { get; }

    public ReactiveCommand<Show, Unit> SearchCommand { get; }

    private readonly ITvMazeService _tvMazeService;
    public ContentSearchViewModel()
    {
        _tvMazeService = new TvMazeService(new TvMazeDataAccess());
        
        SearchResults = new ObservableCollection<Show>();
        SeasonsCheckBoxs = new ObservableCollection<CheckboxOption<Season>>();
        
        SearchCommand = ReactiveCommand.CreateFromTask<Show>(ExecuteSearchCommand);
        
        UpdateCheckboxOptionsCommand = ReactiveCommand.CreateFromTask(UpdateCheckboxOptions);
        this.WhenAnyValue(x => x.SelectedShow)
            .Select(_ => Unit.Default)
            .InvokeCommand(UpdateCheckboxOptionsCommand);
    }


    public async Task ExecuteSearchCommand(Show show)
    {
        var showSearchResults = await _tvMazeService.FindShowIdByNameAsync(_searchText);
        SearchResults.Clear();
        SearchResults.AddRange(showSearchResults);
    }

    private async Task UpdateCheckboxOptions()
    {
        SeasonsCheckBoxs.Clear();
        if (SelectedShow != null)
        {
            var seasons = await _tvMazeService.ListSeasonsAsync(SelectedShow.Id);
            seasons.ForEach(season => SeasonsCheckBoxs.Add(new CheckboxOption<Season>($"Season: {season.Number}", season)));
        }
    }
    
}