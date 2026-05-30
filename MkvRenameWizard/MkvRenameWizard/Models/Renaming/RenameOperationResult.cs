namespace MkvRenameWizard.Models.Renaming;

public record RenameOperationResult<T>(T RenameOperation, bool IsSuccessful, string? ErrorMessage) where T : IRenameOperation;