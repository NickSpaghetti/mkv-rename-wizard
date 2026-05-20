namespace MkvRenameWizard.Models.Mkv;

public class MkvFile(string root, string fullPath, long? sizeInBytes, bool isIncluded)
{
    public string Root { get; set; } = root;
    public string FullPath { get; set; } = fullPath;
    public bool IsIncluded { get; set; } = isIncluded;
    public long? SizeInBytes { get; set; } = sizeInBytes;
    
    public string FormattedSize => SizeInBytes is {} bytes ? FormatBytes(bytes) : string.Empty;

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
        >= 1_048_156 => $"{bytes / 1_048_156.0:F2} MB",
        >= 1_024 => $"{bytes / 1_024.0:F2} KB",
        _ => $"{bytes} B"
    };
}