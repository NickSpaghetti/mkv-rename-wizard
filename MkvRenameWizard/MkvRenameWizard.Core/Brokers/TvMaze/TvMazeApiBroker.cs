using System.Text.Json;
using MkvRenameWizard.Core.Models.TvMaze.Dto;

namespace MkvRenameWizard.Core.Brokers.TvMaze;

internal sealed class TvMazeApiBroker(HttpClient httpClient) : ITvMazeApiBroker
{
    public async Task<List<ShowSearchResultDto>?> SearchShowsAsync(string query, CancellationToken cancellationToken = default)
    {
        var uri = $"search/shows?q={Uri.EscapeDataString(query)}";
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync(
            stream,
            TvMazeJsonContext.Default.ListShowSearchResultDto,
            cancellationToken);
    }

    public async Task<List<ListSeasonResultDto>?> GetSeasonsAsync(long showId, CancellationToken cancellationToken = default)
    {
        var uri = $"shows/{showId}/seasons";
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync(
            stream,
            TvMazeJsonContext.Default.ListListSeasonResultDto,
            cancellationToken);
    }

    public async Task<List<EpisodeDto>?> GetEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default)
    {
        var uri = $"seasons/{seasonId}/episodes";
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync(
            stream,
            TvMazeJsonContext.Default.ListEpisodeDto,
            cancellationToken);
    }
}
