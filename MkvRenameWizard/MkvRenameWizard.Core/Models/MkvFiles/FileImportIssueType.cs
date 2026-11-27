namespace MkvRenameWizard.Core.Models.MkvFiles;

public enum FileImportIssueType
{
    Unknown,
    FileNotFound,
    UnsupportedFileType,
    PermissionDenied,
    InvalidContainer,
    NoSupportedFilesFound
}
