using System.Diagnostics;
using BurakBim.Server.Com;
using BurakBim.Server.Core;
using BurakBim.Server.Detection;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>
/// 1.3: PlanSwift connector (PlanSwift 9, 10, 11 Professional / Standard) through PlanSwift's COM/OLE API
/// ("PlanSwift9.PlanCenter"; IPlanSwift / IItem / IPropertyObject). PlanSwift keeps everything in one item tree:
/// \Job (pages, takeoff folders and items, estimating), \Settings, templates. These tools walk that tree, read
/// takeoff results (Area, Linear Total, Qty, Segment Count, Volume ...), set formulas, create items and total the job.
/// Drawing takeoffs on the page canvas: capture app=planswift + computer_* tools (with the user's permission).
/// </summary>
public static class PlanSwiftTools
{
    static readonly string[] ProgIds = { "PlanSwift9.PlanCenter", "PlanSwift11.PlanCenter", "PlanSwift10.PlanCenter", "PlanSwift.PlanCenter" };
    static readonly object Gate = new();
    static object? _app;

    static object App(Dictionary<string, object> a)
    {
        if (!Config.AllowCom) throw new ToolException("COM automation is disabled (allowComAutomation=false).");
        lock (Gate)
        {
            if (_app != null) { try { ComInterop.Get(_app, "CurrentVersion"); return _app; } catch { _app = null; } }
            var running = Process.GetProcesses().Any(p => { try { return p.ProcessName.StartsWith("PlanSwift", StringComparison.OrdinalIgnoreCase); } catch { return false; } });
            if (!running && !Json.Bool(a, "launch"))
                throw new ToolException("PlanSwift is not running. Start it and open a job (or call again with launch=true).");
            foreach (var id in ProgIds)
            {
                try
                {
                    var o = ComInterop.GetActive(id);
                    if (o == null)
                    {
                        var t = Type.GetTypeFromProgID(id); if (t == null) continue;
                        o = Activator.CreateInstance(t); // PlanSwift's OLE server returns the running application
                    }
                    if (o != null) { _app = o; return o; }
                }
                catch { }
            }
            throw new ToolException("PlanSwift's API (PlanSwift9.PlanCenter) is not registered. Run PlanSwift once as administrator, or repair the installation. Meanwhile use capture app=planswift + computer_* tools.");
        }
    }

    static object Item(Dictionary<string, object> a, string path)
    {
        var app = App(a);
        var it = ComInterop.Call(app, "GetItem", path) ?? throw new ToolException($"No PlanSwift item at '{path}'. Use planswift_tree from \\Job.");
        return it;
    }

    static string? S2(object o, string member) { try { return Convert.ToString(ComInterop.Get(o, member)); } catch { return null; } }
    static int I2(object o, string member) { try { return Convert.ToInt32(ComInterop.Get(o, member)); } catch { return 0; } }

    static string? Prop(object item, string name)
    {
        try { return Convert.ToString(ComInterop.Call(item, "GetPropertyResultAsString", name)); }
        catch { try { var p = ComInterop.Call(item, "GetProperty", name); return p == null ? null : Convert.ToString(ComInterop.Call(p, "ResultAsString")); } catch { return null; } }
    }

    static readonly string[] Quantities = { "Qty", "Quantity", "Count", "Area", "Linear Total", "Length", "Perimeter", "Volume", "Segment Count", "Section Count", "Total", "Price Each", "Cost Total", "Price Total", "Units", "Height", "Depth", "Width" };

    static Dictionary<string, object> Describe(object item, bool withProps, IEnumerable<string>? props = null)
    {
        var d = Json.O("name", S2(item, "Name"), "type", S2(item, "ItemType"), "path", S2(item, "FullPath"), "guid", S2(item, "GUID"), "children", I2(item, "ChildCount"));
        if (withProps)
        {
            var q = new Dictionary<string, object>();
            foreach (var p in props ?? Quantities)
            {
                var v = Prop(item, p);
                if (!string.IsNullOrEmpty(v)) q[p] = v;
            }
            if (q.Count > 0) d["values"] = q;
        }
        return d;
    }

    static void Add(ToolRegistry r, string name, string title, string desc, bool ro, Dictionary<string, object> schema, Func<Dictionary<string, object>, object?> h)
        => r.Add(new Tool { Name = name, Title = title, Toolset = "planswift", Description = desc, ReadOnly = ro, InputSchema = schema, Handler = (x, c) => { lock (Gate) return h(x); } });

    public static void Register(ToolRegistry r)
    {
        var launch = O("launch", Bool("Start PlanSwift if it is not running"));

        Add(r, "planswift_status", "PlanSwift status",
            "Installed/running PlanSwift, API connection, version, and the top level of the open job.", true, Obj(launch), a =>
            {
                var installed = SoftwareDetector.Detect().Where(p => p.Family == "planswift").Select(p => (object)Json.O("name", p.Name, "exe", p.Exe, "version", p.Version)).ToList();
                var d = Json.O("installed", installed);
                try
                {
                    var app = App(a);
                    d["version"] = S2(app, "CurrentVersion"); d["edition"] = S2(app, "Edition");
                    var job = ComInterop.Call(app, "GetItem", "\\Job");
                    if (job != null)
                    {
                        d["job"] = Describe(job, false);
                        var kids = new List<object>();
                        for (int i = 0; i < I2(job, "ChildCount") && i < 50; i++) { var c = ComInterop.Call(job, "ChildItem", i); if (c != null) kids.Add(Describe(c, false)); }
                        d["jobChildren"] = kids;
                    }
                }
                catch (ToolException ex) { d["connection"] = ex.Message; }
                return d;
            });

        Add(r, "planswift_tree", "PlanSwift item tree (job, pages, takeoff, estimating)",
            "Walk PlanSwift's item tree from a path (default \\Job): names, types, full paths and takeoff quantities (Area, Linear Total, Qty, Volume, Count ...).",
            true, Obj(O("path", Str("Start path, e.g. \\Job, \\Job\\Takeoff, \\Job\\Pages")), O("depth", Int("Levels (default 3)")), O("quantities", Bool("Include quantity values (default true)")),
                O("limit", Int("Max items (default 500)")), launch), a =>
            {
                var root = Item(a, Json.Str(a, "path") ?? "\\Job");
                int depth = (int)Json.Int(a, "depth", 3), limit = (int)Json.Int(a, "limit", 500); bool q = Json.Bool(a, "quantities", true);
                var outp = new List<object>();
                void Walk(object it, int level)
                {
                    if (outp.Count >= limit) return;
                    var d = Describe(it, q); d["level"] = level; outp.Add(d);
                    if (level >= depth) return;
                    for (int i = 0; i < I2(it, "ChildCount") && outp.Count < limit; i++)
                    {
                        object? c = null; try { c = ComInterop.Call(it, "ChildItem", i); } catch { }
                        if (c != null) Walk(c, level + 1);
                    }
                }
                Walk(root, 0);
                return Json.O("count", outp.Count, "items", outp);
            });

        Add(r, "planswift_item", "PlanSwift item: all properties",
            "All properties of one item (by full path or GUID) with results and formulas.", true,
            Obj(R("path", Str("Item full path, e.g. \\Job\\Takeoff\\Walls\\Interior Wall")), launch), a =>
            {
                var it = Item(a, Json.Str(a, "path")!);
                var d = Describe(it, false);
                var props = new List<object>();
                for (int i = 0; i < I2(it, "PropertyCount") && i < 400; i++)
                {
                    try
                    {
                        var p = ComInterop.Call(it, "PropertyItem", i); if (p == null) continue;
                        var name = S2(p, "Name");
                        props.Add(Json.O("name", name, "result", name == null ? null : Prop(it, name), "formula", S2(p, "Formula"), "units", S2(p, "InputUnits")));
                    }
                    catch { }
                }
                d["properties"] = props;
                return d;
            });

        Add(r, "planswift_set", "PlanSwift: set a property formula/value",
            "Set a property of an item (value or formula, e.g. Height = 8, Waste % = 10, Price Each = 12.5). PlanSwift recalculates the takeoff and estimate.",
            false, Obj(R("path", Str("Item full path")), R("property", Str("Property name")), R("formula", Str("New value or formula")), launch), a =>
            {
                var it = Item(a, Json.Str(a, "path")!);
                var prop = Json.Str(a, "property")!;
                ComInterop.Call(it, "SetPropertyFormula", prop, Json.Str(a, "formula") ?? "");
                return Json.O("ok", true, "property", prop, "result", Prop(it, prop));
            });

        Add(r, "planswift_new_item", "PlanSwift: create a takeoff item / folder",
            "Create a child item under a parent path: type Area, Linear, Segment, Count, Folder (or any PlanSwift item type) with a name, and optional properties {name: formula}. Then draw it on the page with computer_* tools if needed.",
            false, Obj(R("parent", Str("Parent path, e.g. \\Job\\Takeoff")), R("type", Str("Area | Linear | Segment | Count | Folder | ...")), R("name", Str("Item name")),
                O("properties", AnyObj("{propertyName: formula}")), launch), a =>
            {
                var parent = Item(a, Json.Str(a, "parent")!);
                var it = ComInterop.Call(parent, "NewItem", Json.Str(a, "type")!, Json.Str(a, "name")!) ?? throw new ToolException("NewItem returned nothing.");
                foreach (var kv in Json.Obj(a, "properties") ?? new Dictionary<string, object>())
                    ComInterop.Call(it, "SetPropertyFormula", kv.Key, Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "");
                return Describe(it, true);
            });

        Add(r, "planswift_quantities", "PlanSwift takeoff quantities report",
            "Flat list of every takeoff item under a path (default \\Job) with its quantities - ready for an estimate or Excel.", true,
            Obj(O("path", Str("Start path (default \\Job)")), O("properties", Arr("Which properties (default Qty, Area, Linear Total, Volume, Count, Segment Count, Price Total ...)", Json.O("type", "string"))),
                O("limit", Int("Max items (default 2000)")), launch), a =>
            {
                var root = Item(a, Json.Str(a, "path") ?? "\\Job");
                var props = Json.StrList(a, "properties"); var use = props.Count > 0 ? props : Quantities.ToList();
                int limit = (int)Json.Int(a, "limit", 2000);
                var rows = new List<object>();
                void Walk(object it, int lvl)
                {
                    if (rows.Count >= limit || lvl > 12) return;
                    var d = Describe(it, true, use);
                    if (Json.Get(d, "values") != null && lvl > 0) rows.Add(d);
                    for (int i = 0; i < I2(it, "ChildCount") && rows.Count < limit; i++)
                    {
                        object? c = null; try { c = ComInterop.Call(it, "ChildItem", i); } catch { }
                        if (c != null) Walk(c, lvl + 1);
                    }
                }
                Walk(root, 0);
                return Json.O("count", rows.Count, "items", rows);
            });

        Add(r, "planswift_call", "PlanSwift API: call any method",
            "Call any method/property of the PlanSwift API on the application (target omitted) or on an item path (target=item path): e.g. method=GetJobTotal, CloseJob, DeleteItem, GetZoom, CurrentViewport.",
            false, Obj(R("method", Str("Method or property name")), O("target", Str("Item path (omit = application)")), O("args", Arr("Arguments")), launch), a =>
            {
                var target = Json.Str(a, "target") is { } t ? Item(a, t) : App(a);
                var res = ComTyped.Call(target, Json.Str(a, "method")!, Json.Get(a, "args") ?? new List<object>());
                return res;
            });
    }
}
