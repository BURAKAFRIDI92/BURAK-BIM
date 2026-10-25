// BURAK BIM universal API access: reach ANY public class, property or method of the host API
// (Revit, AutoCAD, Civil 3D, Navisworks ...) without a hand-written tool for it.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BurakBim.Shared;

namespace BurakBim.Bridge
{
    public static class Reflector
    {
        // ---------------- handle store ----------------
        static readonly Dictionary<int, object> Handles = new Dictionary<int, object>();
        static readonly Queue<int> Order = new Queue<int>();
        static int _next = 1;
        const int MaxHandles = 20000;

        public static string Keep(object o)
        {
            lock (Handles)
            {
                int id = _next++;
                Handles[id] = o;
                Order.Enqueue(id);
                while (Order.Count > MaxHandles) Handles.Remove(Order.Dequeue());
                return "$h" + id;
            }
        }

        public static object FromHandle(string h)
        {
            int id;
            if (h == null || !h.StartsWith("$h") || !int.TryParse(h.Substring(2), out id))
                throw new BridgeException("Not a handle: " + h);
            lock (Handles)
            {
                object o;
                if (Handles.TryGetValue(id, out o)) return o;
            }
            throw new BridgeException("Handle " + h + " expired. Re-query the object.");
        }

        public static void ClearHandles() { lock (Handles) { Handles.Clear(); Order.Clear(); } }

        // ---------------- type lookup ----------------
        public static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var t = Type.GetType(name, false);
            if (t != null) return t;
            Type bySimple = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.IsDynamic) continue;
                try
                {
                    t = a.GetType(name, false);
                    if (t != null) return t;
                }
                catch { }
            }
            if (name.IndexOf('.') < 0)
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.IsDynamic) continue;
                    Type[] types;
                    try { types = a.GetExportedTypes(); } catch { continue; }
                    foreach (var x in types)
                        if (x.Name == name) { if (bySimple == null) bySimple = x; }
                }
            }
            return bySimple;
        }

        // ---------------- path evaluation ----------------
        /// <summary>
        /// Path grammar:  root(.member | [index] | .Method())*
        /// root = named host root (doc, uidoc, uiapp, app, ...), a handle ($h12) or type:Full.Type.Name (static access).
        /// </summary>
        public static object Eval(string path, IDictionary<string, Func<object>> roots)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new BridgeException("Empty path.");
            var segs = Split(path.Trim());
            object cur;
            Type staticType = null;
            var first = segs[0];
            if (first.StartsWith("$h")) cur = FromHandle(first);
            else if (first.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                staticType = FindType(first.Substring(5));
                if (staticType == null) throw new BridgeException("Type not found: " + first.Substring(5));
                cur = null;
            }
            else
            {
                Func<object> f;
                if (roots == null || !roots.TryGetValue(first, out f))
                    throw new BridgeException("Unknown root '" + first + "'. Roots: " + (roots == null ? "" : string.Join(", ", roots.Keys)) + ", $h<handle>, type:<Full.Type.Name>.");
                cur = f();
            }
            for (int i = 1; i < segs.Count; i++)
            {
                var seg = segs[i];
                if (cur == null && staticType == null) throw new BridgeException("Null value before '" + seg + "' in " + path);
                cur = Step(cur, staticType, seg);
                staticType = null;
            }
            if (staticType != null) return staticType;
            return cur;
        }

        static List<string> Split(string path)
        {
            // split on '.' but keep [..] and (...) together
            var list = new List<string>();
            int depth = 0; int start = 0;
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (c == '[' || c == '(') depth++;
                else if (c == ']' || c == ')') depth--;
                else if (c == '.' && depth == 0)
                {
                    // allow "type:Autodesk.Revit.DB.XYZ.BasisZ" -> type part resolved greedily below
                    list.Add(path.Substring(start, i - start));
                    start = i + 1;
                }
                if (c == '[' && depth == 1 && i > start)
                {
                    // name[3] -> "name" and "[3]"
                }
            }
            list.Add(path.Substring(start));
            // merge type: segments greedily to the longest resolvable type name
            if (list[0].StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                int best = -1;
                for (int n = list.Count; n >= 1; n--)
                {
                    var candidate = string.Join(".", list.Take(n).ToArray());
                    if (FindType(candidate.Substring(5)) != null) { best = n; break; }
                }
                if (best > 1)
                {
                    var merged = new List<string> { string.Join(".", list.Take(best).ToArray()) };
                    merged.AddRange(list.Skip(best));
                    list = merged;
                }
            }
            // expand "Items[2]" into "Items", "[2]"
            var expanded = new List<string>();
            foreach (var s in list)
            {
                int b = s.IndexOf('[');
                if (b > 0 && s.EndsWith("]") && !s.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                {
                    expanded.Add(s.Substring(0, b));
                    expanded.Add(s.Substring(b));
                }
                else expanded.Add(s);
            }
            return expanded;
        }

        static object Step(object cur, Type staticType, string seg)
        {
            var type = staticType ?? cur.GetType();
            var flags = BindingFlags.Public | (staticType != null ? BindingFlags.Static : BindingFlags.Instance | BindingFlags.Static) | BindingFlags.FlattenHierarchy;
            if (seg.StartsWith("[") && seg.EndsWith("]"))
            {
                var key = seg.Substring(1, seg.Length - 2).Trim().Trim('"', '\'');
                int idx;
                var list = cur as IList;
                if (list != null && int.TryParse(key, out idx)) return list[idx];
                var dict = cur as IDictionary;
                if (dict != null) return dict[key];
                var en = cur as IEnumerable;
                if (en != null && int.TryParse(key, out idx)) return en.Cast<object>().ElementAt(idx);
                // indexer property
                var ip = type.GetProperties(flags).FirstOrDefault(p => p.GetIndexParameters().Length == 1);
                if (ip != null) return ip.GetValue(cur, new[] { ConvertArg(key, ip.GetIndexParameters()[0].ParameterType) });
                throw new BridgeException("Cannot index " + type.FullName);
            }
            if (seg.EndsWith(")"))
            {
                int p = seg.IndexOf('(');
                var name = seg.Substring(0, p);
                var argText = seg.Substring(p + 1, seg.Length - p - 2).Trim();
                var args = new List<object>();
                if (argText.Length > 0)
                {
                    var parsed = Json.Parse("[" + argText + "]") as List<object>;
                    if (parsed != null) args = parsed;
                }
                return CallMethod(cur, staticType ?? cur.GetType(), name, args, staticType != null);
            }
            var prop = type.GetProperty(seg, flags);
            if (prop == null)
            {
                try { prop = type.GetProperties(flags).FirstOrDefault(x => x.Name == seg && x.GetIndexParameters().Length == 0); } catch { }
            }
            if (prop != null && prop.GetIndexParameters().Length == 0) return prop.GetValue(staticType != null ? null : cur, null);
            var field = type.GetField(seg, flags);
            if (field != null) return field.GetValue(staticType != null ? null : cur);
            // zero-arg method without parentheses (GetTypeId)
            var m = type.GetMethods(flags).FirstOrDefault(x => x.Name == seg && x.GetParameters().Length == 0);
            if (m != null) return m.Invoke(staticType != null ? null : cur, null);
            // nested type
            var nested = type.GetNestedType(seg, BindingFlags.Public);
            if (nested != null) return nested;
            throw new BridgeException("'" + seg + "' is not a public property, field or method of " + type.FullName + ". Use api_inspect to list members.");
        }

        public static object CallMethod(object target, Type type, string name, IList<object> args, bool isStatic)
        {
            args = args ?? new List<object>();
            if (name == "new" || name == ".ctor")
            {
                foreach (var ctor in type.GetConstructors().OrderBy(c => c.GetParameters().Length))
                {
                    object[] conv;
                    if (TryConvertArgs(ctor.GetParameters(), args, out conv)) return ctor.Invoke(conv);
                }
                throw new BridgeException("No constructor of " + type.FullName + " accepts " + args.Count + " argument(s) of those types.");
            }
            var flags = BindingFlags.Public | BindingFlags.FlattenHierarchy | (isStatic ? BindingFlags.Static : BindingFlags.Instance | BindingFlags.Static);
            var candidates = type.GetMethods(flags).Where(m => m.Name == name).ToList();
            // extension methods (e.g. Navisworks GetClash) anywhere in loaded assemblies
            if (candidates.Count == 0 && !isStatic)
            {
                foreach (var em in FindExtensionMethods(name, type))
                {
                    var withThis = new List<object> { target };
                    withThis.AddRange(args);
                    object[] conv;
                    if (TryConvertArgs(em.GetParameters(), withThis, out conv)) return em.Invoke(null, conv);
                }
                throw new BridgeException("Method '" + name + "' not found on " + type.FullName + ".");
            }
            foreach (var m in candidates.OrderBy(m => Math.Abs(m.GetParameters().Length - args.Count)))
            {
                var ps = m.GetParameters();
                object[] conv;
                if (!TryConvertArgs(ps, args, out conv)) continue;
                if (m.ContainsGenericParameters) continue;
                return m.Invoke(m.IsStatic ? null : target, conv);
            }
            var sigs = string.Join(" | ", candidates.Select(Signature).ToArray());
            throw new BridgeException("No overload of " + type.Name + "." + name + " matches the " + args.Count + " argument(s). Overloads: " + sigs);
        }

        static IEnumerable<MethodInfo> FindExtensionMethods(string name, Type thisType)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.IsDynamic) continue;
                var an = a.GetName().Name ?? "";
                if (an.StartsWith("System") || an.StartsWith("mscorlib") || an.StartsWith("Microsoft.")) continue;
                Type[] types;
                try { types = a.GetExportedTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (!(t.IsAbstract && t.IsSealed)) continue;
                    MethodInfo[] ms;
                    try { ms = t.GetMethods(BindingFlags.Public | BindingFlags.Static); } catch { continue; }
                    foreach (var m in ms)
                    {
                        if (m.Name != name) continue;
                        var ps = m.GetParameters();
                        if (ps.Length > 0 && ps[0].ParameterType.IsAssignableFrom(thisType)) yield return m;
                    }
                }
            }
        }

        static bool TryConvertArgs(ParameterInfo[] ps, IList<object> args, out object[] converted)
        {
            converted = null;
            int required = ps.Count(p => !p.IsOptional);
            if (args.Count < required || args.Count > ps.Length) return false;
            var r = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (i >= args.Count) { r[i] = ps[i].DefaultValue == DBNull.Value ? null : ps[i].DefaultValue; continue; }
                var pt = ps[i].ParameterType;
                if (pt.IsByRef) pt = pt.GetElementType();
                try { r[i] = ConvertArg(args[i], pt); }
                catch { return false; }
                if (r[i] == null && args[i] != null) return false;
            }
            converted = r;
            return true;
        }

        /// <summary>
        /// JSON value -> CLR value. Supports handles ("$h12"), enums by name, numbers -> any numeric/ElementId-like
        /// single-number constructors, [x,y,z] -> any type with a 2/3-number constructor (XYZ, Point3d, UV ...),
        /// lists -> arrays/List&lt;T&gt;, {"$new":"Type","args":[...]} and {"$static":"type:Name.Member"}.
        /// </summary>
        public static object ConvertArg(object v, Type t)
        {
            if (v == null) return null;
            var s = v as string;
            if (s != null && s.StartsWith("$h")) { var o = FromHandle(s); if (t.IsInstanceOfType(o)) return o; throw new InvalidCastException(); }
            if (t == typeof(object)) return v;
            if (t.IsInstanceOfType(v) && !(v is IList) && !(v is IDictionary)) return v;
            var u = Nullable.GetUnderlyingType(t);
            if (u != null) t = u;
            if (t.IsEnum)
            {
                if (s != null) return Enum.Parse(t, s.Contains(".") ? s.Substring(s.LastIndexOf('.') + 1) : s, true);
                return Enum.ToObject(t, Convert.ToInt64(v, CultureInfo.InvariantCulture));
            }
            if (t == typeof(string)) return Convert.ToString(v, CultureInfo.InvariantCulture);
            if (t.IsPrimitive || t == typeof(decimal))
            {
                if (v is IList || v is IDictionary) throw new InvalidCastException();
                return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
            }
            var dict = v as Dictionary<string, object>;
            if (dict != null)
            {
                var newType = Json.Str(dict, "$new");
                if (newType != null)
                {
                    var nt = FindType(newType);
                    if (nt == null) throw new BridgeException("Type not found: " + newType);
                    return CallMethod(null, nt, "new", Json.Arr(dict, "args") ?? new List<object>(), true);
                }
                var st = Json.Str(dict, "$static");
                if (st != null) return Eval(st.StartsWith("type:") ? st : "type:" + st, null);
                var h = Json.Str(dict, "$h");
                if (h != null) return FromHandle(h);
            }
            var list = v as IList;
            if (list != null)
            {
                if (t.IsArray)
                {
                    var et = t.GetElementType();
                    var arr = Array.CreateInstance(et, list.Count);
                    for (int i = 0; i < list.Count; i++) arr.SetValue(ConvertArg(list[i], et), i);
                    return arr;
                }
                if (t.IsGenericType)
                {
                    var ga = t.GetGenericArguments();
                    if (ga.Length == 1 && t.IsAssignableFrom(typeof(List<>).MakeGenericType(ga[0])))
                    {
                        var l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ga[0]));
                        foreach (var x in list) l.Add(ConvertArg(x, ga[0]));
                        return l;
                    }
                }
                // numeric tuple -> ctor(double, double[, double])
                if (list.Count >= 2 && list.Count <= 4 && list.Cast<object>().All(x => x is double || x is long))
                {
                    foreach (var c in t.GetConstructors())
                    {
                        var ps = c.GetParameters();
                        if (ps.Length == list.Count && ps.All(p => p.ParameterType == typeof(double)))
                            return c.Invoke(list.Cast<object>().Select(x => (object)Json.ToDouble(x)).ToArray());
                    }
                }
                // ICollection<T> with Add
                var add = t.GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1);
                var defCtor = t.GetConstructor(Type.EmptyTypes);
                if (add != null && defCtor != null)
                {
                    var inst = defCtor.Invoke(null);
                    var et = add.GetParameters()[0].ParameterType;
                    foreach (var x in list) add.Invoke(inst, new[] { ConvertArg(x, et) });
                    return inst;
                }
            }
            if (v is double || v is long)
            {
                // ElementId(long) / ElementId(int) / ObjectId etc.
                foreach (var c in t.GetConstructors())
                {
                    var ps = c.GetParameters();
                    if (ps.Length != 1) continue;
                    var pt = ps[0].ParameterType;
                    if (pt == typeof(long) || pt == typeof(int) || pt == typeof(double))
                    {
                        try { return c.Invoke(new[] { Convert.ChangeType(v, pt, CultureInfo.InvariantCulture) }); } catch { }
                    }
                }
            }
            if (s != null)
            {
                // static Parse(string)
                var parse = t.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (parse != null) return parse.Invoke(null, new object[] { s });
                // static member by name, e.g. BuiltInCategory-like static properties
                var sp = t.GetProperty(s, BindingFlags.Public | BindingFlags.Static);
                if (sp != null && t.IsAssignableFrom(sp.PropertyType)) return sp.GetValue(null, null);
                var c1 = t.GetConstructor(new[] { typeof(string) });
                if (c1 != null) return c1.Invoke(new object[] { s });
            }
            throw new InvalidCastException("Cannot convert " + v.GetType().Name + " to " + t.FullName);
        }

        // ---------------- result description ----------------
        public static object Describe(object o, int depth = 0, int maxItems = 200)
        {
            if (o == null) return null;
            if (o is string || o is bool || o is double || o is float || o is decimal || o is int || o is long || o is short || o is byte || o is uint || o is ulong || o is DateTime || o is Guid)
                return o;
            if (o is Enum) return o.ToString();
            var t = o as Type;
            if (t != null) return Json.O("type", "System.Type", "fullName", t.FullName, "hint", "Use api_inspect with type:" + t.FullName);
            var d = new Dictionary<string, object>();
            d["$h"] = Keep(o);
            d["type"] = o.GetType().FullName;
            AddIfPresent(d, o, "Id");
            AddIfPresent(d, o, "Name");
            AddIfPresent(d, o, "DisplayName");
            AddIfPresent(d, o, "Title");
            if (o is IEnumerable && !(o is string))
            {
                if (depth > 0) { d["enumerable"] = true; return d; }
                var items = new List<object>();
                int count = 0;
                try
                {
                    foreach (var x in (IEnumerable)o)
                    {
                        if (count < maxItems) items.Add(Describe(x, depth + 1, maxItems));
                        count++;
                        if (count > 100000) break;
                    }
                }
                catch (Exception ex) { d["enumerateError"] = ex.Message; }
                d["count"] = count;
                d["items"] = items;
                if (count > maxItems) d["truncated"] = true;
                return d;
            }
            string text = null;
            try { text = o.ToString(); } catch { }
            if (text != null && text != o.GetType().FullName) d["text"] = text.Length > 300 ? text.Substring(0, 300) : text;
            return d;
        }

        static void AddIfPresent(Dictionary<string, object> d, object o, string prop)
        {
            try
            {
                var p = o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p == null || p.GetIndexParameters().Length > 0) return;
                var v = p.GetValue(o, null);
                if (v == null) return;
                if (v is string || v.GetType().IsPrimitive || v is Enum) d[prop.ToLowerInvariant()] = v is Enum ? v.ToString() : v;
                else
                {
                    var s = v.ToString();
                    if (!string.IsNullOrEmpty(s) && s != v.GetType().FullName) d[prop.ToLowerInvariant()] = s;
                }
            }
            catch { }
        }

        public static string Signature(MethodBase m)
        {
            var ps = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name + (p.IsOptional ? "?" : "")).ToArray());
            var mi = m as MethodInfo;
            return (mi != null ? mi.ReturnType.Name + " " : "") + m.Name + "(" + ps + ")" + (m.IsStatic ? " [static]" : "");
        }

        public static object Members(Type t, string search, int limit)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            Func<string, bool> ok = n => string.IsNullOrEmpty(search) || n.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            var props = t.GetProperties(flags).Where(p => ok(p.Name)).Take(limit)
                .Select(p => (object)(p.PropertyType.Name + " " + p.Name + (p.CanWrite ? " { get; set; }" : " { get; }") + (p.GetGetMethod() != null && p.GetGetMethod().IsStatic ? " [static]" : ""))).ToList();
            var methods = t.GetMethods(flags).Where(m => !m.IsSpecialName && ok(m.Name) && m.DeclaringType != typeof(object)).Take(limit)
                .Select(m => (object)Signature(m)).ToList();
            var ctors = t.GetConstructors().Select(c => (object)Signature(c)).ToList();
            var fields = t.GetFields(flags).Where(f => ok(f.Name)).Take(limit).Select(f => (object)(f.FieldType.Name + " " + f.Name + (f.IsStatic ? " [static]" : ""))).ToList();
            var r = Json.O("type", t.FullName, "baseType", t.BaseType != null ? t.BaseType.FullName : null,
                "assembly", t.Assembly.GetName().Name, "constructors", ctors, "properties", props, "methods", methods, "fields", fields);
            if (t.IsEnum) r["enumValues"] = Enum.GetNames(t).Where(ok).Take(limit * 4).Cast<object>().ToList();
            return r;
        }

        public static object SearchTypes(string search, int limit)
        {
            var r = new List<object>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.IsDynamic) continue;
                var an = a.GetName().Name ?? "";
                if (an.StartsWith("System") || an == "mscorlib" || an.StartsWith("Microsoft.") || an.StartsWith("netstandard")) continue;
                Type[] types;
                try { types = a.GetExportedTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t.FullName != null && t.FullName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        r.Add(t.FullName);
                        if (r.Count >= limit) return r;
                    }
                }
            }
            return r;
        }

        public static void SetMember(object target, Type type, string member, object value)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var p = type.GetProperty(member, flags);
            if (p != null && p.CanWrite) { p.SetValue(target, ConvertArg(value, p.PropertyType), null); return; }
            var f = type.GetField(member, flags);
            if (f != null && !f.IsInitOnly) { f.SetValue(target, ConvertArg(value, f.FieldType)); return; }
            throw new BridgeException("'" + member + "' is not a writable property/field of " + type.FullName);
        }
    }
}
