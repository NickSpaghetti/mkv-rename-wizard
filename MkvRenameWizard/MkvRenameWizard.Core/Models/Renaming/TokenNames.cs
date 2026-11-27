namespace MkvRenameWizard.Core.Models.Renaming;

public static class TokenNames
{
    public const string Show = nameof(Show);
    public const string Season = nameof(Season);
    public const string SeasonPadded = "S##";
    public const string Episode = nameof(Episode);
    public const string EpisodePadded = "E##";
    public const string SeasonEpisodePadded = $"{SeasonPadded}{EpisodePadded}";
    public const string Title = nameof(Title);
    public const string Year = nameof(Year);
    public const string Ext = nameof(Ext);
    public const string RunTime = nameof(RunTime);
    public const string DefaultPattern = $"{{{SeasonEpisodePadded}}} {{{Title}}}";
}
