using System.Diagnostics;
using System.Runtime.InteropServices;
using BurakBim.Server.Core;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;
#if WINDOWS
using System.Windows.Automation;
#endif

namespace BurakBim.Server.Tools;

/// <summary>
/// Windows UI Automation: see and operate ANY window, dialog, ribbon, tree or grid of ANY application
/// (Revit/AutoCAD/Navisworks dialogs, ReCap Pro, Inventor, Robot...). Last-resort universal control + screenshots.
/// </summary>
public static class UiTools
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    static readonly Dictionary<string, string> AppProcess = new(StringComparer.OrdinalIgnoreCase)
    {
        ["revit"] = "Revit", ["autocad"] = "acad", ["civil3d"] = "acad", ["navisworks"] = "Roamer",
        ["inventor"] = "Inventor", ["recap"] = "ReCap", ["robot"] = "robot"
    };

    static Process FindProcess(Dictionary<string, object> a)
    {
        var pid = Json.Int(a, "pid", 0);
        if (pid > 0) return Process.GetProcessById((int)pid);
        var app = Json.Str(a, "app") ?? throw new ToolException("Give app (revit, autocad, navisworks, inventor, recap, robot or a process name) or pid.");
        var name = AppProcess.TryGetValue(app, out var n) ? n : app.Replace(".exe", "");
        var procs = Process.GetProcessesByName(name).Where(p => p.MainWindowHandle != IntPtr.Zero).OrderByDescending(p => p.StartTime).ToList();
        return procs.FirstOrDefault() ?? throw new ToolException($"No window of '{app}' is open.");
    }

    public static void Register(ToolRegistry r)
    {
        var appP = O("app", Str("revit | autocad | navisworks | inventor | recap | robot | any process name"));
        var pidP = O("pid", Int("Process id (instead of app)"));

        r.Add(new Tool
        {
            Name = "screenshot", Title = "Screenshot of an application", Toolset = "ui", ReadOnly = true,
            Description = "Capture the main window of an application (or the whole screen) as an image so you can SEE the current state: model view, dialogs, results.",
            InputSchema = Obj(appP, pidP, O("fullScreen", Bool("Capture the primary screen")), O("maxWidth", Int("Downscale to this width (default 1600)"))),
            Handler = (a, ctx) => Capture(a)
        });
#if WINDOWS
        r.Add(new Tool
        {
            Name = "ui_windows", Title = "List application windows", Toolset = "ui", ReadOnly = true,
            Description = "Top-level windows and open dialogs of an application (title, class, automation id).",
            InputSchema = Obj(appP, pidP),
            Handler = (a, ctx) =>
            {
                Guard();
                var p = FindProcess(a);
                var cond = new PropertyCondition(AutomationElement.ProcessIdProperty, p.Id);
                var wins = AutomationElement.RootElement.FindAll(TreeScope.Children, cond);
                var list = new List<object>();
                foreach (AutomationElement w in wins) list.Add(Describe(w));
                return Json.O("process", p.ProcessName, "pid", p.Id, "windows", list);
            }
        });
        r.Add(new Tool
        {
            Name = "ui_tree", Title = "Inspect UI controls", Toolset = "ui", ReadOnly = true,
            Description = "UI Automation tree of an application window/dialog: buttons, tabs, menus, text boxes, lists, tree items with names and automation ids. Use search to find a control, then ui_click / ui_type.",
            InputSchema = Obj(appP, pidP, O("window", Str("Window/dialog title contains (default: main window)")), O("search", Str("Only controls whose name/id contains this")),
                O("controlType", Str("Button, Edit, TabItem, MenuItem, ListItem, TreeItem, ComboBox, CheckBox ...")), O("depth", Int("Max depth (default 6)")), O("limit", Int("Max controls (default 300)"))),
            Handler = (a, ctx) =>
            {
                Guard();
                var root = Window(a);
                var list = new List<object>();
                Walk(root, 0, (int)Json.Int(a, "depth", 6), Json.Str(a, "search"), Json.Str(a, "controlType"), list, (int)Json.Int(a, "limit", 300), "");
                return Json.O("window", root.Current.Name, "controls", list);
            }
        });
        r.Add(new Tool
        {
            Name = "ui_click", Title = "Click a UI control", Toolset = "ui", ReadOnly = false,
            Description = "Find a control by name or automation id (and optional type) in an application window/dialog and invoke it: button press, tab/list/tree item select, checkbox toggle, menu expand.",
            InputSchema = Obj(appP, pidP, O("window", Str("Window/dialog title contains")), R("name", Str("Control name or automation id (exact first, then contains)")),
                O("controlType", Str("e.g. Button, TabItem, MenuItem")), O("index", Int("If several match, which one (0-based)"))),
            Handler = (a, ctx) =>
            {
                Guard();
                var el = Find(a);
                if (el.TryGetCurrentPattern(InvokePattern.Pattern, out var ip)) ((InvokePattern)ip).Invoke();
                else if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var sp)) ((SelectionItemPattern)sp).Select();
                else if (el.TryGetCurrentPattern(TogglePattern.Pattern, out var tp)) ((TogglePattern)tp).Toggle();
                else if (el.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var ep)) ((ExpandCollapsePattern)ep).Expand();
                else
                {
                    var rect = el.Current.BoundingRectangle;
                    ClickAt((int)(rect.Left + rect.Width / 2), (int)(rect.Top + rect.Height / 2));
                }
                return Json.O("clicked", el.Current.Name, "type", el.Current.ControlType.ProgrammaticName);
            }
        });
        r.Add(new Tool
        {
            Name = "ui_type", Title = "Type into a UI control", Toolset = "ui", ReadOnly = false,
            Description = "Set the text of an edit box / combo box found by name or automation id (ValuePattern), or type into the focused control when name is omitted. Use ui_keys for Enter/Tab/Esc.",
            InputSchema = Obj(appP, pidP, O("window", Str("Window/dialog title contains")), O("name", Str("Control name/automation id")), R("text", Str("Text"))),
            Handler = (a, ctx) =>
            {
                Guard();
                var text = Json.Str(a, "text") ?? "";
                if (string.IsNullOrEmpty(Json.Str(a, "name")))
                {
                    Focus(FindProcess(a));
                    System.Windows.Forms.SendKeys.SendWait(EscapeKeys(text));
                    return Json.O("typed", text.Length);
                }
                var el = Find(a);
                if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp)) ((ValuePattern)vp).SetValue(text);
                else { el.SetFocus(); System.Windows.Forms.SendKeys.SendWait(EscapeKeys(text)); }
                return Json.O("set", el.Current.Name, "text", text);
            }
        });
        r.Add(new Tool
        {
            Name = "ui_keys", Title = "Send keystrokes", Toolset = "ui", ReadOnly = false,
            Description = "Bring the application to the front and send keys (SendKeys syntax): {ENTER} {ESC} {TAB} {F2} ^s (Ctrl+S) %f (Alt+F) +{TAB}. Revit/AutoCAD keyboard shortcuts work too (e.g. 'WA' for Wall in Revit, then {ESC}{ESC}).",
            InputSchema = Obj(appP, pidP, R("keys", Str("SendKeys string"))),
            Handler = (a, ctx) =>
            {
                Guard();
                Focus(FindProcess(a));
                System.Windows.Forms.SendKeys.SendWait(Json.Str(a, "keys") ?? "");
                return Json.O("sent", Json.Str(a, "keys"));
            }
        });
        r.Add(new Tool
        {
            Name = "ui_click_at", Title = "Click at screen position", Toolset = "ui", ReadOnly = false,
            Description = "Click at absolute screen coordinates (from screenshot / ui_tree bounds) - for canvases and controls without automation support (ReCap, 3D views). button: left|right|double.",
            InputSchema = Obj(R("x", Int("Screen X")), R("y", Int("Screen Y")), O("button", Enum("Button", "left", "right", "double")), appP, pidP),
            Handler = (a, ctx) =>
            {
                Guard();
                if (Json.Get(a, "app") != null || Json.Get(a, "pid") != null) Focus(FindProcess(a));
                ClickAt((int)Json.Int(a, "x"), (int)Json.Int(a, "y"), Json.Str(a, "button", "left")!);
                return Json.O("clicked", new List<object> { Json.Int(a, "x"), Json.Int(a, "y") });
            }
        });
#endif
    }

    static void Guard()
    {
        if (!Config.AllowUi) throw new ToolException("UI automation is disabled (allowUiAutomation=false in burakbim.json).");
    }

    static void Focus(Process p)
    {
        ShowWindow(p.MainWindowHandle, 9); // SW_RESTORE
        SetForegroundWindow(p.MainWindowHandle);
        Thread.Sleep(250);
    }

    static void ClickAt(int x, int y, string button = "left")
    {
        SetCursorPos(x, y);
        Thread.Sleep(50);
        uint down = button == "right" ? 0x0008u : 0x0002u, up = button == "right" ? 0x0010u : 0x0004u;
        mouse_event(down, 0, 0, 0, UIntPtr.Zero); mouse_event(up, 0, 0, 0, UIntPtr.Zero);
        if (button == "double") { Thread.Sleep(60); mouse_event(down, 0, 0, 0, UIntPtr.Zero); mouse_event(up, 0, 0, 0, UIntPtr.Zero); }
    }

    static string EscapeKeys(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in s) sb.Append("+^%~(){}[]".IndexOf(c) >= 0 ? "{" + c + "}" : c.ToString());
        return sb.ToString();
    }

    static object Capture(Dictionary<string, object> a)
    {
#if WINDOWS
        int x, y, w, h;
        if (Json.Bool(a, "fullScreen") || (Json.Get(a, "app") == null && Json.Get(a, "pid") == null))
        {
            var b = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            x = b.X; y = b.Y; w = b.Width; h = b.Height;
        }
        else
        {
            var p = FindProcess(a);
            Focus(p);
            GetWindowRect(p.MainWindowHandle, out var r);
            x = r.Left; y = r.Top; w = r.Right - r.Left; h = r.Bottom - r.Top;
        }
        using var bmp = new System.Drawing.Bitmap(Math.Max(1, w), Math.Max(1, h));
        using (var g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(x, y, 0, 0, bmp.Size);
        int maxW = (int)Json.Int(a, "maxWidth", 1600);
        System.Drawing.Bitmap outBmp = bmp;
        if (w > maxW)
        {
            outBmp = new System.Drawing.Bitmap(bmp, new System.Drawing.Size(maxW, (int)(h * (double)maxW / w)));
        }
        using var ms = new MemoryStream();
        outBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        if (!ReferenceEquals(outBmp, bmp)) outBmp.Dispose();
        var file = Path.Combine(BurakPaths.Sub("captures"), "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
        File.WriteAllBytes(file, ms.ToArray());
        return new ImageResult { Base64 = Convert.ToBase64String(ms.ToArray()), Caption = $"Screen region {x},{y} {w}x{h} (saved {file})" };
#else
        throw new ToolException("Screenshots need the Windows build of BURAK BIM.");
#endif
    }

#if WINDOWS
    static AutomationElement Window(Dictionary<string, object> a)
    {
        var p = FindProcess(a);
        var title = Json.Str(a, "window");
        if (string.IsNullOrEmpty(title)) return AutomationElement.FromHandle(p.MainWindowHandle);
        var cond = new PropertyCondition(AutomationElement.ProcessIdProperty, p.Id);
        // dialogs can be top-level or children of the main window
        foreach (AutomationElement w in AutomationElement.RootElement.FindAll(TreeScope.Children, cond))
            if (w.Current.Name.Contains(title, StringComparison.OrdinalIgnoreCase)) return w;
        var main = AutomationElement.FromHandle(p.MainWindowHandle);
        foreach (AutomationElement w in main.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)))
            if (w.Current.Name.Contains(title, StringComparison.OrdinalIgnoreCase)) return w;
        throw new ToolException($"No window/dialog titled '{title}'. Use ui_windows.");
    }

    static Dictionary<string, object> Describe(AutomationElement e)
    {
        var c = e.Current;
        var r = c.BoundingRectangle;
        return Json.O("name", c.Name, "id", c.AutomationId, "type", c.ControlType.ProgrammaticName.Replace("ControlType.", ""), "class", c.ClassName,
            "enabled", c.IsEnabled, "bounds", r.IsEmpty ? null : new List<object> { (int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height });
    }

    static void Walk(AutomationElement el, int depth, int max, string? search, string? type, List<object> list, int limit, string path)
    {
        if (list.Count >= limit || depth > max) return;
        var walker = TreeWalker.ControlViewWalker;
        var child = walker.GetFirstChild(el);
        while (child != null && list.Count < limit)
        {
            Dictionary<string, object>? d = null;
            try { d = Describe(child); } catch { }
            if (d != null)
            {
                var name = Json.Str(d, "name", "")!; var id = Json.Str(d, "id", "")!; var t = Json.Str(d, "type", "")!;
                bool match = (search == null || name.Contains(search, StringComparison.OrdinalIgnoreCase) || id.Contains(search, StringComparison.OrdinalIgnoreCase))
                             && (type == null || t.Equals(type, StringComparison.OrdinalIgnoreCase));
                var p = path.Length == 0 ? name : path + " > " + name;
                if (match && (name.Length > 0 || id.Length > 0)) { d["path"] = p; d["depth"] = depth; list.Add(d); }
                Walk(child, depth + 1, max, search, type, list, limit, p);
            }
            try { child = walker.GetNextSibling(child); } catch { break; }
        }
    }

    static AutomationElement Find(Dictionary<string, object> a)
    {
        var root = Window(a);
        var name = Json.Str(a, "name") ?? throw new ToolException("name required");
        var type = Json.Str(a, "controlType");
        var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Where(e => { try { return type == null || e.Current.ControlType.ProgrammaticName.EndsWith("." + type, StringComparison.OrdinalIgnoreCase); } catch { return false; } })
            .ToList();
        var exact = all.Where(e => { try { return e.Current.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || e.Current.AutomationId.Equals(name, StringComparison.OrdinalIgnoreCase); } catch { return false; } }).ToList();
        var hits = exact.Count > 0 ? exact : all.Where(e => { try { return e.Current.Name.Contains(name, StringComparison.OrdinalIgnoreCase); } catch { return false; } }).ToList();
        int idx = (int)Json.Int(a, "index", 0);
        if (hits.Count <= idx) throw new ToolException($"No control '{name}'{(type != null ? " of type " + type : "")} found. Use ui_tree with search.");
        return hits[idx];
    }
#endif
}
