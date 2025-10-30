using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentIcons.Avalonia.Fluent;
using FluentAurora.Core.Logging;

namespace FluentAurora.Controls;

public class ReactiveArtwork : Control
{
    // Variables
    private readonly DispatcherTimer _animationTimer;
    private double _currentScale = 1.0;
    private double _targetScale = 1.0;
    private double _currentOpacity = 0.3;
    private double _targetOpacity = 0.3;
    private readonly SymbolIcon _placeholderIcon;
    private bool _isStopping = false;

    // Window Properties
    public static readonly StyledProperty<Bitmap?> SourceImageProperty = AvaloniaProperty.Register<ReactiveArtwork, Bitmap?>(nameof(SourceImage));

    public static readonly StyledProperty<double> BaseScaleProperty = AvaloniaProperty.Register<ReactiveArtwork, double>(nameof(BaseScale), 0.8);

    public static readonly StyledProperty<double> MaxScaleProperty = AvaloniaProperty.Register<ReactiveArtwork, double>(nameof(MaxScale), 1.3);

    public static readonly StyledProperty<double> BaseOpacityProperty = AvaloniaProperty.Register<ReactiveArtwork, double>(nameof(BaseOpacity), 1);

    public static readonly StyledProperty<double> MaxOpacityProperty = AvaloniaProperty.Register<ReactiveArtwork, double>(nameof(MaxOpacity), 1);

    public static readonly StyledProperty<double> CornerRadiusProperty = AvaloniaProperty.Register<ReactiveArtwork, double>(nameof(CornerRadius), 12.0);

    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<ReactiveArtwork, bool>(nameof(IsActive), false);

    public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty = AvaloniaProperty.Register<ReactiveArtwork, IBrush?>(nameof(PlaceholderBrush), CreateDefaultPlaceholderBrush());

    public static readonly StyledProperty<double> TransitionDurationProperty = AvaloniaProperty.Register<ReactiveArtwork, double>(nameof(TransitionDuration), 0.5);

    public Bitmap? SourceImage
    {
        get => GetValue(SourceImageProperty);
        set => SetValue(SourceImageProperty, value);
    }

    public double BaseScale
    {
        get => GetValue(BaseScaleProperty);
        set => SetValue(BaseScaleProperty, value);
    }

    public double MaxScale
    {
        get => GetValue(MaxScaleProperty);
        set => SetValue(MaxScaleProperty, value);
    }

    public double BaseOpacity
    {
        get => GetValue(BaseOpacityProperty);
        set => SetValue(BaseOpacityProperty, value);
    }

    public double MaxOpacity
    {
        get => GetValue(MaxOpacityProperty);
        set => SetValue(MaxOpacityProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public IBrush? PlaceholderBrush
    {
        get => GetValue(PlaceholderBrushProperty);
        set => SetValue(PlaceholderBrushProperty, value);
    }

    public double TransitionDuration
    {
        get => GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    static ReactiveArtwork()
    {
        AffectsRender<ReactiveArtwork>(SourceImageProperty, IsActiveProperty, CornerRadiusProperty, PlaceholderBrushProperty);
        SourceImageProperty.Changed.AddClassHandler<ReactiveArtwork>((x, e) => x.OnSourceImageChanged(e));
        IsActiveProperty.Changed.AddClassHandler<ReactiveArtwork>((x, e) => x.OnIsActiveChanged(e));
        BaseScaleProperty.Changed.AddClassHandler<ReactiveArtwork>((x, e) => x.OnBaseScaleChanged(e));
        MaxScaleProperty.Changed.AddClassHandler<ReactiveArtwork>((x, e) => x.OnMaxScaleChanged(e));
    }

    private static IBrush CreateDefaultPlaceholderBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromRgb(0x3D, 0x3D, 0x3D), 0),
                new GradientStop(Color.FromRgb(0x2D, 0x2D, 0x2D), 1)
            }
        };
    }

    // Constructors
    public ReactiveArtwork()
    {
        Logger.Debug("AmbientVisualizer: Initializing control");

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _animationTimer.Tick += OnAnimationTick;

        _currentScale = BaseScale;
        _targetScale = BaseScale;
        _currentOpacity = BaseOpacity;
        _targetOpacity = BaseOpacity;

        // Create placeholder icon
        _placeholderIcon = new SymbolIcon
        {
            Symbol = FluentIcons.Common.Symbol.MusicNote2,
            FontSize = 64,
            Opacity = 0.3,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsVisible = false // Start invisible
        };

        VisualChildren.Add(_placeholderIcon);
        LogicalChildren.Add(_placeholderIcon);

        Logger.Debug("AmbientVisualizer: Control initialized successfully");
    }

    // Functions
    // Events
    private void OnSourceImageChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is Bitmap bitmap)
        {
            Logger.Info($"AmbientVisualizer: Source image changed - Size: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
        }
        else
        {
            Logger.Info("AmbientVisualizer: Source image cleared, using placeholder");
        }

        UpdatePlaceholderVisibility();
    }

    private void OnBaseScaleChanged(AvaloniaPropertyChangedEventArgs e)
    {
        double newBaseScale = (double)(e.NewValue ?? 0.8);
        Logger.Trace($"ReactiveArtwork: BaseScale changed from {e.OldValue} to {newBaseScale}");

        if (!IsActive)
        {
            // Update current and target scale when not active
            _currentScale = newBaseScale;
            _targetScale = newBaseScale;
            InvalidateVisual();
            InvalidateArrange();
            Logger.Debug($"ReactiveArtwork: Updated current scale to {newBaseScale} (inactive)");
        }
        else
        {
            // If already active, animation will handle the transition to new scale
            Logger.Debug($"ReactiveArtwork: BaseScale updated while active, will affect next intensity calculation");
        }
    }

    private void OnMaxScaleChanged(AvaloniaPropertyChangedEventArgs e)
    {
        // This is only for Logging purposes, updating is done by the animation and this isn't used when Reactive part isn't active
        double newMaxScale = (double)(e.NewValue ?? 1.3);
        Logger.Trace($"ReactiveArtwork: MaxScale changed from {e.OldValue} to {newMaxScale}");
        
        if (IsActive)
        {
            Logger.Debug($"ReactiveArtwork: MaxScale updated while active, will affect next intensity calculation");
        }
    }

    private void OnIsActiveChanged(AvaloniaPropertyChangedEventArgs e)
    {
        bool isActive = (bool)(e.NewValue ?? false);
        Logger.Info($"AmbientVisualizer: IsActive changed to {isActive}");

        // Reset to base scale when becoming active
        if (!isActive)
        {
            _currentScale = BaseScale;
            _targetScale = BaseScale;
            _currentOpacity = BaseOpacity;
            _targetOpacity = BaseOpacity;
            InvalidateVisual();
            InvalidateArrange();
        }

        UpdatePlaceholderVisibility();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        // Smooth interpolation
        double interpolationFactor = 0.5; // Default for active state

        if (_isStopping)
        {
            // Slow transition when stopping for smoother effect
            interpolationFactor = 0.15;
        }

        _currentScale = _currentScale * (1 - interpolationFactor) + _targetScale * interpolationFactor;
        _currentOpacity = _currentOpacity * (1 - interpolationFactor) + _targetOpacity * interpolationFactor;

        InvalidateVisual();
        InvalidateArrange(); // Re-arrange to update icon transform

        // Check if artwork has reached base values while stopping
        if (_isStopping)
        {
            double scaleDiff = Math.Abs(_currentScale - BaseScale);
            double opacityDiff = Math.Abs(_currentOpacity - BaseOpacity);

            // If very close to base values, snap and stop
            if (scaleDiff < 0.001 && opacityDiff < 0.001)
            {
                _currentScale = BaseScale;
                _currentOpacity = BaseOpacity;
                _isStopping = false;
                _animationTimer.Stop();
                InvalidateVisual();
                InvalidateArrange();

                Logger.Debug("AmbientVisualizer: Transition to base values completed, animation stopped");
            }
        }
    }

    // Methods
    private void UpdatePlaceholderVisibility()
    {
        bool shouldShowPlaceholder = SourceImage == null;
        _placeholderIcon.IsVisible = shouldShowPlaceholder;

        Logger.Debug($"AmbientVisualizer: Placeholder visibility set to {shouldShowPlaceholder}");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _placeholderIcon.Measure(availableSize);
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Calculate scaled dimension for the icon (Base * Max)
        double baseWidth = 400;
        double baseHeight = 400;
        double scaledWidth = baseWidth * _currentScale;
        double scaledHeight = baseHeight * _currentScale;

        double centerX = finalSize.Width / 2;
        double centerY = finalSize.Height / 2;

        Rect iconRect = new Rect(
            centerX - scaledWidth / 2,
            centerY - scaledHeight / 2,
            scaledWidth,
            scaledHeight);

        _placeholderIcon.Arrange(iconRect);

        // This is to make sure the SymbolIcon is scaling with the image
        _placeholderIcon.RenderTransform = new ScaleTransform(_currentScale, _currentScale);
        _placeholderIcon.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        return base.ArrangeOverride(finalSize);
    }

    public void UpdateIntensity(float intensity)
    {
        if (!IsActive)
        {
            return;
        }

        // Intensity must be [0,1]
        float originalIntensity = intensity;
        intensity = Math.Clamp(intensity, 0f, 1f);

        if (originalIntensity != intensity)
        {
            Logger.Warning($"AmbientVisualizer: Intensity clamped from {originalIntensity:F3} to {intensity:F3}");
        }

        // Exponential scaling for a more dramatic effect
        intensity = (float)Math.Pow(intensity, 0.5);

        _targetScale = BaseScale + (MaxScale - BaseScale) * intensity;
        _targetOpacity = BaseOpacity + (MaxOpacity - BaseOpacity) * intensity;

        Logger.Trace($"AmbientVisualizer: Intensity updated - Target Scale: {_targetScale:F3}, Target Opacity: {_targetOpacity:F3}");

        InvalidateVisual();
    }

    public void Start()
    {
        Logger.Info("AmbientVisualizer: Starting animation");

        IsActive = true;
        _isStopping = false;
        
        _currentScale = BaseScale;
        _targetScale = BaseScale;
        _currentOpacity = BaseOpacity;
        _targetOpacity = BaseOpacity;

        _animationTimer.Start();
        UpdatePlaceholderVisibility();

        Logger.Debug($"AmbientVisualizer: Animation started - BaseScale: {BaseScale}, MaxScale: {MaxScale}, BaseOpacity: {BaseOpacity}, MaxOpacity: {MaxOpacity}");
    }

    public void Stop()
    {
        Logger.Info("AmbientVisualizer: Stopping animation (transitioning to base values)");

        IsActive = false;
        _isStopping = true;

        // Base values for smooth transition
        _targetScale = BaseScale;
        _targetOpacity = BaseOpacity;

        // Timer keeps running for the transition and will auto-stop when the image is scaled down to base values
        UpdatePlaceholderVisibility();
    }

    private void Reset()
    {
        Logger.Debug("AmbientVisualizer: Resetting to base values");

        _currentScale = BaseScale;
        _targetScale = BaseScale;
        _currentOpacity = BaseOpacity;
        _targetOpacity = BaseOpacity;
        InvalidateVisual();
    }

    private StreamGeometry CreateRoundedRectGeometry(Rect rect, double cornerRadius)
    {
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            double x = rect.X;
            double y = rect.Y;
            double width = rect.Width;
            double height = rect.Height;
            double radius = Math.Min(cornerRadius, Math.Min(width, height) / 2);

            // Going from top-left after the corner
            context.BeginFigure(new Point(x + radius, y), true);

            // Top edge, top-right corner
            context.LineTo(new Point(x + width - radius, y));
            context.ArcTo(
                new Point(x + width, y + radius),
                new Size(radius, radius),
                0,
                false,
                SweepDirection.Clockwise);

            // Right edge, bottom-right corner
            context.LineTo(new Point(x + width, y + height - radius));
            context.ArcTo(
                new Point(x + width - radius, y + height),
                new Size(radius, radius),
                0,
                false,
                SweepDirection.Clockwise);

            // Bottom edge, bottom-left corner
            context.LineTo(new Point(x + radius, y + height));
            context.ArcTo(
                new Point(x, y + height - radius),
                new Size(radius, radius),
                0,
                false,
                SweepDirection.Clockwise);

            // Left edge, bottom-left corner
            context.LineTo(new Point(x, y + radius));
            context.ArcTo(
                new Point(x + radius, y),
                new Size(radius, radius),
                0,
                false,
                SweepDirection.Clockwise);

            context.EndFigure(true);
        }
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        try
        {
            Rect bounds = Bounds;
            double centerX = bounds.Width / 2;
            double centerY = bounds.Height / 2;

            // Matching the base dimensions (400x400)
            double baseWidth = 400;
            double baseHeight = 400;
            double scaledWidth = baseWidth * _currentScale;
            double scaledHeight = baseHeight * _currentScale;
            double scaledCornerRadius = CornerRadius * _currentScale;

            Rect rect = new Rect(
                centerX - scaledWidth / 2,
                centerY - scaledHeight / 2,
                scaledWidth,
                scaledHeight);

            StreamGeometry geometry = CreateRoundedRectGeometry(rect, scaledCornerRadius);

            // Check if the image is available, use placeholder as backup
            if (SourceImage != null)
            {
                // ImageBrush contains the artwork with settings
                ImageBrush imageBrush = new ImageBrush(SourceImage)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                using (context.PushOpacity(_currentOpacity))
                {
                    context.DrawGeometry(imageBrush, null, geometry);
                }
            }
            else if (PlaceholderBrush != null)
            {
                // Rendering placeholder and it's gradient which is used by the artwork border
                using (context.PushOpacity(_currentOpacity))
                {
                    context.DrawGeometry(PlaceholderBrush, null, geometry);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"AmbientVisualizer: Error during render - {ex.Message}");
        }
    }
}