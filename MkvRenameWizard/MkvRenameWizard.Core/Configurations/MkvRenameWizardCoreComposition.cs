using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MkvRenameWizard.Core.Brokers.Caches;
using MkvRenameWizard.Core.Brokers.FileSystems;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Brokers.TvMaze;
using MkvRenameWizard.Core.Services.Aggregations;
using MkvRenameWizard.Core.Services.Coordinations.TvMaze;
using MkvRenameWizard.Core.Services.Foundations.MkvFiles;
using MkvRenameWizard.Core.Services.Foundations.Renaming;
using MkvRenameWizard.Core.Services.Foundations.TvMazeCache;
using MkvRenameWizard.Core.Services.Foundations.TvMazeEpisodes;
using MkvRenameWizard.Core.Services.Foundations.TvMazeSeasons;
using MkvRenameWizard.Core.Services.Foundations.TvMazeShows;
using MkvRenameWizard.Core.Services.Orchestrations.Renaming;
using MkvRenameWizard.Core.Services.Orchestrations.TvMaze;

namespace MkvRenameWizard.Core.Configurations;

public static class MkvRenameWizardCoreComposition
{
    public static IServiceCollection AddMkvRenameWizardCore(
        this IServiceCollection services,
        Uri? tvMazeBaseAddress = null)
    {
        services.AddHttpClient<ITvMazeApiBroker, TvMazeApiBroker>(client =>
        {
            client.BaseAddress = tvMazeBaseAddress ?? new Uri("https://api.tvmaze.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "MkvRenameWizard/1.0 (+https://github.com/NickSpaghetti/mkv-rename-wizard)");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

        services.TryAddSingleton<ILoggingBroker, LoggingBroker>();
        services.TryAddSingleton<IFileSystemBroker, FileSystemBroker>();
        services.TryAddSingleton<ITvMazeCacheBroker, TvMazeCacheBroker>();

        services.TryAddSingleton<ITvMazeShowService, TvMazeShowService>();
        services.TryAddSingleton<ITvMazeSeasonService, TvMazeSeasonService>();
        services.TryAddSingleton<ITvMazeEpisodeService, TvMazeEpisodeService>();
        services.TryAddSingleton<ITvMazeCacheService, TvMazeCacheService>();
        services.TryAddSingleton<IMkvFileService, MkvFileService>();
        services.TryAddSingleton<IRenamePlanService, RenamePlanService>();
        services.TryAddSingleton<IFileRenameService, FileRenameService>();

        services.TryAddSingleton<ITvMazeShowOrchestrationService, TvMazeShowOrchestrationService>();
        services.TryAddSingleton<ITvMazeSeasonOrchestrationService, TvMazeSeasonOrchestrationService>();
        services.TryAddSingleton<IRenameOrchestrationService, RenameOrchestrationService>();

        services.TryAddSingleton<ITvMazeCoordinationService, TvMazeCoordinationService>();
        services.TryAddSingleton<IMkvRenameWizardService, MkvRenameWizardService>();

        return services;
    }
}
