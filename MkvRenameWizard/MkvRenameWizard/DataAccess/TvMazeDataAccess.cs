namespace MkvRenameWizard.DataAccess;

using System;
using System.Net.Http;
using System.Threading.Tasks;

public class TvMazeDataAccess : ITvMazeDataAccess
{
    private readonly HttpClient _httpClient;

    public TvMazeDataAccess(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HttpResponseMessage> FindShowIdByNameAsync(string showName)
    {
        var uri = $"search/shows?q={Uri.EscapeDataString(showName)}";
        return await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
    }

    public async Task<HttpResponseMessage> ListSeasonsAsync(long showId)
    {
        var uri = $"shows/{showId}/seasons";
        return await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
    }

    public async Task<HttpResponseMessage> ListEpisodesBySeasonIdAsync(long seasonId)
    {
        var uri = $"seasons/{seasonId}/episodes";
        return await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
    }
}
