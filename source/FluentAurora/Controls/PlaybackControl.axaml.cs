using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FluentAurora.ViewModels;

namespace FluentAurora.Controls;

public partial class PlaybackControl : UserControl
{
    // Properties
    private PlaybackControlViewModel? _viewModel;
    private bool _pointerPressed = false;
    private Point _pressedPoint;

    // Constructors
    public PlaybackControl()
    {
        InitializeComponent();
        _viewModel = new PlaybackControlViewModel();
        DataContext = _viewModel;

        // Wire up seeking events
        if (this.FindControl<Slider>("ProgressSlider") is { } slider)
        {
            slider.AddHandler(PointerPressedEvent, OnSliderPointerPressed, handledEventsToo: true);
            slider.AddHandler(PointerMovedEvent, OnSliderPointerMoved, handledEventsToo: true);
            slider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased, handledEventsToo: true);
            slider.AddHandler(PointerCaptureLostEvent, OnSliderPointerCaptureLost, handledEventsToo: true);
        }
    }

    // Events
    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pointerPressed = true;
        _pressedPoint = e.GetPosition((Control?)sender);
        _viewModel?.PrepareForPotentialSeeking();
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