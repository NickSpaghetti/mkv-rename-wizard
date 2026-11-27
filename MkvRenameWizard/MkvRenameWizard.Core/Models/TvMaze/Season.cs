namespace MkvRenameWizard.Core.Models.TvMaze;

public sealed record Season
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Number { get; init; }
    public int TotalEpisodeCount { get; init; }
    public bool HasEpisodeOrder { get; init; }
}
