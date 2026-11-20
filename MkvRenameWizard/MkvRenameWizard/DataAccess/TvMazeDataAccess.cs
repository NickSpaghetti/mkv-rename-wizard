using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MkvRenameWizard.DataAccess;

public class TvMazeDataAccess : ITvMazeDataAccess
{
    private readonly HttpClient _httpClient;

    public TvMazeDataAccess(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<HttpResponseMessage> FindShowIdByNameAsync(string showName, CancellationToken cancellationToken = default)
    {
        var uri = $"search/shows?q={Uri.EscapeDataString(showName)}";
        return _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public Task<HttpResponseMessage> ListSeasonsAsync(long showId, CancellationToken cancellationToken = default)
    {
        var uri = $"shows/{showId}/seasons";
        return _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public Task<HttpResponseMessage> ListEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default)
    {
        var uri = $"seasons/{seasonId}/episodes";
        return _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
