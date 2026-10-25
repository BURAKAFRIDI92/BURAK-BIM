using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Com;

/// <summary>
/// Out-of-process COM automation for Autodesk apps that expose a COM API:
/// Inventor ("Inventor.Application"), AutoCAD + all verticals ("AutoCAD.Application"),
/// Robot ("Robot.Application"), and any other ProgID. Late-bound: every version works.
/// </summary>
public static class ComInterop
{
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)] static extern int CLSIDFromProgID(string progId, out Guid clsid);
    [DllImport("oleaut32.dll")] static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object obj);

    static readonly Dictionary<int, object> Handles = new();
    static int _next = 1;

    public static string Keep(object o) { lock (Handles) { var id = _next++; Handles[id] = o; return "$c" + id; } }

    public static object FromHandle(string h)
    {
        if (h.StartsWith("$c") && int.TryParse(h[2..], out var id))
            lock (Handles) if (Handles.TryGetValue(id, out var o)) return o;
        throw new ToolException("Unknown or expired COM handle " + h);
    }

    public static object? GetActive(string progId)
    {
        if (!OperatingSystem.IsWindows()) throw new ToolException("COM automation needs Windows.");
        if (CLSIDFromProgID(progId, out var clsid) != 0) return null;
        return GetActiveObject(ref clsid, IntPtr.Zero, out var o) == 0 ? o : null;
    }

    /// <summary>Attach to the running application, or start it when launch=true.</summary>
    public static object Connect(string progId, bool launch, bool visible = true)
    {
        if (!Config.AllowCom) throw new ToolException("COM automation is disabled in burakbim.json (allowComAutomation=false).");
        var o = GetActive(progId);
        if (o != null) return o;
        if (!launch) throw new ToolException($"'{progId}' is not running. Start the application, or call again with launch=true.");
        var t = Type.GetTypeFromProgID(progId) ?? throw new ToolException($"ProgID '{progId}' is not registered (application not installed?).");
        o = Activator.CreateInstance(t)!;
        if (visible) { try { Set(o, "Visible", true); } catch { } }
        return o;
    }

    public static object? Get(object o, string member, params object?[] args) =>
        o.GetType().InvokeMember(member, BindingFlags.GetProperty, null, o, args);

    public static void Set(object o, string member, object? value) =>
        o.GetType().InvokeMember(member, BindingFlags.SetProperty, null, o, new[] { value });

    public static object? Call(object o, string member, params object?[] args) =>
        o.GetType().InvokeMember(member, BindingFlags.InvokeMethod, null, o, args);

    /// <summary>Walk "A.B.Item(3).C" from a COM object.</summary>
    public static object? Eval(object root, string? path)
    {
        object? cur = root;
        if (string.IsNullOrWhiteSpace(path)) return cur;
        foreach (var raw in SplitPath(path))
        {
            if (cur == null) throw new ToolException($"null before '{raw}' in {path}");
            var seg = raw.Trim();
            var paren = seg.IndexOf('(');
            if (paren > 0 && seg.EndsWith(")"))
            {
                var name = seg[..paren];
                var argText = seg[(paren + 1)..^1].Trim();
                var args = argText.Length == 0 ? Array.Empty<object?>() : ((List<object>)Json.Parse("[" + argText + "]")).Select(ToCom).ToArray();
                try { cur = Get(cur, name, args); }
                catch { cur = Call(cur, name, args); }
            }
            else
            {
                try { cur = Get(cur, seg); }
                catch (TargetInvocationException ex) { throw new ToolException($"'{seg}': {ex.InnerException?.Message ?? ex.Message}"); }
                catch (MissingMethodException) { cur = Call(cur, seg); }
            }
        }
        return cur;
    }

    static IEnumerable<string> SplitPath(string path)
    {
        int depth = 0, start = 0;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '(') depth++;
            else if (path[i] == ')') depth--;
            else if (path[i] == '.' && depth == 0) { yield return path[start..i]; start = i + 1; }
        }
        yield return path[start..];
    }

    public static object? ToCom(object? v) => v switch
    {
        string s when s.StartsWith("$c") => FromHandle(s),
        long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
        List<object> list => list.Select(ToCom).ToArray(),
        _ => v
    };

    public static object? Describe(object? o, int maxItems = 100)
    {
        switch (o)
        {
            case null: return null;
            case string or bool or double or float or int or long or short or byte or decimal or DateTime: return o;
            case Array arr:
                return arr.Cast<object?>().Take(maxItems).Select(x => Describe(x, maxItems)).ToList();
        }
        if (o.GetType().IsEnum) return o.ToString();
        if (Marshal.IsComObject(o))
        {
            var d = Json.O("$c", Keep(o), "type", ComTypeName(o));
            foreach (var p in new[] { "Name", "DisplayName", "InternalName", "FullFileName", "Count", "Type", "Value", "Expression" })
            {
                try { var v = Get(o, p); if (v is string or double or int or bool or long or short) d[p] = v; } catch { }
            }
            return d;
        }
        return o.ToString();
    }

    // ---------------- type information (members of any COM object) ----------------
    [ComImport, Guid("00020400-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDispatchInfo
    {
        [PreserveSig] int GetTypeInfoCount(out uint count);
        [PreserveSig] int GetTypeInfo(uint index, uint lcid, out ITypeInfo? info);
    }

    public static string ComTypeName(object o)
    {
        try
        {
            if (o is IDispatchInfo disp && disp.GetTypeInfo(0, 0, out var ti) == 0 && ti != null)
            {
                ti.GetDocumentation(-1, out var name, out _, out _, out _);
                return name;
            }
        }
        catch { }
        return "COM object";
    }

    public static Dictionary<string, object> Members(object o, string? search)
    {
        if (o is not IDispatchInfo disp || disp.GetTypeInfo(0, 0, out var ti) != 0 || ti == null)
            throw new ToolException("This COM object exposes no type information.");
        ti.GetDocumentation(-1, out var typeName, out var typeDoc, out _, out _);
        ti.GetTypeAttr(out var pAttr);
        var attr = Marshal.PtrToStructure<TYPEATTR>(pAttr);
        var props = new SortedDictionary<string, string>();
        var methods = new SortedDictionary<string, string>();
        try
        {
            for (int i = 0; i < attr.cFuncs; i++)
            {
                ti.GetFuncDesc(i, out var pFunc);
                try
                {
                    var f = Marshal.PtrToStructure<FUNCDESC>(pFunc);
                    var names = new string[f.cParams + 1];
                    ti.GetNames(f.memid, names, names.Length, out var got);
                    var name = names[0];
                    if (name.StartsWith("_") || name is "QueryInterface" or "AddRef" or "Release" or "GetTypeInfoCount" or "GetTypeInfo" or "GetIDsOfNames" or "Invoke") continue;
                    if (!string.IsNullOrEmpty(search) && !name.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                    ti.GetDocumentation(f.memid, out _, out var doc, out _, out _);
                    var ps = string.Join(", ", names.Skip(1).Take(Math.Max(0, got - 1)));
                    var kind = f.invkind;
                    if (kind.HasFlag(INVOKEKIND.INVOKE_PROPERTYGET) || kind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUT) || kind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUTREF))
                    {
                        var rw = kind.HasFlag(INVOKEKIND.INVOKE_PROPERTYGET) ? "get" : "set";
                        props[name] = props.TryGetValue(name, out var prev) ? (prev.Contains(rw) ? prev : prev + "/" + rw) : rw + (ps.Length > 0 ? "(" + ps + ")" : "") + (string.IsNullOrEmpty(doc) ? "" : " - " + doc);
                    }
                    else methods[name] = "(" + ps + ")" + (string.IsNullOrEmpty(doc) ? "" : " - " + doc);
                }
                finally { ti.ReleaseFuncDesc(pFunc); }
            }
        }
        finally { ti.ReleaseTypeAttr(pAttr); }
        return Json.O("type", typeName, "doc", typeDoc,
            "properties", props.ToDictionary(k => k.Key, v => (object)v.Value),
            "methods", methods.ToDictionary(k => k.Key, v => (object)v.Value));
    }

    // ---------------- type-library constants (enums such as Robot I_LT_SUPPORT, Inventor kPartDocumentObject) ----------------
    static readonly Dictionary<string, Dictionary<string, object>> ConstCache = new();

    public static Dictionary<string, object> Constants(object comObj)
    {
        if (comObj is not IDispatchInfo disp || disp.GetTypeInfo(0, 0, out var ti) != 0 || ti == null)
            throw new ToolException("No type information on this COM object.");
        ti.GetContainingTypeLib(out var lib, out _);
        lib.GetDocumentation(-1, out var libName, out _, out _, out _);
        lock (ConstCache)
        {
            if (ConstCache.TryGetValue(libName, out var cached)) return cached;
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            int count = lib.GetTypeInfoCount();
            for (int i = 0; i < count; i++)
            {
                lib.GetTypeInfoType(i, out var kind);
                if (kind != TYPEKIND.TKIND_ENUM) continue;
                lib.GetTypeInfo(i, out var eti);
                eti.GetTypeAttr(out var pAttr);
                try
                {
                    var attr = Marshal.PtrToStructure<TYPEATTR>(pAttr);
                    for (int v = 0; v < attr.cVars; v++)
                    {
                        eti.GetVarDesc(v, out var pVar);
                        try
                        {
                            var vd = Marshal.PtrToStructure<VARDESC>(pVar);
                            var names = new string[1];
                            eti.GetNames(vd.memid, names, 1, out _);
                            if (vd.varkind == VARKIND.VAR_CONST && vd.desc.lpvarValue != IntPtr.Zero)
                                map[names[0]] = Marshal.GetObjectForNativeVariant(vd.desc.lpvarValue)!;
                        }
                        finally { eti.ReleaseVarDesc(pVar); }
                    }
                }
                finally { eti.ReleaseTypeAttr(pAttr); }
            }
            ConstCache[libName] = map;
            return map;
        }
    }

    /// <summary>Value of a named type-library constant, e.g. Const(robotApp, "I_LT_SUPPORT").</summary>
    public static object Const(object comObj, string name)
    {
        var map = Constants(comObj);
        if (map.TryGetValue(name, out var v)) return v;
        throw new ToolException($"Constant '{name}' not found in the application's type library. Use com_constants to search.");
    }
}
