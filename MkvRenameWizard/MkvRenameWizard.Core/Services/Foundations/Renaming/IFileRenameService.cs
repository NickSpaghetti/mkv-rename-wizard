using MkvRenameWizard.Core.Models.Renaming;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal interface IFileRenameService
{
    IReadOnlyList<RenameOperationResult> ExecuteRenameOperations(IReadOnlyList<RenameFileOperation> operations);
}
