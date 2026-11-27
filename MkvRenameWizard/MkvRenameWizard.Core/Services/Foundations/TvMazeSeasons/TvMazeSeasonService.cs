using System.Diagnostics;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Brokers.TvMaze;
using MkvRenameWizard.Core.Diagnostics;
using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeSeasons;

internal sealed partial class TvMazeSeasonService(
    ITvMazeApiBroker tvMazeApiBroker,
    ILoggingBroker loggingBroker) : ITvMazeSeasonService
{
    public Task<IReadOnlyList<Season>> RetrieveSeasonsByShowIdAsync(long showId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(async () =>
        {
            ValidateShowId(showId);

            using var activity = CoreTelemetry.ActivitySource.StartActivity("TvMaze.RetrieveSeasons");
            var start = Stopwatch.GetTimestamp();
            var seasonDtoList = await tvMazeApiBroker.GetSeasonsAsync(showId, cancellationToken);
            CoreTelemetry.TvMazeRequestDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);

            if (seasonDtoList is null || seasonDtoList.Count == 0)
            {
                return Array.Empty<Season>();
            }

            var seasons = new List<Season>(seasonDtoList.Count);
            foreach (var dto in seasonDtoList)
            {
                seasons.Add(new Season
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    Number = dto.Number,
                    TotalEpisodeCount = dto.EpisodeOrder ?? 0,
                    HasEpisodeOrder = dto.EpisodeOrder.HasValue
                });
            }

            return (IReadOnlyList<Season>)seasons;
        });
}
