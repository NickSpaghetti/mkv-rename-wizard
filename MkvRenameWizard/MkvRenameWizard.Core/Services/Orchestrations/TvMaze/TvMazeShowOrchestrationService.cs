using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.TvMaze;
using MkvRenameWizard.Core.Services.Foundations.TvMazeCache;
using MkvRenameWizard.Core.Services.Foundations.TvMazeShows;

namespace MkvRenameWizard.Core.Services.Orchestrations.TvMaze;

internal sealed partial class TvMazeShowOrchestrationService(
    ITvMazeShowService tvMazeShowService,
    ITvMazeCacheService tvMazeCacheService,
    ILoggingBroker loggingBroker) : ITvMazeShowOrchestrationService
{
    public Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default) =>
        TryCatchAsync(async () =>
        {
            var cached = tvMazeCacheService.RetrieveCachedShows(showName);
            if (cached is not null)
            {
                return cached;
            }

            var shows = await tvMazeShowService.SearchShowsByNameAsync(showName, cancellationToken);
            if (shows.Count > 0)
            {
                tvMazeCacheService.CacheShows(showName, shows);
            }

            return shows;
        });
}
