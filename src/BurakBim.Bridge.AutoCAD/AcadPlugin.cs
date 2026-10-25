// BURAK BIM - AutoCAD-family bridge. One plugin for AutoCAD and every AutoCAD-based product:
// Civil 3D, Plant 3D, AutoCAD Architecture/MEP/Electrical/Mechanical, Map 3D, Advance Steel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using BurakBim.Shared;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(BurakBim.Bridge.AutoCAD.AcadPlugin))]
[assembly: CommandClass(typeof(BurakBim.Bridge.AutoCAD.AcadUserCommands))]

namespace BurakBim.Bridge.AutoCAD
{
    public sealed class AcadPlugin : IExtensionApplication
    {
        internal static BridgeServer Server;
        internal static AcadHost Host;

        public void Initialize()
        {
            try
            {
                var here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                // The autoloader bundle (trusted location, no SECURELOAD prompt) holds only this small DLL;
                // heavy dependencies (Roslyn) live in the install folder named by burakbim.probe.
                var probe = ReadProbe(here);
                BridgeAssemblyResolver.Register(here, probe);
                Host = new AcadHost(probe ?? here);
                BridgeLog.Name = "autocad-" + Host.Year;
                Server = new BridgeServer(Host);
                AcadCommands.Register(Server, Host);
                Server.Start();
                WriteLine("\nBURAK BIM connector running for " + Host.Product + " (type BURAKBIM for status).");
            }
            catch (System.Exception ex)
            {
                BridgeLog.Write("Initialize failed: " + ex);
            }
        }

        public void Terminate()
        {
            try { if (Server != null) Server.Stop(); } catch { }
        }

        static string ReadProbe(string here)
        {
            try
            {
                var f = Path.Combine(here, "burakbim.probe");
                if (File.Exists(f)) { var d = File.ReadAllText(f).Trim(); if (Directory.Exists(d)) return d; }
            }
            catch { }
            return null;
        }

        internal static void WriteLine(string s)
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc != null) doc.Editor.WriteMessage(s);
            }
            catch { }
        }
    }

    public sealed class AcadUserCommands
    {
        [CommandMethod("BURAKBIM", CommandFlags.Session)]
        public void Status()
        {
            var s = AcadPlugin.Server;
            AcadPlugin.WriteLine("\nBURAK BIM " + BurakInfo.Version + " | " + (s != null && s.IsRunning ? "RUNNING on port " + s.Port : "STOPPED") +
                " | " + (AcadPlugin.Host != null ? AcadPlugin.Host.Product : "") + " | folder: " + BurakPaths.Root);
        }

        [CommandMethod("BURAKBIMTOGGLE", CommandFlags.Session)]
        public void Toggle()
        {
            var s = AcadPlugin.Server;
            if (s == null) return;
            if (s.IsRunning) s.Stop(); else s.Start();
            Status();
        }
    }

    public sealed class AcadHost : IHostAdapter
    {
        readonly Dispatcher _dispatcher;
        readonly MainThreadQueue _queue;
        readonly Dictionary<string, Func<object>> _roots;
        readonly int _year;
        readonly string _product;

        public AcadHost(string bridgeDir)
        {
            _dispatcher = Dispatcher.CurrentDispatcher; // Initialize() runs on the AutoCAD main thread
            _queue = new MainThreadQueue(() => _dispatcher.BeginInvoke(new Action(() => _queue.Drain())));
            _year = DetectYear(bridgeDir);
            _product = DetectProduct() + " " + _year;
            _roots = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase)
            {
                { "app", () => null },
                { "docs", () => AcApp.DocumentManager },
                { "doc", () => AcApp.DocumentManager.MdiActiveDocument },
                { "db", () => Db },
                { "ed", () => AcApp.DocumentManager.MdiActiveDocument == null ? null : AcApp.DocumentManager.MdiActiveDocument.Editor },
                { "civil", () => CivilDoc() }
            };
        }

        static int DetectYear(string dir)
        {
            int y;
            if (int.TryParse(Path.GetFileName(dir), out y) && y > 2000) return y;
            var v = AcApp.Version;
            if (v.Major == 24) return 2021 + v.Minor;
            if (v.Major == 25) return 2025 + v.Minor;
            return 2000 + v.Major + 1;
        }

        static bool Loaded(string asm)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => string.Equals(a.GetName().Name, asm, StringComparison.OrdinalIgnoreCase));
        }

        static string DetectProduct()
        {
            if (Loaded("AeccDbMgd")) return "Autodesk Civil 3D";
            if (Loaded("PnP3dObjectsMgd") || Loaded("PnPProjectManagerMgd")) return "AutoCAD Plant 3D";
            if (Loaded("ASObjectsMgd") || Loaded("AdvanceSteel.CADAccess")) return "Advance Steel";
            if (Loaded("AecbBuildingBaseMgd")) return "AutoCAD MEP";
            if (Loaded("AecBaseMgd")) return "AutoCAD Architecture";
            if (Loaded("ManagedMapApi")) return "AutoCAD Map 3D";
            if (Loaded("AcEMgd") || Loaded("AceMgd")) return "AutoCAD Electrical";
            return "AutoCAD";
        }

        internal static object CivilDoc()
        {
            var t = Reflector.FindType("Autodesk.Civil.ApplicationServices.CivilApplication");
            if (t == null) return null;
            var p = t.GetProperty("ActiveDocument", BindingFlags.Public | BindingFlags.Static);
            return p == null ? null : p.GetValue(null, null);
        }

        public static Database Db
        {
            get
            {
                var d = AcApp.DocumentManager.MdiActiveDocument;
                return d == null ? null : d.Database;
            }
        }

        public static Document RequireDoc()
        {
            var d = AcApp.DocumentManager.MdiActiveDocument;
            if (d == null) throw new BridgeException("No drawing is open.");
            return d;
        }

        public string Host { get { return "autocad"; } }
        public string Product { get { return _product; } }
        public int Year { get { return _year; } }
        public IDictionary<string, Func<object>> Roots { get { return _roots; } }

        public object InvokeOnMain(Func<object> work, int timeoutMs) { return _queue.Invoke(work, timeoutMs); }

        public object RunWrite(string label, Func<object> work)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return work();
            using (doc.LockDocument())
            {
                return work();
            }
        }

        public ScriptGlobals CreateScriptGlobals()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            return new AcadScriptGlobals
            {
                doc = doc,
                db = doc == null ? null : doc.Database,
                ed = doc == null ? null : doc.Editor,
                civil = CivilDoc()
            };
        }

        public string[] ScriptImports
        {
            get
            {
                return new[] { "System", "System.Linq", "System.Collections.Generic", "Autodesk.AutoCAD.ApplicationServices",
                    "Autodesk.AutoCAD.DatabaseServices", "Autodesk.AutoCAD.Geometry", "Autodesk.AutoCAD.EditorInput", "Autodesk.AutoCAD.Colors" };
            }
        }

        public object TryInvokeRibbonItem(string id, object item)
        {
            string macro = null;
            try { var p = item.GetType().GetProperty("CommandParameter"); if (p != null) macro = p.GetValue(item, null) as string; } catch { }
            if (string.IsNullOrEmpty(macro)) return null;
            AcadCommands.Send(macro.Replace("^C", "\x03"), false);
            return Json.O("sent", macro, "via", "ribbon macro");
        }
    }

    public class AcadScriptGlobals : ScriptGlobals
    {
        public Document doc;
        public Database db;
        public Editor ed;
        /// <summary>Civil 3D CivilDocument (null in plain AutoCAD).</summary>
        public object civil;
    }
}
