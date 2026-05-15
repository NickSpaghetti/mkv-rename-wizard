namespace MkvRenameWizard.Models.TvMaze;

public class Season
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public int TotalEpisodeCount { get; set; }
}