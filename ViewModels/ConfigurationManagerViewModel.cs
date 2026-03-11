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
/// 配置管理器视图模型 - 支持 Package 格式
/// </summary>
public sealed partial class ConfigurationManagerViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private Action<List<S7DataPackage>>? _loadCallback;
    private Action? _closeCallback;

    /// <summary>
    /// 配置文件列表
    /// </summary>
    public ObservableCollection<string> ConfigFiles { get; } = new();

    /// <summary>
    /// 选中的配置文件
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadSelected))]
    private string? _selectedConfigFile;

    /// <summary>
    /// 是否可以加载选中配置
    /// </summary>
    public bool CanLoadSelected => !string.IsNullOrEmpty(SelectedConfigFile);

    /// <summary>
    /// 新配置名称
    /// </summary>
    [ObservableProperty]
    private string _newConfigName = "";

    /// <summary>
    /// 当前 Package 列表
    /// </summary>
    public ObservableCollection<S7DataPackage> CurrentPackages { get; } = new();

    /// <summary>
    /// 选中的 Package
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPackageSelected))]
    private S7DataPackage? _selectedPackage;

    /// <summary>
    /// 是否有选中的 Package
    /// </summary>
    public bool IsPackageSelected => SelectedPackage != null;

    /// <summary>
    /// 选中的数据项（Package 内）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsItemSelected))]
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

    #region Package 编辑属性

    /// <summary>
    /// 编辑中的 Package 名称
    /// </summary>
    [ObservableProperty]
    private string _editPackageName = "";

    /// <summary>
    /// 编辑中的 DB 号
    /// </summary>
    [ObservableProperty]
    private int _editDbNumber = 1;

    /// <summary>
    /// 编辑中的区域类型
    /// </summary>
    [ObservableProperty]
    private S7AreaType _editArea = S7AreaType.DB;

    /// <summary>
    /// 编辑中的起始地址
    /// </summary>
    [ObservableProperty]
    private int _editStartAddress;

    /// <summary>
    /// 编辑中的长度
    /// </summary>
    [ObservableProperty]
    private int _editLength;

    /// <summary>
    /// 区域类型列表
    /// </summary>
    public S7AreaType[] AreaTypes { get; } = Enum.GetValues(typeof(S7AreaType)).Cast<S7AreaType>().ToArray();

    /// <summary>
    /// 数据类型列表
    /// </summary>
    public S7DataType[] DataTypes { get; } = Enum.GetValues(typeof(S7DataType)).Cast<S7DataType>().ToArray();

    #endregion

    #region 数据项编辑属性

    /// <summary>
    /// 编辑中的数据项名称
    /// </summary>
    [ObservableProperty]
    private string _editItemName = "";

    /// <summary>
    /// 编辑中的数据项类型
    /// </summary>
    [ObservableProperty]
    private S7DataType _editItemType = S7DataType.Byte;

    /// <summary>
    /// 编辑中的偏移量
    /// </summary>
    [ObservableProperty]
    private int _editItemOffset;

    /// <summary>
    /// 编辑中的位偏移
    /// </summary>
    [ObservableProperty]
    private int? _editItemBitOffset;

    /// <summary>
    /// 编辑中的备注
    /// </summary>
    [ObservableProperty]
    private string _editItemRemark = "";

    /// <summary>
    /// 是否显示位偏移编辑器
    /// </summary>
    public bool ShowBitOffsetEditor => EditItemType == S7DataType.Bit;

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public ConfigurationManagerViewModel(ConfigService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    /// <summary>
    /// 设置回调函数
    /// </summary>
    public void SetCallbacks(Action<List<S7DataPackage>> loadCallback, Action closeCallback)
    {
        _loadCallback = loadCallback ?? throw new ArgumentNullException(nameof(loadCallback));
        _closeCallback = closeCallback ?? throw new ArgumentNullException(nameof(closeCallback));
    }

    /// <summary>
    /// 选中配置文件改变时加载
    /// </summary>
    partial void OnSelectedConfigFileChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadSelectedConfigAsync();
        }
    }

    /// <summary>
    /// 选中 Package 改变时更新编辑器
    /// </summary>
    partial void OnSelectedPackageChanged(S7DataPackage? value)
    {
        if (value != null)
        {
            EditPackageName = value.Name;
            EditDbNumber = value.DbNumber;
            EditArea = value.Area;
            EditStartAddress = value.StartAddress;
            EditLength = value.Length;
        }
    }

    /// <summary>
    /// 选中数据项改变时更新编辑器
    /// </summary>
    partial void OnSelectedDataItemChanged(S7DataItem? value)
    {
        if (value != null)
        {
            EditItemName = value.Name;
            EditItemType = value.Type;
            EditItemOffset = value.Offset;
            EditItemBitOffset = value.BitOffset;
            EditItemRemark = value.Remark;
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
            var packages = await _configService.LoadPackagesAsync(fileName);

            CurrentPackages.Clear();
            foreach (var package in packages)
            {
                CurrentPackages.Add(package);
            }

            SelectedPackage = null;
            SelectedDataItem = null;
            StatusMessage = $"已加载 {packages.Count} 个数据包";
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
            var samplePackages = new List<S7DataPackage>
            {
                new()
                {
                    Name = "数据包1",
                    DbNumber = 1,
                    Area = S7AreaType.DB,
                    StartAddress = 0,
                    Length = 10,
                    Items = new ObservableCollection<S7DataItem>()
                }
            };
            await _configService.SavePackagesAsync(fileName, samplePackages);
            await LoadConfigFilesAsync();
            SelectedConfigFile = fileName;
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
            var packages = CurrentPackages.ToList();
            await _configService.SavePackagesAsync(fileName, packages);
            StatusMessage = $"已保存配置: {SelectedConfigFile}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
        }
    }

    #region Package 操作

    /// <summary>
    /// 添加 Package
    /// </summary>
    [RelayCommand]
    private void AddPackage()
    {
        var newPackage = new S7DataPackage
        {
            Name = $"数据包{CurrentPackages.Count + 1}",
            DbNumber = 1,
            Area = S7AreaType.DB,
            StartAddress = 0,
            Length = 10,
            Items = new ObservableCollection<S7DataItem>()
        };
        CurrentPackages.Add(newPackage);
        SelectedPackage = newPackage;
        StatusMessage = $"已添加: {newPackage.Name}";
    }

    /// <summary>
    /// 删除选中 Package
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedPackage()
    {
        if (SelectedPackage == null)
        {
            StatusMessage = "请先选择数据包";
            return;
        }
        var name = SelectedPackage.Name;
        CurrentPackages.Remove(SelectedPackage);
        StatusMessage = $"已删除: {name}";
        SelectedPackage = null;
    }

    /// <summary>
    /// 应用 Package 编辑
    /// </summary>
    [RelayCommand]
    private void ApplyPackageEdit()
    {
        if (SelectedPackage == null)
            return;

        SelectedPackage.Name = EditPackageName;
        SelectedPackage.DbNumber = EditDbNumber;
        SelectedPackage.Area = EditArea;
        SelectedPackage.StartAddress = EditStartAddress;
        SelectedPackage.Length = EditLength;
        StatusMessage = $"已更新数据包: {EditPackageName}";
    }

    #endregion

    #region 数据项操作

    /// <summary>
    /// 添加数据项
    /// </summary>
    [RelayCommand]
    private void AddDataItem()
    {
        if (SelectedPackage == null)
        {
            StatusMessage = "请先选择数据包";
            return;
        }

        var newItem = new S7DataItem
        {
            Name = $"数据项{SelectedPackage.Items.Count + 1}",
            Type = S7DataType.Byte,
            Offset = 0,
            Remark = ""
        };
        SelectedPackage.Items.Add(newItem);
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
        if (SelectedPackage == null)
        {
            StatusMessage = "请先选择数据包";
            return;
        }

        var name = SelectedDataItem.Name;
        SelectedPackage.Items.Remove(SelectedDataItem);
        StatusMessage = $"已删除: {name}";
        SelectedDataItem = null;
    }

    /// <summary>
    /// 应用数据项编辑
    /// </summary>
    [RelayCommand]
    private void ApplyDataItemEdit()
    {
        if (SelectedDataItem == null)
            return;

        SelectedDataItem.Name = EditItemName;
        SelectedDataItem.Type = EditItemType;
        SelectedDataItem.Offset = EditItemOffset;
        SelectedDataItem.BitOffset = EditItemBitOffset;
        SelectedDataItem.Remark = EditItemRemark;
        StatusMessage = $"已更新数据项: {EditItemName}";
    }

    #endregion

    /// <summary>
    /// 应用并关闭
    /// </summary>
    [RelayCommand]
    private void ApplyAndClose()
    {
        var packages = CurrentPackages.ToList();
        _loadCallback?.Invoke(packages);
        StatusMessage = "配置已应用";
        _closeCallback?.Invoke();
    }
}
