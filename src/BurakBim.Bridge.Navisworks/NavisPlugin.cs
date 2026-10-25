// BURAK BIM - Navisworks Manage / Simulate bridge (2022-2027).
// Only the plugin entry point is compiled against the Navisworks API; everything else is late-bound
// (dynamic/reflection) so one source fits every Navisworks year.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using Autodesk.Navisworks.Api.Plugins;
using BurakBim.Shared;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace BurakBim.Bridge.Navisworks
{
    [Plugin("BurakBim.Bridge", "BURK", DisplayName = "BURAK BIM", ToolTip = "BURAK BIM AI connector for Navisworks")]
    public sealed class NavisPlugin : EventWatcherPlugin
    {
        internal static BridgeServer Server;
        internal static NavisHost Host;

        public override void OnLoaded()
        {
            try
            {
                var here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                // heavy dependencies (Roslyn) live on the install drive, not next to this small plugin
                var probe = ReadProbeDir(here);
                BridgeAssemblyResolver.Register(here, probe);
                Host = new NavisHost(here, probe);
                BridgeLog.Name = "navisworks-" + Host.Year;
                Server = new BridgeServer(Host);
                NavisCommands.Register(Server, Host);
                Server.Start();
            }
            catch (Exception ex)
            {
                BridgeLog.Write("OnLoaded failed: " + ex);
            }
        }

        public override void OnUnloading()
        {
            try { if (Server != null) Server.Stop(); } catch { }
        }

        static string ReadProbeDir(string here)
        {
            try
            {
                var f = Path.Combine(here, "burakbim.probe");
                if (File.Exists(f)) return File.ReadAllText(f).Trim();
            }
            catch { }
            return null;
        }
    }

    public sealed class NavisHost : IHostAdapter
    {
        readonly SynchronizationContext _ctx;
        readonly Dispatcher _dispatcher;
        readonly MainThreadQueue _queue;
        readonly Dictionary<string, Func<object>> _roots;
        readonly int _year;

        public NavisHost(string here, string probe)
        {
            _ctx = SynchronizationContext.Current;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _queue = new MainThreadQueue(Kick);
            _year = DetectYear(here, probe);
            _roots = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase)
            {
                { "doc", () => NwApp.ActiveDocument },
                { "app", () => typeof(NwApp) },
                { "clash", () => NavisCommands.ClashData() },
                { "timeliner", () => NavisCommands.Timeliner() },
                { "selection", () => NavisCommands.Doc().CurrentSelection.SelectedItems }
            };
        }

        void Kick()
        {
            if (_ctx != null) _ctx.Post(_ => _queue.Drain(), null);
            else _dispatcher.BeginInvoke(new Action(() => _queue.Drain()));
        }

        static int DetectYear(string here, string probe)
        {
            int y;
            foreach (var d in new[] { probe, here })
                if (!string.IsNullOrEmpty(d) && int.TryParse(Path.GetFileName(d.TrimEnd('\\', '/')), out y) && y > 2000) return y;
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var m = System.Text.RegularExpressions.Regex.Match(exe, @"20\d\d");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        public string Host { get { return "navisworks"; } }
        public string Product
        {
            get
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                var dir = Path.GetFileName(Path.GetDirectoryName(exe)) ?? "Navisworks";
                return dir.StartsWith("Navisworks") ? "Autodesk " + dir : "Autodesk Navisworks " + _year;
            }
        }
        public int Year { get { return _year; } }
        public IDictionary<string, Func<object>> Roots { get { return _roots; } }

        public object InvokeOnMain(Func<object> work, int timeoutMs) { return _queue.Invoke(work, timeoutMs); }

        public object RunWrite(string label, Func<object> work)
        {
            dynamic doc = NwApp.ActiveDocument;
            dynamic tx = null;
            try { tx = doc.BeginTransaction(label); } catch { }
            try
            {
                var r = work();
                if (tx != null) tx.Commit();
                return r;
            }
            finally
            {
                if (tx != null) { try { tx.Dispose(); } catch { } }
            }
        }

        public ScriptGlobals CreateScriptGlobals()
        {
            // make Clash / TimeLiner / COM interop assemblies available to the C# compiler
            foreach (var asm in new[] { "Autodesk.Navisworks.Clash", "Autodesk.Navisworks.Timeliner", "Autodesk.Navisworks.ComApi", "Autodesk.Navisworks.Interop.ComApi" })
            {
                try
                {
                    if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == asm)) continue;
                    var f = Path.Combine(Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName), asm + ".dll");
                    if (File.Exists(f)) Assembly.LoadFrom(f);
                }
                catch { }
            }
            return new NavisScriptGlobals { doc = NwApp.ActiveDocument };
        }

        public string[] ScriptImports
        {
            get
            {
                var list = new List<string> { "System", "System.Linq", "System.Collections.Generic", "Autodesk.Navisworks.Api" };
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Autodesk.Navisworks.Clash")) list.Add("Autodesk.Navisworks.Api.Clash");
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Autodesk.Navisworks.Timeliner")) list.Add("Autodesk.Navisworks.Api.Timeliner");
                return list.ToArray();
            }
        }

        public object TryInvokeRibbonItem(string id, object item) { return null; }
    }

    /// <summary>Globals for Navisworks C# scripts and tool-pack snippets (helpers are late-bound so one build fits 2022-2027).</summary>
    public class NavisScriptGlobals : ScriptGlobals
    {
        public Autodesk.Navisworks.Api.Document doc;

        /// <summary>Items from the 'handles' argument, a 'search' argument {category, property, value, op}, or the current selection.</summary>
        public dynamic Items() { return NavisCommands.ItemsFrom(Args); }
        /// <summary>Items found by a property search.</summary>
        public dynamic Find(string category, string property, string value = null, string op = null)
        {
            var p = BurakBim.Shared.Json.O("category", category, "property", property);
            if (value != null) p["value"] = value;
            p["op"] = op ?? (value == null ? "defined" : "equals");
            return NavisCommands.Find(p);
        }
        public dynamic NewItems() { return NavisCommands.NewCollection(); }
        public Dictionary<string, object> Desc(object item, bool path = true) { return NavisCommands.Item((dynamic)item, path); }
        public List<object> DescAll(System.Collections.IEnumerable items, int limit = 300)
        {
            var r = new List<object>();
            foreach (var it in items) { if (r.Count >= limit) break; r.Add(NavisCommands.Item((dynamic)it, true)); }
            return r;
        }
        public string PathOf(object item) { return NavisCommands.PathOf((dynamic)item); }
        public string Keep(object o) { return Reflector.Keep(o); }
        public object FromHandle(string h) { return Reflector.FromHandle(h); }
        /// <summary>Clash Detective document part (DocumentClash).</summary>
        public dynamic ClashDoc() { return NavisCommands.ClashData(); }
        /// <summary>TimeLiner document part (DocumentTimeliner).</summary>
        public dynamic TimelinerDoc() { return NavisCommands.Timeliner(); }
        /// <summary>Clash test by name.</summary>
        public dynamic Test(string name)
        {
            foreach (dynamic t in ClashDoc().TestsData.Tests)
                if (string.Equals((string)t.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return t;
            throw new BridgeException("Clash test '" + name + "' not found.");
        }
        /// <summary>Scale factor from model units to millimetres.</summary>
        public double ToMm
        {
            get
            {
                string u = ((dynamic)doc).Units.ToString();
                switch (u)
                {
                    case "Millimeters": return 1;
                    case "Centimeters": return 10;
                    case "Meters": return 1000;
                    case "Kilometers": return 1000000;
                    case "Inches": return 25.4;
                    case "Feet": return 304.8;
                    case "Yards": return 914.4;
                    case "Miles": return 1609344;
                    default: return 1;
                }
            }
        }
    }
}
