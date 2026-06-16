using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace V3dfy.App.Converters;

public sealed class ViewportMaxSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double viewportSize ||
            double.IsNaN(viewportSize) ||
            double.IsInfinity(viewportSize) ||
            viewportSize <= 0)
        {
            return DependencyProperty.UnsetValue;
        }

        var reservedSize = ParseReservedSize(parameter, culture);
        return Math.Max(0d, viewportSize - reservedSize);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;

    private static double ParseReservedSize(object parameter, CultureInfo culture)
    {
        return parameter switch
        {
            double doubleValue => doubleValue,
            string text when double.TryParse(text, NumberStyles.Float, culture, out var parsed) => parsed,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0d
        };
    }
}
