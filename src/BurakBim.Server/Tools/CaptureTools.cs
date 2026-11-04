using System.Diagnostics;
using BurakBim.Server.Bridges;
using BurakBim.Server.Core;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>
/// 1.3 CAPTURE MODE - the AI agent SEES what is going on.
///  - revit_capture: renders any Revit view (plan, ceiling plan, section, elevation, 3D, drafting, sheet) through the
///    Revit API, independent of window position/overlap, and returns the image(s) plus view facts.
///  - capture: picture of ANY application window (Revit, AutoCAD, Civil 3D, Navisworks, SAP2000, ETABS, CSiBridge, SAFE,
///    Archicad, Bluebeam Revu, PlanSwift, Robot, Rhino, Excel ...), a dialog, a monitor or a screen region, with an
///    optional coordinate grid for precise computer control.
///  - capture_mode: when on (default), every change made through BURAK BIM comes back with a picture of the result.
/// </summary>
public static class CaptureTools
{
    public static readonly Dictionary<string, string[]> AppProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["revit"] = new[] { "Revit" },
        ["autocad"] = new[] { "acad" }, ["civil3d"] = new[] { "acad" }, ["civil 3d"] = new[] { "acad" }, ["acad"] = new[] { "acad" },
        ["navisworks"] = new[] { "Roamer" }, ["inventor"] = new[] { "Inventor" }, ["recap"] = new[] { "ReCap" }, ["robot"] = new[] { "robot" },
        ["sap2000"] = new[] { "SAP2000" }, ["etabs"] = new[] { "ETABS" }, ["csibridge"] = new[] { "CSiBridge" }, ["safe"] = new[] { "SAFE" },
        ["csi"] = new[] { "ETABS", "SAP2000", "CSiBridge", "SAFE" },
        ["archicad"] = new[] { "Archicad", "ARCHICAD" }, ["bluebeam"] = new[] { "Revu" }, ["revu"] = new[] { "Revu" },
        ["planswift"] = new[] { "PlanSwift", "PlanSwift11", "PlanSwift10" }, ["rhino"] = new[] { "Rhino" }, ["grasshopper"] = new[] { "Rhino" },
        ["sketchup"] = new[] { "SketchUp" }, ["tekla"] = new[] { "TeklaStructures" }, ["excel"] = new[] { "EXCEL" }, ["word"] = new[] { "WINWORD" },
        ["dynamo"] = new[] { "DynamoSandbox", "Revit" }, ["3dsmax"] = new[] { "3dsmax" }, ["infraworks"] = new[] { "InfraWorks" },
    };

    // toolset -> application for capture mode
    static readonly Dictionary<string, string> ToolsetApp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["revit"] = "revit", ["scan"] = "revit", ["inspector"] = "revit", ["dynamo"] = "revit",
        ["autocad"] = "autocad", ["navisworks"] = "navisworks", ["csi"] = "csi", ["archicad"] = "archicad",
        ["bluebeam"] = "bluebeam", ["planswift"] = "planswift", ["robot"] = "robot", ["inventor"] = "inventor", ["rhino"] = "rhino",
    };

    public static Process FindProcess(string? app, long pid = 0)
    {
        if (pid > 0) return Process.GetProcessById((int)pid);
        if (string.IsNullOrWhiteSpace(app)) throw new ToolException("Give app (revit, autocad, civil3d, navisworks, sap2000, etabs, csibridge, safe, archicad, bluebeam, planswift, robot, inventor, rhino ... or any process name) or pid.");
        var names = AppProcesses.TryGetValue(app.Trim(), out var n) ? n : new[] { Path.GetFileNameWithoutExtension(app.Trim()) };
        var procs = names.SelectMany(x => { try { return Process.GetProcessesByName(x); } catch { return Array.Empty<Process>(); } })
            .Where(p => { try { return p.MainWindowHandle != IntPtr.Zero; } catch { return false; } })
            .OrderByDescending(p => { try { return p.StartTime; } catch { return DateTime.MinValue; } }).ToList();
        if (procs.Count == 0)
        {
            // fuzzy: any process whose name or window title contains the text
            procs = Process.GetProcesses().Where(p =>
            {
                try { return p.MainWindowHandle != IntPtr.Zero && (p.ProcessName.Contains(app, StringComparison.OrdinalIgnoreCase) || p.MainWindowTitle.Contains(app, StringComparison.OrdinalIgnoreCase)); }
                catch { return false; }
            }).ToList();
        }
        return procs.FirstOrDefault() ?? throw new ToolException($"No open window of '{app}'. Start it (burak_launch) or check the name with capture fullScreen=true.");
    }

    public static string Mode => (Json.Str(Config.Data, "captureMode") ?? "changes").ToLowerInvariant();

    public static void Register(ToolRegistry r)
    {
        r.Add(new Tool
        {
            Name = "capture", Title = "Capture: see any application, dialog or screen", Toolset = "capture", ReadOnly = true,
            Description = "Take a picture so you can SEE the current state: the main window of an application (Revit, AutoCAD/Civil 3D, Navisworks, SAP2000, ETABS, CSiBridge, SAFE, Archicad, Bluebeam Revu, PlanSwift, Robot, Inventor, Rhino, Excel or any process), " +
                          "one of its dialogs (window=title text), a monitor, the whole desktop, or a region. Works even when the window is behind other windows. " +
                          "With grid=true a labelled screen-coordinate grid is drawn so computer_click lands exactly. For Revit model views prefer revit_capture (renders any view through the API).",
            InputSchema = Obj(O("app", Str("Application or process name (omit for the whole screen)")), O("pid", Int("Process id")), O("window", Str("Capture the window/dialog whose title contains this text")),
                O("fullScreen", Bool("Whole desktop (all monitors)")), O("monitor", Int("Monitor number 1, 2, ... (instead of the whole desktop)")),
                O("region", Arr("[x, y, width, height] in screen pixels", Json.O("type", "integer"))), O("grid", Bool("Overlay a labelled coordinate grid (for precise clicking)")),
                O("maxWidth", Int("Downscale to this width (default 1568)")), O("format", Enum("Image format", "jpeg", "png"))),
            Handler = (a, ctx) => Capture(a)
        });

        r.Add(new Tool
        {
            Name = "revit_capture", Title = "Revit capture: see any view", Toolset = "revit", ReadOnly = true, Priority = 90,
            Description = "Render Revit views to images and SEE them: the active view by default, or views by id/name (floor/ceiling plans, sections, elevations, 3D views, drafting views, legends, sheets). " +
                          "Returns each image with view facts (scale, detail level, level, element counts by category). Use it after modelling to check the result, and before modelling to understand the project. " +
                          "allOpenViews=true captures every open view tab. mode=window grabs the Revit window itself (dialogs, ribbon, properties palette).",
            InputSchema = Obj(O("view", Str("View id or name (default: active view). Sheets: sheet number or name")), O("views", Arr("Several view ids/names (max 8)")),
                O("allOpenViews", Bool("Capture every open view tab")), O("pixelSize", Int("Longest image side in pixels (default 1600, max 6000)")),
                O("format", Enum("Image format", "png", "jpg")), O("mode", Enum("view = render through the API (default); window = screenshot of the Revit window", "view", "window")), Target),
            Handler = (a, ctx) => RevitCapture(a, ctx)
        });
        r.Add(new Tool
        {
            Name = "revit_capture_views", Title = "Revit: views that can be captured", Toolset = "revit", ReadOnly = true,
            Description = "List printable graphical views and sheets (id, name, type) for revit_capture.",
            InputSchema = Obj(O("search", Str("Name filter")), O("limit", Int("Max (default 300)")), Target),
            Handler = (a, ctx) => BridgeClient.Call("revit", Json.Str(a, "target"), "revit.capture.views", a, ctx)
        });
        r.Add(new Tool
        {
            Name = "capture_mode", Title = "Capture mode on/off", Toolset = "capture", ReadOnly = false,
            Description = "Capture mode: when 'changes' (default) every tool that changes Revit, AutoCAD, Navisworks, SAP2000/ETABS, Archicad, Bluebeam, PlanSwift ... returns a picture of the result, so you can verify your work visually. 'off' saves context. Optional maxWidth for those pictures.",
            InputSchema = Obj(R("mode", Enum("Mode", "changes", "off")), O("maxWidth", Int("Width of automatic captures (default 1024)"))),
            Handler = (a, ctx) =>
            {
                var m = Json.Str(a, "mode") ?? "changes";
                Config.Set("captureMode", m == "off" ? "off" : "changes");
                if (Json.Get(a, "maxWidth") != null) Config.Set("captureMaxWidth", Json.Int(a, "maxWidth", 1024));
                return Json.O("captureMode", Mode, "maxWidth", Json.Int(Config.Data, "captureMaxWidth", 1024));
            }
        });
    }

    // ================================================================================ Revit view capture
    static object RevitCapture(Dictionary<string, object> a, ToolContext ctx)
    {
        if ((Json.Str(a, "mode") ?? "view") == "window")
            return Capture(Json.O("app", "revit", "maxWidth", Json.Int(a, "pixelSize", 1568)));
        var args = new Dictionary<string, object>(a); args.Remove("target"); args.Remove("mode");
        var res = BridgeClient.Call("revit", Json.Str(a, "target"), "revit.capture", args, ctx, 240000) as Dictionary<string, object>
                  ?? throw new ToolException("Revit returned no capture.");
        var rich = new RichResult();
        var facts = new List<object>();
        foreach (var img in Json.Arr(res, "images") ?? new List<object>())
        {
            if (img is not Dictionary<string, object> d) continue;
            var file = Json.Str(d, "file");
            facts.Add(Json.O("view", Json.Str(d, "view"), "viewId", Json.Get(d, "viewId"), "type", Json.Str(d, "type"), "file", file, "error", Json.Str(d, "error"), "facts", Json.Get(d, "facts")));
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) continue;
            rich.Images.Add(FileImage(file, (int)Json.Int(a, "pixelSize", 1600), $"Revit view '{Json.Str(d, "view")}' ({Json.Str(d, "type")})"));
        }
        rich.Data = Json.O("captured", rich.Images.Count, "views", facts, "folder", Json.Str(res, "folder"));
        if (rich.Images.Count == 0) throw new ToolException("No image was produced: " + Json.Serialize(facts));
        return rich;
    }

    /// <summary>Load an image file and return it (re-encoded/downscaled when large) as MCP image content.</summary>
    public static ImageResult FileImage(string file, int maxSide, string caption)
    {
#if WINDOWS
        using var src = new System.Drawing.Bitmap(file);
        return Encode(src, Math.Min(maxSide, 1600), file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && new FileInfo(file).Length < 900_000 ? "png" : "jpeg", caption + $" - saved {file}", false, 0, 0, 1.0);
#else
        var bytes = File.ReadAllBytes(file);
        return new ImageResult { Base64 = Convert.ToBase64String(bytes), MimeType = file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg", Caption = caption };
#endif
    }

    // ================================================================================ generic capture
    public static object Capture(Dictionary<string, object> a)
    {
#if WINDOWS
        int maxW = (int)Json.Int(a, "maxWidth", 1568);
        var fmt = Json.Str(a, "format") ?? "jpeg";
        bool grid = Json.Bool(a, "grid");
        var region = Json.IntList(a, "region");
        if (region.Count == 4)
        {
            using var bmp = Screen((int)region[0], (int)region[1], (int)region[2], (int)region[3]);
            return Encode(bmp, maxW, fmt, $"Screen region x={region[0]} y={region[1]} {region[2]}x{region[3]}", grid, (int)region[0], (int)region[1], 1.0);
        }
        var app = Json.Str(a, "app"); var pid = Json.Int(a, "pid", 0);
        if (Json.Bool(a, "fullScreen") || Json.Get(a, "monitor") != null || (app == null && pid == 0))
        {
            int x, y, w, h; string what;
            var mon = (int)Json.Int(a, "monitor", 0);
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (mon >= 1 && mon <= screens.Length)
            {
                var b = screens[mon - 1].Bounds; x = b.X; y = b.Y; w = b.Width; h = b.Height; what = $"Monitor {mon} of {screens.Length}";
            }
            else
            {
                var v = Win32.VirtualScreen(); x = v.x; y = v.y; w = v.w; h = v.h; what = $"Whole desktop ({screens.Length} monitor(s))";
            }
            using var bmp = Screen(x, y, w, h);
            return Encode(bmp, maxW, fmt, $"{what}: screen origin ({x},{y}), size {w}x{h}", grid, x, y, 1.0);
        }
        var p = FindProcess(app, pid);
        var hwnd = p.MainWindowHandle;
        var title = Json.Str(a, "window");
        if (!string.IsNullOrEmpty(title))
        {
            var hit = Win32.WindowsOf(p.Id).FirstOrDefault(h => Win32.Title(h).Contains(title, StringComparison.OrdinalIgnoreCase));
            if (hit == IntPtr.Zero)
                throw new ToolException($"No window titled '{title}' in {p.ProcessName}. Open windows: " + string.Join(" | ", Win32.WindowsOf(p.Id).Select(Win32.Title).Where(t => t.Length > 0)));
            hwnd = hit;
        }
        using var wb = WindowImage(hwnd, out var r);
        var dialogs = Win32.WindowsOf(p.Id).Where(h => h != hwnd).Select(Win32.Title).Where(t => t.Length > 0).Take(8).ToList();
        var cap = $"{p.ProcessName} window '{Win32.Title(hwnd)}': screen origin ({r.Left},{r.Top}), size {r.Width}x{r.Height}" +
                  (dialogs.Count > 0 ? ". Other open windows/dialogs: " + string.Join(" | ", dialogs) : "");
        return Encode(wb, maxW, fmt, cap, grid, r.Left, r.Top, 1.0);
#else
        throw new ToolException("Capture needs the Windows build of BURAK BIM.");
#endif
    }

    /// <summary>Capture mode: picture of the app a write tool just changed (null when off / not applicable).</summary>
    public static ImageResult? AutoCapture(Tool tool, Dictionary<string, object> args, ToolContext ctx)
    {
        if (Mode == "off") return null;
        if (tool.Toolset is "computer" or "capture" or "core" or "com") return null;
        string? app = null;
        if (ToolsetApp.TryGetValue(tool.Toolset, out var ta)) app = ta;
        else if (tool.Toolset is "api" or "ui") app = (Json.Str(args, "app") ?? "revit").ToLowerInvariant();
        if (app == null) return null;
        int maxW = (int)Json.Int(Config.Data, "captureMaxWidth", 1024);
        // through the Revit API when Revit is idle; after UI actions/posted commands (a dialog may be open) use the window
        bool apiOk = tool.Toolset != "ui" && !tool.Name.Contains("command") && tool.Name != "ribbon_invoke";
        if (app == "revit" && apiOk)
        {
            try
            {
                if (BridgeClient.Discover("revit").Count > 0)
                {
                    var res = BridgeClient.Call("revit", Json.Str(args, "target"), "revit.capture", Json.O("pixelSize", Math.Max(800, maxW)), ctx, 15000) as Dictionary<string, object>;
                    var first = (Json.Arr(res, "images") ?? new List<object>()).OfType<Dictionary<string, object>>().FirstOrDefault();
                    var f = first == null ? null : Json.Str(first, "file");
                    if (f != null && File.Exists(f)) return FileImage(f, maxW, $"Revit active view '{Json.Str(first!, "view")}' after {tool.Name}");
                }
            }
            catch { /* fall back to the window */ }
        }
#if WINDOWS
        try
        {
            var o = Capture(Json.O("app", app, "maxWidth", maxW, "format", "jpeg")) as ImageResult;
            return o == null ? null : new ImageResult { Base64 = o.Base64, MimeType = o.MimeType, Caption = $"{app} after {tool.Name}" };
        }
        catch { return null; }
#else
        return null;
#endif
    }

#if WINDOWS
    // ================================================================================ imaging
    public static System.Drawing.Bitmap Screen(int x, int y, int w, int h)
    {
        var bmp = new System.Drawing.Bitmap(Math.Max(1, w), Math.Max(1, h), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, bmp.Size, System.Drawing.CopyPixelOperation.SourceCopy);
        return bmp;
    }

    /// <summary>Window image via PrintWindow (works when covered by other windows); falls back to a screen copy.</summary>
    public static System.Drawing.Bitmap WindowImage(IntPtr hwnd, out Win32.RECT r)
    {
        if (Win32.IsIconic(hwnd)) { Win32.ShowWindow(hwnd, 9); Thread.Sleep(400); }
        Win32.GetWindowRect(hwnd, out r);
        int w = Math.Max(1, r.Width), h = Math.Max(1, r.Height);
        var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        bool ok;
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            var hdc = g.GetHdc();
            try { ok = Win32.PrintWindow(hwnd, hdc, 2 /* PW_RENDERFULLCONTENT */); }
            finally { g.ReleaseHdc(hdc); }
        }
        if (!ok || MostlyBlack(bmp))
        {
            bmp.Dispose();
            Win32.Focus(hwnd);
            Thread.Sleep(250);
            Win32.GetWindowRect(hwnd, out r);
            return Screen(r.Left, r.Top, Math.Max(1, r.Width), Math.Max(1, r.Height));
        }
        return bmp;
    }

    static bool MostlyBlack(System.Drawing.Bitmap b)
    {
        int dark = 0, n = 0;
        for (int y = 0; y < b.Height; y += Math.Max(1, b.Height / 24))
            for (int x = 0; x < b.Width; x += Math.Max(1, b.Width / 24))
            {
                var c = b.GetPixel(x, y); n++;
                if (c.R < 8 && c.G < 8 && c.B < 8) dark++;
            }
        return n > 0 && dark > n * 0.92;
    }

    /// <summary>Downscale, optionally draw a screen-coordinate grid, encode, save a copy in captures\.</summary>
    public static ImageResult Encode(System.Drawing.Bitmap src, int maxW, string fmt, string caption, bool grid, int originX, int originY, double _)
    {
        maxW = Math.Max(320, Math.Min(4000, maxW));
        double scale = src.Width > maxW ? (double)maxW / src.Width : 1.0;
        int w = Math.Max(1, (int)Math.Round(src.Width * scale)), h = Math.Max(1, (int)Math.Round(src.Height * scale));
        using var outBmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = System.Drawing.Graphics.FromImage(outBmp))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, w, h);
            if (grid)
            {
                int step = src.Width > 2400 ? 200 : 100; // screen pixels between lines
                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(110, 255, 0, 160), 1);
                using var font = new System.Drawing.Font("Segoe UI", 8f);
                using var bg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(170, 255, 255, 255));
                using var fg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 200, 0, 120));
                for (int sx = (originX / step + 1) * step; sx < originX + src.Width; sx += step)
                {
                    float px = (float)((sx - originX) * scale);
                    g.DrawLine(pen, px, 0, px, h);
                    var t = sx.ToString(); var sz = g.MeasureString(t, font);
                    g.FillRectangle(bg, px + 1, 1, sz.Width, sz.Height); g.DrawString(t, font, fg, px + 1, 1);
                }
                for (int sy = (originY / step + 1) * step; sy < originY + src.Height; sy += step)
                {
                    float py = (float)((sy - originY) * scale);
                    g.DrawLine(pen, 0, py, w, py);
                    var t = sy.ToString(); var sz = g.MeasureString(t, font);
                    g.FillRectangle(bg, 1, py + 1, sz.Width, sz.Height); g.DrawString(t, font, fg, 1, py + 1);
                }
            }
        }
        using var ms = new MemoryStream();
        string mime;
        if (fmt.StartsWith("png", StringComparison.OrdinalIgnoreCase)) { outBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); mime = "image/png"; }
        else
        {
            var enc = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
            using var ep = new System.Drawing.Imaging.EncoderParameters(1);
            ep.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 82L);
            outBmp.Save(ms, enc, ep); mime = "image/jpeg";
        }
        var bytes = ms.ToArray();
        string saved = "";
        try
        {
            var file = Path.Combine(BurakPaths.Sub("captures"), "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + (mime == "image/png" ? ".png" : ".jpg"));
            File.WriteAllBytes(file, bytes); saved = file;
        }
        catch { }
        var mapping = scale < 1.0
            ? $" Image is scaled {scale:0.###}: screenX = {originX} + imageX / {scale:0.###}, screenY = {originY} + imageY / {scale:0.###}."
            : $" Image pixels = screen pixels: screenX = {originX} + imageX, screenY = {originY} + imageY.";
        return new ImageResult { Base64 = Convert.ToBase64String(bytes), MimeType = mime, Caption = caption + "." + mapping + (grid ? " Grid labels are SCREEN coordinates." : "") + (saved.Length > 0 ? " Saved: " + saved : "") };
    }
#endif
}
