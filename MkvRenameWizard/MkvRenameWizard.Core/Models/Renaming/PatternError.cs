namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record PatternError(int Column, string TokenName, string? Suggestion);
