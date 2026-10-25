// BURAK BIM ribbon automation: enumerate and press EVERY ribbon tab/panel/button
// (built-in, contextual and third-party add-ins) in hosts built on Autodesk's AdWindows
// ribbon: Revit, AutoCAD and all verticals (Civil 3D, Plant 3D, MEP, Map 3D, Advance Steel), Navisworks.
// Late-bound on purpose: no compile-time reference to AdWindows.dll, so one build fits every year.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BurakBim.Shared;

namespace BurakBim.Bridge
{
    public static class RibbonAutomation
    {
        public sealed class Entry
        {
            public string Tab, TabId, Panel, Path, Id, Text, Kind, Macro, Tooltip;
            public bool Enabled, Visible, Contextual;
            public object Item;

            public Dictionary<string, object> ToJson()
            {
                var d = Json.O("tab", Tab, "panel", Panel, "path", Path, "id", Id, "text", Text, "kind", Kind,
                    "enabled", Enabled, "visible", Visible);
                if (!string.IsNullOrEmpty(Macro)) d["macro"] = Macro;
                if (!string.IsNullOrEmpty(Tooltip)) d["tooltip"] = Tooltip;
                if (Contextual) d["contextual"] = true;
                return d;
            }
        }

        public static object GetRibbon()
        {
            var cm = Reflector.FindType("Autodesk.Windows.ComponentManager");
            if (cm == null) throw new BridgeException("Autodesk ribbon (AdWindows) is not loaded in this host yet. Wait until the application window is fully open.");
            var p = cm.GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static);
            var ribbon = p == null ? null : p.GetValue(null, null);
            if (ribbon == null) throw new BridgeException("Ribbon is not available (application UI not created yet).");
            return ribbon;
        }

        static object Prop(object o, string name)
        {
            if (o == null) return null;
            try
            {
                var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                return p == null || p.GetIndexParameters().Length > 0 ? null : p.GetValue(o, null);
            }
            catch { return null; }
        }

        static string S(object o, string name)
        {
            var v = Prop(o, name);
            if (v == null) return null;
            var s = v as string;
            if (s != null) return s;
            // ToolTip can be a RibbonToolTip object
            var title = Prop(v, "Content") ?? Prop(v, "Title");
            return title != null ? title.ToString() : v.ToString();
        }

        static bool B(object o, string name, bool def)
        {
            var v = Prop(o, name);
            return v is bool ? (bool)v : def;
        }

        public static List<Entry> Enumerate(bool includeHidden)
        {
            var result = new List<Entry>();
            var ribbon = GetRibbon();
            var tabs = Prop(ribbon, "Tabs") as IEnumerable;
            if (tabs == null) return result;
            foreach (var tab in tabs)
            {
                var tabVisible = B(tab, "IsVisible", true);
                if (!includeHidden && !tabVisible) continue;
                var tabTitle = S(tab, "Title") ?? S(tab, "Name") ?? S(tab, "Id");
                var tabId = S(tab, "Id");
                var contextual = B(tab, "IsContextualTab", false);
                var panels = Prop(tab, "Panels") as IEnumerable;
                if (panels == null) continue;
                foreach (var panel in panels)
                {
                    var src = Prop(panel, "Source");
                    var panelTitle = S(src, "Title") ?? S(src, "Name") ?? S(src, "Id") ?? "";
                    var items = Prop(src, "Items") as IEnumerable;
                    if (items != null) Walk(items, tabTitle, tabId, panelTitle, tabTitle + " > " + panelTitle, contextual, includeHidden, result, 0);
                    var slide = Prop(src, "SlideOutPanelItems") as IEnumerable;
                    if (slide != null) Walk(slide, tabTitle, tabId, panelTitle, tabTitle + " > " + panelTitle + " (slide-out)", contextual, includeHidden, result, 0);
                }
            }
            return result;
        }

        static void Walk(IEnumerable items, string tab, string tabId, string panel, string prefix, bool contextual, bool includeHidden, List<Entry> result, int depth)
        {
            if (depth > 6) return;
            foreach (var item in items)
            {
                if (item == null) continue;
                var kind = item.GetType().Name;
                if (kind == "RibbonSeparator" || kind == "RibbonPanelBreak" || kind == "RibbonRowBreak") continue;
                var visible = B(item, "IsVisible", true);
                if (!includeHidden && !visible) continue;
                var text = S(item, "Text") ?? S(item, "Name") ?? S(item, "Id") ?? "";
                text = System.Text.RegularExpressions.Regex.Replace(text.Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim();
                var children = Prop(item, "Items") as IEnumerable;
                bool isContainer = children != null && !(item is string);
                var hasCommand = Prop(item, "CommandHandler") != null || !string.IsNullOrEmpty(S(item, "Id"));
                if (!isContainer || kind.Contains("SplitButton") || kind.Contains("ListButton"))
                {
                    if (hasCommand || text.Length > 0)
                    {
                        result.Add(new Entry
                        {
                            Tab = tab, TabId = tabId, Panel = panel, Kind = kind, Text = text,
                            Id = S(item, "Id"), Path = prefix + " > " + (text.Length > 0 ? text : S(item, "Id")),
                            Enabled = B(item, "IsEnabled", true), Visible = visible, Contextual = contextual,
                            Macro = Prop(item, "CommandParameter") as string, Tooltip = S(item, "ToolTip") ?? S(item, "Description"),
                            Item = item
                        });
                    }
                }
                if (isContainer)
                    Walk(children, tab, tabId, panel, kind.Contains("RowPanel") || kind.Contains("Fold") || text.Length == 0 ? prefix : prefix + " > " + text, contextual, includeHidden, result, depth + 1);
            }
        }

        public static Entry Find(string idOrPath)
        {
            if (string.IsNullOrWhiteSpace(idOrPath)) throw new BridgeException("Give a ribbon item id, a path like 'Architecture > Build > Wall', or button text.");
            var all = Enumerate(true);
            var q = idOrPath.Trim();
            Func<string, string> norm = s => (s ?? "").Replace(" ", "").Replace(">", "/").ToLowerInvariant();
            var hit = all.FirstOrDefault(e => string.Equals(e.Id, q, StringComparison.OrdinalIgnoreCase))
                   ?? all.FirstOrDefault(e => norm(e.Path) == norm(q))
                   ?? all.FirstOrDefault(e => norm(e.Path).EndsWith(norm(q)))
                   ?? all.FirstOrDefault(e => string.Equals(e.Text, q, StringComparison.OrdinalIgnoreCase));
            if (hit == null)
            {
                var near = all.Where(e => (e.Path ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(10).Select(e => e.Path).ToArray();
                throw new BridgeException("Ribbon item '" + q + "' not found." + (near.Length > 0 ? " Close matches: " + string.Join(" | ", near) : " Use ribbon_list to browse."));
            }
            return hit;
        }

        /// <summary>Press a ribbon item through its own command handler (call on the UI thread).</summary>
        public static object Invoke(Entry e)
        {
            if (!e.Enabled) throw new BridgeException("'" + e.Path + "' is disabled in the current context (wrong view, nothing selected, or a contextual tab).");
            var handler = Prop(e.Item, "CommandHandler") as System.Windows.Input.ICommand;
            if (handler == null)
            {
                // some items route through the ribbon's global handler
                var ribbon = GetRibbon();
                var ribbonHandler = Prop(ribbon, "CommandHandler") as System.Windows.Input.ICommand;
                handler = ribbonHandler;
            }
            if (handler == null) throw new BridgeException("'" + e.Path + "' has no command handler that can be invoked programmatically.");
            if (!handler.CanExecute(e.Item)) throw new BridgeException("'" + e.Path + "' cannot execute right now.");
            handler.Execute(e.Item);
            return Json.O("invoked", e.Path, "id", e.Id, "via", "ribbon-handler");
        }
    }
}
