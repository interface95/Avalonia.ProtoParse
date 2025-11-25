using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.ProtoParse.Desktop.Core;

namespace Avalonia.ProtoParse.Desktop.Converters;

public class WireTypeToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProtoWireType wireType || Application.Current == null)
            return Brushes.Gray;
        
        var resourceKey = wireType switch
        {
            ProtoWireType.Varint => "WireTypeVarintBrush",
            ProtoWireType.LengthDelimited => "WireTypeLengthDelimitedBrush",
            ProtoWireType.Fixed32 => "WireTypeFixedBrush",
            ProtoWireType.Fixed64 => "WireTypeFixedBrush",
            _ => "WireTypeDefaultBrush"
        };

        if (Application.Current.TryFindResource(resourceKey, out var resource) && resource is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
