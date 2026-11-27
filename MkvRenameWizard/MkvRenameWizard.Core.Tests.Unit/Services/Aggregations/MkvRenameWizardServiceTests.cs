using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.Layers.Exceptions;
using MkvRenameWizard.Core.Models.TvMaze.Exceptions;
using MkvRenameWizard.Core.Services.Aggregations;
using MkvRenameWizard.Core.Services.Coordinations.TvMaze;
using MkvRenameWizard.Core.Services.Foundations.MkvFiles;
using MkvRenameWizard.Core.Services.Orchestrations.Renaming;
using Moq;
using Xunit;

namespace MkvRenameWizard.Core.Tests.Unit.Services.Aggregations;

public sealed class MkvRenameWizardServiceTests
{
    private readonly Mock<ITvMazeCoordinationService> _tvMazeCoordMock = new();
    private readonly Mock<IMkvFileService> _mkvFileMock = new();
    private readonly Mock<IRenameOrchestrationService> _renameOrchMock = new();
    private readonly Mock<ILoggingBroker> _loggingMock = new();
    private readonly MkvRenameWizardService _service;

    public MkvRenameWizardServiceTests()
    {
        _service = new MkvRenameWizardService(
            _tvMazeCoordMock.Object,
            _mkvFileMock.Object,
            _renameOrchMock.Object,
            _loggingMock.Object);
    }

    [Fact]
    public async Task ShouldWrapValidationCategoryIntoDependencyValidationExceptionAsync()
    {
        var inner = new TvMazeValidationException(new Exception("bad query"));
        _tvMazeCoordMock
            .Setup(s => s.SearchShowsByNameAsync("", It.IsAny<CancellationToken>()))
            .ThrowsAsync(inner);

        var thrown = await Assert.ThrowsAsync<MkvRenameWizardDependencyValidationException>(() =>
            _service.SearchShowsByNameAsync(""));

        Assert.Same(inner, thrown.InnerException);
    }

    [Fact]
    public async Task ShouldWrapDependencyCategoryIntoDependencyExceptionAsync()
    {
        var inner = new TvMazeCoordinationDependencyException(new Exception("network down"));
        _tvMazeCoordMock
            .Setup(s => s.SearchShowsByNameAsync("Initial D", It.IsAny<CancellationToken>()))
            .ThrowsAsync(inner);

        var thrown = await Assert.ThrowsAsync<MkvRenameWizardDependencyException>(() =>
            _service.SearchShowsByNameAsync("Initial D"));

        Assert.Same(inner, thrown.InnerException);
    }

    [Fact]
    public async Task ShouldWrapUnexpectedExceptionIntoServiceExceptionAsync()
    {
        var inner = new InvalidOperationException("boom");
        _tvMazeCoordMock
            .Setup(s => s.SearchShowsByNameAsync("Initial D", It.IsAny<CancellationToken>()))
            .ThrowsAsync(inner);

        var thrown = await Assert.ThrowsAsync<MkvRenameWizardServiceException>(() =>
            _service.SearchShowsByNameAsync("Initial D"));

        Assert.Same(inner, thrown.InnerException);
    }
}
