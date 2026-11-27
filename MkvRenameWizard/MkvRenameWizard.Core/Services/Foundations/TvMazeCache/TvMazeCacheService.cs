using MkvRenameWizard.Core.Brokers.Caches;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeCache;

internal sealed partial class TvMazeCacheService(
    ITvMazeCacheBroker cacheBroker,
    ILoggingBroker loggingBroker) : ITvMazeCacheService
{
    private const int MaxSearchCacheEntries = 50;
    private const int MaxSeasonCacheEntries = 100;
    private const int MaxEpisodeCacheEntries = 200;

    public IReadOnlyList<Show>? RetrieveCachedShows(string query) =>
        TryCatch(() =>
        {
            ValidateQuery(query);
            return cacheBroker.TryGetShows(query.Trim(), out var shows) ? shows : null;
        });

    public void CacheShows(string query, IReadOnlyList<Show> shows) =>
        TryCatch(() =>
        {
            ValidateQuery(query);
            ValidateList(shows, nameof(shows));

            if (cacheBroker.ShowsCount >= MaxSearchCacheEntries)
            {
                cacheBroker.ClearShows();
            }

            cacheBroker.SetShows(query.Trim(), shows);
        });

    public IReadOnlyList<Season>? RetrieveCachedSeasons(long showId) =>
        TryCatch(() =>
        {
            ValidateId(showId, nameof(showId));
            return cacheBroker.TryGetSeasons(showId, out var seasons) ? seasons : null;
        });

    public void CacheSeasons(long showId, IReadOnlyList<Season> seasons) =>
        TryCatch(() =>
        {
            ValidateId(showId, nameof(showId));
            ValidateList(seasons, nameof(seasons));

            if (cacheBroker.SeasonsCount >= MaxSeasonCacheEntries)
            {
                cacheBroker.ClearSeasons();
            }

            cacheBroker.SetSeasons(showId, seasons);
        });

    public IReadOnlyList<Episode>? RetrieveCachedEpisodes(long seasonId) =>
        TryCatch(() =>
        {
            ValidateId(seasonId, nameof(seasonId));
            return cacheBroker.TryGetEpisodes(seasonId, out var episodes) ? episodes : null;
        });

    public void CacheEpisodes(long seasonId, IReadOnlyList<Episode> episodes) =>
        TryCatch(() =>
        {
            ValidateId(seasonId, nameof(seasonId));
            ValidateList(episodes, nameof(episodes));

            if (cacheBroker.EpisodesCount >= MaxEpisodeCacheEntries)
            {
                cacheBroker.ClearEpisodes();
            }

            cacheBroker.SetEpisodes(seasonId, episodes);
        });
}
