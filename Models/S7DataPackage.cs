using CommunityToolkit.Mvvm.ComponentModel;
using S7Assistant.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace S7Assistant.Models;

/// <summary>
/// S7 数据包类 - 用于批量读取的配置单元
/// </summary>
public sealed partial class S7DataPackage : ObservableObject
{
    /// <summary>
    /// 包名称（用于分组显示）
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// DB块编号（用于DB类型地址）
    /// </summary>
    [ObservableProperty]
    private int _dbNumber = 1;

    /// <summary>
    /// 内存区域类型
    /// </summary>
    [ObservableProperty]
    private S7AreaType _area;

    /// <summary>
    /// 起始地址（字节偏移）
    /// </summary>
    [ObservableProperty]
    private int _startAddress;

    /// <summary>
    /// 读取长度（字节数）
    /// </summary>
    [ObservableProperty]
    private int _length;

    /// <summary>
    /// 数据项列表
    /// </summary>
    public ObservableCollection<S7DataItem> Items { get; set; } = new();

    /// <summary>
    /// 获取用于批量读取的地址对象
    /// </summary>
    [JsonIgnore]
    public S7Address ReadAddress => Area switch
    {
        S7AreaType.DB => S7Address.CreateDB(DbNumber, StartAddress),
        S7AreaType.I => S7Address.CreateInput(StartAddress),
        S7AreaType.Q => S7Address.CreateOutput(StartAddress),
        S7AreaType.M => S7Address.CreateMemory(StartAddress),
        _ => throw new NotSupportedException($"不支持的区域类型: {Area}")
    };

    /// <summary>
    /// 更新所有数据项的完整地址
    /// </summary>
    public void UpdateFullAddresses()
    {
        foreach (var item in Items)
        {
            var absoluteAddress = StartAddress + item.Offset;
            var prefix = Area.GetPrefix();

            if (Area == S7AreaType.DB)
            {
                item.FullAddress = item.BitOffset.HasValue
                    ? $"DB{DbNumber}.DBX{absoluteAddress}.{item.BitOffset}"
                    : $"DB{DbNumber}.DBB{absoluteAddress}";
            }
            else
            {
                item.FullAddress = item.BitOffset.HasValue
                    ? $"{prefix}{absoluteAddress}.{item.BitOffset}"
                    : $"{prefix}{absoluteAddress}";
            }
        }
    }

    /// <summary>
    /// 从读取的字节缓冲区中更新所有数据项的值
    /// </summary>
    /// <param name="buffer">读取到的字节数组</param>
    /// <exception cref="InvalidOperationException">当数据项配置无效时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当偏移量超出缓冲区范围时抛出</exception>
    public void UpdateItemsFromBuffer(byte[] buffer)
    {
        foreach (var item in Items)
        {
            var offset = item.Offset;
            var bit = item.BitOffset;

            // 检查偏移量是否在有效范围内
            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"数据项 '{item.Name}' 的偏移量 {offset} 超出缓冲区范围 [0, {buffer.Length - 1}]");
            }

            // 根据数据类型从缓冲区解析值
            var value = ParseValueFromBuffer(buffer, offset, bit, item.Type);
            item.CurrentValue = value;
            item.Value = FormatValue(value, item.Type, item.TypeValues);
            item.RawValue = GetRawBytes(buffer, offset, item.Type);
        }
    }

    /// <summary>
    /// 从缓冲区解析值
    /// </summary>
    private static object? ParseValueFromBuffer(byte[] buffer, int offset, int? bitOffset, S7DataType type)
    {
        return type switch
        {
            S7DataType.Bit when bitOffset.HasValue => (buffer[offset] & (1 << bitOffset.Value)) != 0,
            S7DataType.Bit => buffer[offset] != 0,
            S7DataType.Byte => buffer[offset],
            S7DataType.Word when offset + 1 < buffer.Length =>
                BitConverter.ToUInt16(new[] { buffer[offset + 1], buffer[offset] }, 0),
            S7DataType.Int when offset + 1 < buffer.Length =>
                BitConverter.ToInt16(new[] { buffer[offset + 1], buffer[offset] }, 0),
            S7DataType.DWord when offset + 3 < buffer.Length =>
                BitConverter.ToUInt32(new[] { buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset] }, 0),
            S7DataType.DInt when offset + 3 < buffer.Length =>
                BitConverter.ToInt32(new[] { buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset] }, 0),
            S7DataType.Real when offset + 3 < buffer.Length =>
                BitConverter.ToSingle(new[] { buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset] }, 0),
            S7DataType.LWord when offset + 7 < buffer.Length =>
                BitConverter.ToUInt64(new[] { buffer[offset + 7], buffer[offset + 6], buffer[offset + 5], buffer[offset + 4], buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset] }, 0),
            S7DataType.LInt when offset + 7 < buffer.Length =>
                BitConverter.ToInt64(new[] { buffer[offset + 7], buffer[offset + 6], buffer[offset + 5], buffer[offset + 4], buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset] }, 0),
            S7DataType.LReal when offset + 7 < buffer.Length =>
                BitConverter.ToDouble(new[] { buffer[offset + 7], buffer[offset + 6], buffer[offset + 5], buffer[offset + 4], buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset] }, 0),
            _ => null
        };
    }

    /// <summary>
    /// 获取原始字节数组
    /// </summary>
    private static byte[]? GetRawBytes(byte[] buffer, int offset, S7DataType type)
    {
        int length = type.GetByteLength();
        if (offset + length > buffer.Length)
            return null;

        var result = new byte[length];
        Array.Copy(buffer, offset, result, 0, length);
        return result;
    }

    /// <summary>
    /// 格式化值显示
    /// </summary>
    private static string FormatValue(object? value, S7DataType type, Dictionary<string, string>? typeValues)
    {
        if (value == null)
            return "--";

        var stringValue = value.ToString() ?? "";

        // 如果有值映射，尝试获取描述
        if (typeValues != null && typeValues.Count > 0)
        {
            if (typeValues.TryGetValue(stringValue, out var desc))
                return desc;
        }

        return stringValue;
    }
}
