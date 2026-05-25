namespace MkvRenameWizard.Models.FileImport;

public enum FileImportIssueType
{
    Unknown,
    FileNotFound,
    UnsupportedFileType,
    PermissionDenied,
    InvalidContainer,
    NoSupportedFilesFound
}