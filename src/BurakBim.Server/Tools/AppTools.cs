using System.Net.Http;
using System.Text;
using BurakBim.Server.Com;
using BurakBim.Server.Core;
using BurakBim.Server.Detection;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>
/// Connectors for non-bridge applications:
///  - Robot Structural Analysis (RobotOM COM)          toolset "robot"
///  - Rhinoceros 7/8 + Grasshopper (Rhino COM)         toolset "rhino"
///  - Graphisoft Archicad (JSON API + Tapir add-on)    toolset "archicad"
///  - Dynamo graphs (.dyn JSON + DynamoCLI)            toolset "dynamo"
///  - com_constants (type-library enums of any COM app) toolset "com"
/// </summary>
public static class AppTools
{
    static readonly object Gate = new();
    static T Locked<T>(Func<T> f) { lock (Gate) return f(); }

    static void Add(ToolRegistry r, string set, string name, string title, string desc, bool ro, Dictionary<string, object> schema,
        Func<Dictionary<string, object>, object?> h, bool destructive = false)
        => r.Add(new Tool { Name = name, Title = title, Toolset = set, Description = desc, ReadOnly = ro, Destructive = destructive, InputSchema = schema, Handler = (a, c) => h(a) });

    public static void Register(ToolRegistry r)
    {
        RegisterCom(r);
        RegisterRobot(r);
        RegisterRhino(r);
        RegisterArchicad(r);
        RegisterDynamo(r);
    }

    // =====================================================================================================
    static void RegisterCom(ToolRegistry r)
    {
        Add(r, "com", "com_constants", "COM: type-library constants",
            "Named enum constants from a COM application's type library (e.g. Robot I_LT_SUPPORT, Inventor kPartDocumentObject, AutoCAD acActiveViewport). Use the values with com_call / com_set.",
            true, Obj(R("progId", Str("COM ProgID, e.g. Robot.Application")), O("search", Str("Filter by name")), O("limit", Int("Max results (default 200)"))),
            a => Locked(() =>
            {
                var app = ComInterop.Connect(Json.Str(a, "progId")!, false);
                var s = Json.Str(a, "search");
                var map = ComInterop.Constants(app);
                var hits = map.Where(kv => string.IsNullOrEmpty(s) || kv.Key.Contains(s, StringComparison.OrdinalIgnoreCase))
                    .Take((int)Json.Int(a, "limit", 200)).ToDictionary(k => k.Key, v => v.Value);
                return Json.O("total", map.Count, "matched", hits.Count, "constants", hits);
            }));
    }

    // =====================================================================================================
    // Robot Structural Analysis (RobotOM)
    const string ROBOT = "Robot.Application";

    static dynamic Robot(Dictionary<string, object> a)
    {
        dynamic app = ComInterop.Connect(ROBOT, Json.Bool(a, "launch"));
        try { if (Convert.ToInt32(app.Visible) == 0) { app.Visible = 1; app.Interactive = 1; } } catch { }
        return app;
    }
    static dynamic Structure(dynamic app)
    {
        if (!Convert.ToBoolean(app.Project.IsActive)) throw new ToolException("No Robot project is open. Use robot_project with action=new or open.");
        return app.Project.Structure;
    }
    static int K(object app, string name) => Convert.ToInt32(ComInterop.Const(app, name));
    static string Sel(List<long>? nums, string? text) => !string.IsNullOrWhiteSpace(text) ? text! : nums is { Count: > 0 } ? string.Join(" ", nums) : throw new ToolException("Give numbers (list) or selection text such as \"1to10 15\".");

    static void RegisterRobot(ToolRegistry r)
    {
        var launch = O("launch", Bool("Start Robot if not running"));

        Add(r, "robot", "robot_info", "Robot status",
            "Robot Structural Analysis version, open project, structure type and node/bar/case counts.",
            true, Obj(launch), a => Locked(() =>
            {
                var app = Robot(a);
                var d = Json.O("version", (object)SafeStr(() => app.Version), "projectActive", Convert.ToBoolean(app.Project.IsActive));
                if (Convert.ToBoolean(app.Project.IsActive))
                {
                    var s = app.Project.Structure;
                    d["name"] = SafeStr(() => app.Project.Name);
                    d["file"] = SafeStr(() => app.Project.FileName);
                    d["type"] = SafeStr(() => app.Project.Type.ToString());
                    d["nodes"] = (int)s.Nodes.GetAll().Count;
                    d["bars"] = (int)s.Bars.GetAll().Count;
                    d["cases"] = (int)s.Cases.GetAll().Count;
                    d["resultsAvailable"] = SafeStr(() => s.Results.Available.ToString());
                }
                return d;
            }));

        Add(r, "robot", "robot_project", "Robot project: new / open / save / close",
            "Create (frame2d, frame3d, shell, truss2d, truss3d, grillage, building), open, save, save as or close a Robot project.",
            false, Obj(R("action", Enum("Action", "new", "open", "save", "saveas", "close")), O("type", Enum("Project type for new", "frame2d", "frame3d", "truss2d", "truss3d", "grillage", "shell", "building")),
                O("path", Str("File path (.rtd) for open/saveas")), launch),
            a => Locked(() =>
            {
                var app = Robot(a);
                var path = Json.Str(a, "path");
                switch (Json.Str(a, "action"))
                {
                    case "new":
                        var t = Json.Str(a, "type", "frame3d") switch
                        {
                            "frame2d" => "I_PT_FRAME_2D", "truss2d" => "I_PT_TRUSS_2D", "truss3d" => "I_PT_TRUSS_3D", "grillage" => "I_PT_GRILLAGE",
                            "shell" => "I_PT_SHELL", "building" => "I_PT_BUILDING", _ => "I_PT_FRAME_3D"
                        };
                        app.Project.New(K(app, t)); break;
                    case "open": app.Project.Open(path ?? throw new ToolException("path required")); break;
                    case "save": app.Project.Save(); break;
                    case "saveas": app.Project.SaveAs(path ?? throw new ToolException("path required")); break;
                    case "close": app.Project.Close(); break;
                }
                return Json.O("ok", true, "file", SafeStr(() => app.Project.FileName));
            }));

        Add(r, "robot", "robot_nodes", "Robot nodes: create / list / delete",
            "Create nodes from [[x,y,z],...] (metres, auto-numbered or numbered), list nodes with coordinates and support labels, or delete by selection.",
            false, Obj(R("action", Enum("Action", "create", "list", "delete")), O("points", Arr("[[x,y,z], ...] for create")), O("numbers", Arr("Node numbers")),
                O("selection", Str("Robot selection text, e.g. 1to20")), O("limit", Int("Max listed (default 500)")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                switch (Json.Str(a, "action"))
                {
                    case "create":
                        var made = new List<object>();
                        var nums = Json.IntList(a, "numbers");
                        int i = 0;
                        foreach (var p in Json.Arr(a, "points") ?? throw new ToolException("points required"))
                        {
                            var v = Json.Vec(p);
                            int n = nums != null && i < nums.Count ? (int)nums[i] : (int)s.Nodes.FreeNumber;
                            s.Nodes.Create(n, v[0], v[1], v.Length > 2 ? v[2] : 0.0);
                            made.Add(n); i++;
                        }
                        return Json.O("created", made);
                    case "delete":
                        dynamic sel = s.Selections.Create(K(app, "I_OT_NODE"));
                        sel.FromText(Sel(Json.IntList(a, "numbers"), Json.Str(a, "selection")));
                        s.Nodes.DeleteMany(sel);
                        return Json.O("deleted", (string)sel.ToText());
                    default:
                        var all = s.Nodes.GetAll(); var list = new List<object>();
                        int lim = (int)Json.Int(a, "limit", 500), cnt = (int)all.Count;
                        int sup = K(app, "I_LT_SUPPORT");
                        for (int k = 1; k <= cnt && list.Count < lim; k++)
                        {
                            var nd = all.Get(k);
                            list.Add(Json.O("n", (int)nd.Number, "x", (double)nd.X, "y", (double)nd.Y, "z", (double)nd.Z,
                                "support", SafeStr(() => Convert.ToInt32(nd.HasLabel(sup)) != 0 ? (string)nd.GetLabelName(sup) : null)));
                        }
                        return Json.O("count", cnt, "nodes", list);
                }
            }));

        Add(r, "robot", "robot_bars", "Robot bars: create / list / delete",
            "Create bars between node pairs [[start,end],...] with optional section and material labels, list bars (nodes, section, material, length) or delete.",
            false, Obj(R("action", Enum("Action", "create", "list", "delete")), O("pairs", Arr("[[startNode, endNode], ...]")), O("section", Str("Section label, e.g. IPE 300 (loaded from the section database if missing)")),
                O("material", Str("Material label, e.g. S235")), O("numbers", Arr("Bar numbers")), O("selection", Str("Selection text")), O("limit", Int("Max listed")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                int secT = K(app, "I_LT_BAR_SECTION"), matT = K(app, "I_LT_MATERIAL");
                switch (Json.Str(a, "action"))
                {
                    case "create":
                        var sec = Json.Str(a, "section"); var mat = Json.Str(a, "material");
                        if (sec != null) EnsureDbLabel(s, secT, sec);
                        if (mat != null) EnsureDbLabel(s, matT, mat);
                        var made = new List<object>();
                        foreach (var p in Json.Arr(a, "pairs") ?? throw new ToolException("pairs required"))
                        {
                            var v = Json.Vec(p);
                            int n = (int)s.Bars.FreeNumber;
                            s.Bars.Create(n, (int)v[0], (int)v[1]);
                            var bar = s.Bars.Get(n);
                            if (sec != null) bar.SetLabel(secT, sec);
                            if (mat != null) bar.SetLabel(matT, mat);
                            made.Add(n);
                        }
                        return Json.O("created", made);
                    case "delete":
                        dynamic sel = s.Selections.Create(K(app, "I_OT_BAR"));
                        sel.FromText(Sel(Json.IntList(a, "numbers"), Json.Str(a, "selection")));
                        s.Bars.DeleteMany(sel);
                        return Json.O("deleted", (string)sel.ToText());
                    default:
                        var all = s.Bars.GetAll(); var list = new List<object>();
                        int lim = (int)Json.Int(a, "limit", 500), cnt = (int)all.Count;
                        for (int k = 1; k <= cnt && list.Count < lim; k++)
                        {
                            var b = all.Get(k);
                            list.Add(Json.O("n", (int)b.Number, "start", (int)b.StartNode, "end", (int)b.EndNode,
                                "length", SafeNum(() => b.Length),
                                "section", SafeStr(() => Convert.ToInt32(b.HasLabel(secT)) != 0 ? (string)b.GetLabelName(secT) : null),
                                "material", SafeStr(() => Convert.ToInt32(b.HasLabel(matT)) != 0 ? (string)b.GetLabelName(matT) : null)));
                        }
                        return Json.O("count", cnt, "bars", list);
                }
            }));

        Add(r, "robot", "robot_supports", "Robot: define and assign supports",
            "Create a nodal support label (fixed, pinned, roller or custom UX..RZ fixities) and assign it to nodes.",
            false, Obj(R("name", Str("Support label name")), O("preset", Enum("Preset", "fixed", "pinned", "rollerX", "rollerY", "custom")),
                O("fix", Arr("Custom fixities [UX,UY,UZ,RX,RY,RZ] as 0/1")), O("numbers", Arr("Node numbers")), O("selection", Str("Selection text")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                int t = K(app, "I_LT_SUPPORT"); var name = Json.Str(a, "name")!;
                int[] f = Json.Str(a, "preset", "fixed") switch
                {
                    "pinned" => new[] { 1, 1, 1, 0, 0, 0 }, "rollerX" => new[] { 0, 1, 1, 0, 0, 0 }, "rollerY" => new[] { 1, 0, 1, 0, 0, 0 },
                    "custom" => (Json.IntList(a, "fix") ?? throw new ToolException("fix required")).Select(x => (int)x).ToArray(),
                    _ => new[] { 1, 1, 1, 1, 1, 1 }
                };
                var lab = s.Labels.Create(t, name); var d = lab.Data;
                d.UX = f[0]; d.UY = f[1]; d.UZ = f[2]; d.RX = f[3]; d.RY = f[4]; d.RZ = f[5];
                s.Labels.Store(lab);
                dynamic sel = s.Selections.Create(K(app, "I_OT_NODE"));
                sel.FromText(Sel(Json.IntList(a, "numbers"), Json.Str(a, "selection")));
                s.Nodes.SetLabel(sel, t, name);
                return Json.O("label", name, "nodes", (string)sel.ToText());
            }));

        Add(r, "robot", "robot_labels", "Robot labels: list / assign section, material, release",
            "List labels of a type (section, material, support, release, thickness) or assign an existing / database label to bars.",
            false, Obj(R("action", Enum("Action", "list", "assign")), R("type", Enum("Label type", "section", "material", "support", "release", "thickness", "offset")),
                O("name", Str("Label name (sections / materials are loaded from the Robot database when missing)")), O("numbers", Arr("Bar numbers")), O("selection", Str("Selection text")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                int t = K(app, Json.Str(a, "type") switch
                {
                    "material" => "I_LT_MATERIAL", "support" => "I_LT_SUPPORT", "release" => "I_LT_BAR_RELEASE",
                    "thickness" => "I_LT_PANEL_THICKNESS", "offset" => "I_LT_BAR_OFFSET", _ => "I_LT_BAR_SECTION"
                });
                if (Json.Str(a, "action") == "list")
                {
                    var names = s.Labels.GetAvailableNames(t); var list = new List<object>();
                    for (int i = 1; i <= (int)names.Count; i++) list.Add((string)names.Get(i));
                    return Json.O("labels", list);
                }
                var name = Json.Str(a, "name") ?? throw new ToolException("name required");
                if (Json.Str(a, "type") is "section" or "material") EnsureDbLabel(s, t, name);
                dynamic sel = s.Selections.Create(K(app, "I_OT_BAR"));
                sel.FromText(Sel(Json.IntList(a, "numbers"), Json.Str(a, "selection")));
                s.Bars.SetLabel(sel, t, name);
                return Json.O("assigned", name, "bars", (string)sel.ToText());
            }));

        Add(r, "robot", "robot_cases", "Robot load cases: list / create / delete",
            "List load cases (number, name, nature, type) or create a simple case (dead, live, wind, snow, temperature, accidental, seismic).",
            false, Obj(R("action", Enum("Action", "list", "create", "delete")), O("name", Str("Case name")), O("number", Int("Case number (auto if omitted)")),
                O("nature", Enum("Nature", "dead", "live", "wind", "snow", "temperature", "accidental", "seismic")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                switch (Json.Str(a, "action"))
                {
                    case "create":
                        int n = (int)Json.Int(a, "number", 0); if (n <= 0) n = (int)s.Cases.FreeNumber;
                        var nat = Json.Str(a, "nature", "dead") switch
                        {
                            "live" => "I_CN_EXPLOATATION", "wind" => "I_CN_WIND", "snow" => "I_CN_SNOW", "temperature" => "I_CN_TEMPERATURE",
                            "accidental" => "I_CN_ACCIDENTAL", "seismic" => "I_CN_SEISMIC", _ => "I_CN_PERMANENT"
                        };
                        s.Cases.CreateSimple(n, Json.Str(a, "name") ?? ("Case " + n), K(app, nat), K(app, "I_CAT_STATIC_LINEAR"));
                        return Json.O("created", n);
                    case "delete":
                        s.Cases.Delete((int)Json.Int(a, "number")); return Json.O("deleted", Json.Int(a, "number"));
                    default:
                        var all = s.Cases.GetAll(); var list = new List<object>();
                        for (int i = 1; i <= (int)all.Count; i++)
                        {
                            var c = all.Get(i);
                            list.Add(Json.O("n", (int)c.Number, "name", (string)c.Name, "nature", SafeStr(() => c.Nature.ToString()), "type", SafeStr(() => c.Type.ToString())));
                        }
                        return Json.O("cases", list);
                }
            }));

        Add(r, "robot", "robot_loads", "Robot: apply loads",
            "Add a load record to a simple case: self-weight, nodal force/moment, bar uniform load or panel uniform pressure. Units are Robot's current units (default kN, m).",
            false, Obj(R("case", Int("Load case number")), R("kind", Enum("Load kind", "selfweight", "nodal", "bar_uniform", "panel_uniform")),
                O("numbers", Arr("Target nodes / bars / panels")), O("selection", Str("Selection text")),
                O("fx", Num("FX / PX")), O("fy", Num("FY / PY")), O("fz", Num("FZ / PZ (negative = down)")), O("mx", Num("MX")), O("my", Num("MY")), O("mz", Num("MZ")),
                O("local", Bool("Bar load in local axes")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                var c = s.Cases.Get((int)Json.Int(a, "case"));
                string kind = Json.Str(a, "kind")!;
                var rt = kind switch { "selfweight" => "I_LRT_DEAD", "nodal" => "I_LRT_NODE_FORCE", "bar_uniform" => "I_LRT_BAR_UNIFORM", _ => "I_LRT_UNIFORM" };
                int idx = (int)c.Records.New(K(app, rt));
                var rec = c.Records.Get(idx);
                void V(string k, string key) { if (a.ContainsKey(key)) rec.SetValue(K(app, k), Json.Num(a, key)); }
                switch (kind)
                {
                    case "selfweight":
                        rec.SetValue(K(app, "I_DRV_Z"), -1.0);
                        if (!a.ContainsKey("numbers") && !a.ContainsKey("selection")) { rec.SetValue(K(app, "I_DRV_ENTIRE_STRUCTURE"), 1.0); return Json.O("record", idx, "entireStructure", true); }
                        break;
                    case "nodal":
                        V("I_NFRV_FX", "fx"); V("I_NFRV_FY", "fy"); V("I_NFRV_FZ", "fz"); V("I_NFRV_CX", "mx"); V("I_NFRV_CY", "my"); V("I_NFRV_CZ", "mz"); break;
                    case "bar_uniform":
                        V("I_BURV_PX", "fx"); V("I_BURV_PY", "fy"); V("I_BURV_PZ", "fz");
                        if (Json.Bool(a, "local")) rec.SetValue(K(app, "I_BURV_LOCAL"), 1.0); break;
                    default:
                        V("I_URV_PX", "fx"); V("I_URV_PY", "fy"); V("I_URV_PZ", "fz"); break;
                }
                rec.Objects.FromText(Sel(Json.IntList(a, "numbers"), Json.Str(a, "selection")));
                return Json.O("record", idx, "case", Json.Int(a, "case"), "kind", kind);
            }));

        Add(r, "robot", "robot_combinations", "Robot: create load combination",
            "Create a ULS / SLS / accidental combination from {caseNumber: factor}.",
            false, Obj(R("name", Str("Combination name")), R("factors", AnyObj("{\"1\": 1.35, \"2\": 1.5}")), O("type", Enum("Type", "uls", "sls", "accidental")), O("number", Int("Number (auto)")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                int n = (int)Json.Int(a, "number", 0); if (n <= 0) n = (int)s.Cases.FreeNumber;
                var ct = Json.Str(a, "type", "uls") switch { "sls" => "I_CBT_SLS", "accidental" => "I_CBT_ACC", _ => "I_CBT_ULS" };
                var comb = s.Cases.CreateCombination(n, Json.Str(a, "name"), K(app, ct), K(app, "I_CN_PERMANENT"), K(app, "I_CAT_COMB"));
                foreach (var kv in Json.Obj(a, "factors") ?? new()) comb.CaseFactors.New(int.Parse(kv.Key), Json.ToDouble(kv.Value));
                return Json.O("created", n);
            }));

        Add(r, "robot", "robot_calculate", "Robot: run analysis",
            "Run the structural calculation (all cases). Returns success flag and result availability.",
            false, Obj(launch), a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app);
                int rc = (int)app.Project.CalcEngine.Calculate();
                return Json.O("returnCode", rc, "resultsAvailable", SafeStr(() => s.Results.Available.ToString()));
            }));

        Add(r, "robot", "robot_results", "Robot: analysis results",
            "Reactions, nodal displacements or bar forces (FX..MZ at relative positions) for a case or combination.",
            true, Obj(R("kind", Enum("Result", "reactions", "displacements", "bar_forces")), R("case", Int("Case / combination number")),
                O("numbers", Arr("Nodes or bars (default all, max 500)")), O("positions", Arr("Relative positions along bars, default [0, 0.5, 1]")), launch),
            a => Locked(() =>
            {
                var app = Robot(a); var s = Structure(app); var res = s.Results;
                int cs = (int)Json.Int(a, "case"); var kind = Json.Str(a, "kind");
                var nums = Json.IntList(a, "numbers")?.Select(x => (int)x).ToList();
                var rows = new List<object>();
                if (kind == "bar_forces")
                {
                    nums ??= AllNumbers(s.Bars.GetAll());
                    var pos = (Json.Arr(a, "positions") ?? new List<object> { 0.0, 0.5, 1.0 }).Select(x => Json.ToDouble(x)).ToList();
                    foreach (var b in nums.Take(500))
                        foreach (var p in pos)
                        {
                            var f = res.Bars.Forces.Value(b, cs, p);
                            rows.Add(Json.O("bar", b, "pos", p, "FX", (double)f.FX, "FY", (double)f.FY, "FZ", (double)f.FZ, "MX", (double)f.MX, "MY", (double)f.MY, "MZ", (double)f.MZ));
                        }
                }
                else
                {
                    nums ??= AllNumbers(s.Nodes.GetAll());
                    foreach (var n in nums.Take(500))
                    {
                        if (kind == "reactions")
                        {
                            try
                            {
                                var f = res.Nodes.Reactions.Value(n, cs);
                                rows.Add(Json.O("node", n, "FX", (double)f.FX, "FY", (double)f.FY, "FZ", (double)f.FZ, "MX", (double)f.MX, "MY", (double)f.MY, "MZ", (double)f.MZ));
                            }
                            catch { /* not a supported node */ }
                        }
                        else
                        {
                            var d = res.Nodes.Displacements.Value(n, cs);
                            rows.Add(Json.O("node", n, "UX", (double)d.UX, "UY", (double)d.UY, "UZ", (double)d.UZ, "RX", (double)d.RX, "RY", (double)d.RY, "RZ", (double)d.RZ));
                        }
                    }
                }
                return Json.O("case", cs, "kind", kind, "rows", rows);
            }));

        Add(r, "robot", "robot_view", "Robot: view and export picture",
            "Redraw / zoom-all the active Robot view and optionally save a screen capture of it to a file (bmp/jpg/png).",
            false, Obj(O("path", Str("Image file to save")), launch),
            a => Locked(() =>
            {
                var app = Robot(a);
                try { app.Project.ViewMngr.Refresh(); } catch { }
                var path = Json.Str(a, "path");
                if (path != null)
                {
                    var view = app.Project.ViewMngr.GetView(1);
                    view.Redraw(1);
                    var p = app.CmpntFactory.Create(K(app, "I_CT_SCREEN_CAPTURE_PARAMS"));
                    p.Name = Path.GetFileNameWithoutExtension(path); p.UpdateType = K(app, "I_SCUT_COPY_TO_CLIPBOARD");
                    view.MakeScreenCapture(p);
                    app.Project.PrintEngine.SaveScreenCaptureToFile(p.Name, path, K(app, "I_SCF_" + Path.GetExtension(path).TrimStart('.').ToUpperInvariant()));
                }
                return Json.O("ok", true, "path", path);
            }));
    }

    static List<int> AllNumbers(dynamic coll)
    {
        var l = new List<int>();
        for (int i = 1; i <= (int)coll.Count; i++) l.Add((int)coll.Get(i).Number);
        return l;
    }

    static void EnsureDbLabel(dynamic s, int type, string name)
    {
        if (Convert.ToInt32(s.Labels.Exist(type, name)) != 0) return;
        var lab = s.Labels.Create(type, name);
        try { lab.Data.LoadFromDBase(name); } catch { }
        s.Labels.Store(lab);
    }

    static string? SafeStr(Func<object?> f) { try { return f()?.ToString(); } catch { return null; } }
    static double? SafeNum(Func<object?> f) { try { return Convert.ToDouble(f()); } catch { return null; } }

    // =====================================================================================================
    // Rhinoceros 7 / 8 + Grasshopper
    static readonly string[] RhinoProgIds = { "Rhino.Application.8", "Rhino.Application.7", "Rhino.Application" };

    static dynamic Rhino(Dictionary<string, object> a)
    {
        if (!Config.AllowCom) throw new ToolException("COM automation is disabled in burakbim.json (allowComAutomation=false).");
        foreach (var p in RhinoProgIds) { var o = ComInterop.GetActive(p); if (o != null) return o; }
        if (!Json.Bool(a, "launch")) throw new ToolException("Rhino is not running (or was not started with automation). Call again with launch=true to start Rhino through COM.");
        foreach (var p in RhinoProgIds)
            if (Type.GetTypeFromProgID(p) != null) { dynamic o = ComInterop.Connect(p, true); try { o.Visible = 1; } catch { } return o; }
        throw new ToolException("Rhino COM server is not registered (Rhino 7/8 not installed?).");
    }

    static dynamic Rs(dynamic rhino)
    {
        for (int i = 0; i < 40; i++)
        {
            try { var o = rhino.GetScriptObject(); if (o != null) return o; } catch { }
            Thread.Sleep(250);
        }
        throw new ToolException("RhinoScript object is not available (Rhino still loading?).");
    }

    static void RegisterRhino(ToolRegistry r)
    {
        var launch = O("launch", Bool("Start Rhino if not running"));

        Add(r, "rhino", "rhino_info", "Rhino status",
            "Rhino version, document path, units, layers and object counts.", true, Obj(launch),
            a => Locked(() =>
            {
                var rh = Rhino(a); var rs = Rs(rh);
                var objs = rs.AllObjects() as Array;
                return Json.O("build", SafeStr(() => rs.BuildDate()), "document", SafeStr(() => rs.DocumentPath()) + SafeStr(() => rs.DocumentName()),
                    "units", SafeStr(() => rs.UnitSystemName(true, false, false)), "layers", (rs.LayerNames() as Array)?.Length ?? 0, "objects", objs?.Length ?? 0,
                    "currentLayer", SafeStr(() => rs.CurrentLayer()));
            }));

        Add(r, "rhino", "rhino_command", "Rhino: run command",
            "Run any Rhino command macro exactly as typed on the command line, e.g. \"_-Line 0,0,0 10,0,0\" or \"_-SaveAs D:\\\\x.3dm _Enter\". Prefix with - for no dialogs.",
            false, Obj(R("command", Str("Command macro")), O("echo", Bool("Echo to the command line")), launch),
            a => Locked(() =>
            {
                var rh = Rhino(a);
                var ok = rh.RunScript(Json.Str(a, "command"), Json.Bool(a, "echo") ? 1 : 0);
                string? last = null; try { last = Rs(rh).LastCommandName(); } catch { }
                return Json.O("ok", Convert.ToBoolean(ok), "lastCommand", last);
            }));

        Add(r, "rhino", "rhino_script", "Rhino: call RhinoScript method",
            "Call any RhinoScript method on the running Rhino (AddLine, AddBox, ObjectsByLayer, ObjectName, BoundingBox, LayerColor, ExportFile, ...). Arrays are passed as JSON arrays.",
            false, Obj(R("method", Str("RhinoScript method name")), O("args", Arr("Arguments")), launch),
            a => Locked(() =>
            {
                object rs = Rs(Rhino(a));
                var args = (Json.Arr(a, "args") ?? new()).Select(ToVariant).ToArray();
                return FromVariant(ComInterop.Call(rs, Json.Str(a, "method")!, args));
            }));

        Add(r, "rhino", "rhino_layers", "Rhino layers",
            "List layers (name, color, visible, locked, object count) or add / delete / set current / show / hide / lock / unlock / color a layer.",
            false, Obj(R("action", Enum("Action", "list", "add", "delete", "current", "show", "hide", "lock", "unlock", "color")), O("name", Str("Layer (full path)")),
                O("rgb", Arr("[r,g,b] for add/color")), launch),
            a => Locked(() =>
            {
                dynamic rs = Rs(Rhino(a)); var name = Json.Str(a, "name");
                int Rgb() { var c = Json.Vec(Json.Get(a, "rgb") ?? new List<object> { 0.0, 0.0, 0.0 }); return (int)c[0] | ((int)c[1] << 8) | ((int)c[2] << 16); }
                switch (Json.Str(a, "action"))
                {
                    case "add": return Json.O("added", (string)(a.ContainsKey("rgb") ? rs.AddLayer(name, Rgb()) : rs.AddLayer(name)));
                    case "delete": return Json.O("deleted", Convert.ToBoolean(rs.DeleteLayer(name)));
                    case "current": rs.CurrentLayer(name); return Json.O("current", name);
                    case "show": rs.LayerVisible(name, true); return Json.O("ok", true);
                    case "hide": rs.LayerVisible(name, false); return Json.O("ok", true);
                    case "lock": rs.LayerLocked(name, true); return Json.O("ok", true);
                    case "unlock": rs.LayerLocked(name, false); return Json.O("ok", true);
                    case "color": rs.LayerColor(name, Rgb()); return Json.O("ok", true);
                }
                var list = new List<object>();
                foreach (var l in (rs.LayerNames() as Array) ?? Array.Empty<object>())
                {
                    string n = l.ToString()!;
                    int col = Convert.ToInt32(rs.LayerColor(n));
                    list.Add(Json.O("name", n, "color", new List<object> { col & 255, (col >> 8) & 255, (col >> 16) & 255 },
                        "visible", Convert.ToBoolean(rs.LayerVisible(n)), "locked", Convert.ToBoolean(rs.LayerLocked(n)), "objects", (rs.ObjectsByLayer(n) as Array)?.Length ?? 0));
                }
                return Json.O("layers", list);
            }));

        Add(r, "rhino", "rhino_objects", "Rhino objects",
            "List objects (id, type, layer, name, bounding box) filtered by layer / type / selection, or select, delete, move, copy, hide, show, rename, set layer.",
            false, Obj(R("action", Enum("Action", "list", "select", "delete", "move", "copy", "hide", "show", "rename", "layer")),
                O("ids", Arr("Object GUIDs")), O("layer", Str("Layer filter / target")), O("selected", Bool("Use the current selection")),
                O("type", Int("RhinoScript type filter (1 point, 4 curve, 8 surface, 16 polysurface, 32 mesh, 4096 block)")), O("vector", Pt("Move/copy vector")),
                O("name", Str("New name")), O("limit", Int("Max listed (default 500)")), launch),
            a => Locked(() =>
            {
                dynamic rs = Rs(Rhino(a));
                var ids = Json.StrList(a, "ids");
                if (ids == null || ids.Count == 0)
                {
                    object? got = Json.Bool(a, "selected") ? rs.SelectedObjects() : Json.Str(a, "layer") is { } ly && Json.Str(a, "action") == "list" ? rs.ObjectsByLayer(ly)
                        : a.ContainsKey("type") ? rs.ObjectsByType((int)Json.Int(a, "type")) : rs.AllObjects();
                    ids = ((got as Array) ?? Array.Empty<object>()).Cast<object>().Select(x => x.ToString()!).ToList();
                }
                object Arr() => ids!.Cast<object>().ToArray();
                switch (Json.Str(a, "action"))
                {
                    case "select": rs.UnselectAllObjects(); return Json.O("selected", Convert.ToInt32(rs.SelectObjects(Arr())));
                    case "delete": return Json.O("deleted", Convert.ToInt32(rs.DeleteObjects(Arr())));
                    case "hide": return Json.O("hidden", Convert.ToInt32(rs.HideObjects(Arr())));
                    case "show": return Json.O("shown", Convert.ToInt32(rs.ShowObjects(Arr())));
                    case "move": return Json.O("moved", (rs.MoveObjects(Arr(), Vec3(a)) as Array)?.Length ?? 0);
                    case "copy": return Json.O("copies", FromVariant(rs.CopyObjects(Arr(), new object[] { 0.0, 0.0, 0.0 }, Vec3(a))));
                    case "rename": foreach (var id in ids) rs.ObjectName(id, Json.Str(a, "name")); return Json.O("renamed", ids.Count);
                    case "layer": rs.ObjectLayer(Arr(), Json.Str(a, "layer")); return Json.O("moved", ids.Count, "layer", Json.Str(a, "layer"));
                }
                var list = new List<object>();
                foreach (var id in ids.Take((int)Json.Int(a, "limit", 500)))
                {
                    list.Add(Json.O("id", id, "type", Convert.ToInt32(rs.ObjectType(id)), "layer", SafeStr(() => rs.ObjectLayer(id)), "name", SafeStr(() => rs.ObjectName(id)),
                        "bbox", FromVariant(SafeObj(() => rs.BoundingBox(id)))));
                }
                return Json.O("count", ids.Count, "objects", list);
            }));

        Add(r, "rhino", "rhino_file", "Rhino file: open / save / import / export",
            "Open, save, save as, import or export (3dm, dwg, dxf, ifc, step, stl, obj, fbx, skp, ...) the Rhino document. Export uses the selected objects, or ids.",
            false, Obj(R("action", Enum("Action", "open", "save", "saveas", "import", "export", "new")), O("path", Str("File path")), O("ids", Arr("Objects to export (default: all)")), launch),
            a => Locked(() =>
            {
                var rh = Rhino(a); var path = Json.Str(a, "path");
                string Q(string p) => "\"" + p + "\"";
                switch (Json.Str(a, "action"))
                {
                    case "new": rh.RunScript("_-New _None", 0); break;
                    case "open": rh.RunScript("_-Open " + Q(Need(path)) + " _Enter", 0); break;
                    case "save": rh.RunScript("_-Save _Enter", 0); break;
                    case "saveas": rh.RunScript("_-SaveAs " + Q(Need(path)) + " _Enter", 0); break;
                    case "import": rh.RunScript("_-Import " + Q(Need(path)) + " _Enter", 0); break;
                    case "export":
                        dynamic rs = Rs(rh); rs.UnselectAllObjects();
                        var ids = Json.StrList(a, "ids");
                        if (ids is { Count: > 0 }) rs.SelectObjects(ids.Cast<object>().ToArray()); else rs.AllObjects(true);
                        rh.RunScript("_-Export " + Q(Need(path)) + " _Enter _Enter", 0);
                        break;
                }
                return Json.O("ok", true, "path", path);
            }));

        Add(r, "rhino", "grasshopper_open", "Grasshopper: open definition",
            "Load Grasshopper (if needed), open a .gh / .ghx definition and optionally show or hide the editor.",
            false, Obj(R("path", Str(".gh / .ghx path")), O("showEditor", Bool("Show the Grasshopper editor (default true)")), launch),
            a => Locked(() =>
            {
                dynamic gh = Gh(Rhino(a));
                if (Json.Bool(a, "showEditor", true)) gh.ShowEditor(); else gh.HideEditor();
                var ok = gh.OpenDocument(Json.Str(a, "path"));
                return Json.O("opened", Convert.ToBoolean(ok), "path", Json.Str(a, "path"));
            }));

        Add(r, "rhino", "grasshopper_run", "Grasshopper: set inputs, solve, bake",
            "Assign values to named parameters (by nickname), recompute the solution and optionally bake named outputs onto a layer.",
            false, Obj(O("inputs", AnyObj("{\"Width\": 12.5, \"Name\": \"A\"}")), O("bake", Arr("Parameter nicknames to bake")), O("layer", Str("Bake layer")),
                O("closeAfter", Bool("Close the document afterwards")), launch),
            a => Locked(() =>
            {
                dynamic gh = Gh(Rhino(a));
                var set = new List<object>();
                foreach (var kv in Json.Obj(a, "inputs") ?? new())
                {
                    gh.AssignDataToParameter(kv.Key, ToVariant(kv.Value)); set.Add(kv.Key);
                }
                gh.RunSolver(true);
                var baked = new List<object>();
                foreach (var p in Json.StrList(a, "bake") ?? new())
                {
                    try { baked.Add(Json.O("param", p, "ids", FromVariant(gh.BakeDataInObject(p, Json.Str(a, "layer") ?? "BURAK BIM")))); }
                    catch (Exception ex) { baked.Add(Json.O("param", p, "error", ex.Message)); }
                }
                if (Json.Bool(a, "closeAfter")) try { gh.CloseDocument(); } catch { }
                return Json.O("inputs", set, "solved", true, "baked", baked);
            }));
    }

    static dynamic Gh(dynamic rhino)
    {
        dynamic rs = Rs(rhino);
        object? gh = null;
        try { gh = rs.GetPlugInObject("Grasshopper"); } catch { }
        if (gh == null) { rhino.RunScript("_-Grasshopper _Window _Show _Enter", 0); Thread.Sleep(1500); gh = rs.GetPlugInObject("Grasshopper"); }
        return gh ?? throw new ToolException("Grasshopper plug-in object is not available.");
    }

    static string Need(string? p) => p ?? throw new ToolException("path required");
    static object? SafeObj(Func<object?> f) { try { return f(); } catch { return null; } }
    static object Vec3(Dictionary<string, object> a) { var v = Json.Vec(Json.Get(a, "vector") ?? throw new ToolException("vector required")); return new object[] { v[0], v[1], v.Length > 2 ? v[2] : 0.0 }; }

    static object? ToVariant(object? v) => v switch
    {
        List<object> l => l.Select(ToVariant).ToArray(),
        long x when x is >= int.MinValue and <= int.MaxValue => (int)x,
        _ => v
    };

    static object? FromVariant(object? v) => v switch
    {
        null => null,
        DBNull => null,
        Array arr => arr.Cast<object?>().Select(FromVariant).ToList(),
        _ => ComInterop.Describe(v)
    };

    // =====================================================================================================
    // Archicad JSON API (built in since Archicad 24) + Tapir add-on commands
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    const int AcPortFirst = 19723, AcPortLast = 19744;

    static Dictionary<string, object>? AcPost(int port, string command, object? parameters, int timeoutMs = 120000)
    {
        var body = Json.O("command", command);
        if (parameters != null) body["parameters"] = parameters;
        using var cts = new CancellationTokenSource(timeoutMs);
        using var content = new StringContent(Json.Serialize(body), Encoding.UTF8, "application/json");
        var resp = Http.PostAsync($"http://127.0.0.1:{port}", content, cts.Token).GetAwaiter().GetResult();
        return Json.ParseObject(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
    }

    static List<(int port, Dictionary<string, object> info)> AcInstances()
    {
        var tasks = Enumerable.Range(AcPortFirst, AcPortLast - AcPortFirst + 1).Select(p => Task.Run(() =>
        {
            try
            {
                var alive = AcPost(p, "API.IsAlive", null, 600);
                if (alive == null || !Json.Bool(alive, "succeeded")) return ((int, Dictionary<string, object>)?)null;
                var info = AcPost(p, "API.GetProductInfo", null, 2000);
                return (p, Json.Obj(info!, "result") ?? new());
            }
            catch { return null; }
        })).ToArray();
        Task.WaitAll(tasks);
        return tasks.Select(t => t.Result).Where(x => x != null).Select(x => x!.Value).ToList();
    }

    internal static object? Ac(Dictionary<string, object> a, string command, object? parameters)
    {
        int port = (int)Json.Int(a, "port", 0);
        if (port == 0)
        {
            var inst = AcInstances();
            if (inst.Count == 0) throw new ToolException("No running Archicad answered on ports 19723-19744. Start Archicad 24+ (the JSON API is on by default) and open a project.");
            port = inst[0].port;
        }
        var res = AcPost(port, command, parameters) ?? throw new ToolException("Empty answer from Archicad.");
        if (!Json.Bool(res, "succeeded"))
        {
            var err = Json.Obj(res, "error");
            throw new ToolException($"Archicad {command} failed: {(err != null ? Json.Str(err, "message") : Json.Serialize(res))}");
        }
        return Json.Get(res, "result");
    }

    internal static object? Tapir(Dictionary<string, object> a, string cmd, object? p)
    {
        var r = Ac(a, "API.ExecuteAddOnCommand", Json.O("addOnCommandId", Json.O("commandNamespace", "TapirCommand", "commandName", cmd),
            "addOnCommandParameters", p ?? Json.O()));
        return r is Dictionary<string, object> d && d.TryGetValue("addOnCommandResponse", out var x) ? x : r;
    }

    static List<object> ElemIds(Dictionary<string, object> a) =>
        (Json.StrList(a, "guids") ?? throw new ToolException("guids required")).Select(g => (object)Json.O("elementId", Json.O("guid", g))).ToList();

    static void RegisterArchicad(ToolRegistry r)
    {
        var port = O("port", Int("Archicad API port (default: first running instance)"));

        Add(r, "archicad", "archicad_info", "Archicad instances",
            "Running Archicad instances (port, version, build, language). Requires Archicad 24 or newer.", true, Obj(),
            a => AcInstances().Select(i => (object)Json.O("port", i.port, "product", i.info)).ToList());

        Add(r, "archicad", "archicad_command", "Archicad: JSON API command",
            "Send any Archicad JSON API command (API.GetElementsByType, API.GetBoundingBoxes3D, API.GetAttributesByType, API.GetBuildingMaterialPhysicalProperties, ...) with its parameters.",
            false, Obj(R("command", Str("Command, e.g. API.GetAllElements")), O("parameters", AnyObj("Command parameters")), port),
            a => Ac(a, Json.Str(a, "command")!, Json.Obj(a, "parameters")));

        Add(r, "archicad", "archicad_tapir", "Archicad: Tapir add-on command",
            "Run a Tapir add-on command (GetProjectInfo, GetStories, GetSelectedElements, GetDetailsOfElements, CreateColumns, CreateSlabs, CreateObjects, MoveElements, HighlightElements, SetGDLParametersOfElements, TeamworkReceive, ...). Needs the free Tapir add-on.",
            false, Obj(R("command", Str("Tapir command name")), O("parameters", AnyObj("Command parameters")), port),
            a => Tapir(a, Json.Str(a, "command")!, Json.Obj(a, "parameters")));

        Add(r, "archicad", "archicad_elements", "Archicad elements",
            "Element GUIDs of a type (Wall, Slab, Column, Beam, Door, Window, Object, Zone, Roof, Stair, Railing, CurtainWall, Mesh, Morph, ...) or all elements, optionally with 3D bounding boxes.",
            true, Obj(O("type", Str("Element type (omit for all)")), O("boundingBoxes", Bool("Include 3D bounding boxes")), O("limit", Int("Max (default 2000)")), port),
            a =>
            {
                var t = Json.Str(a, "type");
                var res = (Dictionary<string, object>)Ac(a, t == null ? "API.GetAllElements" : "API.GetElementsByType", t == null ? null : Json.O("elementType", t))!;
                var els = (Json.Arr(res, "elements") ?? new()).Take((int)Json.Int(a, "limit", 2000)).ToList();
                var d = Json.O("count", (Json.Arr(res, "elements") ?? new()).Count, "elements", els);
                if (Json.Bool(a, "boundingBoxes") && els.Count > 0) d["boundingBoxes"] = Ac(a, "API.Get3DBoundingBoxes", Json.O("elements", els));
                return d;
            });

        Add(r, "archicad", "archicad_properties", "Archicad properties: find / read / write",
            "Find property ids (built-in nonLocalizedName or user-defined group/name), read property values of elements, or write values.",
            false, Obj(R("action", Enum("Action", "ids", "get", "set", "all")),
                O("builtIn", Arr("Built-in names, e.g. General_ElementID, Geometry_Area")), O("userDefined", Arr("User-defined [[group, name], ...]")),
                O("guids", Arr("Element GUIDs")), O("propertyIds", Arr("Property GUIDs")), O("values", Arr("For set: [{guid, propertyId, value}]")), port),
            a =>
            {
                switch (Json.Str(a, "action"))
                {
                    case "all": return Ac(a, "API.GetAllPropertyIds", Json.O("propertyType", "All"));
                    case "ids":
                        var props = new List<object>();
                        foreach (var n in Json.StrList(a, "builtIn") ?? new()) props.Add(Json.O("type", "BuiltIn", "nonLocalizedName", n));
                        foreach (var u in Json.Arr(a, "userDefined") ?? new()) { var p = (List<object>)u; props.Add(Json.O("type", "UserDefined", "localizedName", p.Select(x => x).ToList())); }
                        return Ac(a, "API.GetPropertyIds", Json.O("properties", props));
                    case "get":
                        return Ac(a, "API.GetPropertyValuesOfElements", Json.O("elements", ElemIds(a),
                            "properties", (Json.StrList(a, "propertyIds") ?? throw new ToolException("propertyIds required")).Select(g => (object)Json.O("propertyId", Json.O("guid", g))).ToList()));
                    default:
                        var vals = (Json.Arr(a, "values") ?? throw new ToolException("values required")).Cast<Dictionary<string, object>>().Select(v => (object)Json.O(
                            "elementId", Json.O("guid", Json.Str(v, "guid")), "propertyId", Json.O("guid", Json.Str(v, "propertyId")),
                            "propertyValue", Json.O("value", Json.Get(v, "value")))).ToList();
                        return Ac(a, "API.SetPropertyValuesOfElements", Json.O("elementPropertyValues", vals));
                }
            });

        Add(r, "archicad", "archicad_classifications", "Archicad classifications",
            "List classification systems, the items of a system, the classifications of elements, or set an element's classification.",
            false, Obj(R("action", Enum("Action", "systems", "items", "of_elements", "set")), O("systemGuid", Str("Classification system GUID")),
                O("itemGuid", Str("Classification item GUID for set")), O("guids", Arr("Element GUIDs")), port),
            a => Json.Str(a, "action") switch
            {
                "systems" => Ac(a, "API.GetAllClassificationSystems", null),
                "items" => Ac(a, "API.GetAllClassificationsInSystem", Json.O("classificationSystemId", Json.O("guid", Json.Str(a, "systemGuid")))),
                "of_elements" => Ac(a, "API.GetClassificationsOfElements", Json.O("elements", ElemIds(a),
                    "classificationSystemIds", new List<object> { Json.O("classificationSystemId", Json.O("guid", Json.Str(a, "systemGuid"))) })),
                _ => Ac(a, "API.SetClassificationsOfElements", Json.O("elementClassifications", ElemIds(a).Select(e => (object)Json.O(
                    "elementId", ((Dictionary<string, object>)e)["elementId"],
                    "classificationId", Json.O("classificationSystemId", Json.O("guid", Json.Str(a, "systemGuid")), "classificationItemId", Json.O("guid", Json.Str(a, "itemGuid"))))).ToList()))
            });

        Add(r, "archicad", "archicad_navigator", "Archicad navigator / views",
            "Navigator tree (ProjectMap, ViewMap, LayoutBook, PublisherSets) and publisher set names.",
            true, Obj(O("tree", Enum("Tree", "ProjectMap", "ViewMap", "LayoutBook", "PublisherSets")), port),
            a => Json.O("tree", Ac(a, "API.GetNavigatorItemTree", Json.O("navigatorTreeId", Json.O("type", Json.Str(a, "tree", "ProjectMap")))),
                "publisherSets", SafeObj(() => Ac(a, "API.GetPublisherSetNames", null))));

        Add(r, "archicad", "archicad_publish", "Archicad: publish publisher set",
            "Publish a publisher set (PDF / DWG / IFC / BIMx outputs as configured in Archicad), optionally into a folder.",
            false, Obj(R("publisherSet", Str("Publisher set name")), O("outputPath", Str("Output folder")), port),
            a =>
            {
                var p = Json.O("publisherSetName", Json.Str(a, "publisherSet"));
                if (Json.Str(a, "outputPath") is { } o) p["outputPath"] = o;
                return Ac(a, "API.PublishPublisherSet", p) ?? Json.O("published", Json.Str(a, "publisherSet"));
            });

        Add(r, "archicad", "archicad_project", "Archicad project info and stories (Tapir)",
            "Project info fields and story levels via Tapir.", true, Obj(port),
            a => Json.O("project", SafeObj(() => Tapir(a, "GetProjectInfo", null)), "stories", SafeObj(() => Tapir(a, "GetStories", null))));

        Add(r, "archicad", "archicad_selection", "Archicad selection (Tapir)",
            "Get the selected elements, change the selection or highlight elements in Archicad.",
            false, Obj(R("action", Enum("Action", "get", "add", "remove", "highlight", "clear_highlight")), O("guids", Arr("Element GUIDs")), port),
            a => Json.Str(a, "action") switch
            {
                "get" => Tapir(a, "GetSelectedElements", null),
                "add" => Tapir(a, "ChangeSelectionOfElements", Json.O("addElementsToSelection", ElemIds(a))),
                "remove" => Tapir(a, "ChangeSelectionOfElements", Json.O("removeElementsFromSelection", ElemIds(a))),
                "highlight" => Tapir(a, "HighlightElements", Json.O("elements", ElemIds(a), "highlightedColors",
                    ElemIds(a).Select(_ => (object)new List<object> { 255, 80, 0, 255 }).ToList(), "wireframe3D", false)),
                _ => Tapir(a, "HighlightElements", Json.O("elements", new List<object>(), "highlightedColors", new List<object>()))
            });

        Add(r, "archicad", "archicad_create", "Archicad: create columns / slabs / objects (Tapir)",
            "Create columns at points, slabs from polygons, or library objects at points via Tapir.",
            false, Obj(R("kind", Enum("Kind", "columns", "slabs", "objects")), O("points", Arr("[[x,y,z], ...] (metres) for columns/objects")),
                O("polygons", Arr("[[[x,y],...], ...] for slabs")), O("level", Num("Slab level (m)")), O("objectName", Str("Library part name for objects")), port),
            a =>
            {
                List<object> Pts() => (Json.Arr(a, "points") ?? throw new ToolException("points required")).Select(p => { var v = Json.Vec(p); return (object)Json.O("x", v[0], "y", v[1], "z", v.Length > 2 ? v[2] : 0.0); }).ToList();
                switch (Json.Str(a, "kind"))
                {
                    case "columns": return Tapir(a, "CreateColumns", Json.O("columnsData", Pts().Select(p => (object)Json.O("coordinates", p)).ToList()));
                    case "objects": return Tapir(a, "CreateObjects", Json.O("objectsData", Pts().Select(p => (object)Json.O("libraryPartName", Json.Str(a, "objectName"), "coordinates", p, "dimensions", Json.O("x", 1.0, "y", 1.0, "z", 1.0))).ToList()));
                    default:
                        var slabs = (Json.Arr(a, "polygons") ?? throw new ToolException("polygons required")).Select(poly => (object)Json.O(
                            "level", Json.Num(a, "level"),
                            "polygonCoordinates", ((List<object>)poly).Select(p => { var v = Json.Vec(p); return (object)Json.O("x", v[0], "y", v[1]); }).ToList())).ToList();
                        return Tapir(a, "CreateSlabs", Json.O("slabsData", slabs));
                }
            });

        Add(r, "archicad", "archicad_move", "Archicad: move elements (Tapir)",
            "Move (or copy) elements by a vector in metres.", false,
            Obj(R("guids", Arr("Element GUIDs")), R("vector", Pt("Move vector")), O("copy", Bool("Copy instead of move")), port),
            a =>
            {
                var v = Json.Vec(Json.Get(a, "vector"));
                return Tapir(a, "MoveElements", Json.O("elementsWithMoveVectors", ElemIds(a).Select(e => (object)Json.O("elementId", ((Dictionary<string, object>)e)["elementId"],
                    "moveVector", Json.O("x", v[0], "y", v[1], "z", v.Length > 2 ? v[2] : 0.0), "copy", Json.Bool(a, "copy"))).ToList()));
            });
    }

    // =====================================================================================================
    // Dynamo graphs (.dyn = JSON since Dynamo 2.0)
    static void RegisterDynamo(ToolRegistry r)
    {
        Add(r, "dynamo", "dynamo_inspect", "Dynamo: inspect graph",
            "Read a .dyn file: Dynamo version, inputs (name, type, value), outputs, node list (type, function, package) and required packages - so inspectors can review a script before it runs.",
            true, Obj(R("path", Str(".dyn file")), O("nodes", Bool("Include the full node list"))),
            a =>
            {
                var g = Json.ParseObject(File.ReadAllText(Need(Json.Str(a, "path"))));
                var nodes = Json.Arr(g, "Nodes") ?? new();
                var view = Json.Obj(g, "View");
                var names = (Json.Arr(view ?? new(), "NodeViews") ?? new()).Cast<Dictionary<string, object>>()
                    .GroupBy(n => Json.Str(n, "Id") ?? "").ToDictionary(x => x.Key, x => Json.Str(x.First(), "Name"));
                var d = Json.O("name", Json.Str(g, "Name"), "dynamo", Json.Str(view ?? new(), "Dynamo") is { } ? Json.Obj(view!, "Dynamo") : null,
                    "inputs", Json.Arr(g, "Inputs"), "outputs", Json.Arr(g, "Outputs"),
                    "packages", Json.Arr(g, "NodeLibraryDependencies"), "nodeCount", nodes.Count,
                    "pythonNodes", nodes.Cast<Dictionary<string, object>>().Count(n => (Json.Str(n, "NodeType") ?? "").Contains("Python")));
                if (Json.Bool(a, "nodes"))
                    d["nodes"] = nodes.Cast<Dictionary<string, object>>().Select(n => (object)Json.O("id", Json.Str(n, "Id"), "name", names.GetValueOrDefault(Json.Str(n, "Id") ?? ""),
                        "type", Json.Str(n, "NodeType"), "function", Json.Str(n, "FunctionSignature") ?? Json.Str(n, "ConcreteType"))).ToList();
                return d;
            });

        Add(r, "dynamo", "dynamo_set_inputs", "Dynamo: set graph inputs",
            "Write a copy of a .dyn with new input values (by input name or node id). Run it in Revit with revit_dynamo_run, or headless with dynamo_cli.",
            false, Obj(R("path", Str("Source .dyn")), R("values", AnyObj("{\"Input name or id\": value}")), O("outPath", Str("Output .dyn (default: <name>.burak.dyn)"))),
            a =>
            {
                var src = Need(Json.Str(a, "path"));
                var g = Json.ParseObject(File.ReadAllText(src));
                var vals = Json.Obj(a, "values") ?? new();
                var view = Json.Obj(g, "View");
                var idByName = (Json.Arr(view ?? new(), "NodeViews") ?? new()).Cast<Dictionary<string, object>>()
                    .Where(n => Json.Str(n, "Name") != null).GroupBy(n => Json.Str(n, "Name")!, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => Json.Str(x.First(), "Id"), StringComparer.OrdinalIgnoreCase);
                var changed = new List<object>();
                foreach (var kv in vals)
                {
                    var id = idByName.GetValueOrDefault(kv.Key) ?? kv.Key;
                    var val = kv.Value is string s ? s : Json.Serialize(kv.Value).Trim('"');
                    foreach (var list in new[] { Json.Arr(g, "Inputs"), Json.Arr(g, "Nodes") })
                        foreach (var n in (list ?? new()).Cast<Dictionary<string, object>>())
                            if (string.Equals(Json.Str(n, "Id"), id, StringComparison.OrdinalIgnoreCase) || string.Equals(Json.Str(n, "Name"), kv.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (n.ContainsKey("Value") || list == Json.Arr(g, "Inputs")) n["Value"] = list == Json.Arr(g, "Inputs") ? val : (object)(kv.Value ?? "");
                                if (n.ContainsKey("InputValue")) n["InputValue"] = kv.Value!;
                                changed.Add(kv.Key);
                            }
                }
                var outPath = Json.Str(a, "outPath") ?? Path.Combine(Path.GetDirectoryName(src)!, Path.GetFileNameWithoutExtension(src) + ".burak.dyn");
                File.WriteAllText(outPath, Json.Serialize(g, true));
                return Json.O("written", outPath, "changed", changed.Distinct().ToList());
            });

        Add(r, "dynamo", "dynamo_cli", "Dynamo: run graph headless",
            "Run a .dyn with DynamoCLI (Dynamo Sandbox / Core installs) without Revit and return the output-node values. For Revit graphs use revit_dynamo_run.",
            false, Obj(R("path", Str(".dyn file")), O("cli", Str("DynamoCLI.exe path (auto-detected)")), O("timeoutSec", Int("Timeout (default 300)"))),
            a =>
            {
                var cli = Json.Str(a, "cli") ?? FindDynamoCli() ?? throw new ToolException("DynamoCLI.exe not found. Install Dynamo Sandbox (Core) or pass cli=<path>.");
                var outJson = Path.Combine(BurakPaths.ScratchDir, "dynamo-" + Guid.NewGuid().ToString("N")[..8] + ".json");
                var psi = new System.Diagnostics.ProcessStartInfo(cli) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(Need(Json.Str(a, "path")));
                psi.ArgumentList.Add("-v"); psi.ArgumentList.Add(outJson);
                using var p = System.Diagnostics.Process.Start(psi)!;
                var so = p.StandardOutput.ReadToEndAsync(); var se = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit((int)Json.Int(a, "timeoutSec", 300) * 1000)) { try { p.Kill(true); } catch { } throw new ToolException("DynamoCLI timed out."); }
                return Json.O("exitCode", p.ExitCode, "results", File.Exists(outJson) ? Json.Parse(File.ReadAllText(outJson)) : null,
                    "log", Tail(so.Result + se.Result, 4000));
            });
    }

    static string Tail(string s, int n) => s.Length <= n ? s : s[^n..];

    static string? FindDynamoCli()
    {
        foreach (var drv in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var core = Path.Combine(drv.RootDirectory.FullName, @"Program Files\Autodesk\Dynamo\Core");
            if (!Directory.Exists(core)) continue;
            var hit = Directory.GetDirectories(core).OrderByDescending(x => x).Select(d => Path.Combine(d, "DynamoCLI.exe")).FirstOrDefault(File.Exists);
            if (hit != null) return hit;
        }
        foreach (var p in SoftwareDetector.Detect().Where(p => p.InstallDir != null && p.Name.Contains("Dynamo", StringComparison.OrdinalIgnoreCase)))
        {
            var f = Path.Combine(p.InstallDir!, "DynamoCLI.exe");
            if (File.Exists(f)) return f;
        }
        return null;
    }
}
