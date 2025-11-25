using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Protobuf.Decode.Parser;

namespace Avalonia.ProtoParse.Desktop.Converters;

public class ParentNodeBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProtoDisplayNode node || node.Children.Count <= 0) 
            return Brushes.Transparent;
        if (Application.Current != null && 
            Application.Current.TryFindResource("ParentNodeBackground", out var res) && 
            res is IBrush brush)
        {
            return brush;
        }
        // Fallback for design time or missing resource
        return SolidColorBrush.Parse("#F8FAFF");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ParentNodeBorderBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProtoDisplayNode node || node.Children.Count <= 0) return Brushes.Transparent;
        if (Application.Current != null && 
            Application.Current.TryFindResource("ParentNodeBorder", out var res) && 
            res is IBrush brush)
        {
            return brush;
        }
        // Fallback
        return SolidColorBrush.Parse("#DCE0FE");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ParentNodeBorderThicknessConverter : IValueConverter
{
    private static readonly Thickness ThicknessOne = new(1);
    private static readonly Thickness ThicknessZero = new(0);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProtoDisplayNode node && node.Children.Count > 0)
        {
            return ThicknessOne;
        }
        return ThicknessZero;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class HasChildrenConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
