using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DynamicData;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Services;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class ContentSearchViewModel : ViewModelBase
{
    private readonly List<INotifyPropertyChanged> _seasonSelectionSubscriptions = [];
    private CancellationTokenSource? _posterLoadCancellationTokenSource;

    public string SearchText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<ShowSearchResultViewModel> SearchResults { get; } = [];
    
    public ShowSearchResultViewModel? SelectedShow
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasSelectedShow));
            this.RaisePropertyChanged(nameof(HasNoSelectedShow));
        }
    }

    public ObservableCollection<CheckboxOption<Season>> SeasonsCheckBoxes { get; } = [];

    public Bitmap? SelectedShowPoster
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsPosterLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasSearched
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RaiseSearchStateProperties();
        }
    }

    public bool IsSearching
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RaiseSearchStateProperties();
        }
    }

    public int SelectedSeasonCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int SelectedEpisodeCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int ResultCount => SearchResults.Count;
    public int SeasonCount => SeasonsCheckBoxes.Count;
    public bool HasSelectedShow => SelectedShow != null;
    public bool HasNoSelectedShow => SelectedShow == null;
    public bool ShowInitialPrompt => HasNoSelectedShow && (!HasSearched || IsSearching);
    public bool ShowNoSearchResults => HasNoSelectedShow && HasSearched && !IsSearching && ResultCount  == 0;
    public bool CanContinue => HasSelectedShow && SelectedSeasonCount > 0;
    public bool CanClearAllSeasons => HasSelectedShow && SeasonCount > 0;
    public string ResultCountLabel => ResultCount == 1 ? "1 RESULT" : $"{ResultCount} RESULT";

    public string SelectionSummary => SelectedShow == null
        ? "Search for a show to continue"
        : $"{SelectedSeasonCount} of {SeasonCount} selected";

    public string EpisodeSummary => SelectedEpisodeCount == 1 ? "1 episode" : $"{SelectedEpisodeCount} episodes";
    public string SelectedShowMeta => BuildSelectedShowMeta();
    
    public ReactiveCommand<Unit, Unit> UpdateCheckboxOptionsCommand { get; }
    public ReactiveCommand<Show, Unit> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllSeasonsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearAllSeasonsCommand { get; }
    

    private readonly ITvMazeService _tvMazeService;
    private readonly IImageLoadingService _imageLoadingService;
    public ContentSearchViewModel(ITvMazeService tvMazeService, IImageLoadingService imageLoadingService)
    {
        _tvMazeService = tvMazeService;
        _imageLoadingService = imageLoadingService;
        
        
        SearchResults.CollectionChanged += (_,_) =>
        {
          this.RaisePropertyChanged(nameof(ResultCountLabel));
          RaiseSearchStateProperties();
        };
        
        SeasonsCheckBoxes = new ObservableCollection<CheckboxOption<Season>>();
        
        SearchCommand = ReactiveCommand.CreateFromTask<Show>(ExecuteSearchCommand);
        
        UpdateCheckboxOptionsCommand = ReactiveCommand.CreateFromTask(UpdateCheckboxOptions);
        this.WhenAnyValue(x => x.SelectedShow)
            .Do(show => _ = LoadPosterAsync(show?.MediumImageUrl ?? string.Empty))
            .Select(_ => Unit.Default)
            .InvokeCommand(UpdateCheckboxOptionsCommand);

        SelectAllSeasonsCommand = ReactiveCommand.Create(SelectAllSeasons);
        var canClearAllSeasons = this.WhenAnyValue(x => x.SelectedSeasonCount, x => x.SelectedShow).Select(_ => CanClearAllSeasons);
        ClearAllSeasonsCommand = ReactiveCommand.Create(ClearAllSeasons, canClearAllSeasons);
    }


    public async Task ExecuteSearchCommand(Show show)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SearchResults.Clear();
            SelectedShow = null;
            HasSearched = false;
            UpdateSelectionMetrics();
            return;
        }
        
        HasSearched = true;
        IsSearching = true;
        try
        {
            var showSearchResult = (await _tvMazeService.FindShowIdByNameAsync(SearchText))
                .Select(show => new ShowSearchResultViewModel(show)).ToList();
            await Task.WhenAll(showSearchResult.Select( async vm =>
            {
                vm.Thumbnail = await LoadResultThumbnailAsync(vm.MediumImageUrl);
            }));
            SearchResults.Clear();
            SearchResults.AddRange(showSearchResult);
            SelectedShow = SearchResults.FirstOrDefault();
        }
        finally
        {
            IsSearching = false;
        }
        
    }
    

    private async Task UpdateCheckboxOptions()
    {
        foreach (var seasonSelectionSubscriptoin in _seasonSelectionSubscriptions)
        {
            seasonSelectionSubscriptoin.PropertyChanged -= OnSeasonSelectionChanged;
        }
        
        _seasonSelectionSubscriptions.Clear();
        SeasonsCheckBoxes.Clear();
        if (SelectedShow != null)
        {
            var seasons = await _tvMazeService.ListSeasonsAsync(SelectedShow.Id);
            seasons.ForEach(season =>
            {
                var option = new CheckboxOption<Season>($"Season: {season.Number}  .  {season.TotalEpisodeCount}", season);
                option.PropertyChanged += OnSeasonSelectionChanged;
                _seasonSelectionSubscriptions.Add(option);
                SeasonsCheckBoxes.Add(option);
            });
        }
    }

    private async Task LoadPosterAsync(string imageUrl)
    {
        _posterLoadCancellationTokenSource?.Cancel();
        _posterLoadCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _posterLoadCancellationTokenSource.Token;

        SelectedShowPoster = null;
        this.RaisePropertyChanged(nameof(HasSelectedShow));
        this.RaisePropertyChanged(nameof(SelectedShowMeta));
        
        if (string.IsNullOrEmpty(imageUrl))
        {
            return;
        }

        try
        {
            IsPosterLoading = true;
            SelectedShowPoster = await _imageLoadingService.LoadBitMapAsync(imageUrl, cancellationToken);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {

        }
        catch
        {
            SelectedShowPoster = null;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsPosterLoading = false;
            }
        }
    }

    private async Task<Bitmap?> LoadResultThumbnailAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }
        
        try
        {
           return await _imageLoadingService.LoadBitMapAsync(imageUrl, CancellationToken.None);
        }
        catch(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Image load failed for {imageUrl}: {ex.Message}");
            return null;
        }
    }

    private void SelectAllSeasons()
    {
        foreach (var option in SeasonsCheckBoxes)
        {
            option.IsChecked = true;
        }
        UpdateSelectionMetrics();
    }

    private void ClearAllSeasons()
    {
        foreach (var option in SeasonsCheckBoxes)
        {
            option.IsChecked = false;
        }
        UpdateSelectionMetrics();
    }

    private void OnSeasonSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CheckboxOption<Season>.IsChecked))
        {
            UpdateSelectionMetrics();
        }
    }
    
    private void UpdateSelectionMetrics()
    {
        SelectedSeasonCount = SeasonsCheckBoxes.Count(option => option.IsChecked);
        SelectedEpisodeCount = SeasonsCheckBoxes
            .Where(option => option.IsChecked)
            .Sum(option => option.Value.TotalEpisodeCount);
        
        this.RaisePropertyChanged(nameof(SelectionSummary));
        this.RaisePropertyChanged(nameof(EpisodeSummary));
        this.RaisePropertyChanged(nameof(SelectedShowMeta));
        this.RaisePropertyChanged(nameof(CanContinue));
        this.RaisePropertyChanged(nameof(CanClearAllSeasons));
        RaiseSearchStateProperties();
    }

    private void RaiseSearchStateProperties()
    {
        this.RaisePropertyChanged(nameof(ShowInitialPrompt));
        this.RaisePropertyChanged(nameof(ShowNoSearchResults));
        this.RaisePropertyChanged(nameof(HasNoSelectedShow));
        this.RaisePropertyChanged(nameof(HasSelectedShow));
        this.RaisePropertyChanged(nameof(IsSearching));
    }

    private string BuildSelectedShowMeta()
    {
        if (SelectedShow == null)
        {
            return string.Empty;
        }

        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(SelectedShow.YearRange))
        {
            pieces.Add(SelectedShow.YearRange);
        }

        if (SelectedShow.RatingAverage is { } ratingAverage)
        {
            pieces.Add($"* {ratingAverage:0.0}");
        }

        if (SeasonCount > 0)
        {
            pieces.Add(SeasonCount == 1 ? "1 season" : $"{SeasonCount} seasons");
        }
        
        return string.Join("  .  ", pieces);
    }
    
}