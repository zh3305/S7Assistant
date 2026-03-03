namespace S7Assistant.Models;

/// <summary>
/// PLC运行状态
/// </summary>
public enum PlcStatus
{
    /// <summary>
    /// 未知状态
    /// </summary>
    Unknown,

    /// <summary>
    /// 运行中
    /// </summary>
    Run,

    /// <summary>
    /// 停止
    /// </summary>
    Stop,

    /// <summary>
    /// 离线
    /// </summary>
    Offline
}
