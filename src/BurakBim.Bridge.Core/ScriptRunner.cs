// BURAK BIM script runner: compile and run C# against the live host API (full API surface).
// Built with Roslyn when BURAK_ROSLYN is defined (normal installer builds).
// Each distinct snippet is compiled once and cached, so tool-pack tools run in milliseconds after first use.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BurakBim.Bridge
{
    public static class ScriptRunner
    {
#if BURAK_ROSLYN
        static readonly Dictionary<string, Microsoft.CodeAnalysis.Scripting.ScriptRunner<object>> Cache =
            new Dictionary<string, Microsoft.CodeAnalysis.Scripting.ScriptRunner<object>>();
#endif

        public static bool Available
        {
            get
            {
#if BURAK_ROSLYN
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Run C# script text. Must be called on the host main thread.</summary>
        public static object Run(string code, ScriptGlobals globals, string[] imports)
        {
#if BURAK_ROSLYN
            var key = globals.GetType().FullName + "\n" + code;
            Microsoft.CodeAnalysis.Scripting.ScriptRunner<object> runner;
            lock (Cache)
            {
                if (!Cache.TryGetValue(key, out runner))
                {
                    var refs = new List<Assembly>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            if (a.IsDynamic || string.IsNullOrEmpty(a.Location)) continue;
                            if (!seen.Add(a.GetName().Name)) continue;
                            refs.Add(a);
                        }
                        catch { }
                    }
                    var options = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                        .WithReferences(refs)
                        .WithImports(imports ?? new string[0])
                        .WithEmitDebugInformation(false);
                    var script = Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.Create<object>(code, options, globals.GetType());
                    var errors = script.Compile().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
                    if (errors.Count > 0)
                        throw new BridgeException("C# compile error:\n" + string.Join("\n", errors.Take(15).Select(d => d.ToString()).ToArray()));
                    runner = script.CreateDelegate();
                    if (Cache.Count > 500) Cache.Clear();
                    Cache[key] = runner;
                }
            }
            return runner(globals).GetAwaiter().GetResult();
#else
            throw new BridgeException("C# scripting is not included in this bridge build. Re-run the BURAK BIM installer (it builds with Roslyn), or use api_get / api_call / api_set which reach the same API.");
#endif
        }
    }
}
