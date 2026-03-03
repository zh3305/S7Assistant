using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace S7Assistant.Converters;

/// <summary>
/// 布尔值转颜色转换器
/// true=绿色, false=灰色
/// </summary>
public sealed class BoolToColorConverter : IValueConverter
{
    /// <summary>
    /// 静态实例，用于XAML引用
    /// </summary>
    public static BoolToColorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // 返回 SolidColorBrush 以便直接用于 Foreground
            return boolValue
                ? new SolidColorBrush(Color.Parse("#22c55e"))  // 绿色
                : new SolidColorBrush(Color.Parse("#6b7280")); // 灰色
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 静态转换器实例集合（用于XAML引用）
/// </summary>
public static class BoolConverters
{
    public static BoolToColorConverter BoolToColor { get; } = new();
}
