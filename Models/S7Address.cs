using System;

namespace S7Assistant.Models;

/// <summary>
/// S7 地址信息
/// </summary>
public sealed class S7Address
{
    /// <summary>
    /// 内存区域
    /// </summary>
    public S7AreaType Area { get; private set; }

    /// <summary>
    /// 数据块编号（仅DB区域有效）
    /// </summary>
    public int? DBNumber { get; private set; }

    /// <summary>
    /// 字节偏移
    /// </summary>
    public int Offset { get; private set; }

    /// <summary>
    /// 位偏移（仅Bit类型有效）
    /// </summary>
    public int? Bit { get; private set; }

    /// <summary>
    /// 原始地址字符串
    /// </summary>
    public string OriginalAddress { get; private set; } = string.Empty;

    /// <summary>
    /// 从字符串解析地址
    /// </summary>
    /// <exception cref="ArgumentException">地址格式无效时抛出</exception>
    /// <exception cref="NotSupportedException">不支持的区域时抛出</exception>
    public static S7Address Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("地址不能为空", nameof(address));

        address = address.Trim().ToUpper();

        var result = new S7Address { OriginalAddress = address };

        // 解析 DBn.DBXx.b 或 DBn.DBDx 格式
        if (address.StartsWith("DB"))
        {
            result.Area = S7AreaType.DB;

            // 提取 DB 号: DB1.DBD0 -> 1
            var afterDB = address[2..];
            var dotIndex = afterDB.IndexOf('.');
            if (dotIndex <= 0)
                throw new ArgumentException($"无效的DB地址格式: {address}，应为 DBn.DBXx.b 或 DBn.DBDx");

            if (!int.TryParse(afterDB[..dotIndex], out int dbNum))
                throw new ArgumentException($"无效的DB编号: {address}");

            if (dbNum < 0 || dbNum > 65535)
                throw new ArgumentOutOfRangeException(nameof(address), $"DB编号超出范围 (0-65535): {dbNum}");

            result.DBNumber = dbNum;

            // 提取偏移量和位号: DBD0 或 DBX0.0
            var afterDot = afterDB[(dotIndex + 1)..];
            result.ParseOffsetAndBit(afterDot);
        }
        // 解析 I/Q/M/T/C 区域: I0.0, M10.0
        else
        {
            result.Area = address[0] switch
            {
                'I' => S7AreaType.I,
                'Q' => S7AreaType.Q,
                'M' => S7AreaType.M,
                'T' => S7AreaType.T,
                'C' => S7AreaType.C,
                _ => throw new NotSupportedException($"不支持的内存区域: {address[0]}")
            };

            var afterArea = address[1..];
            if (string.IsNullOrEmpty(afterArea))
            {
                // 单个字母如 "M" 是无效地址
                throw new ArgumentException($"无效的地址格式: {address}");
            }

            result.ParseOffsetAndBit(afterArea);
        }

        return result;
    }

    /// <summary>
    /// 解析偏移量和位号
    /// 支持 formats: DBB0, DBW2, DBD4, DBX9.0, 0, 0.0
    /// </summary>
    private void ParseOffsetAndBit(string valuePart)
    {
        // 处理可能的前导点: .0.0 -> 0.0
        if (valuePart.StartsWith("."))
            valuePart = "0" + valuePart;

        // 去除常见的 S7 地址类型前缀
        // DBX -> 位, DBB -> 字节, DBW -> 字, DBD -> 双字
        if (valuePart.StartsWith("DBX", StringComparison.OrdinalIgnoreCase))
        {
            valuePart = valuePart[3..]; // 去掉 DBX
        }
        else if (valuePart.StartsWith("DBB", StringComparison.OrdinalIgnoreCase))
        {
            valuePart = valuePart[3..]; // 去掉 DBB
        }
        else if (valuePart.StartsWith("DBW", StringComparison.OrdinalIgnoreCase))
        {
            valuePart = valuePart[3..]; // 去掉 DBW
        }
        else if (valuePart.StartsWith("DBD", StringComparison.OrdinalIgnoreCase))
        {
            valuePart = valuePart[3..]; // 去掉 DBD
        }

        var parts = valuePart.Split('.');
        if (parts.Length == 0 || parts.Length > 2)
            throw new ArgumentException($"无效的偏移量格式: {valuePart}");

        // 解析偏移量
        if (!int.TryParse(parts[0], out int offset))
            throw new ArgumentException($"无效的偏移量: {parts[0]}");

        if (offset < 0 || offset > 65535)
            throw new ArgumentOutOfRangeException(nameof(valuePart), $"偏移量超出范围 (0-65535): {offset}");

        Offset = offset;

        // 解析位号（如果有）
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out int bit))
                throw new ArgumentException($"无效的位号: {parts[1]}");

            if (bit < 0 || bit > 7)
                throw new ArgumentOutOfRangeException(nameof(valuePart), $"位号超出范围 (0-7): {bit}");

            Bit = bit;
        }
    }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    public override string ToString()
    {
        var area = Area.GetPrefix();
        var db = DBNumber.HasValue ? $"{DBNumber.Value}" : "";
        var bit = Bit.HasValue ? $".{Bit.Value}" : "";

        // 根据区域类型决定格式
        if (Area == S7AreaType.DB)
        {
            return Bit.HasValue
                ? $"DB{db}.DBX{Offset}.{Bit}"
                : $"DB{db}.DBD{Offset}";
        }
        else
        {
            return Bit.HasValue
                ? $"{area}{Offset}.{Bit}"
                : $"{area}{Offset}";
        }
    }

    /// <summary>
    /// 创建DB块地址
    /// </summary>
    public static S7Address CreateDB(int dbNumber, int offset, int? bit = null)
    {
        if (dbNumber < 0 || dbNumber > 65535)
            throw new ArgumentOutOfRangeException(nameof(dbNumber), $"DB编号超出范围 (0-65535): {dbNumber}");

        if (offset < 0 || offset > 65535)
            throw new ArgumentOutOfRangeException(nameof(offset), $"偏移量超出范围 (0-65535): {offset}");

        if (bit.HasValue && (bit < 0 || bit > 7))
            throw new ArgumentOutOfRangeException(nameof(bit), $"位号超出范围 (0-7): {bit}");

        return new S7Address
        {
            Area = S7AreaType.DB,
            DBNumber = dbNumber,
            Offset = offset,
            Bit = bit,
            OriginalAddress = "" // 会在ToString中正确显示
        };
    }

    /// <summary>
    /// 创建位存储器地址
    /// </summary>
    public static S7Address CreateMemory(int offset, int? bit = null)
    {
        return CreateAddress(S7AreaType.M, offset, bit);
    }

    /// <summary>
    /// 创建输入地址
    /// </summary>
    public static S7Address CreateInput(int offset, int? bit = null)
    {
        return CreateAddress(S7AreaType.I, offset, bit);
    }

    /// <summary>
    /// 创建输出地址
    /// </summary>
    public static S7Address CreateOutput(int offset, int? bit = null)
    {
        return CreateAddress(S7AreaType.Q, offset, bit);
    }

    /// <summary>
    /// 创建定时器地址
    /// </summary>
    public static S7Address CreateTimer(int offset, int? bit = null)
    {
        return CreateAddress(S7AreaType.T, offset, bit);
    }

    /// <summary>
    /// 创建计数器地址
    /// </summary>
    public static S7Address CreateCounter(int offset, int? bit = null)
    {
        return CreateAddress(S7AreaType.C, offset, bit);
    }

    /// <summary>
    /// 创建地址的通用方法
    /// </summary>
    private static S7Address CreateAddress(S7AreaType area, int offset, int? bit = null)
    {
        if (offset < 0 || offset > 65535)
            throw new ArgumentOutOfRangeException(nameof(offset), $"偏移量超出范围 (0-65535): {offset}");

        if (bit.HasValue && (bit < 0 || bit > 7))
            throw new ArgumentOutOfRangeException(nameof(bit), $"位号超出范围 (0-7): {bit}");

        return new S7Address
        {
            Area = area,
            Offset = offset,
            Bit = bit,
            OriginalAddress = ""
        };
    }
}
