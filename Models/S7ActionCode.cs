using System;
using System.ComponentModel;

namespace S7Assistant.Models;

/// <summary>
/// S7 操作代码枚举
/// </summary>
public enum S7ActionCode
{
    [Description("读取")]
    Read,

    [Description("写入")]
    Write
}

/// <summary>
/// S7 操作代码扩展
/// </summary>
public static class S7ActionCodeExtensions
{
    /// <summary>
    /// 尝试解析字符串为操作代码
    /// </summary>
    public static bool TryParse(string codeString, out S7ActionCode code)
    {
        code = S7ActionCode.Read;
        if (string.IsNullOrEmpty(codeString))
            return false;
        return Enum.TryParse<S7ActionCode>(codeString, true, out code);
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    public static string ToCodeString(this S7ActionCode code)
    {
        return code.ToString();
    }
}
