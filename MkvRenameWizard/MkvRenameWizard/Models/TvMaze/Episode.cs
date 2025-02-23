namespace MkvRenameWizard.Models.TvMaze;

public class Episode
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Season { get; set; }
    public int? EpisodeNumber { get; set; }
    public string Type { get; set; } = string.Empty;
}