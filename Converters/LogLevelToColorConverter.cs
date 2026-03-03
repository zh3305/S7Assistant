using Avalonia.Data.Converters;
using Avalonia.Media;
using S7Assistant.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace S7Assistant.Converters;

/// <summary>
/// 日志级别转颜色转换器
/// </summary>
public sealed class LogLevelToColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count == 0)
            return Brushes.Gray;

        if (values[0] is LogEntry entry)
        {
            return entry.Level switch
            {
                LogLevel.Debug => Brushes.Gray,
                LogLevel.Info => Brushes.Black,
                LogLevel.Warning => Brushes.Orange,
                LogLevel.Error => Brushes.Red,
                _ => Brushes.Black
            };
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
