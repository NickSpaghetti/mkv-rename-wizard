using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.Renaming.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal sealed partial class FileRenameService
{
    private static void ValidateOperations(IReadOnlyList<RenameFileOperation> operations)
    {
        if (operations is null)
        {
            throw new InvalidRenameOperationException("Rename operations list is required.");
        }
    }
}
