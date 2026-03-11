using S7Assistant.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace S7Assistant.Core.Interfaces;

/// <summary>
/// S7 客户端服务接口
/// </summary>
public interface IS7ClientService : IDisposable
{
    /// <summary>
    /// 使用的提供商类型
    /// </summary>
    S7ProviderType ProviderType { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// PLC运行状态
    /// </summary>
    PlcStatus PlcStatus { get; }

    /// <summary>
    /// 最后一次通信耗时（毫秒）
    /// </summary>
    int LastCommunicationTime { get; }

    // ==================== 事件 ====================

    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// PLC状态变化事件
    /// </summary>
    event EventHandler<PlcStatusChangedEventArgs>? PlcStatusChanged;

    // ==================== 连接管理 ====================

    /// <summary>
    /// 连接到PLC
    /// </summary>
    /// <exception cref="ArgumentNullException">配置为null时抛出</exception>
    /// <exception cref="InvalidOperationException">已连接时抛出</exception>
    /// <exception cref="ConnectionException">连接失败时抛出</exception>
    Task ConnectAsync(S7ConnectionConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开连接
    /// </summary>
    /// <exception cref="InvalidOperationException">未连接时抛出</exception>
    Task DisconnectAsync();

    // ==================== 数据读取 ====================

    /// <summary>
    /// 读取单个地址的数据
    /// </summary>
    /// <exception cref="InvalidOperationException">未连接时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">地址超出范围时抛出</exception>
    /// <exception cref="IOException">读取失败时抛出</exception>
    Task<(object Value, byte[] RawValue)> ReadAsync(
        S7Address address,
        S7DataType dataType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量读取多个地址的数据
    /// </summary>
    /// <exception cref="InvalidOperationException">未连接时抛出</exception>
    /// <exception cref="IOException">任意一个地址读取失败时抛出</exception>
    Task<Dictionary<S7Address, (object Value, byte[] RawValue)>> ReadMultipleAsync(
        IEnumerable<(S7Address address, S7DataType dataType)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量读取指定长度的字节数组
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">读取长度（字节数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>读取到的字节数组</returns>
    /// <exception cref="InvalidOperationException">未连接时抛出</exception>
    /// <exception cref="IOException">读取失败时抛出</exception>
    Task<byte[]> ReadBytesAsync(
        S7Address address,
        int length,
        CancellationToken cancellationToken = default);

    // ==================== 数据写入 ====================

    /// <summary>
    /// 向指定地址写入数据
    /// </summary>
    /// <exception cref="InvalidOperationException">未连接时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">地址超出范围时抛出</exception>
    /// <exception cref="IOException">写入失败时抛出</exception>
    Task WriteAsync(
        S7Address address,
        S7DataType dataType,
        object value,
        CancellationToken cancellationToken = default);

    // ==================== 状态查询 ====================

    /// <summary>
    /// 获取PLC运行状态
    /// </summary>
    /// <exception cref="InvalidOperationException">未连接时抛出</exception>
    Task<PlcStatus> GetPlcStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 连接异常
/// </summary>
public class ConnectionException : Exception
{
    public ConnectionException(string message) : base(message) { }
    public ConnectionException(string message, Exception innerException) : base(message, innerException) { }
}
