using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MkvRenameWizard.DataAccess;

public interface ITvMazeDataAccess
{
    Task<HttpResponseMessage> FindShowIdByNameAsync(string showName, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> ListSeasonsAsync(long showId, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> ListEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default);
}
