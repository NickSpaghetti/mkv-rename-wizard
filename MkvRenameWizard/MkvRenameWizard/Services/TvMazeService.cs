using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Models.TvMaze.Dto;

namespace MkvRenameWizard.Services;

public class TvMazeService : ITvMazeService
{
    private readonly ITvMazeDataAccess _tvMazeDataAccess;
    private readonly ILogger<TvMazeService> _logger;
    private readonly ConcurrentDictionary<long, List<Episode>> _seasonEpisodesCache = new();

    public TvMazeService(ITvMazeDataAccess tvMazeDataAccess, ILogger<TvMazeService> logger)
    {
        _tvMazeDataAccess = tvMazeDataAccess;
        _logger = logger;
    }

    public async Task<List<ShowSearchResult>> FindShowIdByNameAsync(string showName)
    {
        if (string.IsNullOrEmpty(showName))
        {
            return new List<ShowSearchResult>();
        }

        try
        {
            using var response = await _tvMazeDataAccess.FindShowIdByNameAsync(showName);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var tvShowResultsDtoList = await JsonSerializer.DeserializeAsync(
                stream, TvMazeJsonContext.Default.ListShowSearchResultDto);

            if (tvShowResultsDtoList is null || tvShowResultsDtoList.Count == 0)
            {
                return new List<ShowSearchResult>();
            }

            var tvShowResults = new List<ShowSearchResult>(tvShowResultsDtoList.Count);
            foreach (var showResultDto in tvShowResultsDtoList)
            {
                var dto = showResultDto.ShowDto;
                if (dto is null) continue;

                var show = new Show
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    Language = dto.Language,
                    Genres = dto.Genres ?? new List<string>(),
                    Premiered = dto.Premiered,
                    Ended = dto.Ended,
                    RatingAverage = dto.RatingDto?.Average,
                    AirNetworkLabel = dto.NetworkDto?.Name ?? dto.WebChannel?.Name ?? string.Empty,
                    MediumImageUrl = dto.ImageDto?.Medium ?? string.Empty,
                    OriginalImageUrl = dto.ImageDto?.Original ?? string.Empty,
                    PlainSummary = StripTvMazeHtml(dto.Summary ?? "No Summary found"),
                };
                tvShowResults.Add(new ShowSearchResult(showResultDto.Score, show));
            }

            return tvShowResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get list of tv shows with the name {ShowName}", showName);
        }
        return new List<ShowSearchResult>();
    }

    public static string StripTvMazeHtml(string summaryHtml)
    {
        if (string.IsNullOrEmpty(summaryHtml))
        {
            return string.Empty;
        }

        var src = summaryHtml.AsSpan();
        if (src.IndexOf('<') < 0)
        {
            return src.IndexOf('&') < 0 ? summaryHtml : WebUtility.HtmlDecode(summaryHtml);
        }

        char[]? rented = null;
        Span<char> buffer = src.Length <= 512
            ? stackalloc char[src.Length]
            : (rented = ArrayPool<char>.Shared.Rent(src.Length));

        try
        {
            var written = 0;
            var inTag = false;
            var hasEntity = false;

            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>' && inTag)
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    if (c == '&') hasEntity = true;
                    buffer[written++] = c;
                }
            }

            var stripped = new string(buffer[..written]);
            return hasEntity ? WebUtility.HtmlDecode(stripped) : stripped;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    public async Task<List<Season>> ListSeasonsAsync(long showId)
    {
        if (showId <= 0)
        {
            _logger.LogWarning("Invalid show id {ShowId}", showId);
            return new List<Season>();
        }

        try
        {
            using var response = await _tvMazeDataAccess.ListSeasonsAsync(showId);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var seasonDtoList = await JsonSerializer.DeserializeAsync(
                stream, TvMazeJsonContext.Default.ListListSeasonResultDto);

            if (seasonDtoList is null || seasonDtoList.Count == 0)
            {
                return new List<Season>();
            }

            var seasons = new List<Season>(seasonDtoList.Count);
            foreach (var seasonResultDto in seasonDtoList)
            {
                var totalEpisodeCount = seasonResultDto.EpisodeOrder;
                if (totalEpisodeCount == null)
                {
                    var episodes = await ListEpisodesBySeasonAsync(seasonResultDto.Id);
                    _seasonEpisodesCache[seasonResultDto.Id] = episodes;
                    totalEpisodeCount = episodes.Count;
                }
                seasons.Add(new Season
                {
                    Id = seasonResultDto.Id,
                    Name = seasonResultDto.Name,
                    Number = seasonResultDto.Number,
                    TotalEpisodeCount = totalEpisodeCount.Value
                });
            }

            return seasons;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get list of seasons with the show id {ShowId}.", showId);
        }
        return new List<Season>();
    }

    public async Task<List<Episode>> ListEpisodesBySeasonAsync(long seasonId)
    {
        if (seasonId <= 0)
        {
            _logger.LogWarning("Invalid season id {SeasonId}", seasonId);
            return new List<Episode>();
        }

        if (_seasonEpisodesCache.TryRemove(seasonId, out var cached))
        {
            return cached;
        }

        try
        {
            using var response = await _tvMazeDataAccess.ListEpisodesBySeasonIdAsync(seasonId);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var episodesDto = await JsonSerializer.DeserializeAsync(
                stream, TvMazeJsonContext.Default.ListEpisodeDto);

            if (episodesDto is null || episodesDto.Count == 0)
            {
                return new List<Episode>();
            }

            var episodes = new List<Episode>(episodesDto.Count);
            foreach (var episodeDto in episodesDto)
            {
                episodes.Add(new Episode
                {
                    Id = episodeDto.Id,
                    Name = episodeDto.Name,
                    EpisodeNumber = episodeDto.Number,
                    Season = episodeDto.Season,
                    Type = episodeDto.Type,
                    RunTime = episodeDto.Runtime,
                    AirDate = DateOnly.TryParse(episodeDto.Airdate, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var d) ? d : null,
                });
            }

            return episodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get list of episodes with the season id {SeasonId}.", seasonId);
        }

        return new List<Episode>();
    }
}
