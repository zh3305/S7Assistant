using S7Assistant.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace S7Assistant.Services;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// 日志条目
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }

    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {Message}";
    }
}

/// <summary>
/// 日志服务 - 同时输出到UI集合和控制台
/// </summary>
public sealed class LogService
{
    private readonly ObservableCollection<LogEntry> _logs = new();
    private readonly object _lock = new();

    public ObservableCollection<LogEntry> Logs => _logs;

    private int _maxLogCount = 1000;

    /// <summary>
    /// 最大日志数量
    /// </summary>
    public int MaxLogCount
    {
        get => _maxLogCount;
        set => _maxLogCount = Math.Max(100, value);
    }

    /// <summary>
    /// 是否输出到控制台
    /// </summary>
    public bool EnableConsoleOutput { get; set; } = true;

    /// <summary>
    /// 记录日志
    /// </summary>
    public void Log(string message, LogLevel level = LogLevel.Info, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("消息不能为空", nameof(message));

        var entry = new LogEntry
        {
            Message = message,
            Level = level,
            Details = details
        };

        // 输出到控制台
        if (EnableConsoleOutput)
        {
            WriteToConsole(entry);
        }

        // 更新UI集合
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            lock (_lock)
            {
                _logs.Add(entry);

                // 保持日志数量在限制内
                while (_logs.Count > MaxLogCount)
                {
                    _logs.RemoveAt(0);
                }
            }
        });
    }

    /// <summary>
    /// 写入控制台（带颜色）
    /// </summary>
    private void WriteToConsole(LogEntry entry)
    {
        var originalColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = GetConsoleColor(entry.Level);
            Console.WriteLine(entry.ToString());

            if (!string.IsNullOrEmpty(entry.Details))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    详情: {entry.Details}");
            }
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    /// <summary>
    /// 获取日志级别对应的控制台颜色
    /// </summary>
    private static ConsoleColor GetConsoleColor(LogLevel level) => level switch
    {
        LogLevel.Debug => ConsoleColor.DarkGray,
        LogLevel.Info => ConsoleColor.White,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        _ => ConsoleColor.White
    };

    /// <summary>
    /// 清空日志
    /// </summary>
    public void Clear()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            lock (_lock)
            {
                _logs.Clear();
            }
        });

        if (EnableConsoleOutput)
        {
            Console.WriteLine("[日志已清空]");
        }
    }

    /// <summary>
    /// 保存日志到文件
    /// </summary>
    public void SaveToFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("文件路径不能为空", nameof(path));

        lock (_lock)
        {
            var lines = Logs.Select(l => l.ToString());
            File.WriteAllLines(path, lines);
        }

        if (EnableConsoleOutput)
        {
            Console.WriteLine($"[日志已保存到: {path}]");
        }
    }
}
