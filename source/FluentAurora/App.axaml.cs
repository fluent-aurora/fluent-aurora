using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Playback;
using FluentAurora.Core.Settings;
using FluentAurora.Services;
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
            DisableAvaloniaDataAnnotationValidation();

            Services = ServiceConfigurator.ConfigureServices();
            _ = Services.GetRequiredService<ThemeService>(); // Forces the applying of saved theme on startup
            MainWindow mainWindow = Services.GetRequiredService<MainWindow>();
            ISettingsManager settingsManager = Services.GetRequiredService<ISettingsManager>();
            Logger.SetLogLevel(LogLevelHelper.FromString(settingsManager.Application.Debug.Logger.Level));

            mainWindow.Opened += (_, _) =>
            {
                Logger.Info("FluentAurora started");
            };

            mainWindow.Closing += (_, _) =>
            {
                Logger.Info("Closing FluentAurora");
                if (Services.GetService<AudioPlayerService>() is { } audioPlayerService)
                {
                    audioPlayerService.Stop();
                    audioPlayerService.Dispose();
                }
                LogManager.Flush();
            };

            desktop.Exit += (_, _) =>
            {
                Logger.Shutdown();
                settingsManager.SaveAll();
                settingsManager.Dispose();
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
                HandleFatalException(args.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    HandleFatalException(ex);
                }
            };

            Dispatcher.UIThread.UnhandledException += (_, args) =>
            {
                args.Handled = true;
                HandleFatalException(args.Exception);
            };

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void HandleFatalException(Exception ex)
    {
        Logger.Error("Exception encountered");
        Logger.LogExceptionDetails(ex);
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (DataAnnotationsValidationPlugin plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}