using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class ImageDto
{
    [JsonPropertyName("medium")]
    public string Medium { get; set; } = string.Empty;

    [JsonPropertyName("original")]
    public string Original { get; set; } = string.Empty;
}