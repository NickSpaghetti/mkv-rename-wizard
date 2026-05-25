using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

using Avalonia.Platform.Storage;
using FileTypeChecker;
using FileTypeChecker.Extensions;
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
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
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
                    
                    var issue = await ValidateMkvFileAsync(storageFile);
                    if (issue != null)
                    {
                        issues.Add(issue);
                        continue;
                    }
                    
                    isImportedItemFound = true;
                    importedFiles.Add(CreateImportedFile(Path.GetFileName(Path.GetDirectoryName(storageFile.Path.LocalPath)) ?? string.Empty
                        ,storageFile.Path.LocalPath));
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
                var hasFolderSpecificIssue = issues.Any(issue => issue.DisplayName == folder.Name);
                if (!hasFolderSpecificIssue)
                {
                    issues.Add(new FileImportIssue(folder.Name, "No MKV files found", FileImportIssueType.InvalidContainer));
                }
            }

        }
        var sortedImportFiles = importedFiles
            .OrderBy(file => file.FullPath, StringComparer.InvariantCultureIgnoreCase)
            .ToArray();
        return new FileImportResult(sortedImportFiles, issues.ToList(),selectedFolders.ToList());
    }

    public async Task<FileImportResult> OpenMkvFilesAsync(TopLevel topLevel)
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Select MKV files",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("mkv") {Patterns = ["*.mkv"]}]
        });

        if (files.Count == 0)
        {
            return new FileImportResult(ImportedFiles:[], FileImportIssues:[], SelectedFolders:[]);
        }

        var importedFiles = new List<MkvFile>();
        var issues = new List<FileImportIssue>();

        foreach (var file in files)
        {
            var localFilePath = file.Path.LocalPath;
            var parentFolder = Path.GetFileName(Path.GetDirectoryName(localFilePath) ?? string.Empty);

            if (!string.Equals(Path.GetExtension(localFilePath), ".mkv", StringComparison.InvariantCultureIgnoreCase))
            {
                issues.Add(new FileImportIssue(file.Name, "Not a .mkv file", FileImportIssueType.UnsupportedFileType));
                continue;
            }

            var issue = await ValidateMkvFileAsync(file);
            if (issue != null)
            {
                issues.Add(issue);
                continue;
            }
            
            importedFiles.Add(CreateImportedFile(parentFolder,localFilePath));
        }
        
        var sortedImportFiles = importedFiles
            .OrderBy(file => file.FullPath, StringComparer.InvariantCultureIgnoreCase)
            .ToArray();
        return new FileImportResult(sortedImportFiles, issues.ToList(), SelectedFolders: []);
    }

    public async Task<FileImportResult> ImportFromPathsAsync(IReadOnlyList<string> paths)
    {
        var importedFiles = new List<MkvFile>();
        var issues = new List<FileImportIssue>();
        var isImportedFileFound = false;
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var directoryName = Path.GetFileName(path);
                    foreach (var filePath in Directory.EnumerateFiles(path, "*.mkv", SearchOption.AllDirectories))
                    {
                        var issue = await ValidateMkvFileAsync(path);
                        if (issue != null)
                        {
                            issues.Add(issue);
                            continue;
                        }

                        isImportedFileFound = true;
                        importedFiles.Add(CreateImportedFile(directoryName,filePath));
                    }

                    if (!isImportedFileFound)
                    {
                        issues.Add(new FileImportIssue(path, "No *.mkv files found", FileImportIssueType.FileNotFound));
                    }
                }
                else if (File.Exists(path))
                {
                    var issue = await ValidateMkvFileAsync(path);
                    if (issue != null)
                    {
                        issues.Add(issue);
                        continue;
                    }
                    
                    importedFiles.Add(CreateImportedFile(path, path));
                }
                else
                {
                    issues.Add(new FileImportIssue(path, "file not found", FileImportIssueType.FileNotFound));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex,
                    $"A ${nameof(UnauthorizedAccessException)} exception occurred while importing {path}");
                issues.Add(new FileImportIssue(path, $"{ex.Message}", FileImportIssueType.PermissionDenied));
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"An {nameof(IOException)} exception occurred while importing {path}");
                issues.Add(new FileImportIssue(path, $"{ex.Message}", FileImportIssueType.Unknown));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An exception occurred while importing {path}");
                issues.Add(new FileImportIssue(path, $"{ex.Message}", FileImportIssueType.Unknown));
            }
        }
        
        var sortedImportFiles = importedFiles
            .OrderBy(file => file.FullPath, StringComparer.InvariantCultureIgnoreCase)
            .ToArray();
        return new FileImportResult(sortedImportFiles, issues.ToList(), SelectedFolders: []);
    }


    private async Task<FileImportIssue?> ValidateMkvFileAsync(string filePath)
    {
        FileImportIssue? issue = null;
        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            if (!await FileTypeValidator.IsTypeRecognizableAsync(fileStream) ||
                !await fileStream.IsAsync<MatroskaVideo>())
            {
                _logger.LogDebug("Unsupported  file type: {FileType}", filePath);
                return new FileImportIssue(filePath, "Unsupported Video File",
                    FileImportIssueType.UnsupportedFileType);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file");
            issue = new FileImportIssue(filePath, $"{ex.Message}", FileImportIssueType.PermissionDenied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file"); 
            issue = new FileImportIssue(filePath, "Not a mkv container", FileImportIssueType.InvalidContainer);
        }

        return issue;
    }
    
    private async Task<FileImportIssue?> ValidateMkvFileAsync(IStorageFile storageFile)
    {
        
        FileImportIssue? issue = null;
        try
        {
            await using var fileStream = await storageFile.OpenReadAsync();

            if (!await FileTypeValidator.IsTypeRecognizableAsync(fileStream) ||
                !await fileStream.IsAsync<MatroskaVideo>())
            {
                _logger.LogDebug("Unsupported  file type: {FileType}", storageFile.Path);
                return new FileImportIssue(storageFile.Name, "Unsupported Video File",
                    FileImportIssueType.UnsupportedFileType);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file");
            issue = new FileImportIssue(storageFile.Name, $"{ex.Message}", FileImportIssueType.PermissionDenied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while validating the file"); 
            issue = new FileImportIssue(storageFile.Name, "Not a mkv container", FileImportIssueType.InvalidContainer);
        }

        return issue;
    }

    private MkvFile CreateImportedFile(string root, string fullPath)
    {
        var sizeInBytes = TryGetFileSize(fullPath);
        return new MkvFile(root, fullPath, sizeInBytes,true);
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

    private async Task<Dictionary<string, FrozenSet<MkvFile>>> OldSearch(TopLevel topLevel)
    {
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = true
        });
        var fileDict = new Dictionary<string, FrozenSet<string>>();
        foreach (var folder in folders)
        {
            var fullPathList = new List<string>();
            await foreach (var storageItem in folder.GetItemsAsync())
            {
                if (storageItem is not { CanBookmark: true, Path.IsFile: true } ||
                    Path.GetExtension(storageItem.Path.LocalPath) != ".mkv")
                {
                    continue;
                }
                var path = await storageItem.SaveBookmarkAsync();
                Console.WriteLine(path);
                if (path != null)
                {
                    fullPathList.Add(path);
                }
            }

            if (!fileDict.ContainsKey(folder.Name))
            {
                fileDict.Add(folder.Name,fullPathList.ToFrozenSet());
            }
        }
        
        var mkvFileDict = new Dictionary<string, FrozenSet<MkvFile>>();
        var mkvFiles = new List<MkvFile>();
        foreach (var(rootDir, paths) in fileDict)
        {
            mkvFiles.AddRange(paths.Select(importFilePath => new MkvFile(rootDir, importFilePath, null,true)));
            _logger.LogDebug("key:{RootDir}, value:{PathsCount}", rootDir, paths.Count);
            mkvFileDict.Add(rootDir,mkvFiles.ToFrozenSet());
        }
        return mkvFileDict;
    }
}