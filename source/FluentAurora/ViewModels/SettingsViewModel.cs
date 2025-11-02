using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Settings;
using FluentAurora.Services;
using NLog;
using Logger = FluentAurora.Core.Logging.Logger;

namespace FluentAurora.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    public class ThemeItem
    {
        public AppTheme Theme { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class LogLevelItem
    {
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // Properties
    private readonly ISettingsManager _settingsManager;
    private readonly ThemeService _themeService;
    private bool _isUpdatingFromSettings;
    private CancellationTokenSource? _saveDebounceTimer;

    [ObservableProperty] private bool reactiveArtworkEnabled;
    [ObservableProperty] private double artworkScaleRangeStart;
    [ObservableProperty] private double artworkScaleRangeEnd;
    [ObservableProperty] private ThemeItem? selectedTheme;
    [ObservableProperty] private LogLevelItem? selectedLogLevel;

    public ObservableCollection<ThemeItem> AvailableThemes { get; }
    public ObservableCollection<LogLevelItem> AvailableLogLevels { get; }

    public string ScaleRangeText => $"{ArtworkScaleRangeStart:F0}% - {ArtworkScaleRangeEnd:F0}%";

    // Constructor
    public SettingsViewModel(ISettingsManager settingsManager, ThemeService themeService)
    {
        _settingsManager = settingsManager;
        _themeService = themeService;

        AvailableThemes = new ObservableCollection<ThemeItem>();
        AvailableLogLevels = new ObservableCollection<LogLevelItem>();
        InitializeThemes();
        InitializeLogLevels();

        ApplySettings(_settingsManager.Application);
        _settingsManager.SettingsChanged += OnSettingsChanged;
    }

    // Functions
    private void InitializeThemes()
    {
        // Add available themes from the ThemeService
        foreach (AppTheme theme in _themeService.GetAvailableThemes())
        {
            AvailableThemes.Add(new ThemeItem
            {
                Theme = theme,
                DisplayName = GetThemeDisplayName(theme)
            });
        }
    }

    private void InitializeLogLevels()
    {
        AvailableLogLevels.Add(new LogLevelItem
        {
            Level = LogLevel.Trace,
            DisplayName = "Trace",
            Description = "Most detailed logging, includes all messages"
        });

        AvailableLogLevels.Add(new LogLevelItem
        {
            Level = LogLevel.Debug,
            DisplayName = "Debug",
            Description = "Detailed debugging information"
        });

        AvailableLogLevels.Add(new LogLevelItem
        {
            Level = LogLevel.Info,
            DisplayName = "Info",
            Description = "General informational messages"
        });

        AvailableLogLevels.Add(new LogLevelItem
        {
            Level = LogLevel.Warn,
            DisplayName = "Warning",
            Description = "Warning messages and recoverable errors"
        });

        AvailableLogLevels.Add(new LogLevelItem
        {
            Level = LogLevel.Error,
            DisplayName = "Error",
            Description = "Error messages only"
        });

        AvailableLogLevels.Add(new LogLevelItem
        {
            Level = LogLevel.Fatal,
            DisplayName = "Fatal",
            Description = "Only critical/fatal errors"
        });

        AvailableLogLevels.Add(new LogLevelItem
        {
            Level = LogLevel.Off,
            DisplayName = "Off",
            Description = "Disable logging completely"
        });
    }

    private string GetThemeDisplayName(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => "Light",
            AppTheme.Dark => "Dark",
            AppTheme.Black => "Black",
            _ => theme.ToString()
        };
    }

    private void OnSettingsChanged(object? sender, ApplicationSettingsStore settings)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ApplySettings(settings);
        });
    }

    private void ApplySettings(ApplicationSettingsStore settings)
    {
        _isUpdatingFromSettings = true;
        try
        {
            // Theme settings
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Theme == settings.UiSettings.Theme);

            // Playback settings
            ReactiveArtworkEnabled = settings.Playback.ReactiveArtwork.Enabled;
            ArtworkScaleRangeStart = settings.Playback.ReactiveArtwork.Scale.Base * 100;
            ArtworkScaleRangeEnd = settings.Playback.ReactiveArtwork.Scale.Max * 100;

            // Debug settings
            LogLevel logLevel = LogLevelHelper.FromString(settings.Debug.Logger.Level);
            SelectedLogLevel = AvailableLogLevels.FirstOrDefault(level => level.Level == logLevel);

            Logger.Info($"Settings loaded - Theme: {settings.UiSettings.Theme}, ReactiveArtwork: {ReactiveArtworkEnabled}, Scale: {ArtworkScaleRangeStart}%-{ArtworkScaleRangeEnd}%, LogLevel: {settings.Debug.Logger.Level}");
        }
        finally
        {
            _isUpdatingFromSettings = false;
        }
    }

    partial void OnSelectedThemeChanged(ThemeItem? value)
    {
        if (!_isUpdatingFromSettings && value != null)
        {
            _settingsManager.Application.UiSettings.Theme = value.Theme;
            _themeService.SetTheme(value.Theme);
            SaveSettings();
            Logger.Info($"Theme changed to: {value.DisplayName}");
        }
    }

    partial void OnReactiveArtworkEnabledChanged(bool value)
    {
        if (!_isUpdatingFromSettings)
        {
            _settingsManager.Application.Playback.ReactiveArtwork.Enabled = value;
            SaveSettings();
            Logger.Debug($"ReactiveArtwork enabled changed to: {value}");
        }
    }

    partial void OnArtworkScaleRangeStartChanged(double value)
    {
        if (!_isUpdatingFromSettings)
        {
            _settingsManager.Application.Playback.ReactiveArtwork.Scale.Base = value / 100;
            OnPropertyChanged(nameof(ScaleRangeText));
            SaveSettingsDebounced();
            Logger.Trace($"Artwork scale base changed to: {value}%");
        }
    }

    partial void OnArtworkScaleRangeEndChanged(double value)
    {
        if (!_isUpdatingFromSettings)
        {
            _settingsManager.Application.Playback.ReactiveArtwork.Scale.Max = value / 100;
            OnPropertyChanged(nameof(ScaleRangeText));
            SaveSettingsDebounced();
            Logger.Trace($"Artwork scale max changed to: {value}%");
        }
    }

    partial void OnSelectedLogLevelChanged(LogLevelItem? value)
    {
        if (!_isUpdatingFromSettings && value != null)
        {
            _settingsManager.Application.Debug.Logger.Level = LogLevelHelper.ToString(value.Level);
            Logger.SetLogLevel(value.Level);
            SaveSettings();
            Logger.Info($"Log level changed to: {value.DisplayName}");
        }
    }


    private void SaveSettings()
    {
        try
        {
            _settingsManager.SaveAll();
            Logger.Info("Settings saved successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save settings");
            Logger.LogExceptionDetails(ex);
        }
    }

    private async void SaveSettingsDebounced()
    {
        _saveDebounceTimer?.Cancel();
        _saveDebounceTimer = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, _saveDebounceTimer.Token);
            SaveSettings();
        }
        catch (TaskCanceledException)
        {
            // Skipping current save since there was another change
        }
    }

    public void Dispose()
    {
        _saveDebounceTimer?.Cancel();
        _saveDebounceTimer?.Dispose();
        _settingsManager.SettingsChanged -= OnSettingsChanged;
    }
}