using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace S7Assistant.Services;

/// <summary>
/// 配置服务
/// </summary>
public class ConfigService
{
    private readonly string _configDirectory;
    private const string ConnectionConfigFile = "connection.json";

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
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
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
                var config = JsonSerializer.Deserialize<S7ConnectionConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return config;
            }
            catch
            {
                return null;
            }
        });
    }

    #endregion

    #region 数据项配置

    /// <summary>
    /// 获取所有配置文件
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
    /// 加载配置文件
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
            var items = JsonSerializer.Deserialize<List<S7DataItem>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return items ?? throw new InvalidOperationException($"配置文件格式错误或为空: {fileName}");
        });
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    public Task SaveConfigAsync(string fileName, List<S7DataItem> items)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));

        if (items == null)
            throw new ArgumentNullException(nameof(items));

        // 允许保存空配置（用于创建新配置）

        var fullFileName = fileName.EndsWith(".json") ? fileName : $"{fileName}.json";
        var path = Path.Combine(_configDirectory, fullFileName);

        return Task.Run(() =>
        {
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
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
    /// 创建示例配置
    /// </summary>
    public Task CreateSampleConfigAsync()
    {
        var sampleItems = new List<S7DataItem>
        {
            new() { Name = "温度传感器", Type = S7DataType.Real, Address = "DB1.DBD0", Remark = "温度范围: -50~150°C" },
            new() { Name = "启动按钮", Type = S7DataType.Bit, Address = "I0.0", TypeValues = new Dictionary<string, string> { { "0", "未按下" }, { "1", "已按下" } } },
            new() { Name = "电机控制", Type = S7DataType.Bit, Address = "Q0.0", TypeValues = new Dictionary<string, string> { { "0", "停止" }, { "1", "启动" } } }
        };

        return SaveConfigAsync("sample.json", sampleItems);
    }

    #endregion
}
