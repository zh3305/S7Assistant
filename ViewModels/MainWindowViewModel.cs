using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using S7Assistant.Services;
using S7Assistant.Services.S7Client;
using S7Assistant.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace S7Assistant.ViewModels;

/// <summary>
/// 主窗口视图模型
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly LogService _logService;
    private readonly ConfigService _configService;
    private readonly ExcelService _excelService;
    private readonly Func<S7ProviderType, IS7ClientService> _clientFactory;

    private IS7ClientService? _currentClient;
    private CancellationTokenSource? _monitoringCts;
    private readonly object _lockObject = new();
    private IDialogService? _dialogService;
    private Window? _mainWindow;

    #region 属性

    /// <summary>
    /// 当前使用的S7提供程序类型
    /// </summary>
    [ObservableProperty]
    private S7ProviderType _providerType = S7ProviderType.Sharp7;

    /// <summary>
    /// 是否已连接
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartMonitoring))]
    [NotifyPropertyChangedFor(nameof(CanStopMonitoring))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteValueCommand))]
    private bool _isConnected;

    /// <summary>
    /// 是否正在监视
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartMonitoring))]
    [NotifyPropertyChangedFor(nameof(CanStopMonitoring))]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMonitoringCommand))]
    private bool _isMonitoring;

    /// <summary>
    /// 是否可以开始监视
    /// </summary>
    public bool CanStartMonitoring => IsConnected && !IsMonitoring;

    /// <summary>
    /// 是否可以停止监视
    /// </summary>
    public bool CanStopMonitoring => IsMonitoring;

    /// <summary>
    /// PLC状态
    /// </summary>
    [ObservableProperty]
    private PlcStatus _plcStatus = PlcStatus.Unknown;

    /// <summary>
    /// 最后一次通信时间(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _lastCommunicationTime;

    /// <summary>
    /// 连接配置
    /// </summary>
    [ObservableProperty]
    private S7ConnectionConfig _connectionConfig = new();

    /// <summary>
    /// 配置文件列表
    /// </summary>
    public ObservableCollection<string> ConfigFiles { get; } = new();

    /// <summary>
    /// 当前选中的配置文件
    /// </summary>
    [ObservableProperty]
    private string _selectedConfigFile = "";

    /// <summary>
    /// 数据包集合（用于Package格式的配置)
    /// </summary>
    public ObservableCollection<S7DataPackage> DataPackages { get; } = new();

    private DataGridCollectionView? _allDataItemsView;

    /// <summary>
    /// 所有数据项（带分组的集合视图，用于DataGrid显示）
    /// </summary>
    public DataGridCollectionView AllDataItems
    {
        get
        {
            var items = new List<S7DataItem>();
            foreach (var package in DataPackages)
            {
                foreach (var item in package.Items)
                {
                    item.GroupName = package.Name;
                    items.Add(item);
                }
            }

            _allDataItemsView = new DataGridCollectionView(items);
            _allDataItemsView.GroupDescriptions.Add(new DataGridPathGroupDescription("GroupName"));
            return _allDataItemsView;
        }
    }

    /// <summary>
    /// 日志集合
    /// </summary>
    public ObservableCollection<LogEntry> Logs => _logService.Logs;

    /// <summary>
    /// 选中的数据项
    /// </summary>
    [ObservableProperty]
    private S7DataItem? _selectedItem;

    /// <summary>
    /// 监视间隔索引 (0=50ms, 1=100ms, 2=200ms, 3=500ms, 4=1000ms)
    /// </summary>
    [ObservableProperty]
    private int _monitorIntervalIndex = 3; // 默认 500ms（与旧系统一致）

    /// <summary>
    /// 监视间隔(毫秒)
    /// </summary>
    public int MonitorInterval => MonitorIntervalIndex switch
    {
        0 => 50,
        1 => 100,
        2 => 200,
        3 => 500,
        4 => 1000,
        _ => 200
    };

    /// <summary>
    /// 错误计数
    /// </summary>
    [ObservableProperty]
    private int _errorCount;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "就绪";

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public MainWindowViewModel(
        LogService logService,
        ConfigService configService,
        ExcelService excelService,
        Func<S7ProviderType, IS7ClientService> clientFactory,
        IDialogService? dialogService = null)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _dialogService = dialogService;

        // 订阅日志变化以更新错误计数
        _logService.Logs.CollectionChanged += (s, e) =>
        {
            ErrorCount = _logService.Logs.Count(l => l.Level == LogLevel.Error);
        };

        // 加载保存的连接配置
        _ = LoadConnectionConfigAsync();

        // 加载配置文件列表
        _ = LoadConfigFilesListAsync();

        _logService.Log("应用程序已启动", LogLevel.Info);
    }

    /// <summary>
    /// 配置文件选择改变时自动加载
    /// </summary>
    partial void OnSelectedConfigFileChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadSelectedConfigAsync(value);
        }
    }

    /// <summary>
    /// 加载连接配置
    /// </summary>
    private async Task LoadConnectionConfigAsync()
    {
        try
        {
            var savedConfig = await _configService.LoadConnectionConfigAsync();
            if (savedConfig != null)
            {
                ConnectionConfig = savedConfig;
                _logService.Log($"已加载连接配置: {savedConfig.Ip} (CPU: {savedConfig.CpuType})", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"加载连接配置失败: {ex.Message}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// 加载配置文件列表
    /// </summary>
    private async Task LoadConfigFilesListAsync()
    {
        try
        {
            var files = await _configService.GetConfigFilesAsync();
            ConfigFiles.Clear();
            foreach (var file in files)
            {
                // 移除.json扩展名
                var name = Path.GetFileNameWithoutExtension(file);
                ConfigFiles.Add(name);
            }

            if (ConfigFiles.Count > 0)
            {
                // 默认选择第一个配置
                SelectedConfigFile = ConfigFiles[0];
                _logService.Log($"已加载配置文件列表: {ConfigFiles.Count} 个配置", LogLevel.Info);
            }
            else
            {
                _logService.Log("配置目录为空，请创建配置文件", LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"加载配置文件列表失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 加载选中的配置文件（支持Package格式和    /// </summary>
    private async Task LoadSelectedConfigAsync(string configName)
    {
        try
        {
            if (string.IsNullOrEmpty(configName))
                return;

            var fileName = configName.EndsWith(".json") ? configName : $"{configName}.json";
            var packages = await _configService.LoadPackagesAsync(fileName);

            DataPackages.Clear();
            foreach (var package in packages)
            {
                package.UpdateFullAddresses();
                DataPackages.Add(package);
            }

            // 通知 AllDataItems 属性已变更
            OnPropertyChanged(nameof(AllDataItems));

            // 统计数据项数量
            var totalItems = packages.Sum(p => p.Items.Count);
            _logService.Log($"已加载配置: {configName}, 共 {packages.Count} 个数据包, {totalItems} 个数据项", LogLevel.Info);
            StatusMessage = $"已加载 {totalItems} 个数据项";
        }
        catch (Exception ex)
        {
            _logService.Log($"加载配置失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 从数据包中获取所有数据项的扁平化列表
    /// </summary>
    private List<S7DataItem> GetAllDataItemsFromPackages()
    {
        var items = new List<S7DataItem>();
        foreach (var package in DataPackages)
        {
            foreach (var item in package.Items)
            {
                // 设置分组名称用于分组显示
                item.GroupName = package.Name;
                items.Add(item);
            }
        }
        return items;
    }

/// <summary>
/// 设置对话框服务（由窗口初始化后调用）
/// </summary>
    internal void SetDialogService(IDialogService dialogService, Window mainWindow)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }

    #region 连接命令

    /// <summary>
    /// 是否可以连接
    /// </summary>
    public bool CanConnect => !IsConnected;

    /// <summary>
    /// 连接命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        try
        {
            // 验证配置
            ConnectionConfig.IsValid();

            StatusMessage = "正在连接...";
            _logService.Log($"开始连接到PLC: {ConnectionConfig.Ip} (Rack: {ConnectionConfig.Rack}, Slot: {ConnectionConfig.Slot})", LogLevel.Info);

            // 创建客户端
            lock (_lockObject)
            {
                _currentClient?.Dispose();
                _currentClient = _clientFactory(ProviderType);
                ProviderType = _currentClient.ProviderType;
                _logService.Log($"使用通信库: {ProviderType}", LogLevel.Info);

                // 订阅连接状态变化
                _currentClient.ConnectionStateChanged += OnConnectionStateChanged;
                _currentClient.PlcStatusChanged += OnPlcStatusChanged;
            }

            // 连接
            await _currentClient.ConnectAsync(ConnectionConfig);
            _logService.Log("PLC连接建立成功", LogLevel.Info);

            // 获取PLC状态
            try
            {
                PlcStatus = await _currentClient.GetPlcStatusAsync();
                _logService.Log($"PLC状态: {PlcStatus}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _logService.Log($"获取PLC状态失败: {ex.Message}", LogLevel.Warning);
            }

            IsConnected = true;
            IsMonitoring = false;
            StatusMessage = $"已连接到 {ConnectionConfig.Ip}";
            _logService.Log($"连接成功: {ConnectionConfig.Ip}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            IsMonitoring = false;
            StatusMessage = "连接失败";
            _logService.Log($"连接失败: {ex.Message}", LogLevel.Error, ex.StackTrace);
        }
    }

    /// <summary>
    /// 断开连接命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task DisconnectAsync()
    {
        try
        {
            _logService.Log("开始断开连接...", LogLevel.Info);

            if (_currentClient == null)
            {
                _logService.Log("当前没有活动连接", LogLevel.Warning);
                IsConnected = false;
                return;
            }

            // 停止监视
            if (IsMonitoring)
            {
                _logService.Log("正在停止监视...", LogLevel.Info);
                StopMonitoring();
            }

            await _currentClient.DisconnectAsync();
            _logService.Log("PLC连接已断开", LogLevel.Info);

            lock (_lockObject)
            {
                if (_currentClient != null)
                {
                    _currentClient.ConnectionStateChanged -= OnConnectionStateChanged;
                    _currentClient.PlcStatusChanged -= OnPlcStatusChanged;
                    _currentClient.Dispose();
                    _currentClient = null;
                }
            }

            IsConnected = false;
            IsMonitoring = false;
            PlcStatus = PlcStatus.Unknown;
            StatusMessage = "已断开连接";
            _logService.Log("断开连接完成", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _logService.Log($"断开连接失败: {ex.Message}", LogLevel.Error, ex.StackTrace);
            IsConnected = false;
            IsMonitoring = false;
            throw;
        }
    }

    /// <summary>
    /// 打开连接设置命令
    /// </summary>
    [RelayCommand]
    private async Task OpenConnectionSettingsAsync()
    {
        try
        {
            if (_mainWindow == null)
            {
                _logService.Log("主窗口未初始化，无法打开对话框", LogLevel.Error);
                return;
            }

            var dialog = new Views.ConnectionSettingsDialog(ConnectionConfig, async config =>
            {
                ConnectionConfig = config;
                _logService.Log($"连接配置已更新: {config.Ip} (CPU: {config.CpuType}, 类型: {config.ConnectionType})", LogLevel.Info);

                // 保存配置到文件
                try
                {
                    await _configService.SaveConnectionConfigAsync(config);
                    _logService.Log("连接配置已保存", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    _logService.Log($"保存连接配置失败: {ex.Message}", LogLevel.Warning);
                }
            });

            await dialog.ShowDialog(_mainWindow);
        }
        catch (Exception ex)
        {
            _logService.Log($"打开连接设置失败: {ex.Message}", LogLevel.Error);
        }
    }

    #endregion

    #region 数据操作命令

    /// <summary>
    /// 新增数据项命令
    /// </summary>
    [RelayCommand]
    private void AddNewItem()
    {
        var newItem = new S7DataItem
        {
            Name = $"新数据项{GetAllDataItemsFromPackages().Count + 1}",
            Type = S7DataType.Bit,
            Address = "M0.0",
            Length = 1
        };

        // 添加到第一个Package或创建新的Package
        if (DataPackages.Count == 0)
        {
            var newPackage = new S7DataPackage { Name = "默认包" };
            newPackage.Items.Add(newItem);
            DataPackages.Add(newPackage);
        }
        else
        {
            DataPackages[0].Items.Add(newItem);
        }

        SelectedItem = newItem;
        _logService.Log($"添加数据项: {newItem.Name}", LogLevel.Info);
    }

    /// <summary>
    /// 编辑选中项命令
    /// </summary>
    [RelayCommand]
    private void EditSelectedItem()
    {
        if (SelectedItem == null)
            throw new InvalidOperationException("没有选中的数据项");

        _logService.Log($"编辑数据项: {SelectedItem.Name}", LogLevel.Info);
    }

    /// <summary>
    /// 删除选中项命令
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedItem()
    {
        if (SelectedItem == null)
            throw new InvalidOperationException("没有选中的数据项");

        var name = SelectedItem.Name;

        // 从所有Package中查找并删除
        foreach (var package in DataPackages)
        {
            if (package.Items.Remove(SelectedItem))
            {
                break;
            }
        }

        _logService.Log($"删除数据项: {name}", LogLevel.Info);
    }

    /// <summary>
    /// 写入值命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task WriteValueAsync(S7DataItem? item)
    {
        if (item == null)
        {
            _logService.Log("写入失败: 未选择数据项", LogLevel.Warning);
            return;
        }

        if (!IsConnected || _currentClient == null)
        {
            _logService.Log("写入失败: 未连接到PLC", LogLevel.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.WriteValue))
        {
            _logService.Log("写入失败: 写入值不能为空", LogLevel.Warning);
            return;
        }

        try
        {
            _logService.Log($"开始写入: {item.Name} ({item.Address}) = {item.WriteValue}", LogLevel.Info);

            var address = S7Address.Parse(item.Address);
            var value = ParseWriteValue(item.WriteValue, item.Type);

            await _currentClient.WriteAsync(address, item.Type, value);

            item.CurrentValue = value;
            item.WriteValue = null;

            _logService.Log($"写入成功: {item.Name} = {value}, 耗时 {_currentClient.LastCommunicationTime}ms", LogLevel.Info);
            StatusMessage = $"写入成功: {item.Name}";
        }
        catch (Exception ex)
        {
            _logService.Log($"写入失败: {item.Name} - {ex.Message}", LogLevel.Error);
            StatusMessage = $"写入失败: {item.Name}";
        }
    }

    /// <summary>
    /// 全部读取命令（按Package批量读取）
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task ReadAllAsync()
    {
        if (!IsConnected || _currentClient == null)
        {
            _logService.Log("未连接到PLC，无法读取数据", LogLevel.Warning);
            return;
        }

        StatusMessage = "正在读取全部数据...";
        var totalItems = DataPackages.Sum(p => p.Items.Count);
        _logService.Log($"开始读取全部数据，共 {DataPackages.Count} 个数据包, {totalItems} 个数据项", LogLevel.Info);

        var errors = 0;
        var success = 0;

        // 按Package批量读取
        foreach (var package in DataPackages)
        {
            try
            {
                // 批量读取整个Package的数据
                var buffer = await _currentClient.ReadBytesAsync(
                    package.ReadAddress,
                    package.Length);

                // 从缓冲区解析所有数据项的值
                package.UpdateItemsFromBuffer(buffer);
                success += package.Items.Count;
            }
            catch (Exception ex)
            {
                errors += package.Items.Count;
                _logService.Log($"Package读取失败: {package.Name} - {ex.Message}", LogLevel.Error);
            }
        }

        StatusMessage = errors > 0
            ? $"读取完成，成功 {success}，失败 {errors}"
            : $"读取完成，共 {success} 项";
        _logService.Log($"全部读取完成: 成功 {success}, 失败 {errors}, 耗时 {_currentClient.LastCommunicationTime}ms", LogLevel.Info);
    }

    /// <summary>
    /// 刷新PLC状态命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task RefreshStatusAsync()
    {
        if (!IsConnected || _currentClient == null)
        {
            _logService.Log("未连接到PLC，无法刷新状态", LogLevel.Warning);
            return;
        }

        try
        {
            PlcStatus = await _currentClient.GetPlcStatusAsync();
            _logService.Log($"PLC状态刷新: {PlcStatus}", LogLevel.Info);
            StatusMessage = $"PLC状态: {PlcStatus}";
        }
        catch (Exception ex)
        {
            _logService.Log($"获取PLC状态失败: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    #endregion

    #region 监视命令

    /// <summary>
    /// 开始监视命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private void StartMonitoring()
    {
        if (_monitoringCts != null)
        {
            _logService.Log("监视已在运行中", LogLevel.Warning);
            return;
        }

        _monitoringCts = new CancellationTokenSource();
        IsMonitoring = true;
        _ = Task.Run(() => MonitoringLoop(_monitoringCts.Token));

        StatusMessage = "监视已启动";
        var totalItems = DataPackages.Sum(p => p.Items.Count);
        _logService.Log($"启动监视，间隔: {MonitorInterval}ms，共 {DataPackages.Count} 个数据包, {totalItems} 个数据项", LogLevel.Info);
    }

    /// <summary>
    /// 停止监视命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopMonitoring))]
    private void StopMonitoring()
    {
        if (_monitoringCts == null)
        {
            _logService.Log("监视未在运行", LogLevel.Warning);
            return;
        }

        _logService.Log("正在停止监视...", LogLevel.Info);
        _monitoringCts.Cancel();
        _monitoringCts.Dispose();
        _monitoringCts = null;
        IsMonitoring = false;

        StatusMessage = "监视已停止";
        _logService.Log("监视已停止", LogLevel.Info);
    }

    /// <summary>
    /// 监视循环（按Package批量读取）
    /// </summary>
    private async Task MonitoringLoop(CancellationToken cancellationToken)
    {
        _logService.Log("监视循环已启动", LogLevel.Info);
        int loopCount = 0;
        int errorCount = 0;

        while (!cancellationToken.IsCancellationRequested && IsConnected && _currentClient != null)
        {
            loopCount++;
            try
            {
                int successCount = 0;
                int failCount = 0;

                // 按Package批量读取
                foreach (var package in DataPackages)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // 批量读取整个Package的数据
                        var buffer = await _currentClient.ReadBytesAsync(
                            package.ReadAddress,
                            package.Length,
                            cancellationToken);

                        // 从缓冲区解析所有数据项的值
                        package.UpdateItemsFromBuffer(buffer);
                        successCount += package.Items.Count;
                    }
                    catch (Exception ex)
                    {
                        failCount += package.Items.Count;
                        // Package读取失败，每10次循环记录一次
                        if (loopCount % 10 == 0)
                        {
                            _logService.Log($"Package读取失败: {package.Name} - {ex.Message}", LogLevel.Warning);
                        }
                    }
                }

                LastCommunicationTime = _currentClient.LastCommunicationTime;

                // 每10次循环记录一次状态
                if (loopCount % 10 == 0)
                {
                    var totalItems = DataPackages.Sum(p => p.Items.Count);
                    _logService.Log($"监视循环 #{loopCount}: 成功 {successCount}, 失败 {failCount}, 耗时 {LastCommunicationTime}ms (按{DataPackages.Count}个Package读取)", LogLevel.Info);
                }

                if (failCount > 0)
                {
                    errorCount += failCount;
                }
            }
            catch (Exception ex)
            {
                _logService.Log($"监视循环错误: {ex.Message}", LogLevel.Error);
            }

            try
            {
                await Task.Delay(MonitorInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logService.Log("监视循环被取消", LogLevel.Info);
                break;
            }
        }

        // 确保退出时更新状态
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (IsMonitoring)
            {
                IsMonitoring = false;
                StatusMessage = "监视已停止";
            }
        });
        _logService.Log($"监视循环结束，共执行 {loopCount} 次循环，累计错误 {errorCount} 次", LogLevel.Info);
    }

    #endregion

    #region 配置管理命令

    /// <summary>
    /// 打开配置管理器命令
    /// </summary>
    [RelayCommand]
    private async Task OpenConfigManagerAsync()
    {
        try
        {
            if (_mainWindow == null)
            {
                _logService.Log("主窗口未初始化，无法打开对话框", LogLevel.Error);
                return;
            }

            var window = new Views.ConfigurationManagerWindow(packages =>
            {
                DataPackages.Clear();
                foreach (var package in packages)
                {
                    DataPackages.Add(package);
                }
                OnPropertyChanged(nameof(AllDataItems));
                _logService.Log($"已加载配置: {packages.Count} 个数据包", LogLevel.Info);
            });

            await window.ShowDialog(_mainWindow);
            _logService.Log("配置管理器已关闭", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _logService.Log($"打开配置管理器失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 从Excel导入命令
    /// </summary>
    [RelayCommand]
    private async Task ImportFromExcelAsync()
    {
        try
        {
            if (_dialogService == null)
            {
                _logService.Log("对话框服务未初始化", LogLevel.Warning);
                return;
            }

            var filePath = await _dialogService.ShowOpenFileDialogAsync(
                "导入Excel文件",
                ("Excel文件", new[] { "xlsx", "xls" })
            );

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log("未选择文件", LogLevel.Info);
                return;
            }

            if (!File.Exists(filePath))
            {
                _logService.Log("文件不存在", LogLevel.Warning);
                return;
            }

            var items = await _excelService.ImportFromExcelAsync(filePath);
            DataPackages.Clear();

            // 将所有items放入一个Package
            var package = new S7DataPackage { Name = "导入的数据" };
            foreach (var item in items)
            {
                package.Items.Add(item);
            }
            DataPackages.Add(package);

            _logService.Log($"从Excel导入: {items.Count} 个数据项", LogLevel.Info);
            StatusMessage = $"已导入 {items.Count} 个数据项";
        }
        catch (Exception ex)
        {
            _logService.Log($"导入Excel失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 导出到Excel命令
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        try
        {
            if (_dialogService == null)
            {
                _logService.Log("对话框服务未初始化", LogLevel.Warning);
                return;
            }

            var defaultFileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "导出Excel文件",
                defaultFileName,
                ("Excel文件", new[] { "xlsx" })
            );

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log("未选择保存位置", LogLevel.Info);
                return;
            }

            var items = GetAllDataItemsFromPackages();
            await _excelService.ExportToExcelAsync(filePath, items);

            _logService.Log($"已导出到Excel: {items.Count} 个数据项", LogLevel.Info);
            StatusMessage = $"已导出 {items.Count} 个数据项";
        }
        catch (Exception ex)
        {
            _logService.Log($"导出Excel失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 加载配置命令
    /// </summary>
    [RelayCommand]
    private async Task LoadConfigAsync()
    {
        try
        {
            if (_dialogService == null)
            {
                _logService.Log("对话框服务未初始化", LogLevel.Warning);
                return;
            }

            var filePath = await _dialogService.ShowOpenFileDialogAsync(
                "加载配置文件",
                ("JSON配置文件", new[] { "json" })
            );

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log("未选择配置文件", LogLevel.Info);
                return;
            }

            var fileName = Path.GetFileName(filePath);
            var packages = await _configService.LoadPackagesAsync(fileName);
            DataPackages.Clear();
            foreach (var package in packages)
            {
                DataPackages.Add(package);
            }

            var totalItems = packages.Sum(p => p.Items.Count);
            _logService.Log($"加载配置: {fileName}, {totalItems} 个数据项", LogLevel.Info);
            StatusMessage = $"已加载 {totalItems} 个数据项";
        }
        catch (Exception ex)
        {
            _logService.Log($"加载配置失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 保存配置命令
    /// </summary>
    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
            if (_dialogService == null)
            {
                _logService.Log("对话框服务未初始化", LogLevel.Warning);
                return;
            }

            var defaultFileName = $"config_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "保存配置文件",
                defaultFileName,
                ("JSON配置文件", new[] { "json" })
            );

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log("未选择保存位置", LogLevel.Info);
                return;
            }

            var fileName = Path.GetFileName(filePath);
            await _configService.SavePackagesAsync(fileName, DataPackages.ToList());

            var totalItems = DataPackages.Sum(p => p.Items.Count);
            _logService.Log($"保存配置: {fileName}, {totalItems} 个数据项", LogLevel.Info);
            StatusMessage = $"已保存 {totalItems} 个数据项";
        }
        catch (Exception ex)
        {
            _logService.Log($"保存配置失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 清空日志命令
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        _logService.Clear();
        ErrorCount = 0;
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 连接状态变化事件处理
    /// </summary>
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsConnected = e.IsConnected;
            StatusMessage = e.Message;
            _logService.Log(e.Message, e.IsConnected ? LogLevel.Info : LogLevel.Warning);
        });
    }

    /// <summary>
    /// PLC状态变化事件处理
    /// </summary>
    private void OnPlcStatusChanged(object? sender, PlcStatusChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            PlcStatus = e.Status;
            _logService.Log($"PLC状态变化: {e.Status}", LogLevel.Info);
        });
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 解析写入值
    /// </summary>
    private static object ParseWriteValue(string value, S7DataType type)
    {
        return type switch
        {
            S7DataType.Bit => bool.Parse(value),
            S7DataType.Byte => byte.Parse(value),
            S7DataType.Word => ushort.Parse(value),
            S7DataType.Int => short.Parse(value),
            S7DataType.DWord => uint.Parse(value),
            S7DataType.DInt => int.Parse(value),
            S7DataType.Real => float.Parse(value),
            S7DataType.LReal => double.Parse(value),
            S7DataType.LWord => ulong.Parse(value),
            S7DataType.LInt => long.Parse(value),
            _ => throw new NotSupportedException($"不支持的数据类型: {type}")
        };
    }

    #endregion

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Dispose()
    {
        StopMonitoring();
        lock (_lockObject)
        {
            _currentClient?.Dispose();
            _currentClient = null;
        }
    }
}
