namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record RenamePlanItem(
    int OperationId,
    string SourcePath,
    string SourceFileName,
    string? TargetPath,
    string? TargetFileName,
    RenamePreviewStatus Status);
