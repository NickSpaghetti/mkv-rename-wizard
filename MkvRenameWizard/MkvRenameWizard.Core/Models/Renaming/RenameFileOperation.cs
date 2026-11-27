namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record RenameFileOperation(
    int OperationId,
    string SourcePath,
    string? TargetPath);
