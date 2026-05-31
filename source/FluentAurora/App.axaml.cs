using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Settings;
using FluentAurora.Services;
using FluentAurora.Views;

namespace FluentAurora;

public partial class App : Application
{
    /// <summary>
    /// Desktop instance
    /// </summary>
    public static readonly IClassicDesktopStyleApplicationLifetime? Desktop = Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    /// <summary>
    /// Main Window instance
    /// </summary>
    public static Window? MainWindow => Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;

    /// <summary>
    /// DI Services
    /// </summary>
    public static IServiceProvider Services = ServiceConfigurator.ConfigureServices();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Desktop != null)
        {
            // Global exception handlers
            AppLogger.Trace("Registering global exception handlers");
            RegisterGlobalExceptionHandlers();

            // Initialize loading and saving of Settings
            SettingsService settingsService = Services.GetRequiredService<SettingsService>();
            AppLogger.SetLogLevel(settingsService.Settings.Debug.LogLevel);
            settingsService.SaveSettings();

            // Load Language
            LocalizationService.LoadLanguage();

            // Get MainWindow
            AppLogger.Debug("Resolving MainWindow from services");
            MainWindow mainWindow = Services.GetRequiredService<MainWindow>();
            Desktop.MainWindow = mainWindow;

            // Wire up window events
            mainWindow.Opened += (_, _) =>
            {
                AppLogger.Info("Launching FluentAurora");
                AppLogger.Debug("Main window opened");
            };

            // Application exit handler
            Desktop.Exit += (_, _) =>
            {
                AppLogger.Info("Closing FluentAurora");
                AppLogger.Debug("Flushing logs before shutdown");
                AppLogger.Flush();
                AppLogger.Debug("Shutting down logger");
                AppLogger.Shutdown();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Registers global exception handlers for unhandled exceptions
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            AppLogger.Error("Unobserved task exception occurred");
            HandleFatalException(args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            bool isTerminating = args.IsTerminating;
            AppLogger.Error($"Unhandled exception in AppDomain (Terminating: {isTerminating})");

            if (args.ExceptionObject is Exception ex)
            {
                HandleFatalException(ex);
            }
            else
            {
                AppLogger.Error($"Non-exception object thrown: {args.ExceptionObject?.GetType().FullName ?? "null"}");
            }
        };

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            args.Handled = true;
            AppLogger.Error("Unhandled exception on UI thread");
            HandleFatalException(args.Exception);
        };
    }

    private static void HandleFatalException(Exception ex)
    {
        try
        {
            AppLogger.Error("=== Fatal Exception Encountered ===");
            AppLogger.LogExceptionDetails(ex, includeEnvironmentInfo: true);

            // Ensure logs are written before a potential crash
            AppLogger.Flush();
        }
        catch
        {
            // If logging fails, we can do little about it
            // Just ensure we don't throw from the exception handler
        }
    }
}