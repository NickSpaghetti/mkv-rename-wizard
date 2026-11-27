namespace MkvRenameWizard.Core.Models.MkvFiles;

public sealed record FileImportResult(
    IReadOnlyList<MkvFile> ImportedFiles,
    IReadOnlyList<FileImportIssue> FileImportIssues,
    IReadOnlyList<string> SelectedFolders);
