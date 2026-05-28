using System.Collections.Generic;
using System.Threading.Tasks;
using MkvRenameWizard.Models.Renaming;

namespace MkvRenameWizard.Services;

public interface IRenameService<T> where T : IRenameOperation
{
    Task<List<RenameOperationResult<T>>> ExecuteAsync(IEnumerable<T> operations);
}