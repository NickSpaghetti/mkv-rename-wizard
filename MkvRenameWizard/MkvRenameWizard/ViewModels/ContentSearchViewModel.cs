using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Services;

namespace MkvRenameWizard.ViewModels;

public class ContentSearchViewModel : ViewModelBase
{
    private readonly List<INotifyPropertyChanged> _seasonSelectionSubscriptions = [];
    private CancellationTokenSource? _posterLoadCancellationTokenSource;
    private CancellationTokenSource? _thumbnailLoadCancellationTokenSource;
    private CancellationTokenSource? _seasonsLoadCancellationTokenSource;
    private bool _ownsSelectedShowPoster;
    private bool _suppressSelectionMetrics;
    private bool _isLoadingThumbnails;

    public string SearchText
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public ObservableCollection<ShowSearchResultViewModel> SearchResults { get; } = [];

    public ShowSearchResultViewModel? SelectedShow
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasSelectedShow));
                OnPropertyChanged(nameof(HasNoSelectedShow));
                ClearAllSeasonsCommand.NotifyCanExecuteChanged();
                _ = LoadPosterAsync(value);
                _ = UpdateCheckboxOptions();
            }
        }
    }

    public ObservableCollection<CheckboxOption<Season>> SeasonsCheckBoxes { get; } = [];

    public Bitmap? SelectedShowPoster
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsPosterLoading
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool HasSearched
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaiseSearchStateProperties();
            }
        }
    }

    public bool IsSearching
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaiseSearchStateProperties();
            }
        }
    }

    public int SelectedSeasonCount
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ClearAllSeasonsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int SelectedEpisodeCount
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int ResultCount => SearchResults.Count;
    public int SeasonCount => SeasonsCheckBoxes.Count;
    public bool HasSelectedShow => SelectedShow != null;
    public bool HasNoSelectedShow => SelectedShow == null;
    public bool ShowInitialPrompt => HasNoSelectedShow && (!HasSearched || IsSearching);
    public bool ShowNoSearchResults => HasNoSelectedShow && HasSearched && !IsSearching && ResultCount == 0;
    public bool CanContinue => HasSelectedShow && SelectedSeasonCount > 0;
    public bool CanClearAllSeasons => HasSelectedShow && SeasonCount > 0;
    public string ResultCountLabel => ResultCount == 1 ? "1 RESULT" : $"{ResultCount} RESULT";

    public string SelectionSummary => SelectedShow == null
        ? "Search for a show to continue"
        : $"{SelectedSeasonCount} of {SeasonCount} selected";

    public string EpisodeSummary => SelectedEpisodeCount == 1 ? "1 episode" : $"{SelectedEpisodeCount} episodes";
    public string SelectedShowMeta => BuildSelectedShowMeta();

    public IAsyncRelayCommand UpdateCheckboxOptionsCommand { get; }
    public IAsyncRelayCommand<Show?> SearchCommand { get; }
    public IRelayCommand SelectAllSeasonsCommand { get; }
    public IRelayCommand ClearAllSeasonsCommand { get; }

    private readonly ITvMazeService _tvMazeService;
    private readonly IImageLoadingService _imageLoadingService;

    public ContentSearchViewModel(ITvMazeService tvMazeService, IImageLoadingService imageLoadingService)
    {
        _tvMazeService = tvMazeService;
        _imageLoadingService = imageLoadingService;

        SearchCommand = new AsyncRelayCommand<Show?>(ExecuteSearchCommand);
        UpdateCheckboxOptionsCommand = new AsyncRelayCommand(UpdateCheckboxOptions);
        SelectAllSeasonsCommand = new RelayCommand(SelectAllSeasons);
        ClearAllSeasonsCommand = new RelayCommand(ClearAllSeasons, CanExecuteClearAllSeasons);

        SearchResults.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ResultCount));
            OnPropertyChanged(nameof(ResultCountLabel));
            RaiseSearchStateProperties();
        };

        SeasonsCheckBoxes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SeasonCount));
            OnPropertyChanged(nameof(CanClearAllSeasons));
            ClearAllSeasonsCommand.NotifyCanExecuteChanged();
        };
    }

    private bool CanExecuteClearAllSeasons() => SelectedShow != null && SeasonsCheckBoxes.Any(x => x.IsChecked);

    public async Task ExecuteSearchCommand(Show? show)
    {
        CancelAndDispose(ref _thumbnailLoadCancellationTokenSource);

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            DisposeAndClearSearchResults();
            SelectedShow = null;
            HasSearched = false;
            UpdateSelectionMetrics();
            return;
        }

        HasSearched = true;
        IsSearching = true;
        try
        {
            var results = await _tvMazeService.FindShowIdByNameAsync(SearchText);
            var showSearchResult = new List<ShowSearchResultViewModel>(results.Count);
            foreach (var r in results)
            {
                showSearchResult.Add(new ShowSearchResultViewModel(r));
            }

            SetSelectedShowPoster(null, ownsBitmap: false);
            DisposeAndClearSearchResults();
            for (var i = 0; i < showSearchResult.Count; i++)
            {
                SearchResults.Add(showSearchResult[i]);
            }

            _thumbnailLoadCancellationTokenSource = new CancellationTokenSource();
            var thumbToken = _thumbnailLoadCancellationTokenSource.Token;
            _isLoadingThumbnails = true;
            _ = LoadResultThumbnailsAsync(showSearchResult, thumbToken);

            SelectedShow = SearchResults.FirstOrDefault();
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task LoadResultThumbnailsAsync(List<ShowSearchResultViewModel> items, CancellationToken cancellationToken)
    {
        var tasks = new Task[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            tasks[i] = LoadSingleThumbnailAsync(items[i], cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _isLoadingThumbnails = false;
            }
        }
    }

    private async Task LoadSingleThumbnailAsync(ShowSearchResultViewModel vm, CancellationToken cancellationToken)
    {
        var bmp = await LoadResultThumbnailAsync(vm.MediumImageUrl, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            bmp?.Dispose();
            return;
        }

        vm.Thumbnail = bmp;

        if (ReferenceEquals(SelectedShow, vm) && SelectedShowPoster is null && bmp is not null)
        {
            SetSelectedShowPoster(bmp, ownsBitmap: false);
            IsPosterLoading = false;
        }
    }

    private async Task UpdateCheckboxOptions()
    {
        CancelAndDispose(ref _seasonsLoadCancellationTokenSource);
        _seasonsLoadCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _seasonsLoadCancellationTokenSource.Token;

        foreach (var seasonSelectionSubscription in _seasonSelectionSubscriptions)
        {
            seasonSelectionSubscription.PropertyChanged -= OnSeasonSelectionChanged;
        }

        _seasonSelectionSubscriptions.Clear();
        SeasonsCheckBoxes.Clear();

        var selectedShow = SelectedShow;
        if (selectedShow != null)
        {
            var seasons = await _tvMazeService.ListSeasonsAsync(selectedShow.Id, cancellationToken);
            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(SelectedShow, selectedShow))
            {
                return;
            }

            foreach (var season in seasons)
            {
                var option = new CheckboxOption<Season>($"Season: {season.Number}  .  {season.TotalEpisodeCount}", season);
                option.PropertyChanged += OnSeasonSelectionChanged;
                _seasonSelectionSubscriptions.Add(option);
                SeasonsCheckBoxes.Add(option);
            }
            UpdateSelectionMetrics();
        }
    }

    private async Task LoadPosterAsync(ShowSearchResultViewModel? show)
    {
        CancelAndDispose(ref _posterLoadCancellationTokenSource);
        _posterLoadCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _posterLoadCancellationTokenSource.Token;

        SetSelectedShowPoster(null, ownsBitmap: false);
        OnPropertyChanged(nameof(HasSelectedShow));
        OnPropertyChanged(nameof(SelectedShowMeta));

        if (show == null || string.IsNullOrEmpty(show.MediumImageUrl))
        {
            IsPosterLoading = false;
            return;
        }

        if (show.Thumbnail != null)
        {
            SetSelectedShowPoster(show.Thumbnail, ownsBitmap: false);
            IsPosterLoading = false;
            return;
        }

        if (_isLoadingThumbnails)
        {
            IsPosterLoading = true;
            return;
        }

        try
        {
            IsPosterLoading = true;
            var loaded = await _imageLoadingService.LoadBitMapAsync(show.MediumImageUrl, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                loaded?.Dispose();
                return;
            }
            SetSelectedShowPoster(loaded, ownsBitmap: true);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            SetSelectedShowPoster(null, ownsBitmap: false);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsPosterLoading = false;
            }
        }
    }

    private async Task<Bitmap?> LoadResultThumbnailAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        try
        {
            return await _imageLoadingService.LoadBitMapAsync(imageUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Image load failed for {imageUrl}: {ex.Message}");
            return null;
        }
    }

    private void SelectAllSeasons()
    {
        _suppressSelectionMetrics = true;
        try
        {
            foreach (var option in SeasonsCheckBoxes)
            {
                option.IsChecked = true;
            }
        }
        finally
        {
            _suppressSelectionMetrics = false;
        }
        UpdateSelectionMetrics();
    }

    private void ClearAllSeasons()
    {
        _suppressSelectionMetrics = true;
        try
        {
            foreach (var option in SeasonsCheckBoxes)
            {
                option.IsChecked = false;
            }
        }
        finally
        {
            _suppressSelectionMetrics = false;
        }
        UpdateSelectionMetrics();
    }

    private void OnSeasonSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppressSelectionMetrics && e.PropertyName == nameof(CheckboxOption<Season>.IsChecked))
        {
            UpdateSelectionMetrics();
        }
    }

    private void UpdateSelectionMetrics()
    {
        var selectedSeasons = 0;
        var selectedEpisodes = 0;
        for (var i = 0; i < SeasonsCheckBoxes.Count; i++)
        {
            var option = SeasonsCheckBoxes[i];
            if (option.IsChecked)
            {
                selectedSeasons++;
                selectedEpisodes += option.Value.TotalEpisodeCount;
            }
        }

        SelectedSeasonCount = selectedSeasons;
        SelectedEpisodeCount = selectedEpisodes;

        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(EpisodeSummary));
        OnPropertyChanged(nameof(SelectedShowMeta));
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(CanClearAllSeasons));
        ClearAllSeasonsCommand.NotifyCanExecuteChanged();
        RaiseSearchStateProperties();
    }

    private void RaiseSearchStateProperties()
    {
        OnPropertyChanged(nameof(ShowInitialPrompt));
        OnPropertyChanged(nameof(ShowNoSearchResults));
        OnPropertyChanged(nameof(HasNoSelectedShow));
        OnPropertyChanged(nameof(HasSelectedShow));
        OnPropertyChanged(nameof(IsSearching));
    }

    private string BuildSelectedShowMeta()
    {
        if (SelectedShow == null)
        {
            return string.Empty;
        }

        var pieces = new List<string>(3);
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

    private void SetSelectedShowPoster(Bitmap? newPoster, bool ownsBitmap)
    {
        if (ReferenceEquals(SelectedShowPoster, newPoster))
        {
            _ownsSelectedShowPoster = ownsBitmap;
            return;
        }

        var previous = SelectedShowPoster;
        var previousOwned = _ownsSelectedShowPoster;
        _ownsSelectedShowPoster = ownsBitmap;
        SelectedShowPoster = newPoster;

        if (previousOwned)
        {
            previous?.Dispose();
        }
    }

    private void DisposeAndClearSearchResults()
    {
        for (var i = 0; i < SearchResults.Count; i++)
        {
            SearchResults[i].Dispose();
        }
        SearchResults.Clear();
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        if (cts is null) return;
        cts.Cancel();
        cts.Dispose();
        cts = null;
    }

    public void Reset()
    {
        CancelAndDispose(ref _posterLoadCancellationTokenSource);
        CancelAndDispose(ref _thumbnailLoadCancellationTokenSource);
        CancelAndDispose(ref _seasonsLoadCancellationTokenSource);
        _isLoadingThumbnails = false;

        foreach (var seasonSelectSubscription in _seasonSelectionSubscriptions)
        {
            seasonSelectSubscription.PropertyChanged -= OnSeasonSelectionChanged;
        }

        _seasonSelectionSubscriptions.Clear();
        SearchText = string.Empty;
        SetSelectedShowPoster(null, ownsBitmap: false);
        DisposeAndClearSearchResults();
        SelectedShow = null;
        IsPosterLoading = false;
        HasSearched = false;
        IsSearching = false;
        SelectedSeasonCount = 0;
        SelectedEpisodeCount = 0;

        OnPropertyChanged(nameof(SearchResults));
        OnPropertyChanged(nameof(SeasonCount));
        UpdateSelectionMetrics();
    }
}
