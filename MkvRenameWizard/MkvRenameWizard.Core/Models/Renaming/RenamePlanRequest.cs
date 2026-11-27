namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record RenamePlanRequest(
    string ShowName,
    string Pattern,
    string Prefix,
    CaseStyle CaseStyle,
    string? TargetDirectory,
    IReadOnlyList<RenameCandidate> Candidates);
