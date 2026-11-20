using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public sealed class ListSeasonResultDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("episodeOrder")]
    public int? EpisodeOrder { get; set; }
}
