using MkvRenameWizard.Models.FileImport;

namespace MkvRenameWizard.ViewModels;

public class FileImportIssueViewModel(FileImportIssue fileImportIssue)
{
    public string DisplayName => fileImportIssue.DisplayName;
    public string Reason => fileImportIssue.Reason;
    public FileImportIssueType Type => fileImportIssue.FileImportIssueType;
    public string Detail => $"{DisplayName}: {Reason}";

}