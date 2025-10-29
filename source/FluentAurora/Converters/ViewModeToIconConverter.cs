using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentIcons.Common;
using FluentAurora.ViewModels;

namespace FluentAurora.Converters;

public class ViewModeToIconConverter : IValueConverter
{
    public static readonly ViewModeToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ViewMode viewMode)
        {
            return viewMode switch
            {
                ViewMode.Folders => Symbol.Folder,
                ViewMode.AllSongs => Symbol.MusicNote2,
                ViewMode.Playlists => Symbol.List,
                _ => Symbol.Folder
            };
        }
        return Symbol.Folder;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}