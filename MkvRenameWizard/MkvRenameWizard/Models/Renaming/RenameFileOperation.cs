namespace MkvRenameWizard.Models.Renaming;

public record RenameFileOperation(int OperationId, string SourcePath, string? TargetPath) : IRenameOperation;