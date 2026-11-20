using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Models.TvMaze.Dto;

namespace MkvRenameWizard.Services;

public class TvMazeService : ITvMazeService
{
    private const int MaxSearchCacheEntries = 50;
    private const int MaxSeasonCacheEntries = 100;
    private const int MaxEpisodeCacheEntries = 200;

    private readonly ITvMazeDataAccess _tvMazeDataAccess;
    private readonly ILogger<TvMazeService> _logger;
    private readonly ConcurrentDictionary<string, List<ShowSearchResult>> _searchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, List<Season>> _seasonsCache = new();
    private readonly ConcurrentDictionary<long, List<Episode>> _seasonEpisodesCache = new();

    public TvMazeService(ITvMazeDataAccess tvMazeDataAccess, ILogger<TvMazeService> logger)
    {
        _tvMazeDataAccess = tvMazeDataAccess;
        _logger = logger;
    }

    public async Task<List<ShowSearchResult>> FindShowIdByNameAsync(string showName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(showName))
        {
            return new List<ShowSearchResult>();
        }

        var cacheKey = showName.Trim();
        if (_searchCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            using var response = await _tvMazeDataAccess.FindShowIdByNameAsync(cacheKey, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var tvShowResultsDtoList = await JsonSerializer.DeserializeAsync(
                stream, TvMazeJsonContext.Default.ListShowSearchResultDto, cancellationToken);

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

            if (_searchCache.Count >= MaxSearchCacheEntries)
            {
                _searchCache.Clear();
            }
            _searchCache[cacheKey] = tvShowResults;

            return tvShowResults;
        }
        catch (OperationCanceledException)
        {
            return new List<ShowSearchResult>();
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

    public async Task<List<Season>> ListSeasonsAsync(long showId, CancellationToken cancellationToken = default)
    {
        if (showId <= 0)
        {
            _logger.LogWarning("Invalid show id {ShowId}", showId);
            return new List<Season>();
        }

        if (_seasonsCache.TryGetValue(showId, out var cached))
        {
            return cached;
        }

        try
        {
            using var response = await _tvMazeDataAccess.ListSeasonsAsync(showId, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var seasonDtoList = await JsonSerializer.DeserializeAsync(
                stream, TvMazeJsonContext.Default.ListListSeasonResultDto, cancellationToken);

            if (seasonDtoList is null || seasonDtoList.Count == 0)
            {
                return new List<Season>();
            }

            // Fetch any seasons with unknown EpisodeOrder in parallel instead of serially
            Dictionary<long, Task<List<Episode>>>? missingOrderTasks = null;
            foreach (var dto in seasonDtoList)
            {
                if (dto.EpisodeOrder == null)
                {
                    missingOrderTasks ??= new Dictionary<long, Task<List<Episode>>>();
                    missingOrderTasks[dto.Id] = ListEpisodesBySeasonAsync(dto.Id, cancellationToken);
                }
            }

            if (missingOrderTasks is not null)
            {
                await Task.WhenAll(missingOrderTasks.Values);
            }

            var seasons = new List<Season>(seasonDtoList.Count);
            foreach (var seasonResultDto in seasonDtoList)
            {
                var totalEpisodeCount = seasonResultDto.EpisodeOrder
                    ?? (missingOrderTasks is not null && missingOrderTasks.TryGetValue(seasonResultDto.Id, out var epTask)
                        ? epTask.Result.Count
                        : 0);

                seasons.Add(new Season
                {
                    Id = seasonResultDto.Id,
                    Name = seasonResultDto.Name,
                    Number = seasonResultDto.Number,
                    TotalEpisodeCount = totalEpisodeCount
                });
            }

            if (_seasonsCache.Count >= MaxSeasonCacheEntries)
            {
                _seasonsCache.Clear();
            }
            _seasonsCache[showId] = seasons;

            return seasons;
        }
        catch (OperationCanceledException)
        {
            return new List<Season>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get list of seasons with the show id {ShowId}.", showId);
        }
        return new List<Season>();
    }

    public async Task<List<Episode>> ListEpisodesBySeasonAsync(long seasonId, CancellationToken cancellationToken = default)
    {
        if (seasonId <= 0)
        {
            _logger.LogWarning("Invalid season id {SeasonId}", seasonId);
            return new List<Episode>();
        }

        if (_seasonEpisodesCache.TryGetValue(seasonId, out var cached))
        {
            return cached;
        }

        try
        {
            using var response = await _tvMazeDataAccess.ListEpisodesBySeasonIdAsync(seasonId, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var episodesDto = await JsonSerializer.DeserializeAsync(
                stream, TvMazeJsonContext.Default.ListEpisodeDto, cancellationToken);

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

            if (_seasonEpisodesCache.Count >= MaxEpisodeCacheEntries)
            {
                _seasonEpisodesCache.Clear();
            }
            _seasonEpisodesCache[seasonId] = episodes;

            return episodes;
        }
        catch (OperationCanceledException)
        {
            return new List<Episode>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get list of episodes with the season id {SeasonId}.", seasonId);
        }

        return new List<Episode>();
    }
}
