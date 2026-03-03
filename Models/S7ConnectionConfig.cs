using System;
using CommunityToolkit.Mvvm.ComponentModel;
using S7Assistant.Models;
using System.ComponentModel;
using System.Net;

namespace S7Assistant.Core.Interfaces;

/// <summary>
/// S7 PLC 连接配置
/// </summary>
public sealed partial class S7ConnectionConfig : ObservableObject
{
    /// <summary>
    /// 使用的提供商类型
    /// </summary>
    [ObservableProperty]
    private S7ProviderType _providerType = S7ProviderType.Sharp7;

    /// <summary>
    /// PLC IP 地址
    /// </summary>
    [ObservableProperty]
    private string _ip = "192.168.1.75";

    /// <summary>
    /// CPU类型（S7200Smart, S7300, S7400, S71200, S71500）
    /// </summary>
    [ObservableProperty]
    private string _cpuType = "S7200Smart";

    /// <summary>
    /// 机架号
    /// </summary>
    [ObservableProperty]
    private int _rack = 0;

    /// <summary>
    /// 槽号
    /// </summary>
    [ObservableProperty]
    private int _slot = 1;

    /// <summary>
    /// 请求的 PDU 大小
    /// </summary>
    [ObservableProperty]
    private int _pduSize = 960;

    /// <summary>
    /// 连接类型（1=PG, 2=OP, 3=Basic）
    /// </summary>
    [ObservableProperty]
    private int _connectionType = 2;

    /// <summary>
    /// 连接超时（毫秒）
    /// </summary>
    [ObservableProperty]
    private int _connectionTimeout = 5000;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Ip))
            throw new InvalidOperationException("IP地址不能为空");

        if (!IPAddress.TryParse(Ip, out _))
            throw new InvalidOperationException($"无效的IP地址: {Ip}");

        if (Rack < 0 || Rack > 7)
            throw new ArgumentOutOfRangeException(nameof(Rack), $"机架号必须在 0-7 之间，当前值: {Rack}");

        if (Slot < 0 || Slot > 31)
            throw new ArgumentOutOfRangeException(nameof(Slot), $"槽号必须在 0-31 之间，当前值: {Slot}");

        return true;
    }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    public override string ToString()
    {
        return $"{Ip} (CPU: {CpuType}, Rack: {Rack}, Slot: {Slot}, Provider: {ProviderType})";
    }
}
