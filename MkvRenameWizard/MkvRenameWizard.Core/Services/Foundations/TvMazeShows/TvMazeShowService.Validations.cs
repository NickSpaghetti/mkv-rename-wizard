using MkvRenameWizard.Core.Models.TvMaze.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeShows;

internal sealed partial class TvMazeShowService
{
    private static void ValidateShowName(string showName)
    {
        if (string.IsNullOrWhiteSpace(showName))
        {
            throw new InvalidTvMazeQueryException("Show name is required.");
        }
    }
}
