using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FluentAurora.Converters;

public class BoolToViewTextConverter : IValueConverter
{
    public static readonly BoolToViewTextConverter Instance = new BoolToViewTextConverter();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool showAllSongs = value is bool b && b;
        return showAllSongs ? "All Songs" : "Folders";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}