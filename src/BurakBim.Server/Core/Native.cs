using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BurakBim.Server.Core;

/// <summary>1.3: per-monitor DPI awareness so screenshots and clicks use the same physical pixels on scaled displays.</summary>
public static class NativeDpi
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    public static void MakeProcessDpiAware()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { } // PER_MONITOR_AWARE_V2
        try { SetProcessDPIAware(); } catch { }
    }
}

/// <summary>Win32 helpers: windows, screen capture (PrintWindow / BitBlt), and input (SendInput).</summary>
public static class Win32
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int MessageBoxW(IntPtr h, string text, string caption, uint type);

    public const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

    public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
    public static string Class(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }

    /// <summary>All visible top-level windows of a process (main window first, then dialogs).</summary>
    public static List<IntPtr> WindowsOf(int pid)
    {
        var list = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            GetWindowThreadProcessId(h, out var p);
            if (p == pid && IsWindowVisible(h) && GetWindowRect(h, out var r) && r.Width > 40 && r.Height > 40) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>Bring a window to the foreground reliably (works around the foreground lock).</summary>
    public static void Focus(IntPtr h)
    {
        if (h == IntPtr.Zero) return;
        if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
        var fg = GetForegroundWindow();
        uint fgThread = GetWindowThreadProcessId(fg, out _);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        try { BringWindowToTop(h); SetForegroundWindow(h); }
        finally { if (attached) AttachThreadInput(me, fgThread, false); }
        Thread.Sleep(150);
    }

    // ------------------------------------------------------------------ SendInput
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION u; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern short VkKeyScan(char c);

    const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    const uint MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004, MOUSEEVENTF_RIGHTDOWN = 0x0008,
        MOUSEEVENTF_RIGHTUP = 0x0010, MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040, MOUSEEVENTF_WHEEL = 0x0800,
        MOUSEEVENTF_HWHEEL = 0x01000, MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000;
    const uint KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004, KEYEVENTF_EXTENDEDKEY = 0x0001;

    static void Send(params INPUT[] inputs)
    {
        if (inputs.Length == 0) return;
        var n = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (n != inputs.Length)
            throw new ToolException("Windows blocked the simulated input (another program may be elevated/administrator, or the screen is locked). Run the target app without admin rights, or ask the user to do this step.");
    }

    static INPUT Mouse(uint flags, int dx = 0, int dy = 0, uint data = 0) =>
        new() { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = flags, mouseData = data } } };

    public static void MoveTo(int x, int y)
    {
        // absolute coordinates normalised to the whole virtual desktop (all monitors)
        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN), vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN)), vh = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));
        int nx = (int)Math.Round((x - vx) * 65535.0 / Math.Max(1, vw - 1));
        int ny = (int)Math.Round((y - vy) * 65535.0 / Math.Max(1, vh - 1));
        Send(Mouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, nx, ny));
        SetCursorPos(x, y); // exact landing pixel
        Thread.Sleep(30);
    }

    public static void Click(int x, int y, string button = "left", int count = 1)
    {
        MoveTo(x, y);
        uint down = button switch { "right" => MOUSEEVENTF_RIGHTDOWN, "middle" => MOUSEEVENTF_MIDDLEDOWN, _ => MOUSEEVENTF_LEFTDOWN };
        uint up = button switch { "right" => MOUSEEVENTF_RIGHTUP, "middle" => MOUSEEVENTF_MIDDLEUP, _ => MOUSEEVENTF_LEFTUP };
        for (int i = 0; i < Math.Max(1, count); i++)
        {
            Send(Mouse(down), Mouse(up));
            Thread.Sleep(45);
        }
    }

    public static void MouseDown(string button = "left") => Send(Mouse(button == "right" ? MOUSEEVENTF_RIGHTDOWN : button == "middle" ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_LEFTDOWN));
    public static void MouseUp(string button = "left") => Send(Mouse(button == "right" ? MOUSEEVENTF_RIGHTUP : button == "middle" ? MOUSEEVENTF_MIDDLEUP : MOUSEEVENTF_LEFTUP));

    public static void Drag(int x1, int y1, int x2, int y2, string button = "left", int steps = 20)
    {
        MoveTo(x1, y1);
        MouseDown(button);
        Thread.Sleep(60);
        for (int i = 1; i <= steps; i++)
        {
            MoveTo(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps);
            Thread.Sleep(10);
        }
        Thread.Sleep(60);
        MouseUp(button);
    }

    public static void Scroll(int x, int y, int clicks, bool horizontal = false)
    {
        MoveTo(x, y);
        // positive = up / right
        Send(Mouse(horizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)(clicks * 120))));
    }

    /// <summary>Type text exactly (Unicode, independent of keyboard layout).</summary>
    public static void TypeText(string text, int delayMs = 5)
    {
        foreach (var ch in text)
        {
            if (ch == '\n') { Key("enter"); continue; }
            if (ch == '\r') continue;
            if (ch == '\t') { Key("tab"); continue; }
            Send(new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE } } },
                 new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } } });
            if (delayMs > 0) Thread.Sleep(delayMs);
        }
    }

    static readonly Dictionary<string, ushort> Vk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = 0x0D, ["return"] = 0x0D, ["esc"] = 0x1B, ["escape"] = 0x1B, ["tab"] = 0x09, ["space"] = 0x20, ["backspace"] = 0x08,
        ["delete"] = 0x2E, ["del"] = 0x2E, ["insert"] = 0x2D, ["home"] = 0x24, ["end"] = 0x23, ["pageup"] = 0x21, ["pagedown"] = 0x22,
        ["left"] = 0x25, ["up"] = 0x26, ["right"] = 0x27, ["down"] = 0x28, ["ctrl"] = 0x11, ["control"] = 0x11, ["shift"] = 0x10,
        ["alt"] = 0x12, ["win"] = 0x5B, ["windows"] = 0x5B, ["menu"] = 0x5D, ["apps"] = 0x5D, ["capslock"] = 0x14, ["printscreen"] = 0x2C,
        ["f1"] = 0x70, ["f2"] = 0x71, ["f3"] = 0x72, ["f4"] = 0x73, ["f5"] = 0x74, ["f6"] = 0x75, ["f7"] = 0x76, ["f8"] = 0x77,
        ["f9"] = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B, ["plus"] = 0xBB, ["minus"] = 0xBD, ["comma"] = 0xBC, ["period"] = 0xBE,
    };
    static readonly HashSet<ushort> Extended = new() { 0x2E, 0x2D, 0x24, 0x23, 0x21, 0x22, 0x25, 0x26, 0x27, 0x28, 0x5B, 0x5D };

    static ushort ToVk(string k)
    {
        if (Vk.TryGetValue(k, out var v)) return v;
        if (k.Length == 1)
        {
            var c = char.ToUpperInvariant(k[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return c;
            var scan = VkKeyScan(k[0]);
            if (scan != -1) return (ushort)(scan & 0xFF);
        }
        throw new ToolException($"Unknown key '{k}'. Use names like enter, esc, tab, f2, ctrl, shift, alt, win, up, delete, or single characters; combine with '+': ctrl+s, ctrl+shift+z, alt+f4.");
    }

    static INPUT KeyInput(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (Extended.Contains(vk) ? KEYEVENTF_EXTENDEDKEY : 0) } }
    };

    /// <summary>Press a key or chord: "enter", "ctrl+s", "ctrl+shift+esc", "alt+f4".</summary>
    public static void Key(string chord, int repeat = 1)
    {
        var parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ToVk).ToList();
        if (chord.EndsWith("++")) parts.Add(0xBB);
        for (int r = 0; r < Math.Max(1, repeat); r++)
        {
            var seq = new List<INPUT>();
            foreach (var vk in parts) seq.Add(KeyInput(vk, false));
            for (int i = parts.Count - 1; i >= 0; i--) seq.Add(KeyInput(parts[i], true));
            Send(seq.ToArray());
            Thread.Sleep(40);
        }
    }

    // ------------------------------------------------------------------ capture
    public static (int x, int y, int w, int h) VirtualScreen() =>
        (GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN), GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));
}
