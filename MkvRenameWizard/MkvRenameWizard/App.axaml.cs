using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Core.Configurations;
using MkvRenameWizard.Services;
using MkvRenameWizard.ViewModels;
using MkvRenameWizard.Views;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace MkvRenameWizard;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _serviceProvider = RegisterDependencies();
            desktop.Exit += (_, _) =>
            {
                _serviceProvider?.Dispose();
                Log.CloseAndFlush();
            };

            try
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider RegisterDependencies()
    {
        var services = new ServiceCollection();

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            nameof(MkvRenameWizard),
            "Logs");

        var logLevel = LogLevel.Warning;
        var serilogLevel = LogEventLevel.Warning;
#if DEBUG
        logLevel = LogLevel.Debug;
        serilogLevel = LogEventLevel.Debug;
#endif

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Is(serilogLevel)
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(logDirectory, "logs-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        services.AddLogging(configure =>
        {
            configure.SetMinimumLevel(logLevel);
            configure.AddJsonConsole();
            configure.AddSerilog(serilogLogger, dispose: true);
        });

        services.AddMkvRenameWizardCore();
        services.AddHttpClient();

        services.TryAddTransient<IImageLoadingService, ImageLoadingService>();
        services.TryAddTransient<IClipboardService, AvaloniaClipBoardService>();
        services.TryAddSingleton<ITvMazeService, TvMazeService>();
        services.TryAddSingleton<IMkvFinderService, MkvFinderService>();
        services.TryAddSingleton<IFileRenameOperationService, FileRenameOperationService>();
        services.TryAddSingleton<IFileLoggerService>(sp => new FileLoggerService(logDirectory, sp.GetService<ILogger<FileLoggerService>>()));

        services.TryAddSingleton<ContentSearchViewModel>();
        services.TryAddSingleton<ShowSearchResultViewModel>();
        services.TryAddSingleton<ContentSelectViewModel>();
        services.TryAddSingleton<OutputFileConfigurationViewModel>();
        services.TryAddSingleton<RenameResultViewModel>();
        services.TryAddSingleton<WizardViewModel>();
        services.TryAddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
