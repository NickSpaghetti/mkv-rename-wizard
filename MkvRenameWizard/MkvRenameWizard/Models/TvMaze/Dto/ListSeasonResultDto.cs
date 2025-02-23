using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class ListSeasonResultDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("episodeOrder")]
    public int? EpisodeOrder { get; set; }

    [JsonPropertyName("premiereDate")]
    public string PremiereDate { get; set; } = string.Empty;

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("network")]
    public NetworkDto Network { get; set; }

    [JsonPropertyName("webChannel")]
    public object? WebChannel { get; set; }

    [JsonPropertyName("image")]
    public ImageDto Image { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("_links")]
    public LinksDto Links { get; set; }
    
}