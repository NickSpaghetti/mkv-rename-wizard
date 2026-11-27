namespace MkvRenameWizard.Cli.Exposers.Stdio;

internal interface IStdioExposer
{
    Task<int> RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken = default);
}
