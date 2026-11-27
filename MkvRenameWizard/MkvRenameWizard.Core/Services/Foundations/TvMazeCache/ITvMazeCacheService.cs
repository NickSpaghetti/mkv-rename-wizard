using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeCache;

internal interface ITvMazeCacheService
{
    IReadOnlyList<Show>? RetrieveCachedShows(string query);
    void CacheShows(string query, IReadOnlyList<Show> shows);

    IReadOnlyList<Season>? RetrieveCachedSeasons(long showId);
    void CacheSeasons(long showId, IReadOnlyList<Season> seasons);

    IReadOnlyList<Episode>? RetrieveCachedEpisodes(long seasonId);
    void CacheEpisodes(long seasonId, IReadOnlyList<Episode> episodes);
}
