using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.TvMaze;
using MkvRenameWizard.Core.Services.Foundations.TvMazeCache;
using MkvRenameWizard.Core.Services.Foundations.TvMazeEpisodes;
using MkvRenameWizard.Core.Services.Foundations.TvMazeSeasons;

namespace MkvRenameWizard.Core.Services.Orchestrations.TvMaze;

internal sealed partial class TvMazeSeasonOrchestrationService(
    ITvMazeSeasonService tvMazeSeasonService,
    ITvMazeEpisodeService tvMazeEpisodeService,
    ITvMazeCacheService tvMazeCacheService,
    ILoggingBroker loggingBroker) : ITvMazeSeasonOrchestrationService
{
    public Task<IReadOnlyList<Season>> RetrieveSeasonsByShowIdAsync(long showId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(async () =>
        {
            var cached = tvMazeCacheService.RetrieveCachedSeasons(showId);
            if (cached is not null)
            {
                return cached;
            }

            var rawSeasons = await tvMazeSeasonService.RetrieveSeasonsByShowIdAsync(showId, cancellationToken);
            if (rawSeasons.Count == 0)
            {
                return rawSeasons;
            }

            Dictionary<long, Task<IReadOnlyList<Episode>>>? missingOrderTasks = null;
            for (var i = 0; i < rawSeasons.Count; i++)
            {
                var season = rawSeasons[i];
                if (!season.HasEpisodeOrder)
                {
                    missingOrderTasks ??= new Dictionary<long, Task<IReadOnlyList<Episode>>>();
                    missingOrderTasks[season.Id] = RetrieveEpisodesCoreAsync(season.Id, cancellationToken);
                }
            }

            if (missingOrderTasks is not null)
            {
                await Task.WhenAll(missingOrderTasks.Values);
            }

            var resolvedSeasons = new List<Season>(rawSeasons.Count);
            for (var i = 0; i < rawSeasons.Count; i++)
            {
                var season = rawSeasons[i];
                if (!season.HasEpisodeOrder &&
                    missingOrderTasks is not null &&
                    missingOrderTasks.TryGetValue(season.Id, out var epTask))
                {
                    resolvedSeasons.Add(season with { TotalEpisodeCount = epTask.Result.Count });
                }
                else
                {
                    resolvedSeasons.Add(season);
                }
            }

            IReadOnlyList<Season> result = resolvedSeasons;
            tvMazeCacheService.CacheSeasons(showId, result);
            return result;
        });

    public Task<IReadOnlyList<Episode>> RetrieveEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(() => RetrieveEpisodesCoreAsync(seasonId, cancellationToken));

    private async Task<IReadOnlyList<Episode>> RetrieveEpisodesCoreAsync(long seasonId, CancellationToken cancellationToken)
    {
        var cached = tvMazeCacheService.RetrieveCachedEpisodes(seasonId);
        if (cached is not null)
        {
            return cached;
        }

        var episodes = await tvMazeEpisodeService.RetrieveEpisodesBySeasonIdAsync(seasonId, cancellationToken);
        if (episodes.Count > 0)
        {
            tvMazeCacheService.CacheEpisodes(seasonId, episodes);
        }

        return episodes;
    }
}
