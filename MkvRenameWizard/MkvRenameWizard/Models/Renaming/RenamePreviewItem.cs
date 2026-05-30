namespace MkvRenameWizard.Models.Renaming;

public record RenamePreviewItem<T>(T RenameOperation, string SourceFileName, RenamePreviewStatus RenamePreviewStatus)
    where T : IRenameOperation;