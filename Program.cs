using Avalonia;
using System;

namespace S7Assistant;

/// <summary>
/// 应用程序入口点
/// </summary>
internal class Program
{
    /// <summary>
    /// 应用程序主入口点
    /// </summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// 配置并构建Avalonia应用程序
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
