using System.Diagnostics;
using BurakBim.Server.Com;
using BurakBim.Server.Core;
using BurakBim.Server.Detection;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>COM automation: generic (any ProgID) + Inventor + AutoCAD LISP + ReCap.</summary>
public static class ComTools
{
    static readonly object ComGate = new();

    static object Root(Dictionary<string, object> a)
    {
        var h = Json.Str(a, "handle");
        if (!string.IsNullOrEmpty(h)) return ComInterop.FromHandle(h);
        var prog = Json.Str(a, "progId") ?? throw new ToolException("Give progId (e.g. Inventor.Application, AutoCAD.Application, Robot.Application) or handle.");
        return ComInterop.Connect(prog, Json.Bool(a, "launch"));
    }

    static T Locked<T>(Func<T> f) { lock (ComGate) return f(); }

    public static void Register(ToolRegistry r)
    {
        var prog = O("progId", Str("COM ProgID: Inventor.Application, AutoCAD.Application (all AutoCAD verticals), Robot.Application, ..."));
        var handle = O("handle", Str("Start from a $c handle instead of the application"));
        var launch = O("launch", Bool("Start the application if not running"));

        r.Add(new Tool
        {
            Name = "com_get", Title = "COM: read value", Toolset = "com", ReadOnly = true,
            Description = "Read a property path from a COM application, e.g. progId=Inventor.Application path=ActiveDocument.ComponentDefinition.Parameters.UserParameters.Count. Objects return $c handles.",
            InputSchema = Obj(prog, handle, O("path", Str("Dotted path; Item(1) / Method(args) allowed")), launch),
            Handler = (a, ctx) => Locked(() => ComInterop.Describe(ComInterop.Eval(Root(a), Json.Str(a, "path"))))
        });
        r.Add(new Tool
        {
            Name = "com_call", Title = "COM: call method", Toolset = "com", ReadOnly = false,
            Description = "Call a method on a COM object (application path or $c handle) with JSON args ($c handles allowed).",
            InputSchema = Obj(prog, handle, O("path", Str("Path to the object owning the method")), R("method", Str("Method name")), O("args", Arr("Arguments")), launch),
            Handler = (a, ctx) => Locked(() =>
            {
                var target = ComInterop.Eval(Root(a), Json.Str(a, "path")) ?? throw new ToolException("Target is null");
                var args = (Json.Arr(a, "args") ?? new()).Select(ComInterop.ToCom).ToArray();
                return ComInterop.Describe(ComInterop.Call(target, Json.Str(a, "method")!, args));
            })
        });
        r.Add(new Tool
        {
            Name = "com_set", Title = "COM: set property", Toolset = "com", ReadOnly = false,
            Description = "Set a property on a COM object.",
            InputSchema = Obj(prog, handle, O("path", Str("Path to the owner object")), R("member", Str("Property")), R("value", Any("Value")), launch),
            Handler = (a, ctx) => Locked(() =>
            {
                var target = ComInterop.Eval(Root(a), Json.Str(a, "path")) ?? throw new ToolException("Target is null");
                ComInterop.Set(target, Json.Str(a, "member")!, ComInterop.ToCom(Json.Get(a, "value")));
                return Json.O("set", Json.Str(a, "member"));
            })
        });
        r.Add(new Tool
        {
            Name = "com_members", Title = "COM: list members", Toolset = "com", ReadOnly = true,
            Description = "Properties and methods (with help text) of any COM object - discover Inventor / AutoCAD / Robot APIs.",
            InputSchema = Obj(prog, handle, O("path", Str("Path")), O("search", Str("Filter")), launch),
            Handler = (a, ctx) => Locked(() => ComInterop.Members(ComInterop.Eval(Root(a), Json.Str(a, "path")) ?? throw new ToolException("null"), Json.Str(a, "search")))
        });

        // ---------------- Inventor ----------------
        const string INV = "Inventor.Application";
        object Inv(Dictionary<string, object> a) => ComInterop.Connect(INV, Json.Bool(a, "launch"));
        object ActiveDoc(object app) => ComInterop.Get(app, "ActiveDocument") ?? throw new ToolException("No Inventor document is open.");
        var invLaunch = O("launch", Bool("Start Inventor if not running"));

        r.Add(new Tool
        {
            Name = "inventor_info", Title = "Inventor status", Toolset = "inventor", ReadOnly = true,
            Description = "Inventor version, open documents, active document type and path.",
            InputSchema = Obj(invLaunch),
            Handler = (a, ctx) => Locked(() =>
            {
                var app = Inv(a);
                var docs = new List<object>();
                var col = ComInterop.Get(app, "Documents")!;
                int n = Convert.ToInt32(ComInterop.Get(col, "Count"));
                for (int i = 1; i <= n; i++) { var d = ComInterop.Get(col, "Item", i)!; docs.Add(Json.O("name", ComInterop.Get(d, "DisplayName"), "path", ComInterop.Get(d, "FullFileName"), "type", ComInterop.Get(d, "DocumentType")?.ToString())); }
                object? active = null;
                try { var d = ComInterop.Get(app, "ActiveDocument"); if (d != null) active = ComInterop.Get(d, "FullFileName"); } catch { }
                return Json.O("version", ComInterop.Get(ComInterop.Get(app, "SoftwareVersion")!, "DisplayVersion"), "documents", docs, "active", active,
                    "note", "DocumentType: 12290=part, 12291=assembly, 12292=drawing, 12293=presentation");
            })
        });

        var cmdCache = new Dictionary<int, List<Dictionary<string, object>>>();
        r.Add(new Tool
        {
            Name = "inventor_commands", Title = "List Inventor commands", Toolset = "inventor", ReadOnly = true,
            Description = "Every Inventor command (ControlDefinitions: ~4000 incl. add-ins) with internal name, display name and description. Filter with search.",
            InputSchema = Obj(O("search", Str("Filter, e.g. Extrude, Fillet, Constraint, Frame Generator")), O("limit", Int("Max (default 200)")), invLaunch),
            Handler = (a, ctx) => Locked(() =>
            {
                var app = Inv(a);
                var key = Process.GetProcessesByName("Inventor").FirstOrDefault()?.Id ?? 0;
                if (!cmdCache.TryGetValue(key, out var list))
                {
                    list = new();
                    var defs = ComInterop.Get(ComInterop.Get(app, "CommandManager")!, "ControlDefinitions")!;
                    foreach (var d in (System.Collections.IEnumerable)defs)
                    {
                        try { list.Add(Json.O("name", ComInterop.Get(d, "InternalName"), "display", ComInterop.Get(d, "DisplayName"), "description", ComInterop.Get(d, "DescriptionText"))); } catch { }
                    }
                    cmdCache[key] = list;
                }
                var s = Json.Str(a, "search");
                var hits = list.Where(x => s == null || Json.Serialize(x).Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
                return Json.O("total", list.Count, "matching", hits.Count, "commands", hits.Take((int)Json.Int(a, "limit", 200)).Cast<object>().ToList());
            })
        });
        r.Add(new Tool
        {
            Name = "inventor_command", Title = "Run an Inventor command", Toolset = "inventor", ReadOnly = false,
            Description = "Execute any Inventor command by internal name (e.g. PartExtrudeCmd, AppZoomallCmd, AssemblyPlaceComponentCmd). synchronous=true waits for the command to finish.",
            InputSchema = Obj(R("name", Str("ControlDefinition internal name")), O("synchronous", Bool("Wait (default true)")), invLaunch),
            Handler = (a, ctx) => Locked(() =>
            {
                var app = Inv(a);
                var defs = ComInterop.Get(ComInterop.Get(app, "CommandManager")!, "ControlDefinitions")!;
                var def = ComInterop.Get(defs, "Item", Json.Str(a, "name")) ?? throw new ToolException("Command not found.");
                ComInterop.Call(def, "Execute2", Json.Bool(a, "synchronous", true));
                return Json.O("executed", Json.Str(a, "name"));
            })
        });
        r.Add(new Tool
        {
            Name = "inventor_documents", Title = "Inventor documents", Toolset = "inventor", ReadOnly = false,
            Description = "open | new (part/assembly/drawing, optional template) | save | saveas/export (by extension: .ipt .iam .idw .dwg .stp .step .igs .sat .stl .pdf .dwf) | close.",
            InputSchema = Obj(R("op", Enum("Operation", "open", "new", "save", "saveas", "export", "close")), O("path", Str("File path")),
                O("kind", Enum("New document kind", "part", "assembly", "drawing", "presentation")), O("template", Str("Template path")), invLaunch),
            Handler = (a, ctx) => Locked<object>(() =>
            {
                var app = Inv(a);
                var docs = ComInterop.Get(app, "Documents")!;
                var path = Json.Str(a, "path");
                switch (Json.Str(a, "op"))
                {
                    case "open": return ComInterop.Describe(ComInterop.Call(docs, "Open", path, true))!;
                    case "new":
                        {
                            var kind = Json.Str(a, "kind", "part");
                            int t = kind switch { "assembly" => 12291, "drawing" => 12292, "presentation" => 12293, _ => 12290 };
                            var tpl = Json.Str(a, "template") ?? ComInterop.Call(ComInterop.Get(app, "FileManager")!, "GetTemplateFile", t)?.ToString();
                            return ComInterop.Describe(ComInterop.Call(docs, "Add", t, tpl, true))!;
                        }
                    case "save": ComInterop.Call(ActiveDoc(app), "Save"); return Json.O("saved", ComInterop.Get(ActiveDoc(app), "FullFileName"));
                    case "saveas":
                    case "export":
                        if (string.IsNullOrEmpty(path)) throw new ToolException("path required");
                        ComInterop.Call(ActiveDoc(app), "SaveAs", path, Json.Str(a, "op") == "export");
                        return Json.O("written", path);
                    case "close": ComInterop.Call(ActiveDoc(app), "Close", true); return Json.O("closed", true);
                    default: throw new ToolException("op: open, new, save, saveas, export, close");
                }
            })
        });
        r.Add(new Tool
        {
            Name = "inventor_parameters", Title = "Inventor parameters", Toolset = "inventor", ReadOnly = false,
            Description = "List model/user parameters of the active part/assembly, or set them: values {\"d0\": \"50 mm\", \"Length\": \"1200 mm\"}. Updates the model.",
            InputSchema = Obj(O("values", AnyObj("Parameter expressions to set")), invLaunch),
            Handler = (a, ctx) => Locked(() =>
            {
                var doc = ActiveDoc(Inv(a));
                var pars = ComInterop.Get(ComInterop.Get(doc, "ComponentDefinition")!, "Parameters")!;
                var values = Json.Obj(a, "values");
                if (values != null)
                {
                    foreach (var kv in values)
                    {
                        var prm = ComInterop.Get(pars, "Item", kv.Key) ?? throw new ToolException("Parameter " + kv.Key + " not found");
                        ComInterop.Set(prm, "Expression", Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture));
                    }
                    ComInterop.Call(doc, "Update");
                }
                var list = new List<object>();
                foreach (var p in (System.Collections.IEnumerable)pars)
                    try { list.Add(Json.O("name", ComInterop.Get(p, "Name"), "expression", ComInterop.Get(p, "Expression"), "units", ComInterop.Get(p, "Units"))); } catch { }
                return Json.O("parameters", list, "updated", values?.Count ?? 0);
            })
        });
        r.Add(new Tool
        {
            Name = "inventor_iproperties", Title = "Inventor iProperties", Toolset = "inventor", ReadOnly = false,
            Description = "Read all iProperties of the active document, or set: values {\"Design Tracking Properties/Part Number\": \"BB-001\", \"Inventor Summary Information/Title\": \"...\"}.",
            InputSchema = Obj(O("values", AnyObj("'Set name/Property name': value")), invLaunch),
            Handler = (a, ctx) => Locked(() =>
            {
                var doc = ActiveDoc(Inv(a));
                var sets = ComInterop.Get(doc, "PropertySets")!;
                var values = Json.Obj(a, "values");
                if (values != null)
                    foreach (var kv in values)
                    {
                        var parts = kv.Key.Split('/', 2);
                        if (parts.Length != 2) throw new ToolException("Use 'Set name/Property name'");
                        var set = ComInterop.Get(sets, "Item", parts[0])!;
                        var prop = ComInterop.Get(set, "Item", parts[1])!;
                        ComInterop.Set(prop, "Value", kv.Value);
                    }
                var all = new Dictionary<string, object>();
                foreach (var set in (System.Collections.IEnumerable)sets)
                {
                    var props = new Dictionary<string, object>();
                    foreach (var p in (System.Collections.IEnumerable)set)
                        try { var v = ComInterop.Get(p, "Value"); props[ComInterop.Get(p, "Name")!.ToString()!] = v is string or double or int or bool or DateTime ? v : v?.ToString()!; } catch { }
                    all[ComInterop.Get(set, "Name")!.ToString()!] = props;
                }
                return all;
            })
        });
        r.Add(new Tool
        {
            Name = "inventor_ilogic", Title = "Run an iLogic rule", Toolset = "inventor", ReadOnly = false,
            Description = "Run an iLogic rule in the active document (or an external rule file path).",
            InputSchema = Obj(R("rule", Str("Rule name or external .iLogicVb path")), invLaunch),
            Handler = (a, ctx) => Locked(() =>
            {
                var app = Inv(a);
                var addin = ComInterop.Get(ComInterop.Get(app, "ApplicationAddIns")!, "ItemById", "{3BDD8D79-2179-4B11-8A5A-257B1C0263AC}") ?? throw new ToolException("iLogic add-in not found.");
                var auto = ComInterop.Get(addin, "Automation")!;
                var rule = Json.Str(a, "rule")!;
                var doc = ActiveDoc(app);
                if (File.Exists(rule)) ComInterop.Call(auto, "RunExternalRule", doc, rule);
                else ComInterop.Call(auto, "RunRule", doc, rule);
                return Json.O("ran", rule);
            })
        });

        // ---------------- AutoCAD (COM): synchronous LISP ----------------
        r.Add(new Tool
        {
            Name = "autocad_lisp", Title = "Evaluate AutoLISP", Toolset = "autocad", ReadOnly = false,
            Description = "Evaluate an AutoLISP expression in the running AutoCAD / Civil 3D (via COM) and return its printed value - synchronous. Works even without the bridge. e.g. (getvar \"DWGNAME\"), (sslength (ssget \"X\" '((0 . \"LINE\")))).",
            InputSchema = Obj(R("expr", Str("AutoLISP expression")), O("progId", Str("Default AutoCAD.Application"))),
            Handler = (a, ctx) => Locked(() =>
            {
                var app = ComInterop.Connect(Json.Str(a, "progId", "AutoCAD.Application")!, false);
                var doc = ComInterop.Get(app, "ActiveDocument") ?? throw new ToolException("No drawing open.");
                var outFile = Path.Combine(BurakPaths.ScratchDir, "lisp-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
                var lispPath = outFile.Replace("\\", "/");
                var expr = Json.Str(a, "expr")!;
                var code = $"(progn (setq *bb-f* (open \"{lispPath}\" \"w\")) (prin1 (vl-catch-all-apply '(lambda () {expr})) *bb-f*) (close *bb-f*) (princ)) ";
                ComInterop.Call(doc, "SendCommand", code);
                for (int i = 0; i < 100 && !File.Exists(outFile); i++) Thread.Sleep(100);
                if (!File.Exists(outFile)) throw new ToolException("AutoCAD did not evaluate the expression (busy in a command or dialog?).");
                var text = File.ReadAllText(outFile).Trim();
                try { File.Delete(outFile); } catch { }
                return Json.O("result", text);
            })
        });

        // ---------------- ReCap ----------------
        r.Add(new Tool
        {
            Name = "recap_info", Title = "ReCap Pro status", Toolset = "recap", ReadOnly = true,
            Description = "ReCap Pro installation, running instances, and the scan projects (.rcp/.rcs) found in a folder. ReCap has no public automation API: drive its UI with ui_* tools; process scans in Revit/Navisworks/AutoCAD (scan_* tools).",
            InputSchema = Obj(O("folder", Str("Folder to list .rcp/.rcs/.e57 files"))),
            Handler = (a, ctx) =>
            {
                var rc = SoftwareDetector.Detect().Where(p => p.Family == "recap").Select(p => (object)p.ToJson(SoftwareDetector.Running())).ToList();
                var folder = Json.Str(a, "folder");
                List<object>? files = null;
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".rcp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".rcs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".e57", StringComparison.OrdinalIgnoreCase))
                        .Take(500).Select(f => (object)Json.O("file", f, "sizeMB", Math.Round(new FileInfo(f).Length / 1048576.0, 1))).ToList();
                return Json.O("recap", rc, "files", files);
            }
        });
        r.Add(new Tool
        {
            Name = "recap_open", Title = "Open in ReCap Pro", Toolset = "recap", ReadOnly = false,
            Description = "Start ReCap Pro, optionally opening a .rcp project or importing scan files.",
            InputSchema = Obj(O("path", Str(".rcp / .rcs / .e57 file"))),
            Handler = (a, ctx) =>
            {
                var rc = SoftwareDetector.Detect().FirstOrDefault(p => p.Family == "recap" && p.Exe != null) ?? throw new ToolException("ReCap Pro is not installed.");
                var path = Json.Str(a, "path");
                var p = Process.Start(new ProcessStartInfo(rc.Exe!, string.IsNullOrEmpty(path) ? "" : "\"" + path + "\"") { UseShellExecute = true })!;
                return Json.O("started", rc.Name, "pid", p.Id, "next", "Use screenshot + ui_tree/ui_click to operate ReCap (register scans, export).");
            }
        });
    }
}
