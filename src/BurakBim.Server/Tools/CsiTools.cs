using System.Diagnostics;
using BurakBim.Server.Com;
using BurakBim.Server.Core;
using BurakBim.Server.Detection;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>
/// 1.3: CSI connector - SAP2000, ETABS, CSiBridge and SAFE through the official CSI OAPI (COM, any version with API v1:
/// SAP2000 v20+, ETABS v17+, CSiBridge v20+, SAFE v20+). Attaches to the running program (or starts it).
/// Purpose-built tools for the common workflow (model, sections, loads, analysis, results, design) plus csi_call,
/// which reaches EVERY OAPI function with automatic handling of the API's by-reference outputs.
/// Values are in the model's present units (csi_units shows / sets them).
/// </summary>
public static class CsiTools
{
    sealed record CsiApp(string Id, string Name, string ProgId, string[] Helpers, string Process);

    static readonly CsiApp[] Apps =
    {
        new("etabs", "ETABS", "CSI.ETABS.API.ETABSObject", new[] { "CSiAPIv1.Helper", "ETABSv1.Helper" }, "ETABS"),
        new("sap2000", "SAP2000", "CSI.SAP2000.API.SapObject", new[] { "CSiAPIv1.Helper", "SAP2000v1.Helper" }, "SAP2000"),
        new("csibridge", "CSiBridge", "CSI.CSiBridge.API.SapObject", new[] { "CSiAPIv1.Helper", "CSiBridge1.Helper", "CSiBridgev1.Helper" }, "CSiBridge"),
        new("safe", "SAFE", "CSI.SAFE.API.ETABSObject", new[] { "CSiAPIv1.Helper", "SAFEv1.Helper" }, "SAFE"),
    };

    static readonly object Gate = new();
    static readonly Dictionary<string, object> Attached = new();
    static T Locked<T>(Func<T> f) { lock (Gate) return f(); }

    // eUnits (same numbers in every CSI product)
    static readonly Dictionary<string, int> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lb_in_F"] = 1, ["lb_ft_F"] = 2, ["kip_in_F"] = 3, ["kip_ft_F"] = 4, ["kN_mm_C"] = 5, ["kN_m_C"] = 6, ["kgf_mm_C"] = 7, ["kgf_m_C"] = 8,
        ["N_mm_C"] = 9, ["N_m_C"] = 10, ["Ton_mm_C"] = 11, ["Ton_m_C"] = 12, ["kN_cm_C"] = 13, ["kgf_cm_C"] = 14, ["N_cm_C"] = 15, ["Ton_cm_C"] = 16
    };

    static CsiApp Pick(Dictionary<string, object> a)
    {
        var id = (Json.Str(a, "app") ?? "").ToLowerInvariant().Replace(" ", "");
        if (id.Length > 0)
            return Apps.FirstOrDefault(x => x.Id == id || x.Name.Equals(id, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ToolException("app must be etabs, sap2000, csibridge or safe.");
        // the running one, else the installed one
        foreach (var x in Apps) if (Process.GetProcessesByName(x.Process).Length > 0) return x;
        var installed = SoftwareDetector.Detect().Select(p => p.Family).ToHashSet();
        return Apps.FirstOrDefault(x => installed.Contains(x.Id)) ?? throw new ToolException("No CSI program (ETABS, SAP2000, CSiBridge, SAFE) is running or installed. Start it, or pass app=etabs|sap2000|csibridge|safe.");
    }

    /// <summary>The CSI API object (cOAPI) of a running program; launch=true starts one.</summary>
    static dynamic Api(Dictionary<string, object> a)
    {
        if (!Config.AllowCom) throw new ToolException("COM automation is disabled (allowComAutomation=false).");
        var app = Pick(a);
        var pid = (int)Json.Int(a, "pid", 0);
        if (Attached.TryGetValue(app.Id, out var cached))
        {
            try { _ = ((dynamic)cached).SapModel; return cached; } catch { Attached.Remove(app.Id); }
        }
        object? obj = null; var errors = new List<string>();
        if (pid == 0)
        {
            try { obj = ComInterop.GetActive(app.ProgId); } catch (Exception ex) { errors.Add("GetActiveObject: " + ex.Message); }
        }
        if (obj == null)
        {
            foreach (var h in app.Helpers)
            {
                try
                {
                    var t = Type.GetTypeFromProgID(h); if (t == null) continue;
                    dynamic helper = Activator.CreateInstance(t)!;
                    if (pid > 0) obj = helper.GetObjectProcess(app.ProgId, pid);
                    else if (Process.GetProcessesByName(app.Process).Length > 0) obj = helper.GetObject(app.ProgId);
                    if (obj == null && Json.Bool(a, "launch"))
                    {
                        obj = helper.CreateObjectProgID(app.ProgId);
                        ((dynamic)obj!).ApplicationStart();
                    }
                    if (obj != null) break;
                }
                catch (Exception ex) { errors.Add(h + ": " + ex.Message); }
            }
        }
        if (obj == null)
        {
            var running = Process.GetProcessesByName(app.Process).Length > 0;
            var registered = Type.GetTypeFromProgID(app.ProgId) != null || app.Helpers.Any(h => Type.GetTypeFromProgID(h) != null);
            if (!registered)
                throw new ToolException($"The {app.Name} API is not registered on this PC. Run csi_register_api app={app.Id} (one-time, asks for administrator approval), then retry.");
            if (!running)
                throw new ToolException($"{app.Name} is not running. Start it with a model (burak_launch app={app.Id}) or call again with launch=true.");
            throw new ToolException($"Could not attach to the running {app.Name}: {string.Join("; ", errors)}. If several instances are open, pass pid. Make sure {app.Name} and the AI client run with the same rights (both normal, or both administrator).");
        }
        Attached[app.Id] = obj;
        return obj;
    }

    static dynamic Model(Dictionary<string, object> a) => Api(a).SapModel;

    /// <summary>Typed call on SapModel.&lt;path&gt;.Method with JSON args; returns {ret, outputs}.</summary>
    static Dictionary<string, object> Call(Dictionary<string, object> a, string path, object? args)
    {
        var api = (object)Api(a);
        var dot = path.LastIndexOf('.');
        var objPath = dot > 0 ? path[..dot] : "";
        var method = dot > 0 ? path[(dot + 1)..] : path;
        if (!objPath.StartsWith("SapModel", StringComparison.OrdinalIgnoreCase) && objPath.Length > 0 && !objPath.StartsWith("$c")) objPath = "SapModel." + objPath;
        var target = objPath.StartsWith("$c") ? ComInterop.FromHandle(objPath) : ComInterop.Eval(api, objPath) ?? throw new ToolException("Object path is null: " + objPath);
        return ComTyped.Call(target, method, args, api);
    }

    static void Ok(Dictionary<string, object> r, string what)
    {
        var ret = Json.Get(r, "ret");
        if (ret is int i && i != 0) throw new ToolException($"{what} failed (CSI returned {i}). Check names/units, and that the model is unlocked (csi_unlock) for edits.");
    }

    static List<string> Names(Dictionary<string, object> a, string obj)
    {
        var r = Call(a, obj + ".GetNameList", null);
        var o = Json.Obj(r, "outputs")!;
        var arr = o.Values.OfType<List<object?>>().FirstOrDefault() ?? new List<object?>();
        return arr.Select(x => x?.ToString() ?? "").ToList();
    }

    static Dictionary<string, object> Outs(Dictionary<string, object> r) => Json.Obj(r, "outputs") ?? new Dictionary<string, object>();
    static object? OutAt(Dictionary<string, object> r, int index) => Outs(r).Values.ElementAtOrDefault(index);

    static void Add(ToolRegistry r, string name, string title, string desc, bool ro, Dictionary<string, object> schema, Func<Dictionary<string, object>, object?> h, int prio = 0)
        => r.Add(new Tool { Name = name, Title = title, Toolset = "csi", Description = desc, ReadOnly = ro, InputSchema = schema, Handler = (x, c) => Locked(() => h(x)), Priority = prio });

    public static void Register(ToolRegistry r)
    {
        var appP = O("app", Enum("CSI program (default: the running one)", "etabs", "sap2000", "csibridge", "safe"));
        var pidP = O("pid", Int("Process id when several instances are open"));

        Add(r, "csi_status", "CSI status (SAP2000 / ETABS / CSiBridge / SAFE)",
            "Installed and running CSI programs, attach state, open model file, program version, present units, object counts and lock state. Start here for structural work.",
            true, Obj(appP, pidP, O("launch", Bool("Start the program if it is not running"))), a =>
            {
                var installed = SoftwareDetector.Detect().Where(p => p.Family is "sap2000" or "etabs" or "csibridge" or "safe")
                    .Select(p => (object)Json.O("name", p.Name, "family", p.Family, "exe", p.Exe, "running", Process.GetProcessesByName(Path.GetFileNameWithoutExtension(p.Exe ?? "x")).Length)).ToList();
                var d = Json.O("installed", installed);
                try
                {
                    var app = Pick(a);
                    var m = Model(a);
                    d["app"] = app.Name;
                    d["file"] = SafeStr(() => (string)m.GetModelFilename(true));
                    var v = Call(a, "SapModel.GetVersion", null); d["version"] = Outs(v).Values.FirstOrDefault();
                    d["units"] = UnitName(SafeInt(() => (int)m.GetPresentUnits()));
                    d["locked"] = SafeStr(() => ((bool)m.GetModelIsLocked()).ToString());
                    d["points"] = SafeInt(() => (int)m.PointObj.Count());
                    d["frames"] = SafeInt(() => (int)m.FrameObj.Count());
                    d["areas"] = SafeInt(() => (int)m.AreaObj.Count());
                    d["loadPatterns"] = Names(a, "LoadPatterns");
                    d["loadCases"] = Names(a, "LoadCases");
                    d["combos"] = Names(a, "RespCombo");
                }
                catch (ToolException ex) { d["attach"] = ex.Message; }
                return d;
            }, 95);

        Add(r, "csi_call", "CSI API: call ANY OAPI function",
            "Call any function of the CSI OAPI (SAP2000/ETABS/CSiBridge/SAFE) by path, e.g. 'FrameObj.GetSection', 'PropFrame.SetRectangle', 'Results.FrameForce', 'DesignConcrete.GetSummaryResultsBeam', 'Story.GetStories', 'File.Save'. " +
            "args: list in signature order (input parameters first; by-ref outputs can be omitted) or an object by parameter name. Enum parameters accept numbers or constant names (e.g. 'Dead', 'kN_m_C'). " +
            "Returns ret (0 = success), every by-ref output by name, and 'rows' when outputs are parallel arrays (results tables). Use csi_members to see signatures.",
            false, Obj(R("path", Str("Function path under SapModel, e.g. 'FrameObj.GetNameList' (or 'SapModel.Analyze.RunAnalysis')")),
                O("args", Any("Arguments: [..] positional or {name: value}")), appP, pidP, O("rowsLimit", Int("Max rows returned for result tables (default 2000)"))),
            a =>
            {
                var res = Call(a, Json.Str(a, "path") ?? throw new ToolException("path required"), Json.Get(a, "args"));
                var rows = ComTyped.Rows(Outs(res), (int)Json.Int(a, "rowsLimit", 2000));
                if (rows.Count > 0) res["rows"] = rows;
                return res;
            }, 90);

        Add(r, "csi_members", "CSI API: list functions and signatures",
            "Members of a CSI API object with parameter signatures, e.g. path='FrameObj', 'PropFrame', 'Results', 'DesignSteel', 'Story' (ETABS), 'LoadCases.StaticLinear'. search filters names; signature=Method shows one full signature (types, by-ref outputs).",
            true, Obj(O("path", Str("Object path under SapModel (default SapModel)")), O("search", Str("Filter member names")), O("signature", Str("Method name to show its full signature")), appP, pidP),
            a =>
            {
                var api = (object)Api(a);
                var path = Json.Str(a, "path") ?? "";
                if (!path.StartsWith("SapModel", StringComparison.OrdinalIgnoreCase)) path = path.Length == 0 ? "SapModel" : "SapModel." + path;
                var obj = ComInterop.Eval(api, path) ?? throw new ToolException("null: " + path);
                if (Json.Str(a, "signature") is { } m)
                    return Json.O("signature", ComTyped.GetSignature(obj, m)?.ToString() ?? "no type information for " + m);
                return ComInterop.Members(obj, Json.Str(a, "search"));
            });

        Add(r, "csi_register_api", "Register the CSI API (one-time)",
            "Runs RegisterSAP2000.exe / RegisterETABS.exe / RegisterCSiBridge.exe / RegisterSAFE.exe from the program folder so other programs (BURAK BIM) can connect. Windows asks the user for administrator approval.",
            false, Obj(R("app", Enum("Program", "etabs", "sap2000", "csibridge", "safe"))), a =>
            {
                var app = Pick(a);
                var prod = SoftwareDetector.Detect().Where(p => p.Family == app.Id && p.Exe != null).OrderByDescending(p => p.Year).FirstOrDefault()
                           ?? throw new ToolException(app.Name + " is not installed.");
                var dir = Path.GetDirectoryName(prod.Exe!)!;
                var reg = Directory.GetFiles(dir, "Register*.exe").FirstOrDefault(f => Path.GetFileName(f).Contains(app.Name, StringComparison.OrdinalIgnoreCase))
                          ?? Directory.GetFiles(dir, "Register*.exe").FirstOrDefault()
                          ?? throw new ToolException("No Register*.exe in " + dir + ". Reinstall/repair the program.");
                var p = Process.Start(new ProcessStartInfo(reg) { UseShellExecute = true, Verb = "runas", WorkingDirectory = dir });
                p?.WaitForExit(120000);
                return Json.O("ran", reg, "exitCode", p?.HasExited == true ? p.ExitCode : (int?)null, "next", "Call csi_status to connect.");
            });

        Add(r, "csi_units", "CSI units: show / set",
            "Show the present units, or set them (kN_m_C, kN_mm_C, N_mm_C, N_m_C, kgf_m_C, Ton_m_C, kip_ft_F, kip_in_F, lb_in_F, lb_ft_F, kN_cm_C ...). All BURAK BIM CSI tools use the present units.",
            false, Obj(O("set", Enum("New units", Units.Keys.ToArray())), appP, pidP), a =>
            {
                var m = Model(a);
                if (Json.Str(a, "set") is { } u)
                {
                    if (!Units.TryGetValue(u, out var code)) throw new ToolException("Unknown units " + u);
                    int ret = m.SetPresentUnits(code);
                    if (ret != 0) throw new ToolException("SetPresentUnits failed.");
                }
                return Json.O("units", UnitName((int)m.GetPresentUnits()));
            });

        Add(r, "csi_file", "CSI model file: new / open / save / save as",
            "new: blank model (or grid-only for ETABS/SAP2000 with units), open: .sdb/.edb/.bdb/.fdb path, save (current file), saveas: path.",
            false, Obj(R("action", Enum("Action", "new", "open", "save", "saveas")), O("path", Str("File path for open / saveas")), O("units", Enum("Units for new", Units.Keys.ToArray())),
                appP, pidP, O("launch", Bool("Start the program if needed"))), a =>
            {
                var m = Model(a);
                int ret;
                switch (Json.Str(a, "action"))
                {
                    case "new":
                        ret = m.InitializeNewModel(Units.TryGetValue(Json.Str(a, "units") ?? "kN_m_C", out var uc) ? uc : 6);
                        if (ret == 0) ret = m.File.NewBlank();
                        break;
                    case "open": ret = m.File.OpenFile(Json.Str(a, "path") ?? throw new ToolException("path required")); break;
                    case "save": ret = m.File.Save(""); break;
                    case "saveas": ret = m.File.Save(Json.Str(a, "path") ?? throw new ToolException("path required")); break;
                    default: throw new ToolException("action: new | open | save | saveas");
                }
                if (ret != 0) throw new ToolException($"File {Json.Str(a, "action")} failed (ret {ret}).");
                return Json.O("ok", true, "file", SafeStr(() => (string)m.GetModelFilename(true)));
            });

        Add(r, "csi_unlock", "CSI: unlock model for editing", "Unlock the model (after analysis, edits need an unlocked model; this deletes analysis results).",
            false, Obj(appP, pidP), a => { var m = Model(a); int ret = m.SetModelIsLocked(false); return Json.O("ok", ret == 0, "locked", (bool)m.GetModelIsLocked()); });

        // ------------------------------------------------------------------ geometry
        Add(r, "csi_points", "CSI joints: list / add / restraints",
            "list: joints with coordinates (and restraints). add: points [[x,y,z],...] (present units). restrain: names + dof [U1,U2,U3,R1,R2,R3] booleans, or preset fixed|pinned|roller|free.",
            false, Obj(R("action", Enum("Action", "list", "add", "restrain")), O("points", Arr("[[x,y,z], ...] for add")), O("names", Arr("Joint names", Json.O("type", "string"))),
                O("preset", Enum("Restraint preset", "fixed", "pinned", "roller", "free")), O("dof", Arr("[U1,U2,U3,R1,R2,R3] booleans", Json.O("type", "boolean"))),
                O("limit", Int("Max listed (default 1000)")), appP, pidP), a =>
            {
                switch (Json.Str(a, "action"))
                {
                    case "list":
                        var list = new List<object>();
                        foreach (var n in Names(a, "PointObj").Take((int)Json.Int(a, "limit", 1000)))
                        {
                            var c = Outs(Call(a, "PointObj.GetCoordCartesian", new List<object> { n })).Values.Take(3).ToList();
                            var rs = OutAt(Call(a, "PointObj.GetRestraint", new List<object> { n }), 0);
                            list.Add(Json.O("name", n, "xyz", c, "restraint", rs));
                        }
                        return Json.O("count", list.Count, "points", list);
                    case "add":
                        var made = new List<object>();
                        foreach (var p in Json.Arr(a, "points") ?? throw new ToolException("points required"))
                        {
                            var v = Json.Vec(p) ?? throw new ToolException("point must be [x,y,z]");
                            var res = Call(a, "PointObj.AddCartesian", new List<object> { v[0], v[1], v[2] });
                            Ok(res, "AddCartesian"); made.Add(OutAt(res, 0)!);
                        }
                        return Json.O("added", made);
                    case "restrain":
                        var dof = Json.Get(a, "dof") is List<object> dl ? dl.Select(x => (object)(x is bool bb ? bb : Json.ToDouble(x) != 0)).ToList()
                            : (Json.Str(a, "preset") ?? "fixed") switch
                            {
                                "pinned" => new List<object> { true, true, true, false, false, false },
                                "roller" => new List<object> { false, false, true, false, false, false },
                                "free" => new List<object> { false, false, false, false, false, false },
                                _ => new List<object> { true, true, true, true, true, true }
                            };
                        var done = new List<object>();
                        foreach (var n in Json.StrList(a, "names")) { Ok(Call(a, "PointObj.SetRestraint", new List<object> { n, dof }), "SetRestraint " + n); done.Add(n); }
                        return Json.O("restrained", done, "dof", dof);
                }
                throw new ToolException("action: list | add | restrain");
            });

        Add(r, "csi_frames", "CSI frames (beams/columns/braces): list / add / set section / delete",
            "list: frames with end joints, section and length. add: segments [[x1,y1,z1,x2,y2,z2], ...] with section. section: assign a section to names. delete: names.",
            false, Obj(R("action", Enum("Action", "list", "add", "section", "delete")), O("segments", Arr("[[x1,y1,z1,x2,y2,z2], ...] for add")), O("section", Str("Frame section name")),
                O("names", Arr("Frame names", Json.O("type", "string"))), O("limit", Int("Max listed (default 1000)")), appP, pidP), a =>
            {
                switch (Json.Str(a, "action"))
                {
                    case "list":
                        var list = new List<object>();
                        foreach (var n in Names(a, "FrameObj").Take((int)Json.Int(a, "limit", 1000)))
                        {
                            var pts = Outs(Call(a, "FrameObj.GetPoints", new List<object> { n })).Values.Take(2).ToList();
                            var sec = OutAt(Call(a, "FrameObj.GetSection", new List<object> { n }), 0);
                            list.Add(Json.O("name", n, "joints", pts, "section", sec));
                        }
                        return Json.O("count", list.Count, "frames", list);
                    case "add":
                        var made = new List<object>();
                        var secName = Json.Str(a, "section") ?? "Default";
                        foreach (var sObj in Json.Arr(a, "segments") ?? throw new ToolException("segments required"))
                        {
                            var c = (sObj as List<object>)?.Select(x => Json.ToDouble(x)).ToList();
                            if (c == null || c.Count != 6) throw new ToolException("Each segment is [x1,y1,z1,x2,y2,z2].");
                            var res = Call(a, "FrameObj.AddByCoord", new List<object> { c[0], c[1], c[2], c[3], c[4], c[5], "", secName });
                            Ok(res, "AddByCoord"); made.Add(OutAt(res, 0)!);
                        }
                        return Json.O("added", made, "section", secName);
                    case "section":
                        var sec2 = Json.Str(a, "section") ?? throw new ToolException("section required");
                        foreach (var n in Json.StrList(a, "names")) Ok(Call(a, "FrameObj.SetSection", new List<object> { n, sec2 }), "SetSection " + n);
                        return Json.O("ok", true);
                    case "delete":
                        foreach (var n in Json.StrList(a, "names")) Ok(Call(a, "FrameObj.Delete", new List<object> { n }), "Delete " + n);
                        return Json.O("deleted", Json.StrList(a, "names"));
                }
                throw new ToolException("action: list | add | section | delete");
            });

        Add(r, "csi_areas", "CSI areas (slabs/walls/shells): list / add",
            "list: area objects with their joints and property. add: polygons [[[x,y,z],...], ...] with property name.",
            false, Obj(R("action", Enum("Action", "list", "add")), O("polygons", Arr("[[[x,y,z],...], ...]")), O("property", Str("Area property (slab/wall/shell) name")),
                O("limit", Int("Max listed (default 500)")), appP, pidP), a =>
            {
                if (Json.Str(a, "action") == "list")
                {
                    var list = new List<object>();
                    foreach (var n in Names(a, "AreaObj").Take((int)Json.Int(a, "limit", 500)))
                    {
                        var pts = Outs(Call(a, "AreaObj.GetPoints", new List<object> { n }));
                        var prop = OutAt(Call(a, "AreaObj.GetProperty", new List<object> { n }), 0);
                        list.Add(Json.O("name", n, "joints", pts.Values.ElementAtOrDefault(1), "property", prop));
                    }
                    return Json.O("count", list.Count, "areas", list);
                }
                var made = new List<object>();
                foreach (var poly in Json.Arr(a, "polygons") ?? throw new ToolException("polygons required"))
                {
                    var pts = (poly as List<object>)?.Select(Json.Vec).Where(v => v != null).ToList() ?? new List<double[]?>();
                    if (pts.Count < 3) throw new ToolException("A polygon needs at least 3 points.");
                    var res = Call(a, "AreaObj.AddByCoord", new List<object> { pts.Count,
                        pts.Select(p => (object)p![0]).ToList(), pts.Select(p => (object)p![1]).ToList(), pts.Select(p => (object)p![2]).ToList(), "", Json.Str(a, "property") ?? "Default" });
                    Ok(res, "AddByCoord");
                    made.Add(Outs(res).Values.ElementAtOrDefault(3) ?? "");
                }
                return Json.O("added", made);
            });

        Add(r, "csi_sections", "CSI materials and sections: list / add",
            "list: materials, frame sections and area properties. add_material: name + type (Steel, Concrete, Rebar, Tendon, Aluminum, ColdFormed, NoDesign). " +
            "add_rectangle: concrete/steel rectangle (material, depth t3, width t2). add_circle: (material, diameter). Use csi_call for I-sections, imports from section libraries (PropFrame.ImportProp) etc.",
            false, Obj(R("action", Enum("Action", "list", "add_material", "add_rectangle", "add_circle")), O("name", Str("Name")), O("material", Str("Material name")),
                O("materialType", Enum("Material type", "Steel", "Concrete", "NoDesign", "Aluminum", "ColdFormed", "Rebar", "Tendon")),
                O("depth", Num("t3 (depth)")), O("width", Num("t2 (width)")), O("diameter", Num("Diameter")), appP, pidP), a =>
            {
                var name = Json.Str(a, "name");
                switch (Json.Str(a, "action"))
                {
                    case "list":
                        return Json.O("materials", Names(a, "PropMaterial"), "frameSections", Names(a, "PropFrame"), "areaProperties", SafeList(() => Names(a, "PropArea")));
                    case "add_material":
                        var types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Steel"] = 1, ["Concrete"] = 2, ["NoDesign"] = 3, ["Aluminum"] = 4, ["ColdFormed"] = 5, ["Rebar"] = 6, ["Tendon"] = 7 };
                        Ok(Call(a, "PropMaterial.SetMaterial", new List<object> { name ?? throw new ToolException("name required"), types.TryGetValue(Json.Str(a, "materialType") ?? "Concrete", out var mt) ? mt : 2 }), "SetMaterial");
                        return Json.O("ok", true, "material", name);
                    case "add_rectangle":
                        Ok(Call(a, "PropFrame.SetRectangle", new List<object> { name ?? throw new ToolException("name required"), Json.Str(a, "material") ?? throw new ToolException("material required"), Json.Num(a, "depth"), Json.Num(a, "width") }), "SetRectangle");
                        return Json.O("ok", true, "section", name);
                    case "add_circle":
                        Ok(Call(a, "PropFrame.SetCircle", new List<object> { name ?? throw new ToolException("name required"), Json.Str(a, "material") ?? throw new ToolException("material required"), Json.Num(a, "diameter") }), "SetCircle");
                        return Json.O("ok", true, "section", name);
                }
                throw new ToolException("action: list | add_material | add_rectangle | add_circle");
            });

        // ------------------------------------------------------------------ loads
        Add(r, "csi_loads", "CSI loads: patterns, combinations, assign loads",
            "add_pattern: name + type (Dead, SuperDead, Live, ReduceLive, Quake, Wind, Snow, Other, RoofLive ...) + selfWeight multiplier. " +
            "add_combo: name + cases {name: scale} (+ comboType linear|envelope|abs|srss). point_load: joints + [F1,F2,F3,M1,M2,M3]. " +
            "frame_udl: frames + value + direction (gravity=10, global X=4, Y=5, Z=6, local 1-3). area_uniform: areas + value + direction.",
            false, Obj(R("action", Enum("Action", "add_pattern", "add_combo", "point_load", "frame_udl", "area_uniform")), O("name", Str("Pattern / combo name")),
                O("type", Str("Pattern type name (Dead, Live, Wind, Quake ...)")), O("selfWeight", Num("Self-weight multiplier (default 0, 1 for Dead)")),
                O("cases", AnyObj("Combo: {caseOrComboName: scaleFactor}")), O("comboType", Enum("Combo type", "linear", "envelope", "abs", "srss", "range")),
                O("pattern", Str("Load pattern for assignments")), O("names", Arr("Object names", Json.O("type", "string"))), O("forces", Arr("[F1,F2,F3,M1,M2,M3]", Json.O("type", "number"))),
                O("value", Num("Load value (force/length or force/area)")), O("direction", Int("Load direction code (default 10 = gravity)")), O("replace", Bool("Replace existing loads (default true)")),
                appP, pidP), a =>
            {
                var pattern = Json.Str(a, "pattern") ?? Json.Str(a, "name");
                bool replace = Json.Bool(a, "replace", true);
                switch (Json.Str(a, "action"))
                {
                    case "add_pattern":
                        var pTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Dead"] = 1, ["SuperDead"] = 2, ["Live"] = 3, ["ReduceLive"] = 4, ["Quake"] = 5, ["Wind"] = 6, ["Snow"] = 7, ["Other"] = 8, ["Move"] = 9, ["Temperature"] = 10, ["RoofLive"] = 11, ["Notional"] = 12 };
                        var t = Json.Str(a, "type") ?? "Dead";
                        Ok(Call(a, "LoadPatterns.Add", new List<object> { Json.Str(a, "name") ?? throw new ToolException("name required"), pTypes.TryGetValue(t, out var tc) ? tc : (object)t, Json.Num(a, "selfWeight", 0), true }), "LoadPatterns.Add");
                        return Json.O("ok", true, "patterns", Names(a, "LoadPatterns"));
                    case "add_combo":
                        var cname = Json.Str(a, "name") ?? throw new ToolException("name required");
                        var ct = (Json.Str(a, "comboType") ?? "linear") switch { "envelope" => 1, "abs" => 2, "srss" => 3, "range" => 4, _ => 0 };
                        Ok(Call(a, "RespCombo.Add", new List<object> { cname, ct }), "RespCombo.Add");
                        var cases = Names(a, "LoadCases").ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (var kv in Json.Obj(a, "cases") ?? new Dictionary<string, object>())
                            Ok(Call(a, "RespCombo.SetCaseList", new List<object> { cname, cases.Contains(kv.Key) ? 0 : 1, kv.Key, Json.ToDouble(kv.Value, 1) }), "SetCaseList " + kv.Key);
                        return Json.O("ok", true, "combo", cname);
                    case "point_load":
                        var f = Json.Arr(a, "forces") ?? throw new ToolException("forces [F1,F2,F3,M1,M2,M3] required");
                        foreach (var n in Json.StrList(a, "names")) Ok(Call(a, "PointObj.SetLoadForce", new List<object> { n, pattern ?? throw new ToolException("pattern required"), f, replace }), "SetLoadForce " + n);
                        return Json.O("ok", true);
                    case "frame_udl":
                        foreach (var n in Json.StrList(a, "names"))
                            Ok(Call(a, "FrameObj.SetLoadDistributed", new List<object> { n, pattern ?? throw new ToolException("pattern required"), 1, (int)Json.Int(a, "direction", 10), 0.0, 1.0, Json.Num(a, "value"), Json.Num(a, "value"), "Global", true, replace }), "SetLoadDistributed " + n);
                        return Json.O("ok", true);
                    case "area_uniform":
                        foreach (var n in Json.StrList(a, "names"))
                            Ok(Call(a, "AreaObj.SetLoadUniform", new List<object> { n, pattern ?? throw new ToolException("pattern required"), Json.Num(a, "value"), (int)Json.Int(a, "direction", 10), replace, "Global" }), "SetLoadUniform " + n);
                        return Json.O("ok", true);
                }
                throw new ToolException("action: add_pattern | add_combo | point_load | frame_udl | area_uniform");
            });

        // ------------------------------------------------------------------ analysis + results + design
        Add(r, "csi_analyze", "CSI: run analysis",
            "Run the analysis (the model must be saved; give path if it was never saved). cases: only these load cases (others are not run).",
            false, Obj(O("cases", Arr("Load cases to run (default all)", Json.O("type", "string"))), O("path", Str("Save as this file first (if the model is new)")), appP, pidP), a =>
            {
                var m = Model(a);
                if (Json.Str(a, "path") is { } p) { int s = m.File.Save(p); if (s != 0) throw new ToolException("Save failed: " + p); }
                var cases = Json.StrList(a, "cases");
                if (cases.Count > 0)
                {
                    Call(a, "Analyze.SetRunCaseFlag", new List<object> { "", false, true });
                    foreach (var c in cases) Ok(Call(a, "Analyze.SetRunCaseFlag", new List<object> { c, true, false }), "SetRunCaseFlag " + c);
                }
                var sw = Stopwatch.StartNew();
                int ret = m.Analyze.RunAnalysis();
                if (ret != 0) throw new ToolException($"RunAnalysis returned {ret}. Check the model (warnings in the analysis log) and that it was saved.");
                return Json.O("ok", true, "seconds", Math.Round(sw.Elapsed.TotalSeconds, 1), "file", SafeStr(() => (string)m.GetModelFilename(true)));
            });

        Add(r, "csi_results", "CSI results: reactions, displacements, forces, modal, drifts",
            "Analysis results as tables for selected cases/combos: base_reactions, joint_reactions, joint_displacements, frame_forces, area_forces, modal_periods, modal_participation, story_drifts (ETABS), story_forces (ETABS). " +
            "name = object/group name ('ALL' group default). Results are in present units.",
            true, Obj(R("kind", Enum("Result", "base_reactions", "joint_reactions", "joint_displacements", "frame_forces", "area_forces", "modal_periods", "modal_participation", "story_drifts", "story_forces")),
                O("cases", Arr("Load cases for output", Json.O("type", "string"))), O("combos", Arr("Load combinations for output", Json.O("type", "string"))),
                O("name", Str("Object or group name (default group ALL)")), O("limit", Int("Max rows (default 2000)")), appP, pidP), a =>
            {
                Call(a, "Results.Setup.DeselectAllCasesAndCombosForOutput", null);
                foreach (var c in Json.StrList(a, "cases")) Ok(Call(a, "Results.Setup.SetCaseSelectedForOutput", new List<object> { c, true }), "select case " + c);
                foreach (var c in Json.StrList(a, "combos")) Ok(Call(a, "Results.Setup.SetComboSelectedForOutput", new List<object> { c, true }), "select combo " + c);
                var name = Json.Str(a, "name") ?? "ALL";
                const int groupElm = 2; // eItemTypeElm.GroupElm
                var kind = Json.Str(a, "kind");
                Dictionary<string, object> res = kind switch
                {
                    "base_reactions" => Call(a, "Results.BaseReact", null),
                    "joint_reactions" => Call(a, "Results.JointReact", new List<object> { name, groupElm }),
                    "joint_displacements" => Call(a, "Results.JointDispl", new List<object> { name, groupElm }),
                    "frame_forces" => Call(a, "Results.FrameForce", new List<object> { name, groupElm }),
                    "area_forces" => Call(a, "Results.AreaForceShell", new List<object> { name, groupElm }),
                    "modal_periods" => Call(a, "Results.ModalPeriod", null),
                    "modal_participation" => Call(a, "Results.ModalParticipatingMassRatios", null),
                    "story_drifts" => Call(a, "Results.StoryDrifts", null),
                    "story_forces" => Call(a, "Results.StoryForces", null),
                    _ => throw new ToolException("Unknown kind")
                };
                var rows = ComTyped.Rows(Outs(res), (int)Json.Int(a, "limit", 2000));
                if (Json.Get(res, "ret") is int rc && rc != 0 && rows.Count == 0)
                    throw new ToolException($"No results (ret {rc}). Run csi_analyze first and select cases/combos that were run.");
                return Json.O("kind", kind, "units", SafeStr(() => UnitName((int)Model(a).GetPresentUnits())), "rows", rows, "count", rows.Count);
            });

        Add(r, "csi_design", "CSI design: steel / concrete / composite - run and summarize",
            "start: run the design (after analysis). summary: per-member results (ratios, governing combo, errors/warnings) for 'name' or group ALL. Uses DesignSteel / DesignConcrete / DesignCompositeBeam.",
            false, Obj(R("type", Enum("Design", "steel", "concrete", "composite")), R("action", Enum("Action", "start", "summary")), O("name", Str("Frame or group (default ALL)")),
                O("limit", Int("Max rows (default 1000)")), appP, pidP), a =>
            {
                var obj = (Json.Str(a, "type") ?? "steel") switch { "concrete" => "DesignConcrete", "composite" => "DesignCompositeBeam", _ => "DesignSteel" };
                if (Json.Str(a, "action") == "start")
                {
                    var rs = Call(a, obj + ".StartDesign", null); Ok(rs, obj + ".StartDesign");
                    return Json.O("ok", true, "design", obj);
                }
                var name = Json.Str(a, "name") ?? "ALL";
                Dictionary<string, object> res;
                try { res = Call(a, obj + ".GetSummaryResults", new List<object> { name }); }
                catch (ToolException) { res = Call(a, obj + ".GetSummaryResultsBeam", new List<object> { name }); }
                var rows = ComTyped.Rows(Outs(res), (int)Json.Int(a, "limit", 1000));
                return Json.O("design", obj, "rows", rows, "count", rows.Count, "ret", Json.Get(res, "ret"));
            });

        Add(r, "csi_select", "CSI: select / clear selection / groups",
            "Select objects in the CSI view (so the user sees them): names of frames/areas/points, a group, or clear. Also lists groups.",
            false, Obj(R("action", Enum("Action", "select", "clear", "groups")), O("kind", Enum("Object type", "frame", "area", "point")), O("names", Arr("Names", Json.O("type", "string"))),
                O("group", Str("Group name to select")), appP, pidP), a =>
            {
                switch (Json.Str(a, "action"))
                {
                    case "clear": Call(a, "SelectObj.ClearSelection", null); return Json.O("ok", true);
                    case "groups": return Json.O("groups", Names(a, "GroupDef"));
                }
                if (Json.Str(a, "group") is { } g) { Ok(Call(a, "SelectObj.Group", new List<object> { g }), "Select group"); }
                var obj = (Json.Str(a, "kind") ?? "frame") switch { "area" => "AreaObj", "point" => "PointObj", _ => "FrameObj" };
                foreach (var n in Json.StrList(a, "names")) Call(a, obj + ".SetSelected", new List<object> { n, true });
                try { Call(a, "View.RefreshView", new List<object> { 0, false }); } catch { }
                return Json.O("ok", true);
            });
    }

    static string UnitName(int code) => Units.FirstOrDefault(kv => kv.Value == code).Key ?? code.ToString();
    static string? SafeStr(Func<string> f) { try { return f(); } catch { return null; } }
    static int SafeInt(Func<int> f) { try { return f(); } catch { return -1; } }
    static object SafeList(Func<List<string>> f) { try { return f(); } catch (Exception ex) { return "n/a: " + ex.Message; } }
}
