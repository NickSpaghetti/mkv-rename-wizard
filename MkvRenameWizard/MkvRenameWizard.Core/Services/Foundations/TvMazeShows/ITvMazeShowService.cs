using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeShows;

internal interface ITvMazeShowService
{
    Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default);
}
