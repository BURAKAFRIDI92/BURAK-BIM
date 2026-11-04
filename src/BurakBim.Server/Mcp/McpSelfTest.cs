using BurakBim.Shared;

namespace BurakBim.Server.Mcp;

/// <summary>
/// 1.3: BurakBim.Server.exe --mcp-selftest - simulates every AI client profile (initialize + tools/list + calls)
/// and validates what each one would receive: tool count limit, valid unique names, portable input schemas.
/// Runs in CI before every release so a schema/tool change can never break a client again.
/// </summary>
public static class McpSelfTest
{
    static readonly string[] Clients =
    {
        "claude-code", "claude-ai", "Visual Studio Code", "cursor-vscode", "codex-mcp-client", "antigravity", "gemini-cli-mcp-client",
        "windsurf-client", "Cline", "Roo Code", "Zed", "lmstudio", "kiro", "some-unknown-client"
    };

    static readonly HashSet<string> StrictForbidden = new() { "additionalProperties", "$schema", "$ref", "$defs", "definitions", "oneOf", "anyOf", "allOf", "format", "default" };

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        foreach (var client in Clients)
        {
            var server = new McpServer(Program.BuildRegistry());
            var init = server.Dispatch(Json.O("jsonrpc", "2.0", "id", 1, "method", "initialize", "params",
                Json.O("protocolVersion", "2025-06-18", "clientInfo", Json.O("name", client, "version", "1.0"), "capabilities", Json.O())));
            Check(init != null && Json.Obj(init, "result") != null, $"[{client}] initialize");
            var list = server.Dispatch(Json.O("jsonrpc", "2.0", "id", 2, "method", "tools/list"));
            var tools = Json.Arr(Json.Obj(list!, "result"), "tools")?.OfType<Dictionary<string, object>>().ToList() ?? new();
            var profile = server.Profile;
            Check(tools.Count > 0, $"[{client}] tools/list returned {tools.Count} tools (profile {profile.Name}, max {(profile.MaxTools == 0 ? "all" : profile.MaxTools.ToString())})");
            if (profile.MaxTools > 0) Check(tools.Count <= profile.MaxTools, $"[{client}] tool count within the client's limit");
            var names = tools.Select(t => Json.Str(t, "name") ?? "").ToList();
            Check(names.Distinct().Count() == names.Count, $"[{client}] tool names unique");
            Check(names.All(n => System.Text.RegularExpressions.Regex.IsMatch(n, "^[a-zA-Z0-9_-]{1,64}$")), $"[{client}] tool names valid");
            foreach (var essential in new[] { "burak_status", "burak_tools", "burak_run", "capture", "computer_request_control" })
                if (!names.Contains(essential)) Check(false, $"[{client}] essential tool {essential} listed");
            int bad = 0; string? firstBad = null;
            foreach (var t in tools)
            {
                var schema = Json.Obj(t, "inputSchema");
                var problem = Validate(schema, profile.StrictSchema, "$");
                if (problem != null) { bad++; firstBad ??= Json.Str(t, "name") + ": " + problem; }
                try { Json.Parse(Json.Serialize(t)); } catch (Exception ex) { bad++; firstBad ??= Json.Str(t, "name") + ": not serializable " + ex.Message; }
            }
            Check(bad == 0, $"[{client}] every input schema portable" + (firstBad != null ? $" (first problem: {firstBad})" : ""));
        }

        // calls through the protocol
        var s = new McpServer(Program.BuildRegistry());
        s.Dispatch(Json.O("jsonrpc", "2.0", "id", 1, "method", "initialize", "params", Json.O("protocolVersion", "2025-11-25", "clientInfo", Json.O("name", "selftest"))));
        var search = s.Dispatch(Json.O("jsonrpc", "2.0", "id", 3, "method", "tools/call", "params", Json.O("name", "burak_tools", "arguments", Json.O("query", "frame forces"))));
        Check(Json.Bool(Json.Obj(search!, "result"), "isError") == false, "burak_tools search works");
        var run = s.Dispatch(Json.O("jsonrpc", "2.0", "id", 4, "method", "tools/call", "params", Json.O("name", "burak_run", "arguments", Json.O("tool", "burak_guide", "arguments", Json.O()))));
        Check(Json.Bool(Json.Obj(run!, "result"), "isError") == false, "burak_run -> burak_guide works");
        var unknown = s.Dispatch(Json.O("jsonrpc", "2.0", "id", 5, "method", "tools/call", "params", Json.O("name", "revit_wals", "arguments", Json.O())));
        Check(Json.Bool(Json.Obj(unknown!, "result"), "isError"), "unknown tool -> helpful error");
        var prompts = s.Dispatch(Json.O("jsonrpc", "2.0", "id", 7, "method", "prompts/list"));
        var pl = Json.Arr(Json.Obj(prompts!, "result"), "prompts") ?? new List<object>();
        Check(pl.OfType<Dictionary<string, object>>().Any(x => Json.Str(x, "name") == "skill-bim-base"), $"skills as MCP prompts ({pl.Count} prompts)");
        var res = s.Dispatch(Json.O("jsonrpc", "2.0", "id", 8, "method", "resources/read", "params", Json.O("uri", "burak://skills/revit-model-health")));
        Check(Json.Obj(res!, "result") != null, "skills as MCP resources");
        Check(Skills.SkillLibrary.All().Count >= 27, $"{Skills.SkillLibrary.All().Count} skills embedded");
        var registry = Program.BuildRegistry();
        var unknownTools = Skills.SkillLibrary.All().Where(x => !x.Custom)
            .SelectMany(x => System.Text.RegularExpressions.Regex.Matches(x.Markdown, @"`((?:revit|navis|navisworks|autocad|civil|csi|bluebeam|planswift|pdf|archicad|rhino|robot|inventor|scan|inspector|burak|capture|computer|ribbon|api|com|ui|dynamo|script|grasshopper|recap)_[a-z0-9_]+)")
                .Select(m => (skill: x.Name, tool: m.Groups[1].Value)))
            .Where(r => registry.Get(r.tool) == null && r.tool is not ("burak_tools" or "burak_run")).Select(r => r.skill + ":" + r.tool).Distinct().ToList();
        Check(unknownTools.Count == 0, "every tool named in a skill exists" + (unknownTools.Count > 0 ? " - missing " + string.Join(", ", unknownTools) : ""));
        var ping = s.Dispatch(Json.O("jsonrpc", "2.0", "id", 6, "method", "ping"));
        Check(ping != null, "ping");
        var notif = s.Dispatch(Json.O("jsonrpc", "2.0", "method", "notifications/initialized"));
        Check(notif == null, "notifications get no reply");

        Console.WriteLine(fails == 0 ? "ALL MCP TESTS PASSED" : $"{fails} MCP TEST(S) FAILED");
        return fails == 0 ? 0 : 1;
    }

    static string? Validate(Dictionary<string, object>? s, bool strict, string path)
    {
        if (s == null) return path + " missing";
        if (path == "$" && Json.Str(s, "type") != "object") return "root type is not object";
        foreach (var kv in s)
        {
            if (strict && StrictForbidden.Contains(kv.Key)) return $"{path}.{kv.Key} not allowed for this client";
            if (kv.Key is "$ref" or "oneOf" or "anyOf" or "allOf") return $"{path}.{kv.Key} not portable";
        }
        if (Json.Get(s, "type") is List<object>) return path + ".type is an array";
        var type = Json.Str(s, "type");
        if (type == "array" && Json.Get(s, "items") == null) return path + " array without items";
        if (Json.Obj(s, "properties") is { } props)
        {
            foreach (var kv in props)
            {
                var r = Validate(kv.Value as Dictionary<string, object>, strict, path + "." + kv.Key);
                if (r != null) return r;
            }
            if (Json.Arr(s, "required") is { } req && req.Any(x => x is not string n || !props.ContainsKey(n))) return path + ".required lists unknown properties";
        }
        if (Json.Obj(s, "items") is { } items) return Validate(items, strict, path + "[]") is { } ir && !ir.EndsWith("missing") ? ir : null;
        return null;
    }
}
