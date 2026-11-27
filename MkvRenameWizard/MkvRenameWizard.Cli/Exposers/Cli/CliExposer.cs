using System.Globalization;
using System.Text.Json;
using MkvRenameWizard.Cli.Protocol;
using MkvRenameWizard.Core.Models.Categories;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.TvMaze;
using MkvRenameWizard.Core.Services.Aggregations;

namespace MkvRenameWizard.Cli.Exposers.Cli;

internal sealed class CliExposer(IMkvRenameWizardService coreService) : ICliExposer
{
    public async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default,
        TextReader? input = null)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            WriteUsage(output);
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args.Length > 1 ? args[1..] : Array.Empty<string>();

        try
        {
            return command switch
            {
                "search" => await RunSearchAsync(rest, output, error, cancellationToken),
                "seasons" => await RunSeasonsAsync(rest, output, error, cancellationToken),
                "episodes" => await RunEpisodesAsync(rest, output, error, cancellationToken),
                "scan" => RunScan(rest, output, error),
                "tokens" => RunTokens(rest, output),
                "validate-pattern" => RunValidatePattern(rest, output, error),
                "plan" => await RunPlanAsync(rest, output, error, input, cancellationToken),
                "apply" => await RunApplyAsync(rest, output, error),
                "undo" => await RunUndoAsync(rest, output, error),
                "sessions" => await RunSessionsAsync(rest, output, error),
                "rename" => await RunRenameAsync(rest, output, error, input, cancellationToken),
                _ => WriteUnknownCommand(command, error)
            };
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Cancelled.");
            return 130;
        }
        catch (Exception exception) when (exception is IValidationCategory)
        {
            await error.WriteLineAsync($"Validation error: {exception.InnerException?.Message ?? exception.Message}");
            return 2;
        }
        catch (Exception exception) when (exception is IDependencyCategory)
        {
            await error.WriteLineAsync($"Dependency error: {exception.InnerException?.Message ?? exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is IServiceCategory)
        {
            await error.WriteLineAsync($"Service error: {exception.InnerException?.Message ?? exception.Message}");
            return 1;
        }
    }

    private async Task<int> RunSearchAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var asJson = HasFlag(args, "--json");
        var query = string.Join(' ', args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)));
        if (string.IsNullOrWhiteSpace(query))
        {
            await error.WriteLineAsync("Usage: mkv-rename-cli search <query> [--json]");
            return 2;
        }

        var shows = await coreService.SearchShowsByNameAsync(query, cancellationToken);
        if (asJson)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(shows, CliJsonContext.Default.IReadOnlyListShow));
            return 0;
        }

        foreach (var show in shows)
        {
            var years = string.IsNullOrEmpty(show.YearRange) ? string.Empty : $" ({show.YearRange})";
            await output.WriteLineAsync($"[{show.Id}] {show.Name}{years}");
        }

        return 0;
    }

    private async Task<int> RunSeasonsAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var asJson = HasFlag(args, "--json");
        var idArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        if (!long.TryParse(idArg, CultureInfo.InvariantCulture, out var showId))
        {
            await error.WriteLineAsync("Usage: mkv-rename-cli seasons <showId> [--json]");
            return 2;
        }

        var seasons = await coreService.RetrieveSeasonsByShowIdAsync(showId, cancellationToken);
        if (asJson)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(seasons, CliJsonContext.Default.IReadOnlyListSeason));
            return 0;
        }

        foreach (var season in seasons)
        {
            await output.WriteLineAsync($"[{season.Id}] Season {season.Number} ({season.TotalEpisodeCount} episodes) {season.Name}".TrimEnd());
        }

        return 0;
    }

    private async Task<int> RunEpisodesAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var asJson = HasFlag(args, "--json");
        var idArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        if (!long.TryParse(idArg, CultureInfo.InvariantCulture, out var seasonId))
        {
            await error.WriteLineAsync("Usage: mkv-rename-cli episodes <seasonId> [--json]");
            return 2;
        }

        var episodes = await coreService.RetrieveEpisodesBySeasonIdAsync(seasonId, cancellationToken);
        if (asJson)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(episodes, CliJsonContext.Default.IReadOnlyListEpisode));
            return 0;
        }

        foreach (var ep in episodes)
        {
            var num = ep.EpisodeNumber is { } n ? $"S{ep.Season:D2}E{n:D2}" : $"S{ep.Season:D2}E??";
            await output.WriteLineAsync($"{num} {ep.Name}");
        }

        return 0;
    }

    private int RunScan(string[] args, TextWriter output, TextWriter error)
    {
        var asJson = HasFlag(args, "--json");
        var paths = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        if (paths.Length == 0)
        {
            error.WriteLine("Usage: mkv-rename-cli scan <path...> [--json]");
            return 2;
        }

        var result = coreService.ImportMkvFilesFromPaths(paths);
        if (asJson)
        {
            output.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.FileImportResult));
            return 0;
        }

        foreach (var file in result.ImportedFiles)
        {
            output.WriteLine(file.FullPath);
        }

        foreach (var issue in result.FileImportIssues)
        {
            error.WriteLine($"[issue:{issue.FileImportIssueType}] {issue.DisplayName}: {issue.Reason}");
        }

        return 0;
    }

    private int RunTokens(string[] args, TextWriter output)
    {
        var asJson = HasFlag(args, "--json");
        var tokens = coreService.RetrieveAvailableTokens();
        if (asJson)
        {
            output.WriteLine(JsonSerializer.Serialize(tokens, CliJsonContext.Default.IReadOnlyListPatternToken));
            return 0;
        }

        foreach (var token in tokens)
        {
            output.WriteLine($"{{{token.Name}}}\t{token.Description}\t(e.g. {token.ExampleValue})");
        }

        return 0;
    }

    private int RunValidatePattern(string[] args, TextWriter output, TextWriter error)
    {
        var asJson = HasFlag(args, "--json");
        var pattern = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        if (pattern is null)
        {
            error.WriteLine("Usage: mkv-rename-cli validate-pattern <pattern> [--json]");
            return 2;
        }

        var result = coreService.ValidatePattern(pattern);
        if (asJson)
        {
            output.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.PatternValidationResult));
            return result.IsValid ? 0 : 2;
        }

        if (result.IsValid)
        {
            output.WriteLine("Pattern is valid.");
            return 0;
        }

        foreach (var err in result.Errors)
        {
            var hint = err.Suggestion is null ? string.Empty : $" (did you mean '{{{err.Suggestion}}}'?)";
            error.WriteLine($"Unknown token '{{{err.TokenName}}}' at column {err.Column}{hint}");
        }

        return 2;
    }

    private async Task<int> RunPlanAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        TextReader? input,
        CancellationToken cancellationToken)
    {
        var asJson = HasFlag(args, "--json");
        var built = await BuildPlanFromArgsAsync(args, "plan", error, input, cancellationToken);
        if (built.ExitCode != 0 || built.Plan is null || built.Request is null)
        {
            return built.ExitCode;
        }

        var now = DateTimeOffset.UtcNow;
        var session = new CliSessionState(
            SessionId: Guid.NewGuid(),
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            AppliedAtUtc: null,
            UndoneAtUtc: null,
            Status: CliSessionStatus.Planned,
            Request: built.Request,
            Plan: built.Plan);

        var stateDir = ResolveStateDirectory(args);
        var stateFilePath = SaveSessionState(stateDir, session);

        if (asJson)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(session, CliJsonContext.Default.CliSessionState));
        }
        else
        {
            await WritePlanItemsAsync(output, session.Plan.Items);
            await output.WriteLineAsync(
                $"Plan (dry run): {session.Plan.ReadyCount} ready, {session.Plan.ConflictCount} conflict(s), {session.Plan.SkippedCount} skipped, {session.Plan.DoneCount} done.");
            await output.WriteLineAsync($"Session: {session.SessionId}");
            await output.WriteLineAsync($"State:   {stateFilePath}");
            await output.WriteLineAsync($"Apply:   mkv-rename-cli apply {session.SessionId} --yes");
        }

        return !session.Plan.IsPatternValid || session.Plan.ConflictCount > 0 ? 2 : 0;
    }

    private async Task<int> RunApplyAsync(string[] args, TextWriter output, TextWriter error)
    {
        var asJson = HasFlag(args, "--json");
        var execute = HasFlag(args, "--yes") || HasFlag(args, "--apply");
        var target = ExtractPositionalArgs(args).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(target))
        {
            await error.WriteLineAsync("Usage: mkv-rename-cli apply <sessionId | state-file.json> [--yes | --apply] [--state-dir <dir>] [--json]");
            return 2;
        }

        var stateDir = ResolveStateDirectory(args);
        var loaded = TryLoadSessionState(stateDir, target, out var session, out var stateFilePath, out var loadError);
        if (!loaded || session is null || stateFilePath is null)
        {
            await error.WriteLineAsync(loadError ?? $"Session '{target}' not found.");
            return 2;
        }

        if (!session.Plan.IsPatternValid || session.Plan.ConflictCount > 0)
        {
            await error.WriteLineAsync($"Session {session.SessionId} has pattern errors or conflicts and cannot be applied.");
            return 2;
        }

        if (session.Status == CliSessionStatus.Applied)
        {
            await error.WriteLineAsync($"Session {session.SessionId} was already applied at {session.AppliedAtUtc:O}.");
            return 2;
        }

        if (!execute)
        {
            if (asJson)
            {
                var drySummary = new CliRenameSummary(true, session.Plan, null, session.SessionId, stateFilePath);
                await output.WriteLineAsync(JsonSerializer.Serialize(drySummary, CliJsonContext.Default.CliRenameSummary));
            }
            else
            {
                await WritePlanItemsAsync(output, session.Plan.Items);
                await output.WriteLineAsync(
                    $"Dry run for session {session.SessionId}: {session.Plan.ReadyCount} ready, {session.Plan.ConflictCount} conflict(s), {session.Plan.SkippedCount} skipped, {session.Plan.DoneCount} done. Pass --yes to apply.");
            }

            return 0;
        }

        var operations = BuildReadyOperations(session.Plan);

        var results = operations.Length > 0
            ? coreService.ExecuteRenameOperations(operations)
            : Array.Empty<RenameOperationResult>();

        var now = DateTimeOffset.UtcNow;
        var allSucceeded = results.All(r => r.IsSuccessful);
        var updated = session with
        {
            UpdatedAtUtc = now,
            AppliedAtUtc = now,
            Status = allSucceeded ? CliSessionStatus.Applied : CliSessionStatus.Failed,
            Results = results
        };

        SaveSessionStateToPath(stateFilePath, updated);

        if (asJson)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(updated, CliJsonContext.Default.CliSessionState));
        }
        else
        {
            var okCount = await WriteOperationResultsAsync(output, results, "OK", "FAIL");
            var failCount = results.Count - okCount;
            await output.WriteLineAsync($"Applied session {updated.SessionId}: {okCount} renamed, {failCount} failed.");
            if (okCount > 0)
            {
                await output.WriteLineAsync($"Undo:    mkv-rename-cli undo {updated.SessionId} --yes");
            }
        }

        return allSucceeded ? 0 : 1;
    }

    private async Task<int> RunUndoAsync(string[] args, TextWriter output, TextWriter error)
    {
        var asJson = HasFlag(args, "--json");
        var execute = HasFlag(args, "--yes") || HasFlag(args, "--apply");
        var target = ExtractPositionalArgs(args).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(target))
        {
            await error.WriteLineAsync("Usage: mkv-rename-cli undo <sessionId | state-file.json> [--yes | --apply] [--state-dir <dir>] [--json]");
            return 2;
        }

        var stateDir = ResolveStateDirectory(args);
        var loaded = TryLoadSessionState(stateDir, target, out var session, out var stateFilePath, out var loadError);
        if (!loaded || session is null || stateFilePath is null)
        {
            await error.WriteLineAsync(loadError ?? $"Session '{target}' not found.");
            return 2;
        }

        if (session.Status == CliSessionStatus.Undone)
        {
            await error.WriteLineAsync($"Session {session.SessionId} was already undone at {session.UndoneAtUtc:O}.");
            return 2;
        }

        if (session.Results is null || !session.Results.Any(r => r.IsSuccessful))
        {
            await error.WriteLineAsync($"Session {session.SessionId} has no applied operations to undo.");
            return 2;
        }

        if (!execute)
        {
            if (asJson)
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(session, CliJsonContext.Default.CliSessionState));
            }
            else
            {
                var reversible = session.Results.Where(r => r.IsSuccessful && r.TargetPath is not null).Reverse().ToList();
                foreach (var r in reversible)
                {
                    await output.WriteLineAsync($"[UNDO DRY-RUN] {Path.GetFileName(r.TargetPath)} -> {Path.GetFileName(r.SourcePath)}");
                }

                await output.WriteLineAsync($"Dry run undo for session {session.SessionId}: {reversible.Count} file(s) would be restored. Pass --yes to undo.");
            }

            return 0;
        }

        var undoResults = coreService.UndoRenameOperations(session.Results);
        var now = DateTimeOffset.UtcNow;
        var allSucceeded = undoResults.All(r => r.IsSuccessful);
        var updated = session with
        {
            UpdatedAtUtc = now,
            UndoneAtUtc = now,
            Status = allSucceeded ? CliSessionStatus.Undone : CliSessionStatus.Failed,
            UndoResults = undoResults
        };

        SaveSessionStateToPath(stateFilePath, updated);

        if (asJson)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(updated, CliJsonContext.Default.CliSessionState));
        }
        else
        {
            var okCount = await WriteOperationResultsAsync(output, undoResults, "UNDONE", "UNDO FAIL");
            var failCount = undoResults.Count - okCount;
            await output.WriteLineAsync($"Undone session {updated.SessionId}: {okCount} restored, {failCount} failed.");
        }

        return allSucceeded ? 0 : 1;
    }

    private async Task<int> RunSessionsAsync(string[] args, TextWriter output, TextWriter error)
    {
        var asJson = HasFlag(args, "--json");
        var stateDir = ResolveStateDirectory(args);
        var positional = ExtractPositionalArgs(args);
        var subcommand = positional.Count > 0 ? positional[0].ToLowerInvariant() : "list";

        if (subcommand == "show")
        {
            var target = positional.Count > 1 ? positional[1] : null;
            if (string.IsNullOrWhiteSpace(target))
            {
                await error.WriteLineAsync("Usage: mkv-rename-cli sessions show <sessionId | state-file.json> [--state-dir <dir>] [--json]");
                return 2;
            }

            if (!TryLoadSessionState(stateDir, target, out var session, out var stateFilePath, out var loadError) || session is null)
            {
                await error.WriteLineAsync(loadError ?? $"Session '{target}' not found.");
                return 2;
            }

            if (asJson)
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(session, CliJsonContext.Default.CliSessionState));
                return 0;
            }

            await output.WriteLineAsync($"Session: {session.SessionId}");
            await output.WriteLineAsync($"Status:  {session.Status}");
            await output.WriteLineAsync($"Created: {session.CreatedAtUtc:O}");
            if (session.AppliedAtUtc is not null)
            {
                await output.WriteLineAsync($"Applied: {session.AppliedAtUtc:O}");
            }

            if (session.UndoneAtUtc is not null)
            {
                await output.WriteLineAsync($"Undone:  {session.UndoneAtUtc:O}");
            }

            await output.WriteLineAsync($"Show:    {session.Request.ShowName}");
            await output.WriteLineAsync($"Pattern: {session.Request.Pattern}");
            await output.WriteLineAsync($"State:   {stateFilePath}");
            await WritePlanItemsAsync(output, session.Plan.Items, indent: "  ");

            return 0;
        }

        if (subcommand != "list")
        {
            await error.WriteLineAsync("Usage: mkv-rename-cli sessions [list | show <sessionId>] [--state-dir <dir>] [--json]");
            return 2;
        }

        var summaries = new List<CliSessionSummary>();
        if (Directory.Exists(stateDir))
        {
            foreach (var file in Directory.EnumerateFiles(stateDir, "*.json"))
            {
                var text = File.ReadAllText(file);
                if (!text.TrimStart().StartsWith('{'))
                {
                    continue;
                }

                var session = JsonSerializer.Deserialize(text, CliJsonContext.Default.CliSessionState);
                if (session is not null)
                {
                    summaries.Add(new CliSessionSummary(
                        session.SessionId,
                        session.CreatedAtUtc,
                        session.UpdatedAtUtc,
                        session.Status,
                        session.Request.ShowName,
                        session.Request.Pattern,
                        session.Plan.ReadyCount,
                        session.Plan.ConflictCount,
                        file));
                }
            }
        }

        summaries.Sort((a, b) => b.CreatedAtUtc.CompareTo(a.CreatedAtUtc));

        if (asJson)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize<IReadOnlyList<CliSessionSummary>>(summaries, CliJsonContext.Default.IReadOnlyListCliSessionSummary));
            return 0;
        }

        if (summaries.Count == 0)
        {
            await output.WriteLineAsync($"No sessions found in {stateDir}.");
            return 0;
        }

        foreach (var s in summaries)
        {
            await output.WriteLineAsync($"{s.SessionId}  {s.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}Z  {s.Status,-8}  {s.ShowName} ({s.ReadyCount} ready)");
        }

        return 0;
    }

    private async Task<int> RunRenameAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        TextReader? input,
        CancellationToken cancellationToken)
    {
        var asJson = HasFlag(args, "--json");
        var apply = HasFlag(args, "--apply") || HasFlag(args, "--yes");

        var built = await BuildPlanFromArgsAsync(args, "rename", error, input, cancellationToken);
        if (built.ExitCode != 0 || built.Plan is null || built.Request is null)
        {
            return built.ExitCode;
        }

        var plan = built.Plan;
        IReadOnlyList<RenameOperationResult>? executionResults = null;
        Guid? sessionId = null;
        string? stateFilePath = null;

        if (apply && plan.IsPatternValid && plan.ConflictCount == 0 && plan.ReadyCount > 0)
        {
            var operations = BuildReadyOperations(plan);

            executionResults = coreService.ExecuteRenameOperations(operations);

            var now = DateTimeOffset.UtcNow;
            var allSucceeded = executionResults.All(r => r.IsSuccessful);
            var session = new CliSessionState(
                SessionId: Guid.NewGuid(),
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                AppliedAtUtc: now,
                UndoneAtUtc: null,
                Status: allSucceeded ? CliSessionStatus.Applied : CliSessionStatus.Failed,
                Request: built.Request,
                Plan: plan,
                Results: executionResults);

            var stateDir = ResolveStateDirectory(args);
            stateFilePath = SaveSessionState(stateDir, session);
            sessionId = session.SessionId;
        }

        if (asJson)
        {
            var summary = new CliRenameSummary(!apply, plan, executionResults, sessionId, stateFilePath);
            await output.WriteLineAsync(JsonSerializer.Serialize(summary, CliJsonContext.Default.CliRenameSummary));
        }
        else
        {
            await WritePlanItemsAsync(output, plan.Items);

            if (!apply)
            {
                await output.WriteLineAsync($"Dry run: {plan.ReadyCount} ready, {plan.ConflictCount} conflict(s), {plan.SkippedCount} skipped, {plan.DoneCount} done. Pass --apply to rename.");
            }
            else if (executionResults is not null)
            {
                var okCount = executionResults.Count(r => r.IsSuccessful);
                var failCount = executionResults.Count - okCount;
                await output.WriteLineAsync($"Renamed {okCount} file(s), {failCount} failed.");
                if (sessionId is not null)
                {
                    await output.WriteLineAsync($"Session: {sessionId}");
                    await output.WriteLineAsync($"Undo:    mkv-rename-cli undo {sessionId} --yes");
                }
            }
        }

        if (!plan.IsPatternValid || plan.ConflictCount > 0)
        {
            return 2;
        }

        if (executionResults is not null && executionResults.Any(r => !r.IsSuccessful))
        {
            return 1;
        }

        return 0;
    }

    private async Task<CliBuiltPlan> BuildPlanFromArgsAsync(
        string[] args,
        string commandName,
        TextWriter error,
        TextReader? input,
        CancellationToken cancellationToken)
    {
        var showNameArg = GetOptionValue(args, "--show");
        var showIdArg = GetOptionValue(args, "--show-id");
        var seasonsArg = GetOptionValue(args, "--season");
        var pattern = GetOptionValue(args, "--pattern") ?? TokenNames.DefaultPattern;
        var prefix = GetOptionValue(args, "--prefix") ?? string.Empty;
        var caseArg = GetOptionValue(args, "--case") ?? "default";
        var targetDir = GetOptionValue(args, "--output");
        var orderArg = GetOptionValue(args, "--order");
        var inputArg = GetOptionValue(args, "--input");
        var orderJsonArg = GetOptionValue(args, "--order-json");

        var paths = ExtractPositionalArgs(args);

        CliOrderInput? orderInput = null;
        if (!string.IsNullOrWhiteSpace(inputArg) || !string.IsNullOrWhiteSpace(orderJsonArg))
        {
            if (!TryLoadOrderInput(inputArg, orderJsonArg, input, out orderInput, out var orderError))
            {
                await error.WriteLineAsync(orderError ?? "Invalid JSON order input.");
                return new CliBuiltPlan(2, null, null);
            }
        }

        var hasJsonSources = orderInput is not null && ((orderInput.Files?.Count ?? 0) > 0 || (orderInput.Pairs?.Count ?? 0) > 0);
        if ((paths.Count == 0 && !hasJsonSources) || (string.IsNullOrWhiteSpace(showNameArg) && string.IsNullOrWhiteSpace(showIdArg)))
        {
            await error.WriteLineAsync($"Usage: mkv-rename-cli {commandName} <path...> (--show <name> | --show-id <id>) --season <num[,num...]> [--pattern <pat>] [--prefix <pre>] [--case default|snake|pascal|camel] [--output <dir>] [--order 2,1,3] [--input <file.json | ->] [--order-json <json>] [--state-dir <dir>] [--json]");
            return new CliBuiltPlan(2, null, null);
        }

        if (!TryParseCaseStyle(caseArg, out var caseStyle))
        {
            await error.WriteLineAsync($"Unknown case style '{caseArg}'. Valid values: default, snake, pascal, camel.");
            return new CliBuiltPlan(2, null, null);
        }

        long showId;
        string resolvedShowName;
        if (!string.IsNullOrWhiteSpace(showIdArg))
        {
            if (!long.TryParse(showIdArg, CultureInfo.InvariantCulture, out showId))
            {
                await error.WriteLineAsync($"Invalid --show-id '{showIdArg}'.");
                return new CliBuiltPlan(2, null, null);
            }

            resolvedShowName = showNameArg ?? string.Empty;
        }
        else
        {
            var matches = await coreService.SearchShowsByNameAsync(showNameArg!, cancellationToken);
            if (matches.Count == 0)
            {
                await error.WriteLineAsync($"No TVMaze show found matching '{showNameArg}'.");
                return new CliBuiltPlan(1, null, null);
            }

            showId = matches[0].Id;
            resolvedShowName = matches[0].Name;
        }

        var allSeasons = await coreService.RetrieveSeasonsByShowIdAsync(showId, cancellationToken);
        var selectedSeasonNumbers = ParseSeasonNumbers(seasonsArg);
        var matchedSeasons = selectedSeasonNumbers.Count == 0
            ? allSeasons
            : allSeasons.Where(s => selectedSeasonNumbers.Contains(s.Number)).ToList();

        if (matchedSeasons.Count == 0)
        {
            await error.WriteLineAsync("No matching seasons found.");
            return new CliBuiltPlan(1, null, null);
        }

        var episodes = new List<Episode>();
        foreach (var season in matchedSeasons)
        {
            var seasonEpisodes = await coreService.RetrieveEpisodesBySeasonIdAsync(season.Id, cancellationToken);
            episodes.AddRange(seasonEpisodes);
        }

        RenameCandidate[] candidates;
        if (orderInput?.Pairs is { Count: > 0 } pairs)
        {
            var candidateList = new List<RenameCandidate>(pairs.Count);
            foreach (var pair in pairs)
            {
                Episode? ep = null;
                if (pair.EpisodeIndex is { } epIdx)
                {
                    if (epIdx < 1 || epIdx > episodes.Count)
                    {
                        await error.WriteLineAsync($"Invalid episodeIndex {epIdx} in JSON pairs (must be between 1 and {episodes.Count}).");
                        return new CliBuiltPlan(2, null, null);
                    }

                    ep = episodes[epIdx - 1];
                }
                else if (pair.EpisodeNumber is { } epNum)
                {
                    ep = episodes.FirstOrDefault(e =>
                        e.EpisodeNumber == epNum && (pair.Season is null || e.Season == pair.Season));
                }

                if (ep is null)
                {
                    await error.WriteLineAsync($"Could not match episode for '{pair.SourcePath}' in JSON pairs.");
                    return new CliBuiltPlan(2, null, null);
                }

                candidateList.Add(new RenameCandidate(pair.SourcePath, ep.Season, ep.EpisodeNumber, ep.Name, ep.RunTime, ep.AirDate));
            }

            candidates = candidateList.ToArray();
        }
        else
        {
            List<string> orderedFilePaths;
            if (orderInput?.Files is { Count: > 0 } jsonFiles)
            {
                orderedFilePaths = jsonFiles.ToList();
            }
            else
            {
                var import = coreService.ImportMkvFilesFromPaths(paths);
                orderedFilePaths = import.ImportedFiles.Select(f => f.FullPath).ToList();
            }

            string? permError = null;
            var permutation = !string.IsNullOrWhiteSpace(orderArg)
                ? ParseOrderArg(orderArg, out permError)
                : (orderInput?.Order is { Count: > 0 } jsonOrder ? jsonOrder.ToList() : null);

            if (!string.IsNullOrWhiteSpace(orderArg) && permutation is null)
            {
                await error.WriteLineAsync(permError ?? $"Invalid --order '{orderArg}'.");
                return new CliBuiltPlan(2, null, null);
            }

            if (permutation is not null)
            {
                if (!TryApplyOrder(orderedFilePaths, permutation, out var reordered, out var applyError))
                {
                    await error.WriteLineAsync(applyError ?? "Invalid file order.");
                    return new CliBuiltPlan(2, null, null);
                }

                orderedFilePaths = reordered;
            }

            var pairCount = Math.Min(orderedFilePaths.Count, episodes.Count);
            candidates = new RenameCandidate[pairCount];
            for (var i = 0; i < pairCount; i++)
            {
                var ep = episodes[i];
                candidates[i] = new RenameCandidate(orderedFilePaths[i], ep.Season, ep.EpisodeNumber, ep.Name, ep.RunTime, ep.AirDate);
            }
        }

        var request = new RenamePlanRequest(
            resolvedShowName,
            pattern,
            prefix,
            caseStyle,
            targetDir,
            candidates);

        var plan = coreService.BuildRenamePlan(request);
        return new CliBuiltPlan(0, request, plan);
    }

    private static string ResolveStateDirectory(string[] args)
    {
        var explicitDir = GetOptionValue(args, "--state-dir");
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            return explicitDir;
        }

        var envDir = Environment.GetEnvironmentVariable("MKV_RENAME_STATE_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            return envDir;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".mkv-rename-wizard", "sessions");
    }

    private static string SaveSessionState(string stateDir, CliSessionState session)
    {
        var filePath = Path.Combine(stateDir, $"{session.SessionId:D}.json");
        SaveSessionStateToPath(filePath, session);
        return filePath;
    }

    private static void SaveSessionStateToPath(string filePath, CliSessionState session)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(session, CliJsonContext.Default.CliSessionState);
        File.WriteAllText(filePath, json);
    }

    private static bool TryLoadSessionState(
        string stateDir,
        string target,
        out CliSessionState? session,
        out string? stateFilePath,
        out string? errorMessage)
    {
        session = null;
        errorMessage = null;

        if (File.Exists(target))
        {
            stateFilePath = Path.GetFullPath(target);
        }
        else if (Guid.TryParse(target, out var guid))
        {
            stateFilePath = Path.Combine(stateDir, $"{guid:D}.json");
        }
        else
        {
            stateFilePath = Path.Combine(stateDir, target.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? target : $"{target}.json");
        }

        if (!File.Exists(stateFilePath))
        {
            errorMessage = $"Session state file not found: {stateFilePath}";
            return false;
        }

        var json = File.ReadAllText(stateFilePath);
        if (!json.TrimStart().StartsWith('{'))
        {
            errorMessage = $"Session state file is not a valid JSON object: {stateFilePath}";
            return false;
        }

        session = JsonSerializer.Deserialize(json, CliJsonContext.Default.CliSessionState);
        if (session is null)
        {
            errorMessage = $"Session state file is empty or invalid: {stateFilePath}";
            return false;
        }

        return true;
    }

    private static bool TryLoadOrderInput(
        string? inputArg,
        string? orderJsonArg,
        TextReader? stdin,
        out CliOrderInput? orderInput,
        out string? errorMessage)
    {
        orderInput = null;
        errorMessage = null;

        string jsonText;
        if (!string.IsNullOrWhiteSpace(orderJsonArg))
        {
            jsonText = orderJsonArg;
        }
        else if (string.Equals(inputArg, "-", StringComparison.Ordinal))
        {
            jsonText = (stdin ?? Console.In).ReadToEnd();
        }
        else if (!string.IsNullOrWhiteSpace(inputArg))
        {
            if (!File.Exists(inputArg))
            {
                errorMessage = $"Order input file not found: {inputArg}";
                return false;
            }

            jsonText = File.ReadAllText(inputArg);
        }
        else
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(jsonText) || !jsonText.TrimStart().StartsWith('{'))
        {
            errorMessage = "JSON order input must be a non-empty JSON object.";
            return false;
        }

        orderInput = JsonSerializer.Deserialize(jsonText, CliJsonContext.Default.CliOrderInput);
        if (orderInput is null)
        {
            errorMessage = "JSON order input could not be deserialized.";
            return false;
        }

        return true;
    }

    private static List<int>? ParseOrderArg(string orderArg, out string? errorMessage)
    {
        errorMessage = null;
        var parts = orderArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            errorMessage = "Option --order requires at least one 1-based index (e.g. --order 2,1,3).";
            return null;
        }

        var indices = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, CultureInfo.InvariantCulture, out var idx) || idx < 1)
            {
                errorMessage = $"Invalid index '{part}' in --order (expected 1-based positive integers).";
                return null;
            }

            indices.Add(idx);
        }

        return indices;
    }

    private static bool TryApplyOrder(
        List<string> files,
        List<int> oneBasedOrder,
        out List<string> reordered,
        out string? errorMessage)
    {
        reordered = new List<string>(oneBasedOrder.Count);
        errorMessage = null;
        var seen = new HashSet<int>();

        foreach (var oneBased in oneBasedOrder)
        {
            if (oneBased < 1 || oneBased > files.Count)
            {
                errorMessage = $"Order index {oneBased} is out of range (1..{files.Count}).";
                return false;
            }

            if (!seen.Add(oneBased))
            {
                errorMessage = $"Duplicate index {oneBased} in order.";
                return false;
            }

            reordered.Add(files[oneBased - 1]);
        }

        return true;
    }

    private static async Task WritePlanItemsAsync(
        TextWriter output,
        IReadOnlyList<RenamePlanItem> items,
        string indent = "")
    {
        foreach (var item in items)
        {
            await output.WriteLineAsync(
                $"{indent}[{item.Status}] {item.SourceFileName} -> {item.TargetFileName ?? "(none)"}");
        }
    }

    private static RenameFileOperation[] BuildReadyOperations(RenamePlan plan) =>
        plan.Items
            .Where(item => item.Status == RenamePreviewStatus.Ready && item.TargetPath is not null)
            .Select(item => new RenameFileOperation(item.OperationId, item.SourcePath, item.TargetPath))
            .ToArray();

    private static async Task<int> WriteOperationResultsAsync(
        TextWriter output,
        IReadOnlyList<RenameOperationResult> results,
        string okTag,
        string failTag)
    {
        var okCount = 0;
        foreach (var result in results)
        {
            if (result.IsSuccessful)
            {
                okCount++;
            }

            var statusTag = result.IsSuccessful ? okTag : failTag;
            var suffix = result.IsSuccessful
                ? Path.GetFileName(result.TargetPath) ?? "(none)"
                : result.ErrorMessage ?? "unknown error";
            await output.WriteLineAsync($"[{statusTag}] {Path.GetFileName(result.SourcePath)} -> {suffix}");
        }

        return okCount;
    }

    private static int WriteUnknownCommand(string command, TextWriter error)
    {
        error.WriteLine($"Unknown command '{command}'. Run 'mkv-rename-cli --help' for usage.");
        return 2;
    }

    private void WriteUsage(TextWriter output)
    {
        output.WriteLine("mkv-rename-cli: Rename MakeMKV TV show rips using TVMaze metadata");
        output.WriteLine();
        output.WriteLine("Commands:");
        output.WriteLine("  help / --help                      Show all commands and template syntax");
        output.WriteLine("  serve --stdio                      Start long-lived NDJSON RPC loop for TUI clients");
        output.WriteLine("  search <query> [--json]            Search TVMaze shows");
        output.WriteLine("  seasons <showId> [--json]          List seasons for a show");
        output.WriteLine("  episodes <seasonId> [--json]       List episodes for a season");
        output.WriteLine("  scan <path...> [--json]            Discover and validate MKV files");
        output.WriteLine("  tokens [--json]                    List available filename pattern tokens");
        output.WriteLine("  validate-pattern <pat> [--json]    Validate a filename pattern");
        output.WriteLine("  plan <path...> [options]           Dry-run plan & save a GUID session state file");
        output.WriteLine("  apply <sessionId|file> [--yes]     Preview (default) or apply a saved session");
        output.WriteLine("  undo <sessionId|file> [--yes]      Preview (default) or undo an applied session");
        output.WriteLine("  sessions [list|show <id>]          List or inspect saved sessions (~/.mkv-rename-wizard/sessions)");
        output.WriteLine("  rename <path...> [options]         One-shot preview (default) or --apply rename");
        output.WriteLine();
        output.WriteLine("Reordering & State Options (plan / rename):");
        output.WriteLine("  --order <2,1,3>                    Reorder scanned files by 1-based index");
        output.WriteLine("  --input <file.json | ->            Load custom file order or pairs JSON (or '-' for stdin)");
        output.WriteLine("  --order-json <json>                Inline JSON blob for custom file order or pairs");
        output.WriteLine("  --state-dir <dir>                  Override session state dir (default: ~/.mkv-rename-wizard/sessions)");
        output.WriteLine();
        output.WriteLine($"Template Syntax: wrap tokens in {{braces}}, default: \"{TokenNames.DefaultPattern}\"");
        output.WriteLine("  Token        Description                Example");
        foreach (var token in coreService.RetrieveAvailableTokens())
        {
            output.WriteLine($"  {$"{{{token.Name}}}",-12} {token.Description,-26} {token.ExampleValue}");
        }
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetOptionValue(string[] args, string option)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], option, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static List<string> ExtractPositionalArgs(string[] args)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--show" or "--show-id" or "--season" or "--pattern" or "--prefix" or "--case" or "--output"
                or "--order" or "--input" or "--order-json" or "--state-dir")
            {
                i++;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(arg);
        }

        return result;
    }

    private static HashSet<int> ParseSeasonNumbers(string? seasonsArg)
    {
        var set = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(seasonsArg))
        {
            return set;
        }

        foreach (var part in seasonsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, CultureInfo.InvariantCulture, out var num))
            {
                set.Add(num);
            }
        }

        return set;
    }

    private static bool TryParseCaseStyle(string value, out CaseStyle style)
    {
        switch (value.ToLowerInvariant())
        {
            case "default":
                style = CaseStyle.Default;
                return true;
            case "snake":
            case "snakecase":
                style = CaseStyle.SnakeCase;
                return true;
            case "pascal":
            case "pascalcase":
                style = CaseStyle.PascalCase;
                return true;
            case "camel":
            case "camelcase":
                style = CaseStyle.CamelCase;
                return true;
            default:
                style = CaseStyle.Default;
                return false;
        }
    }
}
