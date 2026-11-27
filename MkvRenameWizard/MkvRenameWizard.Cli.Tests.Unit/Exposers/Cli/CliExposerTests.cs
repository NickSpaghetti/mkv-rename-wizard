using System.Text.Json;
using MkvRenameWizard.Cli.Exposers.Cli;
using MkvRenameWizard.Cli.Protocol;
using MkvRenameWizard.Core.Models.MkvFiles;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.TvMaze;
using MkvRenameWizard.Core.Services.Aggregations;
using Moq;
using Xunit;

namespace MkvRenameWizard.Cli.Tests.Unit.Exposers.Cli;

public sealed class CliExposerTests : IDisposable
{
    private readonly Mock<IMkvRenameWizardService> _coreMock = new();
    private readonly CliExposer _exposer;
    private readonly string _tempStateDir;

    public CliExposerTests()
    {
        _exposer = new CliExposer(_coreMock.Object);
        _tempStateDir = Path.Combine(Path.GetTempPath(), $"mkv-rename-cli-tests-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempStateDir))
        {
            Directory.Delete(_tempStateDir, recursive: true);
        }
    }

    [Fact]
    public async Task ShouldDryRunRenameByDefaultWithoutCallingExecuteAsync()
    {
        SetupTwoEpisodeFixture();

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await _exposer.RunAsync(
            ["rename", "/media", "--show-id", "10", "--season", "1", "--state-dir", _tempStateDir],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("Dry run: 2 ready", stdout.ToString());
        _coreMock.Verify(s => s.ExecuteRenameOperations(It.IsAny<IReadOnlyList<RenameFileOperation>>()), Times.Never);
    }

    [Fact]
    public async Task ShouldRenderDefaultPatternAndAvailableTokensInHelpAsync()
    {
        _coreMock.Setup(s => s.RetrieveAvailableTokens()).Returns([
            new PatternToken(TokenNames.SeasonEpisodePadded, "Season & Episode Padded", "S01E09"),
            new PatternToken(TokenNames.Title, "Episode Title", "Act. 9: Battle to the Limit")
        ]);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await _exposer.RunAsync(["--help"], stdout, stderr);

        var output = stdout.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains(TokenNames.DefaultPattern, output);
        Assert.Contains("{S##E##}", output);
        Assert.Contains("Season & Episode Padded", output);
        Assert.Contains("{Title}", output);
        _coreMock.Verify(s => s.RetrieveAvailableTokens(), Times.Once);
    }

    [Fact]
    public async Task ShouldPlanWithOrderFlagAndSaveSessionStateFileAsync()
    {
        RenamePlanRequest? capturedRequest = null;
        SetupTwoEpisodeFixture(req => capturedRequest = req);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await _exposer.RunAsync(
            ["plan", "/media", "--show-id", "10", "--season", "1", "--order", "2,1", "--state-dir", _tempStateDir, "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedRequest);
        Assert.Equal("/media/title_t01.mkv", capturedRequest!.Candidates[0].SourcePath);
        Assert.Equal(1, capturedRequest.Candidates[0].EpisodeNumber);
        Assert.Equal("/media/title_t00.mkv", capturedRequest.Candidates[1].SourcePath);
        Assert.Equal(2, capturedRequest.Candidates[1].EpisodeNumber);

        var session = JsonSerializer.Deserialize(stdout.ToString(), CliJsonContext.Default.CliSessionState);
        Assert.NotNull(session);
        Assert.NotEqual(Guid.Empty, session!.SessionId);
        Assert.Equal(CliSessionStatus.Planned, session.Status);
        Assert.True(File.Exists(Path.Combine(_tempStateDir, $"{session.SessionId:D}.json")));
        _coreMock.Verify(s => s.ExecuteRenameOperations(It.IsAny<IReadOnlyList<RenameFileOperation>>()), Times.Never);
    }

    [Fact]
    public async Task ShouldAcceptJsonOrderBlobFromStdinAsync()
    {
        RenamePlanRequest? capturedRequest = null;
        SetupTwoEpisodeFixture(req => capturedRequest = req);

        var jsonBlob = """
        {
          "pairs": [
            { "sourcePath": "/media/custom_b.mkv", "season": 1, "episodeNumber": 2 },
            { "sourcePath": "/media/custom_a.mkv", "season": 1, "episodeNumber": 1 }
          ]
        }
        """;

        using var stdin = new StringReader(jsonBlob);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await _exposer.RunAsync(
            ["plan", "--show-id", "10", "--season", "1", "--input", "-", "--state-dir", _tempStateDir, "--json"],
            stdout,
            stderr,
            input: stdin);

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedRequest);
        Assert.Equal(2, capturedRequest!.Candidates.Count);
        Assert.Equal("/media/custom_b.mkv", capturedRequest.Candidates[0].SourcePath);
        Assert.Equal(2, capturedRequest.Candidates[0].EpisodeNumber);
        Assert.Equal("/media/custom_a.mkv", capturedRequest.Candidates[1].SourcePath);
        Assert.Equal(1, capturedRequest.Candidates[1].EpisodeNumber);
    }

    [Fact]
    public async Task ShouldRunPlanApplyDryRunApplyYesAndUndoLifecycleAsync()
    {
        SetupTwoEpisodeFixture();

        _coreMock.Setup(s => s.ExecuteRenameOperations(It.IsAny<IReadOnlyList<RenameFileOperation>>()))
            .Returns((IReadOnlyList<RenameFileOperation> ops) =>
                ops.Select(o => new RenameOperationResult(o.OperationId, o.SourcePath, o.TargetPath, true, null)).ToArray());

        _coreMock.Setup(s => s.UndoRenameOperations(It.IsAny<IReadOnlyList<RenameOperationResult>>()))
            .Returns((IReadOnlyList<RenameOperationResult> res) =>
                res.Reverse().Select(r => new RenameOperationResult(r.OperationId, r.TargetPath!, r.SourcePath, true, null)).ToArray());

        // 1. Plan
        using var planOut = new StringWriter();
        using var err = new StringWriter();
        var planExit = await _exposer.RunAsync(
            ["plan", "/media", "--show-id", "10", "--season", "1", "--state-dir", _tempStateDir, "--json"],
            planOut,
            err);
        Assert.Equal(0, planExit);
        var planned = JsonSerializer.Deserialize(planOut.ToString(), CliJsonContext.Default.CliSessionState)!;

        // 2. Apply without --yes -> dry run
        using var applyDryOut = new StringWriter();
        var applyDryExit = await _exposer.RunAsync(
            ["apply", planned.SessionId.ToString("D"), "--state-dir", _tempStateDir],
            applyDryOut,
            err);
        Assert.Equal(0, applyDryExit);
        Assert.Contains("Dry run for session", applyDryOut.ToString());
        _coreMock.Verify(s => s.ExecuteRenameOperations(It.IsAny<IReadOnlyList<RenameFileOperation>>()), Times.Never);

        // 3. Apply with --yes -> executes and marks Applied
        using var applyOut = new StringWriter();
        var applyExit = await _exposer.RunAsync(
            ["apply", planned.SessionId.ToString("D"), "--yes", "--state-dir", _tempStateDir, "--json"],
            applyOut,
            err);
        Assert.Equal(0, applyExit);
        var applied = JsonSerializer.Deserialize(applyOut.ToString(), CliJsonContext.Default.CliSessionState)!;
        Assert.Equal(CliSessionStatus.Applied, applied.Status);
        Assert.NotNull(applied.AppliedAtUtc);
        Assert.Equal(2, applied.Results!.Count);
        _coreMock.Verify(s => s.ExecuteRenameOperations(It.IsAny<IReadOnlyList<RenameFileOperation>>()), Times.Once);

        // 4. Double-apply is rejected
        using var secondApplyOut = new StringWriter();
        using var secondApplyErr = new StringWriter();
        var secondApplyExit = await _exposer.RunAsync(
            ["apply", planned.SessionId.ToString("D"), "--yes", "--state-dir", _tempStateDir],
            secondApplyOut,
            secondApplyErr);
        Assert.Equal(2, secondApplyExit);
        Assert.Contains("already applied", secondApplyErr.ToString());

        // 5. Undo without --yes -> dry run
        using var undoDryOut = new StringWriter();
        var undoDryExit = await _exposer.RunAsync(
            ["undo", planned.SessionId.ToString("D"), "--state-dir", _tempStateDir],
            undoDryOut,
            err);
        Assert.Equal(0, undoDryExit);
        Assert.Contains("Dry run undo for session", undoDryOut.ToString());
        _coreMock.Verify(s => s.UndoRenameOperations(It.IsAny<IReadOnlyList<RenameOperationResult>>()), Times.Never);

        // 6. Undo with --yes -> restores and marks Undone
        using var undoOut = new StringWriter();
        var undoExit = await _exposer.RunAsync(
            ["undo", planned.SessionId.ToString("D"), "--yes", "--state-dir", _tempStateDir, "--json"],
            undoOut,
            err);
        Assert.Equal(0, undoExit);
        var undone = JsonSerializer.Deserialize(undoOut.ToString(), CliJsonContext.Default.CliSessionState)!;
        Assert.Equal(CliSessionStatus.Undone, undone.Status);
        Assert.NotNull(undone.UndoneAtUtc);
        _coreMock.Verify(s => s.UndoRenameOperations(It.IsAny<IReadOnlyList<RenameOperationResult>>()), Times.Once);

        // 7. Sessions list shows the session
        using var listOut = new StringWriter();
        var listExit = await _exposer.RunAsync(
            ["sessions", "list", "--state-dir", _tempStateDir, "--json"],
            listOut,
            err);
        Assert.Equal(0, listExit);
        var sessions = JsonSerializer.Deserialize(listOut.ToString(), CliJsonContext.Default.IReadOnlyListCliSessionSummary)!;
        Assert.Single(sessions);
        Assert.Equal(planned.SessionId, sessions[0].SessionId);
        Assert.Equal(CliSessionStatus.Undone, sessions[0].Status);
    }

    private void SetupTwoEpisodeFixture(Action<RenamePlanRequest>? onBuildPlan = null)
    {
        _coreMock.Setup(s => s.RetrieveSeasonsByShowIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Season { Id = 100, Number = 1, TotalEpisodeCount = 2, HasEpisodeOrder = true }]);

        _coreMock.Setup(s => s.RetrieveEpisodesBySeasonIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Episode { Id = 1, Season = 1, EpisodeNumber = 1, Name = "Pilot", RunTime = 30 },
                new Episode { Id = 2, Season = 1, EpisodeNumber = 2, Name = "Second", RunTime = 30 }
            ]);

        _coreMock.Setup(s => s.ImportMkvFilesFromPaths(It.IsAny<IReadOnlyList<string>>()))
            .Returns(new FileImportResult([
                new MkvFile("s1", "/media/title_t00.mkv", 1024, true),
                new MkvFile("s1", "/media/title_t01.mkv", 2048, true)
            ], [], []));

        _coreMock.Setup(s => s.BuildRenamePlan(It.IsAny<RenamePlanRequest>()))
            .Returns((RenamePlanRequest req) =>
            {
                onBuildPlan?.Invoke(req);
                var items = req.Candidates.Select((c, idx) => new RenamePlanItem(
                    idx + 1,
                    c.SourcePath,
                    Path.GetFileName(c.SourcePath),
                    $"/media/S{c.Season:D2}E{c.EpisodeNumber:D2} {c.EpisodeName}.mkv",
                    $"S{c.Season:D2}E{c.EpisodeNumber:D2} {c.EpisodeName}.mkv",
                    RenamePreviewStatus.Ready)).ToArray();

                return new RenamePlan(
                    IsPatternValid: true,
                    ReadyCount: items.Length,
                    ConflictCount: 0,
                    SkippedCount: 0,
                    DoneCount: 0,
                    Segments: [],
                    Errors: [],
                    Items: items);
            });
    }
}
