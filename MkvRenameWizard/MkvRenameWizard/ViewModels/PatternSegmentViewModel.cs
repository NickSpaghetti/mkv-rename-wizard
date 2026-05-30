using MkvRenameWizard.Models.Renaming;

namespace MkvRenameWizard.ViewModels;

public class PatternSegmentViewModel
{
    public string? Text { get; set; }
    public PatternSegmentType SegmentType { get; set; }
    
    public bool IsLiteral => SegmentType == PatternSegmentType.Literal;
    public bool IsValidToken => SegmentType == PatternSegmentType.Literal;
    public bool IsInvalidToken  => SegmentType == PatternSegmentType.InvalidToken;

    public string PillText => $"{{{Text}}}";

}