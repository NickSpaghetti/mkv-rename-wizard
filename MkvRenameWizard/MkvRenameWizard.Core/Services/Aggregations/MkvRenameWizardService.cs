using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.MkvFiles;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.TvMaze;
using MkvRenameWizard.Core.Services.Coordinations.TvMaze;
using MkvRenameWizard.Core.Services.Foundations.MkvFiles;
using MkvRenameWizard.Core.Services.Orchestrations.Renaming;

namespace MkvRenameWizard.Core.Services.Aggregations;

internal sealed partial class MkvRenameWizardService(
    ITvMazeCoordinationService tvMazeCoordinationService,
    IMkvFileService mkvFileService,
    IRenameOrchestrationService renameOrchestrationService,
    ILoggingBroker loggingBroker) : IMkvRenameWizardService
{
    public Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default) =>
        TryCatchAsync(() => tvMazeCoordinationService.SearchShowsByNameAsync(showName, cancellationToken));

    public Task<IReadOnlyList<Season>> RetrieveSeasonsByShowIdAsync(long showId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(() => tvMazeCoordinationService.RetrieveSeasonsByShowIdAsync(showId, cancellationToken));

    public Task<IReadOnlyList<Episode>> RetrieveEpisodesBySeasonIdAsync(long seasonId, CancellationToken cancellationToken = default) =>
        TryCatchAsync(() => tvMazeCoordinationService.RetrieveEpisodesBySeasonIdAsync(seasonId, cancellationToken));

    public FileImportResult ImportMkvFilesFromPaths(IReadOnlyList<string> paths) =>
        TryCatch(() => mkvFileService.ImportFromPaths(paths));

    public IReadOnlyList<PatternToken> RetrieveAvailableTokens() =>
        TryCatch(renameOrchestrationService.RetrieveAvailableTokens);

    public PatternValidationResult ValidatePattern(string pattern) =>
        TryCatch(() => renameOrchestrationService.ValidatePattern(pattern));

    public RenamePlan BuildRenamePlan(RenamePlanRequest request) =>
        TryCatch(() => renameOrchestrationService.BuildRenamePlan(request));

    public IReadOnlyList<RenameOperationResult> ExecuteRenameOperations(IReadOnlyList<RenameFileOperation> operations) =>
        TryCatch(() => renameOrchestrationService.ExecuteRenameOperations(operations));

    public IReadOnlyList<RenameOperationResult> UndoRenameOperations(IReadOnlyList<RenameOperationResult> previousResults) =>
        TryCatch(() => renameOrchestrationService.UndoRenameOperations(previousResults));
}
