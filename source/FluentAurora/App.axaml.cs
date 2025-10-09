using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAurora.Services;
using FluentAurora.ViewModels;
using FluentAurora.Views;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Logger = FluentAurora.Core.Logging.Logger;

namespace FluentAurora;

public partial class App : Application
{
    public static Window? MainWindow => Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
    public static IServiceProvider? Services { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            Services = ServiceConfigurator.ConfigureServices();

            MainWindow mainWindow = Services.GetRequiredService<MainWindow>();

            mainWindow.Opened += (_, _) =>
            {
                Logger.Info("FluentAurora started");
            };
            mainWindow.Closing += (_, _) =>
            {
                Logger.Info("Closing FluentAurora");
                LogManager.Flush();
            };
            desktop.Exit += (_, _) =>
            {
                Logger.Shutdown();
            };
            
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (DataAnnotationsValidationPlugin plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}