namespace MkvRenameWizard.Core.Models.TvMaze;

public sealed record Episode
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Season { get; init; }
    public int? EpisodeNumber { get; init; }
    public string Type { get; init; } = string.Empty;
    public long RunTime { get; init; }
    public DateOnly? AirDate { get; init; }
}
