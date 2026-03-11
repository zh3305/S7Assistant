using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace S7Assistant.Services;

/// <summary>
/// 配置服务
/// </summary>
public class ConfigService
{
    private readonly string _configDirectory;
    private const string ConnectionConfigFile = "connection.json";

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ConfigDirectory => _configDirectory;

    public ConfigService(string configDirectory = "config")
    {
        _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));

        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }

    #region 连接配置

    /// <summary>
    /// 保存连接配置
    /// </summary>
    public Task SaveConnectionConfigAsync(S7ConnectionConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var path = Path.Combine(_configDirectory, ConnectionConfigFile);

        return Task.Run(() =>
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(path, json);
        });
    }

    /// <summary>
    /// 加载连接配置
    /// </summary>
    public Task<S7ConnectionConfig?> LoadConnectionConfigAsync()
    {
        var path = Path.Combine(_configDirectory, ConnectionConfigFile);

        if (!File.Exists(path))
            return Task.FromResult<S7ConnectionConfig?>(null);

        return Task.Run(() =>
        {
            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<S7ConnectionConfig>(json, JsonOptions);
                return config;
            }
            catch
            {
                return null;
            }
        });
    }

    #endregion

    #region 旧版配置（向后兼容）

    /// <summary>
    /// 加载旧版配置文件（单个数据项列表）
    /// </summary>
    public Task<List<S7DataItem>> LoadConfigAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));

        var path = Path.Combine(_configDirectory, fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"配置文件不存在: {fileName}");

        return Task.Run(() =>
        {
            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<S7DataItem>>(json, JsonOptions);

            return items ?? throw new InvalidOperationException($"配置文件格式错误或为空: {fileName}");
        });
    }

    /// <summary>
    /// 保存旧版配置文件（单个数据项列表）
    /// </summary>
    public Task SaveConfigAsync(string fileName, List<S7DataItem> items)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));

        if (items == null)
            throw new ArgumentNullException(nameof(items));

        var fullFileName = fileName.EndsWith(".json") ? fileName : $"{fileName}.json";
        var path = Path.Combine(_configDirectory, fullFileName);

        return Task.Run(() =>
        {
            var json = JsonSerializer.Serialize(items, JsonOptions);
            File.WriteAllText(path, json);
        });
    }

    #endregion

    #region Package 配置

    /// <summary>
    /// 加载 Package 配置文件
    /// </summary>
    public Task<List<S7DataPackage>> LoadPackagesAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));

        var path = Path.Combine(_configDirectory, fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"配置文件不存在: {fileName}");

        return Task.Run(() =>
        {
            var json = File.ReadAllText(path);
            var packages = JsonSerializer.Deserialize<List<S7DataPackage>>(json, JsonOptions);

            return packages ?? throw new InvalidOperationException($"配置文件格式错误或为空: {fileName}");
        });
    }

    /// <summary>
    /// 保存 Package 配置文件
    /// </summary>
    public Task SavePackagesAsync(string fileName, List<S7DataPackage> packages)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));

        if (packages == null)
            throw new ArgumentNullException(nameof(packages));

        var fullFileName = fileName.EndsWith(".json") ? fileName : $"{fileName}.json";
        var path = Path.Combine(_configDirectory, fullFileName);

        return Task.Run(() =>
        {
            var json = JsonSerializer.Serialize(packages, JsonOptions);
            File.WriteAllText(path, json);
        });
    }

    /// <summary>
    /// 获取所有配置文件列表
    /// </summary>
    public Task<string[]> GetConfigFilesAsync()
    {
        return Task.Run(() =>
        {
            return Directory.GetFiles(_configDirectory, "*.json")
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(f => f != ConnectionConfigFile) // 排除连接配置文件
                .OrderBy(f => f)
                .ToArray();
        });
    }

    /// <summary>
    /// 删除配置文件
    /// </summary>
    public Task DeleteConfigAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));

        var fullFileName = fileName.EndsWith(".json") ? fileName : $"{fileName}.json";
        var path = Path.Combine(_configDirectory, fullFileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"配置文件不存在: {fileName}");

        return Task.Run(() =>
        {
            File.Delete(path);
        });
    }

    /// <summary>
    /// 检查配置文件是否存在
    /// </summary>
    public bool ConfigExists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var fullFileName = fileName.EndsWith(".json") ? fileName : $"{fileName}.json";
        var path = Path.Combine(_configDirectory, fullFileName);

        return File.Exists(path);
    }

    /// <summary>
    /// 创建示例 Package 配置
    /// </summary>
    public Task CreateSamplePackageConfigAsync()
    {
        var samplePackages = new List<S7DataPackage>
        {
            new()
            {
                Name = "综合控制柜_读取",
                DbNumber = 1,
                Area = S7AreaType.DB,
                StartAddress = 200,
                Length = 132,
                Items = new ObservableCollection<S7DataItem>
                {
                    new() { Name = "生命信号_读取", Type = S7DataType.Byte, Offset = 0 },
                    new() { Name = "车位状态", Type = S7DataType.Byte, Offset = 1, TypeValues = new Dictionary<string, string> { { "55", "有车" }, { "66", "无车" } } },
                    new() { Name = "送电状态", Type = S7DataType.Byte, Offset = 2 },
                    new() { Name = "进出计数", Type = S7DataType.Byte, Offset = 6 }
                }
            },
            new()
            {
                Name = "综合控制柜_控制",
                DbNumber = 1,
                Area = S7AreaType.DB,
                StartAddress = 320,
                Length = 12,
                Items = new ObservableCollection<S7DataItem>
                {
                    new() { Name = "生命信号_写入", Type = S7DataType.Byte, Offset = 0 },
                    new() { Name = "异常报警", Type = S7DataType.Byte, Offset = 1, TypeValues = new Dictionary<string, string> { { "55", "报警" }, { "66", "正常" } } },
                    new() { Name = "作业状态", Type = S7DataType.Byte, Offset = 2, TypeValues = new Dictionary<string, string> { { "66", "结束" }, { "201", "作业中" } } },
                    new() { Name = "门禁1开关", Type = S7DataType.Byte, Offset = 3, TypeValues = new Dictionary<string, string> { { "202", "开" }, { "203", "关" } } },
                    new() { Name = "门禁1报警", Type = S7DataType.Byte, Offset = 4, TypeValues = new Dictionary<string, string> { { "202", "正常" }, { "203", "报警" } } }
                }
            }
        };

        return SavePackagesAsync("sample_package.json", samplePackages);
    }

    #endregion
}
