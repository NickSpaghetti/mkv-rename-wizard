using System.Collections.Generic;
using System.Threading.Tasks;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.Services;

public interface ITvMazeService
{
    Task<List<Show>> FindShowIdByNameAsync(string showName);
    Task<List<Season>> ListSeasonsAsync(long showId);
    Task<List<Episode>> ListEpisodesBySeasonAsync(long seasonId);
}