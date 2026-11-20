using System;

namespace MkvRenameWizard.FileTypes;

public static class MatroskaVideo
{
    // EBML header magic bytes for Matroska (.mkv) containers
    public static ReadOnlySpan<byte> MagicBytes => [0x1A, 0x45, 0xDF, 0xA3];

    public static bool HasValidHeader(ReadOnlySpan<byte> header)
        => header.Length >= 4 && header[..4].SequenceEqual(MagicBytes);
}
