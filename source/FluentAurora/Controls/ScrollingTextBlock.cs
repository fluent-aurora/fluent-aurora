using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace FluentAurora.Controls;

public class ScrollingTextBlock : Control
{
    // Properties
    private DispatcherTimer? _scrollTimer;
    private DispatcherTimer? _pauseTimer;
    private double _offset = 0;
    private double _textWidth;
    private bool _needsScrolling;
    private FormattedText? _formattedText;

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<ScrollingTextBlock, string>(nameof(Text), string.Empty);
    public static readonly StyledProperty<bool> CenterTextProperty = AvaloniaProperty.Register<ScrollingTextBlock, bool>(nameof(CenterText), false);
    public static readonly StyledProperty<double> FontSizeProperty = TextBlock.FontSizeProperty.AddOwner<ScrollingTextBlock>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextBlock.FontWeightProperty.AddOwner<ScrollingTextBlock>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextBlock.FontFamilyProperty.AddOwner<ScrollingTextBlock>();
    public static readonly StyledProperty<FontStyle> FontStyleProperty = TextBlock.FontStyleProperty.AddOwner<ScrollingTextBlock>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextBlock.ForegroundProperty.AddOwner<ScrollingTextBlock>();
    public static readonly StyledProperty<double> ScrollSpeedProperty = AvaloniaProperty.Register<ScrollingTextBlock, double>(nameof(ScrollSpeed), 30);
    public static readonly StyledProperty<int> PauseBeforeScrollProperty = AvaloniaProperty.Register<ScrollingTextBlock, int>(nameof(PauseBeforeScroll), 2000);
    public static readonly StyledProperty<double> ScrollGapProperty = AvaloniaProperty.Register<ScrollingTextBlock, double>(nameof(ScrollGap), 50);
    public static readonly StyledProperty<bool> ShowToolTipProperty = AvaloniaProperty.Register<ScrollingTextBlock, bool>(nameof(ShowToolTip), true);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool CenterText
    {
        get => GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public FontStyle FontStyle
    {
        get => GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double ScrollSpeed
    {
        get => GetValue(ScrollSpeedProperty);
        set => SetValue(ScrollSpeedProperty, value);
    }

    public int PauseBeforeScroll
    {
        get => GetValue(PauseBeforeScrollProperty);
        set => SetValue(PauseBeforeScrollProperty, value);
    }

    public double ScrollGap
    {
        get => GetValue(ScrollGapProperty);
        set => SetValue(ScrollGapProperty, value);
    }

    public bool ShowToolTip
    {
        get => GetValue(ShowToolTipProperty);
        set => SetValue(ShowToolTipProperty, value);
    }

    // Constructors
    static ScrollingTextBlock()
    {
        AffectsRender<ScrollingTextBlock>(TextProperty, FontSizeProperty, FontWeightProperty, ForegroundProperty);
        AffectsMeasure<ScrollingTextBlock>(TextProperty, FontSizeProperty, FontWeightProperty);
        BoundsProperty.Changed.AddClassHandler<ScrollingTextBlock>((x, e) => x.CheckIfScrollingNeeded());
        TextProperty.Changed.AddClassHandler<ScrollingTextBlock>((x, e) => x.UpdateToolTip());
    }

    public ScrollingTextBlock()
    {
        _scrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _scrollTimer.Tick += OnScrollTimerTick;
        _pauseTimer = new DispatcherTimer();
        _pauseTimer.Tick += OnPauseTimerTick;

        UpdateToolTip();
    }

    // Events
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == FontSizeProperty || change.Property == FontWeightProperty || change.Property == FontFamilyProperty || change.Property == FontStyleProperty)
        {
            ResetScrolling();
        }
        if (change.Property == ShowToolTipProperty)
        {
            UpdateToolTip();
        }
    }

    private void UpdateToolTip()
    {
        if (ShowToolTip && !string.IsNullOrEmpty(Text))
        {
            ToolTip.SetTip(this, Text);
        }
        else
        {
            ToolTip.SetTip(this, null);
        }
    }

    private void ResetScrolling()
    {
        _offset = 0;
        _scrollTimer?.Stop();
        _pauseTimer?.Stop();
        _formattedText = null;
        InvalidateVisual();
        Dispatcher.UIThread.Post(CheckIfScrollingNeeded, DispatcherPriority.Background);
    }

    private void CheckIfScrollingNeeded()
    {
        if (Bounds.Width <= 0 || string.IsNullOrEmpty(Text))
        {
            _needsScrolling = false;
            _scrollTimer?.Stop();
            _pauseTimer?.Stop();
            return;
        }
        Typeface typeface = new Typeface(FontFamily, FontStyle, FontWeight);
        IBrush foreground = Foreground ?? new SolidColorBrush(Colors.White);

        _formattedText = new FormattedText(Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, foreground);
        _textWidth = _formattedText.Width;
        _needsScrolling = _textWidth > Bounds.Width;
        if (_needsScrolling)
        {
            StartInitialPause();
        }
        else
        {
            _scrollTimer?.Stop();
            _pauseTimer?.Stop();
            InvalidateVisual();
        }
    }

    private void StartInitialPause()
    {
        _offset = 0;
        _pauseTimer!.Interval = TimeSpan.FromMilliseconds(PauseBeforeScroll);
        _pauseTimer.Start();
    }

    private void OnPauseTimerTick(object? sender, EventArgs e)
    {
        _pauseTimer?.Stop();
        _scrollTimer?.Start();
    }

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        if (!_needsScrolling)
        {
            return;
        }
        _offset -= ScrollSpeed / 60.0;
        // Reset when text 1 has completely scrolled off the screen
        if (_offset <= -(_textWidth + ScrollGap))
        {
            _offset = 0;
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = FontSize * 1.5;
        return new Size(availableSize.Width, height);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(CheckIfScrollingNeeded, DispatcherPriority.Background);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scrollTimer?.Stop();
        _pauseTimer?.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (string.IsNullOrEmpty(Text) || Bounds.Width <= 0)
        {
            return;
        }
        if (_formattedText == null)
        {
            Typeface typeface = new Typeface(FontFamily, FontStyle, FontWeight);
            IBrush foreground = Foreground ?? new SolidColorBrush(Colors.White);
            _formattedText = new FormattedText(Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, foreground);
        }
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            double yPos = (Bounds.Height - _formattedText.Height) / 2;
            if (_needsScrolling)
            {
                // Draw the text twice for loop
                context.DrawText(_formattedText, new Point(_offset, yPos));
                context.DrawText(_formattedText, new Point(_offset + _textWidth + ScrollGap, yPos));
            }
            else
            {
                // Center the text if there's no scrolling
                double xPos = CenterText
                    ? (Bounds.Width - _formattedText.Width) / 2 // centered
                    : 0; // left-aligned
                context.DrawText(_formattedText, new Point(xPos, yPos));
            }
        }
    }
}