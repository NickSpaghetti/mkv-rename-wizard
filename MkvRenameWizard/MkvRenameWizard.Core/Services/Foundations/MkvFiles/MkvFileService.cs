using System.Diagnostics;
using MkvRenameWizard.Core.Brokers.FileSystems;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Diagnostics;
using MkvRenameWizard.Core.Models.FileSystems;
using MkvRenameWizard.Core.Models.MkvFiles;

namespace MkvRenameWizard.Core.Services.Foundations.MkvFiles;

internal sealed partial class MkvFileService(
    IFileSystemBroker fileSystemBroker,
    ILoggingBroker loggingBroker) : IMkvFileService
{
    private const byte EbmlByte0 = 0x1A;
    private const byte EbmlByte1 = 0x45;
    private const byte EbmlByte2 = 0xDF;
    private const byte EbmlByte3 = 0xA3;

    public FileImportResult ImportFromPaths(IReadOnlyList<string> paths) =>
        TryCatch(() =>
        {
            ValidatePaths(paths);

            using var activity = CoreTelemetry.ActivitySource.StartActivity("MkvFiles.ImportFromPaths");
            var start = Stopwatch.GetTimestamp();

            var importedFiles = new List<MkvFile>(paths.Count);
            var issues = new List<FileImportIssue>();
            var selectedFolders = new List<string>();

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    issues.Add(CreateIssue(path, "Path is empty", FileImportIssueType.FileNotFound));
                    continue;
                }

                try
                {
                    if (fileSystemBroker.DirectoryExists(path))
                    {
                        var directoryName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (!string.IsNullOrEmpty(directoryName))
                        {
                            selectedFolders.Add(directoryName);
                        }

                        var isImportedFileFound = false;
                        foreach (var filePath in fileSystemBroker.EnumerateFiles(path, "*.mkv", SearchOption.AllDirectories))
                        {
                            if (!TryValidateMkvContainer(filePath, out var sizeInBytes, out var issue))
                            {
                                issues.Add(issue!);
                                continue;
                            }

                            isImportedFileFound = true;
                            var parentDir = Path.GetFileName(Path.GetDirectoryName(filePath.AsSpan()));
                            var rootName = parentDir.IsEmpty ? directoryName : new string(parentDir);
                            importedFiles.Add(new MkvFile(rootName, filePath, sizeInBytes, IsIncluded: true));
                        }

                        if (!isImportedFileFound)
                        {
                            issues.Add(CreateIssue(path, "No *.mkv files found", FileImportIssueType.FileNotFound));
                        }
                    }
                    else if (fileSystemBroker.FileExists(path))
                    {
                        var ext = Path.GetExtension(path.AsSpan());
                        if (!ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(CreateIssue(path, "Not a .mkv file", FileImportIssueType.UnsupportedFileType));
                            continue;
                        }

                        if (!TryValidateMkvContainer(path, out var sizeInBytes, out var issue))
                        {
                            issues.Add(issue!);
                            continue;
                        }

                        var parentDir = Path.GetFileName(Path.GetDirectoryName(path.AsSpan()));
                        var rootName = parentDir.IsEmpty ? string.Empty : new string(parentDir);
                        importedFiles.Add(new MkvFile(rootName, path, sizeInBytes, IsIncluded: true));
                    }
                    else
                    {
                        issues.Add(CreateIssue(path, "File or folder not found", FileImportIssueType.FileNotFound));
                    }
                }
                catch (UnauthorizedAccessException unauthorizedAccessException)
                {
                    loggingBroker.LogError(unauthorizedAccessException);
                    issues.Add(CreateIssue(path, unauthorizedAccessException.Message, FileImportIssueType.PermissionDenied));
                }
                catch (IOException ioException)
                {
                    loggingBroker.LogError(ioException);
                    issues.Add(CreateIssue(path, ioException.Message, FileImportIssueType.Unknown));
                }
            }

            importedFiles.Sort(static (a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(a.FullPath, b.FullPath));

            CoreTelemetry.MkvScanDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            CoreTelemetry.MkvFilesScanned.Add(importedFiles.Count);

            return new FileImportResult(importedFiles, issues, selectedFolders);
        });

    private bool TryValidateMkvContainer(string filePath, out long? sizeInBytes, out FileImportIssue? issue)
    {
        try
        {
            var header = fileSystemBroker.ReadFileHeaderAndLength(filePath);
            if (!HasValidEbmlHeader(header))
            {
                loggingBroker.LogDebug($"Unsupported video container header: {filePath}");
                sizeInBytes = null;
                issue = CreateIssue(filePath, "Unsupported Video File", FileImportIssueType.UnsupportedFileType);
                return false;
            }

            sizeInBytes = header.SizeInBytes;
            issue = null;
            return true;
        }
        catch (UnauthorizedAccessException unauthorizedAccessException)
        {
            loggingBroker.LogError(unauthorizedAccessException);
            sizeInBytes = null;
            issue = CreateIssue(filePath, unauthorizedAccessException.Message, FileImportIssueType.PermissionDenied);
            return false;
        }
        catch (IOException ioException)
        {
            loggingBroker.LogError(ioException);
            sizeInBytes = null;
            issue = CreateIssue(filePath, "Not a valid MKV container", FileImportIssueType.InvalidContainer);
            return false;
        }
    }

    private static bool HasValidEbmlHeader(FileHeaderInfo header) =>
        header.BytesRead >= 4
        && header.Byte0 == EbmlByte0
        && header.Byte1 == EbmlByte1
        && header.Byte2 == EbmlByte2
        && header.Byte3 == EbmlByte3;

    private static FileImportIssue CreateIssue(string? displayName, string? reason, FileImportIssueType issueType) =>
        new(
            string.IsNullOrWhiteSpace(displayName) ? FileImportIssueType.Unknown.ToString("G") : displayName,
            string.IsNullOrWhiteSpace(reason) ? FileImportIssueType.Unknown.ToString("G") : reason,
            issueType);
}
