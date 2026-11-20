using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.FileTypes;
using MkvRenameWizard.Models.FileImport;
using MkvRenameWizard.Models.Mkv;

namespace MkvRenameWizard.Services;

public class MkvFinderService : IMkvFinderService
{
    private readonly ILogger<MkvFinderService> _logger;

    public MkvFinderService(ILogger<MkvFinderService> logger)
    {
        _logger = logger;
    }

    public async Task<FileImportResult> OpenMkvFoldersAsync(TopLevel topLevel)
    {
        var importedFiles = new List<MkvFile>();
        var issues = new List<FileImportIssue>();
        var selectedFolders = new List<string>();
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = true
        });

        foreach (var folder in folders)
        {
            selectedFolders.Add(folder.Name);
            var isImportedItemFound = false;
            var isFolderHavePermissionDenied = false;

            try
            {
                await foreach (var storageItem in folder.GetItemsAsync())
                {
                    if (storageItem is not IStorageFile storageFile || !storageItem.Path.IsFile)
                    {
                        issues.Add(new FileImportIssue(storageItem.Name,
                            FileImportIssueType.NoSupportedFilesFound.ToString("G"),
                            FileImportIssueType.NoSupportedFilesFound));
                        continue;
                    }

                    var (issue, sizeInBytes) = await ValidateMkvFileAsync(storageFile);
                    if (issue != null)
                    {
                        issues.Add(issue);
                        continue;
                    }

                    isImportedItemFound = true;
                    var localPath = storageFile.Path.LocalPath;
                    var parentDir = Path.GetFileName(Path.GetDirectoryName(localPath.AsSpan()));
                    importedFiles.Add(new MkvFile(
                        parentDir.IsEmpty ? string.Empty : new string(parentDir),
                        localPath,
                        sizeInBytes ?? TryGetFileSize(localPath),
                        true));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                isFolderHavePermissionDenied = true;
                _logger.LogError(ex, "An Unauthorized Access Exception occurred while validating the file");
                issues.Add(new FileImportIssue(folder.Name, ex.Message, FileImportIssueType.PermissionDenied));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while opening the file");
                issues.Add(new FileImportIssue(folder.Name, ex.Message, FileImportIssueType.Unknown));
            }

            if (!isImportedItemFound && selectedFolders.Count > 0 && !isFolderHavePermissionDenied)
            {
                var hasFolderSpecificIssue = false;
                for (var i = 0; i < issues.Count; i++)
                {
                    if (issues[i].DisplayName == folder.Name)
                    {
                        hasFolderSpecificIssue = true;
                        break;
                    }
                }

                if (!hasFolderSpecificIssue)
                {
                    issues.Add(new FileImportIssue(folder.Name, "No MKV files found", FileImportIssueType.InvalidContainer));
                }
            }
        }

        importedFiles.Sort(static (a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(a.FullPath, b.FullPath));
        return new FileImportResult(importedFiles.ToArray(), issues, selectedFolders);
    }

    public async Task<FileImportResult> OpenMkvFilesAsync(TopLevel topLevel)
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select MKV files",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("mkv") { Patterns = ["*.mkv"] }]
        });

        if (files.Count == 0)
        {
            return new FileImportResult(ImportedFiles: [], FileImportIssues: [], SelectedFolders: []);
        }

        var importedFiles = new List<MkvFile>(files.Count);
        var issues = new List<FileImportIssue>();

        foreach (var file in files)
        {
            var localFilePath = file.Path.LocalPath;
            var ext = Path.GetExtension(localFilePath.AsSpan());

            if (!ext.Equals(".mkv", StringComparison.InvariantCultureIgnoreCase))
            {
                issues.Add(new FileImportIssue(file.Name, "Not a .mkv file", FileImportIssueType.UnsupportedFileType));
                continue;
            }

            var (issue, sizeInBytes) = await ValidateMkvFileAsync(file);
            if (issue != null)
            {
                issues.Add(issue);
                continue;
            }

            var parentDir = Path.GetFileName(Path.GetDirectoryName(localFilePath.AsSpan()));
            importedFiles.Add(new MkvFile(
                parentDir.IsEmpty ? string.Empty : new string(parentDir),
                localFilePath,
                sizeInBytes ?? TryGetFileSize(localFilePath),
                true));
        }

        importedFiles.Sort(static (a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(a.FullPath, b.FullPath));
        return new FileImportResult(importedFiles.ToArray(), issues, SelectedFolders: []);
    }

    public Task<FileImportResult> ImportFromPathsAsync(IReadOnlyList<string> paths)
    {
        var importedFiles = new List<MkvFile>(paths.Count);
        var issues = new List<FileImportIssue>();

        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var isImportedFileFound = false;
                    var directoryName = Path.GetFileName(path);
                    foreach (var filePath in Directory.EnumerateFiles(path, "*.mkv", SearchOption.AllDirectories))
                    {
                        var (issue, sizeInBytes) = ValidateMkvFile(filePath);
                        if (issue != null)
                        {
                            issues.Add(issue);
                            continue;
                        }

                        isImportedFileFound = true;
                        importedFiles.Add(new MkvFile(directoryName, filePath, sizeInBytes, true));
                    }

                    if (!isImportedFileFound)
                    {
                        issues.Add(new FileImportIssue(path, "No *.mkv files found", FileImportIssueType.FileNotFound));
                    }
                }
                else if (File.Exists(path))
                {
                    var (issue, sizeInBytes) = ValidateMkvFile(path);
                    if (issue != null)
                    {
                        issues.Add(issue);
                        continue;
                    }

                    importedFiles.Add(new MkvFile(path, path, sizeInBytes, true));
                }
                else
                {
                    issues.Add(new FileImportIssue(path, "file not found", FileImportIssueType.FileNotFound));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "An {ExceptionType} exception occurred while importing {Path}", nameof(UnauthorizedAccessException), path);
                issues.Add(new FileImportIssue(path, ex.Message, FileImportIssueType.PermissionDenied));
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "An {ExceptionType} exception occurred while importing {Path}", nameof(IOException), path);
                issues.Add(new FileImportIssue(path, ex.Message, FileImportIssueType.Unknown));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while importing {Path}", path);
                issues.Add(new FileImportIssue(path, ex.Message, FileImportIssueType.Unknown));
            }
        }

        importedFiles.Sort(static (a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(a.FullPath, b.FullPath));
        return Task.FromResult(new FileImportResult(importedFiles.ToArray(), issues, SelectedFolders: []));
    }

    private (FileImportIssue? Issue, long? SizeInBytes) ValidateMkvFile(string filePath)
    {
        try
        {
            using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[4];
            var bytesRead = RandomAccess.Read(handle, header, 0);

            if (bytesRead < 4 || !MatroskaVideo.HasValidHeader(header))
            {
                _logger.LogDebug("Unsupported file type: {FileType}", filePath);
                return (new FileImportIssue(filePath, "Unsupported Video File", FileImportIssueType.UnsupportedFileType), null);
            }

            return (null, RandomAccess.GetLength(handle));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file");
            return (new FileImportIssue(filePath, ex.Message, FileImportIssueType.PermissionDenied), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file");
            return (new FileImportIssue(filePath, "Not a mkv container", FileImportIssueType.InvalidContainer), null);
        }
    }

    private async Task<(FileImportIssue? Issue, long? SizeInBytes)> ValidateMkvFileAsync(IStorageFile storageFile)
    {
        try
        {
            await using var fileStream = await storageFile.OpenReadAsync();
            var header = new byte[4];
            var totalRead = 0;
            while (totalRead < 4)
            {
                var read = await fileStream.ReadAsync(header.AsMemory(totalRead, 4 - totalRead));
                if (read == 0) break;
                totalRead += read;
            }

            if (totalRead < 4 || !MatroskaVideo.HasValidHeader(header))
            {
                _logger.LogDebug("Unsupported file type: {FileType}", storageFile.Path);
                return (new FileImportIssue(storageFile.Name, "Unsupported Video File", FileImportIssueType.UnsupportedFileType), null);
            }

            long? length = fileStream.CanSeek ? fileStream.Length : null;
            return (null, length);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file");
            return (new FileImportIssue(storageFile.Name, ex.Message, FileImportIssueType.PermissionDenied), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file");
            return (new FileImportIssue(storageFile.Name, "Not a mkv container", FileImportIssueType.InvalidContainer), null);
        }
    }

    private long? TryGetFileSize(string fullPath)
    {
        try
        {
            var fileInfo = new FileInfo(fullPath);
            return fileInfo.Exists ? fileInfo.Length : null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while trying to get file size");
            return null;
        }
    }
}
