using System.Collections.Generic;
using MkvRenameWizard.Core.Services.Aggregations;
using MkvRenameWizard.Models.Renaming;
using CoreRenameFileOperation = MkvRenameWizard.Core.Models.Renaming.RenameFileOperation;
using CoreRenameOperationResult = MkvRenameWizard.Core.Models.Renaming.RenameOperationResult;

namespace MkvRenameWizard.Services;

public class FileRenameOperationService(IMkvRenameWizardService coreService) : IFileRenameOperationService
{
    public List<RenameOperationResult<RenameFileOperation>> Execute(IReadOnlyList<RenameFileOperation> operations)
    {
        var coreOps = new CoreRenameFileOperation[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            coreOps[i] = new CoreRenameFileOperation(
                operations[i].OperationId,
                operations[i].SourcePath,
                operations[i].TargetPath);
        }

        var coreResults = coreService.ExecuteRenameOperations(coreOps);
        var results = new List<RenameOperationResult<RenameFileOperation>>(coreResults.Count);
        for (var i = 0; i < coreResults.Count; i++)
        {
            var r = coreResults[i];
            results.Add(new RenameOperationResult<RenameFileOperation>(
                operations[i],
                r.IsSuccessful,
                r.ErrorMessage));
        }

        return results;
    }

    public List<RenameOperationResult<RenameFileOperation>> Undo(
        IReadOnlyList<RenameOperationResult<RenameFileOperation>> previousResults)
    {
        var corePrevious = new CoreRenameOperationResult[previousResults.Count];
        for (var i = 0; i < previousResults.Count; i++)
        {
            var r = previousResults[i];
            corePrevious[i] = new CoreRenameOperationResult(
                r.RenameOperation.OperationId,
                r.RenameOperation.SourcePath,
                r.RenameOperation.TargetPath,
                r.IsSuccessful,
                r.ErrorMessage);
        }

        var coreUndoResults = coreService.UndoRenameOperations(corePrevious);
        var results = new List<RenameOperationResult<RenameFileOperation>>(coreUndoResults.Count);
        for (var i = 0; i < coreUndoResults.Count; i++)
        {
            var r = coreUndoResults[i];
            results.Add(new RenameOperationResult<RenameFileOperation>(
                new RenameFileOperation(r.OperationId, r.SourcePath, r.TargetPath),
                r.IsSuccessful,
                r.ErrorMessage));
        }

        return results;
    }
}
