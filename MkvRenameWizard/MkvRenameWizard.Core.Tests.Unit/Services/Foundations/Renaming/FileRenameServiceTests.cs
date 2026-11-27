using MkvRenameWizard.Core.Brokers.FileSystems;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Models.Renaming.Exceptions;
using MkvRenameWizard.Core.Services.Foundations.Renaming;
using Moq;
using Xunit;

namespace MkvRenameWizard.Core.Tests.Unit.Services.Foundations.Renaming;

public sealed class FileRenameServiceTests
{
    private readonly Mock<IFileSystemBroker> _fsBrokerMock = new();
    private readonly Mock<ILoggingBroker> _loggingBrokerMock = new();
    private readonly FileRenameService _service;

    public FileRenameServiceTests()
    {
        _service = new FileRenameService(_fsBrokerMock.Object, _loggingBrokerMock.Object);
    }

    [Fact]
    public void ShouldCreateTargetDirectoryOnceAndContinueBatchAfterIoFailure()
    {
        var ops = new List<RenameFileOperation>
        {
            new(1, "/src/a.mkv", "/dst/S01E01.mkv"),
            new(2, "/src/b.mkv", "/dst/S01E02.mkv"),
            new(3, "/src/c.mkv", "")
        };

        _fsBrokerMock
            .Setup(fs => fs.MoveFile("/src/b.mkv", "/dst/S01E02.mkv", false))
            .Throws(new IOException("File already exists"));

        var results = _service.ExecuteRenameOperations(ops);

        Assert.Equal(3, results.Count);
        Assert.True(results[0].IsSuccessful);
        Assert.False(results[1].IsSuccessful);
        Assert.Contains("already exists", results[1].ErrorMessage);
        Assert.False(results[2].IsSuccessful);

        _fsBrokerMock.Verify(fs => fs.CreateDirectory("/dst"), Times.Once);
    }

    [Fact]
    public void ShouldThrowValidationExceptionOnNullOperations()
    {
        Assert.Throws<FileRenameValidationException>(() => _service.ExecuteRenameOperations(null!));
    }
}
