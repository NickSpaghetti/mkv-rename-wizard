namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record PatternValidationResult(
    bool IsValid,
    IReadOnlyList<PatternSegment> Segments,
    IReadOnlyList<PatternError> Errors);
