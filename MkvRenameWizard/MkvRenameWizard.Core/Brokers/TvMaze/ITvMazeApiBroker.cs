using MkvRenameWizard.Core.Models.TvMaze.Dto;

namespace MkvRenameWizard.Core.Brokers.TvMaze;

internal interface ITvMazeApiBroker
{
    Task<List<ShowSearchResultDto>?> SearchShowsAsync(string query, CancellationToken cancellationToken = default);
    Task<List<ListSeasonResultDto>?> GetSeasonsAsync(long showId, CancellationToken cancellationToken = default);
    Task<List<EpisodeDto>?> GetEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default);
}
