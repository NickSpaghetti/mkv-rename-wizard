using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Renaming;

namespace MkvRenameWizard.Services;

public class FileRenameOperationService(ILogger<FileRenameOperationService> logger) : IFileRenameOperationService
{
    public List<RenameOperationResult<RenameFileOperation>> Execute(IReadOnlyList<RenameFileOperation> operations)
    {
        var results = new List<RenameOperationResult<RenameFileOperation>>(operations.Count);
        string? lastCreatedDir = null;
        HashSet<string>? createdDirs = null;

        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            try
            {
                if (string.IsNullOrEmpty(operation.TargetPath))
                {
                    results.Add(new RenameOperationResult<RenameFileOperation>(
                        operation, IsSuccessful: false, "Target path is empty."));
                    continue;
                }

                var targetDir = Path.GetDirectoryName(operation.TargetPath);
                if (!string.IsNullOrEmpty(targetDir) &&
                    !string.Equals(targetDir, lastCreatedDir, StringComparison.Ordinal))
                {
                    createdDirs ??= new HashSet<string>(StringComparer.Ordinal);
                    if (createdDirs.Add(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    lastCreatedDir = targetDir;
                }

                File.Move(operation.SourcePath, operation.TargetPath, overwrite: false);

                results.Add(new RenameOperationResult<RenameFileOperation>(
                    operation, IsSuccessful: true, string.Empty));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to rename {SourcePath} to {TargetPath}",
                    operation.SourcePath, operation.TargetPath);
                results.Add(new RenameOperationResult<RenameFileOperation>(
                    operation, IsSuccessful: false, ex.Message));
            }
        }

        return results;
    }
}
