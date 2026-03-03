using System;
using S7Assistant.Models;

namespace S7Assistant.Core.Interfaces;

/// <summary>
/// PLC状态变化事件参数
/// </summary>
public sealed class PlcStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// PLC状态
    /// </summary>
    public required PlcStatus Status { get; init; }
}
