using System.Runtime.InteropServices;
using BurakBim.Server.Com;
using BurakBim.Server.Core;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>
/// AutoCAD ActiveX (COM) tools - work with AutoCAD and EVERY AutoCAD-based product (Civil 3D, Plant 3D, Map 3D,
/// Architecture, MEP, Electrical, Mechanical, Advance Steel), any version, with or without the in-process bridge.
/// Entities are addressed by their handle (hex string).
/// </summary>
public static class AcadComTools
{
    static readonly object Gate = new();

    /// <summary>COM calls into a busy AutoCAD are rejected (RPC_E_CALL_REJECTED / RPC_E_SERVERCALL_RETRYLATER): retry briefly.</summary>
    internal static T Retry<T>(Func<T> f)
    {
        lock (Gate)
        {
            for (int i = 0; ; i++)
            {
                try { return f(); }
                catch (COMException ex) when ((uint)ex.HResult is 0x80010001 or 0x8001010A && i < 40) { Thread.Sleep(250); }
            }
        }
    }

    static dynamic App(Dictionary<string, object> a)
    {
        var prog = Json.Str(a, "progId") ?? "AutoCAD.Application";
        return ComInterop.Connect(prog, Json.Bool(a, "launch"));
    }
    static dynamic Doc(Dictionary<string, object> a)
    {
        dynamic app = App(a);
        dynamic doc = app.ActiveDocument;
        if (doc == null) throw new ToolException("No drawing is open in AutoCAD.");
        return doc;
    }
    static dynamic Space(object docObj, Dictionary<string, object> a) { dynamic doc = docObj; return Json.Str(a, "space") == "paper" ? doc.PaperSpace : doc.ModelSpace; }
    static double[] P3(object? v) { var d = Json.Vec(v ?? throw new ToolException("point required")); return new[] { d[0], d.Length > 1 ? d[1] : 0, d.Length > 2 ? d[2] : 0 }; }
    static double[] P3(Dictionary<string, object> a, string k) => P3(Json.Get(a, k) ?? throw new ToolException(k + " required"));
    static List<object> Out(object? v) => v is double[] d ? d.Select(x => (object)Math.Round(x, 6)).ToList() : v is Array arr ? arr.Cast<object>().Select(x => (object)Math.Round(Convert.ToDouble(x), 6)).ToList() : new List<object>();
    static dynamic ByHandle(object docObj, string h) { dynamic doc = docObj; try { return doc.HandleToObject(h); } catch { throw new ToolException("No entity with handle " + h); } }
    static IEnumerable<dynamic> Handles(object docObj, Dictionary<string, object> a)
    {
        dynamic doc = docObj;
        var hs = Json.StrList(a, "handles");
        if (hs == null || hs.Count == 0)
        {
            dynamic ss = doc.PickfirstSelectionSet;
            var l = new List<dynamic>(); for (int i = 0; i < (int)ss.Count; i++) l.Add(ss.Item(i));
            if (l.Count == 0) throw new ToolException("Give handles (from autocad_select) or pre-select objects in AutoCAD.");
            return l;
        }
        return hs.Select(h => (dynamic)ByHandle(doc, h)).ToList();
    }
    static object Desc(object eObj)
    {
        dynamic e = eObj;
        var d = Json.O("handle", (string)e.Handle, "type", (string)e.ObjectName, "layer", Safe(() => (string)e.Layer), "color", Safe(() => (object)(int)e.color));
        try { object mn, mx; e.GetBoundingBox(out mn, out mx); d["min"] = Out(mn); d["max"] = Out(mx); } catch { }
        foreach (var p in new[] { "Length", "Area", "Radius", "TextString", "Name", "EffectiveName", "Volume" })
            try { var v = ((object)e).GetType().InvokeMember(p, System.Reflection.BindingFlags.GetProperty, null, (object)e, null); if (v is string or double or int) d[p] = v; } catch { }
        return d;
    }
    static object? Safe(Func<object?> f) { try { return f(); } catch { return null; } }
    static int K(object app, string name, int fallback) { try { return Convert.ToInt32(ComInterop.Const(app, name)); } catch { return fallback; } }

    static void Add(ToolRegistry r, string name, string title, string desc, bool ro, Dictionary<string, object> schema, Func<Dictionary<string, object>, object?> h, string set = "autocad")
        => r.Add(new Tool { Name = name, Title = title, Description = desc, Toolset = set, ReadOnly = ro, InputSchema = schema, Handler = (a, c) => Retry(() => h(a)) });

    public static void Register(ToolRegistry r)
    {
        var common = new[] { O("progId", Str("AutoCAD COM ProgID (default AutoCAD.Application; e.g. AutoCAD.Application.24 for 2021-2024, .25 for 2025+)")), O("launch", Bool("Start AutoCAD if not running")) };
        Dictionary<string, object> Sc(params (string, Dictionary<string, object>, bool)[] p) => Obj(p.Concat(common).ToArray());
        var handles = O("handles", Arr("Entity handles (omit = current pick-first selection)", Str("handle")));
        var space = O("space", Enum("Space", "model", "paper"));

        // ---------------- drawings ----------------
        Add(r, "autocad_drawings", "AutoCAD: open drawings", "All open drawings (name, path, saved state, active) and the AutoCAD product/version - works for Civil 3D, Plant 3D and every vertical.", true, Sc(), a =>
        {
            dynamic app = App(a); var l = new List<object>(); dynamic docs = app.Documents;
            for (int i = 0; i < (int)docs.Count; i++) { dynamic d = docs.Item(i); l.Add(Json.O("name", (string)d.Name, "path", (string)d.FullName, "saved", (bool)d.Saved, "active", (bool)d.Active)); }
            return Json.O("product", Safe(() => (string)app.Name), "version", Safe(() => (string)app.Version), "drawings", l);
        });
        Add(r, "autocad_drawing_file", "AutoCAD: open / new / save / save as / close / activate", "Drawing file operations. saveAs takes a version: 2018 (default), 2013, 2010, 2007, 2004, 2000, and format dwg or dxf.", false,
            Sc(R("action", Enum("Action", "open", "new", "save", "saveas", "close", "activate")), O("path", Str("File / template path")), O("name", Str("Drawing name for activate/close")),
               O("version", Enum("DWG/DXF version for saveas", "2018", "2013", "2010", "2007", "2004", "2000")), O("format", Enum("Format", "dwg", "dxf")), O("readOnly", Bool("Open read-only")), O("saveChanges", Bool("Save on close"))), a =>
        {
            dynamic app = App(a); dynamic docs = app.Documents; var path = Json.Str(a, "path");
            dynamic Find() { var n = Json.Str(a, "name"); if (n == null) return app.ActiveDocument; for (int i = 0; i < (int)docs.Count; i++) { dynamic d = docs.Item(i); if (string.Equals((string)d.Name, n, StringComparison.OrdinalIgnoreCase) || string.Equals((string)d.FullName, n, StringComparison.OrdinalIgnoreCase)) return d; } throw new ToolException("Drawing not open: " + n); }
            switch (Json.Str(a, "action"))
            {
                case "open": dynamic o = docs.Open(path ?? throw new ToolException("path required"), Json.Bool(a, "readOnly")); return Json.O("opened", (string)o.FullName);
                case "new": dynamic nd = path == null ? docs.Add() : docs.Add(path); return Json.O("created", (string)nd.Name);
                case "save": dynamic sd = Find(); sd.Save(); return Json.O("saved", (string)sd.FullName);
                case "saveas":
                    dynamic sa = Find(); var ver = Json.Str(a, "version", "2018"); var fmt = Json.Str(a, "format", "dwg");
                    int code = K(app, $"ac{ver}_{fmt}", -1);
                    if (code < 0) code = K(app, "ac" + (ver switch { "2018" => "2018", "2013" => "2013", "2010" => "2010", "2007" => "2007", "2004" => "2004", _ => "2000" }) + "_" + fmt, -1);
                    if (code >= 0) sa.SaveAs(path ?? throw new ToolException("path required"), code); else sa.SaveAs(path ?? throw new ToolException("path required"));
                    return Json.O("written", path);
                case "close": dynamic cd = Find(); var cn = (string)cd.Name; cd.Close(Json.Bool(a, "saveChanges")); return Json.O("closed", cn);
                default: dynamic ad = Find(); ad.Activate(); return Json.O("active", (string)ad.Name);
            }
        });
        Add(r, "autocad_drawing_cleanup", "AutoCAD: purge / audit / regen / zoom", "Purge all unused named objects (repeat for nested), audit and fix errors, regenerate, and zoom extents - the standard drawing clean-up before issue.", false,
            Sc(O("purge", Bool("Purge all (default true)")), O("audit", Bool("Audit and fix (default true)")), O("zoomExtents", Bool("Zoom extents (default true)"))), a =>
        {
            dynamic doc = Doc(a); dynamic app = App(a);
            if (Json.Bool(a, "purge", true)) for (int i = 0; i < 3; i++) doc.PurgeAll();
            if (Json.Bool(a, "audit", true)) doc.AuditInfo(true);
            doc.Regen(1);
            if (Json.Bool(a, "zoomExtents", true)) app.ZoomExtents();
            return Json.O("drawing", (string)doc.Name, "done", true);
        });
        Add(r, "autocad_zoom", "AutoCAD: zoom", "Zoom extents, all, window (two points), centre (point + magnification) or to entities (handles).", false,
            Sc(R("mode", Enum("Mode", "extents", "all", "window", "center", "entities")), O("p1", Pt("Corner / centre")), O("p2", Pt("Opposite corner")), O("magnification", Num("Height for center")), handles), a =>
        {
            dynamic app = App(a);
            switch (Json.Str(a, "mode"))
            {
                case "all": app.ZoomAll(); break;
                case "window": app.ZoomWindow(P3(a, "p1"), P3(a, "p2")); break;
                case "center": app.ZoomCenter(P3(a, "p1"), Json.Num(a, "magnification", 100)); break;
                case "entities":
                    double[] mn = { double.MaxValue, double.MaxValue, 0 }, mx = { double.MinValue, double.MinValue, 0 };
                    foreach (var e in Handles((object)Doc(a), a)) { object a1, b1; e.GetBoundingBox(out a1, out b1); var x = (double[])a1; var y = (double[])b1; mn[0] = Math.Min(mn[0], x[0]); mn[1] = Math.Min(mn[1], x[1]); mx[0] = Math.Max(mx[0], y[0]); mx[1] = Math.Max(mx[1], y[1]); }
                    app.ZoomWindow(mn, mx); break;
                default: app.ZoomExtents(); break;
            }
            return Json.O("zoom", Json.Str(a, "mode"));
        });

        // ---------------- selection & properties ----------------
        Add(r, "autocad_select", "AutoCAD: select entities", "Find entities by DXF type (LINE, LWPOLYLINE, INSERT, TEXT, MTEXT, CIRCLE, HATCH, DIMENSION, AECC_*, ACPP*...), layer (wildcards) and/or window, and return handles + summary. Optionally makes them the active selection.",
            true, Sc(O("types", Str("DXF types, comma separated, wildcards allowed, e.g. LINE,LWPOLYLINE")), O("layers", Str("Layer names / wildcards, e.g. A-WALL*")),
               O("p1", Pt("Window corner")), O("p2", Pt("Window opposite corner")), O("crossing", Bool("Crossing instead of inside window")), O("limit", Int("Max returned (default 500)")), O("highlight", Bool("Highlight in AutoCAD"))), a =>
        {
            dynamic doc = Doc(a);
            var name = "BURAKBIM_" + Guid.NewGuid().ToString("N")[..8];
            dynamic ss = doc.SelectionSets.Add(name);
            try
            {
                var codes = new List<short>(); var vals = new List<object>();
                if (Json.Str(a, "types") is { } t) { codes.Add(0); vals.Add(t); }
                if (Json.Str(a, "layers") is { } l) { codes.Add(8); vals.Add(l); }
                object fc = codes.Count == 0 ? Type.Missing : codes.ToArray(); object fv = vals.Count == 0 ? Type.Missing : vals.ToArray();
                if (a.ContainsKey("p1") && a.ContainsKey("p2"))
                    ss.Select(Json.Bool(a, "crossing") ? 1 : 0, P3(a, "p1"), P3(a, "p2"), fc, fv);   // acSelectionSetWindow=0, Crossing=1
                else ss.Select(5, Type.Missing, Type.Missing, fc, fv);                                // acSelectionSetAll=5
                var list = new List<object>(); var byType = new Dictionary<string, int>();
                int n = (int)ss.Count;
                for (int i = 0; i < n; i++)
                {
                    dynamic e = ss.Item(i); string ty = e.ObjectName; byType[ty] = byType.GetValueOrDefault(ty) + 1;
                    if (list.Count < (int)Json.Int(a, "limit", 500)) list.Add(Json.O("handle", (string)e.Handle, "type", ty, "layer", (string)e.Layer));
                }
                if (Json.Bool(a, "highlight")) ss.Highlight(true);
                return Json.O("count", n, "byType", byType, "entities", list);
            }
            finally { try { ss.Delete(); } catch { } }
        });
        Add(r, "autocad_entity_info", "AutoCAD: entity details", "Type, layer, colour, bounding box, length / area / radius / text / block name of entities.", true, Sc(handles), a =>
            Handles((object)Doc(a), a).Select(e => Desc(e)).ToList());
        Add(r, "autocad_entity_set", "AutoCAD: change entity properties", "Set layer, ACI colour (1-255, 256 = ByLayer) or RGB, linetype, lineweight (mm x 100, -1 ByLayer), linetype scale, visibility.", false,
            Sc(handles, O("layer", Str("Layer")), O("color", Int("ACI colour")), O("rgb", Arr("[r,g,b]")), O("linetype", Str("Linetype")), O("lineweight", Int("Lineweight, e.g. 25 = 0.25 mm")), O("linetypeScale", Num("Linetype scale")), O("visible", Bool("Visible"))), a =>
        {
            dynamic app = App(a); int n = 0;
            foreach (var e in Handles((object)Doc(a), a))
            {
                if (Json.Str(a, "layer") is { } ly) e.Layer = ly;
                if (a.ContainsKey("color")) e.color = (int)Json.Int(a, "color");
                if (Json.Get(a, "rgb") is { } rgbv) { var c = Json.Vec(rgbv); dynamic tc = e.TrueColor; tc.SetRGB((int)c[0], (int)c[1], (int)c[2]); e.TrueColor = tc; }
                if (Json.Str(a, "linetype") is { } lt) e.Linetype = lt;
                if (a.ContainsKey("lineweight")) e.Lineweight = (int)Json.Int(a, "lineweight");
                if (a.ContainsKey("linetypeScale")) e.LinetypeScale = Json.Num(a, "linetypeScale");
                if (a.ContainsKey("visible")) e.Visible = Json.Bool(a, "visible");
                e.Update(); n++;
            }
            return Json.O("changed", n);
        });
        Add(r, "autocad_transform", "AutoCAD: move / copy / rotate / scale / mirror", "Transform entities. Angles in degrees. Copy and mirror return the new handles.", false,
            Sc(handles, R("op", Enum("Operation", "move", "copy", "rotate", "scale", "mirror")), O("from", Pt("Base point")), O("to", Pt("Destination / second mirror point")),
               O("angle", Num("Rotation angle (deg)")), O("factor", Num("Scale factor")), O("keepOriginal", Bool("Mirror: keep source (default true)"))), a =>
        {
            var made = new List<object>(); var op = Json.Str(a, "op");
            foreach (var e in Handles((object)Doc(a), a))
            {
                switch (op)
                {
                    case "move": e.Move(P3(a, "from"), P3(a, "to")); break;
                    case "copy": dynamic c = e.Copy(); c.Move(P3(a, "from"), P3(a, "to")); made.Add((string)c.Handle); break;
                    case "rotate": e.Rotate(P3(a, "from"), Json.Num(a, "angle") * Math.PI / 180); break;
                    case "scale": e.ScaleEntity(P3(a, "from"), Json.Num(a, "factor", 1)); break;
                    case "mirror": dynamic m = e.Mirror(P3(a, "from"), P3(a, "to")); made.Add((string)m.Handle); if (!Json.Bool(a, "keepOriginal", true)) e.Delete(); break;
                }
            }
            return Json.O("op", op, "newHandles", made);
        });
        Add(r, "autocad_array", "AutoCAD: rectangular / polar array", "Array entities: rectangular (rows, columns, spacing) or polar (count, angle, centre).", false,
            Sc(handles, R("kind", Enum("Kind", "rectangular", "polar")), O("rows", Int("Rows")), O("columns", Int("Columns")), O("rowSpacing", Num("Row spacing")), O("columnSpacing", Num("Column spacing")),
               O("count", Int("Polar count")), O("angle", Num("Fill angle (deg, default 360)")), O("center", Pt("Polar centre"))), a =>
        {
            var made = new List<object>();
            foreach (var e in Handles((object)Doc(a), a))
            {
                object res = Json.Str(a, "kind") == "polar"
                    ? e.ArrayPolar((int)Json.Int(a, "count", 4), Json.Num(a, "angle", 360) * Math.PI / 180, P3(a, "center"))
                    : e.ArrayRectangular((int)Json.Int(a, "rows", 1), (int)Json.Int(a, "columns", 2), 1, Json.Num(a, "rowSpacing", 1000), Json.Num(a, "columnSpacing", 1000), 0.0);
                foreach (dynamic x in (Array)res) made.Add((string)x.Handle);
            }
            return Json.O("created", made);
        });
        Add(r, "autocad_offset_explode", "AutoCAD: offset / explode / delete", "Offset curves by a distance (negative = other side), explode blocks/polylines, or delete entities.", false,
            Sc(handles, R("op", Enum("Operation", "offset", "explode", "delete")), O("distance", Num("Offset distance"))), a =>
        {
            var made = new List<object>(); int n = 0;
            foreach (var e in Handles((object)Doc(a), a))
            {
                switch (Json.Str(a, "op"))
                {
                    case "offset": foreach (dynamic x in (Array)e.Offset(Json.Num(a, "distance"))) made.Add((string)x.Handle); break;
                    case "explode": foreach (dynamic x in (Array)e.Explode()) made.Add((string)x.Handle); e.Delete(); break;
                    default: e.Delete(); n++; break;
                }
            }
            return Json.O("newHandles", made, "deleted", n);
        });

        // ---------------- creation ----------------
        Add(r, "autocad_add", "AutoCAD: add 2D geometry", "Add line, polyline (closed optional, with width), circle, arc, ellipse, point, spline, 3D polyline, text, mtext, revision-cloud-like closed polyline. Coordinates in drawing units.",
            false, Sc(R("kind", Enum("Kind", "line", "polyline", "circle", "arc", "ellipse", "point", "spline", "3dpolyline", "text", "mtext")), O("points", Arr("[[x,y,z], ...]")),
               O("center", Pt("Centre / insertion")), O("radius", Num("Radius")), O("startAngle", Num("Arc start (deg)")), O("endAngle", Num("Arc end (deg)")), O("majorAxis", Pt("Ellipse major axis vector")), O("ratio", Num("Ellipse radius ratio")),
               O("closed", Bool("Close polyline")), O("width", Num("Polyline global width")), O("text", Str("Text / MText contents")), O("height", Num("Text height")), O("rotation", Num("Text rotation (deg)")), O("mtextWidth", Num("MText width")),
               O("layer", Str("Layer (created if missing)")), space), a =>
        {
            dynamic doc = Doc(a); dynamic sp = Space(doc, a); dynamic e;
            var pts = (Json.Arr(a, "points") ?? new()).Select(P3).ToList();
            switch (Json.Str(a, "kind"))
            {
                case "line": e = sp.AddLine(pts[0], pts[1]); break;
                case "polyline":
                    e = sp.AddLightWeightPolyline(pts.SelectMany(p => new[] { p[0], p[1] }).ToArray());
                    if (Json.Bool(a, "closed")) e.Closed = true; if (a.ContainsKey("width")) e.ConstantWidth = Json.Num(a, "width");
                    if (pts.Count > 0 && pts[0][2] != 0) e.Elevation = pts[0][2]; break;
                case "circle": e = sp.AddCircle(P3(a, "center"), Json.Num(a, "radius", 1)); break;
                case "arc": e = sp.AddArc(P3(a, "center"), Json.Num(a, "radius", 1), Json.Num(a, "startAngle") * Math.PI / 180, Json.Num(a, "endAngle", 90) * Math.PI / 180); break;
                case "ellipse": e = sp.AddEllipse(P3(a, "center"), P3(a, "majorAxis"), Json.Num(a, "ratio", 0.5)); break;
                case "point": e = sp.AddPoint(pts.Count > 0 ? pts[0] : P3(a, "center")); break;
                case "spline": e = sp.AddSpline(pts.SelectMany(p => p).ToArray(), new double[] { 0, 0, 0 }, new double[] { 0, 0, 0 }); break;
                case "3dpolyline": e = sp.Add3DPoly(pts.SelectMany(p => p).ToArray()); if (Json.Bool(a, "closed")) e.Closed = true; break;
                case "text": e = sp.AddText(Json.Str(a, "text") ?? "", pts.Count > 0 ? pts[0] : P3(a, "center"), Json.Num(a, "height", 2.5)); if (a.ContainsKey("rotation")) e.Rotation = Json.Num(a, "rotation") * Math.PI / 180; break;
                default: e = sp.AddMText(pts.Count > 0 ? pts[0] : P3(a, "center"), Json.Num(a, "mtextWidth", 0), Json.Str(a, "text") ?? ""); if (a.ContainsKey("height")) e.Height = Json.Num(a, "height"); break;
            }
            if (Json.Str(a, "layer") is { } ly) { doc.Layers.Add(ly); e.Layer = ly; }
            Stamp(e);
            return Desc(e);
        });
        Add(r, "autocad_add_solid", "AutoCAD: add 3D solid", "Box, cylinder, cone, sphere, wedge, torus, or extrude / revolve a closed polyline (handle) into a solid.", false,
            Sc(R("kind", Enum("Kind", "box", "cylinder", "cone", "sphere", "wedge", "torus", "extrude", "revolve")), O("center", Pt("Centre / origin")), O("length", Num("Length (X)")), O("width", Num("Width (Y)")),
               O("height", Num("Height (Z)")), O("radius", Num("Radius")), O("tubeRadius", Num("Torus tube radius")), O("profile", Str("Closed polyline handle for extrude/revolve")), O("taper", Num("Extrude taper (deg)")),
               O("axisPoint", Pt("Revolve axis point")), O("axisDir", Pt("Revolve axis direction")), O("angle", Num("Revolve angle (deg)")), O("layer", Str("Layer")), space), a =>
        {
            dynamic doc = Doc(a); dynamic sp = Space(doc, a); dynamic e; var c = a.ContainsKey("center") ? P3(a, "center") : new double[] { 0, 0, 0 };
            switch (Json.Str(a, "kind"))
            {
                case "box": e = sp.AddBox(c, Json.Num(a, "length", 1000), Json.Num(a, "width", 1000), Json.Num(a, "height", 1000)); break;
                case "cylinder": e = sp.AddCylinder(c, Json.Num(a, "radius", 500), Json.Num(a, "height", 1000)); break;
                case "cone": e = sp.AddCone(c, Json.Num(a, "radius", 500), Json.Num(a, "height", 1000)); break;
                case "sphere": e = sp.AddSphere(c, Json.Num(a, "radius", 500)); break;
                case "wedge": e = sp.AddWedge(c, Json.Num(a, "length", 1000), Json.Num(a, "width", 1000), Json.Num(a, "height", 1000)); break;
                case "torus": e = sp.AddTorus(c, Json.Num(a, "radius", 1000), Json.Num(a, "tubeRadius", 100)); break;
                default:
                    dynamic pl = ByHandle(doc, Json.Str(a, "profile") ?? throw new ToolException("profile handle required"));
                    object regs = sp.AddRegion(new object[] { pl }); dynamic reg = ((Array)regs).GetValue(0)!;
                    e = Json.Str(a, "kind") == "extrude" ? sp.AddExtrudedSolid(reg, Json.Num(a, "height", 1000), Json.Num(a, "taper") * Math.PI / 180)
                        : sp.AddRevolvedSolid(reg, P3(a, "axisPoint"), P3(a, "axisDir"), Json.Num(a, "angle", 360) * Math.PI / 180);
                    reg.Delete(); break;
            }
            if (Json.Str(a, "layer") is { } ly) { doc.Layers.Add(ly); e.Layer = ly; }
            Stamp(e);
            return Json.O("handle", (string)e.Handle, "type", (string)e.ObjectName, "volume", Safe(() => (double)e.Volume));
        });
        Add(r, "autocad_add_dimension", "AutoCAD: add dimension", "Aligned, rotated (linear), radial, diametric or angular dimension with the current dimension style (or a named one).", false,
            Sc(R("kind", Enum("Kind", "aligned", "rotated", "radial", "diametric", "angular")), O("p1", Pt("First point / centre")), O("p2", Pt("Second point / chord point")), O("p3", Pt("Text / dimension line location")),
               O("rotation", Num("Rotated: angle (deg)")), O("leaderLength", Num("Radial leader length")), O("style", Str("Dimension style")), O("layer", Str("Layer")), space), a =>
        {
            dynamic doc = Doc(a); dynamic sp = Space(doc, a); dynamic e;
            if (Json.Str(a, "style") is { } st) doc.ActiveDimStyle = doc.DimStyles.Item(st);
            switch (Json.Str(a, "kind"))
            {
                case "rotated": e = sp.AddDimRotated(P3(a, "p1"), P3(a, "p2"), P3(a, "p3"), Json.Num(a, "rotation") * Math.PI / 180); break;
                case "radial": e = sp.AddDimRadial(P3(a, "p1"), P3(a, "p2"), Json.Num(a, "leaderLength", 5)); break;
                case "diametric": e = sp.AddDimDiametric(P3(a, "p1"), P3(a, "p2"), Json.Num(a, "leaderLength", 5)); break;
                case "angular": e = sp.AddDimAngular(P3(a, "p1"), P3(a, "p2"), P3(a, "p3"), P3(a, "p3")); break;
                default: e = sp.AddDimAligned(P3(a, "p1"), P3(a, "p2"), P3(a, "p3")); break;
            }
            if (Json.Str(a, "layer") is { } ly) { doc.Layers.Add(ly); e.Layer = ly; }
            Stamp(e);
            return Json.O("handle", (string)e.Handle, "measurement", Safe(() => (double)e.Measurement));
        });
        Add(r, "autocad_add_hatch", "AutoCAD: hatch closed boundaries", "Hatch closed polylines / circles (handles) with a pattern (ANSI31, SOLID, AR-CONC, ...), scale and angle.", false,
            Sc(handles, O("pattern", Str("Pattern name (default ANSI31)")), O("scale", Num("Pattern scale")), O("angle", Num("Angle (deg)")), O("layer", Str("Layer")), space), a =>
        {
            dynamic doc = Doc(a); dynamic sp = Space(doc, a); var made = new List<object>();
            foreach (var b in Handles(doc, a))
            {
                var pat = Json.Str(a, "pattern", "ANSI31")!;
                dynamic h = sp.AddHatch(pat.Equals("SOLID", StringComparison.OrdinalIgnoreCase) ? 1 : 1, pat, true);
                h.AppendOuterLoop(new object[] { b });
                if (a.ContainsKey("scale")) h.PatternScale = Json.Num(a, "scale");
                if (a.ContainsKey("angle")) h.PatternAngle = Json.Num(a, "angle") * Math.PI / 180;
                if (Json.Str(a, "layer") is { } ly) { doc.Layers.Add(ly); h.Layer = ly; }
                h.Evaluate(); Stamp(h); made.Add(Json.O("handle", (string)h.Handle, "area", Safe(() => (double)h.Area)));
            }
            return made;
        });
        Add(r, "autocad_add_table", "AutoCAD: add table", "Insert a table from rows of text (first row = header) at a point.", false,
            Sc(R("rows", Arr("[[\"Header A\",\"Header B\"],[\"1\",\"2\"], ...]")), R("insert", Pt("Insertion point")), O("rowHeight", Num("Row height")), O("columnWidth", Num("Column width")), O("title", Str("Title row text")), space), a =>
        {
            dynamic doc = Doc(a); dynamic sp = Space(doc, a);
            var rows = (Json.Arr(a, "rows") ?? new()).Select(r0 => ((List<object>)r0).Select(x => Convert.ToString(x) ?? "").ToList()).ToList();
            var title = Json.Str(a, "title"); int off = title == null ? 0 : 1;
            int nr = rows.Count + off, nc = rows.Max(x => x.Count);
            dynamic t = sp.AddTable(P3(a, "insert"), nr, nc, Json.Num(a, "rowHeight", 8), Json.Num(a, "columnWidth", 40));
            if (title != null) { t.MergeCells(0, 0, 0, nc - 1); t.SetText(0, 0, title); }
            for (int i = 0; i < rows.Count; i++) for (int j = 0; j < rows[i].Count; j++) t.SetText(i + off, j, rows[i][j]);
            Stamp(t);
            return Json.O("handle", (string)t.Handle, "rows", nr, "columns", nc);
        });

        // ---------------- blocks, xrefs, images ----------------
        Add(r, "autocad_insert_block", "AutoCAD: insert block / DWG", "Insert a block definition or an external DWG file as a block at a point with scale, rotation (deg) and attribute values {TAG: value}.", false,
            Sc(R("block", Str("Block name or .dwg path")), R("insert", Pt("Insertion point")), O("scale", Num("Uniform scale")), O("rotation", Num("Rotation (deg)")), O("attributes", AnyObj("{TAG: value}")), O("layer", Str("Layer")), space), a =>
        {
            dynamic doc = Doc(a); dynamic sp = Space(doc, a); var s = Json.Num(a, "scale", 1);
            dynamic br = sp.InsertBlock(P3(a, "insert"), Json.Str(a, "block"), s, s, s, Json.Num(a, "rotation") * Math.PI / 180);
            var vals = Json.Obj(a, "attributes");
            if (vals != null && (bool)br.HasAttributes)
                foreach (dynamic at in (Array)br.GetAttributes()) { var tag = (string)at.TagString; var kv = vals.FirstOrDefault(x => string.Equals(x.Key, tag, StringComparison.OrdinalIgnoreCase)); if (kv.Key != null) at.TextString = Convert.ToString(kv.Value); }
            if (Json.Str(a, "layer") is { } ly) { doc.Layers.Add(ly); br.Layer = ly; }
            Stamp(br);
            return Json.O("handle", (string)br.Handle, "name", (string)br.EffectiveName);
        });
        Add(r, "autocad_block_define", "AutoCAD: create block from entities", "Create a block definition from entities (copied), with a base point; optionally replace the entities with one block reference.", false,
            Sc(handles, R("name", Str("Block name")), R("basePoint", Pt("Base point")), O("replace", Bool("Replace originals with an insert (default true)")), space), a =>
        {
            dynamic doc = Doc(a); var ents = Handles((object)doc, a).ToList();
            dynamic blk = doc.Blocks.Add(P3(a, "basePoint"), Json.Str(a, "name"));
            doc.CopyObjects(ents.Cast<object>().ToArray(), blk);
            string? ins = null;
            if (Json.Bool(a, "replace", true)) { foreach (var e in ents) e.Delete(); dynamic br = Space(doc, a).InsertBlock(P3(a, "basePoint"), Json.Str(a, "name"), 1.0, 1.0, 1.0, 0.0); ins = br.Handle; }
            return Json.O("block", Json.Str(a, "name"), "entities", ents.Count, "insert", ins);
        });
        Add(r, "autocad_blocks_report", "AutoCAD: block references and attributes", "Every block reference (optionally of one block) with position, rotation, scale, layer and attribute values - the classic attribute extraction (also for Plant 3D / Electrical symbols).", true,
            Sc(O("block", Str("Only this block name (wildcards * ?)")), O("csvPath", Str("Also write a CSV file")), O("limit", Int("Max rows (default 2000)")), space), a =>
        {
            dynamic doc = Doc(a); dynamic sp = Space(doc, a); var rows = new List<Dictionary<string, object>>(); var tags = new List<string>();
            var rx = Json.Str(a, "block") is { } bn ? new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(bn).Replace("\\*", ".*").Replace("\\?", ".") + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) : null;
            foreach (dynamic e in sp)
            {
                if ((string)e.ObjectName != "AcDbBlockReference") continue;
                string name = e.EffectiveName; if (rx != null && !rx.IsMatch(name)) continue;
                object ip = e.InsertionPoint;
                var row = new Dictionary<string, object> { ["handle"] = (string)e.Handle, ["block"] = name, ["layer"] = (string)e.Layer, ["x"] = Out(ip)[0], ["y"] = Out(ip)[1], ["z"] = Out(ip)[2],
                    ["rotation"] = Math.Round((double)e.Rotation * 180 / Math.PI, 4), ["scaleX"] = (double)e.XScaleFactor };
                if ((bool)e.HasAttributes) foreach (dynamic at in (Array)e.GetAttributes()) { string tg = at.TagString; row[tg] = (string)at.TextString; if (!tags.Contains(tg)) tags.Add(tg); }
                rows.Add(row); if (rows.Count >= (int)Json.Int(a, "limit", 2000)) break;
            }
            if (Json.Str(a, "csvPath") is { } csv)
            {
                var cols = new[] { "handle", "block", "layer", "x", "y", "z", "rotation", "scaleX" }.Concat(tags).ToList();
                string Q(object? v) { var s0 = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? ""; return s0.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s0.Replace("\"", "\"\"") + "\"" : s0; }
                File.WriteAllLines(csv, new[] { string.Join(",", cols) }.Concat(rows.Select(rw => string.Join(",", cols.Select(c => Q(rw.GetValueOrDefault(c)))))));
            }
            return Json.O("count", rows.Count, "attributeTags", tags, "rows", rows.Take(500).Cast<object>().ToList());
        });
        Add(r, "autocad_block_attributes_set", "AutoCAD: set block attribute values", "Set attribute values {TAG: value} on block references (handles), or on every reference of a block name.", false,
            Sc(handles, O("block", Str("All references of this block")), R("values", AnyObj("{TAG: value}")), space), a =>
        {
            dynamic doc = Doc(a); var vals = Json.Obj(a, "values")!; int n = 0;
            IEnumerable<dynamic> refs;
            if (Json.Str(a, "block") is { } bn) { var l = new List<dynamic>(); foreach (dynamic e in Space(doc, a)) if ((string)e.ObjectName == "AcDbBlockReference" && string.Equals((string)e.EffectiveName, bn, StringComparison.OrdinalIgnoreCase)) l.Add(e); refs = l; }
            else refs = Handles(doc, a);
            foreach (var br in refs)
                if ((bool)br.HasAttributes)
                    foreach (dynamic at in (Array)br.GetAttributes())
                    {
                        var kv = vals.FirstOrDefault(x => string.Equals(x.Key, (string)at.TagString, StringComparison.OrdinalIgnoreCase));
                        if (kv.Key != null) { at.TextString = Convert.ToString(kv.Value); n++; }
                    }
            return Json.O("attributesChanged", n);
        });
        Add(r, "autocad_xrefs", "AutoCAD: external references", "List xrefs (path, status, nesting), attach a DWG as xref (attach/overlay), reload, unload, bind or detach.", false,
            Sc(R("action", Enum("Action", "list", "attach", "overlay", "reload", "unload", "bind", "detach")), O("name", Str("Xref name")), O("path", Str("DWG path for attach")), O("insert", Pt("Insertion point")), O("scale", Num("Scale")), space), a =>
        {
            dynamic doc = Doc(a); var name = Json.Str(a, "name");
            switch (Json.Str(a, "action"))
            {
                case "attach":
                case "overlay":
                    var p = Json.Str(a, "path") ?? throw new ToolException("path required"); var s = Json.Num(a, "scale", 1);
                    dynamic x = Space(doc, a).AttachExternalReference(p, name ?? Path.GetFileNameWithoutExtension(p), a.ContainsKey("insert") ? P3(a, "insert") : new double[] { 0, 0, 0 }, s, s, s, 0.0, Json.Str(a, "action") == "overlay");
                    return Json.O("handle", (string)x.Handle);
                case "reload": doc.Blocks.Item(name).Reload(); return Json.O("reloaded", name);
                case "unload": doc.Blocks.Item(name).Unload(); return Json.O("unloaded", name);
                case "bind": doc.Blocks.Item(name).Bind(false); return Json.O("bound", name);
                case "detach": doc.Blocks.Item(name).Detach(); return Json.O("detached", name);
            }
            var l = new List<object>();
            foreach (dynamic b in doc.Blocks) if ((bool)b.IsXRef) l.Add(Json.O("name", (string)b.Name, "path", Safe(() => (string)b.Path)));
            return l;
        });
        Add(r, "autocad_attach_image", "AutoCAD: attach raster image / PDF underlay", "Attach a raster image (jpg, png, tif) at a point with scale and rotation.", false,
            Sc(R("path", Str("Image path")), R("insert", Pt("Insertion point")), O("scale", Num("Scale")), O("rotation", Num("Rotation (deg)")), space), a =>
        {
            dynamic doc = Doc(a);
            dynamic img = Space(doc, a).AddRaster(Json.Str(a, "path"), P3(a, "insert"), Json.Num(a, "scale", 1), Json.Num(a, "rotation") * Math.PI / 180);
            return Json.O("handle", (string)img.Handle);
        });

        // ---------------- text ----------------
        Add(r, "autocad_text_find_replace", "AutoCAD: find & replace text", "Find (and optionally replace) text in TEXT, MTEXT, attributes and table cells across model and paper space. Dry run unless apply=true.", false,
            Sc(R("find", Str("Text to find")), O("replace", Str("Replacement")), O("apply", Bool("Write changes")), O("matchCase", Bool("Case sensitive"))), a =>
        {
            dynamic doc = Doc(a); var f = Json.Str(a, "find")!; var rep = Json.Str(a, "replace") ?? ""; var cmp = Json.Bool(a, "matchCase") ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var hits = new List<object>();
            void Check(dynamic obj, string where)
            {
                string t = obj.TextString; if (t.IndexOf(f, cmp) < 0) return;
                var nt = System.Text.RegularExpressions.Regex.Replace(t, System.Text.RegularExpressions.Regex.Escape(f), rep.Replace("$", "$$"), cmp == StringComparison.Ordinal ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                hits.Add(Json.O("handle", (string)obj.Handle, "where", where, "from", t, "to", nt));
                if (Json.Bool(a, "apply")) obj.TextString = nt;
            }
            foreach (dynamic blk in doc.Blocks)
            {
                if (!(bool)blk.IsLayout) continue;
                foreach (dynamic e in blk)
                {
                    string on = e.ObjectName;
                    if (on is "AcDbText" or "AcDbMText") Check(e, (string)blk.Name);
                    else if (on == "AcDbBlockReference" && (bool)e.HasAttributes) foreach (dynamic at in (Array)e.GetAttributes()) Check(at, (string)blk.Name + " / " + (string)e.EffectiveName);
                }
            }
            return Json.O("applied", Json.Bool(a, "apply"), "matches", hits.Count, "items", hits.Take(500).ToList());
        });
        Add(r, "autocad_text_export", "AutoCAD: all texts", "All TEXT / MTEXT contents with position, height, layer and space - for spell checks, title-block audits and translation.", true,
            Sc(O("layer", Str("Only this layer")), O("limit", Int("Max (default 2000)"))), a =>
        {
            dynamic doc = Doc(a); var l = new List<object>();
            foreach (dynamic blk in doc.Blocks)
            {
                if (!(bool)blk.IsLayout) continue;
                foreach (dynamic e in blk)
                {
                    string on = e.ObjectName; if (on is not ("AcDbText" or "AcDbMText")) continue;
                    if (Json.Str(a, "layer") is { } ly && !string.Equals((string)e.Layer, ly, StringComparison.OrdinalIgnoreCase)) continue;
                    l.Add(Json.O("handle", (string)e.Handle, "text", (string)e.TextString, "layer", (string)e.Layer, "space", (string)blk.Name, "height", Safe(() => (double)e.Height),
                        "at", Out(on == "AcDbText" ? e.InsertionPoint : e.InsertionPoint)));
                    if (l.Count >= (int)Json.Int(a, "limit", 2000)) return l;
                }
            }
            return l;
        });

        // ---------------- layers / styles / layouts / plotting ----------------
        Add(r, "autocad_layer_report", "AutoCAD: layer usage report", "Every layer with state (on, frozen, locked, plot), colour, linetype, lineweight and the number of entities on it - finds empty and non-standard layers.", true, Sc(), a =>
        {
            dynamic doc = Doc(a); var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (dynamic blk in doc.Blocks) foreach (dynamic e in blk) { string ly = e.Layer; counts[ly] = counts.GetValueOrDefault(ly) + 1; }
            var l = new List<object>();
            foreach (dynamic ly in doc.Layers)
            {
                string n = ly.Name;
                l.Add(Json.O("name", n, "on", (bool)ly.LayerOn, "frozen", (bool)ly.Freeze, "locked", (bool)ly.Lock, "plot", (bool)ly.Plottable, "color", (int)ly.color,
                    "linetype", (string)ly.Linetype, "lineweight", (int)ly.Lineweight, "entities", counts.GetValueOrDefault(n), "description", Safe(() => (string)ly.Description)));
            }
            return Json.O("current", (string)doc.ActiveLayer.Name, "layers", l);
        });
        Add(r, "autocad_layer_standard", "AutoCAD: apply a layer standard", "Create / update many layers at once from a list [{name, color, linetype, lineweight, plot, description}] - e.g. the company CAD standard. Missing linetypes are loaded from acad.lin.", false,
            Sc(R("layers", Arr("[{\"name\":\"A-WALL\",\"color\":1,\"linetype\":\"Continuous\",\"lineweight\":35}]"))), a =>
        {
            dynamic doc = Doc(a); var done = new List<object>();
            foreach (var o in (Json.Arr(a, "layers") ?? new()).Cast<Dictionary<string, object>>())
            {
                dynamic ly = doc.Layers.Add(Json.Str(o, "name"));
                if (o.ContainsKey("color")) ly.color = (int)Json.Int(o, "color");
                if (Json.Str(o, "linetype") is { } lt) { try { doc.Linetypes.Item(lt); } catch { try { doc.Linetypes.Load(lt, "acad.lin"); } catch { } } ly.Linetype = lt; }
                if (o.ContainsKey("lineweight")) ly.Lineweight = (int)Json.Int(o, "lineweight");
                if (o.ContainsKey("plot")) ly.Plottable = Json.Bool(o, "plot");
                if (Json.Str(o, "description") is { } ds) ly.Description = ds;
                done.Add(Json.Str(o, "name"));
            }
            return Json.O("layers", done);
        });
        Add(r, "autocad_styles", "AutoCAD: text / dimension styles, linetypes", "List text styles, dimension styles and linetypes; set the current text or dimension style; load a linetype.", false,
            Sc(O("setTextStyle", Str("Make current")), O("setDimStyle", Str("Make current")), O("loadLinetype", Str("Linetype name from acad.lin / acadiso.lin"))), a =>
        {
            dynamic doc = Doc(a);
            if (Json.Str(a, "setTextStyle") is { } ts) doc.ActiveTextStyle = doc.TextStyles.Item(ts);
            if (Json.Str(a, "setDimStyle") is { } dsn) doc.ActiveDimStyle = doc.DimStyles.Item(dsn);
            if (Json.Str(a, "loadLinetype") is { } lt) { try { doc.Linetypes.Load(lt, "acadiso.lin"); } catch { doc.Linetypes.Load(lt, "acad.lin"); } }
            List<object> Names(dynamic col) { var l = new List<object>(); foreach (dynamic x in col) l.Add((string)x.Name); return l; }
            return Json.O("textStyles", Names(doc.TextStyles), "dimStyles", Names(doc.DimStyles), "linetypes", Names(doc.Linetypes),
                "currentTextStyle", (string)doc.ActiveTextStyle.Name, "currentDimStyle", (string)doc.ActiveDimStyle.Name);
        });
        Add(r, "autocad_layouts", "AutoCAD: layouts", "List layouts (tab order, paper size, plot device, plot style), create, rename, delete, copy or activate a layout.", false,
            Sc(R("action", Enum("Action", "list", "create", "rename", "delete", "activate")), O("name", Str("Layout name")), O("newName", Str("New name"))), a =>
        {
            dynamic doc = Doc(a); var n = Json.Str(a, "name");
            switch (Json.Str(a, "action"))
            {
                case "create": doc.Layouts.Add(n); return Json.O("created", n);
                case "rename": doc.Layouts.Item(n).Name = Json.Str(a, "newName"); return Json.O("renamed", Json.Str(a, "newName"));
                case "delete": doc.Layouts.Item(n).Delete(); return Json.O("deleted", n);
                case "activate": doc.ActiveLayout = doc.Layouts.Item(n); return Json.O("active", n);
            }
            var l = new List<object>();
            foreach (dynamic lo in doc.Layouts)
                l.Add(Json.O("name", (string)lo.Name, "tab", (int)lo.TabOrder, "paper", Safe(() => (string)lo.CanonicalMediaName), "device", Safe(() => (string)lo.ConfigName), "plotStyle", Safe(() => (string)lo.StyleSheet)));
            return l.OrderBy(x => (int)((Dictionary<string, object>)x)["tab"]).ToList();
        });
        Add(r, "autocad_plot_pdf", "AutoCAD: plot layouts to PDF", "Plot layouts (all paper-space layouts, or the given names) to PDF files with 'DWG To PDF.pc3' (or another device), paper size and plot style - batch issue of drawings.", false,
            Sc(R("folder", Str("Output folder")), O("layouts", Arr("Layout names (default: all paper-space layouts)", Str("name"))), O("device", Str("Plot device (default DWG To PDF.pc3)")),
               O("paper", Str("Canonical media name, e.g. ISO_full_bleed_A1_(841.00_x_594.00_MM)")), O("plotStyle", Str("CTB/STB, e.g. monochrome.ctb"))), a =>
        {
            dynamic doc = Doc(a); dynamic app = App(a); Directory.CreateDirectory(Json.Str(a, "folder")!);
            var want = Json.StrList(a, "layouts"); var done = new List<object>();
            var old = doc.GetVariable("BACKGROUNDPLOT"); doc.SetVariable("BACKGROUNDPLOT", (short)0);
            try
            {
                foreach (dynamic lo in doc.Layouts)
                {
                    string name = lo.Name; if (name == "Model" && want == null) continue;
                    if (want != null && !want.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    doc.ActiveLayout = lo;
                    lo.ConfigName = Json.Str(a, "device", "DWG To PDF.pc3");
                    if (Json.Str(a, "paper") is { } pp) lo.CanonicalMediaName = pp;
                    if (Json.Str(a, "plotStyle") is { } ps) lo.StyleSheet = ps;
                    lo.RefreshPlotDeviceInfo();
                    var file = Path.Combine(Json.Str(a, "folder")!, Path.GetFileNameWithoutExtension((string)doc.Name) + " - " + string.Join("_", name.Split(Path.GetInvalidFileNameChars())) + ".pdf");
                    bool ok = doc.Plot.PlotToFile(file);
                    done.Add(Json.O("layout", name, "file", file, "ok", ok));
                }
            }
            finally { doc.SetVariable("BACKGROUNDPLOT", old); }
            return done;
        });
        Add(r, "autocad_export", "AutoCAD: export / wblock", "Export the drawing or selected entities: wblock to a new DWG, or export WMF, SAT (ACIS), EPS, BMP, STL (solids), DXX.", false,
            Sc(R("path", Str("Output file (extension decides the format)")), handles), a =>
        {
            dynamic doc = Doc(a); var path = Json.Str(a, "path")!; var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            var name = "BURAKBIM_EXP_" + Guid.NewGuid().ToString("N")[..6];
            dynamic ss = doc.SelectionSets.Add(name);
            try
            {
                if (Json.StrList(a, "handles") is { Count: > 0 } hs) ss.AddItems(hs.Select(h => (object)ByHandle(doc, h)).ToArray()); else ss.Select(5);
                if (ext == "DWG") doc.Wblock(path, ss);
                else doc.Export(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path)), ext, ss);
                return Json.O("written", path, "entities", (int)ss.Count);
            }
            finally { try { ss.Delete(); } catch { } }
        });
        Add(r, "autocad_drawing_properties", "AutoCAD: drawing properties (DWGPROPS)", "Read or set drawing properties: title, subject, author, keywords, comments and custom properties - used by title blocks and sheet sets.", false,
            Sc(O("values", AnyObj("{\"Title\":\"...\",\"Author\":\"...\",\"Custom:Project No\":\"BB-24-01\"}"))), a =>
        {
            dynamic doc = Doc(a); dynamic si = doc.SummaryInfo;
            foreach (var kv in Json.Obj(a, "values") ?? new())
            {
                var v = Convert.ToString(kv.Value) ?? "";
                if (kv.Key.StartsWith("Custom:", StringComparison.OrdinalIgnoreCase))
                {
                    var key = kv.Key[7..]; try { si.SetCustomByKey(key, v); } catch { si.AddCustomInfo(key, v); }
                }
                else ((object)si).GetType().InvokeMember(kv.Key, System.Reflection.BindingFlags.SetProperty, null, (object)si, new object[] { v });
            }
            var custom = new Dictionary<string, object>();
            for (int i = 0; i < (int)si.NumCustomInfo(); i++) { object k, v; si.GetCustomByIndex(i, out k, out v); custom[(string)k] = v; }
            return Json.O("Title", (string)si.Title, "Subject", (string)si.Subject, "Author", (string)si.Author, "Keywords", (string)si.Keywords, "Comments", (string)si.Comments,
                "LastSavedBy", (string)si.LastSavedBy, "RevisionNumber", (string)si.RevisionNumber, "custom", custom);
        });
        Add(r, "autocad_quantities", "AutoCAD: lengths, areas and counts by layer", "Take-off from the drawing: total length of lines/polylines/arcs, area of closed polylines and hatches, and block counts - grouped by layer (or block name).", true,
            Sc(O("layers", Str("Layer filter (wildcards)")), space), a =>
        {
            dynamic doc = Doc(a); var res = new Dictionary<string, Dictionary<string, object>>();
            var rx = Json.Str(a, "layers") is { } lf ? new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(lf).Replace("\\*", ".*").Replace("\\?", ".") + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) : null;
            var blocks = new Dictionary<string, int>();
            foreach (dynamic e in Space(doc, a))
            {
                string ly = e.Layer; if (rx != null && !rx.IsMatch(ly)) continue;
                if (!res.TryGetValue(ly, out var d)) res[ly] = d = Json.O("length", 0.0, "area", 0.0, "count", 0);
                d["count"] = (int)d["count"] + 1;
                string on = e.ObjectName;
                try
                {
                    if (on is "AcDbLine" or "AcDbArc" or "AcDbPolyline" or "AcDb2dPolyline" or "AcDb3dPolyline" or "AcDbCircle" or "AcDbSpline" or "AcDbEllipse")
                        d["length"] = (double)d["length"] + Convert.ToDouble(on == "AcDbArc" ? e.ArcLength : on == "AcDbCircle" ? e.Circumference : e.Length);
                    if ((on is "AcDbPolyline" && (bool)e.Closed) || on is "AcDbHatch" or "AcDbCircle" or "AcDbRegion") d["area"] = (double)d["area"] + (double)e.Area;
                    if (on == "AcDbBlockReference") { string bn = e.EffectiveName; blocks[bn] = blocks.GetValueOrDefault(bn) + 1; }
                }
                catch { }
            }
            return Json.O("byLayer", res.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => (object)x.Value), "blocks", blocks.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => (object)x.Value));
        });
        Add(r, "autocad_measure", "AutoCAD: distance / area / angle", "Distance between two points, polyline area & perimeter by handle, or the angle (deg) of a line from p1 to p2.", true,
            Sc(O("p1", Pt("Point 1")), O("p2", Pt("Point 2")), O("handle", Str("Closed polyline / hatch / region handle"))), a =>
        {
            if (Json.Str(a, "handle") is { } h) { dynamic e = ByHandle(Doc(a), h); return Json.O("area", Safe(() => (double)e.Area), "perimeter", Safe(() => (double)e.Length)); }
            var p = P3(a, "p1"); var q = P3(a, "p2"); double dx = q[0] - p[0], dy = q[1] - p[1], dz = q[2] - p[2];
            return Json.O("distance", Math.Sqrt(dx * dx + dy * dy + dz * dz), "distance2D", Math.Sqrt(dx * dx + dy * dy), "angleDeg", Math.Atan2(dy, dx) * 180 / Math.PI, "dz", dz);
        });
        Add(r, "autocad_named_views", "AutoCAD: named views and UCS", "List named views and UCSs, save the current view under a name, or restore a named view.", false,
            Sc(R("action", Enum("Action", "list", "save", "restore")), O("name", Str("View name")), O("center", Pt("Centre for save")), O("width", Num("Width for save")), O("height", Num("Height for save"))), a =>
        {
            dynamic doc = Doc(a);
            switch (Json.Str(a, "action"))
            {
                case "save":
                    dynamic v = doc.Views.Add(Json.Str(a, "name"));
                    if (a.ContainsKey("center")) { var c = P3(a, "center"); v.Center = new[] { c[0], c[1] }; }
                    if (a.ContainsKey("width")) v.Width = Json.Num(a, "width"); if (a.ContainsKey("height")) v.Height = Json.Num(a, "height");
                    return Json.O("saved", Json.Str(a, "name"));
                case "restore": doc.ActiveViewport.SetView(doc.Views.Item(Json.Str(a, "name"))); doc.ActiveViewport = doc.ActiveViewport; return Json.O("restored", Json.Str(a, "name"));
            }
            var views = new List<object>(); foreach (dynamic v in doc.Views) views.Add((string)v.Name);
            var ucs = new List<object>(); foreach (dynamic u in doc.UserCoordinateSystems) ucs.Add((string)u.Name);
            return Json.O("views", views, "ucs", ucs);
        });
        Add(r, "autocad_groups", "AutoCAD: groups", "List groups with their entity count, create a group from entities, or explode (delete) a group.", false,
            Sc(R("action", Enum("Action", "list", "create", "delete")), O("name", Str("Group name")), handles), a =>
        {
            dynamic doc = Doc(a);
            switch (Json.Str(a, "action"))
            {
                case "create": dynamic g = doc.Groups.Add(Json.Str(a, "name")); g.AppendItems(Handles((object)doc, a).Cast<object>().ToArray()); return Json.O("created", Json.Str(a, "name"), "entities", (int)g.Count);
                case "delete": doc.Groups.Item(Json.Str(a, "name")).Delete(); return Json.O("deleted", Json.Str(a, "name"));
            }
            var l = new List<object>(); foreach (dynamic g in doc.Groups) l.Add(Json.O("name", (string)g.Name, "count", (int)g.Count)); return l;
        });

        // ---------------- Civil 3D (COM: AeccXUiLand) ----------------
        Add(r, "civil3d_com_surfaces", "Civil 3D: surfaces and statistics", "TIN / grid surfaces with min / max / mean elevation, 2D and 3D area and point count (Civil 3D COM API, every version).", true, Sc(), a =>
        {
            dynamic cd = CivilDoc(a); var l = new List<object>();
            foreach (dynamic s in cd.Surfaces)
            {
                dynamic st = Safe(() => s.Statistics);
                l.Add(Json.O("name", (string)s.Name, "type", Safe(() => s.Type.ToString()), "minElevation", Safe(() => (double)st.MinElevation), "maxElevation", Safe(() => (double)st.MaxElevation),
                    "meanElevation", Safe(() => (double)st.MeanElevation), "area2D", Safe(() => (double)st.Area2D), "area3D", Safe(() => (double)st.Area3D), "points", Safe(() => (int)st.NumberOfPoints)));
            }
            return l;
        }, "autocad");
        Add(r, "civil3d_com_alignments", "Civil 3D: alignments and profiles", "Alignments (site and siteless) with length, start / end station and their profiles (Civil 3D COM API).", true, Sc(), a =>
        {
            dynamic cd = CivilDoc(a); var l = new List<object>();
            void One(dynamic al, string site)
            {
                var profs = new List<object>(); try { foreach (dynamic p in al.Profiles) profs.Add((string)p.Name); } catch { }
                l.Add(Json.O("name", (string)al.Name, "site", site, "length", Safe(() => (double)al.Length), "startStation", Safe(() => (double)al.StartingStation), "endStation", Safe(() => (double)al.EndingStation), "profiles", profs));
            }
            try { foreach (dynamic al in cd.AlignmentsSiteless) One(al, "(siteless)"); } catch { }
            try { foreach (dynamic site in cd.Sites) foreach (dynamic al in site.Alignments) One(al, (string)site.Name); } catch { }
            return l;
        }, "autocad");
        Add(r, "civil3d_com_points", "Civil 3D: COGO points", "List COGO points (number, northing, easting, elevation, description) or add points from [[e, n, z, \"desc\"], ...].", false,
            Sc(O("add", Arr("[[easting, northing, elevation, \"description\"], ...]")), O("limit", Int("Max listed (default 1000)"))), a =>
        {
            dynamic cd = CivilDoc(a); var added = new List<object>();
            foreach (var row in Json.Arr(a, "add") ?? new())
            {
                var v = (List<object>)row;
                dynamic p = cd.Points.Add(new[] { Json.ToDouble(v[0]), Json.ToDouble(v[1]), v.Count > 2 ? Json.ToDouble(v[2]) : 0.0 });
                if (v.Count > 3) p.RawDescription = Convert.ToString(v[3]);
                added.Add((int)p.Number);
            }
            var l = new List<object>();
            foreach (dynamic p in cd.Points) { if (l.Count >= (int)Json.Int(a, "limit", 1000)) break; l.Add(Json.O("number", (int)p.Number, "easting", (double)p.Easting, "northing", (double)p.Northing, "elevation", (double)p.Elevation, "description", Safe(() => (string)p.RawDescription))); }
            return Json.O("added", added, "points", l);
        }, "autocad");
    }

    /// <summary>Tag AI-created AutoCAD entities (XData app BURAKBIM) so inspectors can find them.</summary>
    static void Stamp(object eObj)
    {
        dynamic e = eObj;
        try
        {
            dynamic doc = e.Document; try { doc.RegisteredApplications.Add("BURAKBIM"); } catch { }
            e.SetXData(new short[] { 1001, 1000 }, new object[] { "BURAKBIM", "AI|unverified|" + DateTime.Now.ToString("s") + "|" + Environment.UserName });
        }
        catch { }
    }

    /// <summary>Civil 3D COM document (AeccXUiLand.AeccApplication.&lt;ver&gt;), any Civil 3D version.</summary>
    static dynamic CivilDoc(Dictionary<string, object> a)
    {
        dynamic app = App(a);
        foreach (var major in new[] { 13, 14 })
            for (int minor = 9; minor >= 0; minor--)
            {
                try { dynamic civ = app.GetInterfaceObject($"AeccXUiLand.AeccApplication.{major}.{minor}"); if (civ != null) return civ.ActiveDocument; } catch { }
            }
        throw new ToolException("Civil 3D COM interface not found - is this AutoCAD Civil 3D with a drawing open?");
    }
}
