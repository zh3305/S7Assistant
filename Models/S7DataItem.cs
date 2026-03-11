using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace S7Assistant.Models;

/// <summary>
/// S7 数据项类
/// </summary>
public sealed partial class S7DataItem : ObservableObject
{
    // ==================== 基本属性 ====================

    /// <summary>
    /// 数据项名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 当前值（带格式的显示值）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private string _value = string.Empty;

    /// <summary>
    /// 当前值（用于数据显示）
    /// </summary>
    [ObservableProperty]
    private object? _currentValue;

    /// <summary>
    /// 写入值（用于数据写入）
    /// </summary>
    [ObservableProperty]
    private string? _writeValue;

    /// <summary>
    /// 原始字节数组
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HexValue))]
    private byte[]? _rawValue;

    /// <summary>
    /// 十六进制值显示
    /// </summary>
    [JsonIgnore]
    public string HexValue => RawValue != null
        ? BitConverter.ToString(RawValue).Replace("-", "")
        : "--";

    // ==================== 类型与地址 ====================

    /// <summary>
    /// 数据类型
    /// </summary>
    [ObservableProperty]
    private S7DataType _type;

    /// <summary>
    /// PLC地址（兼容旧配置格式）
    /// </summary>
    [ObservableProperty]
    private string _address = string.Empty;

    /// <summary>
    /// 字节偏移量（相对于Package的起始地址）
    /// </summary>
    [ObservableProperty]
    private int _offset;

    /// <summary>
    /// 位偏移（仅Bit类型有效，0-7）
    /// </summary>
    [ObservableProperty]
    private int? _bitOffset;

    /// <summary>
    /// 所属分组名称
    /// </summary>
    [ObservableProperty]
    private string _groupName = string.Empty;

    /// <summary>
    /// 完整地址（Package起始地址 + 偏移量）
    /// </summary>
    [ObservableProperty]
    private string _fullAddress = string.Empty;

    /// <summary>
    /// 解析后的地址对象（惰性求值）
    /// </summary>
    private S7Address? _parsedAddress;

    /// <summary>
    /// 解析后的地址对象
    /// </summary>
    [JsonIgnore]
    public S7Address ParsedAddress => _parsedAddress ??= S7Address.Parse(Address);

    /// <summary>
    /// 数据长度
    /// </summary>
    [ObservableProperty]
    private int _length;

    /// <summary>
    /// DB块编号（用于DB类型地址）
    /// </summary>
    [ObservableProperty]
    private int _dBNumber;

    // ==================== 扩展属性 ====================

    /// <summary>
    /// 值映射字典（用于枚举值的文字描述）
    /// </summary>
    public Dictionary<string, string> TypeValues { get; set; } = new();

    /// <summary>
    /// 备注说明
    /// </summary>
    [ObservableProperty]
    private string _remark = string.Empty;

    /// <summary>
    /// 是否打开提示气泡
    /// </summary>
    [ObservableProperty]
    private bool _isOpenTip;

    /// <summary>
    /// 获取值的文字描述
    /// </summary>
    [JsonIgnore]
    public string Description
    {
        get
        {
            if (TypeValues is { Count: > 0 })
                TypeValues.TryGetValue(Value, out string? desc);
            return "";
        }
    }

    /// <summary>
    /// 是否有描述
    /// </summary>
    [JsonIgnore]
    public bool HasDescription => !string.IsNullOrEmpty(Description);

    // ==================== 命令 ====================

    /// <summary>
    /// 打开提示命令
    /// </summary>
    [RelayCommand]
    public async Task OpenTipAsync()
    {
        IsOpenTip = true;
        await Task.Delay(5000);
        IsOpenTip = false;
    }
}
