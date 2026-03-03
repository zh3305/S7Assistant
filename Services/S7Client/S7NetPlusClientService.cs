using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using S7.Net;
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
/// S7.Net 客户端服务实现
/// </summary>
public sealed class S7NetPlusClientService : IS7ClientService, IDisposable
{
    private Plc? _client;
    private readonly Stopwatch _stopwatch;
    private S7ConnectionConfig? _currentConfig;

    public S7ProviderType ProviderType => S7ProviderType.S7NetPlus;
    public bool IsConnected => _client?.IsConnected ?? false;
    public PlcStatus PlcStatus { get; private set; } = PlcStatus.Unknown;
    public int LastCommunicationTime { get; private set; }

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<PlcStatusChangedEventArgs>? PlcStatusChanged;

    public S7NetPlusClientService()
    {
        _stopwatch = new Stopwatch();
        _client = null;
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

    private static CpuType ParseCpuType(string? cpuType)
    {
        return cpuType?.ToUpper() switch
        {
            "S7200" => CpuType.S7200,
            "S7200SMART" => CpuType.S7200Smart,
            "S7300" => CpuType.S7300,
            "S7400" => CpuType.S7400,
            "S71200" => CpuType.S71200,
            "S71500" => CpuType.S71500,
            _ => CpuType.S7200Smart
        };
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
            try
            {
                var cpuType = ParseCpuType(config.CpuType);
                _client = new Plc(cpuType, config.Ip, (short)config.Rack, (short)config.Slot);

                _client.Open();

                if (!_client.IsConnected)
                {
                    _client = null;
                    throw new ConnectionException(
                        $"连接失败 (IP: {config.Ip}, Rack: {config.Rack}, Slot: {config.Slot})");
                }

                _currentConfig = config;
                OnConnectionStateChanged(true, "连接成功");
            }
            catch (Exception ex) when (ex is not ConnectionException)
            {
                _client = null;
                throw new ConnectionException($"连接失败: {ex.Message} (IP: {config.Ip})", ex);
            }
        }, cancellationToken);
    }

    public Task DisconnectAsync()
    {
        return Task.Run(() =>
        {
            if (!IsConnected)
                throw new InvalidOperationException("未连接到PLC");

            _client?.Close();
            _client = null;
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
        if (!IsConnected || _client == null)
            throw new InvalidOperationException("未连接到PLC，无法读取数据");

        _stopwatch.Restart();

        try
        {
            int byteLength = dataType.GetByteLength();
            byte[] buffer = address.Area switch
            {
                S7AreaType.DB when address.DBNumber.HasValue =>
                    _client.ReadBytes(DataType.DataBlock, address.DBNumber.Value, address.Offset, byteLength),
                S7AreaType.I =>
                    _client.ReadBytes(DataType.Input, 0, address.Offset, byteLength),
                S7AreaType.Q =>
                    _client.ReadBytes(DataType.Output, 0, address.Offset, byteLength),
                S7AreaType.M =>
                    _client.ReadBytes(DataType.Memory, 0, address.Offset, byteLength),
                S7AreaType.T =>
                    _client.ReadBytes(DataType.Timer, 0, address.Offset, byteLength),
                S7AreaType.C =>
                    _client.ReadBytes(DataType.Counter, 0, address.Offset, byteLength),
                _ => throw new NotSupportedException($"不支持的内存区域: {address.Area}")
            };

            var value = ConvertBytesToValue(buffer, dataType, address.Bit);

            _stopwatch.Stop();
            LastCommunicationTime = (int)_stopwatch.ElapsedMilliseconds;

            return (value, buffer);
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            throw new IOException($"读取数据失败: {ex.Message}", ex);
        }
    }

    public async Task<Dictionary<S7Address, (object Value, byte[] RawValue)>> ReadMultipleAsync(
        IEnumerable<(S7Address address, S7DataType dataType)> items,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _client == null)
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
        if (!IsConnected || _client == null)
            throw new InvalidOperationException("未连接到PLC，无法写入数据");

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _stopwatch.Restart();

        try
        {
            byte[] buffer;

            // 位类型需要先读取整个字节，修改特定位后再写回
            if (dataType == S7DataType.Bit && address.Bit.HasValue)
            {
                buffer = await ReadByteForBitWriteAsync(address);
                ModifyBitInBuffer(buffer, address.Bit.Value, (bool)value);
            }
            else
            {
                buffer = ConvertValueToBytes(value, dataType);
            }

            switch (address.Area)
            {
                case S7AreaType.DB when address.DBNumber.HasValue:
                    _client.WriteBytes(DataType.DataBlock, address.DBNumber.Value, address.Offset, buffer);
                    break;
                case S7AreaType.I:
                    _client.WriteBytes(DataType.Input, 0, address.Offset, buffer);
                    break;
                case S7AreaType.Q:
                    _client.WriteBytes(DataType.Output, 0, address.Offset, buffer);
                    break;
                case S7AreaType.M:
                    _client.WriteBytes(DataType.Memory, 0, address.Offset, buffer);
                    break;
                case S7AreaType.T:
                    _client.WriteBytes(DataType.Timer, 0, address.Offset, buffer);
                    break;
                case S7AreaType.C:
                    _client.WriteBytes(DataType.Counter, 0, address.Offset, buffer);
                    break;
                default:
                    throw new NotSupportedException($"不支持的内存区域: {address.Area}");
            }

            _stopwatch.Stop();
            LastCommunicationTime = (int)_stopwatch.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            throw new IOException($"写入数据失败: {ex.Message}", ex);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 读取用于位写入的字节
    /// </summary>
    private async Task<byte[]> ReadByteForBitWriteAsync(S7Address address)
    {
        if (!IsConnected || _client == null)
            throw new InvalidOperationException("未连接到PLC");

        return await Task.Run(() =>
        {
            byte[] buffer = address.Area switch
            {
                S7AreaType.DB when address.DBNumber.HasValue =>
                    _client.ReadBytes(DataType.DataBlock, address.DBNumber.Value, address.Offset, 1),
                S7AreaType.I =>
                    _client.ReadBytes(DataType.Input, 0, address.Offset, 1),
                S7AreaType.Q =>
                    _client.ReadBytes(DataType.Output, 0, address.Offset, 1),
                S7AreaType.M =>
                    _client.ReadBytes(DataType.Memory, 0, address.Offset, 1),
                S7AreaType.T =>
                    _client.ReadBytes(DataType.Timer, 0, address.Offset, 1),
                S7AreaType.C =>
                    _client.ReadBytes(DataType.Counter, 0, address.Offset, 1),
                _ => throw new NotSupportedException($"不支持的内存区域: {address.Area}")
            };
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
        if (!IsConnected || _client == null)
            throw new InvalidOperationException("未连接到PLC");

        return await Task.Run(() =>
        {
            try
            {
                // S7.Net 没有直接获取状态的方法，需要通过读取特定区域来判断
                // 尝试读取一个已知地址来判断连接状态
                var testValue = _client.Read("DB1.DBX0.0");
                if (testValue != null)
                {
                    PlcStatus = PlcStatus.Run;
                    OnPlcStatusChanged(PlcStatus.Run);
                }
                else
                {
                    PlcStatus = PlcStatus.Unknown;
                    OnPlcStatusChanged(PlcStatus.Unknown);
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
            _client?.Close();
        }
        _client = null;
        _currentConfig = null;
        GC.SuppressFinalize(this);
    }
}
