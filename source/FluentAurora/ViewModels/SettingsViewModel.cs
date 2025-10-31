using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Settings;

namespace FluentAurora.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    // Properties
    private readonly ISettingsManager _settingsManager;
    private bool _isUpdatingFromSettings;
    private CancellationTokenSource? _saveDebounceTimer;
    
    [ObservableProperty] private bool reactiveArtworkEnabled;
    [ObservableProperty] private double artworkScaleRangeStart;
    [ObservableProperty] private double artworkScaleRangeEnd;
    
    public string ScaleRangeText => $"{ArtworkScaleRangeStart:F0}% - {ArtworkScaleRangeEnd:F0}%";

    // Constructor
    public SettingsViewModel(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        ApplySettings(_settingsManager.Application);
        _settingsManager.SettingsChanged += OnSettingsChanged;
    }

    // Functions
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
            ReactiveArtworkEnabled = settings.Playback.ReactiveArtwork.Enabled;
            ArtworkScaleRangeStart = settings.Playback.ReactiveArtwork.Scale.Base * 100;
            ArtworkScaleRangeEnd = settings.Playback.ReactiveArtwork.Scale.Max * 100;

            Logger.Info($"Settings loaded - ReactiveArtwork: {ReactiveArtworkEnabled}, Scale: {ArtworkScaleRangeStart}%-{ArtworkScaleRangeEnd}%");
        }
        finally
        {
            _isUpdatingFromSettings = false;
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