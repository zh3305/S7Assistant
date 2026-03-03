using Avalonia.Controls;
using Avalonia.Platform.Storage;
using S7Assistant.Core.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace S7Assistant.Services;

/// <summary>
/// 对话框服务实现
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>
    /// 显示打开文件对话框
    /// </summary>
    public async Task<string?> ShowOpenFileDialogAsync(string title, params (string name, string[] extensions)[] filters)
    {
        var storageProvider = _owner.StorageProvider;
        if (storageProvider == null)
            return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters.Select(f => new FilePickerFileType(f.name)
            {
                Patterns = f.extensions.Select(e => $"*.{e}").ToArray()
            }).ToArray()
        };

        var result = await storageProvider.OpenFilePickerAsync(options);
        if (result.Count == 0)
            return null;

        return result[0].Path.LocalPath;
    }

    /// <summary>
    /// 显示保存文件对话框
    /// </summary>
    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, params (string name, string[] extensions)[] filters)
    {
        var storageProvider = _owner.StorageProvider;
        if (storageProvider == null)
            return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = filters.FirstOrDefault().extensions?.FirstOrDefault() ?? "",
            SuggestedFileName = defaultFileName,
            FileTypeChoices = filters.Select(f => new FilePickerFileType(f.name)
            {
                Patterns = f.extensions.Select(e => $"*.{e}").ToArray()
            }).ToArray()
        };

        var result = await storageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    /// <summary>
    /// 显示消息对话框
    /// </summary>
    public async Task ShowMessageDialogAsync(string title, string message, DialogMessageType type = DialogMessageType.Info)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var textBlock = new TextBlock
        {
            Text = message,
            Margin = new Avalonia.Thickness(20),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 10, 0, 20)
        };

        var okButton = new Button
        {
            Content = "确定",
            Padding = new Avalonia.Thickness(20, 8),
            MinWidth = 80
        };

        okButton.Click += (s, e) => dialog.Close();

        buttonPanel.Children.Add(okButton);

        var mainPanel = new StackPanel
        {
            Children = { textBlock, buttonPanel }
        };

        dialog.Content = mainPanel;

        await dialog.ShowDialog(_owner);
    }
}
