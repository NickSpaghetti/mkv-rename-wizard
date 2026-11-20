using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public sealed class ShowSearchResultDto
{
    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("show")]
    public ShowDto? ShowDto { get; set; }
}
