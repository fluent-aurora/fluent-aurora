using System;
using FluentAurora.Controls;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Playback;
using FluentAurora.ViewModels;
using FluentAurora.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAurora.Services;

public static class ServiceConfigurator
{
    public static IServiceProvider ConfigureServices()
    {
        ServiceCollection services = new ServiceCollection();

        // Core
        services.AddSingleton<AudioPlayerService>(); // Audio Player

        // Controls
        services.AddTransient<CompactPlayer>();
        services.AddSingleton<CompactPlayerViewModel>();
        services.AddTransient<ExpandedPlayer>();
        services.AddSingleton<ExpandedPlayerViewModel>();

        // Views
        services.AddSingleton<LibraryView>();
        services.AddSingleton<LibraryViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        // Services
        services.AddSingleton<DatabaseManager>();
        services.AddSingleton<PlaybackControlService>();
        services.AddSingleton<StoragePickerService>();

        return services.BuildServiceProvider();
    }
}