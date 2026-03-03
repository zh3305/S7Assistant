using S7Assistant.Models;
using MiniExcelLibs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace S7Assistant.Services;

/// <summary>
/// Excel导入导出服务
/// </summary>
public class ExcelService
{
    /// <summary>
    /// 从Excel导入配置
    /// </summary>
    public Task<List<S7DataItem>> ImportFromExcelAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"文件不存在: {filePath}");

        return Task.Run(() =>
        {
            var rows = MiniExcel.Query(filePath).ToList();
            var items = new List<S7DataItem>();

            foreach (var row in rows.Skip(1)) // 跳过标题行
            {
                try
                {
                    string? lengthStr = GetValue(row, "长度", "Length");
                    int length = 1;
                    if (!string.IsNullOrEmpty(lengthStr))
                    {
                        int.TryParse(lengthStr, out length);
                    }

                    var item = new S7DataItem
                    {
                        Name = GetValue(row, "名称", "Name") ?? throw new InvalidOperationException($"行 {items.Count + 2} 缺少名称列"),
                        Type = ParseEnum<S7DataType>(GetValue(row, "类型", "Type") ?? throw new InvalidOperationException($"行 {items.Count + 2} 缺少类型列")),
                        Address = GetValue(row, "地址", "Address") ?? throw new InvalidOperationException($"行 {items.Count + 2} 缺少地址列"),
                        Length = length,
                        Remark = GetValue(row, "备注", "Remark") ?? ""
                    };

                    // 解析值映射
                    var typeValuesStr = GetValue(row, "值映射", "TypeValues", "Values");
                    if (!string.IsNullOrEmpty(typeValuesStr))
                    {
                        item.TypeValues = ParseTypeValues(typeValuesStr);
                    }

                    items.Add(item);
                }
                catch (InvalidOperationException)
                {
                    throw; // 重新抛出，让调用者知道哪行有问题
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"导入第 {items.Count + 2} 行失败: {ex.Message}", ex);
                }
            }

            if (items.Count == 0)
                throw new InvalidOperationException("Excel文件不包含有效的数据行");

            return items;
        });
    }

    /// <summary>
    /// 导出配置到Excel
    /// </summary>
    public Task ExportToExcelAsync(string filePath, List<S7DataItem> items)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (items == null)
            throw new ArgumentNullException(nameof(items));

        if (items.Count == 0)
            throw new ArgumentException("数据项不能为空", nameof(items));

        return Task.Run(() =>
        {
            var data = items.Select(item => new
            {
                名称 = item.Name,
                类型 = item.Type.ToString(),
                地址 = item.Address,
                长度 = item.Length,
                备注 = item.Remark,
                值映射 = string.Join("; ", item.TypeValues.Select(kv => $"{kv.Key}={kv.Value}"))
            });

            MiniExcel.SaveAs(filePath, data);
        });
    }

    /// <summary>
    /// 生成Excel模板
    /// </summary>
    public Task<string> GenerateTemplateAsync(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentException("保存路径不能为空", nameof(savePath));

        var template = new[]
        {
            new { 名称 = "温度传感器", 类型 = "Real", 地址 = "DB1.DBD0", 长度 = 1, 操作 = "Read", 备注 = "温度范围: -50~150°C", 值映射 = "" },
            new { 名称 = "启动按钮", 类型 = "Bit", 地址 = "I0.0", 长度 = 1, 操作 = "Read", 备注 = "", 值映射 = "0=未按下;1=已按下" },
            new { 名称 = "电机控制", 类型 = "Bit", 地址 = "Q0.0", 长度 = 1, 操作 = "Write", 备注 = "", 值映射 = "0=停止;1=启动" }
        };

        MiniExcel.SaveAs(savePath, template);
        return Task.FromResult(savePath);
    }

    private static string? GetValue(IDictionary<string, object> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && value != null)
                return value?.ToString();
        }
        return null;
    }

    private static T ParseEnum<T>(string? value) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, true, out var result))
            return result;
        throw new InvalidOperationException($"无效的枚举值: {value} (类型: {typeof(T).Name})");
    }

    private static Dictionary<string, string> ParseTypeValues(string input)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(input))
            return dict;

        var pairs = input.Split([';', ','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split(['=', ':'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                dict[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return dict;
    }
}
