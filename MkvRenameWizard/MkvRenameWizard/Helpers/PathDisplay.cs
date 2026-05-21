using System;
using System.IO;

namespace MkvRenameWizard.Helpers;

public static class PathDisplay
{
    public static string GetSafeFileName(string? path, string emptyFallBackName = "")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return emptyFallBackName;
        }

        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return path;
        }

        if (fileName.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return path;
        }

        return fileName;
    }
}