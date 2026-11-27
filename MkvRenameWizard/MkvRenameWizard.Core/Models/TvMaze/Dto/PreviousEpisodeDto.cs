using System.Text.Json.Serialization;

namespace MkvRenameWizard.Core.Models.TvMaze.Dto;

public class PreviousEpisodeDto
{
    public class Previousepisode
    {
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;
    }
}