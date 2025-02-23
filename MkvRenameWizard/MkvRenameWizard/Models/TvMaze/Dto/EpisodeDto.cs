using System;
using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class EpisodeDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

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

    [JsonPropertyName("airtime")]
    public string Airtime { get; set; } = string.Empty;

    [JsonPropertyName("airstamp")]
    public DateTime Airstamp { get; set; }

    [JsonPropertyName("runtime")]
    public int Runtime { get; set; }

    [JsonPropertyName("rating")]
    public RatingDto Rating { get; set; }

    [JsonPropertyName("image")]
    public ImageDto? Image { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("_links")]
    public LinksDto Links { get; set; }
}