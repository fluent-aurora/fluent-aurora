using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FluentAurora.Converters;

public class PositionToTimeConverter : IValueConverter, IMultiValueConverter
{
    // Properties
    public static readonly PositionToTimeConverter Instance = new PositionToTimeConverter();

    // Methods
    /// <summary>
    /// Formats the value (in seconds) to a format (hours\minutes\seconds)
    /// </summary>
    /// <param name="seconds">Value (in milliseconds)</param>
    /// <returns>Formatted value in hours\minutes\seconds format</returns>
    private static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");

    /// <summary>
    /// Single value conversion
    /// </summary>
    /// <param name="value">Value (in milliseconds)</param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns>Formated Value if it is present, otherwise an empty string</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int currentMs)
        {
            return string.Empty;
        }
        double currentSec = currentMs / 1000.0;
        return $"{Format(currentSec)}";
    }

    /// <summary>
    /// Multi-value conversion
    /// </summary>
    /// <param name="values">Values (in milliseconds)</param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns>Formated values (Current Position/Song Duration) if values are present, otherwise an empty string</returns>
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [int currentMs, int durationMs])
        {
            return string.Empty;
        }
        double currentSec = currentMs / 1000.0;
        double durationSec = durationMs / 1000.0;
        return $"{Format(currentSec)} / {Format(durationSec)}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}