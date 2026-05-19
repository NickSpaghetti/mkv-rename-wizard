using System.IO;

namespace MkvRenameWizard.Models.FileImport;

public record FileImportIssue
{
    public string DisplayName { get; }
    public string Reason { get; }
    public FileImportIssueType FileImportIssueType { get; } 
    
    public  FileImportIssue (string displayName, string reason, FileImportIssueType fileImportIssueType)
    {
       DisplayName = string.IsNullOrWhiteSpace(displayName) ? FileImportIssueType.Unknown.ToString("G") : displayName;
       Reason = string.IsNullOrWhiteSpace(reason) ? FileImportIssueType.Unknown.ToString("G") : reason;
       FileImportIssueType = fileImportIssueType;
    }

}