using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RBX_AntiAFK.Core;

class WindowInfo
{
    public IntPtr Handle { get; }
    public string Title { get; }
    public bool IsMinimized => IsIconic(Handle);
    public bool IsForeground => GetForegroundWindow() == Handle;
    public bool IsVisible => IsWindowVisible(Handle);
    public bool IsValidWindow => IsWindow(Handle);

    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;
    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;
    private const int SW_HIDE = 0;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_TOP = new(0);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public WindowInfo(IntPtr handle, string title)
    {
        Handle = handle;
        Title = title;
    }

    public void Show() => ShowWindow(Handle, SW_SHOW);
    public void Hide() => ShowWindow(Handle, SW_HIDE);
    public void Restore() => ShowWindow(Handle, SW_RESTORE);
    public void Minimize() => ShowWindow(Handle, SW_MINIMIZE);
    public void Maximize() => ShowWindow(Handle, SW_MAXIMIZE);
    public void Activate() => SetForegroundWindow(Handle);

    public void SetTransparency(int transparency)
    {
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const int LWA_ALPHA = 0x2;

        int style = (int)GetWindowLong(Handle, GWL_EXSTYLE);
        _ = SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_LAYERED);
        SetLayeredWindowAttributes(Handle, 0, (byte)transparency, LWA_ALPHA);
    }

    public void SetNoTopMost() => SetWindowPos(Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    public void SetTop() => SetWindowPos(Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    public void SetTopMost() => SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
}

class WinManager
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private class EnumWindowsData
    {
        public required HashSet<int> ProcessIds;
        public required ConcurrentBag<WindowInfo> Windows;
    }

    public static List<WindowInfo> GetWindowsByProcessName(string processName)
    {
        ConcurrentBag<WindowInfo> windows = new();
        Process[] processes = Process.GetProcessesByName(processName);
        HashSet<int> processIds = new(processes.Select(p => p.Id));

        EnumWindowsData data = new() { ProcessIds = processIds, Windows = windows };
        GCHandle gch = GCHandle.Alloc(data);

        try
        {
            EnumWindows(EnumWindowsCallback, (IntPtr)gch);
        }
        finally
        {
            gch.Free();
        }

        return windows.ToList();
    }

    private static bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
    {
        GCHandle gch = (GCHandle)lParam;
        EnumWindowsData data = (EnumWindowsData)gch.Target!;

        _ = GetWindowThreadProcessId(hWnd, out uint processId);

        if (data.ProcessIds.Contains((int)processId))
        {
            StringBuilder sb = new(256);
            _ = GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            data.Windows.Add(new WindowInfo(hWnd, title));
        }

        return true;
    }

    public static List<WindowInfo> GetVisibleRobloxWindows() =>
        GetWindowsByProcessName("RobloxPlayerBeta")
            .Where(w => w.IsVisible && w.Title == "Roblox")
            .ToList();

    public static List<WindowInfo> GetHiddenRobloxWindows() =>
        GetWindowsByProcessName("RobloxPlayerBeta")
            .Where(w => !w.IsVisible && w.Title == "Roblox")
            .ToList();

    public static List<WindowInfo> GetAllRobloxWindows() =>
        GetWindowsByProcessName("RobloxPlayerBeta")
            .Where(w => w.Title == "Roblox")
            .ToList();

    public static WindowInfo? GetActiveWindow()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return null;

        StringBuilder sb = new(256);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        string title = sb.ToString();

        return new WindowInfo(hWnd, title);
    }
}
