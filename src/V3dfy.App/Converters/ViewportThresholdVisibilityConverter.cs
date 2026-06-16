using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace V3dfy.App.Converters;

public sealed class ViewportThresholdVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double viewportSize ||
            double.IsNaN(viewportSize) ||
            double.IsInfinity(viewportSize) ||
            viewportSize <= 0)
        {
            return Visibility.Collapsed;
        }

        var rule = ParseRule(parameter, culture);
        var isVisible = rule.Operator switch
        {
            ThresholdOperator.LessThan => viewportSize < rule.Threshold,
            ThresholdOperator.GreaterOrEqual => viewportSize >= rule.Threshold,
            _ => false,
        };

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;

    private static ThresholdRule ParseRule(object parameter, CultureInfo culture)
    {
        var text = parameter?.ToString() ?? string.Empty;
        var separatorIndex = text.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == text.Length - 1)
        {
            return new(ThresholdOperator.GreaterOrEqual, 0d);
        }

        var operatorText = text[..separatorIndex];
        var thresholdText = text[(separatorIndex + 1)..];
        if (!double.TryParse(thresholdText, NumberStyles.Float, culture, out var threshold) &&
            !double.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold))
        {
            threshold = 0d;
        }

        var thresholdOperator = operatorText.Equals("LessThan", StringComparison.OrdinalIgnoreCase)
            ? ThresholdOperator.LessThan
            : ThresholdOperator.GreaterOrEqual;
        return new(thresholdOperator, threshold);
    }

    private readonly record struct ThresholdRule(
        ThresholdOperator Operator,
        double Threshold);

    private enum ThresholdOperator
    {
        LessThan,
        GreaterOrEqual,
    }
}
