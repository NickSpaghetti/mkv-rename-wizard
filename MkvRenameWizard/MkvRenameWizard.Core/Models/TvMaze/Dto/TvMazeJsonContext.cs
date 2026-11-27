using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MkvRenameWizard.Core.Models.TvMaze.Dto;

[JsonSerializable(typeof(List<ShowSearchResultDto>))]
[JsonSerializable(typeof(List<ListSeasonResultDto>))]
[JsonSerializable(typeof(List<EpisodeDto>))]
internal partial class TvMazeJsonContext : JsonSerializerContext;
