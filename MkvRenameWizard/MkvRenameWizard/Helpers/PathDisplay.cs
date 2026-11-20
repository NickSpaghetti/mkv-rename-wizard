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

        var fileName = Path.GetFileName(path.AsSpan());
        if (fileName.IsWhiteSpace())
        {
            return path;
        }

        if (fileName.IndexOfAny(FilePatternHelper.InvalidFileNameChars) >= 0)
        {
            return path;
        }

        return new string(fileName);
    }
}
