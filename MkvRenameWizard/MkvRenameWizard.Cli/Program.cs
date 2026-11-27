using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Cli.Exposers.Cli;
using MkvRenameWizard.Cli.Exposers.Stdio;
using MkvRenameWizard.Core.Configurations;

namespace MkvRenameWizard.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
        });

        var tvMazeBaseUrl = Environment.GetEnvironmentVariable("TVMAZE_BASE_URL");
        Uri? tvMazeUri = Uri.TryCreate(tvMazeBaseUrl, UriKind.Absolute, out var parsedUri) ? parsedUri : null;

        services.AddMkvRenameWizardCore(tvMazeUri);
        services.AddSingleton<ICliExposer, CliExposer>();
        services.AddSingleton<IStdioExposer, StdioExposer>();

        await using var provider = services.BuildServiceProvider();

        if (args.Length >= 1 && string.Equals(args[0], "serve", StringComparison.OrdinalIgnoreCase))
        {
            var stdio = provider.GetRequiredService<IStdioExposer>();
            return await stdio.RunAsync(Console.In, Console.Out, cts.Token);
        }

        var cli = provider.GetRequiredService<ICliExposer>();
        return await cli.RunAsync(args, Console.Out, Console.Error, cts.Token, Console.In);
    }
}
