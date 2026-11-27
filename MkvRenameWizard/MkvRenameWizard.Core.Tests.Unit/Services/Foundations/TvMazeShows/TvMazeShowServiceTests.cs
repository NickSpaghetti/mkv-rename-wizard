using System.Net;
using System.Text.Json;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Brokers.TvMaze;
using MkvRenameWizard.Core.Models.TvMaze.Dto;
using MkvRenameWizard.Core.Models.TvMaze.Exceptions;
using MkvRenameWizard.Core.Services.Foundations.TvMazeShows;
using Moq;
using Xunit;

namespace MkvRenameWizard.Core.Tests.Unit.Services.Foundations.TvMazeShows;

public sealed class TvMazeShowServiceTests
{
    private readonly Mock<ITvMazeApiBroker> _apiBrokerMock = new();
    private readonly Mock<ILoggingBroker> _loggingBrokerMock = new();
    private readonly TvMazeShowService _service;

    public TvMazeShowServiceTests()
    {
        _service = new TvMazeShowService(_apiBrokerMock.Object, _loggingBrokerMock.Object);
    }

    [Fact]
    public async Task ShouldSearchShowsStripHtmlAndFormatYearRangeAsync()
    {
        var dto = new List<ShowSearchResultDto>
        {
            new()
            {
                Score = 0.95,
                ShowDto = new ShowDto
                {
                    Id = 42,
                    Name = "Initial D",
                    Language = "Japanese",
                    Genres = ["Action", "Anime"],
                    Premiered = "1998-04-18",
                    Ended = "2014-06-22",
                    Summary = "<p>Takumi delivers <b>tofu</b> &amp; drifts.</p>",
                    NetworkDto = new NetworkDto { Name = "Fuji TV" }
                }
            }
        };

        _apiBrokerMock
            .Setup(broker => broker.SearchShowsAsync("Initial D", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var shows = await _service.SearchShowsByNameAsync("  Initial D  ");

        Assert.Single(shows);
        Assert.Equal("Initial D", shows[0].Name);
        Assert.Equal("1998-2014", shows[0].YearRange);
        Assert.Equal("Takumi delivers tofu & drifts.", shows[0].PlainSummary);
        Assert.Equal("Fuji TV", shows[0].AirNetworkLabel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldThrowValidationExceptionOnInvalidShowNameAsync(string invalidName)
    {
        await Assert.ThrowsAsync<TvMazeValidationException>(() =>
            _service.SearchShowsByNameAsync(invalidName));

        _apiBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ShouldThrowDependencyValidationExceptionOnHttpNotFoundAsync()
    {
        _apiBrokerMock
            .Setup(broker => broker.SearchShowsAsync("Missing", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<TvMazeDependencyValidationException>(() =>
            _service.SearchShowsByNameAsync("Missing"));
    }

    [Fact]
    public async Task ShouldThrowDependencyExceptionOnJsonFailureAsync()
    {
        _apiBrokerMock
            .Setup(broker => broker.SearchShowsAsync("Broken", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("Malformed JSON"));

        await Assert.ThrowsAsync<TvMazeDependencyException>(() =>
            _service.SearchShowsByNameAsync("Broken"));
    }

    [Fact]
    public async Task ShouldThrowServiceExceptionOnUnexpectedFailureAsync()
    {
        _apiBrokerMock
            .Setup(broker => broker.SearchShowsAsync("Boom", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        await Assert.ThrowsAsync<TvMazeServiceException>(() =>
            _service.SearchShowsByNameAsync("Boom"));
    }
}
