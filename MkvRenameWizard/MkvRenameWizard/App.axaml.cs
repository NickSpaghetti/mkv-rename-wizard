using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FileTypeChecker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.FileTypes;
using MkvRenameWizard.Services;
using MkvRenameWizard.ViewModels;
using MkvRenameWizard.Views;

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
            desktop.Exit += (_, _) => _serviceProvider?.Dispose();
            
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
        
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), nameof(MkvRenameWizard),"logs");
        
        services.AddLogging(configure =>
        {
            configure.SetMinimumLevel(LogLevel.Debug);
            configure.AddConsole();
        });
        
        services.AddHttpClient<ITvMazeDataAccess, TvMazeDataAccess>(client =>
        {
            client.BaseAddress = new Uri("https://api.tvmaze.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "MkvRenameWizard/1.0 (+https://github.com/NickSpaghetti/mkv-rename-wizard)");
        });

        services.AddHttpClient();
        
        services.TryAddTransient<IImageLoadingService, ImageLoadingService>();
        services.TryAddSingleton<ITvMazeService, TvMazeService>();
        services.TryAddSingleton<IMkvFinderService, MkvFinderService>();
        
        services.TryAddSingleton<ContentSearchViewModel>();
        services.TryAddSingleton<ShowSearchResultViewModel>();
        services.TryAddSingleton<ContentSelectViewModel>();
        services.TryAddSingleton<OutputFileConfigurationViewModel>(_ => new OutputFileConfigurationViewModel(new Dictionary<string, MkvFile>()));
        services.TryAddSingleton<WizardViewModel>();
        services.TryAddSingleton<MainWindowViewModel>();

        FileTypeValidator.RegisterCustomTypes(typeof(MatroskaVideo).Assembly);

        return services.BuildServiceProvider();
    }
}