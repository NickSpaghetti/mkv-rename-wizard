namespace MkvRenameWizard.Models.Rail;

public sealed record RailReorderDragData(RailReorderSide ReorderSide, int SourceIndex, bool IsMoveLinked);
