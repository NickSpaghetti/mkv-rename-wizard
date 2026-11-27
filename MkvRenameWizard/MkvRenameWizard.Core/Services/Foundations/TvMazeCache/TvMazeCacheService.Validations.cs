using MkvRenameWizard.Core.Models.TvMaze.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeCache;

internal sealed partial class TvMazeCacheService
{
    private static void ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidTvMazeQueryException("Cache query key is required.");
        }
    }

    private static void ValidateId(long id, string name)
    {
        if (id <= 0)
        {
            throw new InvalidTvMazeQueryException($"Cache key '{name}' must be greater than zero.");
        }
    }

    private static void ValidateList<T>(IReadOnlyList<T> items, string name)
    {
        if (items is null)
        {
            throw new InvalidTvMazeQueryException($"Cache value '{name}' cannot be null.");
        }
    }
}
