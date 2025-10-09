using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FluentAurora.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAurora.Controls;

public partial class ExpandedPlaybackControl : UserControl
{
    // Properties
    private readonly ExtendedPlaybackControlViewModel? _viewModel;
    private bool _pointerPressed = false;
    private Point _pressedPoint;

    public ExpandedPlaybackControl()
    {
        InitializeComponent();
        _viewModel = App.Services?.GetRequiredService<ExtendedPlaybackControlViewModel>();
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
    }

    // Events
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