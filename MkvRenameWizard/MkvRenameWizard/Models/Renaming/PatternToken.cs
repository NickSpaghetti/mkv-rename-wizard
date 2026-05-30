namespace MkvRenameWizard.Models.Renaming;

/// <summary>
/// Describes a single valid substitution token (ie: {Title}, {S##E##}}
/// </summary>
/// <param name="Name"></param>
/// <param name="Description"></param>
/// <param name="ExampleValue"></param>
public record PatternToken(string Name, string Description, string ExampleValue);