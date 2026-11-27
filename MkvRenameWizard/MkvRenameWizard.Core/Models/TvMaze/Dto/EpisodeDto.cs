using System.Text.Json.Serialization;

namespace MkvRenameWizard.Core.Models.TvMaze.Dto;

public sealed class EpisodeDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("airdate")]
    public string Airdate { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public long Runtime { get; set; }
}
