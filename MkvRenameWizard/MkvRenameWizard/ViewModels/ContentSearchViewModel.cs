using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DynamicData;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Services;
using ReactiveUI;

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
        private set => this.RaiseAndSetIfChanged(ref field, value);
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
    public bool ShowNoSearchResults => HasNoSelectedShow && HasSearched && !IsSearching && ResultCount == 0;
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

        SearchResults.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(ResultCountLabel));
            RaiseSearchStateProperties();
        };

        SeasonsCheckBoxes = new ObservableCollection<CheckboxOption<Season>>();

        SearchCommand = ReactiveCommand.CreateFromTask<Show>(ExecuteSearchCommand);

        UpdateCheckboxOptionsCommand = ReactiveCommand.CreateFromTask(UpdateCheckboxOptions);
        this.WhenAnyValue(x => x.SelectedShow)
            .Do(show => _ = LoadPosterAsync(show))
            .Select(_ => Unit.Default)
            .InvokeCommand(UpdateCheckboxOptionsCommand);

        SelectAllSeasonsCommand = ReactiveCommand.Create(SelectAllSeasons);
        var anySeasonChecked = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => SeasonsCheckBoxes.CollectionChanged += h,
                h => SeasonsCheckBoxes.CollectionChanged -= h)
            .Select(_ => Unit.Default)
            .StartWith(Unit.Default)
            .Merge(this.WhenAnyValue(x => x.SelectedSeasonCount).Select(_ => Unit.Default))
            .Select(_ => SelectedShow != null && SeasonsCheckBoxes.Any(x => x.IsChecked))
            .DistinctUntilChanged();

        ClearAllSeasonsCommand = ReactiveCommand.Create(ClearAllSeasons, anySeasonChecked);
    }

    public async Task ExecuteSearchCommand(Show show)
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
            SearchResults.AddRange(showSearchResult);

            // Start progressive thumbnail loading before selecting the first show so LoadPosterAsync
            // shares the in-flight thumbnail request instead of downloading the same URL twice.
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
            var vm = items[i];
            tasks[i] = LoadSingleThumbnailAsync(vm, cancellationToken);
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

        // If this show is currently selected and waiting for its poster, share the loaded thumbnail directly
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
        this.RaisePropertyChanged(nameof(HasSelectedShow));
        this.RaisePropertyChanged(nameof(SelectedShowMeta));

        if (show == null || string.IsNullOrEmpty(show.MediumImageUrl))
        {
            IsPosterLoading = false;
            return;
        }

        // Reuse the already-downloaded MediumImageUrl thumbnail instead of downloading the same URL twice
        if (show.Thumbnail != null)
        {
            SetSelectedShowPoster(show.Thumbnail, ownsBitmap: false);
            IsPosterLoading = false;
            return;
        }

        // If the background thumbnail batch is currently fetching this show's thumbnail, wait for it
        // rather than issuing a duplicate HTTP request for the exact same MediumImageUrl.
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

        this.RaisePropertyChanged(nameof(SearchResults));
        this.RaisePropertyChanged(nameof(SeasonCount));
        UpdateSelectionMetrics();
    }
}
