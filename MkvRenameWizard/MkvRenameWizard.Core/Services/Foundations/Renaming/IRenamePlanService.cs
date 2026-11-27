using MkvRenameWizard.Core.Models.Renaming;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal interface IRenamePlanService
{
    IReadOnlyList<PatternToken> RetrieveAvailableTokens();
    PatternValidationResult ValidatePattern(string pattern);
    RenamePlan BuildPlan(RenamePlanRequest request);
}
