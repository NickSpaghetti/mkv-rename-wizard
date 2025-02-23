using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Models.TvMaze.Dto;

namespace MkvRenameWizard.Services;

public class TvMazeService : ITvMazeService
    {
        private readonly ITvMazeDataAccess _tvMazeDataAccess;

        public TvMazeService(ITvMazeDataAccess tvMazeDataAccess)
        {
            _tvMazeDataAccess = tvMazeDataAccess;
        }

        public async Task<List<Show>> FindShowIdByNameAsync(string showName)
        {
            if (string.IsNullOrEmpty(showName))
            {
                Console.WriteLine("Cannot search for an empty show");
                Environment.Exit(-1);
            }

            try
            {
                var response = await _tvMazeDataAccess.FindShowIdByNameAsync(showName);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var tvShowResultsDtoList = JsonSerializer.Deserialize<List<ShowSearchResultDto>>(content);

                var tvShowResults = new List<Show>();
                foreach (var showResultDto in tvShowResultsDtoList)
                {
                    tvShowResults.Add(new Show
                    {
                        Id = showResultDto.ShowDto.Id,
                        Name = showResultDto.ShowDto.Name,
                        Language = showResultDto.ShowDto.Language,
                        Genres = showResultDto.ShowDto.Genres
                    });
                }

                return tvShowResults;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get list of TV shows with the name {showName}.");
                Console.WriteLine(ex.Message);
                Environment.Exit(-1);
            }
            return null;
        }

        public async Task<List<Season>> ListSeasonsAsync(long showId)
        {
            if (showId <= 0)
            {
                Console.WriteLine($"Invalid show id {showId}");
                Environment.Exit(-1);
            }

            try
            {
                var response = await _tvMazeDataAccess.ListSeasonsAsync(showId);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var tvShowResultsDtoList = JsonSerializer.Deserialize<List<ListSeasonResultDto>>(content);

                var seasons = new List<Season>();
                foreach (var seasonResultDto in tvShowResultsDtoList)
                {
                    seasons.Add(new Season
                    {
                        Id = seasonResultDto.Id,
                        Name = seasonResultDto.Name,
                        Number = seasonResultDto.Number,
                        TotalEpisodeCount = seasonResultDto.EpisodeOrder
                    });
                }

                return seasons;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get list of seasons with the id {showId}.");
                Console.WriteLine(ex.Message);
                Environment.Exit(-1);
            }
            return null;
        }

        public async Task<List<Episode>> ListEpisodesBySeasonAsync(long seasonId)
        {
            if (seasonId <= 0)
            {
                Console.WriteLine($"Invalid season id {seasonId}");
                Environment.Exit(-1);
            }

            try
            {
                var response = await _tvMazeDataAccess.ListEpisodesBySeasonIdAsync(seasonId);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var episodesDto = JsonSerializer.Deserialize<List<EpisodeDto>>(content);

                var episodes = new List<Episode>();
                foreach (var episodeDto in episodesDto)
                {
                    episodes.Add(new Episode
                    {
                        Id = episodeDto.Id,
                        Name = episodeDto.Name,
                        EpisodeNumber = episodeDto.Number,
                        Season = episodeDto.Season,
                        Type = episodeDto.Type
                    });
                }

                return episodes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get list of episodes with the season id {seasonId}.");
                Console.WriteLine(ex.Message);
                Environment.Exit(-1);
            }

            return null;
        }
    }