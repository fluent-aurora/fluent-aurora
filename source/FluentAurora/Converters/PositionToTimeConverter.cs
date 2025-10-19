using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FluentAurora.Converters;

public class PositionToTimeConverter : IValueConverter, IMultiValueConverter
{
    // Singleton instance
    public static readonly PositionToTimeConverter Instance = new PositionToTimeConverter();

    /// <summary>
    /// Formats seconds to h:mm:ss or mm:ss
    /// </summary>
    private static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");

    /// <summary>
    /// Helper to convert any numeric type to double
    /// </summary>
    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case long l:
                result = l;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    /// <summary>
    /// Single value conversion (milliseconds)
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryConvertToDouble(value, out double ms))
        {
            return string.Empty;
        }

        return Format(ms / 1000.0);
    }

    /// <summary>
    /// Multi-value conversion: current / duration
    /// </summary>
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count != 2)
        {
            return string.Empty;
        }

        if (!TryConvertToDouble(values[0], out double currentMs) || !TryConvertToDouble(values[1], out double durationMs))
        {
            return string.Empty;
        }

        return $"{Format(currentMs / 1000.0)} / {Format(durationMs / 1000.0)}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}