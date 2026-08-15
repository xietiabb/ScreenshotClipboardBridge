using System.Windows.Forms;
using Windows.UI.Notifications;

namespace ScreenshotClipboardBridge.Services;

/// <summary>
/// 转换成功通知服务。
/// 优先使用 Windows 原生 Toast（Windows.UI.Notifications，Win10/11 均正常弹出，
/// 且不打断操作、无独立窗口）；
/// 若 Toast 不可用（例如 AUMID 注册失败），自动回退到托盘气泡提示。
/// </summary>
public sealed class ToastService
{
    /// <summary>AppUserModelID：Toast 通知必须与「带 AUMID 的开始菜单快捷方式」匹配才能弹出。</summary>
    public const string Aumid = "ScreenshotClipboardBridge.App";

    private readonly NotifyIcon? _fallbackIcon;
    private bool _registered;

    public ToastService(NotifyIcon? fallbackIcon = null) => _fallbackIcon = fallbackIcon;

    /// <summary>
    /// 弹出「截图已转换为路径」通知。
    /// </summary>
    /// <param name="fileName">第二行显示的截图文件名。</param>
    public void ShowSaved(string fileName)
    {
        try
        {
            EnsureRegistered();

            // ToastText02 模板：第一行标题、第二行正文。
            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var texts = xml.GetElementsByTagName("text");
            int index = 0;
            foreach (var node in texts)
            {
                if (index == 0)
                {
                    node.AppendChild(xml.CreateTextNode("截图已转换为路径"));
                }
                else if (index == 1)
                {
                    node.AppendChild(xml.CreateTextNode(fileName));
                }
                else
                {
                    break;
                }

                index++;
            }

            var toast = new ToastNotification(xml);
            ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
        }
        catch
        {
            // 回退：托盘气泡（旧式，但聊胜于无）。
            try
            {
                _fallbackIcon?.ShowBalloonTip(2000, "截图已转换为路径", fileName, ToolTipIcon.Info);
            }
            catch
            {
                // 通知是锦上添花，失败不影响核心功能。
            }
        }
    }

    /// <summary>
    /// 确保存在带本应用 AUMID 的开始菜单快捷方式（Toast 弹出的前提，只做一次）。
    /// </summary>
    private void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        Native.AppUserModelIdRegistrar.EnsureShortcut(App.AppPaths.CurrentExePath, Aumid);
        _registered = true;
    }
}
