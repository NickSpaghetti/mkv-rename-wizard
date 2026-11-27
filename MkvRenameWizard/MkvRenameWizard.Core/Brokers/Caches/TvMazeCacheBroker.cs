using System.Collections.Concurrent;
using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Brokers.Caches;

internal sealed class TvMazeCacheBroker : ITvMazeCacheBroker
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<Show>> _shows = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, IReadOnlyList<Season>> _seasons = new();
    private readonly ConcurrentDictionary<long, IReadOnlyList<Episode>> _episodes = new();

    public int ShowsCount => _shows.Count;
    public bool TryGetShows(string query, out IReadOnlyList<Show>? shows) => _shows.TryGetValue(query, out shows);
    public void SetShows(string query, IReadOnlyList<Show> shows) => _shows[query] = shows;
    public void ClearShows() => _shows.Clear();

    public int SeasonsCount => _seasons.Count;
    public bool TryGetSeasons(long showId, out IReadOnlyList<Season>? seasons) => _seasons.TryGetValue(showId, out seasons);
    public void SetSeasons(long showId, IReadOnlyList<Season> seasons) => _seasons[showId] = seasons;
    public void ClearSeasons() => _seasons.Clear();

    public int EpisodesCount => _episodes.Count;
    public bool TryGetEpisodes(long seasonId, out IReadOnlyList<Episode>? episodes) => _episodes.TryGetValue(seasonId, out episodes);
    public void SetEpisodes(long seasonId, IReadOnlyList<Episode> episodes) => _episodes[seasonId] = episodes;
    public void ClearEpisodes() => _episodes.Clear();
}
