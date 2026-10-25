// BURAK BIM - Revit bridge methods: commands, query, edit, create, views, export.
// Units on the wire: millimetres for coordinates and lengths (converted to Revit feet internally).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using BurakBim.Shared;

namespace BurakBim.Bridge.Revit
{
    public static class U
    {
        public const double MmPerFt = 304.8;
        public static double Ft(double mm) { return mm / MmPerFt; }
        public static double Mm(double ft) { return Math.Round(ft * MmPerFt, 3); }
        public static XYZ P(object v)
        {
            var a = Json.Vec(v);
            if (a == null) throw new BridgeException("Expected a point [x,y,z] in mm.");
            return new XYZ(Ft(a[0]), Ft(a[1]), Ft(a[2]));
        }
        public static List<object> Out(XYZ p) { return new List<object> { Mm(p.X), Mm(p.Y), Mm(p.Z) }; }
    }

    public static class RevitCommands
    {
        static RevitHost H;

        static void Add(BridgeServer s, string name, string desc, bool writes, BridgeHandler h)
        {
            s.Register(new BridgeCommand { Name = name, Description = desc, Writes = writes, Handler = h });
        }

        public static void Register(BridgeServer s, RevitHost host)
        {
            H = host;
            Add(s, "revit.info", "Revit version, open documents, active view, units.", false, Info);
            Add(s, "revit.commands.list", "All Revit commands that can be run (PostableCommand list for this Revit version).", false, ListCommands);
            Add(s, "revit.command.run", "Run any Revit command: PostableCommand name (Wall, Align, Section...), internal id (ID_OBJECTS_WALL) or add-in button id.", true, RunCommand);
            Add(s, "revit.elements.query", "Find elements by category/class/type/level/view/parameter filters.", false, Query);
            Add(s, "revit.elements.get", "Full details + all parameters for element ids.", false, GetElements);
            Add(s, "revit.elements.set_params", "Set instance or type parameter values.", true, SetParams);
            Add(s, "revit.elements.delete", "Delete elements.", true, Delete);
            Add(s, "revit.elements.move", "Move or copy elements by an offset (mm).", true, Move);
            Add(s, "revit.elements.rotate", "Rotate elements about a vertical axis through a point.", true, Rotate);
            Add(s, "revit.selection.get", "Currently selected element ids.", false, p => H.UiApp.ActiveUIDocument == null ? new List<object>() : H.UiApp.ActiveUIDocument.Selection.GetElementIds().Select(i => (object)Ids.Of(i)).ToList());
            Add(s, "revit.selection.set", "Select (highlight) elements, optionally zoom to them.", false, SetSelection);
            Add(s, "revit.views.list", "Views and sheets.", false, ListViews);
            Add(s, "revit.view.activate", "Open/activate a view by id or name.", false, ActivateView);
            Add(s, "revit.view.create", "Create a 3D, plan, ceiling, section or sheet view.", true, CreateView);
            Add(s, "revit.levels.list", "Levels with elevations (mm).", false, p => Levels(H.RequireDoc()).Select(l => (object)Json.O("id", Ids.Of(l.Id), "name", l.Name, "elevation", U.Mm(l.Elevation))).ToList());
            Add(s, "revit.types.list", "Loaded types (family symbols / system types) for a category.", false, ListTypes);
            Add(s, "revit.categories.list", "All categories in the model.", false, ListCategories);
            Add(s, "revit.create", "Create wall, floor, roof, ceiling, column, beam, brace, foundation, pipe, duct, cable_tray, conduit, grid, level, room, family instance, text, dimension.", true, Create);
            Add(s, "revit.family.load", "Load a family (.rfa) from disk.", true, LoadFamily);
            Add(s, "revit.doc.open", "Open and activate a model (.rvt/.rfa) - pass detach:true for central models.", false, OpenDoc);
            Add(s, "revit.doc.save", "Save, or Save As when path is given.", false, SaveDoc);
            Add(s, "revit.doc.new", "Create a new project from a template path (or default template).", false, NewDoc);
            Add(s, "revit.export", "Export to IFC, DWG, NWC (Navisworks), PDF, FBX, gbXML or images.", false, Export);
            Add(s, "revit.warnings", "Model warnings (review list).", false, Warnings);
            Add(s, "revit.schedule.data", "Read a schedule's table as rows.", false, ScheduleData);
            Add(s, "revit.undo", "Undo the last change (posts the Undo command).", true, p => RunCommand(Json.O("command", "Undo")));
        }

        // ---------------------------------------------------------------- info & commands
        static object Info(Dictionary<string, object> p)
        {
            var app = H.UiApp.Application;
            var docs = new List<object>();
            foreach (Document d in app.Documents)
                docs.Add(Json.O("title", d.Title, "path", d.PathName, "isFamily", d.IsFamilyDocument, "isLinked", d.IsLinked,
                    "isWorkshared", d.IsWorkshared, "modified", d.IsModified));
            var doc = H.Doc;
            return Json.O("product", app.VersionName, "build", app.VersionBuild, "language", app.Language.ToString(),
                "activeDocument", doc == null ? null : doc.Title,
                "activeView", doc == null ? null : Json.O("id", Ids.Of(doc.ActiveView.Id), "name", doc.ActiveView.Name, "type", doc.ActiveView.ViewType.ToString()),
                "documents", docs, "units", "All coordinates and lengths are millimetres (mm).",
                "scripting", ScriptRunner.Available);
        }

        static object ListCommands(Dictionary<string, object> p)
        {
            var search = Json.Str(p, "search");
            var names = Enum.GetNames(typeof(PostableCommand))
                .Where(n => string.IsNullOrEmpty(search) || n.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(n => n).Cast<object>().ToList();
            return Json.O("count", names.Count, "commands", names,
                "hint", "Run with revit_command. Any ribbon button (incl. add-ins) can also be run by its ribbon id from ribbon_list.");
        }

        static object RunCommand(Dictionary<string, object> p)
        {
            var c = (Json.Str(p, "command") ?? "").Trim();
            if (c.StartsWith("PostableCommand.", StringComparison.OrdinalIgnoreCase)) c = c.Substring(16);
            RevitCommandId cid = null;
            PostableCommand pc;
            if (Enum.TryParse(c.Replace(" ", ""), true, out pc)) cid = RevitCommandId.LookupPostableCommandId(pc);
            if (cid == null) { try { cid = RevitCommandId.LookupCommandId(c); } catch { } }
            if (cid == null)
            {
                var near = Suggest(c, Enum.GetNames(typeof(PostableCommand)));
                throw new BridgeException("Unknown Revit command '" + c + "'." + (near.Length > 0 ? " Did you mean: " + string.Join(", ", near) + "?" : " Use revit_commands or ribbon_list."));
            }
            if (!H.UiApp.CanPostCommand(cid)) throw new BridgeException("Revit cannot run '" + c + "' now (wrong view type, nothing selected, or another command is active).");
            H.UiApp.PostCommand(cid);
            return Json.O("posted", c, "commandId", cid.Name,
                "note", "Revit starts the command when this call returns. Interactive commands wait for picks: finish them in Revit or with ui_* tools (Esc cancels).");
        }

        /// <summary>Closest command names (handles 'DefaultThreeDView' -> 'Default3DView', typos, word order).</summary>
        internal static string[] Suggest(string q, IEnumerable<string> names)
        {
            Func<string, string> norm = x => x.ToLowerInvariant().Replace("three", "3").Replace("two", "2").Replace(" ", "").Replace("_", "");
            var nq = norm(q);
            return names.Select(n => new { n, d = Distance(norm(n), nq) - (norm(n).Contains(nq) || nq.Contains(norm(n)) ? 100 : 0) })
                .OrderBy(x => x.d).Take(8).Select(x => x.n).ToArray();
        }

        static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }

        // ---------------------------------------------------------------- query
        internal static Category FindCategory(Document doc, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            BuiltInCategory bic;
            var n = name.StartsWith("OST_", StringComparison.OrdinalIgnoreCase) ? name : "OST_" + name.Replace(" ", "");
            if (Enum.TryParse(n, true, out bic))
            {
                try { var c = Category.GetCategory(doc, bic); if (c != null) return c; } catch { }
            }
            foreach (Category c in doc.Settings.Categories)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
                foreach (Category sub in c.SubCategories)
                    if (string.Equals(sub.Name, name, StringComparison.OrdinalIgnoreCase)) return sub;
            }
            throw new BridgeException("Category '" + name + "' not found. Use revit_categories.");
        }

        internal static IEnumerable<Level> Levels(Document doc)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation);
        }

        internal static Level FindLevel(Document doc, object nameOrId, double? zFt = null)
        {
            var levels = Levels(doc).ToList();
            if (levels.Count == 0) throw new BridgeException("The model has no levels. Create one with revit_create kind=level.");
            if (nameOrId != null)
            {
                var s = Convert.ToString(nameOrId, System.Globalization.CultureInfo.InvariantCulture);
                var l = levels.FirstOrDefault(x => string.Equals(x.Name, s, StringComparison.OrdinalIgnoreCase))
                     ?? levels.FirstOrDefault(x => Ids.Of(x.Id).ToString() == s);
                if (l == null) throw new BridgeException("Level '" + s + "' not found. Levels: " + string.Join(", ", levels.Select(x => x.Name).ToArray()));
                return l;
            }
            if (zFt.HasValue)
            {
                var below = levels.Where(x => x.Elevation <= zFt.Value + 1e-6).ToList();
                return below.Count > 0 ? below.Last() : levels.First();
            }
            return levels.First();
        }

        static object Query(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            FilteredElementCollector col = Json.Bool(p, "activeViewOnly")
                ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                : new FilteredElementCollector(doc);
            var cat = Json.Str(p, "category");
            if (!string.IsNullOrEmpty(cat)) col = col.OfCategoryId(FindCategory(doc, cat).Id);
            var cls = Json.Str(p, "class");
            if (!string.IsNullOrEmpty(cls))
            {
                var t = Reflector.FindType(cls.Contains(".") ? cls : "Autodesk.Revit.DB." + cls)
                        ?? Reflector.FindType("Autodesk.Revit.DB.Architecture." + cls)
                        ?? Reflector.FindType("Autodesk.Revit.DB.Mechanical." + cls)
                        ?? Reflector.FindType("Autodesk.Revit.DB.Plumbing." + cls)
                        ?? Reflector.FindType("Autodesk.Revit.DB.Structure." + cls);
                if (t == null) throw new BridgeException("Class '" + cls + "' not found.");
                col = col.OfClass(t);
            }
            if (Json.Bool(p, "typesOnly")) col = col.WhereElementIsElementType();
            else col = col.WhereElementIsNotElementType();

            IEnumerable<Element> q = col;
            var level = Json.Str(p, "level");
            if (!string.IsNullOrEmpty(level))
            {
                var lv = FindLevel(doc, level);
                q = q.Where(e => e.LevelId == lv.Id || ParamLevel(e) == lv.Id);
            }
            var typeName = Json.Str(p, "typeName");
            if (!string.IsNullOrEmpty(typeName))
                q = q.Where(e => { var t = doc.GetElement(e.GetTypeId()); return t != null && t.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0; });
            var nameLike = Json.Str(p, "name");
            if (!string.IsNullOrEmpty(nameLike))
                q = q.Where(e => (e.Name ?? "").IndexOf(nameLike, StringComparison.OrdinalIgnoreCase) >= 0);
            var filters = Json.Arr(p, "where");
            if (filters != null)
            {
                foreach (var fo in filters.OfType<Dictionary<string, object>>())
                {
                    var pname = Json.Str(fo, "param"); var op = Json.Str(fo, "op", "="); var val = Json.Str(fo, "value", "");
                    q = q.Where(e => Matches(e, pname, op, val));
                }
            }
            int limit = (int)Json.Int(p, "limit", 500);
            bool withParams = Json.Bool(p, "includeParams");
            var all = q.ToList();
            return Json.O("count", all.Count, "returned", Math.Min(limit, all.Count),
                "elements", all.Take(limit).Select(e => (object)Describe(e, withParams)).ToList());
        }

        static ElementId ParamLevel(Element e)
        {
            var p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM) ?? e.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)
                 ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM) ?? e.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            return p != null && p.StorageType == StorageType.ElementId ? p.AsElementId() : ElementId.InvalidElementId;
        }

        static bool Matches(Element e, string pname, string op, string val)
        {
            var prm = FindParam(e, pname);
            if (prm == null) return op == "missing";
            var s = ParamText(prm) ?? "";
            double a, b;
            bool nums = double.TryParse(NumPart(s), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a)
                     & double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out b);
            switch (op)
            {
                case "=": case "==": return string.Equals(s, val, StringComparison.OrdinalIgnoreCase) || (nums && Math.Abs(a - b) < 1e-6);
                case "!=": return !string.Equals(s, val, StringComparison.OrdinalIgnoreCase);
                case "contains": return s.IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0;
                case "startswith": return s.StartsWith(val, StringComparison.OrdinalIgnoreCase);
                case ">": return nums && a > b;
                case "<": return nums && a < b;
                case ">=": return nums && a >= b;
                case "<=": return nums && a <= b;
                case "empty": return string.IsNullOrEmpty(s);
                case "notempty": return !string.IsNullOrEmpty(s);
                default: return false;
            }
        }

        static string NumPart(string s)
        {
            var chars = s.TakeWhile(c => char.IsDigit(c) || c == '.' || c == '-' || c == ',').ToArray();
            return new string(chars).Replace(",", "");
        }

        internal static Parameter FindParam(Element e, string name)
        {
            if (e == null || string.IsNullOrEmpty(name)) return null;
            var prm = e.LookupParameter(name);
            if (prm != null) return prm;
            BuiltInParameter bip;
            if (Enum.TryParse(name, true, out bip)) { try { prm = e.get_Parameter(bip); } catch { } }
            if (prm != null) return prm;
            foreach (Parameter x in e.Parameters)
                if (string.Equals(x.Definition.Name, name, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }

        internal static string ParamText(Parameter p)
        {
            if (p == null || !p.HasValue) return null;
            switch (p.StorageType)
            {
                case StorageType.String: return p.AsString();
                case StorageType.ElementId:
                    var id = p.AsElementId();
                    if (id == ElementId.InvalidElementId) return null;
                    var el = p.Element.Document.GetElement(id);
                    if (el != null) return el.Name;
                    try { var cat = Category.GetCategory(p.Element.Document, id); if (cat != null) return cat.Name; } catch { }
                    return Ids.Of(id).ToString();
                default: return p.AsValueString() ?? (p.StorageType == StorageType.Integer ? p.AsInteger().ToString() : p.AsDouble().ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        internal static Dictionary<string, object> Describe(Element e, bool withParams)
        {
            var doc = e.Document;
            var d = Json.O("id", Ids.Of(e.Id), "name", e.Name, "category", e.Category != null ? e.Category.Name : null,
                "class", e.GetType().Name, "uniqueId", e.UniqueId);
            var type = doc.GetElement(e.GetTypeId());
            if (type != null)
            {
                d["type"] = type.Name;
                var fs = type as FamilySymbol;
                if (fs != null) d["family"] = fs.FamilyName;
            }
            var lvl = e.LevelId != ElementId.InvalidElementId ? e.LevelId : ParamLevel(e);
            if (lvl != ElementId.InvalidElementId) { var l = doc.GetElement(lvl); if (l != null) d["level"] = l.Name; }
            var loc = e.Location;
            var lp = loc as LocationPoint;
            if (lp != null) d["point"] = U.Out(lp.Point);
            var lc = loc as LocationCurve;
            if (lc != null && lc.Curve.IsBound) { d["start"] = U.Out(lc.Curve.GetEndPoint(0)); d["end"] = U.Out(lc.Curve.GetEndPoint(1)); d["length"] = U.Mm(lc.Curve.Length); }
            var stamp = RevitInspector.ReadStamp(e);
            if (stamp != null) d["burakbim"] = stamp;
            if (withParams) d["parameters"] = Params(e);
            return d;
        }

        static List<object> Params(Element e)
        {
            var list = new List<object>();
            var seen = new HashSet<string>();
            foreach (Parameter prm in e.Parameters)
            {
                try
                {
                    if (!seen.Add(prm.Definition.Name + "|" + prm.Id)) continue;
                    list.Add(Json.O("name", prm.Definition.Name, "value", ParamText(prm), "storage", prm.StorageType.ToString(),
                        "readOnly", prm.IsReadOnly, "shared", prm.IsShared));
                }
                catch { }
            }
            return list.OrderBy(x => Json.Str((Dictionary<string, object>)x, "name")).ToList();
        }

        static List<Element> Elements(Document doc, Dictionary<string, object> p)
        {
            var ids = Json.IntList(p, "ids");
            if (ids.Count == 0 && H.UiApp.ActiveUIDocument != null && Json.Bool(p, "useSelection", true))
                ids = H.UiApp.ActiveUIDocument.Selection.GetElementIds().Select(Ids.Of).ToList();
            if (ids.Count == 0) throw new BridgeException("Pass ids (element ids) or select elements in Revit.");
            var r = new List<Element>();
            foreach (var i in ids)
            {
                var e = doc.GetElement(Ids.Make(i));
                if (e == null) throw new BridgeException("Element " + i + " not found.");
                r.Add(e);
            }
            return r;
        }

        static object GetElements(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            return Elements(doc, p).Select(e =>
            {
                var d = Describe(e, true);
                var t = doc.GetElement(e.GetTypeId());
                if (t != null && Json.Bool(p, "includeTypeParams", true)) d["typeParameters"] = Params(t);
                var bb = e.get_BoundingBox(null);
                if (bb != null) d["bbox"] = Json.O("min", U.Out(bb.Min), "max", U.Out(bb.Max));
                return (object)d;
            }).ToList();
        }

        // ---------------------------------------------------------------- edit
        internal static void SetParam(Parameter prm, object value)
        {
            if (prm.IsReadOnly) throw new BridgeException("Parameter '" + prm.Definition.Name + "' is read-only.");
            bool ok;
            switch (prm.StorageType)
            {
                case StorageType.String: ok = prm.Set(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)); break;
                case StorageType.Integer:
                    if (value is bool) ok = prm.Set((bool)value ? 1 : 0);
                    else if (value is string && !((string)value).All(char.IsDigit)) ok = prm.SetValueString((string)value);
                    else ok = prm.Set((int)Json.ToDouble(value));
                    break;
                case StorageType.Double:
                    if (value is string) ok = prm.SetValueString((string)value);
                    else if (IsLength(prm)) ok = prm.Set(U.Ft(Json.ToDouble(value)));
                    else ok = prm.SetValueString(Json.ToDouble(value).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case StorageType.ElementId:
                    {
                        var s = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                        long id;
                        if (long.TryParse(s, out id)) ok = prm.Set(Ids.Make(id));
                        else
                        {
                            var doc = prm.Element.Document;
                            var target = new FilteredElementCollector(doc).WhereElementIsNotElementType().FirstOrDefault(x => x.Name == s && (x is Level || x is Phase || x is Material))
                                ?? new FilteredElementCollector(doc).WhereElementIsElementType().FirstOrDefault(x => x.Name == s);
                            if (target == null) throw new BridgeException("No element named '" + s + "' for parameter " + prm.Definition.Name);
                            ok = prm.Set(target.Id);
                        }
                        break;
                    }
                default: ok = false; break;
            }
            if (!ok) throw new BridgeException("Revit rejected value '" + value + "' for parameter '" + prm.Definition.Name + "'.");
        }

        static bool IsLength(Parameter prm)
        {
            try { return prm.Definition.GetDataType() == SpecTypeId.Length; } catch { return false; }
        }

        static object SetParams(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var values = Json.Obj(p, "values");
            if (values == null || values.Count == 0) throw new BridgeException("values must be an object {\"Param Name\": value}. Lengths as numbers are mm; strings use project units, e.g. \"3000 mm\".");
            bool onType = Json.Bool(p, "onType");
            return H.RunWrite("BURAK BIM: set parameters", () =>
            {
                var report = new List<object>();
                foreach (var e in Elements(doc, p))
                {
                    var target = onType ? doc.GetElement(e.GetTypeId()) : e;
                    foreach (var kv in values)
                    {
                        var prm = FindParam(target, kv.Key) ?? (onType ? null : FindParam(doc.GetElement(e.GetTypeId()), kv.Key));
                        if (prm == null) { report.Add(Json.O("id", Ids.Of(e.Id), "param", kv.Key, "ok", false, "error", "not found")); continue; }
                        try { SetParam(prm, kv.Value); report.Add(Json.O("id", Ids.Of(e.Id), "param", kv.Key, "ok", true, "value", ParamText(prm))); }
                        catch (Exception ex) { report.Add(Json.O("id", Ids.Of(e.Id), "param", kv.Key, "ok", false, "error", ex.Message)); }
                    }
                }
                return report;
            });
        }

        static object Delete(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var ids = Elements(doc, Json.O("ids", Json.Arr(p, "ids"), "useSelection", false)).Select(e => e.Id).ToList();
            return H.RunWrite("BURAK BIM: delete", () =>
            {
                var deleted = doc.Delete(ids);
                return Json.O("deletedCount", deleted.Count, "deleted", deleted.Select(i => (object)Ids.Of(i)).ToList());
            });
        }

        static object Move(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var els = Elements(doc, p).Select(e => e.Id).ToList();
            var v = U.P(Json.Get(p, "offset"));
            bool copy = Json.Bool(p, "copy");
            return H.RunWrite(copy ? "BURAK BIM: copy" : "BURAK BIM: move", () =>
            {
                if (copy) return ElementTransformUtils.CopyElements(doc, els, v).Select(i => (object)Ids.Of(i)).ToList();
                ElementTransformUtils.MoveElements(doc, els, v);
                return Json.O("moved", els.Count);
            });
        }

        static object Rotate(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var els = Elements(doc, p).Select(e => e.Id).ToList();
            var c = U.P(Json.Get(p, "center"));
            double ang = Json.Num(p, "degrees") * Math.PI / 180.0;
            return H.RunWrite("BURAK BIM: rotate", () =>
            {
                ElementTransformUtils.RotateElements(doc, els, Line.CreateBound(c, c + XYZ.BasisZ), ang);
                return Json.O("rotated", els.Count);
            });
        }

        static object SetSelection(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var ids = Json.IntList(p, "ids").Select(Ids.Make).ToList();
            H.UiApp.ActiveUIDocument.Selection.SetElementIds(ids);
            if (Json.Bool(p, "zoom", true) && ids.Count > 0) H.UiApp.ActiveUIDocument.ShowElements(ids);
            return Json.O("selected", ids.Count);
        }

        // ---------------------------------------------------------------- views
        static object ListViews(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var search = Json.Str(p, "search");
            return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && (string.IsNullOrEmpty(search) || v.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name)
                .Select(v =>
                {
                    var d = Json.O("id", Ids.Of(v.Id), "name", v.Name, "type", v.ViewType.ToString());
                    var sh = v as ViewSheet;
                    if (sh != null) d["sheetNumber"] = sh.SheetNumber;
                    return (object)d;
                }).ToList();
        }

        static View FindView(Document doc, Dictionary<string, object> p)
        {
            var key = Json.Str(p, "view") ?? Json.Str(p, "id") ?? Json.Str(p, "name");
            if (string.IsNullOrEmpty(key)) throw new BridgeException("Pass view (id or name).");
            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).ToList();
            var view = views.FirstOrDefault(v => Ids.Of(v.Id).ToString() == key)
                    ?? views.FirstOrDefault(v => string.Equals(v.Name, key, StringComparison.OrdinalIgnoreCase))
                    ?? views.OfType<ViewSheet>().FirstOrDefault(v => string.Equals(v.SheetNumber, key, StringComparison.OrdinalIgnoreCase));
            if (view == null) throw new BridgeException("View '" + key + "' not found.");
            return view;
        }

        static object ActivateView(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var v = FindView(doc, p);
            H.UiApp.ActiveUIDocument.RequestViewChange(v);
            return Json.O("activating", v.Name, "id", Ids.Of(v.Id));
        }

        static object CreateView(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var kind = (Json.Str(p, "kind", "3d")).ToLowerInvariant();
            var name = Json.Str(p, "name");
            return H.RunWrite("BURAK BIM: create view", () =>
            {
                View v;
                if (kind == "sheet")
                {
                    var tb = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().FirstElementId();
                    var sheet = ViewSheet.Create(doc, tb ?? ElementId.InvalidElementId);
                    var num = Json.Str(p, "number");
                    if (!string.IsNullOrEmpty(num)) sheet.SheetNumber = num;
                    v = sheet;
                }
                else
                {
                    ViewFamily fam = kind == "plan" ? ViewFamily.FloorPlan : kind == "ceiling" ? ViewFamily.CeilingPlan
                        : kind == "section" ? ViewFamily.Section : kind == "structural" ? ViewFamily.StructuralPlan : ViewFamily.ThreeDimensional;
                    var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == fam);
                    if (vft == null) throw new BridgeException("No view type for " + fam);
                    if (fam == ViewFamily.ThreeDimensional) v = View3D.CreateIsometric(doc, vft.Id);
                    else if (fam == ViewFamily.Section)
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        double depth = U.Ft(Json.Num(p, "depth", 1000)), height = U.Ft(Json.Num(p, "height", 6000));
                        var dir = (b - a).Normalize(); var up = XYZ.BasisZ; var viewDir = dir.CrossProduct(up);
                        var t = Transform.Identity; t.Origin = (a + b) / 2; t.BasisX = dir; t.BasisY = up; t.BasisZ = viewDir;
                        var box = new BoundingBoxXYZ { Transform = t, Min = new XYZ(-(b - a).GetLength() / 2, 0, 0), Max = new XYZ((b - a).GetLength() / 2, height, depth) };
                        v = ViewSection.CreateSection(doc, vft.Id, box);
                    }
                    else
                    {
                        var lvl = FindLevel(doc, Json.Get(p, "level"));
                        v = ViewPlan.Create(doc, vft.Id, lvl.Id);
                    }
                }
                if (!string.IsNullOrEmpty(name)) v.Name = name;
                return Json.O("id", Ids.Of(v.Id), "name", v.Name, "type", v.ViewType.ToString());
            });
        }

        // ---------------------------------------------------------------- types / categories
        static object ListTypes(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var col = new FilteredElementCollector(doc).WhereElementIsElementType();
            var cat = Json.Str(p, "category");
            if (!string.IsNullOrEmpty(cat)) col = col.OfCategoryId(FindCategory(doc, cat).Id);
            var cls = Json.Str(p, "class");
            if (!string.IsNullOrEmpty(cls)) { var t = Reflector.FindType("Autodesk.Revit.DB." + cls) ?? Reflector.FindType(cls); if (t != null) col = col.OfClass(t); }
            var search = Json.Str(p, "search");
            return col.Cast<ElementType>()
                .Where(t => string.IsNullOrEmpty(search) || (t.FamilyName + " " + t.Name).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take((int)Json.Int(p, "limit", 500))
                .Select(t => (object)Json.O("id", Ids.Of(t.Id), "family", t.FamilyName, "name", t.Name, "category", t.Category != null ? t.Category.Name : null, "class", t.GetType().Name))
                .ToList();
        }

        static object ListCategories(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var list = new List<object>();
            foreach (Category c in doc.Settings.Categories)
                list.Add(Json.O("name", c.Name, "id", Ids.Of(c.Id), "type", c.CategoryType.ToString()));
            return list.OrderBy(x => Json.Str((Dictionary<string, object>)x, "name")).ToList();
        }

        // ---------------------------------------------------------------- create
        internal static T FindType<T>(Document doc, string name, BuiltInCategory? cat = null) where T : ElementType
        {
            var col = new FilteredElementCollector(doc).OfClass(typeof(T));
            if (cat.HasValue) col = col.OfCategory(cat.Value);
            var all = col.Cast<T>().ToList();
            if (all.Count == 0) throw new BridgeException("No " + typeof(T).Name + (cat.HasValue ? " (" + cat.Value + ")" : "") + " loaded in this model. Load a family first (revit_load_family).");
            if (string.IsNullOrEmpty(name)) return all[0];
            var hit = all.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                   ?? all.FirstOrDefault(t => string.Equals(t.FamilyName + ": " + t.Name, name, StringComparison.OrdinalIgnoreCase))
                   ?? all.FirstOrDefault(t => (t.FamilyName + " " + t.Name).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            if (hit == null) throw new BridgeException("Type '" + name + "' not found. Available: " + string.Join(" | ", all.Take(25).Select(t => t.FamilyName + ": " + t.Name).ToArray()));
            return hit;
        }

        static CurveLoop Loop(object pts)
        {
            var list = pts as List<object>;
            if (list == null || list.Count < 3) throw new BridgeException("boundary needs at least 3 points [[x,y],[x,y],...] in mm.");
            var xyz = list.Select(U.P).ToList();
            var loop = new CurveLoop();
            for (int i = 0; i < xyz.Count; i++)
            {
                var a = xyz[i]; var b = xyz[(i + 1) % xyz.Count];
                if (a.DistanceTo(b) < 1e-4) continue;
                loop.Append(Line.CreateBound(new XYZ(a.X, a.Y, 0), new XYZ(b.X, b.Y, 0)));
            }
            return loop;
        }

        static object Create(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var kind = (Json.Str(p, "kind") ?? "").ToLowerInvariant().Replace(" ", "_");
            var items = Json.Arr(p, "items");
            // batch: {"kind":"wall","items":[{...},{...}]} -> one undo step
            var list = items != null && items.Count > 0 ? items.OfType<Dictionary<string, object>>().ToList() : new List<Dictionary<string, object>> { p };
            return H.RunWrite("BURAK BIM: create " + kind, () =>
            {
                var results = new List<object>();
                foreach (var it in list)
                {
                    var q = new Dictionary<string, object>(p);
                    foreach (var kv in it) q[kv.Key] = kv.Value;
                    results.Add(CreateOne(doc, kind, q));
                }
                return results.Count == 1 ? results[0] : results;
            });
        }

        static object CreateOne(Document doc, string kind, Dictionary<string, object> p)
        {
            Element el;
            switch (kind)
            {
                case "wall":
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        var lvl = FindLevel(doc, Json.Get(p, "level"), a.Z);
                        var wtName = Json.Str(p, "type");
                        var wt = string.IsNullOrEmpty(wtName)
                            ? (new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(x => x.Kind == WallKind.Basic) ?? FindType<WallType>(doc, null))
                            : FindType<WallType>(doc, wtName);
                        double h = U.Ft(Json.Num(p, "height", 3000));
                        double off = U.Ft(Json.Num(p, "baseOffset", 0));
                        var line = Line.CreateBound(new XYZ(a.X, a.Y, lvl.Elevation), new XYZ(b.X, b.Y, lvl.Elevation));
                        var w = Wall.Create(doc, line, wt.Id, lvl.Id, h, off, Json.Bool(p, "flip"), Json.Bool(p, "structural"));
                        var top = Json.Get(p, "topLevel");
                        if (top != null) { var tl = FindLevel(doc, top); w.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(tl.Id); }
                        el = w; break;
                    }
                case "floor":
                case "ceiling":
                case "roof":
                    {
                        var lvl = FindLevel(doc, Json.Get(p, "level"));
                        var loop = Loop(Json.Get(p, "boundary"));
                        if (kind == "floor")
                        {
                            var ft = FindType<FloorType>(doc, Json.Str(p, "type"), BuiltInCategory.OST_Floors);
                            var f = Floor.Create(doc, new List<CurveLoop> { loop }, ft.Id, lvl.Id);
                            var off = Json.Get(p, "offset");
                            if (off != null) f.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(U.Ft(Json.ToDouble(off)));
                            el = f;
                        }
                        else if (kind == "ceiling")
                        {
                            var ct = FindType<CeilingType>(doc, Json.Str(p, "type"));
                            var c = Ceiling.Create(doc, new List<CurveLoop> { loop }, ct.Id, lvl.Id);
                            var off = Json.Get(p, "offset");
                            if (off != null) c.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(U.Ft(Json.ToDouble(off)));
                            el = c;
                        }
                        else
                        {
                            var rt = FindType<RoofType>(doc, Json.Str(p, "type"));
                            var ca = new CurveArray();
                            foreach (Curve cv in loop) ca.Append(cv);
                            ModelCurveArray mapping;
                            el = doc.Create.NewFootPrintRoof(ca, lvl, rt, out mapping);
                        }
                        break;
                    }
                case "column":
                case "structural_column":
                case "foundation":
                case "family_instance":
                case "instance":
                    {
                        var pt = U.P(Json.Get(p, "point") ?? Json.Get(p, "location"));
                        BuiltInCategory? cat = kind == "column" || kind == "structural_column"
                            ? (Json.Bool(p, "architectural") ? BuiltInCategory.OST_Columns : BuiltInCategory.OST_StructuralColumns)
                            : kind == "foundation" ? BuiltInCategory.OST_StructuralFoundation : (BuiltInCategory?)null;
                        var catName = Json.Str(p, "category");
                        FamilySymbol fs;
                        if (cat == null && !string.IsNullOrEmpty(catName))
                        {
                            var c = FindCategory(doc, catName);
                            fs = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategoryId(c.Id).Cast<FamilySymbol>()
                                .FirstOrDefault(x => string.IsNullOrEmpty(Json.Str(p, "type")) || (x.FamilyName + " " + x.Name).IndexOf(Json.Str(p, "type"), StringComparison.OrdinalIgnoreCase) >= 0);
                            if (fs == null) throw new BridgeException("No family type found in category " + catName);
                        }
                        else fs = FindType<FamilySymbol>(doc, Json.Str(p, "type"), cat);
                        if (!fs.IsActive) { fs.Activate(); doc.Regenerate(); }
                        var lvl = FindLevel(doc, Json.Get(p, "level"), pt.Z);
                        StructuralType st = kind.Contains("column") && !Json.Bool(p, "architectural") ? StructuralType.Column
                            : kind == "foundation" ? StructuralType.Footing : StructuralType.NonStructural;
                        var host = Json.Get(p, "hostId");
                        FamilyInstance fi;
                        if (host != null) fi = doc.Create.NewFamilyInstance(pt, fs, doc.GetElement(Ids.Make((long)Json.ToDouble(host))), lvl, st);
                        else fi = doc.Create.NewFamilyInstance(new XYZ(pt.X, pt.Y, lvl.Elevation), fs, lvl, st);
                        var top = Json.Get(p, "topLevel");
                        if (top != null && kind.Contains("column"))
                        {
                            var tp = fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (tp != null) tp.Set(FindLevel(doc, top).Id);
                        }
                        var rot = Json.Get(p, "rotation");
                        if (rot != null)
                            ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(pt, pt + XYZ.BasisZ), Json.ToDouble(rot) * Math.PI / 180);
                        el = fi; break;
                    }
                case "beam":
                case "brace":
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        var fs = FindType<FamilySymbol>(doc, Json.Str(p, "type"), BuiltInCategory.OST_StructuralFraming);
                        if (!fs.IsActive) { fs.Activate(); doc.Regenerate(); }
                        var lvl = FindLevel(doc, Json.Get(p, "level"), Math.Min(a.Z, b.Z));
                        el = doc.Create.NewFamilyInstance(Line.CreateBound(a, b), fs, lvl, kind == "beam" ? StructuralType.Beam : StructuralType.Brace);
                        break;
                    }
                case "pipe":
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        var pt = FindType<PipeType>(doc, Json.Str(p, "type"));
                        var sys = FindType<PipingSystemType>(doc, Json.Str(p, "system"));
                        var lvl = FindLevel(doc, Json.Get(p, "level"), Math.Min(a.Z, b.Z));
                        var pipe = Pipe.Create(doc, sys.Id, pt.Id, lvl.Id, a, b);
                        var dia = Json.Get(p, "diameter");
                        if (dia != null) pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(U.Ft(Json.ToDouble(dia)));
                        el = pipe; break;
                    }
                case "duct":
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        var dt = FindType<DuctType>(doc, Json.Str(p, "type"));
                        var sys = FindType<MechanicalSystemType>(doc, Json.Str(p, "system"));
                        var lvl = FindLevel(doc, Json.Get(p, "level"), Math.Min(a.Z, b.Z));
                        var duct = Duct.Create(doc, sys.Id, dt.Id, lvl.Id, a, b);
                        var w = Json.Get(p, "width"); var h = Json.Get(p, "height"); var d = Json.Get(p, "diameter");
                        if (w != null) { var x = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM); if (x != null) x.Set(U.Ft(Json.ToDouble(w))); }
                        if (h != null) { var x = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM); if (x != null) x.Set(U.Ft(Json.ToDouble(h))); }
                        if (d != null) { var x = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM); if (x != null) x.Set(U.Ft(Json.ToDouble(d))); }
                        el = duct; break;
                    }
                case "cable_tray":
                case "conduit":
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        var lvl = FindLevel(doc, Json.Get(p, "level"), Math.Min(a.Z, b.Z));
                        if (kind == "conduit")
                        {
                            var ct = FindType<Autodesk.Revit.DB.Electrical.ConduitType>(doc, Json.Str(p, "type"));
                            el = Autodesk.Revit.DB.Electrical.Conduit.Create(doc, ct.Id, a, b, lvl.Id);
                        }
                        else
                        {
                            var ct = FindType<Autodesk.Revit.DB.Electrical.CableTrayType>(doc, Json.Str(p, "type"));
                            el = Autodesk.Revit.DB.Electrical.CableTray.Create(doc, ct.Id, a, b, lvl.Id);
                        }
                        break;
                    }
                case "grid":
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        var g = Grid.Create(doc, Line.CreateBound(new XYZ(a.X, a.Y, 0), new XYZ(b.X, b.Y, 0)));
                        var n = Json.Str(p, "name"); if (!string.IsNullOrEmpty(n)) g.Name = n;
                        el = g; break;
                    }
                case "level":
                    {
                        var l = Level.Create(doc, U.Ft(Json.Num(p, "elevation")));
                        var n = Json.Str(p, "name"); if (!string.IsNullOrEmpty(n)) l.Name = n;
                        if (Json.Bool(p, "createPlan", true))
                        {
                            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan);
                            if (vft != null) ViewPlan.Create(doc, vft.Id, l.Id);
                        }
                        el = l; break;
                    }
                case "room":
                    {
                        var lvl = FindLevel(doc, Json.Get(p, "level"));
                        var pt = U.P(Json.Get(p, "point"));
                        var room = doc.Create.NewRoom(lvl, new UV(pt.X, pt.Y));
                        var n = Json.Str(p, "name"); if (!string.IsNullOrEmpty(n)) room.Name = n;
                        var num = Json.Str(p, "number"); if (!string.IsNullOrEmpty(num)) room.Number = num;
                        el = room; break;
                    }
                case "text":
                    {
                        var view = doc.ActiveView;
                        var tt = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).FirstElementId();
                        el = TextNote.Create(doc, view.Id, U.P(Json.Get(p, "point")), Json.Str(p, "text", ""), tt);
                        break;
                    }
                case "model_line":
                case "detail_line":
                    {
                        var a = U.P(Json.Get(p, "start")); var b = U.P(Json.Get(p, "end"));
                        var line = Line.CreateBound(a, b);
                        if (kind == "detail_line") el = doc.Create.NewDetailCurve(doc.ActiveView, line);
                        else
                        {
                            var dir = line.Direction;
                            var helper = Math.Abs(dir.Z) > 0.99 ? XYZ.BasisX : XYZ.BasisZ;
                            var normal = dir.CrossProduct(helper).Normalize();
                            var plane = Plane.CreateByNormalAndOrigin(normal, a);
                            el = doc.Create.NewModelCurve(line, SketchPlane.Create(doc, plane));
                        }
                        break;
                    }
                default:
                    throw new BridgeException("Unknown kind '" + kind + "'. Use: wall, floor, ceiling, roof, column, beam, brace, foundation, family_instance, pipe, duct, cable_tray, conduit, grid, level, room, text, model_line, detail_line. Anything else: revit_script or api_call.");
            }
            var mark = Json.Str(p, "mark");
            if (!string.IsNullOrEmpty(mark)) { var mp = el.get_Parameter(BuiltInParameter.ALL_MODEL_MARK); if (mp != null && !mp.IsReadOnly) mp.Set(mark); }
            var comments = Json.Str(p, "comments");
            if (!string.IsNullOrEmpty(comments)) { var cp = el.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS); if (cp != null && !cp.IsReadOnly) cp.Set(comments); }
            return Json.O("id", Ids.Of(el.Id), "kind", kind, "name", el.Name);
        }

        static object LoadFamily(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var path = Json.Str(p, "path");
            if (!File.Exists(path)) throw new BridgeException("Family file not found: " + path);
            return H.RunWrite("BURAK BIM: load family", () =>
            {
                Family fam;
                if (!doc.LoadFamily(path, out fam) || fam == null)
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    fam = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().FirstOrDefault(f => f.Name == name);
                    if (fam == null) throw new BridgeException("Revit could not load " + path);
                }
                return Json.O("family", fam.Name, "types", fam.GetFamilySymbolIds().Select(i => (object)doc.GetElement(i).Name).ToList());
            });
        }

        // ---------------------------------------------------------------- documents
        static object OpenDoc(Dictionary<string, object> p)
        {
            var path = Json.Str(p, "path");
            if (!File.Exists(path)) throw new BridgeException("File not found: " + path);
            UIDocument ui;
            if (Json.Bool(p, "detach"))
            {
                var mp = ModelPathUtils.ConvertUserVisiblePathToModelPath(path);
                var opts = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets };
                ui = H.UiApp.OpenAndActivateDocument(mp, opts, false);
            }
            else ui = H.UiApp.OpenAndActivateDocument(path);
            return Json.O("opened", ui.Document.Title, "path", ui.Document.PathName);
        }

        static object SaveDoc(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var path = Json.Str(p, "path");
            if (string.IsNullOrEmpty(path))
            {
                if (string.IsNullOrEmpty(doc.PathName)) throw new BridgeException("Model was never saved: pass path for Save As.");
                doc.Save();
                return Json.O("saved", doc.PathName);
            }
            doc.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = Json.Bool(p, "overwrite") });
            return Json.O("saved", path);
        }

        static object NewDoc(Dictionary<string, object> p)
        {
            var tpl = Json.Str(p, "template");
            if (string.IsNullOrEmpty(tpl)) tpl = H.UiApp.Application.DefaultProjectTemplate;
            var doc = string.IsNullOrEmpty(tpl) ? H.UiApp.Application.NewProjectDocument(UnitSystem.Metric) : H.UiApp.Application.NewProjectDocument(tpl);
            var save = Json.Str(p, "path");
            if (!string.IsNullOrEmpty(save)) { doc.SaveAs(save, new SaveAsOptions { OverwriteExistingFile = true }); doc.Close(false); H.UiApp.OpenAndActivateDocument(save); }
            return Json.O("created", true, "template", tpl, "path", save);
        }

        static object Export(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var fmt = (Json.Str(p, "format", "ifc")).ToLowerInvariant();
            var folder = Json.Str(p, "folder") ?? BurakPaths.Sub("exports");
            Directory.CreateDirectory(folder);
            var name = Json.Str(p, "name") ?? Path.GetFileNameWithoutExtension(doc.Title);
            var viewIds = Json.IntList(p, "viewIds").Select(Ids.Make).ToList();
            if (viewIds.Count == 0) viewIds.Add(doc.ActiveView.Id);
            bool ok;
            switch (fmt)
            {
                case "ifc":
                    {
                        var opt = new IFCExportOptions();
                        var ver = Json.Str(p, "ifcVersion");
                        IFCVersion v;
                        if (!string.IsNullOrEmpty(ver) && Enum.TryParse(ver, true, out v)) opt.FileVersion = v;
                        using (var t = new Transaction(doc, "BURAK BIM: IFC export")) { t.Start(); ok = doc.Export(folder, name, opt); t.Commit(); }
                        break;
                    }
                case "dwg":
                    ok = doc.Export(folder, name, viewIds, new DWGExportOptions()); break;
                case "nwc":
                    {
                        if (!OptionalFunctionalityUtils.IsNavisworksExporterAvailable()) throw new BridgeException("Navisworks NWC exporter for Revit is not installed.");
                        var o = new NavisworksExportOptions();
                        if (Json.Bool(p, "currentViewOnly")) { o.ExportScope = NavisworksExportScope.View; o.ViewId = viewIds[0]; }
                        doc.Export(folder, name, o); ok = true; break;
                    }
                case "pdf":
                    {
                        var o = new PDFExportOptions { FileName = name, Combine = Json.Bool(p, "combine", true) };
                        ok = doc.Export(folder, viewIds, o); break;
                    }
                case "fbx":
                    {
                        var vs = new ViewSet();
                        foreach (var id in viewIds) { var v3 = doc.GetElement(id) as View3D; if (v3 != null) vs.Insert(v3); }
                        if (vs.IsEmpty) throw new BridgeException("FBX needs a 3D view id in viewIds.");
                        ok = doc.Export(folder, name, vs, new FBXExportOptions()); break;
                    }
                case "gbxml":
                    {
                        using (var t = new Transaction(doc, "BURAK BIM: gbXML")) { t.Start(); ok = doc.Export(folder, name, new GBXMLExportOptions()); t.RollBack(); }
                        break;
                    }
                case "png":
                case "jpg":
                case "image":
                    {
                        var o = new ImageExportOptions
                        {
                            FilePath = Path.Combine(folder, name),
                            ExportRange = ExportRange.SetOfViews,
                            HLRandWFViewsFileType = fmt == "jpg" ? ImageFileType.JPEGLossless : ImageFileType.PNG,
                            ShadowViewsFileType = fmt == "jpg" ? ImageFileType.JPEGLossless : ImageFileType.PNG,
                            PixelSize = (int)Json.Int(p, "pixels", 1920),
                            ZoomType = ZoomFitType.FitToPage
                        };
                        o.SetViewsAndSheets(viewIds);
                        doc.ExportImage(o); ok = true; break;
                    }
                default: throw new BridgeException("Formats: ifc, dwg, nwc, pdf, fbx, gbxml, png, jpg.");
            }
            return Json.O("ok", ok, "folder", folder, "files", Directory.GetFiles(folder, name + "*").Cast<object>().ToList());
        }

        static object Warnings(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            return doc.GetWarnings().Take((int)Json.Int(p, "limit", 500)).Select(w => (object)Json.O(
                "text", w.GetDescriptionText(), "severity", w.GetSeverity().ToString(),
                "elements", w.GetFailingElements().Select(i => (object)Ids.Of(i)).ToList())).ToList();
        }

        static object ScheduleData(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var key = Json.Str(p, "schedule");
            var sch = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                .FirstOrDefault(v => Ids.Of(v.Id).ToString() == key || string.Equals(v.Name, key, StringComparison.OrdinalIgnoreCase));
            if (sch == null) throw new BridgeException("Schedule '" + key + "' not found.");
            var body = sch.GetTableData().GetSectionData(SectionType.Body);
            var rows = new List<object>();
            for (int r = 0; r < body.NumberOfRows && r < Json.Int(p, "limit", 2000); r++)
            {
                var row = new List<object>();
                for (int c = 0; c < body.NumberOfColumns; c++) row.Add(sch.GetCellText(SectionType.Body, r, c));
                rows.Add(row);
            }
            return Json.O("schedule", sch.Name, "rows", rows);
        }
    }
}
