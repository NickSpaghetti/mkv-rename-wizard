using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze.Dto;

public class ScheduleDto
{
    [JsonPropertyName("time")] public string Time { get; set; } = string.Empty;

    [JsonPropertyName("days")] public List<string> Days { get; set; } = new();
}