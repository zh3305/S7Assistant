using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using S7Assistant.Core.Interfaces;
using S7Assistant.Models;
using S7Assistant.ViewModels;
using System;

namespace S7Assistant.Views;

/// <summary>
/// 连接设置对话框
/// </summary>
public partial class ConnectionSettingsDialog : Window
{
    private readonly Action<S7ConnectionConfig>? _saveCallback;
    private ConnectionSettingsDialogViewModel? _viewModel;
    private readonly Func<S7ProviderType, IS7ClientService> _clientFactory;

    public ConnectionSettingsDialog()
    {
        InitializeComponent();
        _clientFactory = App.ServiceProvider.GetRequiredService<Func<S7ProviderType, IS7ClientService>>();
        _viewModel = new ConnectionSettingsDialogViewModel(() => Close(), _clientFactory);
        DataContext = _viewModel;
        SetupViewModelEvents();
    }

    public ConnectionSettingsDialog(S7ConnectionConfig existingConfig, Action<S7ConnectionConfig>? saveCallback)
    {
        InitializeComponent();
        _saveCallback = saveCallback;
        _clientFactory = App.ServiceProvider.GetRequiredService<Func<S7ProviderType, IS7ClientService>>();
        _viewModel = new ConnectionSettingsDialogViewModel(existingConfig, saveCallback, () => Close(), _clientFactory);
        DataContext = _viewModel;
        SetupViewModelEvents();
    }

    /// <summary>
    /// 设置ViewModel属性变化监听
    /// </summary>
    private void SetupViewModelEvents()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ConnectionSettingsDialogViewModel.IsTesting))
                    UpdateTestState();
                else if (e.PropertyName == nameof(ConnectionSettingsDialogViewModel.TestResultMessage))
                    UpdateTestResult();
            };
        }
    }

    /// <summary>
    /// 更新测试状态UI
    /// </summary>
    private void UpdateTestState()
    {
        if (_viewModel == null) return;

        var testButton = this.FindControl<Button>("TestButton");
        var testProgress = this.FindControl<ProgressBar>("TestProgress");

        if (testButton != null)
            testButton.IsEnabled = !_viewModel.IsTesting;
        if (testProgress != null)
            testProgress.IsVisible = _viewModel.IsTesting;
    }

    /// <summary>
    /// 更新测试结果UI
    /// </summary>
    private void UpdateTestResult()
    {
        if (_viewModel == null) return;

        var testResultBorder = this.FindControl<Border>("TestResultBorder");
        var testResultIndicator = this.FindControl<Ellipse>("TestResultIndicator");
        var testResultText = this.FindControl<TextBlock>("TestResultText");

        if (testResultBorder == null || testResultIndicator == null || testResultText == null)
            return;

        var hasMessage = !string.IsNullOrEmpty(_viewModel.TestResultMessage);
        testResultBorder.IsVisible = hasMessage;

        if (hasMessage)
        {
            testResultText.Text = _viewModel.TestResultMessage;

            if (_viewModel.TestSucceeded)
            {
                testResultBorder.Background = new SolidColorBrush(Color.Parse("#E8F5E9"));
                testResultIndicator.Fill = new SolidColorBrush(Color.Parse("#4CAF50"));
            }
            else
            {
                testResultBorder.Background = new SolidColorBrush(Color.Parse("#FFEBEE"));
                testResultIndicator.Fill = new SolidColorBrush(Color.Parse("#F44336"));
            }
        }
    }

    /// <summary>
    /// 确定按钮点击事件
    /// </summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel?.Config != null)
            {
                // 验证配置
                _viewModel.Config.IsValid();

                // 调用保存回调
                _saveCallback?.Invoke(_viewModel.Config);

                // 关闭对话框（返回true表示确认）
                Close(true);
            }
        }
        catch (Exception ex)
        {
            ShowTestResult($"配置错误: {ex.Message}", false);
        }
    }

    /// <summary>
    /// 测试连接按钮点击事件
    /// </summary>
    private async void OnTestClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.TestConnectionCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 显示测试结果
    /// </summary>
    private void ShowTestResult(string message, bool succeeded)
    {
        if (_viewModel != null)
        {
            _viewModel.TestResultMessage = message;
            _viewModel.TestSucceeded = succeeded;
        }
        UpdateTestResult();
    }
}
