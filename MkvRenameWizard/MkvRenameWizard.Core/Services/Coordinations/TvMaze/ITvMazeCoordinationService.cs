using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Coordinations.TvMaze;

internal interface ITvMazeCoordinationService
{
    Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Season>> RetrieveSeasonsByShowIdAsync(long showId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Episode>> RetrieveEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default);
}
