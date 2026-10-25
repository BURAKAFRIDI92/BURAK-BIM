using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BurakBim.Bridge
{
    /// <summary>What a host (Revit, AutoCAD family, Navisworks) supplies to the shared bridge core.</summary>
    public interface IHostAdapter
    {
        string Host { get; }       // "revit" | "autocad" | "navisworks"
        string Product { get; }    // human name incl. vertical, e.g. "Autodesk Civil 3D 2024"
        int Year { get; }

        /// <summary>Run on the host main thread (API context) and return the result.</summary>
        object InvokeOnMain(Func<object> work, int timeoutMs);

        /// <summary>Named roots usable in api.* paths, e.g. "doc", "uiapp", "app".</summary>
        IDictionary<string, Func<object>> Roots { get; }

        /// <summary>Run a model-changing action inside the host's undo/transaction wrapper.</summary>
        object RunWrite(string label, Func<object> work);

        /// <summary>Script globals object (derives from ScriptGlobals) for script.run.</summary>
        ScriptGlobals CreateScriptGlobals();
        string[] ScriptImports { get; }

        /// <summary>Host-specific ribbon invocation (Revit uses PostCommand). Return null to fall back to the item's CommandHandler.</summary>
        object TryInvokeRibbonItem(string id, object item);
    }

    /// <summary>Base for script globals. Scripts can call Print(...) to stream text back.</summary>
    public class ScriptGlobals
    {
        public readonly List<string> Output = new List<string>();
        public void Print(object value) { Output.Add(value == null ? "null" : value.ToString()); }
        /// <summary>Free-form arguments passed with the script call.</summary>
        public Dictionary<string, object> Args = new Dictionary<string, object>();

        // ---- argument helpers for tool-pack snippets ----
        public bool Has(string k) { return Args != null && Args.ContainsKey(k) && Args[k] != null; }
        public string S(string k, string def = null) { return BurakBim.Shared.Json.Str(Args, k, def); }
        public double N(string k, double def = 0) { return BurakBim.Shared.Json.Num(Args, k, def); }
        public int I(string k, int def = 0) { return (int)BurakBim.Shared.Json.Int(Args, k, def); }
        public bool B(string k, bool def = false) { return BurakBim.Shared.Json.Bool(Args, k, def); }
        public List<long> L(string k) { return BurakBim.Shared.Json.IntList(Args, k); }
        public List<string> SL(string k) { return BurakBim.Shared.Json.StrList(Args, k); }
        public Dictionary<string, object> O(string k) { return BurakBim.Shared.Json.Obj(Args, k); }
        public List<object> A(string k) { return BurakBim.Shared.Json.Arr(Args, k); }
        public object Raw(string k) { return BurakBim.Shared.Json.Get(Args, k); }
        /// <summary>Build a JSON object: R("a", 1, "b", "x").</summary>
        public Dictionary<string, object> R(params object[] kv) { return BurakBim.Shared.Json.O(kv); }
        public void Fail(string message) { throw new BridgeException(message); }
    }

    /// <summary>Loads bridge dependencies (Roslyn etc.) from the bridge folder on the install drive.</summary>
    public static class BridgeAssemblyResolver
    {
        static readonly List<string> Probe = new List<string>();
        static bool _hooked;

        public static void Register(params string[] dirs)
        {
            foreach (var d in dirs)
                if (!string.IsNullOrEmpty(d) && Directory.Exists(d) && !Probe.Contains(d)) Probe.Add(d);
            if (_hooked) return;
            _hooked = true;
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                var name = new AssemblyName(e.Name).Name;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    if (string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase)) return a;
                foreach (var dir in Probe)
                {
                    var f = Path.Combine(dir, name + ".dll");
                    if (File.Exists(f)) { try { return Assembly.LoadFrom(f); } catch { } }
                }
                return null;
            };
        }
    }
}
