using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FluentAurora.Converters;

public class QueueBoolToBackgroundConverter : IValueConverter
{
    public static readonly QueueBoolToBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying && isPlaying)
        {
            // TODO: Make it use Accent Color
            return new SolidColorBrush(Colors.Orange) { Opacity = 0.2 };
        }

        // Get the default card background brush from theme resources
        if (Application.Current?.TryFindResource("CardBackgroundFillColorDefaultBrush", out var defaultBrush) == true && defaultBrush is IBrush brush)
        {
            return brush;
        }

        // Fallback if resource not found
        return new SolidColorBrush(Color.FromRgb(50, 50, 50));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}