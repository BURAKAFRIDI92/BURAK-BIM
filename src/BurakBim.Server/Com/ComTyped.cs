using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Com;

/// <summary>
/// 1.3: type-aware COM calls with BY-REFERENCE output parameters.
/// APIs such as CSI's (SAP2000 / ETABS / CSiBridge / SAFE) return almost everything through ref arguments:
///   ret = SapModel.FrameObj.GetNameList(ref NumberNames, ref MyName[])
/// This reads the method signature from the application's type library, converts JSON arguments to the exact COM
/// types (int, double, string, bool, enums by name, typed SAFEARRAYs), creates placeholders for omitted output
/// parameters, calls by reference and returns every output parameter by name. So EVERY API function is callable
/// with plain JSON - no wrapper needed.
/// </summary>
public static class ComTyped
{
    [ComImport, Guid("00020400-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDispatchInfo
    {
        [PreserveSig] int GetTypeInfoCount(out uint count);
        [PreserveSig] int GetTypeInfo(uint index, uint lcid, out ITypeInfo? info);
    }

    public sealed class Param
    {
        public string Name = "";
        public string Type = "variant";   // int, double, float, string, bool, variant, object, enum, short, date
        public bool IsArray;
        public bool ByRef;
        public bool Out;
        public bool Optional;
        public string? EnumName;
        public override string ToString() => (ByRef ? "ref " : "") + Type + (IsArray ? "[]" : "") + " " + Name + (Optional ? " (optional)" : "");
    }

    public sealed class Signature
    {
        public string Name = "";
        public List<Param> Params = new();
        public string Returns = "variant";
        public string Kind = "method";
        public override string ToString() => $"{Returns} {Name}({string.Join(", ", Params)})";
    }

    static readonly Dictionary<string, Signature?> SigCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Signature of a method/property from the object's type information (null when not available).</summary>
    public static Signature? GetSignature(object comObj, string member)
    {
        if (comObj is not IDispatchInfo disp || disp.GetTypeInfo(0, 0, out var ti) != 0 || ti == null) return null;
        ti.GetDocumentation(-1, out var typeName, out _, out _, out _);
        var key = typeName + "." + member;
        lock (SigCache) if (SigCache.TryGetValue(key, out var cached)) return cached;
        Signature? sig = null;
        ti.GetTypeAttr(out var pAttr);
        try
        {
            var attr = Marshal.PtrToStructure<TYPEATTR>(pAttr);
            for (int i = 0; i < attr.cFuncs && sig == null; i++)
            {
                ti.GetFuncDesc(i, out var pFunc);
                try
                {
                    var f = Marshal.PtrToStructure<FUNCDESC>(pFunc);
                    var names = new string[f.cParams + 1];
                    ti.GetNames(f.memid, names, names.Length, out var got);
                    if (!string.Equals(names[0], member, StringComparison.OrdinalIgnoreCase)) continue;
                    if (f.invkind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUT) || f.invkind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUTREF)) continue;
                    var s = new Signature { Name = names[0], Kind = f.invkind.HasFlag(INVOKEKIND.INVOKE_PROPERTYGET) ? "property" : "method" };
                    int size = Marshal.SizeOf<ELEMDESC>();
                    for (int p = 0; p < f.cParams; p++)
                    {
                        var ed = Marshal.PtrToStructure<ELEMDESC>(f.lprgelemdescParam + p * size);
                        var prm = new Param { Name = p + 1 < got ? names[p + 1] : "arg" + p };
                        Describe(ti, ed.tdesc, prm, 0);
                        var flags = ed.desc.paramdesc.wParamFlags;
                        if (flags.HasFlag(PARAMFLAG.PARAMFLAG_FOUT)) prm.Out = true;
                        if (flags.HasFlag(PARAMFLAG.PARAMFLAG_FOPT) || flags.HasFlag(PARAMFLAG.PARAMFLAG_FHASDEFAULT)) prm.Optional = true;
                        if (p >= f.cParams - f.cParamsOpt && f.cParamsOpt > 0) prm.Optional = true;
                        s.Params.Add(prm);
                    }
                    var rp = new Param(); Describe(ti, f.elemdescFunc.tdesc, rp, 0);
                    s.Returns = rp.Type + (rp.IsArray ? "[]" : "");
                    sig = s;
                }
                finally { ti.ReleaseFuncDesc(pFunc); }
            }
        }
        finally { ti.ReleaseTypeAttr(pAttr); }
        lock (SigCache) SigCache[key] = sig;
        return sig;
    }

    static void Describe(ITypeInfo ti, TYPEDESC td, Param p, int depth)
    {
        if (depth > 6) return;
        var vt = (VarEnum)td.vt;
        switch (vt)
        {
            case VarEnum.VT_PTR:
                p.ByRef = true;
                Describe(ti, Marshal.PtrToStructure<TYPEDESC>(td.lpValue), p, depth + 1); return;
            case VarEnum.VT_SAFEARRAY:
                p.IsArray = true;
                Describe(ti, Marshal.PtrToStructure<TYPEDESC>(td.lpValue), p, depth + 1); return;
            case VarEnum.VT_I4: case VarEnum.VT_INT: case VarEnum.VT_UI4: case VarEnum.VT_UINT: p.Type = "int"; return;
            case VarEnum.VT_I2: case VarEnum.VT_UI2: p.Type = "short"; return;
            case VarEnum.VT_UI1: case VarEnum.VT_I1: p.Type = "byte"; return;
            case VarEnum.VT_I8: case VarEnum.VT_UI8: p.Type = "long"; return;
            case VarEnum.VT_R8: case VarEnum.VT_CY: case VarEnum.VT_DECIMAL: p.Type = "double"; return;
            case VarEnum.VT_R4: p.Type = "float"; return;
            case VarEnum.VT_BSTR: case VarEnum.VT_LPSTR: case VarEnum.VT_LPWSTR: p.Type = "string"; return;
            case VarEnum.VT_BOOL: p.Type = "bool"; return;
            case VarEnum.VT_DATE: p.Type = "date"; return;
            case VarEnum.VT_DISPATCH: case VarEnum.VT_UNKNOWN: p.Type = "object"; return;
            case VarEnum.VT_VOID: case VarEnum.VT_HRESULT: p.Type = "void"; return;
            case VarEnum.VT_USERDEFINED:
                try
                {
                    ti.GetRefTypeInfo((int)td.lpValue.ToInt64(), out var rti);
                    rti.GetTypeAttr(out var pa);
                    try
                    {
                        var a = Marshal.PtrToStructure<TYPEATTR>(pa);
                        rti.GetDocumentation(-1, out var en, out _, out _, out _);
                        if (a.typekind == TYPEKIND.TKIND_ENUM) { p.Type = "enum"; p.EnumName = en; }
                        else if (a.typekind == TYPEKIND.TKIND_ALIAS) Describe(rti, a.tdescAlias, p, depth + 1);
                        else p.Type = "object";
                    }
                    finally { rti.ReleaseTypeAttr(pa); }
                }
                catch { p.Type = "int"; }
                return;
            default: p.Type = "variant"; return;
        }
    }

    /// <summary>
    /// Call a method by name. args: JSON list (positional) or object (by parameter name, case-insensitive).
    /// Returns { ret, outputs: { paramName: value }, signature }.
    /// </summary>
    public static Dictionary<string, object> Call(object target, string method, object? args, object? enumSource = null)
    {
        var sig = GetSignature(target, method);
        object?[] values;
        ParameterModifier mods;
        if (sig != null)
        {
            var n = sig.Params.Count;
            values = new object?[n];
            mods = new ParameterModifier(Math.Max(1, n));
            var positional = args as List<object>;
            var named = args as Dictionary<string, object>;
            if (positional != null && positional.Count > n)
                throw new ToolException($"{method} takes {n} parameters, {positional.Count} given. Signature: {sig}");
            for (int i = 0; i < n; i++)
            {
                var p = sig.Params[i];
                object? given = null; bool has = false;
                if (positional != null && i < positional.Count) { given = positional[i]; has = true; }
                else if (named != null)
                {
                    var hit = named.Keys.FirstOrDefault(k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (hit != null) { given = named[hit]; has = true; }
                }
                if (!has || given == null)
                {
                    if (!p.ByRef && p.Optional) { values[i] = Type.Missing; continue; }
                    values[i] = Default(p);
                }
                else values[i] = Convert(given, p, enumSource ?? target);
                if (p.ByRef) mods[i] = true;
            }
        }
        else
        {
            // no type information: best effort (arrays and nulls by reference)
            var positional = args as List<object> ?? (args is Dictionary<string, object> d ? d.Values.ToList() : new List<object>());
            values = positional.Select(v => Loose(v)).ToArray();
            mods = new ParameterModifier(Math.Max(1, values.Length));
            for (int i = 0; i < values.Length; i++) if (values[i] == null || values[i] is Array) mods[i] = true;
        }

        object? ret;
        try
        {
            ret = target.GetType().InvokeMember(method, BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, target, values,
                values.Length > 0 ? new[] { mods } : null, CultureInfo.InvariantCulture, null);
        }
        catch (TargetInvocationException ex) { throw new ToolException($"{method}: {ex.InnerException?.Message ?? ex.Message}" + (sig != null ? $"  Signature: {sig}" : "")); }
        catch (COMException ex) { throw new ToolException($"{method}: COM error 0x{ex.HResult:X8} {ex.Message}" + (sig != null ? $"  Signature: {sig}" : "")); }
        catch (MissingMethodException) { throw new ToolException($"'{method}' does not exist on this object. Use csi_members / com_members to list members."); }

        var outputs = new Dictionary<string, object>();
        for (int i = 0; i < values.Length; i++)
        {
            bool isRef = sig != null ? sig.Params[i].ByRef : mods[i];
            if (!isRef) continue;
            var name = sig != null ? sig.Params[i].Name : "arg" + i;
            outputs[name] = Out(values[i]);
        }
        return Json.O("ret", Out(ret), "outputs", outputs, "signature", sig?.ToString());
    }

    static object? Default(Param p)
    {
        if (p.IsArray)
            return p.Type switch
            {
                "int" or "enum" => Array.Empty<int>(), "short" => Array.Empty<short>(), "double" => Array.Empty<double>(), "float" => Array.Empty<float>(),
                "string" => Array.Empty<string>(), "bool" => Array.Empty<bool>(), "long" => Array.Empty<long>(), "byte" => Array.Empty<byte>(),
                _ => Array.Empty<object>()
            };
        return p.Type switch
        {
            "int" or "enum" => 0, "short" => (short)0, "double" => 0.0, "float" => 0f, "string" => "", "bool" => false,
            "long" => 0L, "byte" => (byte)0, "date" => DateTime.MinValue, _ => null
        };
    }

    static object? Convert(object v, Param p, object enumSource)
    {
        if (v is string s && s.StartsWith("$c")) return ComInterop.FromHandle(s);
        if (p.IsArray)
        {
            var list = v as List<object> ?? new List<object> { v };
            switch (p.Type)
            {
                case "int": case "enum": return list.Select(x => (int)Scalar(x, "int", p, enumSource)!).ToArray();
                case "short": return list.Select(x => (short)Scalar(x, "short", p, enumSource)!).ToArray();
                case "double": return list.Select(x => (double)Scalar(x, "double", p, enumSource)!).ToArray();
                case "float": return list.Select(x => (float)Scalar(x, "float", p, enumSource)!).ToArray();
                case "string": return list.Select(x => (string)Scalar(x, "string", p, enumSource)!).ToArray();
                case "bool": return list.Select(x => (bool)Scalar(x, "bool", p, enumSource)!).ToArray();
                case "long": return list.Select(x => (long)Scalar(x, "long", p, enumSource)!).ToArray();
                default: return list.Select(Loose).ToArray();
            }
        }
        return Scalar(v, p.Type, p, enumSource);
    }

    static object? Scalar(object? v, string type, Param p, object enumSource)
    {
        if (v == null) return Default(new Param { Type = type });
        var inv = CultureInfo.InvariantCulture;
        switch (type)
        {
            case "int":
            case "enum":
                if (v is string es && !double.TryParse(es, NumberStyles.Float, inv, out _))
                {
                    // enum constant by name, e.g. "kN_m_C", "Dead", "eLoadPatternType_Dead"
                    try
                    {
                        var map = ComInterop.Constants(enumSource);
                        if (map.TryGetValue(es, out var cv)) return System.Convert.ToInt32(cv, inv);
                        var hit = map.Keys.FirstOrDefault(k => k.EndsWith("_" + es, StringComparison.OrdinalIgnoreCase) || k.Equals(p.EnumName + "_" + es, StringComparison.OrdinalIgnoreCase));
                        if (hit != null) return System.Convert.ToInt32(map[hit], inv);
                    }
                    catch { }
                    throw new ToolException($"'{es}' is not a known constant for parameter {p.Name}" + (p.EnumName != null ? $" ({p.EnumName})" : "") + ". Pass the number, or look it up with com_constants.");
                }
                return (int)Math.Round(Json.ToDouble(v));
            case "short": return (short)Math.Round(Json.ToDouble(v));
            case "byte": return (byte)Math.Round(Json.ToDouble(v));
            case "long": return (long)Math.Round(Json.ToDouble(v));
            case "double": return Json.ToDouble(v);
            case "float": return (float)Json.ToDouble(v);
            case "string": return v is string str ? str : System.Convert.ToString(v, inv) ?? "";
            case "bool":
                if (v is bool b) return b;
                var t = System.Convert.ToString(v, inv)!.Trim().ToLowerInvariant();
                return t is "true" or "1" or "yes" or "-1";
            case "date": return DateTime.Parse(System.Convert.ToString(v, inv)!, inv);
            default: return Loose(v);
        }
    }

    static object? Loose(object? v) => v switch
    {
        null => null,
        string s when s.StartsWith("$c") => ComInterop.FromHandle(s),
        long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
        List<object> list when list.All(x => x is string) => list.Cast<string>().ToArray(),
        List<object> list when list.All(x => x is long || x is double) && list.Any(x => x is double) => list.Select(x => Json.ToDouble(x)).ToArray(),
        List<object> list when list.All(x => x is long) => list.Select(x => (int)(long)x).ToArray(),
        List<object> list when list.All(x => x is bool) => list.Cast<bool>().ToArray(),
        List<object> list => list.Select(Loose).ToArray(),
        _ => v
    };

    /// <summary>COM output -> JSON-friendly value (arrays to lists, COM objects to handles).</summary>
    public static object? Out(object? v)
    {
        switch (v)
        {
            case null: return null;
            case string or bool or int or long or short or byte or float: return v;
            case double d: return double.IsNaN(d) || double.IsInfinity(d) ? null : Math.Round(d, 9);
            case DateTime dt: return dt;
            case Array arr:
                var l = new List<object?>();
                foreach (var x in arr) l.Add(Out(x));
                return l;
        }
        if (v.GetType().IsEnum) return System.Convert.ToInt32(v, CultureInfo.InvariantCulture);
        if (Marshal.IsComObject(v)) return ComInterop.Describe(v);
        return v.ToString();
    }

    /// <summary>Parallel output arrays (CSI result functions) -> list of row objects.</summary>
    public static List<object> Rows(Dictionary<string, object> outputs, int limit = 2000)
    {
        var arrays = outputs.Where(kv => kv.Value is List<object?>).ToDictionary(kv => kv.Key, kv => (List<object?>)kv.Value);
        if (arrays.Count == 0) return new List<object>();
        int n = arrays.Values.Max(a => a.Count);
        var cols = arrays.Where(kv => kv.Value.Count == n).Select(kv => kv.Key).ToList();
        var rows = new List<object>();
        for (int i = 0; i < n && i < limit; i++)
        {
            var row = new Dictionary<string, object?>();
            foreach (var c in cols) row[c] = arrays[c][i];
            rows.Add(row);
        }
        return rows;
    }
}
