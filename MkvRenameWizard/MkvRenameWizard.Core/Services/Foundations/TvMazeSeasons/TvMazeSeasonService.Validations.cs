using MkvRenameWizard.Core.Models.TvMaze.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeSeasons;

internal sealed partial class TvMazeSeasonService
{
    private static void ValidateShowId(long showId)
    {
        if (showId <= 0)
        {
            throw new InvalidTvMazeQueryException($"Show ID '{showId}' must be greater than zero.");
        }
    }
}
