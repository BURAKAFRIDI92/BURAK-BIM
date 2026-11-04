using System.Diagnostics;
using BurakBim.Server.Core;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>
/// 1.3 COMPUTER CONTROL - the last-resort layer for commands that no API, ribbon button or command line can reach
/// (e.g. dialogs of Bluebeam, PlanSwift, SAP2000/ETABS forms, license screens, third-party add-ins).
/// Works with ANY AI agent (Claude, ChatGPT/Codex, Antigravity, Cursor, VS Code ...) because it is part of the MCP server.
/// The user must approve first (computer_request_control shows a dialog on their screen). Every action is audited.
/// Coordinates are physical screen pixels (the process is DPI-aware), the same pixels as in capture/computer_screenshot.
/// </summary>
public static class ComputerTools
{
    static void Add(ToolRegistry r, string name, string title, string desc, bool ro, Dictionary<string, object> schema, Func<Dictionary<string, object>, ToolContext, object?> h)
        => r.Add(new Tool { Name = name, Title = title, Toolset = "computer", Description = desc, ReadOnly = ro, InputSchema = schema, Handler = h, OpenWorld = true });

    public static void Register(ToolRegistry r)
    {
        var shotP = O("screenshot", Bool("Return a screenshot after the action (default true)"));
        var appP = O("app", Str("Bring this application to the front first (revit, autocad, sap2000, etabs, bluebeam, planswift, ... or a process name)"));

        Add(r, "computer_request_control", "Ask the user for permission to control the computer",
            "Use ONLY when no BURAK BIM tool/API/ribbon/command can do the task. Shows a Yes/No dialog on the user's screen explaining why; " +
            "mouse/keyboard tools work only after the user clicks Yes. Permission lasts 'minutes' (default 15). Tell the user in the chat what you are about to do.",
            false, Obj(R("reason", Str("One or two sentences the user will read: what you will do and why the API cannot")), O("minutes", Int("How long (1-120, default 15)"))),
            (a, ctx) => Consent.Request(Json.Str(a, "reason") ?? "", (int)Json.Int(a, "minutes", 15), ctx));

        Add(r, "computer_release_control", "Give control back to the user",
            "End computer control now (always call it when the task is finished).", false, Obj(),
            (a, ctx) => { var n = Consent.Actions; Consent.Revoke("released by the agent"); return Json.O("released", true, "actions", n); });

        Add(r, "computer_status", "Computer control status",
            "Whether computer control is granted, until when, and how many actions were done.", true, Obj(),
            (a, ctx) => Consent.Status(Consent.Active ? "granted" : "not granted"));

        Add(r, "computer_screenshot", "Screenshot for computer control",
            "See the screen (all monitors, one monitor, or one app window) with a labelled SCREEN-coordinate grid, to decide exactly where to click. No permission needed (read-only).",
            true, Obj(O("app", Str("Only this application's window")), O("monitor", Int("Monitor number 1, 2, ...")), O("grid", Bool("Coordinate grid (default true)")),
                O("maxWidth", Int("Downscale width (default 1568)"))),
            (a, ctx) =>
            {
                var c = new Dictionary<string, object>(a);
                if (Json.Get(c, "grid") == null) c["grid"] = true;
                if (Json.Get(c, "app") == null && Json.Get(c, "monitor") == null) c["fullScreen"] = true;
                return CaptureTools.Capture(c);
            });

        Add(r, "computer_windows", "List open windows",
            "Every visible top-level window: process, title and screen rectangle - to find dialogs and target clicks.", true,
            Obj(O("app", Str("Only windows of this application"))),
            (a, ctx) =>
            {
#if WINDOWS
                var filter = Json.Str(a, "app");
                var procs = filter == null ? Process.GetProcesses() : new[] { CaptureTools.FindProcess(filter) };
                var list = new List<object>();
                foreach (var p in procs)
                {
                    List<IntPtr> wins; try { wins = Win32.WindowsOf(p.Id); } catch { continue; }
                    foreach (var h in wins)
                    {
                        var t = Win32.Title(h); if (t.Length == 0) continue;
                        Win32.GetWindowRect(h, out var rc);
                        list.Add(Json.O("process", p.ProcessName, "pid", p.Id, "title", t, "class", Win32.Class(h), "rect", new List<object> { rc.Left, rc.Top, rc.Width, rc.Height }));
                    }
                }
                return Json.O("windows", list, "foreground", Win32.Title(Win32.GetForegroundWindow()));
#else
                throw new ToolException("Windows only.");
#endif
            });

        Add(r, "computer_focus", "Bring an application or dialog to the front",
            "Activate an application's main window, or one of its dialogs by title text.", false,
            Obj(R("app", Str("Application or process name")), O("window", Str("Dialog/window title contains"))),
            (a, ctx) =>
            {
                Consent.Require();
                var h = Target(a);
                return After(a, Json.O("focused", Win32.Title(h)), Json.Str(a, "app"));
            });

        Add(r, "computer_click", "Click at a screen position",
            "Mouse click at SCREEN pixel coordinates (read them from computer_screenshot/capture grid or caption mapping). button: left/right/middle; count 2 = double-click. Optional modifier keys held during the click.",
            false, Obj(R("x", Int("Screen X")), R("y", Int("Screen Y")), O("button", Enum("Button", "left", "right", "middle")), O("count", Int("1 = click, 2 = double-click")),
                O("hold", Str("Keys held during the click, e.g. 'ctrl' or 'shift'")), appP, shotP),
            (a, ctx) =>
            {
                Consent.Require();
                if (Json.Str(a, "app") is { } app) Win32.Focus(CaptureTools.FindProcess(app).MainWindowHandle);
                int x = (int)Json.Int(a, "x"), y = (int)Json.Int(a, "y");
                var hold = Json.Str(a, "hold");
                if (!string.IsNullOrEmpty(hold)) HoldKeys(hold, true);
                try { Win32.Click(x, y, Json.Str(a, "button") ?? "left", (int)Json.Int(a, "count", 1)); }
                finally { if (!string.IsNullOrEmpty(hold)) HoldKeys(hold, false); }
                return After(a, Json.O("clicked", new List<object> { x, y }, "button", Json.Str(a, "button") ?? "left"), Json.Str(a, "app"));
            });

        Add(r, "computer_move", "Move the mouse (hover)", "Move the mouse pointer to screen coordinates (to show tooltips / hover menus).", false,
            Obj(R("x", Int("Screen X")), R("y", Int("Screen Y")), shotP),
            (a, ctx) => { Consent.Require(); Win32.MoveTo((int)Json.Int(a, "x"), (int)Json.Int(a, "y")); return After(a, Json.O("moved", true), null); });

        Add(r, "computer_drag", "Drag with the mouse",
            "Press at 'from', move to 'to', release - for window/selection boxes, sliders, grips, drawing in canvases (e.g. PlanSwift/Bluebeam takeoffs, Revit grips).",
            false, Obj(R("from", Arr("[x, y] screen pixels", Json.O("type", "integer"))), R("to", Arr("[x, y] screen pixels", Json.O("type", "integer"))),
                O("button", Enum("Button", "left", "right", "middle")), O("steps", Int("Intermediate moves (default 20)")), appP, shotP),
            (a, ctx) =>
            {
                Consent.Require();
                if (Json.Str(a, "app") is { } app) Win32.Focus(CaptureTools.FindProcess(app).MainWindowHandle);
                var f = Json.IntList(a, "from"); var t = Json.IntList(a, "to");
                if (f.Count < 2 || t.Count < 2) throw new ToolException("from and to must be [x, y].");
                Win32.Drag((int)f[0], (int)f[1], (int)t[0], (int)t[1], Json.Str(a, "button") ?? "left", (int)Json.Int(a, "steps", 20));
                return After(a, Json.O("dragged", true), Json.Str(a, "app"));
            });

        Add(r, "computer_scroll", "Scroll the mouse wheel",
            "Scroll at a screen position: clicks > 0 scrolls up (zoom in in most CAD views), < 0 down. horizontal=true scrolls sideways.",
            false, Obj(R("x", Int("Screen X")), R("y", Int("Screen Y")), R("clicks", Int("Wheel notches (+ up / - down)")), O("horizontal", Bool("Horizontal scroll")), shotP),
            (a, ctx) =>
            {
                Consent.Require();
                Win32.Scroll((int)Json.Int(a, "x"), (int)Json.Int(a, "y"), (int)Json.Int(a, "clicks", -3), Json.Bool(a, "horizontal"));
                return After(a, Json.O("scrolled", Json.Int(a, "clicks", -3)), null);
            });

        Add(r, "computer_type", "Type text",
            "Type text exactly as given (any language/characters, independent of keyboard layout) into the focused control. Optionally click a position first and press Enter after.",
            false, Obj(R("text", Str("Text to type (\\n = Enter)")), O("x", Int("Click here first (screen X)")), O("y", Int("Click here first (screen Y)")),
                O("clear", Bool("Select all (Ctrl+A) and replace existing text")), O("enter", Bool("Press Enter after typing")), appP, shotP),
            (a, ctx) =>
            {
                Consent.Require();
                if (Json.Str(a, "app") is { } app) Win32.Focus(CaptureTools.FindProcess(app).MainWindowHandle);
                if (Json.Get(a, "x") != null && Json.Get(a, "y") != null) Win32.Click((int)Json.Int(a, "x"), (int)Json.Int(a, "y"));
                if (Json.Bool(a, "clear")) { Win32.Key("ctrl+a"); Win32.Key("delete"); }
                var text = Json.Str(a, "text") ?? "";
                Win32.TypeText(text);
                if (Json.Bool(a, "enter")) Win32.Key("enter");
                return After(a, Json.O("typed", text.Length), Json.Str(a, "app"));
            });

        Add(r, "computer_key", "Press keys / shortcuts",
            "Press a key or shortcut: 'enter', 'esc', 'tab', 'f2', 'ctrl+s', 'ctrl+shift+s', 'alt+f4', 'delete', arrows... keys can be a list for a sequence (e.g. Revit 'w','a' for Wall). repeat presses the same key several times.",
            false, Obj(R("keys", Str("Key or shortcut, e.g. 'enter', 'ctrl+s'. Several in order separated by spaces: 'w a' or 'esc esc'")), O("repeat", Int("Press count (default 1)")), appP, shotP),
            (a, ctx) =>
            {
                Consent.Require();
                if (Json.Str(a, "app") is { } app) Win32.Focus(CaptureTools.FindProcess(app).MainWindowHandle);
                var keys = Json.Get(a, "keys") is List<object> l ? l.Select(x => x?.ToString() ?? "").ToList()
                    : (Json.Str(a, "keys") ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                foreach (var k in keys.Where(k => k.Length > 0)) Win32.Key(k, (int)Json.Int(a, "repeat", 1));
                return After(a, Json.O("pressed", keys), Json.Str(a, "app"));
            });

        Add(r, "computer_open", "Open a file, folder, app or URL",
            "Open something with Windows (like double-clicking it): a drawing/model/PDF path, a folder, an application exe, or a URL.",
            false, Obj(R("target", Str("Path, exe or URL")), O("arguments", Str("Command-line arguments")), O("waitSeconds", Int("Wait before the screenshot (default 3)")), shotP),
            (a, ctx) =>
            {
                Consent.Require();
                var t = Json.Str(a, "target") ?? throw new ToolException("target required");
                var psi = new ProcessStartInfo(t) { UseShellExecute = true };
                if (Json.Str(a, "arguments") is { } args) psi.Arguments = args;
                var p = Process.Start(psi);
                Thread.Sleep((int)Json.Int(a, "waitSeconds", 3) * 1000);
                return After(a, Json.O("opened", t, "pid", p?.Id), null);
            });

        Add(r, "computer_wait", "Wait", "Wait a few seconds for the application to finish (loading, regenerating, analysis running) and return a fresh screenshot.", true,
            Obj(O("seconds", Num("Seconds (default 2, max 120)")), O("app", Str("Screenshot of this application instead of the whole screen"))),
            (a, ctx) =>
            {
                Thread.Sleep((int)(Math.Clamp(Json.Num(a, "seconds", 2), 0.1, 120) * 1000));
                var c = Json.O("grid", true);
                if (Json.Str(a, "app") is { } app) c["app"] = app; else c["fullScreen"] = true;
                return CaptureTools.Capture(c);
            });
    }

    static IntPtr Target(Dictionary<string, object> a)
    {
        var p = CaptureTools.FindProcess(Json.Str(a, "app"));
        var h = p.MainWindowHandle;
        var title = Json.Str(a, "window");
        if (!string.IsNullOrEmpty(title))
        {
            var hit = Win32.WindowsOf(p.Id).FirstOrDefault(w => Win32.Title(w).Contains(title, StringComparison.OrdinalIgnoreCase));
            if (hit != IntPtr.Zero) h = hit;
            else throw new ToolException($"No window titled '{title}'. Use computer_windows.");
        }
        Win32.Focus(h);
        return h;
    }

    static void HoldKeys(string keys, bool down)
    {
        // reuse Key() for press/release by sending modifier-only chords is not possible; use keybd_event semantics through Win32.Key on release
        foreach (var k in keys.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            ModifierState(k, down);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    static void ModifierState(string k, bool down)
    {
        byte vk = k.ToLowerInvariant() switch { "ctrl" or "control" => (byte)0x11, "shift" => (byte)0x10, "alt" => (byte)0x12, "win" => (byte)0x5B, _ => (byte)0 };
        if (vk == 0) throw new ToolException("hold supports ctrl, shift, alt, win.");
        keybd_event(vk, 0, down ? 0u : 0x0002u, UIntPtr.Zero);
    }

    /// <summary>After an action: short pause, then a screenshot of the app (or screen) so the agent sees the effect.</summary>
    static object After(Dictionary<string, object> a, Dictionary<string, object> info, string? app)
    {
        if (!Json.Bool(a, "screenshot", true)) return info;
        Thread.Sleep(450);
        try
        {
            var c = Json.O("grid", true, "maxWidth", 1400);
            if (app != null) c["app"] = app; else c["fullScreen"] = true;
            var img = CaptureTools.Capture(c) as ImageResult;
            if (img == null) return info;
            return new ImageResult { Base64 = img.Base64, MimeType = img.MimeType, Caption = Json.Serialize(info) + " | " + img.Caption };
        }
        catch { return info; }
    }
}
