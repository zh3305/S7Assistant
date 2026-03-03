using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace S7Assistant.Converters;

/// <summary>
/// 布尔值转连接状态转换器
/// </summary>
public sealed class BoolToConnectionStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected ? "已连接" : "未连接";
        }
        return "未知";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
