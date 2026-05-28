namespace MkvRenameWizard.Models.Renaming;

public record RenamePreviewItem<T>(T RenameOperation, RenamePreviewStatus RenamePreviewStatus) where T : IRenameOperation;