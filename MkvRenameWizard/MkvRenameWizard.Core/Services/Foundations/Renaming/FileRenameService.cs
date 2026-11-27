using System.Diagnostics;
using MkvRenameWizard.Core.Brokers.FileSystems;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Diagnostics;
using MkvRenameWizard.Core.Models.Renaming;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal sealed partial class FileRenameService(
    IFileSystemBroker fileSystemBroker,
    ILoggingBroker loggingBroker) : IFileRenameService
{
    public IReadOnlyList<RenameOperationResult> ExecuteRenameOperations(IReadOnlyList<RenameFileOperation> operations) =>
        TryCatch(() =>
        {
            ValidateOperations(operations);

            using var activity = CoreTelemetry.ActivitySource.StartActivity("Renaming.Execute");
            var start = Stopwatch.GetTimestamp();

            var results = new List<RenameOperationResult>(operations.Count);
            string? lastCreatedDir = null;
            HashSet<string>? createdDirs = null;

            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                try
                {
                    if (string.IsNullOrWhiteSpace(operation.SourcePath))
                    {
                        results.Add(new RenameOperationResult(
                            operation.OperationId,
                            operation.SourcePath,
                            operation.TargetPath,
                            IsSuccessful: false,
                            ErrorMessage: "Source path is empty."));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(operation.TargetPath))
                    {
                        results.Add(new RenameOperationResult(
                            operation.OperationId,
                            operation.SourcePath,
                            operation.TargetPath,
                            IsSuccessful: false,
                            ErrorMessage: "Target path is empty."));
                        continue;
                    }

                    var targetDir = Path.GetDirectoryName(operation.TargetPath);
                    if (!string.IsNullOrEmpty(targetDir) &&
                        !string.Equals(targetDir, lastCreatedDir, StringComparison.Ordinal))
                    {
                        createdDirs ??= new HashSet<string>(StringComparer.Ordinal);
                        if (createdDirs.Add(targetDir))
                        {
                            fileSystemBroker.CreateDirectory(targetDir);
                        }

                        lastCreatedDir = targetDir;
                    }

                    fileSystemBroker.MoveFile(operation.SourcePath, operation.TargetPath, overwrite: false);

                    results.Add(new RenameOperationResult(
                        operation.OperationId,
                        operation.SourcePath,
                        operation.TargetPath,
                        IsSuccessful: true,
                        ErrorMessage: string.Empty));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    loggingBroker.LogError(ex);
                    results.Add(new RenameOperationResult(
                        operation.OperationId,
                        operation.SourcePath,
                        operation.TargetPath,
                        IsSuccessful: false,
                        ErrorMessage: ex.Message));
                }
            }

            CoreTelemetry.RenameExecuteDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            CoreTelemetry.RenameFilesExecuted.Add(operations.Count);

            return (IReadOnlyList<RenameOperationResult>)results;
        });
}
