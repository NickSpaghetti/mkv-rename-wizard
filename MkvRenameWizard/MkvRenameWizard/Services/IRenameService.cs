using System.Collections.Generic;
using MkvRenameWizard.Models.Renaming;

namespace MkvRenameWizard.Services;

public interface IRenameService<T> where T : IRenameOperation
{
    List<RenameOperationResult<T>> Execute(IReadOnlyList<T> operations);
}
