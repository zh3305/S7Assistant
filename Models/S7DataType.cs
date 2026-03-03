using System;
using System.ComponentModel;

namespace S7Assistant.Models;

/// <summary>
/// S7 数据类型枚举
/// </summary>
public enum S7DataType
{
    [Description("位")]
    Bit = 0,

    [Description("字节")]
    Byte = 1,

    [Description("字")]
    Word = 2,

    [Description("整数")]
    Int = 3,

    [Description("双字")]
    DWord = 4,

    [Description("双整数")]
    DInt = 6,

    [Description("实数")]
    Real = 7,

    [Description("长实数")]
    LReal = 8,

    [Description("长字")]
    LWord = 9,

    [Description("长整数")]
    LInt = 10,

    [Description("日期时间")]
    DateTime = 11,

    [Description("字符串")]
    String = 12
}

/// <summary>
/// S7 数据类型扩展
/// </summary>
public static class S7DataTypeExtensions
{
    /// <summary>
    /// 获取数据类型的字节长度
    /// </summary>
    public static int GetByteLength(this S7DataType type)
    {
        return type switch
        {
            S7DataType.Bit => 1,
            S7DataType.Byte => 1,
            S7DataType.Word => 2,
            S7DataType.Int => 2,
            S7DataType.DWord => 4,
            S7DataType.DInt => 4,
            S7DataType.Real => 4,
            S7DataType.LReal => 8,
            S7DataType.LWord => 8,
            S7DataType.LInt => 8,
            S7DataType.DateTime => 8,
            S7DataType.String => 254,
            _ => throw new NotSupportedException($"不支持的数据类型: {type}")
        };
    }

    /// <summary>
    /// 获取数据类型的默认显示格式
    /// </summary>
    public static string GetFormat(this S7DataType type)
    {
        return type switch
        {
            S7DataType.Real or S7DataType.LReal => "F2",
            S7DataType.DateTime => "yyyy-MM-dd HH:mm:ss",
            _ => ""
        };
    }
}
