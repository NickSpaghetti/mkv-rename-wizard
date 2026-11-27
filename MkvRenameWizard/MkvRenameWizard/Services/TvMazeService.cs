using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Core.Services.Aggregations;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.Services;

public class TvMazeService(
    IMkvRenameWizardService coreService,
    ILogger<TvMazeService> logger) : ITvMazeService
{
    public async Task<List<ShowSearchResult>> FindShowIdByNameAsync(string showName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(showName))
        {
            return new List<ShowSearchResult>();
        }

        try
        {
            var shows = await coreService.SearchShowsByNameAsync(showName, cancellationToken);
            var results = new List<ShowSearchResult>(shows.Count);
            foreach (var s in shows)
            {
                var show = new Show
                {
                    Id = s.Id,
                    Name = s.Name,
                    Language = s.Language,
                    Genres = s.Genres.ToList(),
                    Premiered = s.Premiered,
                    Ended = s.Ended,
                    YearRange = s.YearRange,
                    RatingAverage = s.RatingAverage,
                    AirNetworkLabel = s.AirNetworkLabel,
                    MediumImageUrl = s.MediumImageUrl,
                    OriginalImageUrl = s.OriginalImageUrl,
                    PlainSummary = s.PlainSummary
                };
                results.Add(new ShowSearchResult(s.Score, show));
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            return new List<ShowSearchResult>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get list of tv shows with the name {ShowName}", showName);
            return new List<ShowSearchResult>();
        }
    }

    public async Task<List<Season>> ListSeasonsAsync(long showId, CancellationToken cancellationToken = default)
    {
        if (showId <= 0)
        {
            logger.LogWarning("Invalid show id {ShowId}", showId);
            return new List<Season>();
        }

        try
        {
            var coreSeasons = await coreService.RetrieveSeasonsByShowIdAsync(showId, cancellationToken);
            var seasons = new List<Season>(coreSeasons.Count);
            foreach (var s in coreSeasons)
            {
                seasons.Add(new Season
                {
                    Id = s.Id,
                    Name = s.Name,
                    Number = s.Number,
                    TotalEpisodeCount = s.TotalEpisodeCount
                });
            }

            return seasons;
        }
        catch (OperationCanceledException)
        {
            return new List<Season>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get list of seasons with the show id {ShowId}.", showId);
            return new List<Season>();
        }
    }

    public async Task<List<Episode>> ListEpisodesBySeasonAsync(long seasonId, CancellationToken cancellationToken = default)
    {
        if (seasonId <= 0)
        {
            logger.LogWarning("Invalid season id {SeasonId}", seasonId);
            return new List<Episode>();
        }

        try
        {
            var coreEpisodes = await coreService.RetrieveEpisodesBySeasonIdAsync(seasonId, cancellationToken);
            var episodes = new List<Episode>(coreEpisodes.Count);
            foreach (var e in coreEpisodes)
            {
                episodes.Add(new Episode
                {
                    Id = e.Id,
                    Name = e.Name,
                    EpisodeNumber = e.EpisodeNumber,
                    Season = e.Season,
                    Type = e.Type,
                    RunTime = e.RunTime,
                    AirDate = e.AirDate
                });
            }

            return episodes;
        }
        catch (OperationCanceledException)
        {
            return new List<Episode>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get list of episodes with the season id {SeasonId}.", seasonId);
            return new List<Episode>();
        }
    }
}
