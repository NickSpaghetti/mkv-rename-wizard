using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public sealed class ShowDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("genres")]
    public List<string>? Genres { get; set; }

    [JsonPropertyName("premiered")]
    public string Premiered { get; set; } = string.Empty;

    [JsonPropertyName("ended")]
    public string? Ended { get; set; }

    [JsonPropertyName("rating")]
    public RatingDto? RatingDto { get; set; }

    [JsonPropertyName("network")]
    public NetworkDto? NetworkDto { get; set; }

    [JsonPropertyName("webChannel")]
    public WebChannelDto? WebChannel { get; set; }

    [JsonPropertyName("image")]
    public ImageDto? ImageDto { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}
