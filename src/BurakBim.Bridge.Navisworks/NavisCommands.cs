// BURAK BIM - Navisworks bridge methods: files, model tree, search, properties, visibility,
// viewpoints, Clash Detective (list/create/run/results/status) and TimeLiner.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BurakBim.Shared;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace BurakBim.Bridge.Navisworks
{
    public static class NavisCommands
    {
        static void Add(BridgeServer s, string name, string desc, bool writes, BridgeHandler h)
        {
            s.Register(new BridgeCommand { Name = name, Description = desc, Writes = writes, Handler = h });
        }

        public static void Register(BridgeServer s, NavisHost host)
        {
            Add(s, "navis.info", "Open file, appended models, units, selection count.", false, Info);
            Add(s, "navis.file", "open | append | save (nwd/nwf) | new | refresh.", true, FileOps);
            Add(s, "navis.tree", "Selection tree (model items) to a depth.", false, Tree);
            Add(s, "navis.search", "Find items by property (category/property/value), optionally select them.", false, SearchItems);
            Add(s, "navis.properties", "All property categories/values of selected items (or handles).", false, Properties);
            Add(s, "navis.selection", "get | clear | set (from handles or last search).", false, Selection);
            Add(s, "navis.visibility", "hide | unhide | isolate | unhide_all for selected/handle items.", true, Visibility);
            Add(s, "navis.viewpoints", "list | apply | save viewpoints.", true, Viewpoints);
            Add(s, "navis.clash.tests", "Clash Detective tests with result counts by status.", false, ClashTests);
            Add(s, "navis.clash.run", "Run one clash test by name, or all tests.", true, ClashRun);
            Add(s, "navis.clash.results", "Clash results of a test (status, distance, location, the two items).", false, ClashResults);
            Add(s, "navis.clash.create", "Create a clash test between two searches (e.g. Structure vs MEP).", true, ClashCreate);
            Add(s, "navis.clash.update", "Set status / assigned-to of clash results.", true, ClashUpdate);
            Add(s, "navis.timeliner", "TimeLiner tasks with planned/actual dates.", false, TimelinerTasks);
        }

        // ------------------------------------------------------------ helpers
        internal static dynamic Doc()
        {
            var d = NwApp.ActiveDocument;
            if (d == null) throw new BridgeException("No Navisworks document.");
            return d;
        }

        internal static Type T(string name)
        {
            var t = Reflector.FindType(name);
            if (t == null) throw new BridgeException("Navisworks API type " + name + " not available.");
            return t;
        }

        static object Static(string type, string method, params object[] args)
        {
            var t = T(type);
            var m = t.GetMethods(BindingFlags.Public | BindingFlags.Static).First(x => x.Name == method && x.GetParameters().Length == args.Length);
            return m.Invoke(null, args);
        }

        internal static object Ext(string method, object target, string assemblyHint)
        {
            foreach (var pass in new[] { 0, 1 })
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var an = a.GetName().Name ?? "";
                    if (!an.StartsWith("Autodesk.Navisworks")) continue;
                    Type[] types;
                    try { types = a.GetExportedTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        if (!(t.IsAbstract && t.IsSealed)) continue;
                        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
                        if (m != null && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(target))
                            return m.Invoke(null, new[] { target });
                    }
                }
                if (pass == 0)
                {
                    // load e.g. Autodesk.Navisworks.Clash.dll from the Navisworks folder, then retry
                    var dir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                    var f = Path.Combine(dir, assemblyHint + ".dll");
                    if (File.Exists(f)) { try { Assembly.LoadFrom(f); } catch { } }
                }
            }
            throw new BridgeException(method + " is not available (is this Navisworks Manage with the plugin loaded?).");
        }

        internal static dynamic ClashData() { return Ext("GetClash", NwApp.ActiveDocument, "Autodesk.Navisworks.Clash"); }
        internal static dynamic Timeliner() { return Ext("GetTimeliner", NwApp.ActiveDocument, "Autodesk.Navisworks.Timeliner"); }

        internal static string PathOf(dynamic item)
        {
            var parts = new List<string>();
            dynamic cur = item;
            for (int i = 0; i < 10 && cur != null; i++)
            {
                parts.Insert(0, (string)cur.DisplayName);
                cur = cur.Parent;
            }
            return string.Join(" > ", parts.ToArray());
        }

        internal static Dictionary<string, object> Item(dynamic it, bool path)
        {
            var d = Json.O("$h", Reflector.Keep(it), "name", (string)it.DisplayName, "class", (string)it.ClassDisplayName, "hasGeometry", (bool)it.HasGeometry);
            try { d["guid"] = it.InstanceGuid.ToString(); } catch { }
            if (path) { try { d["path"] = PathOf(it); } catch { } }
            return d;
        }

        internal static dynamic NewCollection()
        {
            return Activator.CreateInstance(T("Autodesk.Navisworks.Api.ModelItemCollection"));
        }

        internal static dynamic ItemsFrom(Dictionary<string, object> p)
        {
            var handles = Json.StrList(p, "handles");
            dynamic col = NewCollection();
            if (handles.Count > 0) { foreach (var h in handles) col.Add((dynamic)Reflector.FromHandle(h)); return col; }
            if (Json.Get(p, "search") is Dictionary<string, object>) return Find(Json.Obj(p, "search"));
            dynamic sel = Doc().CurrentSelection.SelectedItems;
            col.CopyFrom(sel);
            return col;
        }

        // ------------------------------------------------------------ info / files
        static object Info(Dictionary<string, object> p)
        {
            dynamic doc = Doc();
            var models = new List<object>();
            foreach (dynamic m in doc.Models)
            {
                models.Add(Json.O("file", (string)m.FileName, "source", (string)m.SourceFileName, "units", m.Units.ToString()));
            }
            return Json.O("product", NavisPlugin.Host.Product, "file", (string)doc.FileName, "title", (string)doc.Title,
                "units", doc.Units.ToString(), "models", models, "selected", (int)doc.CurrentSelection.SelectedItems.Count,
                "isClear", (bool)doc.IsClear, "scripting", ScriptRunner.Available);
        }

        static object FileOps(Dictionary<string, object> p)
        {
            dynamic doc = Doc();
            var op = (Json.Str(p, "op", "open")).ToLowerInvariant();
            var path = Json.Str(p, "path");
            switch (op)
            {
                case "open":
                    if (!File.Exists(path)) throw new BridgeException("File not found: " + path);
                    doc.OpenFile(path); return Json.O("opened", path);
                case "append":
                    {
                        var paths = Json.StrList(p, "paths");
                        if (!string.IsNullOrEmpty(path)) paths.Insert(0, path);
                        foreach (var f in paths) { if (!File.Exists(f)) throw new BridgeException("File not found: " + f); doc.AppendFile(f); }
                        return Json.O("appended", paths);
                    }
                case "save":
                    if (string.IsNullOrEmpty(path)) path = (string)doc.FileName;
                    if (string.IsNullOrEmpty(path)) throw new BridgeException("Pass path (.nwd or .nwf).");
                    doc.SaveFile(path); return Json.O("saved", path);
                case "new":
                    doc.Clear(); return Json.O("cleared", true);
                case "refresh":
                    try { doc.UpdateFiles(); } catch { doc.Models.ResetAllHidden(); }
                    return Json.O("refreshed", true);
                default: throw new BridgeException("op: open, append, save, new, refresh");
            }
        }

        static object Tree(Dictionary<string, object> p)
        {
            dynamic doc = Doc();
            int depth = (int)Json.Int(p, "depth", 2);
            int limit = (int)Json.Int(p, "limit", 1000);
            var count = new int[] { 0 };
            var roots = new List<object>();
            IEnumerable start = Json.StrList(p, "handles").Count > 0 ? (IEnumerable)ItemsFrom(p) : (IEnumerable)doc.Models.RootItems;
            foreach (dynamic r in start) roots.Add(Node(r, depth, limit, count));
            return Json.O("items", roots, "nodes", count[0]);
        }

        static object Node(dynamic it, int depth, int limit, int[] count)
        {
            count[0]++;
            var d = Item(it, false);
            if (depth > 0 && count[0] < limit)
            {
                var kids = new List<object>();
                foreach (dynamic c in it.Children)
                {
                    if (count[0] >= limit) { d["truncated"] = true; break; }
                    kids.Add(Node(c, depth - 1, limit, count));
                }
                if (kids.Count > 0) d["children"] = kids;
            }
            return d;
        }

        // ------------------------------------------------------------ search / properties
        internal static dynamic Find(Dictionary<string, object> p)
        {
            dynamic doc = Doc();
            dynamic s = Activator.CreateInstance(T("Autodesk.Navisworks.Api.Search"));
            s.Selection.SelectAll();
            s.Locations = (dynamic)Enum.Parse(T("Autodesk.Navisworks.Api.SearchLocations"), "DescendantsAndSelf");
            var cat = Json.Str(p, "category");
            var prop = Json.Str(p, "property");
            var value = Json.Str(p, "value");
            var op = (Json.Str(p, "op", value == null ? "defined" : "equals")).ToLowerInvariant();
            if (string.IsNullOrEmpty(cat) || string.IsNullOrEmpty(prop))
                throw new BridgeException("search needs category and property (display names, e.g. category \"Item\", property \"Layer\"; or \"Element\" / \"Category\" for Revit models).");
            dynamic cond = Static("Autodesk.Navisworks.Api.SearchCondition", "HasPropertyByDisplayName", cat, prop);
            if (op == "equals")
                cond = cond.EqualValue((dynamic)Static("Autodesk.Navisworks.Api.VariantData", "FromDisplayString", value));
            else if (op == "contains")
                cond = cond.DisplayStringContains(value);
            else if (op == "wildcard")
                cond = cond.DisplayStringWildcard(value);
            if (op != "defined") { try { cond = cond.IgnoreStringValueCase(); } catch { } }
            s.SearchConditions.Add(cond);
            return s.FindAll(doc, false);
        }

        static object SearchItems(Dictionary<string, object> p)
        {
            dynamic items = Find(p);
            int limit = (int)Json.Int(p, "limit", 200);
            var list = new List<object>();
            int n = 0;
            foreach (dynamic it in items) { if (n++ < limit) list.Add(Item(it, Json.Bool(p, "paths", true))); }
            if (Json.Bool(p, "select", true)) Doc().CurrentSelection.CopyFrom(items);
            return Json.O("count", n, "items", list, "selected", Json.Bool(p, "select", true), "collection", Reflector.Keep(items));
        }

        static object Properties(Dictionary<string, object> p)
        {
            dynamic items = ItemsFrom(p);
            var filter = Json.Str(p, "category");
            int limit = (int)Json.Int(p, "limit", 20);
            var list = new List<object>();
            foreach (dynamic it in items)
            {
                if (list.Count >= limit) break;
                var cats = new Dictionary<string, object>();
                foreach (dynamic c in it.PropertyCategories)
                {
                    string cname = c.DisplayName;
                    if (!string.IsNullOrEmpty(filter) && cname.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var props = new Dictionary<string, object>();
                    foreach (dynamic pr in c.Properties)
                    {
                        string v;
                        try { v = pr.Value.ToDisplayString(); } catch { v = null; }
                        props[(string)pr.DisplayName] = v;
                    }
                    cats[cname] = props;
                }
                var d = Item(it, true);
                d["properties"] = cats;
                list.Add(d);
            }
            return list;
        }

        static object Selection(Dictionary<string, object> p)
        {
            dynamic doc = Doc();
            var op = (Json.Str(p, "op", "get")).ToLowerInvariant();
            if (op == "clear") { doc.CurrentSelection.Clear(); return Json.O("cleared", true); }
            if (op == "set") { dynamic items = ItemsFrom(p); doc.CurrentSelection.CopyFrom(items); return Json.O("selected", (int)items.Count); }
            var list = new List<object>();
            foreach (dynamic it in doc.CurrentSelection.SelectedItems) { if (list.Count < Json.Int(p, "limit", 200)) list.Add(Item(it, true)); }
            return Json.O("count", (int)doc.CurrentSelection.SelectedItems.Count, "items", list);
        }

        static object Visibility(Dictionary<string, object> p)
        {
            dynamic doc = Doc();
            var op = (Json.Str(p, "op", "isolate")).ToLowerInvariant();
            if (op == "unhide_all") { doc.Models.ResetAllHidden(); return Json.O("ok", true); }
            dynamic items = ItemsFrom(p);
            if (op == "hide" || op == "unhide") { doc.Models.SetHidden(items, op == "hide"); return Json.O(op, (int)items.Count); }
            // isolate: keep ancestors/descendants of the items, hide every sibling branch
            dynamic visible = NewCollection();
            foreach (dynamic it in items) { visible.AddRange(it.AncestorsAndSelf); visible.AddRange(it.Descendants); }
            dynamic hidden = NewCollection();
            foreach (dynamic v in visible)
            {
                if (v.Parent == null) continue;
                foreach (dynamic sib in v.Parent.Children) if (!visible.Contains(sib)) hidden.Add(sib);
            }
            doc.Models.ResetAllHidden();
            doc.Models.SetHidden(hidden, true);
            return Json.O("isolated", (int)items.Count, "hiddenBranches", (int)hidden.Count);
        }

        // ------------------------------------------------------------ viewpoints
        static void WalkViewpoints(dynamic group, string prefix, List<object> list, Dictionary<string, object> map)
        {
            foreach (dynamic it in group.Children)
            {
                string name = it.DisplayName;
                bool isGroup = it.IsGroup;
                var path = prefix.Length == 0 ? name : prefix + " / " + name;
                if (isGroup) WalkViewpoints(it, path, list, map);
                else { list.Add(path); map[path] = it; map[name] = it; }
            }
        }

        static object Viewpoints(Dictionary<string, object> p)
        {
            dynamic doc = Doc();
            var op = (Json.Str(p, "op", "list")).ToLowerInvariant();
            var list = new List<object>();
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            WalkViewpoints(doc.SavedViewpoints.Value, "", list, map);
            var name = Json.Str(p, "name");
            if (op == "apply")
            {
                if (name == null || !map.ContainsKey(name)) throw new BridgeException("Viewpoint '" + name + "' not found.");
                doc.SavedViewpoints.CurrentSavedViewpoint = (dynamic)map[name];
                return Json.O("applied", name);
            }
            if (op == "save")
            {
                dynamic vpCopy = doc.CurrentViewpoint.CreateCopy();
                dynamic saved = Activator.CreateInstance(T("Autodesk.Navisworks.Api.SavedViewpoint"), new object[] { vpCopy });
                saved.DisplayName = string.IsNullOrEmpty(name) ? "BURAK BIM " + DateTime.Now.ToString("yyyy-MM-dd HH-mm") : name;
                doc.SavedViewpoints.AddCopy(saved);
                return Json.O("saved", (string)saved.DisplayName);
            }
            return Json.O("count", list.Count, "viewpoints", list);
        }

        // ------------------------------------------------------------ clash detective
        static dynamic FindTest(string name)
        {
            dynamic data = ClashData().TestsData;
            foreach (dynamic t in data.Tests)
                if (string.Equals((string)t.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return t;
            throw new BridgeException("Clash test '" + name + "' not found. Use navisworks_clash_tests.");
        }

        static IEnumerable<dynamic> Results(dynamic test)
        {
            foreach (dynamic c in test.Children)
            {
                bool isGroup = c.IsGroup;
                if (isGroup) { foreach (dynamic r in c.Children) yield return r; }
                else yield return c;
            }
        }

        static object ClashTests(Dictionary<string, object> p)
        {
            var list = new List<object>();
            foreach (dynamic t in ClashData().TestsData.Tests)
            {
                var counts = new Dictionary<string, object>();
                int total = 0;
                foreach (var r in Results(t)) { string s = r.Status.ToString(); counts[s] = (counts.ContainsKey(s) ? (int)counts[s] : 0) + 1; total++; }
                object lastRun = null;
                try { lastRun = t.LastRun == null ? null : t.LastRun.ToString(); } catch { }
                list.Add(Json.O("name", (string)t.DisplayName, "status", t.Status.ToString(), "type", t.TestType.ToString(),
                    "tolerance", (double)t.Tolerance, "lastRun", lastRun, "results", total, "byStatus", counts));
            }
            return Json.O("units", Doc().Units.ToString(), "tests", list);
        }

        static object ClashRun(Dictionary<string, object> p)
        {
            dynamic data = ClashData().TestsData;
            var name = Json.Str(p, "test");
            if (string.IsNullOrEmpty(name) || name == "*" || name.ToLowerInvariant() == "all") data.TestsRunAllTests();
            else data.TestsRunTest(FindTest(name));
            return ClashTests(p);
        }

        static List<object> Pt(dynamic v)
        {
            try { return new List<object> { (double)v.X, (double)v.Y, (double)v.Z }; } catch { return null; }
        }

        static object ClashResults(Dictionary<string, object> p)
        {
            dynamic test = FindTest(Json.Str(p, "test"));
            var status = Json.Str(p, "status");
            int limit = (int)Json.Int(p, "limit", 300);
            var list = new List<object>();
            int n = 0;
            foreach (var r in Results(test))
            {
                string st = r.Status.ToString();
                if (!string.IsNullOrEmpty(status) && !string.Equals(st, status, StringComparison.OrdinalIgnoreCase)) continue;
                n++;
                if (list.Count >= limit) continue;
                var d = Json.O("$h", Reflector.Keep(r), "name", (string)r.DisplayName, "status", st, "distance", (double)r.Distance, "center", Pt(r.Center));
                try { d["item1"] = r.Item1 == null ? null : PathOf(r.Item1); } catch { }
                try { d["item2"] = r.Item2 == null ? null : PathOf(r.Item2); } catch { }
                try { d["assignedTo"] = (string)r.AssignedTo; } catch { }
                try { d["description"] = (string)r.Description; } catch { }
                try { d["created"] = r.CreatedTime == null ? null : r.CreatedTime.ToString(); } catch { }
                list.Add(d);
            }
            return Json.O("test", (string)test.DisplayName, "units", Doc().Units.ToString(), "matching", n, "results", list);
        }

        static object ClashCreate(Dictionary<string, object> p)
        {
            dynamic data = ClashData().TestsData;
            var a = Json.Obj(p, "selectionA"); var b = Json.Obj(p, "selectionB");
            if (a == null || b == null) throw new BridgeException("selectionA and selectionB are searches: {category, property, value, op}.");
            dynamic itemsA = Find(a); dynamic itemsB = Find(b);
            dynamic test = Activator.CreateInstance(T("Autodesk.Navisworks.Api.Clash.ClashTest"));
            test.DisplayName = Json.Str(p, "name", "BURAK BIM clash " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            test.TestType = (dynamic)Enum.Parse(T("Autodesk.Navisworks.Api.Clash.ClashTestType"), Json.Str(p, "type", "Hard"), true);
            test.Tolerance = Json.Num(p, "tolerance", 0);
            test.SelectionA.Selection.CopyFrom(itemsA);
            test.SelectionB.Selection.CopyFrom(itemsB);
            data.TestsAddCopy(test);
            if (Json.Bool(p, "run", true)) data.TestsRunTest(FindTest((string)test.DisplayName));
            return Json.O("created", (string)test.DisplayName, "itemsA", (int)itemsA.Count, "itemsB", (int)itemsB.Count, "tests", ClashTests(p));
        }

        static object ClashUpdate(Dictionary<string, object> p)
        {
            dynamic data = ClashData().TestsData;
            var handles = Json.StrList(p, "handles");
            if (handles.Count == 0) throw new BridgeException("Pass handles of clash results (from navisworks_clash_results).");
            var status = Json.Str(p, "status");
            var assigned = Json.Str(p, "assignedTo");
            int n = 0;
            foreach (var h in handles)
            {
                dynamic r = Reflector.FromHandle(h);
                if (!string.IsNullOrEmpty(status))
                    data.TestsEditResultStatus(r, (dynamic)Enum.Parse(T("Autodesk.Navisworks.Api.Clash.ClashResultStatus"), status, true));
                if (assigned != null) data.TestsEditResultAssignedTo(r, assigned);
                n++;
            }
            return Json.O("updated", n);
        }

        // ------------------------------------------------------------ timeliner
        static void WalkTasks(IEnumerable children, int depth, List<object> list, int limit)
        {
            foreach (dynamic t in children)
            {
                if (list.Count >= limit) return;
                var d = Json.O("name", (string)t.DisplayName, "depth", depth);
                foreach (var prop in new[] { "PlannedStartDate", "PlannedEndDate", "ActualStartDate", "ActualEndDate", "SimulationTaskTypeName" })
                {
                    try { var pi = ((object)t).GetType().GetProperty(prop); if (pi != null) { var v = pi.GetValue(t, null); if (v != null) d[prop] = v.ToString(); } } catch { }
                }
                list.Add(d);
                try { WalkTasks((IEnumerable)t.Children, depth + 1, list, limit); } catch { }
            }
        }

        static object TimelinerTasks(Dictionary<string, object> p)
        {
            dynamic tl = Timeliner();
            var list = new List<object>();
            IEnumerable roots;
            try { roots = (IEnumerable)tl.TasksRoot.Children; }
            catch { roots = (IEnumerable)tl.Tasks; }
            WalkTasks(roots, 0, list, (int)Json.Int(p, "limit", 2000));
            return Json.O("count", list.Count, "tasks", list);
        }
    }
}
