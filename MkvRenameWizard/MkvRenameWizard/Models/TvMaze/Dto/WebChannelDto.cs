using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class WebChannelDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("country")]
    public CountryDto? Country { get; set; }
    [JsonPropertyName("officialSite")]
    public string? OfficialSite { get; set; }
}