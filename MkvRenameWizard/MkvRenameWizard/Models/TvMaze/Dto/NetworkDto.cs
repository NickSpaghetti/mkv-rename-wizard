using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class NetworkDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public CountryDto CountryDto { get; set; } = new();

    [JsonPropertyName("officialSite")]
    public string? OfficialSite { get; set; }
}