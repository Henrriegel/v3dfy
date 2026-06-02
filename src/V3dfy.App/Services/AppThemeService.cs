using System.Windows;
using System.Windows.Media;

namespace V3dfy.App.Services;

public sealed class AppThemeService
{
    public void Apply(string theme)
    {
        var isDark = string.Equals(theme, "Dark", StringComparison.Ordinal);

        SetBrush("WindowBackgroundBrush", isDark ? "#10151C" : "#F3F6FA");
        SetBrush("CardBackgroundBrush", isDark ? "#18212C" : "#FFFFFF");
        SetBrush("CardBorderBrush", isDark ? "#2B3A4B" : "#D5DEE8");
        SetBrush("PrimaryTextBrush", isDark ? "#F4F7FA" : "#17212B");
        SetBrush("SecondaryTextBrush", isDark ? "#C4D1DD" : "#526577");
        SetBrush("AccentBrush", isDark ? "#4FA3FF" : "#0969DA");
        SetBrush("AccentHoverBrush", isDark ? "#3385D6" : "#075CBF");
        SetBrush("AccentPressedBrush", isDark ? "#286BAE" : "#064B9B");
        SetBrush("ButtonForegroundBrush", "#FFFFFF");
        SetBrush("DisabledBackgroundBrush", isDark ? "#344150" : "#B8C5D1");
        SetBrush("DisabledTextBrush", isDark ? "#8794A3" : "#657585");
        SetBrush("InputBackgroundBrush", isDark ? "#111922" : "#FFFFFF");
        SetBrush("LogBackgroundBrush", isDark ? "#0D131A" : "#EEF3F8");
        SetBrush("ComboBoxForegroundBrush", isDark ? "#F4F7FA" : "#17212B");
        SetBrush("ComboBoxBackgroundBrush", isDark ? "#263544" : "#FFFFFF");
        SetBrush("ComboBoxBorderBrush", isDark ? "#60758A" : "#AFC0D0");
        SetBrush("ComboBoxHoverBrush", isDark ? "#34495D" : "#E8F1FB");
    }

    private static void SetBrush(string key, string hexColor) =>
        Application.Current.Resources[key] =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
}
