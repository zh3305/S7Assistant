using Avalonia.Data.Converters;
using S7Assistant.Models;
using System;
using System.Globalization;

namespace S7Assistant.Converters;

/// <summary>
/// PLC状态转换器
/// </summary>
public sealed class PlcStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PlcStatus status)
        {
            return status switch
            {
                PlcStatus.Run => "运行",
                PlcStatus.Stop => "停止",
                PlcStatus.Unknown => "未知",
                _ => "未知"
            };
        }
        return "未知";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
