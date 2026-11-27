namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record RenameOperationResult(
    int OperationId,
    string SourcePath,
    string? TargetPath,
    bool IsSuccessful,
    string? ErrorMessage);
