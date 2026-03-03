using System;

namespace S7Assistant.Core.Interfaces;

/// <summary>
/// 连接状态变化事件参数
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 是否已连接
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// 状态消息
    /// </summary>
    public required string Message { get; init; }
}
