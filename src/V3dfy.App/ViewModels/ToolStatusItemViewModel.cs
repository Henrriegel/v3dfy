using System.Windows;

namespace V3dfy.App.ViewModels;

public sealed record ToolStatusItemViewModel(
    string Name,
    string StatusText,
    string ReasonText,
    string DetailText,
    string ContextActionText = "",
    string ContextActionToolTip = "",
    Visibility ContextActionVisibility = Visibility.Collapsed);
