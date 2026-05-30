namespace MkvRenameWizard.Models.Renaming;

/// <summary>
/// A single unreconized token found in the Renaming filename patter
/// </summary>
/// <param name="Column">One based column index of the opening '{' in the pattern string</param>
/// <param name="TokenName">The unrecognized token name the user typed without '{' '}' braces</param>
/// <param name="Suggestion">Closest valid TokenName based on the edit distance, or null when not matches found</param>
public record PatternError(int Column, string TokenName, string? Suggestion);