using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace S7Assistant.Views;

/// <summary>
/// 主窗口
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 数据项双击事件
    /// </summary>
    public void OnDataItemDoubleClick(object? sender, RoutedEventArgs e)
    {
        // 双击数据项时触发编辑命令
        if (DataContext is ViewModels.MainWindowViewModel vm && sender is ListBox listBox)
        {
            if (listBox.SelectedItem is Models.S7DataItem item)
            {
                vm.EditSelectedItemCommand.Execute(item);
            }
        }
    }
}
