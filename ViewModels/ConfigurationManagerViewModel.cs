using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Assistant.Models;
using S7Assistant.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace S7Assistant.ViewModels;

/// <summary>
/// 配置管理器视图模型
/// </summary>
public sealed partial class ConfigurationManagerViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private Action<List<S7DataItem>>? _loadCallback;
    private Action? _closeCallback;

    /// <summary>
    /// 配置文件列表
    /// </summary>
    public ObservableCollection<string> ConfigFiles { get; } = new();

    /// <summary>
    /// 选中的配置文件
    /// </summary>
    [ObservableProperty]
    private string? _selectedConfigFile;

    /// <summary>
    /// 新配置名称
    /// </summary>
    [ObservableProperty]
    private string _newConfigName = "";

    /// <summary>
    /// 当前数据项列表（只读集合，使用 Clear/Add 更新内容以正确触发 UI 绑定）
    /// </summary>
    public ObservableCollection<S7DataItem> CurrentDataItems { get; } = new();

    /// <summary>
    /// 选中的数据项
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsItemSelected))]
    [NotifyPropertyChangedFor(nameof(ShowBitEditor))]
    [NotifyPropertyChangedFor(nameof(AddressPreview))]
    private S7DataItem? _selectedDataItem;

    /// <summary>
    /// 是否有选中的数据项
    /// </summary>
    public bool IsItemSelected => SelectedDataItem != null;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "就绪";

    /// <summary>
    /// S7数据类型列表（枚举值数组）
    /// </summary>
    public S7DataType[] S7DataTypes { get; } = Enum.GetValues(typeof(S7DataType)).Cast<S7DataType>().ToArray();

    #region 地址编辑属性

    /// <summary>
    /// 区域类型列表
    /// </summary>
    public S7AreaType[] AreaTypes { get; } = Enum.GetValues(typeof(S7AreaType)).Cast<S7AreaType>().ToArray();

    /// <summary>
    /// 选中的区域类型
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDbAddress))]
    [NotifyPropertyChangedFor(nameof(AddressPreview))]
    private S7AreaType _selectedAreaType = S7AreaType.DB;

    /// <summary>
    /// DB编号
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressPreview))]
    private int _editDbNumber = 1;

    /// <summary>
    /// 偏移量
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressPreview))]
    private int _editOffset = 0;

    /// <summary>
    /// 位号（仅Bit类型）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressPreview))]
    private int _editBitNumber = 0;

    /// <summary>
    /// 是否为DB类型
    /// </summary>
    public bool IsDbAddress => SelectedAreaType == S7AreaType.DB;

    /// <summary>
    /// 是否显示位号编辑器
    /// </summary>
    public bool ShowBitEditor => SelectedDataItem?.Type == S7DataType.Bit;

    /// <summary>
    /// 地址预览
    /// </summary>
    public string AddressPreview => GenerateAddressPreview();

    /// <summary>
    /// 生成地址预览字符串
    /// </summary>
    private string GenerateAddressPreview()
    {
        if (SelectedDataItem == null)
            return "";

        if (SelectedAreaType == S7AreaType.DB)
        {
            return ShowBitEditor
                ? $"DB{EditDbNumber}.DBX{EditOffset}.{EditBitNumber}"
                : $"DB{EditDbNumber}.DBD{EditOffset}";
        }
        else
        {
            var prefix = SelectedAreaType.GetPrefix();
            return ShowBitEditor
                ? $"{prefix}{EditOffset}.{EditBitNumber}"
                : $"{prefix}{EditOffset}";
        }
    }

    /// <summary>
    /// 应用编辑的地址到选中项
    /// </summary>
    [RelayCommand]
    private void ApplyAddress()
    {
        if (SelectedDataItem == null)
            return;

        SelectedDataItem.Address = AddressPreview;
        SelectedDataItem.DBNumber = IsDbAddress ? EditDbNumber : 0;
        StatusMessage = $"地址已更新: {AddressPreview}";
    }

    #endregion

    /// <summary>
    /// 构造函数（用于 DI）
    /// </summary>
    public ConfigurationManagerViewModel(ConfigService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    /// <summary>
    /// 设置回调函数
    /// </summary>
    public void SetCallbacks(Action<List<S7DataItem>> loadCallback, Action closeCallback)
    {
        _loadCallback = loadCallback ?? throw new ArgumentNullException(nameof(loadCallback));
        _closeCallback = closeCallback ?? throw new ArgumentNullException(nameof(closeCallback));
    }

    /// <summary>
    /// 选中数据项改变时，更新地址编辑器
    /// </summary>
    partial void OnSelectedDataItemChanged(S7DataItem? value)
    {
        if (value != null)
        {
            // 解析现有地址并更新编辑器
            ParseAddressToEditor(value.Address);
        }
    }

    /// <summary>
    /// 解析地址到编辑器字段
    /// </summary>
    private void ParseAddressToEditor(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            SelectedAreaType = S7AreaType.DB;
            EditDbNumber = 1;
            EditOffset = 0;
            EditBitNumber = 0;
            return;
        }

        try
        {
            // 尝试解析 DB 类型地址（如 DB1.DBX9.0 或 DB1.DBD10）
            if (address.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
            {
                SelectedAreaType = S7AreaType.DB;

                var parts = address.Split('.');
                if (parts.Length >= 1)
                {
                    // 解析 DB 号
                    var dbPart = parts[0].Substring(2);
                    if (int.TryParse(dbPart, out var dbNum))
                        EditDbNumber = dbNum;
                }

                if (parts.Length >= 2)
                {
                    // 解析偏移量和位号
                    var addrPart = parts[1];
                    if (addrPart.StartsWith("DBX", StringComparison.OrdinalIgnoreCase))
                    {
                        var offsetPart = addrPart.Substring(3);
                        var bitParts = offsetPart.Split('.');
                        if (bitParts.Length >= 1 && int.TryParse(bitParts[0], out var offset))
                            EditOffset = offset;
                        if (bitParts.Length >= 2 && int.TryParse(bitParts[1], out var bit))
                            EditBitNumber = bit;
                    }
                    else if (addrPart.StartsWith("DBD", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(addrPart.Substring(3), out var offset))
                            EditOffset = offset;
                    }
                    else if (addrPart.StartsWith("DBW", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(addrPart.Substring(3), out var offset))
                            EditOffset = offset;
                    }
                    else if (addrPart.StartsWith("DBB", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(addrPart.Substring(3), out var offset))
                            EditOffset = offset;
                    }
                }
            }
            else
            {
                // 解析非 DB 类型地址（如 M0.0 或 I10）
                var prefix = address[0].ToString().ToUpper();
                SelectedAreaType = prefix switch
                {
                    "I" => S7AreaType.I,
                    "Q" => S7AreaType.Q,
                    "M" => S7AreaType.M,
                    "T" => S7AreaType.T,
                    "C" => S7AreaType.C,
                    _ => S7AreaType.DB
                };

                var rest = address.Substring(1);
                var bitParts = rest.Split('.');
                if (bitParts.Length >= 1 && int.TryParse(bitParts[0], out var offset))
                    EditOffset = offset;
                if (bitParts.Length >= 2 && int.TryParse(bitParts[1], out var bit))
                    EditBitNumber = bit;
            }
        }
        catch
        {
            // 解析失败，使用默认值
        }
    }

    /// <summary>
    /// 配置文件选择改变时自动加载
    /// </summary>
    partial void OnSelectedConfigFileChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadSelectedConfigAsync();
        }
    }

    /// <summary>
    /// 加载配置文件列表
    /// </summary>
    public async Task LoadConfigFilesAsync()
    {
        try
        {
            var files = await _configService.GetConfigFilesAsync();
            ConfigFiles.Clear();
            foreach (var file in files)
            {
                ConfigFiles.Add(file);
            }
            StatusMessage = $"找到 {files.Length} 个配置文件";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载配置列表失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 加载选中的配置
    /// </summary>
    [RelayCommand]
    public async Task LoadSelectedConfigAsync()
    {
        if (string.IsNullOrEmpty(SelectedConfigFile))
            return;

        try
        {
            StatusMessage = $"正在加载: {SelectedConfigFile}";
            var fileName = SelectedConfigFile.EndsWith(".json") ? SelectedConfigFile : $"{SelectedConfigFile}.json";
            var items = await _configService.LoadConfigAsync(fileName);

            // 使用 Clear/Add 方式更新集合，确保 UI 绑定正确响应
            CurrentDataItems.Clear();
            foreach (var item in items)
            {
                CurrentDataItems.Add(item);
            }

            StatusMessage = $"已加载 {items.Count} 个数据项";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 创建新配置
    /// </summary>
    [RelayCommand]
    private async Task CreateNewConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(NewConfigName))
        {
            StatusMessage = "请输入配置名称";
            return;
        }

        try
        {
            var fileName = NewConfigName.EndsWith(".json") ? NewConfigName : $"{NewConfigName}.json";
            await _configService.SaveConfigAsync(fileName, new List<S7DataItem>());
            await LoadConfigFilesAsync();
            SelectedConfigFile = NewConfigName;
            NewConfigName = "";
            StatusMessage = $"已创建配置: {fileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 删除选中配置
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedConfigAsync()
    {
        if (string.IsNullOrEmpty(SelectedConfigFile))
        {
            StatusMessage = "请先选择配置文件";
            return;
        }

        try
        {
            var fileName = SelectedConfigFile.EndsWith(".json") ? SelectedConfigFile : $"{SelectedConfigFile}.json";
            await _configService.DeleteConfigAsync(fileName);
            await LoadConfigFilesAsync();
            StatusMessage = $"已删除配置: {SelectedConfigFile}";
            SelectedConfigFile = ConfigFiles.Count > 0 ? ConfigFiles[0] : null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        if (string.IsNullOrEmpty(SelectedConfigFile))
        {
            StatusMessage = "请先选择配置文件";
            return;
        }

        try
        {
            var fileName = SelectedConfigFile.EndsWith(".json") ? SelectedConfigFile : $"{SelectedConfigFile}.json";
            var items = CurrentDataItems.ToList();
            await _configService.SaveConfigAsync(fileName, items);
            StatusMessage = $"已保存配置: {SelectedConfigFile}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 从Excel导入
    /// </summary>
    [RelayCommand]
    private async Task ImportFromExcelAsync()
    {
        // TODO: 实现文件对话框导入Excel
        StatusMessage = "Excel导入功能待实现";
        await Task.CompletedTask;
    }

    /// <summary>
    /// 添加数据项
    /// </summary>
    [RelayCommand]
    private void AddDataItem()
    {
        var newItem = new S7DataItem
        {
            Name = $"新数据项{CurrentDataItems.Count + 1}",
            Type = S7DataType.Bit,
            Address = "DB1.DBX0.0",
            Length = 1,
            DBNumber = 1
        };
        CurrentDataItems.Add(newItem);
        SelectedDataItem = newItem;
        StatusMessage = $"已添加: {newItem.Name}";
    }

    /// <summary>
    /// 删除选中数据项
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedDataItem()
    {
        if (SelectedDataItem == null)
        {
            StatusMessage = "请先选择数据项";
            return;
        }
        CurrentDataItems.Remove(SelectedDataItem);
        StatusMessage = $"已删除: {SelectedDataItem.Name}";
        SelectedDataItem = null;
    }

    /// <summary>
    /// 清空数据项列表
    /// </summary>
    [RelayCommand]
    private void ClearDataItemsCommand()
    {
        CurrentDataItems.Clear();
        SelectedDataItem = null;
        StatusMessage = "已清空数据项列表";
    }

    /// <summary>
    /// 应用并关闭
    /// </summary>
    [RelayCommand]
    private void ApplyAndClose()
    {
        var items = CurrentDataItems.ToList();
        _loadCallback?.Invoke(items);
        StatusMessage = "配置已应用";
        _closeCallback?.Invoke();
    }
}
