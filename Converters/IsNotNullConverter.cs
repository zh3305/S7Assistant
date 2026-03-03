using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace S7Assistant.Converters;

/// <summary>
/// 检查值是否不为null的转换器
/// </summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 静态转换器实例集合
/// </summary>
public static class ObjectConverters
{
    public static IsNotNullConverter IsNotNull { get; } = new();
}
