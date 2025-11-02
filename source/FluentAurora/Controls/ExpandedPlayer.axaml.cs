using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Playback;
using FluentAurora.Core.Settings;
using FluentAurora.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAurora.Controls;

public partial class ExpandedPlayer : UserControl
{
    // Properties
    private readonly ExpandedPlayerViewModel? _viewModel;
    private readonly AudioPlayerService? _audioPlayerService;
    private readonly ISettingsManager? _settingsManager;
    private bool _pointerPressed = false;
    private Point _pressedPoint;
    private ReactiveArtwork? _reactiveArtwork;

    public ExpandedPlayer()
    {
        InitializeComponent();
        _viewModel = App.Services?.GetRequiredService<ExpandedPlayerViewModel>();
        _audioPlayerService = App.Services?.GetRequiredService<AudioPlayerService>();
        _settingsManager = App.Services?.GetRequiredService<ISettingsManager>();
        DataContext = _viewModel;

        // Wire up seeking events
        Slider? progressSlider = this.FindControl<Slider>("ExpandedProgressSlider");
        if (progressSlider != null)
        {
            progressSlider.AddHandler(PointerPressedEvent, OnSliderPointerPressed, handledEventsToo: true);
            progressSlider.AddHandler(PointerMovedEvent, OnSliderPointerMoved, handledEventsToo: true);
            progressSlider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased, handledEventsToo: true);
            progressSlider.AddHandler(PointerCaptureLostEvent, OnSliderPointerCaptureLost, handledEventsToo: true);
        }

        // Wire up visualizer events
        _reactiveArtwork = this.FindControl<ReactiveArtwork>("AmbientVisualizer");
        if (_audioPlayerService != null)
        {
            _audioPlayerService.SpectrumDataAvailable += OnSpectrumDataAvailable;
            _audioPlayerService.PlaybackStarted += OnPlaybackStarted;
            _audioPlayerService.PlaybackStopped += OnPlaybackStopped;
        }

        // Settings changes
        if (_settingsManager != null)
        {
            _settingsManager.SettingsChanged += OnSettingsChanged;
        }
    }

    // Events
    private void OnQueueItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only handle left clicks
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (sender is Border border && border.DataContext is ExpandedPlayerViewModel.QueueItemViewModel item)
            {
                _viewModel?.PlayQueueItemCommand.Execute(item);
                e.Handled = true;
            }
        }
    }
    private void OnSettingsChanged(object? sender, ApplicationSettingsStore settings)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            bool isEnabled = settings.Playback.ReactiveArtwork.Enabled;
            bool isPlaying = _audioPlayerService?.IsPlaying ?? false;

            Logger.Debug($"OnSettingsChanged: ReactiveArtwork enabled={isEnabled}, isPlaying={isPlaying}");

            if (_reactiveArtwork != null)
            {
                if (isEnabled && isPlaying)
                {
                    // Starting visualization because it was just enabled and music is already playing
                    Logger.Debug("OnSettingsChanged: Starting ReactiveArtwork");
                    _reactiveArtwork.Start();
                }
                else if (!isEnabled)
                {
                    // Stopping the reactive artwork because the setting was disabled
                    Logger.Debug("OnSettingsChanged: Stopping ReactiveArtwork");
                    _reactiveArtwork.Stop();
                }
            }
        });
    }

    private void OnSpectrumDataAvailable(float[] data)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Reactive Artwork is disabled, no need to process spectrum data
            if (_viewModel?.IsReactiveArtworkEnabled != true)
            {
                return;
            }

            if (data == null || data.Length == 0)
            {
                Logger.Warning($"OnSpectrumDataAvailable: Invalid spectrum data - {(data == null ? "null" : "empty array")}");
                return;
            }

            Logger.Trace($"OnSpectrumDataAvailable: Processing {data.Length} spectrum samples");

            // Weighted average intensity
            float totalIntensity = 0;
            float totalWeight = 0;

            for (int i = 0; i < data.Length; i++)
            {
                // Favouring mid to high frequencies (Exponentially more weight goes to them)
                float normalizedPosition = i / (float)data.Length;
                float weight = 1.0f + (float)Math.Pow(normalizedPosition, 2) * 8.0f;

                totalIntensity += data[i] * weight;
                totalWeight += weight;
            }

            float avgIntensity = totalIntensity / totalWeight;
            Logger.Trace($"OnSpectrumDataAvailable: Weighted average intensity: {avgIntensity:F4}");

            // Squaring the intensity for more dramatic peaks
            avgIntensity = avgIntensity * avgIntensity;
            Logger.Trace($"OnSpectrumDataAvailable: After squaring: {avgIntensity:F4}");

            // Amplifying for even bigger effect
            float beforeAmplification = avgIntensity;
            avgIntensity = Math.Min(1.0f, avgIntensity * 8.0f);

            if (beforeAmplification * 8.0f > 1.0f)
            {
                Logger.Trace($"OnSpectrumDataAvailable: Intensity clamped from {beforeAmplification * 8.0f:F4} to 1.0");
            }

            Logger.Trace($"OnSpectrumDataAvailable: Final intensity: {avgIntensity:F4}");

            // Updating the visualizer
            if (_reactiveArtwork != null)
            {
                _reactiveArtwork.UpdateIntensity(avgIntensity);
            }
            else
            {
                Logger.Warning("OnSpectrumDataAvailable: ReactiveArtwork is null, cannot update intensity");
            }
        });
    }

    private void OnPlaybackStarted()
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_viewModel?.IsReactiveArtworkEnabled == true && _reactiveArtwork != null)
            {
                Logger.Debug("OnPlaybackStarted: Starting ReactiveArtwork (enabled in settings)");
                _reactiveArtwork.Start();
            }
            else
            {
                Logger.Debug("OnPlaybackStarted: ReactiveArtwork disabled in settings, not starting");
            }
        });
    }

    private void OnPlaybackStopped()
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            _reactiveArtwork?.Stop();
        });
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Slider slider)
        {
            return;
        }

        _pointerPressed = true;
        _pressedPoint = e.GetPosition(slider);
        _viewModel?.PrepareForPotentialSeeking();

        // For immediate click feedback calculate and apply the position
        if (_viewModel != null && slider.Bounds.Width > 0)
        {
            double ratio = Math.Max(0, Math.Min(1, _pressedPoint.X / slider.Bounds.Width));
            int targetPosition = (int)(ratio * _viewModel.SongDuration);
            _viewModel.DisplayPosition = targetPosition;
        }
    }

    private void OnSliderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pointerPressed)
        {
            Point currentPoint = e.GetPosition((Control?)sender);
            double distance = Math.Abs(currentPoint.X - _pressedPoint.X);

            // Only start dragging if moved more than 3 pixels
            // This prevents accidental drags when clicking
            if (distance > 3)
            {
                _viewModel?.StartDragging();
            }
        }
    }

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pointerPressed)
        {
            _pointerPressed = false;
            _viewModel?.EndInteraction();
        }
    }

    private void OnSliderPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_pointerPressed)
        {
            _pointerPressed = false;
            _viewModel?.EndInteraction();
        }
    }
}