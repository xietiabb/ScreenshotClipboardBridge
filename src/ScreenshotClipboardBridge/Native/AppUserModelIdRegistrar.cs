using System.Runtime.InteropServices;
using System.Text;

namespace ScreenshotClipboardBridge.Native;

/// <summary>
/// AUMID（AppUserModelID）注册器。
/// 作用：在开始菜单创建一个指向本程序的快捷方式，并写入 System.AppUserModel.ID 属性。
/// 这是「非商店打包应用」弹出 Windows Toast 通知的前提条件。
/// 实现：IShellLink（创建快捷方式）+ IPropertyStore（写入 AUMID）。
/// 全程 try/catch：注册失败时通知降级为托盘气泡，不影响主功能。
/// </summary>
internal static class AppUserModelIdRegistrar
{
    /// <summary>System.AppUserModel.ID 属性的 PropertyKey（fmtid + pid）。</summary>
    private static readonly Guid AppUserModelIdFmtId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private const uint AppUserModelIdPid = 5;

    /// <summary>VT_LPWSTR 的 variant type 值。</summary>
    private const ushort VtLpwstr = 31;

    /// <summary>
    /// 确保开始菜单快捷方式存在且指向当前 EXE。
    /// 若目标路径已变化（发布新版本后路径不同）会自动重建。
    /// </summary>
    public static void EnsureShortcut(string exePath, string aumid)
    {
        try
        {
            string lnkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "Screenshot Clipboard Bridge.lnk");

            // 已存在且目标一致 → 无需重建。
            if (File.Exists(lnkPath) && ShortcutTargets(lnkPath, exePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(lnkPath)!);

            var link = (IShellLinkW)new ShellLink();
            link.SetPath(exePath);
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? string.Empty);
            link.SetDescription("Screenshot Clipboard Bridge");

            // 写入 AppUserModelID 属性。
            var store = (IPropertyStore)link;
            var key = new PropertyKey(AppUserModelIdFmtId, AppUserModelIdPid);
            var value = new PropVariant { Vt = VtLpwstr, PtrValue = Marshal.StringToCoTaskMemUni(aumid) };
            try
            {
                store.SetValue(ref key, ref value);
                store.Commit();
            }
            finally
            {
                Marshal.FreeCoTaskMem(value.PtrValue);
            }

            ((IPersistFile)link).Save(lnkPath, true);
            Marshal.FinalReleaseComObject(link);
        }
        catch
        {
            // 通知注册失败不致命：ToastService 会自动回退。
        }
    }

    /// <summary>读取现有快捷方式的目标路径，判断是否仍指向当前 EXE。</summary>
    private static bool ShortcutTargets(string lnkPath, string exePath)
    {
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell COM 不可用");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string target = shortcut.TargetPath;
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            return string.Equals(target.TrimEnd('"'), exePath.TrimEnd('"'), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // ==================== COM Interop 声明 ====================

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, out PropVariant pv);
        void SetValue(ref PropertyKey key, ref PropVariant pv);
        void Commit();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile(out IntPtr ppszFileName);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FmtId;
        public uint Pid;

        public PropertyKey(Guid fmtId, uint pid)
        {
            FmtId = fmtId;
            Pid = pid;
        }
    }

    /// <summary>
    /// PROPVARIANT 的极简布局：只需支持 VT_LPWSTR（字符串指针）即可。
    /// 布局：ushort vt + 6 字节保留 + 联合体（8 字节对齐，指针位于偏移 8）。
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Vt;
        [FieldOffset(8)] public IntPtr PtrValue;
    }
}
