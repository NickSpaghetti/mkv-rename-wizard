using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Models.TvMaze.Dto;

namespace MkvRenameWizard.Services;

public partial class TvMazeService : ITvMazeService
    {
        private readonly ITvMazeDataAccess _tvMazeDataAccess;
        private readonly ILogger<TvMazeService> _logger;
        
        [GeneratedRegex("<.*?>")]
        private static partial Regex HtmlTagRegex();

        public TvMazeService(ITvMazeDataAccess tvMazeDataAccess, ILogger<TvMazeService> logger)
        {
            _tvMazeDataAccess = tvMazeDataAccess;
            _logger =  logger;
        }

        public async Task<List<ShowSearchResult>> FindShowIdByNameAsync(string showName)
        {
            if (string.IsNullOrEmpty(showName))
            {
                return new List<ShowSearchResult>();
            }

            try
            {
                var response = await _tvMazeDataAccess.FindShowIdByNameAsync(showName);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var tvShowResultsDtoList = JsonSerializer.Deserialize<List<ShowSearchResultDto>>(content) ?? new List<ShowSearchResultDto>();

                var tvShowResults = new List<ShowSearchResult>();
                foreach (var showResultDto in tvShowResultsDtoList)
                {
                    var show = new Show
                    {
                        Id = showResultDto.ShowDto.Id,
                        Name = showResultDto.ShowDto.Name,
                        Language = showResultDto.ShowDto.Language,
                        Genres = showResultDto.ShowDto.Genres,
                        Premiered = showResultDto.ShowDto.Premiered,
                        Ended = showResultDto.ShowDto.Ended,
                        RatingAverage = showResultDto.ShowDto.RatingDto.Average,
                        AirNetworkLabel = showResultDto.ShowDto.NetworkDto?.Name ?? showResultDto.ShowDto.WebChannel?.Name ?? string.Empty,
                        MediumImageUrl = showResultDto.ShowDto.ImageDto?.Medium ?? string.Empty,
                        OriginalImageUrl = showResultDto.ShowDto.ImageDto?.Original ?? string.Empty,
                        PlainSummary = StripTvMazeHtml(showResultDto.ShowDto.Summary ?? "No Summary found"),
                    };
                    tvShowResults.Add(new ShowSearchResult(showResultDto.Score,show));
                }

                return tvShowResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get list of tv shows with the name {showName}");
            }
            return new List<ShowSearchResult>();
        }

        public static string StripTvMazeHtml(string summaryHtml)
        {
            if (string.IsNullOrEmpty(summaryHtml))
            {
                return string.Empty;
            }
            
            var strippedHtmlTags = HtmlTagRegex().Replace(summaryHtml, string.Empty);
            return WebUtility.HtmlDecode(strippedHtmlTags);
        }

        public async Task<List<Season>> ListSeasonsAsync(long showId)
        {
            if (showId <= 0)
            {
                _logger.LogWarning($"Invalid show id {showId}");
                return new List<Season>();
            }

            try
            {
                var response = await _tvMazeDataAccess.ListSeasonsAsync(showId);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var tvShowResultsDtoList = JsonSerializer.Deserialize<List<ListSeasonResultDto>>(content) ?? new List<ListSeasonResultDto>();

                var seasons = new List<Season>();
                foreach (var seasonResultDto in tvShowResultsDtoList)
                {
                    var totalEpisodecount =  seasonResultDto.EpisodeOrder;
                    if (totalEpisodecount == null)
                    {
                        var episodes = await ListEpisodesBySeasonAsync(seasonResultDto.Id);
                        totalEpisodecount = episodes.Count;
                    }
                    seasons.Add(new Season
                    {
                        Id = seasonResultDto.Id,
                        Name = seasonResultDto.Name,
                        Number = seasonResultDto.Number,
                        TotalEpisodeCount = totalEpisodecount.Value
                    });
                }

                return seasons;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get list of seasons with the show id {showId}.");
            }
            return new List<Season>();
        }

        public async Task<List<Episode>> ListEpisodesBySeasonAsync(long seasonId)
        {
            if (seasonId <= 0)
            {
                _logger.LogWarning($"Invalid season id {seasonId}");
                return new List<Episode>();
            }

            try
            {
                var response = await _tvMazeDataAccess.ListEpisodesBySeasonIdAsync(seasonId);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var episodesDto = JsonSerializer.Deserialize<List<EpisodeDto>>(content) ?? new List<EpisodeDto>();

                var episodes = new List<Episode>();
                foreach (var episodeDto in episodesDto)
                {
                    episodes.Add(new Episode
                    {
                        Id = episodeDto.Id,
                        Name = episodeDto.Name,
                        EpisodeNumber = episodeDto.Number,
                        Season = episodeDto.Season,
                        Type = episodeDto.Type,
                        RunTime = episodeDto.Runtime
                    });
                }

                return episodes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get list of episodes with the season id {seasonId}.");
            }

            return new List<Episode>();
        }
}