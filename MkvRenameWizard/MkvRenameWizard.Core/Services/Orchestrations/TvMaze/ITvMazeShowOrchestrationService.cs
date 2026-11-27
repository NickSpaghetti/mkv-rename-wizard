using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Orchestrations.TvMaze;

internal interface ITvMazeShowOrchestrationService
{
    Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default);
}
