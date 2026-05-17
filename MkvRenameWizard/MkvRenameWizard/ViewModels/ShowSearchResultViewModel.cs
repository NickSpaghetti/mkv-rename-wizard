using System.Collections.Generic;
using System.Collections.Immutable;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.ViewModels;

public class ShowSearchResultViewModel : ViewModelBase
{
    public ShowSearchResultViewModel(ShowSearchResult showSearchResult)
    {
        Score = showSearchResult.Score;
        Id = showSearchResult.Show.Id;
        Name = showSearchResult.Show.Name;
        Genres = showSearchResult.Show.Genres.ToImmutableList();
        YearRange = showSearchResult.Show.YearRange;
        RatingAverage = showSearchResult.Show.RatingAverage;
        AirNetworkLabel =  showSearchResult.Show.AirNetworkLabel;
        MediumImageUrl =  showSearchResult.Show.MediumImageUrl;
        OriginalImageUrl =  showSearchResult.Show.OriginalImageUrl;
        PlanSummary = showSearchResult.Show.PlainSummary;
    }
    
    public double Score { get; }
    public long Id { get; }
    public string Name { get; }
    public IReadOnlyCollection<string> Genres { get; }
    public string YearRange { get; }
    public double? RatingAverage { get; }
    public string AirNetworkLabel { get; }
    public string MediumImageUrl { get; }
    public string OriginalImageUrl { get; }
    public string PlanSummary { get; }
}