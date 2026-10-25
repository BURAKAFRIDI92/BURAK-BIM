using BurakBim.Server.Com;
using BurakBim.Server.Core;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>More COM tools: Inventor (assemblies, BOM, drawings, QA), Robot (results envelopes, releases) and Rhino (geometry, user text, blocks, views).</summary>
public static class ComMoreTools
{
    static void Add(ToolRegistry r, string set, string name, string title, string desc, bool ro, Dictionary<string, object> schema, Func<Dictionary<string, object>, object?> h)
        => r.Add(new Tool { Name = name, Title = title, Description = desc, Toolset = set, ReadOnly = ro, InputSchema = schema, Handler = (a, c) => AcadComTools.Retry(() => h(a)) });

    static object? Safe(Func<object?> f) { try { return f(); } catch { return null; } }
    static int K(object app, string name, int fallback) { try { return Convert.ToInt32(ComInterop.Const(app, name)); } catch { return fallback; } }

    // ===================================================== Inventor
    static dynamic Inv(Dictionary<string, object> a) => ComInterop.Connect("Inventor.Application", Json.Bool(a, "launch"));
    static dynamic Doc(Dictionary<string, object> a) { dynamic app = Inv(a); dynamic d = app.ActiveDocument; return d ?? throw new ToolException("No Inventor document is open."); }
    static dynamic Asm(Dictionary<string, object> a) { dynamic d = Doc(a); if ((int)d.DocumentType != 12291) throw new ToolException("The active Inventor document is not an assembly."); return d; }

    public static void Register(ToolRegistry r)
    {
        var launch = O("launch", Bool("Start the application if not running"));

        Add(r, "inventor", "inventor_mass_properties", "Inventor: mass, volume, area, centre of gravity",
            "Mass (kg), volume (cm3), surface area (cm2), centre of mass (cm) and bounding box of the active part or assembly.", true, Obj(launch), a =>
        {
            dynamic doc = Doc(a); dynamic cd = doc.ComponentDefinition; dynamic mp = cd.MassProperties; dynamic c = mp.CenterOfMass; dynamic rb = cd.RangeBox;
            return Json.O("document", (string)doc.DisplayName, "massKg", (double)mp.Mass, "volumeCm3", (double)mp.Volume, "areaCm2", (double)mp.Area,
                "centerOfMassCm", new List<object> { (double)c.X, (double)c.Y, (double)c.Z },
                "boxMinCm", new List<object> { (double)rb.MinPoint.X, (double)rb.MinPoint.Y, (double)rb.MinPoint.Z }, "boxMaxCm", new List<object> { (double)rb.MaxPoint.X, (double)rb.MaxPoint.Y, (double)rb.MaxPoint.Z });
        });
        Add(r, "inventor", "inventor_bom", "Inventor: bill of materials",
            "Structured or parts-only BOM of the active assembly: item, quantity, part number, description, material, file.", true,
            Obj(O("view", Enum("BOM view", "structured", "parts")), O("csvPath", Str("Also write CSV")), launch), a =>
        {
            dynamic doc = Asm(a); dynamic bom = doc.ComponentDefinition.BOM;
            var parts = Json.Str(a, "view") == "parts";
            if (parts) bom.PartsOnlyViewEnabled = true; else { bom.StructuredViewEnabled = true; bom.StructuredViewFirstLevelOnly = false; }
            dynamic view = null;
            foreach (dynamic v in bom.BOMViews) { int vt = (int)v.ViewType; if ((parts && vt == 62467) || (!parts && vt == 62465)) view = v; }   // kPartsOnlyBOMViewType / kStructuredBOMViewType
            if (view == null) foreach (dynamic v in bom.BOMViews) { string n = v.Name; if (parts ? n.Contains("Part") : n.Contains("Struct")) view = v; }
            if (view == null) throw new ToolException("BOM view not available.");
            var rows = new List<object>();
            void Walk(dynamic rowsCol, string prefix)
            {
                foreach (dynamic row in rowsCol)
                {
                    dynamic def = row.ComponentDefinitions.Item(1); dynamic d = def.Document;
                    string P(string set, string prop) { try { return Convert.ToString(d.PropertySets.Item(set).Item(prop).Value); } catch { return null!; } }
                    rows.Add(Json.O("item", (string)row.ItemNumber, "qty", Safe(() => Convert.ToString(row.ItemQuantity)), "partNumber", P("Design Tracking Properties", "Part Number"),
                        "description", P("Design Tracking Properties", "Description"), "material", P("Design Tracking Properties", "Material"), "file", Safe(() => (string)d.FullFileName)));
                    try { if (row.ChildRows != null) Walk(row.ChildRows, (string)row.ItemNumber + "."); } catch { }
                }
            }
            Walk(view.BOMRows, "");
            if (Json.Str(a, "csvPath") is { } csv)
                File.WriteAllLines(csv, new[] { "Item,Qty,Part Number,Description,Material,File" }.Concat(rows.Cast<Dictionary<string, object>>().Select(x =>
                    string.Join(",", new[] { "item", "qty", "partNumber", "description", "material", "file" }.Select(k => "\"" + (Convert.ToString(x[k]) ?? "").Replace("\"", "\"\"") + "\"")))));
            return Json.O("rows", rows.Count, "bom", rows);
        });
        Add(r, "inventor", "inventor_occurrences", "Inventor: assembly components",
            "Components of the active assembly (name, file, suppressed, visible, grounded, position in cm), optionally recursive.", true,
            Obj(O("recursive", Bool("Include sub-assembly components")), O("limit", Int("Max (default 1000)")), launch), a =>
        {
            dynamic doc = Asm(a); var l = new List<object>(); int lim = (int)Json.Int(a, "limit", 1000);
            void Walk(dynamic occs, int depth)
            {
                foreach (dynamic o in occs)
                {
                    if (l.Count >= lim) return;
                    dynamic t = Safe(() => o.Transformation.Translation);
                    l.Add(Json.O("name", (string)o.Name, "depth", depth, "file", Safe(() => (string)o.Definition.Document.FullFileName), "suppressed", (bool)o.Suppressed, "visible", Safe(() => (bool)o.Visible),
                        "grounded", Safe(() => (bool)o.Grounded), "positionCm", t == null ? null : new List<object> { (double)t.X, (double)t.Y, (double)t.Z }));
                    if (Json.Bool(a, "recursive") && !(bool)o.Suppressed) try { Walk(o.SubOccurrences, depth + 1); } catch { }
                }
            }
            Walk(doc.ComponentDefinition.Occurrences, 0);
            return l;
        });
        Add(r, "inventor", "inventor_place_component", "Inventor: place component",
            "Place a part / sub-assembly file into the active assembly at a position (cm), optionally grounded; repeat with count + spacing.", false,
            Obj(R("path", Str(".ipt / .iam file")), O("position", Pt("Position in cm")), O("count", Int("Copies (default 1)")), O("spacing", Pt("Offset between copies (cm)")), O("grounded", Bool("Ground it")), launch), a =>
        {
            dynamic app = Inv(a); dynamic doc = Asm(a); dynamic tg = app.TransientGeometry;
            var p = Json.Vec(Json.Get(a, "position") ?? new List<object> { 0.0, 0.0, 0.0 }); var sp = Json.Vec(Json.Get(a, "spacing") ?? new List<object> { 0.0, 0.0, 0.0 });
            var made = new List<object>();
            for (int i = 0; i < Math.Max(1, (int)Json.Int(a, "count", 1)); i++)
            {
                dynamic m = tg.CreateMatrix(); m.SetTranslation(tg.CreateVector(p[0] + sp[0] * i, p[1] + sp[1] * i, (p.Length > 2 ? p[2] : 0) + (sp.Length > 2 ? sp[2] : 0) * i));
                dynamic occ = doc.ComponentDefinition.Occurrences.Add(Json.Str(a, "path"), m);
                if (Json.Bool(a, "grounded")) occ.Grounded = true;
                made.Add((string)occ.Name);
            }
            return Json.O("placed", made);
        });
        Add(r, "inventor", "inventor_component_state", "Inventor: suppress / visibility / ground / delete components",
            "Change components of the active assembly by name: suppress, unsuppress, show, hide, ground, unground or delete.", false,
            Obj(R("names", Arr("Component names (e.g. Bolt:3)", Str("name"))), R("action", Enum("Action", "suppress", "unsuppress", "show", "hide", "ground", "unground", "delete")), launch), a =>
        {
            dynamic doc = Asm(a); int n = 0; var want = new HashSet<string>(Json.StrList(a, "names") ?? new(), StringComparer.OrdinalIgnoreCase);
            var hit = new List<dynamic>(); foreach (dynamic o in doc.ComponentDefinition.Occurrences) if (want.Contains((string)o.Name)) hit.Add(o);
            foreach (var o in hit)
            {
                switch (Json.Str(a, "action"))
                {
                    case "suppress": o.Suppress(); break; case "unsuppress": o.Unsuppress(); break;
                    case "show": o.Visible = true; break; case "hide": o.Visible = false; break;
                    case "ground": o.Grounded = true; break; case "unground": o.Grounded = false; break;
                    case "delete": o.Delete(); break;
                }
                n++;
            }
            doc.Update();
            return Json.O("changed", n);
        });
        Add(r, "inventor", "inventor_interference", "Inventor: interference check",
            "Analyze interference between all components (or two named groups) of the active assembly; returns the clashing pairs and interference volume (cm3).", true,
            Obj(O("groupA", Arr("Component names (default: all)", Str("name"))), O("groupB", Arr("Second group (optional)", Str("name"))), launch), a =>
        {
            dynamic app = Inv(a); dynamic doc = Asm(a); dynamic cd = doc.ComponentDefinition;
            dynamic Col(List<string>? names) { dynamic c = app.TransientObjects.CreateObjectCollection(); foreach (dynamic o in cd.Occurrences) if (names == null || names.Contains((string)o.Name, StringComparer.OrdinalIgnoreCase)) c.Add(o); return c; }
            dynamic res = Json.StrList(a, "groupB") is { Count: > 0 } gb ? cd.AnalyzeInterference(Col(Json.StrList(a, "groupA")), Col(gb)) : cd.AnalyzeInterference(Col(Json.StrList(a, "groupA")));
            var l = new List<object>();
            foreach (dynamic it in res) l.Add(Json.O("a", Safe(() => (string)it.OccurrenceOne.Name), "b", Safe(() => (string)it.OccurrenceTwo.Name), "volumeCm3", Safe(() => (double)it.Volume)));
            return Json.O("interferences", l.Count, "pairs", l);
        });
        Add(r, "inventor", "inventor_features", "Inventor: part features",
            "Features of the active part (name, type, suppressed, health) or suppress / unsuppress features by name.", false,
            Obj(O("suppress", Arr("Feature names to suppress", Str("name"))), O("unsuppress", Arr("Feature names to unsuppress", Str("name"))), launch), a =>
        {
            dynamic doc = Doc(a); dynamic feats = doc.ComponentDefinition.Features;
            var sup = new HashSet<string>(Json.StrList(a, "suppress") ?? new(), StringComparer.OrdinalIgnoreCase); var uns = new HashSet<string>(Json.StrList(a, "unsuppress") ?? new(), StringComparer.OrdinalIgnoreCase);
            var l = new List<object>();
            foreach (dynamic f in feats)
            {
                string n = f.Name;
                if (sup.Contains(n)) f.Suppressed = true; if (uns.Contains(n)) f.Suppressed = false;
                l.Add(Json.O("name", n, "type", Safe(() => ComInterop.ComTypeName((object)f)), "suppressed", Safe(() => (bool)f.Suppressed), "health", Safe(() => Convert.ToString(f.HealthStatus))));
            }
            if (sup.Count + uns.Count > 0) doc.Update();
            return l;
        });
        Add(r, "inventor", "inventor_constraints", "Inventor: assembly constraints and joints",
            "Constraints and joints of the active assembly with type, suppressed state and health - finds broken / sick constraints.", true, Obj(launch), a =>
        {
            dynamic doc = Asm(a); var l = new List<object>(); dynamic cd = doc.ComponentDefinition;
            foreach (dynamic c in cd.Constraints) l.Add(Json.O("name", (string)c.Name, "kind", "constraint", "type", Safe(() => ComInterop.ComTypeName((object)c)), "suppressed", Safe(() => (bool)c.Suppressed), "health", Safe(() => Convert.ToString(c.HealthStatus))));
            try { foreach (dynamic j in cd.Joints) l.Add(Json.O("name", (string)j.Name, "kind", "joint", "suppressed", Safe(() => (bool)j.Suppressed), "health", Safe(() => Convert.ToString(j.HealthStatus)))); } catch { }
            return l;
        });
        Add(r, "inventor", "inventor_material", "Inventor: part material",
            "Read the active part's material, list available materials, or assign a material by name.", false,
            Obj(O("set", Str("Material name to assign")), O("list", Bool("List available materials")), launch), a =>
        {
            dynamic doc = Doc(a);
            if (Json.Str(a, "set") is { } m)
            {
                try { doc.ActiveMaterial = doc.Assets.Item(m); }
                catch { try { dynamic app = Inv(a); doc.ActiveMaterial = app.ActiveMaterialLibrary.MaterialAssets.Item(m); } catch { doc.ComponentDefinition.Material = doc.Materials.Item(m); } }
                doc.Update();
            }
            var names = new List<object>();
            if (Json.Bool(a, "list")) { try { dynamic app = Inv(a); foreach (dynamic x in app.ActiveMaterialLibrary.MaterialAssets) names.Add((string)x.DisplayName); } catch { } }
            return Json.O("material", Safe(() => (string)doc.ActiveMaterial.DisplayName) ?? Safe(() => (string)doc.ComponentDefinition.Material.Name), "available", names);
        });
        Add(r, "inventor", "inventor_model_states", "Inventor: model states",
            "List model states (2022+) of the active part/assembly or activate one.", false, Obj(O("activate", Str("Model state name")), launch), a =>
        {
            dynamic doc = Doc(a); dynamic ms = doc.ComponentDefinition.ModelStates;
            if (Json.Str(a, "activate") is { } n) ms.Item(n).Activate();
            var l = new List<object>(); foreach (dynamic s in ms) l.Add((string)s.Name);
            return Json.O("active", Safe(() => (string)ms.ActiveModelState.Name), "states", l);
        });
        Add(r, "inventor", "inventor_drawing", "Inventor: drawing sheets and views",
            "In the active drawing: list sheets and views, add a sheet, or place a base view of a model file (front/top/right/iso) at a sheet position (cm) with scale.", false,
            Obj(R("action", Enum("Action", "list", "add_sheet", "base_view")), O("model", Str("Model file for base_view")), O("orientation", Enum("Orientation", "front", "top", "right", "iso")),
                O("position", Pt("Position on sheet (cm)")), O("scale", Num("Scale, e.g. 0.1")), launch), a =>
        {
            dynamic app = Inv(a); dynamic doc = Doc(a); if ((int)doc.DocumentType != 12292) throw new ToolException("The active document is not a drawing.");
            switch (Json.Str(a, "action"))
            {
                case "add_sheet": dynamic sh = doc.Sheets.Add(); return Json.O("sheet", (string)sh.Name);
                case "base_view":
                    dynamic model = app.Documents.Open(Json.Str(a, "model") ?? throw new ToolException("model required"), false);
                    var p = Json.Vec(Json.Get(a, "position") ?? new List<object> { 15.0, 15.0 });
                    int orient = Json.Str(a, "orientation", "front") switch
                    {
                        "top" => K(app, "kTopViewOrientation", 10765), "right" => K(app, "kRightViewOrientation", 10767), "iso" => K(app, "kIsoTopRightViewOrientation", 10759),
                        _ => K(app, "kFrontViewOrientation", 10764)
                    };
                    dynamic v = doc.ActiveSheet.DrawingViews.AddBaseView(model, app.TransientGeometry.CreatePoint2d(p[0], p[1]), Json.Num(a, "scale", 0.1), orient, K(app, "kHiddenLineDrawingViewStyle", 32257));
                    return Json.O("view", (string)v.Name);
            }
            var l = new List<object>();
            foreach (dynamic s in doc.Sheets)
            {
                var vs = new List<object>(); foreach (dynamic v in s.DrawingViews) vs.Add(Json.O("name", (string)v.Name, "scale", Safe(() => (string)v.ScaleString), "model", Safe(() => (string)v.ReferencedDocumentDescriptor.FullDocumentName)));
                l.Add(Json.O("sheet", (string)s.Name, "size", Safe(() => Convert.ToString(s.Size)), "views", vs));
            }
            return l;
        });
        Add(r, "inventor", "inventor_view_image", "Inventor: save view image",
            "Save the active Inventor view as an image (PNG / JPG / BMP) at a given size, optionally after zoom-all and isometric view.", false,
            Obj(R("path", Str("Image file")), O("width", Int("Pixels (default 1600)")), O("height", Int("Pixels (default 1000)")), O("iso", Bool("Go to home/iso view first")), launch), a =>
        {
            dynamic app = Inv(a); dynamic view = app.ActiveView;
            if (Json.Bool(a, "iso")) { try { view.GoHome(); } catch { } }
            try { view.Fit(); } catch { }
            view.SaveAsBitmap(Json.Str(a, "path"), (int)Json.Int(a, "width", 1600), (int)Json.Int(a, "height", 1000));
            return Json.O("written", Json.Str(a, "path"));
        });
        Add(r, "inventor", "inventor_update_save_all", "Inventor: rebuild / update / save all",
            "Rebuild and update the active document, and optionally save every open document.", false, Obj(O("rebuild", Bool("Full rebuild")), O("saveAll", Bool("Save all open documents")), launch), a =>
        {
            dynamic app = Inv(a); dynamic doc = Doc(a);
            if (Json.Bool(a, "rebuild")) doc.Rebuild(); doc.Update();
            int n = 0; if (Json.Bool(a, "saveAll")) foreach (dynamic d in app.Documents) { if ((bool)d.Dirty) { d.Save(); n++; } }
            return Json.O("updated", (string)doc.DisplayName, "saved", n);
        });

        // ===================================================== Robot
        Add(r, "robot", "robot_result_envelope", "Robot: result envelope",
            "Maximum / minimum bar forces (FX, FZ, MY ...) and nodal displacements over all bars / nodes for one or more cases - the governing values for design checks.", true,
            Obj(R("cases", Arr("Case / combination numbers", Int("case"))), O("positions", Int("Points per bar (default 5)")), launch), a =>
        {
            dynamic app = ComInterop.Connect("Robot.Application", Json.Bool(a, "launch")); dynamic s = app.Project.Structure; dynamic res = s.Results;
            var cases = (Json.IntList(a, "cases") ?? new()).Select(x => (int)x).ToList(); int np = Math.Max(2, (int)Json.Int(a, "positions", 5));
            var names = new[] { "FX", "FY", "FZ", "MX", "MY", "MZ" }; var max = names.ToDictionary(n => n, n => (double.MinValue, 0, 0, 0.0)); var min = names.ToDictionary(n => n, n => (double.MaxValue, 0, 0, 0.0));
            dynamic bars = s.Bars.GetAll();
            for (int i = 1; i <= (int)bars.Count; i++)
            {
                int b = (int)bars.Get(i).Number;
                foreach (var c in cases)
                    for (int k = 0; k < np; k++)
                    {
                        double pos = (double)k / (np - 1); dynamic f = res.Bars.Forces.Value(b, c, pos);
                        foreach (var n in names)
                        {
                            double v = Convert.ToDouble(((object)f).GetType().InvokeMember(n, System.Reflection.BindingFlags.GetProperty, null, (object)f, null));
                            if (v > max[n].Item1) max[n] = (v, b, c, pos); if (v < min[n].Item1) min[n] = (v, b, c, pos);
                        }
                    }
            }
            double umax = 0; int un = 0, uc = 0; dynamic nodes = s.Nodes.GetAll();
            for (int i = 1; i <= (int)nodes.Count; i++)
            {
                int n = (int)nodes.Get(i).Number;
                foreach (var c in cases) { dynamic d = res.Nodes.Displacements.Value(n, c); double u = Math.Sqrt(Math.Pow((double)d.UX, 2) + Math.Pow((double)d.UY, 2) + Math.Pow((double)d.UZ, 2)); if (u > umax) { umax = u; un = n; uc = c; } }
            }
            object Fmt(Dictionary<string, (double, int, int, double)> m) => m.ToDictionary(x => x.Key, x => (object)Json.O("value", x.Value.Item1, "bar", x.Value.Item2, "case", x.Value.Item3, "position", x.Value.Item4));
            return Json.O("max", Fmt(max), "min", Fmt(min), "maxDisplacement", Json.O("value", umax, "node", un, "case", uc));
        });
        Add(r, "robot", "robot_reaction_sum", "Robot: sum of reactions",
            "Sum of support reactions (FX, FY, FZ) per case - equilibrium check against applied loads.", true, Obj(R("cases", Arr("Case numbers", Int("case"))), launch), a =>
        {
            dynamic app = ComInterop.Connect("Robot.Application", Json.Bool(a, "launch")); dynamic s = app.Project.Structure; dynamic nodes = s.Nodes.GetAll();
            var outp = new List<object>();
            foreach (var c in (Json.IntList(a, "cases") ?? new()).Select(x => (int)x))
            {
                double fx = 0, fy = 0, fz = 0;
                for (int i = 1; i <= (int)nodes.Count; i++) { try { dynamic f = s.Results.Nodes.Reactions.Value((int)nodes.Get(i).Number, c); fx += (double)f.FX; fy += (double)f.FY; fz += (double)f.FZ; } catch { } }
                outp.Add(Json.O("case", c, "FX", fx, "FY", fy, "FZ", fz));
            }
            return outp;
        });
        Add(r, "robot", "robot_releases", "Robot: bar end releases",
            "Create a bar release label (pinned-pinned, pinned-fixed, or custom start/end fixities) and assign it to bars.", false,
            Obj(R("name", Str("Release label name")), O("preset", Enum("Preset", "pinned_pinned", "fixed_pinned", "pinned_fixed")), R("bars", Str("Bar selection text, e.g. 1to10")), launch), a =>
        {
            dynamic app = ComInterop.Connect("Robot.Application", Json.Bool(a, "launch")); dynamic s = app.Project.Structure;
            int t = K(app, "I_LT_BAR_RELEASE", 3); dynamic lab = s.Labels.Create(t, Json.Str(a, "name")); dynamic d = lab.Data;
            int free = K(app, "I_BRV_FREE", 1), fix = K(app, "I_BRV_FIXED", 0);
            var p = Json.Str(a, "preset", "pinned_pinned");
            bool startPinned = p is "pinned_pinned" or "pinned_fixed", endPinned = p is "pinned_pinned" or "fixed_pinned";
            d.StartNode.RY = startPinned ? free : fix; d.StartNode.RZ = startPinned ? free : fix;
            d.EndNode.RY = endPinned ? free : fix; d.EndNode.RZ = endPinned ? free : fix;
            s.Labels.Store(lab);
            dynamic sel = s.Selections.Create(K(app, "I_OT_BAR", 2)); sel.FromText(Json.Str(a, "bars"));
            s.Bars.SetLabel(sel, t, Json.Str(a, "name"));
            return Json.O("label", Json.Str(a, "name"), "bars", (string)sel.ToText());
        });

        // ===================================================== Rhino
        Add(r, "rhino", "rhino_add", "Rhino: add geometry",
            "Add point, line, polyline, circle, arc (3 points), box (two corners), sphere, cylinder, text dot or text through RhinoScript; returns the object ids.", false,
            Obj(R("kind", Enum("Kind", "point", "line", "polyline", "circle", "arc", "box", "sphere", "cylinder", "textdot", "text")), O("points", Arr("[[x,y,z], ...]")), O("radius", Num("Radius")),
                O("height", Num("Cylinder height / text height")), O("text", Str("Text")), O("layer", Str("Layer (created if missing)")), launch), a =>
        {
            dynamic rs = RhinoRs(a); var pts = (Json.Arr(a, "points") ?? new()).Select(p => { var v = Json.Vec(p); return new object[] { v[0], v.Length > 1 ? v[1] : 0.0, v.Length > 2 ? v[2] : 0.0 }; }).ToList();
            object P(int i) => pts.Count > i ? pts[i] : throw new ToolException("More points required.");
            object id;
            switch (Json.Str(a, "kind"))
            {
                case "point": id = rs.AddPoint(P(0)); break;
                case "line": id = rs.AddLine(P(0), P(1)); break;
                case "polyline": id = rs.AddPolyline(pts.Cast<object>().ToArray()); break;
                case "circle": id = rs.AddCircle(P(0), Json.Num(a, "radius", 1)); break;
                case "arc": id = rs.AddArc3Pt(P(0), P(2), P(1)); break;
                case "box":
                    var p0 = Json.Vec(Json.Arr(a, "points")![0]); var p1 = Json.Vec(Json.Arr(a, "points")![1]);
                    object C(double x, double y, double z) => new object[] { x, y, z };
                    id = rs.AddBox(new object[] { C(p0[0], p0[1], p0[2]), C(p1[0], p0[1], p0[2]), C(p1[0], p1[1], p0[2]), C(p0[0], p1[1], p0[2]), C(p0[0], p0[1], p1[2]), C(p1[0], p0[1], p1[2]), C(p1[0], p1[1], p1[2]), C(p0[0], p1[1], p1[2]) });
                    break;
                case "sphere": id = rs.AddSphere(P(0), Json.Num(a, "radius", 1)); break;
                case "cylinder": id = rs.AddCylinder(P(0), Json.Num(a, "height", 1), Json.Num(a, "radius", 1)); break;
                case "textdot": id = rs.AddTextDot(Json.Str(a, "text") ?? "", P(0)); break;
                default: id = rs.AddText(Json.Str(a, "text") ?? "", P(0), Json.Num(a, "height", 1)); break;
            }
            if (Json.Str(a, "layer") is { } ly) { if (!(bool)rs.IsLayer(ly)) rs.AddLayer(ly); rs.ObjectLayer(id, ly); }
            try { rs.SetUserText(id, "BURAKBIM", "AI|unverified|" + DateTime.Now.ToString("s")); } catch { }
            return Json.O("id", Convert.ToString(id));
        });
        Add(r, "rhino", "rhino_user_text", "Rhino: object attributes (user text, name, colour)",
            "Read or write object user text (key/value data used for BIM attributes), names and display colour of Rhino objects.", false,
            Obj(R("ids", Arr("Object ids", Str("guid"))), O("set", AnyObj("{key: value} user text to write")), O("name", Str("New object name")), O("rgb", Arr("[r,g,b] colour")), launch), a =>
        {
            dynamic rs = RhinoRs(a); var outp = new List<object>();
            foreach (var id in Json.StrList(a, "ids") ?? new())
            {
                foreach (var kv in Json.Obj(a, "set") ?? new()) rs.SetUserText(id, kv.Key, Convert.ToString(kv.Value));
                if (Json.Str(a, "name") is { } nm) rs.ObjectName(id, nm);
                if (Json.Get(a, "rgb") is { } c) { var v = Json.Vec(c); rs.ObjectColorSource(id, 1); rs.ObjectColor(id, (int)v[0] | ((int)v[1] << 8) | ((int)v[2] << 16)); }
                var data = new Dictionary<string, object>();
                try { foreach (var k in (Array)rs.GetUserText(id)) data[k.ToString()!] = (string)rs.GetUserText(id, k.ToString()); } catch { }
                outp.Add(Json.O("id", id, "name", Safe(() => (string)rs.ObjectName(id)), "userText", data));
            }
            return outp;
        });
        Add(r, "rhino", "rhino_measure", "Rhino: area, volume, length",
            "Area and centroid of surfaces / meshes / closed curves, volume of closed solids, and length of curves.", true, Obj(R("ids", Arr("Object ids", Str("guid"))), launch), a =>
        {
            dynamic rs = RhinoRs(a); var l = new List<object>();
            foreach (var id in Json.StrList(a, "ids") ?? new())
            {
                object? area = Safe(() => ((Array)rs.SurfaceArea(id)).GetValue(0)) ?? Safe(() => ((Array)rs.CurveArea(id)).GetValue(0)) ?? Safe(() => (double)rs.MeshArea(new object[] { id }));
                object? vol = Safe(() => ((Array)rs.SurfaceVolume(id)).GetValue(0)) ?? Safe(() => (double)rs.MeshVolume(new object[] { id }));
                l.Add(Json.O("id", id, "type", Safe(() => (int)rs.ObjectType(id)), "area", area, "volume", vol, "length", Safe(() => (double)rs.CurveLength(id))));
            }
            return l;
        });
        Add(r, "rhino", "rhino_blocks", "Rhino: blocks",
            "List block definitions with instance counts, insert a block at points, or explode block instances.", false,
            Obj(R("action", Enum("Action", "list", "insert", "explode")), O("name", Str("Block name")), O("points", Arr("[[x,y,z], ...] insertion points")), O("ids", Arr("Instance ids to explode", Str("guid"))), launch), a =>
        {
            dynamic rs = RhinoRs(a);
            switch (Json.Str(a, "action"))
            {
                case "insert":
                    var made = new List<object>();
                    foreach (var p in Json.Arr(a, "points") ?? new()) { var v = Json.Vec(p); made.Add(Convert.ToString(rs.InsertBlock(Json.Str(a, "name"), new object[] { v[0], v[1], v.Length > 2 ? v[2] : 0.0 }))!); }
                    return Json.O("inserted", made);
                case "explode":
                    var parts = new List<object>(); foreach (var id in Json.StrList(a, "ids") ?? new()) foreach (var x in (Array)rs.ExplodeBlockInstance(id)) parts.Add(x.ToString()!);
                    return Json.O("objects", parts);
            }
            var l = new List<object>();
            foreach (var b in (rs.BlockNames(true) as Array) ?? Array.Empty<object>()) l.Add(Json.O("name", b.ToString(), "instances", Safe(() => (int)rs.BlockInstanceCount(b.ToString()))));
            return l;
        });
        Add(r, "rhino", "rhino_capture_view", "Rhino: capture view / named views",
            "Save the active viewport to an image file (PNG/JPG), list named views or restore one.", false,
            Obj(O("path", Str("Image file to write")), O("width", Int("Width px (default 1920)")), O("height", Int("Height px (default 1080)")), O("restoreView", Str("Named view to restore first")), launch), a =>
        {
            dynamic rh = RhinoApp(a); dynamic rs = RhinoRs(a);
            if (Json.Str(a, "restoreView") is { } nv) rs.RestoreNamedView(nv);
            if (Json.Str(a, "path") is { } p)
                rh.RunScript($"_-ViewCaptureToFile \"{p}\" _Width={Json.Int(a, "width", 1920)} _Height={Json.Int(a, "height", 1080)} _Enter", 0);
            return Json.O("written", Json.Str(a, "path"), "namedViews", Safe(() => ((Array)rs.NamedViews()).Cast<object>().Select(x => (object)x.ToString()!).ToList()));
        });
    }

    static dynamic RhinoApp(Dictionary<string, object> a)
    {
        foreach (var p in new[] { "Rhino.Application.8", "Rhino.Application.7", "Rhino.Application" }) { var o = ComInterop.GetActive(p); if (o != null) return o; }
        if (!Json.Bool(a, "launch")) throw new ToolException("Rhino is not running. Call again with launch=true.");
        return ComInterop.Connect("Rhino.Application", true);
    }
    static dynamic RhinoRs(Dictionary<string, object> a)
    {
        dynamic rh = RhinoApp(a);
        for (int i = 0; i < 40; i++) { try { var o = rh.GetScriptObject(); if (o != null) return o; } catch { } Thread.Sleep(250); }
        throw new ToolException("RhinoScript object not available.");
    }
}
