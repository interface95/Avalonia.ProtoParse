using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Protobuf.Decode.Parser;

namespace Avalonia.ProtoParse.Desktop.Converters;

public class WireTypeToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProtoWireType wireType)
        {
            return wireType switch
            {
                ProtoWireType.Varint => Brushes.Chocolate,
                ProtoWireType.LengthDelimited => Brushes.Black,
                ProtoWireType.Fixed32 => Brushes.Teal,
                ProtoWireType.Fixed64 => Brushes.Teal,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
