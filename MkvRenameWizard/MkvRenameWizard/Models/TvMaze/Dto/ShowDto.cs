using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class ShowDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("genres")]
        public List<string> Genres { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("runtime")]
        public int Runtime { get; set; }

        [JsonPropertyName("averageRuntime")]
        public int AverageRuntime { get; set; }

        [JsonPropertyName("premiered")]
        public string Premiered { get; set; } = string.Empty;

        [JsonPropertyName("ended")]
        public string? Ended { get; set; }

        [JsonPropertyName("officialSite")]
        public string? OfficialSite { get; set; }

        [JsonPropertyName("schedule")]
        public ScheduleDto ScheduleDto { get; set; } = new();

        [JsonPropertyName("rating")]
        public RatingDto RatingDto { get; set; } = new();

        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("network")]
        public NetworkDto NetworkDto { get; set; } = new();

        [JsonPropertyName("webChannel")]
        public object? WebChannel { get; set; }

        [JsonPropertyName("dvdCountry")]
        public object? DvdCountry { get; set; }

        [JsonPropertyName("externals")]
        public ExternalsDto ExternalsDto { get; set; } = new();

        [JsonPropertyName("image")]
        public ImageDto ImageDto { get; set; } = new();

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("updated")]
        public int Updated { get; set; }

        [JsonPropertyName("_links")]
        public LinksDto LinksDto { get; set; } = new();
    }