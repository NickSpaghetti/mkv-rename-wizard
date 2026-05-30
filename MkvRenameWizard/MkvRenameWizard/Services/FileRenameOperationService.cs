using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Renaming;

namespace MkvRenameWizard.Services;

public class FileRenameOperationService(ILogger<FileRenameOperationService> logger) : IFileRenameOperationService
{
    public Task<List<RenameOperationResult<RenameFileOperation>>> ExecuteAsync(IEnumerable<RenameFileOperation> operations)
    {
        var results = new List<RenameOperationResult<RenameFileOperation>>();

        foreach (var operation in operations)
        {
            try
            {
                var targetDir = Path.GetDirectoryName(operation.TargetPath);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Move(operation.SourcePath, operation.TargetPath, overwrite: false);

                results.Add(
                    new RenameOperationResult<RenameFileOperation>(operation, IsSuccessful: true, string.Empty));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                results.Add(new RenameOperationResult<RenameFileOperation>(operation, IsSuccessful: false, ex.Message));
            }
        }

        return Task.FromResult(results);
    }
}