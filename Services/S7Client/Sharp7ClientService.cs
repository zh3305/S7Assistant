using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace S7Assistant.Services.S7Client;

/// <summary>
/// Sharp7 客户端服务实现
/// </summary>
public sealed class Sharp7ClientService : IS7ClientService, IDisposable
{
    private Sharp7.S7Client? _client;
    private readonly Stopwatch _stopwatch;
    private S7ConnectionConfig? _currentConfig;

    public S7ProviderType ProviderType => S7ProviderType.Sharp7;
    public bool IsConnected => _client?.Connected ?? false;
    public PlcStatus PlcStatus { get; private set; } = PlcStatus.Unknown;
    public int LastCommunicationTime { get; private set; }

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<PlcStatusChangedEventArgs>? PlcStatusChanged;

    public Sharp7ClientService()
    {
        _client = new Sharp7.S7Client();
        _stopwatch = new Stopwatch();
        _currentConfig = null;
        PlcStatus = PlcStatus.Unknown;
        OnConnectionStateChanged(false, "未连接");
        OnPlcStatusChanged(PlcStatus.Unknown);
    }

    private void OnConnectionStateChanged(bool connected, string message)
    {
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs { IsConnected = connected, Message = message });
    }

    private void OnPlcStatusChanged(PlcStatus status)
    {
        if (PlcStatus != status)
        {
            PlcStatus = status;
            PlcStatusChanged?.Invoke(this, new PlcStatusChangedEventArgs { Status = status });
        }
    }

    public async Task ConnectAsync(S7ConnectionConfig config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (IsConnected)
            throw new InvalidOperationException("已经连接到PLC，请先断开");

        config.IsValid();

        await Task.Run(() =>
        {
            _client.SetConnectionType((ushort)config.ConnectionType);

            var result = _client.ConnectTo(config.Ip, (ushort)config.Rack, (ushort)config.Slot);

            if (result != 0)
            {
                throw new ConnectionException(
                    $"连接失败: {_client.ErrorText(result)} (IP: {config.Ip}, Rack: {config.Rack}, Slot: {config.Slot})");
            }

            _currentConfig = config;
            OnConnectionStateChanged(true, "连接成功");
        }, cancellationToken);
    }

    public Task DisconnectAsync()
    {
        return Task.Run(() =>
        {
            if (!IsConnected)
                throw new InvalidOperationException("未连接到PLC");

            _client.Disconnect();
            _currentConfig = null;
            PlcStatus = PlcStatus.Unknown;
            OnConnectionStateChanged(false, "已断开连接");
            OnPlcStatusChanged(PlcStatus.Unknown);
        });
    }

    public async Task<(object Value, byte[] RawValue)> ReadAsync(
        S7Address address,
        S7DataType dataType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("未连接到PLC，无法读取数据");

        _stopwatch.Restart();

        try
        {
            int byteLength = dataType.GetByteLength();
            byte[] buffer = new byte[byteLength];

            int result;
            if (address.Area == S7AreaType.DB && address.DBNumber.HasValue)
            {
                result = _client.DBRead(address.DBNumber.Value, address.Offset, byteLength, buffer);
            }
            else
            {
                int area = address.Area switch
                {
                    S7AreaType.I => 0x81,
                    S7AreaType.Q => 0x82,
                    S7AreaType.M => 0x83,
                    S7AreaType.T => 0x85,
                    S7AreaType.C => 0x86,
                    _ => throw new NotSupportedException($"不支持的内存区域: {address.Area}")
                };
                int wordLen = byteLength / 2;
                if (wordLen < 1) wordLen = 1;
                result = _client.ReadArea(area, 0, address.Offset, wordLen, 0x02, buffer);
            }

            if (result != 0)
            {
                throw new IOException(
                    $"读取失败: {_client.ErrorText(result)} (地址: {address})");
            }

            var value = ConvertBytesToValue(buffer, dataType, address.Bit);

            _stopwatch.Stop();
            LastCommunicationTime = (int)_stopwatch.ElapsedMilliseconds;

            return (value, buffer);
        }
        catch
        {
            _stopwatch.Stop();
            throw;
        }
    }

    /// <summary>
    /// 批量读取指定长度的字节数组
    /// </summary>
    public async Task<byte[]> ReadBytesAsync(
        S7Address address,
        int length,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("未连接到PLC，无法读取数据");

        if (length <= 0)
            throw new ArgumentException("读取长度必须大于0", nameof(length));

        _stopwatch.Restart();

        try
        {
            byte[] buffer = new byte[length];
            int result;

            if (address.Area == S7AreaType.DB && address.DBNumber.HasValue)
            {
                result = await Task.Run(() => _client.DBRead(address.DBNumber.Value, address.Offset, length, buffer), cancellationToken);
            }
            else
            {
                int area = address.Area switch
                {
                    S7AreaType.I => 0x81,
                    S7AreaType.Q => 0x82,
                    S7AreaType.M => 0x83,
                    S7AreaType.T => 0x85,
                    S7AreaType.C => 0x86,
                    _ => throw new NotSupportedException($"不支持的内存区域: {address.Area}")
                };
                int wordLen = length / 2;
                if (wordLen < 1) wordLen = 1;
                result = await Task.Run(() => _client.ReadArea(area, 0, address.Offset, wordLen, 0x02, buffer), cancellationToken);
            }

            if (result != 0)
            {
                throw new IOException(
                    $"批量读取失败: {_client.ErrorText(result)} (地址: {address}, 长度: {length})");
            }

            _stopwatch.Stop();
            LastCommunicationTime = (int)_stopwatch.ElapsedMilliseconds;

            return buffer;
        }
        catch
        {
            _stopwatch.Stop();
            throw;
        }
    }

    public async Task<Dictionary<S7Address, (object Value, byte[] RawValue)>> ReadMultipleAsync(
        IEnumerable<(S7Address address, S7DataType dataType)> items,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("未连接到PLC，无法读取数据");

        if (items == null)
            throw new ArgumentNullException(nameof(items));

        var results = new Dictionary<S7Address, (object Value, byte[] RawValue)>();

        foreach (var (address, dataType) in items)
        {
            var (value, raw) = await ReadAsync(address, dataType, cancellationToken);
            results[address] = (value, raw);
        }

        return results;
    }

    public async Task WriteAsync(
        S7Address address,
        S7DataType dataType,
        object value,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("未连接到PLC，无法写入数据");

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _stopwatch.Restart();

        try
        {
            byte[] buffer;

            // 位类型需要先读取整个字节，修改特定位后再写回，避免覆盖其他位
            if (dataType == S7DataType.Bit && address.Bit.HasValue)
            {
                buffer = await ReadByteForBitWriteAsync(address);
                ModifyBitInBuffer(buffer, address.Bit.Value, (bool)value);
            }
            else
            {
                buffer = ConvertValueToBytes(value, dataType);
            }

            int result;
            if (address.Area == S7AreaType.DB && address.DBNumber.HasValue)
            {
                result = _client.DBWrite(address.DBNumber.Value, address.Offset, buffer.Length, buffer);
            }
            else
            {
                int area = address.Area switch
                {
                    S7AreaType.I => 0x81,
                    S7AreaType.Q => 0x82,
                    S7AreaType.M => 0x83,
                    S7AreaType.T => 0x85,
                    S7AreaType.C => 0x86,
                    _ => throw new NotSupportedException($"不支持的内存区域: {address.Area}")
                };
                int wordLen = buffer.Length / 2;
                if (wordLen < 1) wordLen = 1;
                result = _client.WriteArea(area, 0, address.Offset, wordLen, 0x02, buffer);
            }

            if (result != 0)
            {
                throw new IOException(
                    $"写入失败: {_client.ErrorText(result)} (地址: {address})");
            }

            _stopwatch.Stop();
            LastCommunicationTime = (int)_stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            _stopwatch.Stop();
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 读取用于位写入的字节
    /// </summary>
    private async Task<byte[]> ReadByteForBitWriteAsync(S7Address address)
    {
        return await Task.Run(() =>
        {
            byte[] buffer = new byte[1];
            int result;

            if (address.Area == S7AreaType.DB && address.DBNumber.HasValue)
            {
                result = _client.DBRead(address.DBNumber.Value, address.Offset, 1, buffer);
            }
            else
            {
                int area = address.Area switch
                {
                    S7AreaType.I => 0x81,
                    S7AreaType.Q => 0x82,
                    S7AreaType.M => 0x83,
                    S7AreaType.T => 0x85,
                    S7AreaType.C => 0x86,
                    _ => throw new NotSupportedException($"不支持的内存区域: {address.Area}")
                };
                result = _client.ReadArea(area, 0, address.Offset, 1, 0x02, buffer);
            }

            if (result != 0)
                throw new IOException($"读取失败: {_client.ErrorText(result)}");

            return buffer;
        });
    }

    /// <summary>
    /// 修改缓冲区中的特定位
    /// </summary>
    private static void ModifyBitInBuffer(byte[] buffer, int bitNumber, bool value)
    {
        if (value)
            buffer[0] |= (byte)(1 << bitNumber);  // 置位
        else
            buffer[0] &= (byte)~(1 << bitNumber); // 清位
    }

    /// <summary>
    /// 将值转换为字节数组
    /// </summary>
    private byte[] ConvertValueToBytes(object value, S7DataType dataType)
    {
        return dataType switch
        {
            S7DataType.Bit => new[] { (bool)value ? (byte)1 : (byte)0 },
            S7DataType.Byte => new[] { (byte)value },
            S7DataType.Word => BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)(ushort)value)),
            S7DataType.DWord => BitConverter.GetBytes((uint)IPAddress.HostToNetworkOrder((int)(uint)value)),
            S7DataType.Real => GetBigEndianBytes((float)value),
            S7DataType.Int => BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)(int)value)),
            S7DataType.DInt => BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)value)),
            S7DataType.LInt => BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)value)),
            S7DataType.LReal => GetBigEndianBytes((double)value),
            S7DataType.LWord => BitConverter.GetBytes((ulong)IPAddress.HostToNetworkOrder((long)(ulong)value)),
            _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
        };
    }

    /// <summary>
    /// 将字节数组转换为值
    /// </summary>
    private object ConvertBytesToValue(byte[] buffer, S7DataType dataType, int? bitNumber = null)
    {
        return dataType switch
        {
            S7DataType.Bit => bitNumber.HasValue ? (buffer[0] & (1 << bitNumber.Value)) != 0 : buffer[0] != 0,
            S7DataType.Byte => buffer[0],
            S7DataType.Word => BitConverter.ToUInt16(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(BitConverter.ToInt16(buffer, 0))), 0),
            S7DataType.DWord => BitConverter.ToUInt32(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0))), 0),
            S7DataType.Real => GetFloatFromBigEndian(buffer),
            S7DataType.Int => IPAddress.HostToNetworkOrder(BitConverter.ToInt16(buffer, 0)),
            S7DataType.DInt => IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0)),
            S7DataType.LInt => IPAddress.HostToNetworkOrder(BitConverter.ToInt64(buffer, 0)),
            S7DataType.LReal => GetDoubleFromBigEndian(buffer),
            S7DataType.LWord => BitConverter.ToUInt64(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(BitConverter.ToInt64(buffer, 0))), 0),
            _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
        };
    }

    private static byte[] GetBigEndianBytes(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return bytes;
    }

    private static byte[] GetBigEndianBytes(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return bytes;
    }

    private static float GetFloatFromBigEndian(byte[] buffer)
    {
        var bytes = new byte[4];
        Array.Copy(buffer, bytes, 4);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static double GetDoubleFromBigEndian(byte[] buffer)
    {
        var bytes = new byte[8];
        Array.Copy(buffer, bytes, 8);
        Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// 获取PLC运行状态
    /// </summary>
    public async Task<PlcStatus> GetPlcStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("未连接到PLC");

        return await Task.Run(() =>
        {
            // 使用简单的读取操作来判断 PLC 是否在运行
            try
            {
                // 尝试读取 DB1 的第一个字节来验证 PLC 状态
                byte[] testBuffer = new byte[1];
                int result = _client.DBRead(1, 0, 1, testBuffer);
                if (result == 0)
                {
                    PlcStatus = PlcStatus.Run;
                    OnPlcStatusChanged(PlcStatus.Run);
                }
                else
                {
                    // 如果读取失败，尝试读取 M 区
                    result = _client.MBRead(0, 1, testBuffer);
                    if (result == 0)
                    {
                        PlcStatus = PlcStatus.Run;
                        OnPlcStatusChanged(PlcStatus.Run);
                    }
                    else
                    {
                        PlcStatus = PlcStatus.Stop;
                        OnPlcStatusChanged(PlcStatus.Stop);
                    }
                }
                return PlcStatus;
            }
            catch
            {
                PlcStatus = PlcStatus.Unknown;
                OnPlcStatusChanged(PlcStatus.Unknown);
                return PlcStatus;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (IsConnected)
        {
            _client.Disconnect();
        }
        _client = null;
        _currentConfig = null;
        GC.SuppressFinalize(this);
    }
}
