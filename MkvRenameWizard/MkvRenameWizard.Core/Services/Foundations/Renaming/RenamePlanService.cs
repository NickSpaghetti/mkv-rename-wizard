using System.Diagnostics;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Diagnostics;
using MkvRenameWizard.Core.Models.Renaming;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal sealed partial class RenamePlanService(ILoggingBroker loggingBroker) : IRenamePlanService
{
    public IReadOnlyList<PatternToken> RetrieveAvailableTokens() => ValidTokens;

    public PatternValidationResult ValidatePattern(string pattern) =>
        TryCatch(() =>
        {
            ValidatePatternInput(pattern);
            return ParseAndValidateCore(pattern);
        });

    public RenamePlan BuildPlan(RenamePlanRequest request) =>
        TryCatch(() =>
        {
            ValidatePlanRequest(request);

            using var activity = CoreTelemetry.ActivitySource.StartActivity("Renaming.BuildPlan");
            var start = Stopwatch.GetTimestamp();

            var validation = ParseAndValidateCore(request.Pattern);
            var segments = validation.Segments;
            var errors = validation.Errors;
            var isPatternValid = validation.IsValid;
            var candidateCount = request.Candidates.Count;

            if (!isPatternValid)
            {
                var errorItems = new RenamePlanItem[candidateCount];
                for (var i = 0; i < candidateCount; i++)
                {
                    var sourcePath = request.Candidates[i].SourcePath ?? string.Empty;
                    var sourceFileName = Path.GetFileName(sourcePath);
                    errorItems[i] = new RenamePlanItem(
                        OperationId: i + 1,
                        SourcePath: sourcePath,
                        SourceFileName: sourceFileName,
                        TargetPath: null,
                        TargetFileName: null,
                        Status: RenamePreviewStatus.PatternError);
                }

                CoreTelemetry.RenamePlanDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);

                return new RenamePlan(
                    IsPatternValid: false,
                    ReadyCount: 0,
                    ConflictCount: 0,
                    SkippedCount: 0,
                    DoneCount: 0,
                    Segments: segments,
                    Errors: errors,
                    Items: errorItems);
            }

            var sanitizedShowName = SanitizeToken(request.ShowName.AsSpan());
            var showNameSpan = sanitizedShowName.AsSpan();
            var prefixSpan = (request.Prefix ?? string.Empty).AsSpan();
            var hasExplicitTargetDir = !string.IsNullOrWhiteSpace(request.TargetDirectory);

            var targetFileNames = candidateCount > 0 ? new string?[candidateCount] : Array.Empty<string?>();
            var targetPaths = candidateCount > 0 ? new string?[candidateCount] : Array.Empty<string?>();
            var sourceNames = candidateCount > 0 ? new string[candidateCount] : Array.Empty<string>();
            var statuses = candidateCount > 0 ? new RenamePreviewStatus[candidateCount] : Array.Empty<RenamePreviewStatus>();
            var targetCounts = new Dictionary<string, int>(candidateCount, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < candidateCount; i++)
            {
                var candidate = request.Candidates[i];
                var sourcePath = candidate.SourcePath ?? string.Empty;
                var sourceFileName = Path.GetFileName(sourcePath);
                sourceNames[i] = sourceFileName;

                if (candidate.EpisodeNumber is null)
                {
                    statuses[i] = RenamePreviewStatus.Skipped;
                    continue;
                }

                var extSpan = Path.GetExtension(sourcePath.AsSpan());
                var runTimeStr = candidate.RunTime > 0
                    ? FormatRunTime(candidate.RunTime)
                    : string.Empty;

                var targetFileName = ApplyTokenValuesCore(
                    segments,
                    candidate,
                    showNameSpan,
                    prefixSpan,
                    extSpan,
                    runTimeStr.AsSpan(),
                    request.CaseStyle,
                    appendExtension: extSpan);

                var targetDir = hasExplicitTargetDir
                    ? request.TargetDirectory!
                    : Path.GetDirectoryName(sourcePath);

                var targetPath = string.IsNullOrEmpty(targetDir)
                    ? targetFileName
                    : Path.Combine(targetDir, targetFileName);

                targetFileNames[i] = targetFileName;
                targetPaths[i] = targetPath;

                var isSameFile = hasExplicitTargetDir
                    ? string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(sourceFileName, targetFileName, StringComparison.OrdinalIgnoreCase);

                if (isSameFile)
                {
                    statuses[i] = RenamePreviewStatus.Done;
                }
                else
                {
                    statuses[i] = RenamePreviewStatus.Ready;
                    targetCounts.TryGetValue(targetPath, out var count);
                    targetCounts[targetPath] = count + 1;
                }
            }

            var readyCount = 0;
            var conflictCount = 0;
            var skippedCount = 0;
            var doneCount = 0;
            var items = new RenamePlanItem[candidateCount];

            for (var i = 0; i < candidateCount; i++)
            {
                var finalStatus = statuses[i];
                var targetPath = targetPaths[i];
                if (finalStatus == RenamePreviewStatus.Ready && targetPath is not null && targetCounts[targetPath] > 1)
                {
                    finalStatus = RenamePreviewStatus.Conflict;
                }

                switch (finalStatus)
                {
                    case RenamePreviewStatus.Ready:
                        readyCount++;
                        break;
                    case RenamePreviewStatus.Conflict:
                        conflictCount++;
                        break;
                    case RenamePreviewStatus.Skipped:
                        skippedCount++;
                        break;
                    case RenamePreviewStatus.Done:
                        doneCount++;
                        break;
                }

                items[i] = new RenamePlanItem(
                    OperationId: i + 1,
                    SourcePath: request.Candidates[i].SourcePath ?? string.Empty,
                    SourceFileName: sourceNames[i],
                    TargetPath: targetPath,
                    TargetFileName: targetFileNames[i],
                    Status: finalStatus);
            }

            CoreTelemetry.RenamePlanDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);

            return new RenamePlan(
                IsPatternValid: true,
                ReadyCount: readyCount,
                ConflictCount: conflictCount,
                SkippedCount: skippedCount,
                DoneCount: doneCount,
                Segments: segments,
                Errors: errors,
                Items: items);
        });
}
