using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class SelfDto
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}