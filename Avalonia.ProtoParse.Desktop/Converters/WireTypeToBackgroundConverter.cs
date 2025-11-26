using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.ProtoParse.Desktop.Core;

namespace Avalonia.ProtoParse.Desktop.Converters;

public class WireTypeToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProtoWireType wireType || Application.Current == null)
            return Brushes.Transparent;
        
        var resourceKey = wireType switch
        {
            ProtoWireType.Varint => "WireTypeVarintBackground",
            ProtoWireType.LengthDelimited => "WireTypeLengthDelimitedBackground",
            ProtoWireType.Fixed32 => "WireTypeFixedBackground",
            ProtoWireType.Fixed64 => "WireTypeFixedBackground",
            _ => "WireTypeDefaultBackground"
        };

        if (Application.Current.TryFindResource(resourceKey, out var resource) && resource is IBrush brush)
        {
            return brush;
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
