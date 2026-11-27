namespace MkvRenameWizard.Core.Models.MkvFiles;

public sealed record FileImportIssue(
    string DisplayName,
    string Reason,
    FileImportIssueType FileImportIssueType);
