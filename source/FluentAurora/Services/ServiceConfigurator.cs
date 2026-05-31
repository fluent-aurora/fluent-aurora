using System;
using Microsoft.Extensions.DependencyInjection;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Models;
using FluentAurora.Core.Settings;
using FluentAurora.ViewModels;
using FluentAurora.Views;

namespace FluentAurora.Services;

/// <summary>
/// Configures and registers application services, views, and view models with the dependency injection container.
/// Centralizes all service registration to ensure proper dependency resolution throughout the application.
/// </summary>
public static class ServiceConfigurator
{
    /// <summary>
    /// Configures the dependency injection container by registering all application services, views, and view models.
    /// Builds and returns an <see cref="IServiceProvider"/> for resolving registered dependencies.
    /// </summary>
    /// <returns>
    /// An <see cref="IServiceProvider"/> instance that can resolve all registered services, views, and view models.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown when service registration or container building fails. Details are logged before re-throwing.
    /// </exception>
    public static IServiceProvider ConfigureServices()
    {
        AppLogger.Debug("Configuring dependency injection services");
        try
        {
            ServiceCollection services = new ServiceCollection();

            // Register Services here
            // Settings
            services.AddSingleton<SettingsService>();

            // Theme Service
            services.AddSingleton<ThemeService>(provider =>
            {
                ThemeService themeService = new ThemeService();
                SettingsService settings = provider.GetRequiredService<SettingsService>();
                try
                {
                    Theme savedTheme = settings.Settings.Ui.Theme;
                    themeService.SetTheme(savedTheme);
                    AppLogger.Info($"Loaded saved theme: {savedTheme}");
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Failed to apply saved theme");
                    AppLogger.LogExceptionDetails(ex);
                }
                return themeService;
            });

            // Register Views/ViewModels here
            // Pages

            // Views
            services.AddSingleton<MainViewViewModel>();

            // Windows
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<MainWindow>();

            // Initialize services
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            // Initialize ThemeService
            _ = serviceProvider.GetRequiredService<ThemeService>();
            return serviceProvider;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to configure services");
            AppLogger.LogExceptionDetails(ex);
            throw;
        }
    }
}