using System.Diagnostics;
using System.Globalization;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Brokers.TvMaze;
using MkvRenameWizard.Core.Diagnostics;
using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeEpisodes;

internal sealed partial class TvMazeEpisodeService(
    ITvMazeApiBroker tvMazeApiBroker,
    ILoggingBroker loggingBroker) : ITvMazeEpisodeService
{
    public Task<IReadOnlyList<Episode>> RetrieveEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(async () =>
        {
            ValidateSeasonId(seasonId);

            using var activity = CoreTelemetry.ActivitySource.StartActivity("TvMaze.RetrieveEpisodes");
            var start = Stopwatch.GetTimestamp();
            var episodesDto = await tvMazeApiBroker.GetEpisodesBySeasonIdAsync(seasonId, cancellationToken);
            CoreTelemetry.TvMazeRequestDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);

            if (episodesDto is null || episodesDto.Count == 0)
            {
                return Array.Empty<Episode>();
            }

            var episodes = new List<Episode>(episodesDto.Count);
            foreach (var dto in episodesDto)
            {
                episodes.Add(new Episode
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    EpisodeNumber = dto.Number,
                    Season = dto.Season,
                    Type = dto.Type,
                    RunTime = dto.Runtime,
                    AirDate = DateOnly.TryParse(dto.Airdate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        ? date
                        : null
                });
            }

            return (IReadOnlyList<Episode>)episodes;
        });
}
