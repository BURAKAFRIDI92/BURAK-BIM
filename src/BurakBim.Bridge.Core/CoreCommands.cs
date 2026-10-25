// BURAK BIM: bridge methods common to every host.
using System;
using System.Collections.Generic;
using System.Linq;
using BurakBim.Shared;

namespace BurakBim.Bridge
{
    public static class CoreCommands
    {
        /// <summary>True when a value is already JSON (primitives, string-keyed dictionaries, lists of those).</summary>
        public static bool Plain(object o, int depth = 0)
        {
            if (o == null || o is string || o is bool || o is double || o is float || o is int || o is long || o is short || o is decimal || o is DateTime || o is Guid || o is Enum) return true;
            if (depth > 12) return false;
            var d = o as System.Collections.IDictionary;
            if (d != null)
            {
                foreach (System.Collections.DictionaryEntry e in d) { if (!(e.Key is string) || !Plain(e.Value, depth + 1)) return false; }
                return true;
            }
            var l = o as System.Collections.IList;
            if (l != null) { foreach (var x in l) if (!Plain(x, depth + 1)) return false; return true; }
            return false;
        }

        public static void RegisterAll(BridgeServer s, IHostAdapter host)
        {
            s.Register(new BridgeCommand
            {
                Name = "api.get", Description = "Evaluate an API path (doc.ActiveView.Name, $h3.Parameters, type:Full.Name.StaticProp).",
                Handler = p => Reflector.Describe(Reflector.Eval(Json.Str(p, "path"), host.Roots), 0, (int)Json.Int(p, "maxItems", 200))
            });

            s.Register(new BridgeCommand
            {
                Name = "api.call", Writes = true,
                Description = "Call any API method/constructor: target path or handle, method name ('new' for constructors), JSON args.",
                Handler = p =>
                {
                    var targetPath = Json.Str(p, "target");
                    var method = Json.Str(p, "method");
                    var args = Json.Arr(p, "args") ?? new List<object>();
                    bool write = Json.Bool(p, "transaction", true);
                    Func<object> work = () =>
                    {
                        var target = Reflector.Eval(targetPath, host.Roots);
                        var asType = target as Type;
                        object r = asType != null && (targetPath.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                            ? Reflector.CallMethod(null, asType, method, args, true)
                            : Reflector.CallMethod(target, target.GetType(), method, args, false);
                        return Reflector.Describe(r, 0, (int)Json.Int(p, "maxItems", 200));
                    };
                    return write ? host.RunWrite("BURAK BIM: " + method, work) : work();
                }
            });

            s.Register(new BridgeCommand
            {
                Name = "api.set", Writes = true, Description = "Set a writable property/field: path to the owner object, member name, JSON value.",
                Handler = p => host.RunWrite("BURAK BIM: set " + Json.Str(p, "member"), () =>
                {
                    var targetPath = Json.Str(p, "target");
                    var target = Reflector.Eval(targetPath, host.Roots);
                    var asType = target as Type;
                    if (asType != null && targetPath.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                        Reflector.SetMember(null, asType, Json.Str(p, "member"), Json.Get(p, "value"));
                    else
                        Reflector.SetMember(target, target.GetType(), Json.Str(p, "member"), Json.Get(p, "value"));
                    return Json.O("set", Json.Str(p, "member"), "ok", true);
                })
            });

            s.Register(new BridgeCommand
            {
                Name = "api.inspect", Description = "List constructors, properties, methods, fields and enum values of an object path or type:Full.Name.",
                Handler = p =>
                {
                    var path = Json.Str(p, "path");
                    var typeName = Json.Str(p, "type");
                    Type t;
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        t = Reflector.FindType(typeName);
                        if (t == null) throw new BridgeException("Type not found: " + typeName + ". Try api_types to search.");
                    }
                    else
                    {
                        var o = Reflector.Eval(path, host.Roots);
                        if (o == null) throw new BridgeException(path + " is null");
                        t = o as Type ?? o.GetType();
                    }
                    return Reflector.Members(t, Json.Str(p, "search"), (int)Json.Int(p, "limit", 300));
                }
            });

            s.Register(new BridgeCommand
            {
                Name = "api.types", MainThread = false, Description = "Search loaded API types by (partial) name.",
                Handler = p => Reflector.SearchTypes(Json.Str(p, "search", ""), (int)Json.Int(p, "limit", 100))
            });

            s.Register(new BridgeCommand
            {
                Name = "api.roots", MainThread = false, Description = "Named roots usable in api paths.",
                Handler = p => host.Roots.Keys.ToList()
            });

            s.Register(new BridgeCommand
            {
                Name = "ribbon.list", Description = "Every ribbon tab > panel > button (incl. add-in tabs). Filters: tab, search, includeHidden.",
                Handler = p =>
                {
                    var tab = Json.Str(p, "tab");
                    var search = Json.Str(p, "search");
                    int limit = (int)Json.Int(p, "limit", 400);
                    var all = RibbonAutomation.Enumerate(Json.Bool(p, "includeHidden", false));
                    var tabs = all.Select(e => e.Tab).Distinct().ToList();
                    IEnumerable<RibbonAutomation.Entry> q = all;
                    if (!string.IsNullOrEmpty(tab)) q = q.Where(e => string.Equals(e.Tab, tab, StringComparison.OrdinalIgnoreCase) || string.Equals(e.TabId, tab, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(search)) q = q.Where(e => (e.Path + " " + e.Id + " " + e.Tooltip).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                    var list = q.ToList();
                    return Json.O("tabs", tabs, "total", list.Count, "items", list.Take(limit).Select(e => (object)e.ToJson()).ToList(),
                        "truncated", list.Count > limit);
                }
            });

            s.Register(new BridgeCommand
            {
                Name = "ribbon.invoke", Writes = true, Description = "Press any ribbon button by id, 'Tab > Panel > Button' path, or text.",
                Handler = p =>
                {
                    var e = RibbonAutomation.Find(Json.Str(p, "item"));
                    var hostResult = host.TryInvokeRibbonItem(e.Id, e.Item);
                    if (hostResult != null) return hostResult;
                    return RibbonAutomation.Invoke(e);
                }
            });

            s.Register(new BridgeCommand
            {
                Name = "script.run", Writes = true,
                Description = "Run C# against the live API. Globals expose host objects; Print(x) streams text; last expression is returned.",
                Handler = p =>
                {
                    var code = Json.Str(p, "code");
                    if (string.IsNullOrWhiteSpace(code)) throw new BridgeException("code is empty");
                    var g = host.CreateScriptGlobals();
                    var args = Json.Obj(p, "args");
                    if (args != null) g.Args = args;
                    bool tx = Json.Bool(p, "transaction", true);
                    Func<object> run = () => ScriptRunner.Run(code, g, host.ScriptImports);
                    var result = tx ? host.RunWrite(Json.Str(p, "label", "BURAK BIM: script"), run) : run();
                    return Json.O("result", Plain(result) ? result : Reflector.Describe(result), "output", g.Output);
                }
            });

            s.Register(new BridgeCommand
            {
                Name = "handles.clear", MainThread = false, Description = "Release cached object handles.",
                Handler = p => { Reflector.ClearHandles(); return Json.O("cleared", true); }
            });
        }
    }
}
