using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using S7Assistant.Models;
using S7Assistant.ViewModels;
using System;
using System.Collections.Generic;

namespace S7Assistant.Views;

/// <summary>
/// 配置管理器窗口
/// </summary>
public partial class ConfigurationManagerWindow : Window
{
    private ConfigurationManagerViewModel? _viewModel;

    /// <summary>
    /// 设计时构造函数
    /// </summary>
    public ConfigurationManagerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 运行时构造函数
    /// </summary>
    public ConfigurationManagerWindow(Action<List<S7DataPackage>> loadCallback)
    {
        InitializeComponent();

        // 从 DI 获取 ViewModel
        _viewModel = App.ServiceProvider.GetRequiredService<ConfigurationManagerViewModel>();
        _viewModel.SetCallbacks(loadCallback, Close);
        DataContext = _viewModel;

        // 窗口加载后异步初始化配置文件列表
        Loaded += async (s, e) =>
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadConfigFilesAsync();
            }
        };
    }

    /// <summary>
    /// 加载配置按钮点击事件
    /// </summary>
    private async void OnLoadConfigClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string fileName)
        {
            try
            {
                if (_viewModel != null)
                {
                    _viewModel.SelectedConfigFile = fileName;
                    await _viewModel.LoadSelectedConfigCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                // 显示错误信息
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
