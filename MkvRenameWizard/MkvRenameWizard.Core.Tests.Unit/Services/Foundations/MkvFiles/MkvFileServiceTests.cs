using MkvRenameWizard.Core.Brokers.FileSystems;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.FileSystems;
using MkvRenameWizard.Core.Models.MkvFiles;
using MkvRenameWizard.Core.Models.MkvFiles.Exceptions;
using MkvRenameWizard.Core.Services.Foundations.MkvFiles;
using Moq;
using Xunit;

namespace MkvRenameWizard.Core.Tests.Unit.Services.Foundations.MkvFiles;

public sealed class MkvFileServiceTests
{
    private readonly Mock<IFileSystemBroker> _fsBrokerMock = new();
    private readonly Mock<ILoggingBroker> _loggingBrokerMock = new();
    private readonly MkvFileService _service;

    public MkvFileServiceTests()
    {
        _service = new MkvFileService(_fsBrokerMock.Object, _loggingBrokerMock.Object);
    }

    [Fact]
    public void ShouldImportValidMkvFilesSortedAndReportInvalidContainers()
    {
        var dir = "/media/season1";
        var validB = "/media/season1/title_t01.mkv";
        var validA = "/media/season1/title_t00.mkv";
        var invalid = "/media/season1/corrupt.mkv";

        _fsBrokerMock.Setup(fs => fs.DirectoryExists(dir)).Returns(true);
        _fsBrokerMock.Setup(fs => fs.EnumerateFiles(dir, "*.mkv", SearchOption.AllDirectories))
            .Returns([validB, invalid, validA]);

        _fsBrokerMock.Setup(fs => fs.ReadFileHeaderAndLength(validA))
            .Returns(new FileHeaderInfo(0x1A, 0x45, 0xDF, 0xA3, 4, 1024));
        _fsBrokerMock.Setup(fs => fs.ReadFileHeaderAndLength(validB))
            .Returns(new FileHeaderInfo(0x1A, 0x45, 0xDF, 0xA3, 4, 2048));
        _fsBrokerMock.Setup(fs => fs.ReadFileHeaderAndLength(invalid))
            .Returns(new FileHeaderInfo(0x00, 0x00, 0x00, 0x00, 4, 512));

        var result = _service.ImportFromPaths([dir]);

        Assert.Equal(2, result.ImportedFiles.Count);
        Assert.Equal(validA, result.ImportedFiles[0].FullPath);
        Assert.Equal(validB, result.ImportedFiles[1].FullPath);
        Assert.Single(result.FileImportIssues);
        Assert.Equal(FileImportIssueType.UnsupportedFileType, result.FileImportIssues[0].FileImportIssueType);
    }

    [Fact]
    public void ShouldReportMissingAndUnsupportedIndividualPaths()
    {
        _fsBrokerMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
        _fsBrokerMock.Setup(fs => fs.FileExists("/media/movie.mp4")).Returns(true);
        _fsBrokerMock.Setup(fs => fs.FileExists("/media/missing.mkv")).Returns(false);

        var result = _service.ImportFromPaths(["/media/movie.mp4", "/media/missing.mkv"]);

        Assert.Empty(result.ImportedFiles);
        Assert.Equal(2, result.FileImportIssues.Count);
        Assert.Equal(FileImportIssueType.UnsupportedFileType, result.FileImportIssues[0].FileImportIssueType);
        Assert.Equal(FileImportIssueType.FileNotFound, result.FileImportIssues[1].FileImportIssueType);
    }

    [Fact]
    public void ShouldThrowValidationExceptionOnNullPaths()
    {
        Assert.Throws<MkvFileValidationException>(() => _service.ImportFromPaths(null!));
    }
}
