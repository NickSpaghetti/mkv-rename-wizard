using System.Text.Json.Serialization;
using MkvRenameWizard.Core.Models.MkvFiles;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Cli.Protocol;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    Converters = [
        typeof(JsonStringEnumConverter<CaseStyle>),
        typeof(JsonStringEnumConverter<RenamePreviewStatus>),
        typeof(JsonStringEnumConverter<PatternSegmentType>),
        typeof(JsonStringEnumConverter<FileImportIssueType>),
        typeof(JsonStringEnumConverter<CliSessionStatus>)
    ])]
[JsonSerializable(typeof(RpcRequest))]
[JsonSerializable(typeof(RpcResponse))]
[JsonSerializable(typeof(RpcError))]
[JsonSerializable(typeof(HelloPayload))]
[JsonSerializable(typeof(SearchParams))]
[JsonSerializable(typeof(ShowIdParams))]
[JsonSerializable(typeof(SeasonIdParams))]
[JsonSerializable(typeof(ScanParams))]
[JsonSerializable(typeof(ValidatePatternParams))]
[JsonSerializable(typeof(RenamePlanRequest))]
[JsonSerializable(typeof(RenameExecuteParams))]
[JsonSerializable(typeof(UndoRenameParams))]
[JsonSerializable(typeof(IReadOnlyList<Show>))]
[JsonSerializable(typeof(IReadOnlyList<Season>))]
[JsonSerializable(typeof(IReadOnlyList<Episode>))]
[JsonSerializable(typeof(IReadOnlyList<PatternToken>))]
[JsonSerializable(typeof(FileImportResult))]
[JsonSerializable(typeof(PatternValidationResult))]
[JsonSerializable(typeof(RenamePlan))]
[JsonSerializable(typeof(IReadOnlyList<RenameOperationResult>))]
[JsonSerializable(typeof(CliRenameSummary))]
[JsonSerializable(typeof(CliOrderPairInput))]
[JsonSerializable(typeof(CliOrderInput))]
[JsonSerializable(typeof(CliSessionState))]
[JsonSerializable(typeof(CliSessionSummary))]
[JsonSerializable(typeof(IReadOnlyList<CliSessionSummary>))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
