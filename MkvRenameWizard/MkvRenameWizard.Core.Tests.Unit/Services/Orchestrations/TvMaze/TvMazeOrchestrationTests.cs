using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.TvMaze;
using MkvRenameWizard.Core.Services.Foundations.TvMazeCache;
using MkvRenameWizard.Core.Services.Foundations.TvMazeEpisodes;
using MkvRenameWizard.Core.Services.Foundations.TvMazeSeasons;
using MkvRenameWizard.Core.Services.Foundations.TvMazeShows;
using MkvRenameWizard.Core.Services.Orchestrations.TvMaze;
using Moq;
using Xunit;

namespace MkvRenameWizard.Core.Tests.Unit.Services.Orchestrations.TvMaze;

public sealed class TvMazeOrchestrationTests
{
    [Fact]
    public async Task ShouldReturnCachedShowsWithoutCallingApiFoundationAsync()
    {
        var showServiceMock = new Mock<ITvMazeShowService>();
        var cacheServiceMock = new Mock<ITvMazeCacheService>();
        var loggingMock = new Mock<ILoggingBroker>();

        IReadOnlyList<Show> cached = [new Show { Id = 1, Name = "Initial D" }];
        cacheServiceMock.Setup(c => c.RetrieveCachedShows("Initial D")).Returns(cached);

        var orch = new TvMazeShowOrchestrationService(
            showServiceMock.Object,
            cacheServiceMock.Object,
            loggingMock.Object);

        var result = await orch.SearchShowsByNameAsync("Initial D");

        Assert.Same(cached, result);
        showServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ShouldFanOutEpisodeCountLookupForSeasonsWithoutEpisodeOrderAsync()
    {
        var seasonServiceMock = new Mock<ITvMazeSeasonService>();
        var episodeServiceMock = new Mock<ITvMazeEpisodeService>();
        var cacheServiceMock = new Mock<ITvMazeCacheService>();
        var loggingMock = new Mock<ILoggingBroker>();

        seasonServiceMock
            .Setup(s => s.RetrieveSeasonsByShowIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Season { Id = 101, Number = 1, Name = "First Stage", TotalEpisodeCount = 0, HasEpisodeOrder = false },
                new Season { Id = 102, Number = 2, Name = "Second Stage", TotalEpisodeCount = 13, HasEpisodeOrder = true }
            ]);

        episodeServiceMock
            .Setup(e => e.RetrieveEpisodesBySeasonIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Episode { Id = 1, Season = 1, EpisodeNumber = 1, Name = "Ep 1" },
                new Episode { Id = 2, Season = 1, EpisodeNumber = 2, Name = "Ep 2" }
            ]);

        var orch = new TvMazeSeasonOrchestrationService(
            seasonServiceMock.Object,
            episodeServiceMock.Object,
            cacheServiceMock.Object,
            loggingMock.Object);

        var seasons = await orch.RetrieveSeasonsByShowIdAsync(7);

        Assert.Equal(2, seasons.Count);
        Assert.Equal(2, seasons[0].TotalEpisodeCount);
        Assert.Equal(13, seasons[1].TotalEpisodeCount);

        episodeServiceMock.Verify(e => e.RetrieveEpisodesBySeasonIdAsync(101, It.IsAny<CancellationToken>()), Times.Once);
        episodeServiceMock.Verify(e => e.RetrieveEpisodesBySeasonIdAsync(102, It.IsAny<CancellationToken>()), Times.Never);
    }
}
