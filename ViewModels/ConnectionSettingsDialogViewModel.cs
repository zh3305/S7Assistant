using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using S7Assistant.Services;
using S7Assistant.Services.S7Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace S7Assistant.ViewModels;

/// <summary>
/// 连接设置视图模型
/// </summary>
public sealed partial class ConnectionSettingsDialogViewModel : ObservableObject
{
    private readonly Action<S7ConnectionConfig>? _saveCallback;
    private readonly Action? _closeCallback;
    private readonly Func<S7ProviderType, IS7ClientService> _clientFactory;

    /// <summary>
    /// 连接配置
    /// </summary>
    [ObservableProperty]
    private S7ConnectionConfig _config = new();

    /// <summary>
    /// 是否使用Sharp7
    /// </summary>
    [ObservableProperty]
    private bool _useSharp7 = true;

    /// <summary>
    /// 是否正在测试连接
    /// </summary>
    [ObservableProperty]
    private bool _isTesting;

    /// <summary>
    /// 测试连接结果消息
    /// </summary>
    [ObservableProperty]
    private string? _testResultMessage;

    /// <summary>
    /// 测试连接是否成功
    /// </summary>
    [ObservableProperty]
    private bool _testSucceeded;

    /// <summary>
    /// 可用的CPU类型列表
    /// </summary>
    public string[] CpuTypes { get; } = { "S7200Smart", "S7300", "S7400", "S71200", "S71500" };

    /// <summary>
    /// 可用的连接类型列表
    /// </summary>
    public string[] ConnectionTypes { get; } = { "PG", "OP", "Basic" };

    /// <summary>
    /// 构造函数 - 用于新配置
    /// </summary>
    public ConnectionSettingsDialogViewModel(Action closeCallback, Func<S7ProviderType, IS7ClientService> clientFactory)
    {
        _closeCallback = closeCallback ?? throw new ArgumentNullException(nameof(closeCallback));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));

        Config = new S7ConnectionConfig
        {
            Ip = "192.168.1.1",
            Rack = 0,
            Slot = 1,
            CpuType = "S7200Smart",
            ConnectionType = 2,
            PduSize = 480
        };
    }

    /// <summary>
    /// 构造函数 - 用于编辑现有配置
    /// </summary>
    public ConnectionSettingsDialogViewModel(S7ConnectionConfig existingConfig, Action<S7ConnectionConfig>? saveCallback, Action closeCallback, Func<S7ProviderType, IS7ClientService> clientFactory)
    {
        if (existingConfig == null)
            throw new ArgumentNullException(nameof(existingConfig));
        if (closeCallback == null)
            throw new ArgumentNullException(nameof(closeCallback));
        if (clientFactory == null)
            throw new ArgumentNullException(nameof(clientFactory));

        Config = new S7ConnectionConfig
        {
            Ip = existingConfig.Ip,
            Rack = existingConfig.Rack,
            Slot = existingConfig.Slot,
            CpuType = existingConfig.CpuType,
            ConnectionType = existingConfig.ConnectionType,
            PduSize = existingConfig.PduSize
        };

        UseSharp7 = true;
        _saveCallback = saveCallback;
        _closeCallback = closeCallback;
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// 测试连接
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        TestResultMessage = "正在连接...";
        TestSucceeded = false;

        try
        {
            // 验证配置
            Config.IsValid();

            // 创建客户端并测试连接
            var client = _clientFactory(Config.ProviderType);

            await client.ConnectAsync(Config);
            TestSucceeded = true;
            TestResultMessage = $"连接成功! PLC: {Config.Ip}";

            await client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            TestSucceeded = false;
            TestResultMessage = $"连接失败: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 是否可以测试连接
    /// </summary>
    private bool CanTestConnection() => !IsTesting;
}
