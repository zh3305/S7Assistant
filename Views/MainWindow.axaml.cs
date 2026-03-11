using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using S7Assistant.ViewModels;
using System.Collections.Specialized;

namespace S7Assistant.Views;

/// <summary>
/// 主窗口
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// DataContext 改变时订阅日志集合变化
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Logs.CollectionChanged += OnLogsCollectionChanged;
        }
    }

    /// <summary>
    /// 日志集合变化时自动滚动到底部
    /// </summary>
    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
                scrollViewer?.ScrollToEnd();
            });
        }
    }
}
