using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.Services;

public interface ITvMazeService
{
    Task<List<ShowSearchResult>> FindShowIdByNameAsync(string showName, CancellationToken cancellationToken = default);
    Task<List<Season>> ListSeasonsAsync(long showId, CancellationToken cancellationToken = default);
    Task<List<Episode>> ListEpisodesBySeasonAsync(long seasonId, CancellationToken cancellationToken = default);
}
