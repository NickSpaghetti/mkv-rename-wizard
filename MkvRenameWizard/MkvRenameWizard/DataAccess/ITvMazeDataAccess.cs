using System.Net.Http;
using System.Threading.Tasks;

namespace MkvRenameWizard.DataAccess;

public interface ITvMazeDataAccess
{
    Task<HttpResponseMessage> FindShowIdByNameAsync(string showName);
    Task<HttpResponseMessage> ListSeasonsAsync(long showId);
    Task<HttpResponseMessage> ListEpisodesBySeasonIdAsync(long seasonId);

}