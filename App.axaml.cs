using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using S7Assistant.Services;
using S7Assistant.Services.S7Client;
using S7Assistant.ViewModels;

namespace S7Assistant;

/// <summary>
/// 应用程序入口点
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 服务提供者
    /// </summary>
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>
    /// 初始化应用程序
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 注册服务并配置依赖注入
    /// </summary>
    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // 核心服务
        services.AddSingleton<LogService>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ExcelService>();

        // S7客户端工厂 - 用于创建指定类型的客户端
        services.AddSingleton<Func<S7ProviderType, IS7ClientService>>(sp => providerType =>
        {
            return providerType switch
            {
                S7ProviderType.Sharp7 => new Sharp7ClientService(),
                S7ProviderType.S7NetPlus => new S7NetPlusClientService(),
                _ => throw new ArgumentException($"不支持的S7提供程序类型: {providerType}")
            };
        });

        // 视图模型
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConfigurationManagerViewModel>();
        services.AddTransient<ConnectionSettingsDialogViewModel>();

        ServiceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 应用程序启动时调用
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new Views.MainWindow();
            var viewModel = ServiceProvider.GetRequiredService<MainWindowViewModel>();

            // 创建对话框服务并注入到ViewModel
            var dialogService = new Services.DialogService(mainWindow);
            viewModel.SetDialogService(dialogService, mainWindow);

            mainWindow.DataContext = viewModel;
            desktop.MainWindow = mainWindow;

            // 订阅退出事件以清理资源
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 应用程序退出时清理资源
    /// </summary>
    private void OnExit(object? sender, EventArgs e)
    {
        ServiceProvider?.Dispose();
    }
}
