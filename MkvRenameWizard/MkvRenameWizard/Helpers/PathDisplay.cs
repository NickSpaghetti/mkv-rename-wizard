using System;
using System.Buffers;
using System.IO;
using System.Linq;

namespace MkvRenameWizard.Helpers;

public static class PathDisplay
{
    private static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(
            Path.GetInvalidFileNameChars()
                .Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
                .Distinct()
                .ToArray());

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

        if (fileName.IndexOfAny(InvalidFileNameChars) >= 0)
        {
            return path;
        }

        return new string(fileName);
    }
}
