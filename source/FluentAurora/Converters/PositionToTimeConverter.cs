using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FluentAurora.Converters;

public class PositionToTimeConverter : IMultiValueConverter
{
    public static readonly PositionToTimeConverter Instance = new();
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
        {
            return string.Empty;
        }

        if (values[0] is int current && values[1] is int duration)
        {
            static string Format(int seconds) => TimeSpan.FromSeconds((seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");
            return $"{Format(current)} / {Format(duration)}";
        }

        return string.Empty;
    }
}