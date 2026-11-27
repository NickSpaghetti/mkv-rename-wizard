using System.Text.Json;
using MkvRenameWizard.Cli.Protocol;
using MkvRenameWizard.Core.Models.Categories;
using MkvRenameWizard.Core.Services.Aggregations;

namespace MkvRenameWizard.Cli.Exposers.Stdio;

internal sealed class StdioExposer(IMkvRenameWizardService coreService) : IStdioExposer
{
    public const int ProtocolVersion = 1;

    public async Task<int> RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var responseLine = await HandleLineAsync(line, cancellationToken);
            await output.WriteLineAsync(responseLine.AsMemory(), cancellationToken);
            await output.FlushAsync(cancellationToken);
        }

        return 0;
    }

    internal async Task<string> HandleLineAsync(string line, CancellationToken cancellationToken)
    {
        string? requestId = null;
        string method;
        JsonElement? requestParams;

        if (!TryParseRequest(line, out requestId, out method, out requestParams, out var parseError))
        {
            return SerializeError(requestId, "invalid_json", parseError ?? "Malformed JSON request.", null);
        }

        try
        {
            return method switch
            {
                "hello" => SerializeOk(
                    requestId,
                    new HelloPayload(ProtocolVersion, "mkv-rename-cli", coreService.RetrieveAvailableTokens()),
                    CliJsonContext.Default.HelloPayload),

                "tokens" => SerializeOk(
                    requestId,
                    coreService.RetrieveAvailableTokens(),
                    CliJsonContext.Default.IReadOnlyListPatternToken),

                "search" => await HandleSearchAsync(requestId, requestParams, cancellationToken),
                "seasons" => await HandleSeasonsAsync(requestId, requestParams, cancellationToken),
                "episodes" => await HandleEpisodesAsync(requestId, requestParams, cancellationToken),
                "scan" => HandleScan(requestId, requestParams),
                "validate-pattern" => HandleValidatePattern(requestId, requestParams),
                "plan" => HandlePlan(requestId, requestParams),
                "rename" => HandleRename(requestId, requestParams),
                "undo" => HandleUndo(requestId, requestParams),

                _ => SerializeError(requestId, "unknown_method", $"Unknown method '{method}'.", null)
            };
        }
        catch (OperationCanceledException)
        {
            return SerializeError(requestId, "cancelled", "Request was cancelled.", null);
        }
        catch (Exception exception) when (exception is IValidationCategory)
        {
            return SerializeError(requestId, "validation_error", exception.Message, exception.InnerException?.Message);
        }
        catch (Exception exception) when (exception is IDependencyCategory)
        {
            return SerializeError(requestId, "dependency_error", exception.Message, exception.InnerException?.Message);
        }
        catch (Exception exception) when (exception is IServiceCategory)
        {
            return SerializeError(requestId, "service_error", exception.Message, exception.InnerException?.Message);
        }
    }

    private async Task<string> HandleSearchAsync(string? id, JsonElement? rawParams, CancellationToken cancellationToken)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.SearchParams);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected { \"query\": string }.", null);
        }

        var shows = await coreService.SearchShowsByNameAsync(p.Query, cancellationToken);
        return SerializeOk(id, shows, CliJsonContext.Default.IReadOnlyListShow);
    }

    private async Task<string> HandleSeasonsAsync(string? id, JsonElement? rawParams, CancellationToken cancellationToken)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.ShowIdParams);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected { \"showId\": number }.", null);
        }

        var seasons = await coreService.RetrieveSeasonsByShowIdAsync(p.ShowId, cancellationToken);
        return SerializeOk(id, seasons, CliJsonContext.Default.IReadOnlyListSeason);
    }

    private async Task<string> HandleEpisodesAsync(string? id, JsonElement? rawParams, CancellationToken cancellationToken)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.SeasonIdParams);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected { \"seasonId\": number }.", null);
        }

        var episodes = await coreService.RetrieveEpisodesBySeasonIdAsync(p.SeasonId, cancellationToken);
        return SerializeOk(id, episodes, CliJsonContext.Default.IReadOnlyListEpisode);
    }

    private string HandleScan(string? id, JsonElement? rawParams)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.ScanParams);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected { \"paths\": string[] }.", null);
        }

        var result = coreService.ImportMkvFilesFromPaths(p.Paths);
        return SerializeOk(id, result, CliJsonContext.Default.FileImportResult);
    }

    private string HandleValidatePattern(string? id, JsonElement? rawParams)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.ValidatePatternParams);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected { \"pattern\": string }.", null);
        }

        var result = coreService.ValidatePattern(p.Pattern);
        return SerializeOk(id, result, CliJsonContext.Default.PatternValidationResult);
    }

    private string HandlePlan(string? id, JsonElement? rawParams)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.RenamePlanRequest);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected RenamePlanRequest payload.", null);
        }

        var plan = coreService.BuildRenamePlan(p);
        return SerializeOk(id, plan, CliJsonContext.Default.RenamePlan);
    }

    private string HandleRename(string? id, JsonElement? rawParams)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.RenameExecuteParams);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected { \"operations\": RenameFileOperation[] }.", null);
        }

        var results = coreService.ExecuteRenameOperations(p.Operations);
        return SerializeOk(id, results, CliJsonContext.Default.IReadOnlyListRenameOperationResult);
    }

    private string HandleUndo(string? id, JsonElement? rawParams)
    {
        var p = DeserializeParams(rawParams, CliJsonContext.Default.UndoRenameParams);
        if (p is null)
        {
            return SerializeError(id, "invalid_params", "Expected { \"results\": RenameOperationResult[] }.", null);
        }

        var results = coreService.UndoRenameOperations(p.Results);
        return SerializeOk(id, results, CliJsonContext.Default.IReadOnlyListRenameOperationResult);
    }

    private static bool TryParseRequest(
        string line,
        out string? id,
        out string method,
        out JsonElement? rawParams,
        out string? error)
    {
        id = null;
        method = string.Empty;
        rawParams = null;
        error = null;

        if (!line.TrimStart().StartsWith('{'))
        {
            error = "Request must be a JSON object.";
            return false;
        }

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            error = "Request must be a JSON object.";
            return false;
        }

        if (root.TryGetProperty("id", out var idProp))
        {
            id = idProp.ValueKind == JsonValueKind.String ? idProp.GetString() : idProp.GetRawText();
        }

        if (!root.TryGetProperty("method", out var methodProp) || methodProp.ValueKind != JsonValueKind.String)
        {
            error = "Missing required string property 'method'.";
            return false;
        }

        method = methodProp.GetString() ?? string.Empty;

        if (root.TryGetProperty("params", out var paramsProp))
        {
            rawParams = paramsProp.Clone();
        }

        return true;
    }

    private static T? DeserializeParams<T>(
        JsonElement? rawParams,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (rawParams is not { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        return element.Deserialize(typeInfo);
    }

    private static string SerializeOk<T>(
        string? id,
        T payload,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var element = JsonSerializer.SerializeToElement(payload, typeInfo);
        var response = new RpcResponse(id, Ok: true, Result: element, Error: null);
        return JsonSerializer.Serialize(response, CliJsonContext.Default.RpcResponse);
    }

    private static string SerializeError(string? id, string code, string message, string? detail)
    {
        var response = new RpcResponse(id, Ok: false, Result: null, Error: new RpcError(code, message, detail));
        return JsonSerializer.Serialize(response, CliJsonContext.Default.RpcResponse);
    }
}
