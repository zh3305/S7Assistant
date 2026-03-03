using System.Threading.Tasks;

namespace S7Assistant.Core.Interfaces;

/// <summary>
/// 对话框服务接口
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 显示打开文件对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="filters">文件过滤器</param>
    /// <returns>选中的文件路径，如果取消则返回null</returns>
    Task<string?> ShowOpenFileDialogAsync(string title, params (string name, string[] extensions)[] filters);

    /// <summary>
    /// 显示保存文件对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="defaultFileName">默认文件名</param>
    /// <param name="filters">文件过滤器</param>
    /// <returns>保存的文件路径，如果取消则返回null</returns>
    Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, params (string name, string[] extensions)[] filters);

    /// <summary>
    /// 显示消息对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="type">消息类型</param>
    Task ShowMessageDialogAsync(string title, string message, DialogMessageType type = DialogMessageType.Info);
}

/// <summary>
/// 消息对话框类型
/// </summary>
public enum DialogMessageType
{
    /// <summary>
    /// 信息
    /// </summary>
    Info,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 错误
    /// </summary>
    Error
}
