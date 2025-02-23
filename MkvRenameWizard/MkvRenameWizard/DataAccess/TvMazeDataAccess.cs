namespace MkvRenameWizard.DataAccess;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

public class TvMazeDataAccess : ITvMazeDataAccess
{
    private readonly HttpClient _httpClient;

    public TvMazeDataAccess()
    {
        _httpClient = new HttpClient();
    }

    public async Task<HttpResponseMessage> FindShowIdByNameAsync(string showName)
    {
        var uri = $"https://api.tvmaze.com/search/shows?q={showName}";
        return await _httpClient.GetAsync(uri);
    }

    public async Task<HttpResponseMessage> ListSeasonsAsync(long showId)
    {
        var uri = $"https://api.tvmaze.com/shows/{showId}/seasons";
        return await _httpClient.GetAsync(uri);
    }

    public async Task<HttpResponseMessage> ListEpisodesBySeasonIdAsync(long seasonId)
    {
        var uri = $"https://api.tvmaze.com/seasons/{seasonId}/episodes";
        return await _httpClient.GetAsync(uri);
    }
}