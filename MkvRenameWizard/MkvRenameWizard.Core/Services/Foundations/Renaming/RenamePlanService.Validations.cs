using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.Renaming.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal sealed partial class RenamePlanService
{
    private static void ValidatePatternInput(string pattern)
    {
        if (pattern is null)
        {
            throw new InvalidRenamePlanRequestException("Pattern cannot be null.");
        }
    }

    private static void ValidatePlanRequest(RenamePlanRequest request)
    {
        if (request is null)
        {
            throw new InvalidRenamePlanRequestException("Rename plan request is required.");
        }

        if (request.Pattern is null)
        {
            throw new InvalidRenamePlanRequestException("Pattern cannot be null.");
        }

        if (request.Candidates is null)
        {
            throw new InvalidRenamePlanRequestException("Candidates list cannot be null.");
        }
    }
}
