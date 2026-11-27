using MkvRenameWizard.Core.Models.TvMaze.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeEpisodes;

internal sealed partial class TvMazeEpisodeService
{
    private static void ValidateSeasonId(long seasonId)
    {
        if (seasonId <= 0)
        {
            throw new InvalidTvMazeQueryException($"Season ID '{seasonId}' must be greater than zero.");
        }
    }
}
