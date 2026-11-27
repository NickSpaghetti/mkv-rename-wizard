using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.TvMaze;
using MkvRenameWizard.Core.Services.Orchestrations.TvMaze;

namespace MkvRenameWizard.Core.Services.Coordinations.TvMaze;

internal sealed partial class TvMazeCoordinationService(
    ITvMazeShowOrchestrationService showOrchestrationService,
    ITvMazeSeasonOrchestrationService seasonOrchestrationService,
    ILoggingBroker loggingBroker) : ITvMazeCoordinationService
{
    public Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default) =>
        TryCatchAsync(() => showOrchestrationService.SearchShowsByNameAsync(showName, cancellationToken));

    public Task<IReadOnlyList<Season>> RetrieveSeasonsByShowIdAsync(long showId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(() => seasonOrchestrationService.RetrieveSeasonsByShowIdAsync(showId, cancellationToken));

    public Task<IReadOnlyList<Episode>> RetrieveEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(() => seasonOrchestrationService.RetrieveEpisodesBySeasonIdAsync(seasonId, cancellationToken));
}
