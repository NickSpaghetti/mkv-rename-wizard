using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class RatingDto
{
    [JsonPropertyName("average")] public double? Average { get; set; }
}