using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class LinksDto
{
    [JsonPropertyName("self")]
    public SelfDto SelfDto { get; set; } = new();

    [JsonPropertyName("previousepisode")]
    public PreviousEpisodeDto PreviousEpisodeDto { get; set; } = new();
}