using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FluentAurora.Converters;

public class TupleConverter : IMultiValueConverter
{
    public static readonly TupleConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values?.Count == 2)
        {
            return (values[0], values[1]);
        }
        return null;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}