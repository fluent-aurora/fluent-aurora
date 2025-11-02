using System;
using Avalonia.Controls;
using FluentAurora.Controls;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Playback;
using FluentAurora.Core.Settings;
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
        services.AddSingleton<IApplicationSettings, ApplicationSettings>();
        services.AddSingleton<ISettingsManager, SettingsManager>();

        // Controls
        services.AddTransient<CompactPlayer>();
        services.AddSingleton<CompactPlayerViewModel>();
        services.AddTransient<ExpandedPlayer>();
        services.AddSingleton<ExpandedPlayerViewModel>();

        // Views
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        // Services
        services.AddSingleton<DatabaseManager>();
        services.AddSingleton<PlaybackControlService>();
        services.AddSingleton<StoragePickerService>();
        services.AddSingleton<ThemeService>(provider =>
        {
            ThemeService themeService = new ThemeService();
            ISettingsManager settingsManager = provider.GetRequiredService<ISettingsManager>();
            try
            {
                ApplicationSettingsStore settings = settingsManager.Application;
                AppTheme savedTheme = settings.UiSettings.Theme;
                themeService.SetTheme(savedTheme);
                Logger.Info($"Applied saved theme during service initialization: {savedTheme}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply saved theme: {ex.Message}");
            }

            return themeService;
        });
        services.AddSingleton<PlaylistDialogService>(provider =>
        {
            DatabaseManager dbManager = provider.GetRequiredService<DatabaseManager>();
            Window mainWindow = provider.GetRequiredService<MainWindow>();
            return new PlaylistDialogService(dbManager, mainWindow);
        });

        return services.BuildServiceProvider();
    }
}