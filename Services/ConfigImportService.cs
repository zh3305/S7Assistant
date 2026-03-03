using S7Assistant.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace S7Assistant.Services;

/// <summary>
/// 配置导入服务 - 将旧版 XML 配置转换为新系统 JSON 格式
/// </summary>
public class ConfigImportService
{
    /// <summary>
    /// 从旧版 XML 配置导入
    /// </summary>
    /// <param name="xmlPath">XML 配置文件路径</param>
    /// <param name="defaultDbNumber">默认 DB 编号</param>
    /// <returns>转换后的数据项列表</returns>
    public List<S7DataItem> ImportFromLegacyXml(string xmlPath, int defaultDbNumber = 1)
    {
        if (!File.Exists(xmlPath))
        {
            return new List<S7DataItem>();
        }

        var doc = XDocument.Load(xmlPath);
        var items = new List<S7DataItem>();

        foreach (var package in doc.Descendants("Package"))
        {
            var dbName = package.Attribute("name")?.Value ?? "未命名";
            var dbNumberAttr = package.Attribute("dbnumber")?.Value;
            var dbNumber = !string.IsNullOrEmpty(dbNumberAttr) ? int.Parse(dbNumberAttr) : defaultDbNumber;


            foreach (var nameElem in package.Descendants("Name"))
            {
                var item = ConvertLegacyItem(nameElem, dbNumber);
                if (item != null)
                {
                    items.Add(item);
                }
            }
        }

        return items;
    }

    /// <summary>
    /// 转换单个旧配置项
    /// </summary>
    private S7DataItem? ConvertLegacyItem(XElement elem, int dbNumber)
    {
        var number = elem.Attribute("number")?.Value ?? "";
        var typeStr = elem.Attribute("type")?.Value?.ToLower() ?? "";
        var address = elem.Attribute("address")?.Value ?? "";
        var name = elem.Attribute("name")?.Value ?? "";

        // 解析地址格式：V9.0 或 VB0 或 VD4
        var (offset, bit) = ParseLegacyAddress(address);

        // 转换数据类型
        var dataType = typeStr switch
        {
            "bool" => S7DataType.Bit,
            "byte" => S7DataType.Byte,
            "word" => S7DataType.Word,
            "dword" => S7DataType.DWord,
            "real" => S7DataType.Real,
            "dint" => S7DataType.DInt,
            _ => throw new NotSupportedException($"不支持的数据类型: {typeStr}")
        };

        // 生成新地址格式
        var newAddress = GenerateNewAddress(dbNumber, offset, bit, dataType);

        // 解析备注中的值描述映射
        var typeValues = ParseTypeValues(name);

        // 解析名称（去掉值描述部分）
        var cleanName = ExtractName(name);

        return new S7DataItem
        {
            Name = cleanName,
            Type = dataType,
            Address = newAddress,
            DBNumber = dbNumber,
            Length = 1,
            Remark = name,
            TypeValues = typeValues
        };
    }

    /// <summary>
    /// 解析旧版地址格式
    /// </summary>
    private (int offset, int? bit) ParseLegacyAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return (0, null);

        // 去掉前缀 V
        var addr = address.Trim();
        if (addr.StartsWith("V", StringComparison.OrdinalIgnoreCase))
        {
            addr = addr.Substring(1);
        }

        var parts = addr.Split('.');
        var offset = int.Parse(parts[0]);
        int? bit = parts.Length > 1 ? int.Parse(parts[1]) : null;

        return (offset, bit);
    }

    /// <summary>
    /// 生成新地址格式
    /// </summary>
    private string GenerateNewAddress(int dbNumber, int offset, int? bit, S7DataType dataType)
    {
        if (dataType == S7DataType.Bit && bit.HasValue)
        {
            return $"DB{dbNumber}.DBX{offset}.{bit}";
        }
        else
        {
            // 根据数据类型返回相应格式
            return dataType switch
            {
                S7DataType.Byte => $"DB{dbNumber}.DBB{offset}",
                S7DataType.Word => $"DB{dbNumber}.DBW{offset}",
                S7DataType.DWord => $"DB{dbNumber}.DBD{offset}",
                S7DataType.Real => $"DB{dbNumber}.DBD{offset}",
                S7DataType.Int => $"DB{dbNumber}.DBW{offset}",
                S7DataType.DInt => $"DB{dbNumber}.DBD{offset}",
                _ => $"DB{dbNumber}.DBD{offset}"
            };
        }
    }

    /// <summary>
    /// 解析备注中的值描述映射
    /// </summary>
    private Dictionary<string, string> ParseTypeValues(string name)
    {
        var result = new Dictionary<string, string>();

        // 查找冒号分隔符
        var colonIndex = name.IndexOf(':');
        if (colonIndex < 0)
            return result;

        var valuesPart = name.Substring(colonIndex + 1);

        // 解析格式如 "0=就地,1=远程" 或 "0_就地_1_远程"
        foreach (var pair in valuesPart.Split(',', '_', '-'))
        {
            var kv = pair.Split('=', '—');
            if (kv.Length >= 2)
            {
                var key = kv[0].Trim();
                var value = kv[1].Trim();
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// 提取名称（去掉值描述部分）
    /// </summary>
    private string ExtractName(string name)
    {
        var colonIndex = name.IndexOf(':');
        if (colonIndex > 0)
        {
            return name.Substring(0, colonIndex).Trim();
        }
        return name.Trim();
    }
}
