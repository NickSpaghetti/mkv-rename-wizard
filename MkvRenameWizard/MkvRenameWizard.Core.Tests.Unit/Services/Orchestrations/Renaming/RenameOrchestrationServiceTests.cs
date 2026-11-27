using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Services.Foundations.Renaming;
using MkvRenameWizard.Core.Services.Orchestrations.Renaming;
using Moq;
using Xunit;

namespace MkvRenameWizard.Core.Tests.Unit.Services.Orchestrations.Renaming;

public sealed class RenameOrchestrationServiceTests
{
    [Fact]
    public void ShouldUndoOnlySuccessfulOperationsInReverseOrder()
    {
        var planMock = new Mock<IRenamePlanService>();
        var renameMock = new Mock<IFileRenameService>();
        var loggingMock = new Mock<ILoggingBroker>();

        IReadOnlyList<RenameFileOperation>? captured = null;
        renameMock
            .Setup(s => s.ExecuteRenameOperations(It.IsAny<IReadOnlyList<RenameFileOperation>>()))
            .Callback<IReadOnlyList<RenameFileOperation>>(ops => captured = ops)
            .Returns([]);

        var service = new RenameOrchestrationService(planMock.Object, renameMock.Object, loggingMock.Object);

        var previous = new List<RenameOperationResult>
        {
            new(1, "/media/t00.mkv", "/media/S01E01.mkv", IsSuccessful: true, ErrorMessage: ""),
            new(2, "/media/t01.mkv", "/media/S01E02.mkv", IsSuccessful: false, ErrorMessage: "denied"),
            new(3, "/media/t02.mkv", "/media/S01E03.mkv", IsSuccessful: true, ErrorMessage: "")
        };

        service.UndoRenameOperations(previous);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Count);
        Assert.Equal("/media/S01E03.mkv", captured[0].SourcePath);
        Assert.Equal("/media/t02.mkv", captured[0].TargetPath);
        Assert.Equal("/media/S01E01.mkv", captured[1].SourcePath);
        Assert.Equal("/media/t00.mkv", captured[1].TargetPath);
    }
}
