using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Core.Services.Aggregations;
using MkvRenameWizard.Helpers;
using MkvRenameWizard.Models.FileImport;
using MkvRenameWizard.Models.Mkv;

namespace MkvRenameWizard.Services;

public class MkvFinderService : IMkvFinderService
{
    private readonly IMkvRenameWizardService _coreService;
    private readonly ILogger<MkvFinderService> _logger;

    public MkvFinderService(IMkvRenameWizardService coreService, ILogger<MkvFinderService> logger)
    {
        _coreService = coreService;
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
            var topLevelFilePaths = new List<string>();
            var isFolderHavePermissionDenied = false;

            try
            {
                // Intentionally non-recursive: only inspect direct children of the picked folder.
                await foreach (var storageItem in folder.GetItemsAsync())
                {
                    if (storageItem is not IStorageFile storageFile)
                    {
                        issues.Add(new FileImportIssue(storageItem.Name,
                            FileImportIssueType.NoSupportedFilesFound.ToString("G"),
                            FileImportIssueType.NoSupportedFilesFound));
                        continue;
                    }

                    var localPath = StorageItemPaths.TryResolveExistingLocalPath(storageFile);
                    if (localPath is null)
                    {
                        issues.Add(new FileImportIssue(storageFile.Name,
                            FileImportIssueType.NoSupportedFilesFound.ToString("G"),
                            FileImportIssueType.NoSupportedFilesFound));
                        continue;
                    }

                    topLevelFilePaths.Add(localPath);
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

            var folderImportedCount = 0;
            if (topLevelFilePaths.Count > 0)
            {
                var folderResult = await ImportFromPathsAsync(topLevelFilePaths);
                folderImportedCount = folderResult.ImportedFiles.Count;
                importedFiles.AddRange(folderResult.ImportedFiles);
                issues.AddRange(folderResult.FileImportIssues);
            }

            if (folderImportedCount == 0 && !isFolderHavePermissionDenied)
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

        var localPaths = new List<string>(files.Count);
        foreach (var file in files)
        {
            var localPath = StorageItemPaths.TryResolveExistingLocalPath(file);
            if (localPath is not null)
            {
                localPaths.Add(localPath);
            }
        }

        if (localPaths.Count == 0)
        {
            return new FileImportResult(ImportedFiles: [], FileImportIssues: [], SelectedFolders: []);
        }

        return await ImportFromPathsAsync(localPaths);
    }

    public Task<FileImportResult> ImportFromPathsAsync(IReadOnlyList<string> paths)
    {
        var coreResult = _coreService.ImportMkvFilesFromPaths(paths);

        var importedFiles = new MkvFile[coreResult.ImportedFiles.Count];
        for (var i = 0; i < coreResult.ImportedFiles.Count; i++)
        {
            var f = coreResult.ImportedFiles[i];
            importedFiles[i] = new MkvFile(f.Root, f.FullPath, f.SizeInBytes, f.IsIncluded);
        }

        var issues = new List<FileImportIssue>(coreResult.FileImportIssues.Count);
        for (var i = 0; i < coreResult.FileImportIssues.Count; i++)
        {
            var issue = coreResult.FileImportIssues[i];
            issues.Add(new FileImportIssue(
                issue.DisplayName,
                issue.Reason,
                (FileImportIssueType)(int)issue.FileImportIssueType));
        }

        return Task.FromResult(new FileImportResult(importedFiles, issues, coreResult.SelectedFolders));
    }
}
