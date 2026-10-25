using System.Reflection;
using System.Text;
using BurakBim.Server.Bridges;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Packs;

/// <summary>
/// Tool packs: hundreds of focused tools written as short C# snippets that run inside the host (Revit, Navisworks,
/// AutoCAD...) through the bridge's cached Roslyn runner. Built-in packs are embedded; companies add their own
/// tools by dropping *.pack files into &lt;install&gt;\toolpacks (no rebuild).
///
/// Format:
///   @@ tool_name
///   title: Short title
///   host: revit | navisworks | autocad
///   set: toolset id (default = host)
///   tx: true|false            (true = runs in a transaction / one undo step; default true)
///   readonly: true|false      (default = !tx)
///   desc: one or more lines of description
///   arg: name | type | description        type: string, number, integer, boolean, ids, strings, point, array, object, any; suffix ! = required
///   code:
///   C# statements ... return value;
/// </summary>
public static class PackLoader
{
    public sealed class PackTool
    {
        public string Name = "", Title = "", Host = "revit", Set = "", Desc = "", Code = "", Source = "";
        public bool Tx = true;
        public bool? ReadOnly;
        public List<(string name, string type, bool required, string desc)> Args = new();
    }

    public static List<PackTool> LoadAll(out List<string> errors)
    {
        errors = new List<string>();
        var tools = new List<PackTool>();
        var asm = Assembly.GetExecutingAssembly();
        foreach (var res in asm.GetManifestResourceNames().Where(n => n.EndsWith(".pack", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n))
        {
            using var s = asm.GetManifestResourceStream(res)!;
            using var r = new StreamReader(s, Encoding.UTF8);
            tools.AddRange(Parse(r.ReadToEnd(), res, errors));
        }
        try
        {
            var dir = Path.Combine(BurakPaths.Root, "toolpacks");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.pack").OrderBy(x => x))
                    tools.AddRange(Parse(File.ReadAllText(f), f, errors));
        }
        catch (Exception ex) { errors.Add("toolpacks folder: " + ex.Message); }
        return tools;
    }

    public static List<PackTool> Parse(string text, string source, List<string> errors)
    {
        var list = new List<PackTool>();
        PackTool? cur = null;
        bool inCode = false, inDesc = false;
        var code = new StringBuilder();
        void Close()
        {
            if (cur == null) return;
            cur.Code = code.ToString().Trim();
            if (string.IsNullOrEmpty(cur.Code)) errors.Add($"{source}: {cur.Name} has no code");
            else list.Add(cur);
            code.Clear();
        }
        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("@@"))
            {
                Close();
                cur = new PackTool { Name = line[2..].Trim(), Source = source };
                inCode = false; inDesc = false;
                continue;
            }
            if (cur == null) continue;
            if (inCode) { code.AppendLine(rawLine); continue; }
            if (line.StartsWith("#") || line.Length == 0) { inDesc = false; continue; }
            int c = line.IndexOf(':');
            var key = c > 0 ? line[..c].Trim().ToLowerInvariant() : "";
            var val = c > 0 ? line[(c + 1)..].Trim() : line.Trim();
            switch (key)
            {
                case "title": cur.Title = val; inDesc = false; break;
                case "host": cur.Host = val.ToLowerInvariant(); inDesc = false; break;
                case "set": cur.Set = val.ToLowerInvariant(); inDesc = false; break;
                case "tx": cur.Tx = val.Equals("true", StringComparison.OrdinalIgnoreCase); inDesc = false; break;
                case "readonly": cur.ReadOnly = val.Equals("true", StringComparison.OrdinalIgnoreCase); inDesc = false; break;
                case "desc": cur.Desc = val; inDesc = true; break;
                case "arg":
                    {
                        inDesc = false;
                        var parts = val.Split('|').Select(x => x.Trim()).ToArray();
                        if (parts.Length < 2) { errors.Add($"{source}: {cur.Name} bad arg '{val}'"); break; }
                        var type = parts[1];
                        bool req = type.EndsWith("!");
                        cur.Args.Add((parts[0], type.TrimEnd('!'), req, parts.Length > 2 ? string.Join(" | ", parts.Skip(2)) : parts[0]));
                        break;
                    }
                case "code": inCode = true; inDesc = false; if (val.Length > 0) code.AppendLine(val); break;
                default:
                    if (inDesc) cur.Desc += " " + line.Trim();
                    else errors.Add($"{source}: {cur.Name} unknown line '{line}'");
                    break;
            }
        }
        Close();
        return list;
    }

    static Dictionary<string, object> SchemaFor(string type, string desc) => type.ToLowerInvariant() switch
    {
        "number" => S.Num(desc),
        "integer" or "int" or "id" => S.Int(desc),
        "boolean" or "bool" => S.Bool(desc),
        "ids" => S.Arr(desc, Json.O("type", "integer")),
        "strings" => S.Arr(desc, Json.O("type", "string")),
        "point" => S.Pt(desc + " (mm)"),
        "array" => S.Arr(desc),
        "object" => S.AnyObj(desc),
        "any" => S.Any(desc),
        _ => S.Str(desc)
    };

    public static int Register(ToolRegistry r, out List<string> errors)
    {
        var tools = LoadAll(out errors);
        int n = 0;
        foreach (var t in tools)
        {
            if (r.Get(t.Name) != null) { errors.Add($"{t.Source}: duplicate tool name {t.Name} (skipped)"); continue; }
            var props = t.Args.Select(a => (a.name, SchemaFor(a.type, a.desc), a.required)).ToList();
            props.Add(S.Target);
            var tool = t;
            var host = tool.Host;
            r.Add(new Tool
            {
                Extended = true,
                Name = tool.Name,
                Title = string.IsNullOrEmpty(tool.Title) ? tool.Name : tool.Title,
                Description = tool.Desc,
                Toolset = string.IsNullOrEmpty(tool.Set) ? host : tool.Set,
                InputSchema = S.Obj(props.ToArray()),
                ReadOnly = tool.ReadOnly ?? !tool.Tx,
                Destructive = tool.Name.Contains("delete") || tool.Name.Contains("purge"),
                Handler = (a, ctx) =>
                {
                    var args = new Dictionary<string, object>(a);
                    args.Remove("target");
                    var res = BridgeClient.Call(host, Json.Str(a, "target"), "script.run",
                        Json.O("code", tool.Code, "args", args, "transaction", tool.Tx, "label", "BURAK BIM: " + (string.IsNullOrEmpty(tool.Title) ? tool.Name : tool.Title)),
                        ctx);
                    // unwrap {result, output}; keep output lines when the snippet printed something
                    if (res is Dictionary<string, object> d && d.ContainsKey("result"))
                    {
                        var output = Json.Arr(d, "output");
                        return output is { Count: > 0 } ? Json.O("result", d["result"], "output", output) : d["result"];
                    }
                    return res;
                }
            });
            n++;
        }
        return n;
    }
}
