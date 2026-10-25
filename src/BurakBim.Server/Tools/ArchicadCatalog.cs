using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Tools;

/// <summary>
/// One MCP tool for EVERY Archicad command, generated from the official command schemas:
///  - Graphisoft Archicad JSON API (API.*), Archicad 24-30                     -> archicad_&lt;command&gt;
///  - Tapir add-on commands (elements, MEP, design options, issues/BCF, ...)  -> archicad_&lt;command&gt; (or archicad_tapir_&lt;command&gt; on a name clash)
/// Input schemas are the publishers' own JSON schemas, so the AI sees exact parameter names, enums and required fields.
/// </summary>
public static class ArchicadCatalog
{
    public static int Official, TapirCount;

    public static void Register(ToolRegistry r)
    {
        var taken = new HashSet<string>(r.All.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
        var api = Load("archicad-api.json");
        if (api != null)
        {
            var defs = Json.Obj(api, "definitions") ?? new();
            foreach (var c in (Json.Arr(api, "commands") ?? new()).Cast<Dictionary<string, object>>())
            {
                var name = "archicad_" + Snake(Json.Str(c, "name")!);
                if (!taken.Add(name)) continue;
                var cmd = Json.Str(c, "command")!;
                var since = Json.Int(c, "since", 24);
                r.Add(Make(name, cmd, $"Archicad API: {Json.Str(c, "name")}",
                    $"{Json.Str(c, "description")} (Graphisoft JSON API {cmd}, Archicad {since}+.)",
                    Schema(Json.Obj(c, "inputScheme"), defs), a => AppTools.Ac(a, cmd, Params(a))));
                Official++;
            }
        }
        var tapir = Load("archicad-tapir.json");
        if (tapir != null)
        {
            var defs = Json.Obj(tapir, "definitions") ?? new();
            foreach (var c in (Json.Arr(tapir, "commands") ?? new()).Cast<Dictionary<string, object>>())
            {
                var cmd = Json.Str(c, "name")!;
                var name = "archicad_" + Snake(cmd);
                if (taken.Contains(name)) name = "archicad_tapir_" + Snake(cmd);
                if (!taken.Add(name)) continue;
                r.Add(Make(name, cmd, $"Archicad {Json.Str(c, "group")}: {cmd}",
                    $"{Json.Str(c, "description")} (Tapir add-on command {cmd}, since Tapir {Json.Str(c, "version")}; install the free Tapir add-on in Archicad.)",
                    Schema(Json.Obj(c, "inputScheme"), defs), a => AppTools.Tapir(a, cmd, Params(a))));
                TapirCount++;
            }
        }
    }

    static Tool Make(string name, string cmd, string title, string desc, Dictionary<string, object> schema, Func<Dictionary<string, object>, object?> h)
    {
        var verb = cmd.StartsWith("API.") ? cmd[4..] : cmd;
        bool ro = verb.StartsWith("Get") || verb.StartsWith("Is") || verb.StartsWith("Filter") || verb == "IsAlive";
        bool destructive = verb.StartsWith("Delete") || verb.StartsWith("Remove") || verb is "QuitArchicad" or "CloseProject" or "DeleteElements";
        return new Tool { Name = name, Title = title, Description = desc, Toolset = "archicad", ReadOnly = ro, Destructive = destructive, Extended = true, InputSchema = schema, Handler = (a, ctx) => h(a) };
    }

    /// <summary>Tool arguments minus our own 'port' = the command parameters (null when there are none).</summary>
    static Dictionary<string, object>? Params(Dictionary<string, object> a)
    {
        var p = a.Where(kv => kv.Key != "port").ToDictionary(kv => kv.Key, kv => kv.Value);
        return p.Count == 0 ? null : p;
    }

    static Dictionary<string, object>? Load(string file)
    {
        try
        {
            using var s = typeof(ArchicadCatalog).Assembly.GetManifestResourceStream("BurakBim.Server.Packs." + file);
            if (s == null) return null;
            using var rd = new StreamReader(s);
            return Json.ParseObject(rd.ReadToEnd());
        }
        catch (Exception ex) { Console.Error.WriteLine("[BURAK BIM] archicad catalog " + file + ": " + ex.Message); return null; }
    }

    static Dictionary<string, object> Schema(Dictionary<string, object>? input, Dictionary<string, object> defs)
    {
        Dictionary<string, object> s = Json.O("type", "object", "properties", Json.O());
        if (input != null)
            foreach (var maxDepth in new[] { 6, 3, 1 })
            {
                s = (Dictionary<string, object>)Inline(input, defs, new List<string>(), maxDepth)!;
                if (Json.Serialize(s).Length < 40000) break;   // keep tools/list small for every AI client
            }
        if (!s.ContainsKey("type")) s["type"] = "object";
        var props = Json.Obj(s, "properties") ?? new Dictionary<string, object>();
        props["port"] = Json.O("type", "integer", "description", "Archicad API port (default: first running Archicad, 19723-19744)");
        s["properties"] = props;
        s.Remove("additionalProperties");
        return s;
    }

    /// <summary>Replaces {"$ref":"#/X"} with the definition; recursive / very deep types stop at a plain object.</summary>
    static object? Inline(object? node, Dictionary<string, object> defs, List<string> path, int maxDepth)
    {
        switch (node)
        {
            case Dictionary<string, object> d:
                if (d.TryGetValue("$ref", out var r) && r is string rs)
                {
                    var key = rs.StartsWith("#/") ? rs[2..] : rs;
                    if (path.Count >= maxDepth || path.Contains(key) || !defs.TryGetValue(key, out var def))
                        return Json.O("type", "object", "description", key + " (see the Archicad / Tapir documentation)");
                    path.Add(key);
                    var copy = (Dictionary<string, object>)Inline(def, defs, path, maxDepth)!;
                    path.RemoveAt(path.Count - 1);
                    foreach (var kv in d) if (kv.Key != "$ref") copy[kv.Key] = kv.Value;
                    return copy;
                }
                var o = new Dictionary<string, object>();
                foreach (var kv in d) o[kv.Key] = Inline(kv.Value, defs, path, maxDepth)!;
                return o;
            case List<object> l:
                return l.Select(x => Inline(x, defs, path, maxDepth)).ToList();
            default:
                return node;
        }
    }

    static string Snake(string s)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (i > 0 && ((char.IsUpper(ch) && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]) && char.IsUpper(s[i - 1]))))
                          || (char.IsDigit(ch) && char.IsLetter(s[i - 1])))) sb.Append('_');
            if (char.IsUpper(ch) && i > 0 && char.IsDigit(s[i - 1])) { sb.Append(char.ToLowerInvariant(ch)); continue; }
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }
}
