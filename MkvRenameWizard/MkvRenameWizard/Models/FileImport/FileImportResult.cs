using System.Collections.Generic;
using System.Linq;
using MkvRenameWizard.Models.Mkv;

namespace MkvRenameWizard.Models.FileImport;

public record FileImportResult(
    IReadOnlyList<MkvFile> ImportedFiles,
    IReadOnlyList<FileImportIssue> FileImportIssues,
    IReadOnlyList<string> SelectedFolders)
{
    
    public bool HasImportedFiles => ImportedFiles.Count > 0;
    public bool HasFileImportIssues => FileImportIssues.Count > 0;
    public bool IsPartialFailure => HasImportedFiles && HasFileImportIssues;
    public bool IsBlocked => !HasImportedFiles && HasFileImportIssues;
    public bool IsEmpty => !HasImportedFiles && !HasFileImportIssues;
    public bool HasPermissionsDenied => FileImportIssues.Any(issue => issue.FileImportIssueType == FileImportIssueType.PermissionDenied);

    public bool HasNoSupportedFilesFound =>
        FileImportIssues.Any(issue => issue.FileImportIssueType == FileImportIssueType.NoSupportedFilesFound);

}