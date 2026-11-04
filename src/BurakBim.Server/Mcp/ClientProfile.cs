using System.Text.RegularExpressions;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Mcp;

/// <summary>
/// 1.3: per-AI-client compatibility profile. Every MCP client has its own limits, and BURAK BIM ships ~1100 tools:
///  - VS Code / GitHub Copilot and OpenAI-based clients (Codex, ChatGPT) reject requests with more than 128 tools in total
///    (all servers together), Cursor and Windsurf cap active tools, Antigravity / Gemini cap tools and reject some
///    JSON-schema keywords. Exceeding a limit made BURAK BIM look "not connected" in those apps.
///  - Claude Code loads MCP tools on demand, so it gets everything.
/// The profile decides how many tools are listed and how strict the input schemas must be. Every tool stays reachable
/// through burak_tools + burak_run, so nothing is lost.
/// </summary>
public sealed class ClientProfile
{
    public string Name = "generic";
    /// <summary>Max tools in tools/list (0 = unlimited).</summary>
    public int MaxTools = 60;
    /// <summary>Strip JSON-schema keywords that Gemini-family / older clients reject.</summary>
    public bool StrictSchema;
    /// <summary>Max tool-name length the client accepts for this server (prefix included by the client).</summary>
    public int MaxNameLength = 64;

    static readonly (string pattern, ClientProfile profile)[] Known =
    {
        ("claude-code",            new ClientProfile { Name = "Claude Code", MaxTools = 0, MaxNameLength = 48 }),
        ("claude-ai|claude desktop|^claude$", new ClientProfile { Name = "Claude Desktop", MaxTools = 160, MaxNameLength = 64 }),
        ("visual studio code|vscode|copilot", new ClientProfile { Name = "VS Code (Copilot)", MaxTools = 90 }),
        ("visual studio",          new ClientProfile { Name = "Visual Studio", MaxTools = 90 }),
        ("cursor",                 new ClientProfile { Name = "Cursor", MaxTools = 40, MaxNameLength = 50 }),
        ("windsurf|codeium",       new ClientProfile { Name = "Windsurf", MaxTools = 60 }),
        ("codex|openai|chatgpt",   new ClientProfile { Name = "OpenAI Codex / ChatGPT", MaxTools = 90, StrictSchema = true }),
        ("antigravity",            new ClientProfile { Name = "Google Antigravity", MaxTools = 60, StrictSchema = true }),
        ("gemini",                 new ClientProfile { Name = "Gemini CLI", MaxTools = 90, StrictSchema = true }),
        ("cline|roo|kilo",         new ClientProfile { Name = "Cline / Roo / Kilo", MaxTools = 80 }),
        ("zed",                    new ClientProfile { Name = "Zed", MaxTools = 80 }),
        ("lm ?studio|lmstudio",    new ClientProfile { Name = "LM Studio", MaxTools = 40, StrictSchema = true }),
        ("continue",               new ClientProfile { Name = "Continue", MaxTools = 60 }),
        ("kiro|amazon q|q-dev",    new ClientProfile { Name = "Kiro / Amazon Q", MaxTools = 60, StrictSchema = true }),
        ("trae",                   new ClientProfile { Name = "Trae", MaxTools = 40 }),
        ("jetbrains|intellij|rider", new ClientProfile { Name = "JetBrains AI", MaxTools = 80 }),
        ("goose",                  new ClientProfile { Name = "Goose", MaxTools = 80 }),
        ("ollama|open-webui|librechat|anythingllm", new ClientProfile { Name = "Local LLM host", MaxTools = 30, StrictSchema = true }),
    };

    public static ClientProfile For(string clientName)
    {
        var n = (clientName ?? "").ToLowerInvariant();
        ClientProfile p = new() { Name = string.IsNullOrWhiteSpace(clientName) ? "unknown client" : clientName };
        foreach (var (pattern, profile) in Known)
            if (Regex.IsMatch(n, pattern)) { p = new ClientProfile { Name = profile.Name, MaxTools = profile.MaxTools, StrictSchema = profile.StrictSchema, MaxNameLength = profile.MaxNameLength }; break; }

        // explicit overrides in config\burakbim.json
        var d = Config.Data;
        var mode = (Json.Str(d, "exposure") ?? "auto").ToLowerInvariant();
        if (mode == "full") p.MaxTools = 0;
        else if (mode == "compact") p.MaxTools = Math.Max(20, p.MaxTools == 0 ? 60 : p.MaxTools);
        var max = Json.Int(d, "maxTools", -1);
        if (max >= 0) p.MaxTools = (int)max;
        if (Json.Get(d, "strictSchemas") is bool b) p.StrictSchema = b;
        // environment override (per client config): BURAK_BIM_MAX_TOOLS
        if (int.TryParse(Environment.GetEnvironmentVariable("BURAK_BIM_MAX_TOOLS"), out var envMax) && envMax >= 0) p.MaxTools = envMax;
        return p;
    }
}

/// <summary>
/// Makes tool input schemas portable across MCP clients: removes $ref/$defs (inlined), oneOf/anyOf/allOf,
/// type arrays, nulls, and (strict mode) keywords that Gemini/OpenAI-strict validators reject.
/// </summary>
public static class SchemaSanitizer
{
    static readonly HashSet<string> StrictDrop = new(StringComparer.Ordinal)
        { "additionalProperties", "$schema", "$id", "format", "default", "examples", "const", "pattern", "patternProperties", "minLength", "maxLength", "title", "readOnly", "writeOnly", "deprecated" };

    public static Dictionary<string, object> Clean(Dictionary<string, object> schema, bool strict)
    {
        var defs = new Dictionary<string, object>();
        if (Json.Obj(schema, "$defs") is { } d1) foreach (var kv in d1) defs["#/$defs/" + kv.Key] = kv.Value;
        if (Json.Obj(schema, "definitions") is { } d2) foreach (var kv in d2) defs["#/definitions/" + kv.Key] = kv.Value;
        var outp = Node(schema, defs, strict, 0) as Dictionary<string, object> ?? Json.O();
        if (Json.Str(outp, "type") != "object") outp = Json.O("type", "object", "properties", Json.O());
        if (Json.Get(outp, "properties") == null) outp["properties"] = Json.O();
        return outp;
    }

    static object Node(object? v, Dictionary<string, object> defs, bool strict, int depth)
    {
        if (v is not Dictionary<string, object> s) return Json.O();
        if (depth > 12) return Json.O("type", "object");
        // $ref -> inline
        if (Json.Str(s, "$ref") is { } r)
        {
            if (defs.TryGetValue(r, out var target) && depth < 10) return Node(target, defs, strict, depth + 1);
            return Json.O("type", "object", "description", Json.Str(s, "description") ?? "");
        }
        // oneOf / anyOf / allOf -> first concrete alternative, merged with the parent's description
        foreach (var key in new[] { "anyOf", "oneOf" })
        {
            if (Json.Get(s, key) is List<object> alts && alts.Count > 0)
            {
                var pick = alts.OfType<Dictionary<string, object>>().FirstOrDefault(x => Json.Str(x, "type") != "null") ?? alts[0] as Dictionary<string, object>;
                var merged = new Dictionary<string, object>(pick ?? Json.O());
                foreach (var kv in s) if (kv.Key != key && !merged.ContainsKey(kv.Key)) merged[kv.Key] = kv.Value;
                return Node(merged, defs, strict, depth + 1);
            }
        }
        if (Json.Get(s, "allOf") is List<object> all && all.Count > 0)
        {
            var merged = new Dictionary<string, object>();
            foreach (var part in all.OfType<Dictionary<string, object>>())
            {
                var n = Node(part, defs, strict, depth + 1) as Dictionary<string, object> ?? Json.O();
                foreach (var kv in n)
                {
                    if (kv.Key == "properties" && merged.TryGetValue("properties", out var ep) && ep is Dictionary<string, object> epd && kv.Value is Dictionary<string, object> np)
                        foreach (var pkv in np) epd[pkv.Key] = pkv.Value;
                    else if (kv.Key == "required" && merged.TryGetValue("required", out var er) && er is List<object> erl && kv.Value is List<object> nrl)
                        erl.AddRange(nrl.Where(x => !erl.Contains(x)));
                    else merged[kv.Key] = kv.Value;
                }
            }
            foreach (var kv in s) if (kv.Key != "allOf" && !merged.ContainsKey(kv.Key)) merged[kv.Key] = kv.Value;
            return Node(merged, defs, strict, depth + 1);
        }

        var o = new Dictionary<string, object>();
        // type: ["string","null"] -> "string"
        var type = Json.Get(s, "type");
        if (type is List<object> tl) type = tl.Select(x => x?.ToString()).FirstOrDefault(x => x != null && x != "null") ?? "string";
        var t = type as string;
        if (t == "null") t = "string";
        if (t == null)
        {
            if (Json.Get(s, "properties") != null) t = "object";
            else if (Json.Get(s, "items") != null) t = "array";
            else if (Json.Get(s, "enum") is List<object>) t = "string";
        }
        if (t != null) o["type"] = t;

        foreach (var kv in s)
        {
            switch (kv.Key)
            {
                case "type": case "$ref": case "$defs": case "definitions": case "anyOf": case "oneOf": case "allOf": case "not":
                case "if": case "then": case "else": case "dependentRequired": case "dependentSchemas": case "unevaluatedProperties":
                    continue;
                case "properties":
                    if (kv.Value is Dictionary<string, object> props)
                    {
                        var np = new Dictionary<string, object>();
                        foreach (var pkv in props) np[pkv.Key] = Node(pkv.Value, defs, strict, depth + 1);
                        o["properties"] = np;
                    }
                    continue;
                case "items":
                    o["items"] = kv.Value is List<object> tuple ? Node(tuple.FirstOrDefault(), defs, strict, depth + 1) : Node(kv.Value, defs, strict, depth + 1);
                    continue;
                case "required":
                    if (kv.Value is List<object> req)
                    {
                        var names = (Json.Get(s, "properties") as Dictionary<string, object>)?.Keys.ToHashSet() ?? new HashSet<string>();
                        var keep = req.Where(x => x is string n && names.Contains(n)).Distinct().ToList();
                        if (keep.Count > 0) o["required"] = keep;
                    }
                    continue;
                case "enum":
                    if (kv.Value is List<object> ev)
                    {
                        var vals = ev.Where(x => x != null).ToList();
                        if (vals.Count > 0 && vals.All(x => x is long || x is int))
                        { o["enum"] = vals.Distinct().ToList(); o["type"] = "integer"; }
                        else if (vals.Count > 0 && vals.All(x => x is long || x is int || x is double))
                        { o["enum"] = vals.Distinct().ToList(); o["type"] = "number"; }
                        else
                        {
                            var clean = vals.Select(x => (object)(x is string ? x : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)!)).Distinct().ToList();
                            if (clean.Count > 0) { o["enum"] = clean; o["type"] = "string"; }
                        }
                    }
                    continue;
                case "additionalProperties":
                    if (strict) continue;
                    o[kv.Key] = kv.Value is Dictionary<string, object> ap ? Node(ap, defs, strict, depth + 1) : kv.Value;
                    continue;
                case "description":
                    if (kv.Value is string ds) o["description"] = ds.Length > 1000 ? ds[..1000] : ds;
                    continue;
            }
            if (strict && StrictDrop.Contains(kv.Key)) continue;
            if (kv.Key.StartsWith("$") || kv.Key.StartsWith("x-")) continue;
            if (kv.Value is Dictionary<string, object> || kv.Value is List<object>) { if (!strict) o[kv.Key] = kv.Value; continue; }
            o[kv.Key] = kv.Value;
        }
        if (Json.Str(o, "type") == "array" && Json.Get(o, "items") == null) o["items"] = strict ? Json.O("type", "string") : Json.O();
        if (Json.Str(o, "type") == "object" && strict && Json.Get(o, "properties") == null) o["properties"] = Json.O();
        return o;
    }

    /// <summary>Valid tool name for every client: ^[a-zA-Z0-9_-]{1,64}$.</summary>
    public static bool ValidName(string name, int maxLen) => name.Length <= maxLen && Regex.IsMatch(name, "^[a-zA-Z0-9_-]+$");
}
