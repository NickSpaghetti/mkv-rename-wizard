using System.Collections.Generic;
using MkvRenameWizard.Models.Renaming;

namespace MkvRenameWizard.Services;

public interface IFileRenameOperationService : IRenameService<RenameFileOperation>
{
    List<RenameOperationResult<RenameFileOperation>> Undo(
        IReadOnlyList<RenameOperationResult<RenameFileOperation>> previousResults);
}
