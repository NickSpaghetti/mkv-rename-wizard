using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace MkvRenameWizard.Helpers;

public static class StorageItemPaths
{
    public static IReadOnlyList<string> GetExisingLocalPathsFromTransfer(IDataTransfer dataTransfer)
    {
        if (dataTransfer.TryGetFiles() is not { } storageItems)
        {
            return ArraySegment<string>.Empty;
        }

        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var storageItem in storageItems)
        {
            var path = TryResolveExistingLocalPath(storageItem);
            if (path is null || !seen.Add(path))
            {
                continue;
            }
            paths.Add(path);
        }
        return paths;
    }

    public static string? TryResolveExistingLocalPath(IStorageItem storageItem)
    {
        var path = storageItem.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        path = storageItem.Path.LocalPath;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        return File.Exists(path) || Directory.Exists(path) ? path : null;
    }
}