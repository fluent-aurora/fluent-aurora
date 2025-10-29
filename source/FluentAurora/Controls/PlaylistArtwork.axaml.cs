using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

namespace FluentAurora.Controls;

public partial class PlaylistArtwork : UserControl
{
    public static readonly StyledProperty<byte[]?> ArtworkProperty = AvaloniaProperty.Register<PlaylistArtwork, byte[]?>(nameof(Artwork));

    public static readonly StyledProperty<byte[]?> CustomArtworkProperty = AvaloniaProperty.Register<PlaylistArtwork, byte[]?>(nameof(CustomArtwork));

    public byte[]? Artwork
    {
        get => GetValue(ArtworkProperty);
        set => SetValue(ArtworkProperty, value);
    }

    public byte[]? CustomArtwork
    {
        get => GetValue(CustomArtworkProperty);
        set => SetValue(CustomArtworkProperty, value);
    }

    private Border? _defaultArtwork;
    private Image? _singleArtwork;

    public PlaylistArtwork()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _defaultArtwork = this.FindControl<Border>("DefaultArtwork");
        _singleArtwork = this.FindControl<Image>("SingleArtwork");
        UpdateArtworkDisplay();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ArtworkProperty || change.Property == CustomArtworkProperty)
        {
            UpdateArtworkDisplay();
        }
    }

    private void UpdateArtworkDisplay()
    {
        // Return early if template hasn't been applied yet
        if (_defaultArtwork == null || _singleArtwork == null)
        {
            return;
        }

        // Reset visibility
        _defaultArtwork.IsVisible = true;
        _singleArtwork.IsVisible = false;
        
        // Custom artwork, song artwork
        byte[]? imageData = CustomArtwork ?? Artwork;
        if (imageData is not { Length: > 0 })
        {
            return;
        }

        Bitmap? bitmap = LoadBitmapFromBytes(imageData);
        if (bitmap == null)
        {
            return;
        }

        _singleArtwork.Source = bitmap;
        _singleArtwork.IsVisible = true;
        _defaultArtwork.IsVisible = false;
    }

    private Bitmap? LoadBitmapFromBytes(byte[] data)
    {
        try
        {
            using MemoryStream stream = new MemoryStream(data);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}