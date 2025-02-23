using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class ExternalsDto
{
    [JsonPropertyName("tvrage")]
    public int? Tvrage { get; set; }

    [JsonPropertyName("thetvdb")]
    public int Thetvdb { get; set; }

    [JsonPropertyName("imdb")]
    public string Imdb { get; set; } = string.Empty;
}
