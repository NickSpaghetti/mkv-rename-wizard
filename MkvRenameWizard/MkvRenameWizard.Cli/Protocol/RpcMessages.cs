using System.Text.Json;
using MkvRenameWizard.Core.Models.Renaming;

namespace MkvRenameWizard.Cli.Protocol;

internal sealed record RpcRequest(
    string? Id,
    string Method,
    JsonElement? Params);

internal sealed record RpcError(
    string Code,
    string Message,
    string? Detail);

internal sealed record RpcResponse(
    string? Id,
    bool Ok,
    JsonElement? Result,
    RpcError? Error);

internal sealed record HelloPayload(
    int ProtocolVersion,
    string App,
    IReadOnlyList<PatternToken> Tokens);

internal sealed record SearchParams(string Query);
internal sealed record ShowIdParams(long ShowId);
internal sealed record SeasonIdParams(long SeasonId);
internal sealed record ScanParams(IReadOnlyList<string> Paths);
internal sealed record ValidatePatternParams(string Pattern);
internal sealed record RenameExecuteParams(IReadOnlyList<RenameFileOperation> Operations);
internal sealed record UndoRenameParams(IReadOnlyList<RenameOperationResult> Results);

internal sealed record CliOrderPairInput(
    string SourcePath,
    int? Season = null,
    int? EpisodeNumber = null,
    int? EpisodeIndex = null);

internal sealed record CliOrderInput(
    IReadOnlyList<string>? Files = null,
    IReadOnlyList<int>? Order = null,
    IReadOnlyList<CliOrderPairInput>? Pairs = null);

internal enum CliSessionStatus
{
    Planned,
    Applied,
    Undone,
    Failed
}

internal sealed record CliSessionState(
    Guid SessionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? AppliedAtUtc,
    DateTimeOffset? UndoneAtUtc,
    CliSessionStatus Status,
    RenamePlanRequest Request,
    RenamePlan Plan,
    IReadOnlyList<RenameOperationResult>? Results = null,
    IReadOnlyList<RenameOperationResult>? UndoResults = null);

internal sealed record CliSessionSummary(
    Guid SessionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    CliSessionStatus Status,
    string ShowName,
    string Pattern,
    int ReadyCount,
    int ConflictCount,
    string StateFilePath);

internal sealed record CliBuiltPlan(
    int ExitCode,
    RenamePlanRequest? Request,
    RenamePlan? Plan);

internal sealed record CliRenameSummary(
    bool DryRun,
    RenamePlan Plan,
    IReadOnlyList<RenameOperationResult>? Results,
    Guid? SessionId = null,
    string? StateFilePath = null);
