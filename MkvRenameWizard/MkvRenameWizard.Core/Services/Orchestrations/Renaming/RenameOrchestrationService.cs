using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Services.Foundations.Renaming;

namespace MkvRenameWizard.Core.Services.Orchestrations.Renaming;

internal sealed partial class RenameOrchestrationService(
    IRenamePlanService renamePlanService,
    IFileRenameService fileRenameService,
    ILoggingBroker loggingBroker) : IRenameOrchestrationService
{
    public IReadOnlyList<PatternToken> RetrieveAvailableTokens() =>
        TryCatch(renamePlanService.RetrieveAvailableTokens);

    public PatternValidationResult ValidatePattern(string pattern) =>
        TryCatch(() => renamePlanService.ValidatePattern(pattern));

    public RenamePlan BuildRenamePlan(RenamePlanRequest request) =>
        TryCatch(() => renamePlanService.BuildPlan(request));

    public IReadOnlyList<RenameOperationResult> ExecuteRenameOperations(IReadOnlyList<RenameFileOperation> operations) =>
        TryCatch(() => fileRenameService.ExecuteRenameOperations(operations));

    public IReadOnlyList<RenameOperationResult> UndoRenameOperations(IReadOnlyList<RenameOperationResult> previousResults) =>
        TryCatch(() =>
        {
            if (previousResults is null)
            {
                return fileRenameService.ExecuteRenameOperations(null!);
            }

            var undoOperations = new List<RenameFileOperation>(previousResults.Count);
            for (var i = previousResults.Count - 1; i >= 0; i--)
            {
                var result = previousResults[i];
                if (result.IsSuccessful &&
                    !string.IsNullOrWhiteSpace(result.SourcePath) &&
                    !string.IsNullOrWhiteSpace(result.TargetPath))
                {
                    undoOperations.Add(new RenameFileOperation(
                        result.OperationId,
                        SourcePath: result.TargetPath,
                        TargetPath: result.SourcePath));
                }
            }

            return fileRenameService.ExecuteRenameOperations(undoOperations);
        });
}
