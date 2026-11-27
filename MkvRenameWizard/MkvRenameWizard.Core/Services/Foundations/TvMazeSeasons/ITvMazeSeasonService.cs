using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeSeasons;

internal interface ITvMazeSeasonService
{
    Task<IReadOnlyList<Season>> RetrieveSeasonsByShowIdAsync(long showId, CancellationToken cancellationToken = default);
}
