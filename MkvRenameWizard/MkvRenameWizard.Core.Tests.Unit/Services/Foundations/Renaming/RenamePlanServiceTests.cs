using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.Renaming.Exceptions;
using MkvRenameWizard.Core.Services.Foundations.Renaming;
using Moq;
using Xunit;

namespace MkvRenameWizard.Core.Tests.Unit.Services.Foundations.Renaming;

public sealed class RenamePlanServiceTests
{
    private readonly Mock<ILoggingBroker> _loggingBrokerMock = new();
    private readonly RenamePlanService _service;

    public RenamePlanServiceTests()
    {
        _service = new RenamePlanService(_loggingBrokerMock.Object);
    }

    [Fact]
    public void ShouldBuildPlanWithSanitizationCaseStyleSkippedDoneAndConflictDetection()
    {
        var candidates = new List<RenameCandidate>
        {
            new("/media/title_t00.mkv", 1, 1, "Act. 1: The Ultimate Tofu Store Drift?", 24, new DateOnly(1998, 4, 18)),
            new("/media/S01E02 Already Named.mkv", 1, 2, "Already Named", 24, new DateOnly(1998, 4, 25)),
            new("/media/title_t02.mkv", 1, null, "Special Without Number", 24, null),
            new("/media/title_t03.mkv", 1, 3, "Duplicate", 24, null),
            new("/media/title_t04.mkv", 1, 3, "Duplicate", 24, null)
        };

        var request = new RenamePlanRequest(
            ShowName: "Initial D",
            Pattern: "{S##E##} {Title}",
            Prefix: string.Empty,
            CaseStyle: CaseStyle.Default,
            TargetDirectory: null,
            Candidates: candidates);

        var plan = _service.BuildPlan(request);

        Assert.True(plan.IsPatternValid);
        Assert.Equal(1, plan.ReadyCount);
        Assert.Equal(1, plan.DoneCount);
        Assert.Equal(1, plan.SkippedCount);
        Assert.Equal(2, plan.ConflictCount);

        Assert.Equal("S01E01 Act. 1 The Ultimate Tofu Store Drift.mkv", plan.Items[0].TargetFileName);
        Assert.Equal(RenamePreviewStatus.Ready, plan.Items[0].Status);
        Assert.Equal(RenamePreviewStatus.Done, plan.Items[1].Status);
        Assert.Equal(RenamePreviewStatus.Skipped, plan.Items[2].Status);
        Assert.Equal(RenamePreviewStatus.Conflict, plan.Items[3].Status);
        Assert.Equal(RenamePreviewStatus.Conflict, plan.Items[4].Status);
    }

    [Fact]
    public void ShouldReportPatternErrorsAndLevenshteinSuggestion()
    {
        var validation = _service.ValidatePattern("{S##E##} {Titl}");

        Assert.False(validation.IsValid);
        Assert.Single(validation.Errors);
        Assert.Equal("Titl", validation.Errors[0].TokenName);
        Assert.Equal("Title", validation.Errors[0].Suggestion);
    }

    [Theory]
    [InlineData(CaseStyle.SnakeCase, "s01e01_my_episode.mkv")]
    [InlineData(CaseStyle.PascalCase, "S01E01MyEpisode.mkv")]
    [InlineData(CaseStyle.CamelCase, "s01E01MyEpisode.mkv")]
    public void ShouldApplyCaseStyles(CaseStyle style, string expectedFileName)
    {
        var request = new RenamePlanRequest(
            ShowName: "Show",
            Pattern: "{S##E##} {Title}",
            Prefix: string.Empty,
            CaseStyle: style,
            TargetDirectory: null,
            Candidates: [new RenameCandidate("/media/title_t00.mkv", 1, 1, "My Episode", 30, null)]);

        var plan = _service.BuildPlan(request);

        Assert.Equal(expectedFileName, plan.Items[0].TargetFileName);
    }

    [Fact]
    public void ShouldThrowValidationExceptionOnNullRequest()
    {
        Assert.Throws<RenamePlanValidationException>(() => _service.BuildPlan(null!));
    }
}
