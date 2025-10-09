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
        
        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<PlaybackControlViewModel>();
        
        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<PlaybackControl>();
        
        return services.BuildServiceProvider();
    }
}