using System.Text.Json.Serialization;

namespace MkvRenameWizard.Core.Models.TvMaze.Dto;

public class SelfDto
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}