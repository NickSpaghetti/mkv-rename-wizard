namespace MkvRenameWizard.Cli.Exposers.Cli;

internal interface ICliExposer
{
    Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default,
        TextReader? input = null);
}
