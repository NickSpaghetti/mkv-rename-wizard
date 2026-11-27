using System.Text.Json;
using MkvRenameWizard.Cli.Exposers.Stdio;
using MkvRenameWizard.Core.Models.Layers.Exceptions;
using MkvRenameWizard.Core.Models.Renaming;
using MkvRenameWizard.Core.Services.Aggregations;
using Moq;
using Xunit;

namespace MkvRenameWizard.Cli.Tests.Unit.Exposers.Stdio;

public sealed class StdioExposerTests
{
    private readonly Mock<IMkvRenameWizardService> _coreMock = new();
    private readonly StdioExposer _exposer;

    public StdioExposerTests()
    {
        _exposer = new StdioExposer(_coreMock.Object);
    }

    [Fact]
    public async Task ShouldRespondToHelloWithProtocolVersionAndTokensAsync()
    {
        _coreMock.Setup(s => s.RetrieveAvailableTokens())
            .Returns([new PatternToken("Title", "Episode Title", "Pilot")]);

        var response = await _exposer.HandleLineAsync("""{"id":"req-1","method":"hello"}""", CancellationToken.None);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        Assert.Equal("req-1", root.GetProperty("id").GetString());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(StdioExposer.ProtocolVersion, root.GetProperty("result").GetProperty("protocolVersion").GetInt32());
    }

    [Fact]
    public async Task ShouldReturnUnknownMethodErrorAsync()
    {
        var response = await _exposer.HandleLineAsync("""{"id":"2","method":"does-not-exist"}""", CancellationToken.None);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("unknown_method", root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ShouldMapValidationCategoryExceptionToValidationErrorAsync()
    {
        _coreMock.Setup(s => s.SearchShowsByNameAsync("", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MkvRenameWizardValidationException(new Exception("Show name is required.")));

        var response = await _exposer.HandleLineAsync("""{"id":"3","method":"search","params":{"query":""}}""", CancellationToken.None);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("validation_error", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Show name is required.", root.GetProperty("error").GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ShouldProcessMultipleNdjsonLinesOverStreamsAsync()
    {
        _coreMock.Setup(s => s.RetrieveAvailableTokens()).Returns([]);

        using var reader = new StringReader("{\"id\":\"a\",\"method\":\"tokens\"}\n{\"id\":\"b\",\"method\":\"hello\"}\n");
        using var writer = new StringWriter();

        var exitCode = await _exposer.RunAsync(reader, writer);

        Assert.Equal(0, exitCode);
        var lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task ShouldHandleUndoRpcMethodAsync()
    {
        _coreMock.Setup(s => s.UndoRenameOperations(It.IsAny<IReadOnlyList<RenameOperationResult>>()))
            .Returns([new RenameOperationResult(1, "/media/S01E01.mkv", "/media/t00.mkv", true, "")]);

        var payload = """{"id":"u1","method":"undo","params":{"results":[{"operationId":1,"sourcePath":"/media/t00.mkv","targetPath":"/media/S01E01.mkv","isSuccessful":true,"errorMessage":""}]}}""";
        var response = await _exposer.HandleLineAsync(payload, CancellationToken.None);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("/media/t00.mkv", root.GetProperty("result")[0].GetProperty("targetPath").GetString());
    }
}
