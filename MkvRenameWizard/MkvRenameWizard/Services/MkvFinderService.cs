using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

using Avalonia.Platform.Storage;
using MkvRenameWizard.Models.Mkv;

namespace MkvRenameWizard.Services;

public class MkvFinderService : IMkvFinderService
{
    public async Task<Dictionary<string,FrozenSet<MkvFile>>> OpenMkvFiles(TopLevel topLevel)
    {
        var fileDict = new Dictionary<string, FrozenSet<string>>();
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = true
        });
        foreach (var folder in folders)
        {
            var fullPathList = new List<string>();
            await foreach (var storageItem in folder.GetItemsAsync())
            {
                if (storageItem is not { CanBookmark: true, Path.IsFile: true } ||
                    System.IO.Path.GetExtension(storageItem.Path.LocalPath) != ".mkv")
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
            mkvFiles.AddRange(paths.Select(importFilePath => new MkvFile(rootDir, importFilePath, true)));
            Console.WriteLine($"key:{rootDir}, value:{paths.Count}");
            mkvFileDict.Add(rootDir,mkvFiles.ToFrozenSet());
        }
        return mkvFileDict;
    }
}