using System.Diagnostics;
using BurakBim.Server.Bridges;
using BurakBim.Server.Core;
using BurakBim.Server.Detection;
using BurakBim.Server.Mcp;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

public static class CoreTools
{
    static string SearchArg(Dictionary<string, object> a)
    {
        var l = Json.StrList(a, "searchPaths");
        if (l == null || l.Count == 0) return "";
        foreach (var d in l) if (d.IndexOf('"') >= 0 || d.IndexOf('\'') >= 0) throw new ToolException("Invalid folder: " + d);
        foreach (var d in l) if (d.IndexOf(',') >= 0) throw new ToolException("Folder names with commas are not supported: " + d);
        return " -SearchPaths \"" + string.Join(",", l.Select(d => d.TrimEnd('\\'))) + "\"";
    }

    public static void Register(ToolRegistry r)
    {
        r.Add(new Tool
        {
            Name = "burak_status", Title = "BURAK BIM status", Toolset = "core", ReadOnly = true,
            Description = "START HERE. Installed Autodesk software (2022-2027 and others), which are running, live BURAK BIM bridges, COM apps, config (read-only mode etc.).",
            InputSchema = Obj(O("refresh", Bool("Re-scan installed software"))),
            Handler = (a, ctx) =>
            {
                var running = SoftwareDetector.Running();
                var products = SoftwareDetector.Detect(Json.Bool(a, "refresh")).Select(p => (object)p.ToJson(running)).ToList();
                var bridges = BridgeClient.Discover().Select(d =>
                {
                    object? ping = null;
                    try { ping = BridgeClient.Call(d, "bridge.ping", null, ctx, 5000); } catch (Exception ex) { ping = "unreachable: " + ex.Message; }
                    return (object)Json.O("id", d.Id, "host", d.Host, "product", d.Product, "year", d.Year, "pid", d.Pid, "since", d.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), "ping", ping);
                }).ToList();
                var com = new List<object>();
                if (OperatingSystem.IsWindows())
                    foreach (var prog in new[] { "Inventor.Application", "AutoCAD.Application", "Robot.Application" })
                        try { if (Com.ComInterop.GetActive(prog) != null) com.Add(prog); } catch { }
                return Json.O("burakBim", BurakInfo.Version, "installDir", BurakPaths.Root,
                    "readOnly", Config.ReadOnly, "scripting", Config.AllowScripts,
                    "installed", products, "liveBridges", bridges, "runningComApps", com,
                    "tip", bridges.Count == 0 ? "No bridge is live. Open Revit / AutoCAD / Civil 3D / Navisworks (BURAK BIM loads automatically) or use burak_launch." : "Use the app-specific tools; anything else via ribbon_invoke / api_* / script_run.");
            }
        });

        r.Add(new Tool
        {
            Name = "burak_launch", Title = "Start an Autodesk application", Toolset = "core", ReadOnly = false,
            Description = "Start an installed application (optionally opening a file): revit, autocad (incl. Civil 3D etc. by name), navisworks, inventor, recap, robot. Waits until its BURAK BIM bridge is live when one exists.",
            InputSchema = Obj(R("app", Str("Family or product name, e.g. 'revit', 'civil 3d', 'navisworks manage'")), O("year", Int("Version year, e.g. 2024 (default newest)")),
                O("file", Str("File to open")), O("waitSeconds", Int("Max wait for the bridge (default 240)"))),
            Handler = (a, ctx) =>
            {
                var q = (Json.Str(a, "app") ?? "").ToLowerInvariant();
                var year = (int)Json.Int(a, "year", 0);
                var all = SoftwareDetector.Detect();
                var cands = all.Where(p => p.Exe != null && (p.Family == q || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) && (year == 0 || p.Year == year)).ToList();
                if (q == "autocad") cands = cands.OrderBy(p => p.Name.Contains("Civil") || p.Name.Contains("Plant") ? 1 : 0).ToList();
                var prod = cands.OrderBy(p => q == "autocad" && (p.Name.Contains("Civil") || p.Name.Contains("Plant")) ? 1 : 0).ThenByDescending(p => p.Year).FirstOrDefault()
                    ?? throw new ToolException($"'{q}' {(year > 0 ? year.ToString() : "")} is not installed. Installed: " + string.Join(", ", all.Where(p => p.Exe != null).Select(p => p.Name)));
                var file = Json.Str(a, "file");
                var psi = new ProcessStartInfo(prod.Exe!) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(prod.Exe)! };
                if (prod.Family == "autocad" && prod.AcadRegKey != null)
                {
                    // start the right vertical (Civil 3D, Plant 3D ...) with its own profile
                    var lcid = prod.AcadRegKey.Split(':').Last();
                    var lang = "en-US";
                    try { lang = System.Globalization.CultureInfo.GetCultureInfo(Convert.ToInt32(lcid, 16)).Name; } catch { }
                    var sw = ProductSwitch(prod.Name);
                    psi.Arguments = sw == "ACAD" ? "" : $"/product {sw} /language \"{lang}\"";
                }
                if (!string.IsNullOrEmpty(file)) psi.Arguments = (psi.Arguments + " \"" + file + "\"").Trim();
                var proc = Process.Start(psi)!;
                if (prod.Family is "revit" or "autocad" or "navisworks")
                {
                    var until = DateTime.UtcNow.AddSeconds(Json.Int(a, "waitSeconds", 240));
                    while (DateTime.UtcNow < until)
                    {
                        Thread.Sleep(2000);
                        var d = BridgeClient.Discover(prod.Family).FirstOrDefault(x => x.Pid == proc.Id);
                        if (d != null) return Json.O("started", prod.Name, "pid", proc.Id, "bridge", d.Id, "ready", true);
                    }
                    return Json.O("started", prod.Name, "pid", proc.Id, "ready", false, "note", "Application started but the bridge is not live yet (sign-in, license or a startup dialog may be waiting). Use screenshot / ui_* to check.");
                }
                return Json.O("started", prod.Name, "pid", proc.Id);
            }
        });

        r.Add(new Tool
        {
            Name = "burak_rescan", Title = "Detect new software & install bridges", Toolset = "core", ReadOnly = false,
            Description = "Re-detect installed Autodesk software and build/register BURAK BIM bridges for newly installed versions (runs the installer in rescan mode, same as the logon sentinel). Software in custom folders (e.g. D:\\ALL PROGRAMS) is found automatically from the registry, shortcuts and running apps; add searchPaths to also scan folders directly.",
            InputSchema = Obj(O("searchPaths", Arr("Extra folders to scan, e.g. [\"D:\\\\ALL PROGRAMS\"] (remembered)", Str("folder")))),
            Handler = (a, ctx) =>
            {
                var script = Path.Combine(BurakPaths.Root, "setup", "install.ps1");
                if (!File.Exists(script)) throw new ToolException("Installer script not found at " + script);
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Rescan -Quiet" + SearchArg(a))
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi)!;
                var outText = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(1800000);
                SoftwareDetector.Detect(true);
                return Json.O("exitCode", p.ExitCode, "log", outText.Length > 6000 ? outText[^6000..] : outText);
            }
        });

        r.Add(new Tool
        {
            Name = "burak_config", Title = "View / change BURAK BIM settings", Toolset = "core", ReadOnly = false,
            Description = "Show settings, or set one: readOnly (block all changes), allowScripts, allowComAutomation, allowUiAutomation, toolsets ([\"auto\"] = tools for installed software; [\"all\"]; or a list: core, api, revit, scan, inspector, autocad, navisworks, inventor, recap, com, ui), inspectorName, requestTimeoutSeconds.",
            InputSchema = Obj(O("key", Str("Setting name")), O("value", Any("New value"))),
            Handler = (a, ctx) =>
            {
                var key = Json.Str(a, "key");
                if (!string.IsNullOrEmpty(key))
                {
                    if (key == "readOnly" && Config.ReadOnly && !Json.Bool(a, "value"))
                        throw new ToolException("Read-only mode can only be switched off by editing " + Config.FilePath + " (an AI agent cannot unlock itself).");
                    Config.Set(key, Json.Get(a, "value"));
                }
                return Json.O("file", Config.FilePath, "settings", Config.Data, "note", "toolsets changes apply after the AI client reconnects (restart the MCP server).");
            }
        });

        r.Add(new Tool
        {
            Name = "burak_audit_log", Title = "Audit trail", Toolset = "core", ReadOnly = true,
            Description = "Every AI action through BURAK BIM: time, AI client, session, tool, arguments, result, duration, errors. For BIM inspectors and managers.",
            InputSchema = Obj(O("date", Str("yyyy-MM-dd (default today)")), O("tool", Str("Filter by tool name (contains)")), O("session", Str("Filter by session")),
                O("writesOnly", Bool("Only model-changing actions")), O("errorsOnly", Bool("Only failures")), O("limit", Int("Max (default 200)"))),
            Handler = (a, ctx) =>
            {
                var day = DateTime.TryParse(Json.Str(a, "date"), out var d) ? d : DateTime.Now;
                var tool = Json.Str(a, "tool"); var session = Json.Str(a, "session");
                var rows = Audit.Read(day, o =>
                    (tool == null || Json.Str(o, "tool", "").Contains(tool, StringComparison.OrdinalIgnoreCase)) &&
                    (session == null || Json.Str(o, "session") == session) &&
                    (!Json.Bool(a, "writesOnly") || Json.Bool(o, "writes")) &&
                    (!Json.Bool(a, "errorsOnly") || !Json.Bool(o, "ok", true)), (int)Json.Int(a, "limit", 200));
                return Json.O("date", day.ToString("yyyy-MM-dd"), "count", rows.Count, "currentSession", ctx.Session, "entries", rows.Cast<object>().ToList());
            }
        });

        r.Add(new Tool
        {
            Name = "burak_inspection_report", Title = "Inspection report (HTML)", Toolset = "inspector", ReadOnly = true,
            Description = "Write an HTML inspection report for the open Revit model: AI-created elements by status, provenance, scan deviation, and today's audit trail. Returns the file path.",
            InputSchema = Obj(O("title", Str("Report title")), O("inspector", Str("Inspector name")), Target),
            Handler = (a, ctx) => Reports.InspectionReport.Build(a, ctx)
        });

        r.Add(new Tool
        {
            Name = "burak_guide", Title = "How-to guide", Toolset = "core", ReadOnly = true,
            Description = "Workflow guides: scan (scan-to-BIM), inspector, ribbon (every command/tab), api (full API access).",
            InputSchema = Obj(O("topic", Enum("Topic", "overview", "scan", "inspector", "ribbon", "api"))),
            Handler = (a, ctx) => Guide.Topic(Json.Str(a, "topic", "overview"))
        });
    }

    /// <summary>acad.exe /product switch for AutoCAD verticals.</summary>
    static string ProductSwitch(string name)
    {
        if (name.Contains("Civil 3D")) return "C3D";
        if (name.Contains("Plant 3D")) return "PLNT3D";
        if (name.Contains("Map 3D")) return "MAP";
        if (name.Contains("MEP")) return "MEP";
        if (name.Contains("Architecture")) return "ARCH";
        if (name.Contains("Electrical")) return "ACE";
        if (name.Contains("Mechanical")) return "ACM";
        if (name.Contains("Advance Steel")) return "ADVS";
        return "ACAD";
    }
}
