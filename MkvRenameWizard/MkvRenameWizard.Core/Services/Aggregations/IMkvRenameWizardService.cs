using MkvRenameWizard.Core.Models.MkvFiles;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Aggregations;

public interface IMkvRenameWizardService
{
    Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Season>> RetrieveSeasonsByShowIdAsync(long showId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Episode>> RetrieveEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default);
    FileImportResult ImportMkvFilesFromPaths(IReadOnlyList<string> paths);
    IReadOnlyList<PatternToken> RetrieveAvailableTokens();
    PatternValidationResult ValidatePattern(string pattern);
    RenamePlan BuildRenamePlan(RenamePlanRequest request);
    IReadOnlyList<RenameOperationResult> ExecuteRenameOperations(IReadOnlyList<RenameFileOperation> operations);
    IReadOnlyList<RenameOperationResult> UndoRenameOperations(IReadOnlyList<RenameOperationResult> previousResults);
}
