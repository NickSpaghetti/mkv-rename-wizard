using System.IO;
using FileTypeChecker;
using FileTypeChecker.Abstracts;

namespace MkvRenameWizard.FileTypes;

public class MatroskaVideo : FileType
{
    private static readonly byte[] MkvMagicNumber = [0x1A, 0x45, 0xDF, 0xA3];
    
    public MatroskaVideo() 
        : base("Matroska Video File", "video/x-matroska", "mkv", MkvMagicNumber)
    {
    }
    
    public MatroskaVideo(string name, string mimeType, string extension, byte[] magicBytes) 
        : base(name, mimeType, extension, magicBytes)
    {
    }

    public MatroskaVideo(string name, string mimeType, string extension, byte[][] magicBytes) 
        : base(name, mimeType, extension, magicBytes)
    {
    }

    public MatroskaVideo(string name, string mimeType, string extension, MagicSequence magicBytes) 
        : base(name, mimeType, extension, magicBytes)
    {
    }

    public MatroskaVideo(string name, string mimeType, string extension, MagicSequence[] magicBytesSequence) 
        : base(name, mimeType, extension, magicBytesSequence)
    {
    }
}