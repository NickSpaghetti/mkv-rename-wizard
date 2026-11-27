namespace MkvRenameWizard.Core.Models.Renaming;

public sealed record RenameCandidate(
    string SourcePath,
    int Season,
    int? EpisodeNumber,
    string EpisodeName,
    long RunTime,
    DateOnly? AirDate);
