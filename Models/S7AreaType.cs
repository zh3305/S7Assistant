using System;
using System.ComponentModel;

namespace S7Assistant.Models;

/// <summary>
/// S7 内存区域类型
/// </summary>
public enum S7AreaType
{
    [Description("输入")]
    I = 0x81,

    [Description("输出")]
    Q = 0x82,

    [Description("位存储器")]
    M = 0x83,

    [Description("数据块")]
    DB = 0x84,

    [Description("定时器")]
    T = 0x85,

    [Description("计数器")]
    C = 0x86
}

/// <summary>
/// S7 内存区域扩展
/// </summary>
public static class S7AreaTypeExtensions
{
    /// <summary>
    /// 获取区域前缀
    /// </summary>
    public static string GetPrefix(this S7AreaType area)
    {
        return area switch
        {
            S7AreaType.I => "I",
            S7AreaType.Q => "Q",
            S7AreaType.M => "M",
            S7AreaType.DB => "DB",
            S7AreaType.T => "T",
            S7AreaType.C => "C",
            _ => throw new NotSupportedException($"不支持的内存区域: {area}")
        };
    }
}
