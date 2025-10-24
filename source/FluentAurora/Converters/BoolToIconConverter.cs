using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentIcons.Common;

namespace FluentAurora.Converters;

public class BoolToIconConverter : IValueConverter
{
    public static readonly BoolToIconConverter Instance = new BoolToIconConverter();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool showAllSongs = value is bool b && b;
        return showAllSongs ? Symbol.MusicNote2 : Symbol.Folder;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}