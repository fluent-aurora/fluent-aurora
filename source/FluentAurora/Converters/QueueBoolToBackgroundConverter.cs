using System;
using System.Globalization;
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
            // TODO: Replace this with AccentColor
            return new SolidColorBrush(Colors.Orange) { Opacity = 0.2 };
        }

        // Default background when song is not playing
        return new SolidColorBrush(Color.FromRgb(50, 50, 50));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}