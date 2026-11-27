namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record RenamePlan(
    bool IsPatternValid,
    int ReadyCount,
    int ConflictCount,
    int SkippedCount,
    int DoneCount,
    IReadOnlyList<PatternSegment> Segments,
    IReadOnlyList<PatternError> Errors,
    IReadOnlyList<RenamePlanItem> Items);
