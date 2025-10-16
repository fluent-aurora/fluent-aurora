using System;
using FluentAurora.Controls;
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
        services.AddTransient<PlaybackControl>();
        services.AddSingleton<PlaybackControlViewModel>();
        services.AddTransient<ExpandedPlaybackControl>();
        services.AddSingleton<ExtendedPlaybackControlViewModel>();
        
        // Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        
        // Services
        services.AddSingleton<PlaybackControlService>();
        
        return services.BuildServiceProvider();
    }
}