using MkvRenameWizard.Core.Models.Renaming;

namespace MkvRenameWizard.Core.Services.Orchestrations.Renaming;

internal interface IRenameOrchestrationService
{
    IReadOnlyList<PatternToken> RetrieveAvailableTokens();
    PatternValidationResult ValidatePattern(string pattern);
    RenamePlan BuildRenamePlan(RenamePlanRequest request);
    IReadOnlyList<RenameOperationResult> ExecuteRenameOperations(IReadOnlyList<RenameFileOperation> operations);
    IReadOnlyList<RenameOperationResult> UndoRenameOperations(IReadOnlyList<RenameOperationResult> previousResults);
}
