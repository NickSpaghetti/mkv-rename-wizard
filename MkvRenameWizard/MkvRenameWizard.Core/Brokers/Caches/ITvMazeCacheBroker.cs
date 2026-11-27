using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Brokers.Caches;

internal interface ITvMazeCacheBroker
{
    int ShowsCount { get; }
    bool TryGetShows(string query, out IReadOnlyList<Show>? shows);
    void SetShows(string query, IReadOnlyList<Show> shows);
    void ClearShows();

    int SeasonsCount { get; }
    bool TryGetSeasons(long showId, out IReadOnlyList<Season>? seasons);
    void SetSeasons(long showId, IReadOnlyList<Season> seasons);
    void ClearSeasons();

    int EpisodesCount { get; }
    bool TryGetEpisodes(long seasonId, out IReadOnlyList<Episode>? episodes);
    void SetEpisodes(long seasonId, IReadOnlyList<Episode> episodes);
    void ClearEpisodes();
}
