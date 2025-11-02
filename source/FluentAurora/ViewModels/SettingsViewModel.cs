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

namespace FluentAurora.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    public class ThemeItem
    {
        public AppTheme Theme { get; set; }
        public string DisplayName { get; set; } = string.Empty;
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

    public ObservableCollection<ThemeItem> AvailableThemes { get; }

    public string ScaleRangeText => $"{ArtworkScaleRangeStart:F0}% - {ArtworkScaleRangeEnd:F0}%";

    // Constructor
    public SettingsViewModel(ISettingsManager settingsManager, ThemeService themeService)
    {
        _settingsManager = settingsManager;
        _themeService = themeService;
        
        AvailableThemes = new ObservableCollection<ThemeItem>();
        InitializeThemes();

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

            Logger.Info($"Settings loaded - Theme: {settings.UiSettings.Theme}, ReactiveArtwork: {ReactiveArtworkEnabled}, Scale: {ArtworkScaleRangeStart}%-{ArtworkScaleRangeEnd}%");
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