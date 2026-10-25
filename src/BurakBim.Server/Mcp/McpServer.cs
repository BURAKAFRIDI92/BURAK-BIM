using System.Diagnostics;
using System.IO;
using System.Text;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Mcp;

/// <summary>
/// Model Context Protocol server over stdio (newline-delimited JSON-RPC 2.0).
/// Works with Claude Desktop, Claude Code, Codex, Cursor, Windsurf, Gemini CLI, Google Antigravity, VS Code and any MCP client.
/// </summary>
public sealed class McpServer
{
    static readonly string[] SupportedProtocols = { "2025-06-18", "2025-03-26", "2024-11-05" };
    readonly ToolRegistry _tools;
    readonly object _out = new();
    readonly TextWriter _stdout;
    string _client = "unknown client";
    readonly string _session = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];

    public McpServer(ToolRegistry tools)
    {
        _tools = tools;
        RegisterCatalogTools();
        _stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    }

    public int Run()
    {
        var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        Log($"BURAK BIM {BurakInfo.Version} MCP server ready (root: {BurakPaths.Root}, session {_session})");
        string? line;
        var pending = new List<Task>();
        while ((line = stdin.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            Dictionary<string, object> msg;
            try { msg = Json.ParseObject(line); }
            catch (Exception ex) { Send(Error(null, -32700, "Parse error: " + ex.Message)); continue; }
            // tool calls run concurrently (different apps can work in parallel); everything else inline
            if (Json.Str(msg, "method") == "tools/call")
                pending.Add(Task.Run(() => Handle(msg)));
            else
                Handle(msg);
            pending.RemoveAll(t => t.IsCompleted);
        }
        Task.WaitAll(pending.ToArray(), TimeSpan.FromSeconds(30));
        return 0;
    }

    static void Log(string s) => Console.Error.WriteLine("[BURAK BIM] " + s);

    void Send(Dictionary<string, object> msg)
    {
        var text = Json.Serialize(msg);
        lock (_out) _stdout.WriteLine(text);
    }

    static Dictionary<string, object> Result(object? id, object result) => Json.O("jsonrpc", "2.0", "id", id, "result", result);
    static Dictionary<string, object> Error(object? id, int code, string message) =>
        Json.O("jsonrpc", "2.0", "id", id, "error", Json.O("code", code, "message", message));

    void Handle(Dictionary<string, object> msg)
    {
        var id = Json.Get(msg, "id");
        var method = Json.Str(msg, "method", "");
        var p = Json.Obj(msg, "params") ?? new Dictionary<string, object>();
        bool isNotification = !msg.ContainsKey("id");
        try
        {
            object? result = method switch
            {
                "initialize" => Initialize(p),
                "ping" => Json.O(),
                "tools/list" => Json.O("tools", ListedTools().Select(t => (object)t.ToJson()).ToList()),
                "tools/call" => CallTool(p),
                "resources/list" => Json.O("resources", new List<object>()),
                "resources/templates/list" => Json.O("resourceTemplates", new List<object>()),
                "prompts/list" => Json.O("prompts", Prompts.List()),
                "prompts/get" => Prompts.Get(Json.Str(p, "name", ""), Json.Obj(p, "arguments")),
                "logging/setLevel" => Json.O(),
                _ when method.StartsWith("notifications/") => null,
                _ => throw new McpMethodNotFound(method)
            };
            if (!isNotification) Send(Result(id, result ?? Json.O()));
        }
        catch (McpMethodNotFound)
        {
            if (!isNotification) Send(Error(id, -32601, "Method not found: " + method));
        }
        catch (Exception ex)
        {
            Log($"{method} failed: {ex}");
            if (!isNotification) Send(Error(id, -32603, ex.Message));
        }
    }

    sealed class McpMethodNotFound(string m) : Exception(m);

    object Initialize(Dictionary<string, object> p)
    {
        var requested = Json.Str(p, "protocolVersion", SupportedProtocols[0]);
        var version = SupportedProtocols.Contains(requested) ? requested : SupportedProtocols[0];
        var ci = Json.Obj(p, "clientInfo");
        if (ci != null) _client = $"{Json.Str(ci, "name", "client")} {Json.Str(ci, "version", "")}".Trim();
        Log($"client connected: {_client} (protocol {version})");
        return Json.O(
            "protocolVersion", version,
            "capabilities", Json.O("tools", Json.O("listChanged", false), "prompts", Json.O("listChanged", false), "resources", Json.O(), "logging", Json.O()),
            "serverInfo", Json.O("name", "BURAK BIM", "title", "BURAK BIM - BIM & AI Connector", "version", BurakInfo.Version,
                "icons", new List<object> { Json.O("src", Core.Brand.Icon, "mimeType", "image/png", "sizes", new List<object> { "64x64" }) }),
            "instructions", Guide.ServerInstructions());
    }

    IEnumerable<Tool> VisibleTools()
    {
        var sets = Config.Toolsets.Select(s => s.ToLowerInvariant()).ToList();
        if (sets.Count == 0 || sets.Contains("all")) return _tools.All;
        var allowed = new HashSet<string>(sets.Where(s => s != "auto"));
        if (sets.Contains("auto"))
        {
            allowed.UnionWith(new[] { "core", "com", "ui", "api" });
            foreach (var f in Detection.SoftwareDetector.Detect().Select(x => x.Family).Distinct())
            {
                allowed.Add(f);
                if (f == "revit") { allowed.Add("scan"); allowed.Add("inspector"); allowed.Add("dynamo"); }
                if (f == "recap") allowed.Add("scan");
            }
        }
        return _tools.All.Where(t => allowed.Contains(t.Toolset));
    }

    // ---------------- exposure: full list, or compact list + burak_tools / burak_run ----------------
    bool? _compact;
    bool Compact
    {
        get
        {
            if (_compact.HasValue) return _compact.Value;
            var mode = (Json.Str(Config.Data, "exposure") ?? "auto").ToLowerInvariant();
            if (mode == "full") _compact = false;
            else if (mode == "compact") _compact = true;
            else
            {
                // auto: Claude Code loads MCP tools on demand (tool search), so it gets the full list; other clients get the
                // full list unless the definitions would flood the AI's context window
                var size = VisibleTools().Sum(t => Json.Serialize(t.ToJson()).Length);
                _compact = !_client.Contains("claude-code", StringComparison.OrdinalIgnoreCase)
                           && size > (int)Json.Int(Config.Data, "autoCompactAboveKB", 150) * 1024;
            }
            Log("tool exposure: " + (_compact.Value ? "compact (pack/catalog tools via burak_tools + burak_run)" : "full"));
            return _compact.Value;
        }
    }

    IEnumerable<Tool> ListedTools() => Compact ? VisibleTools().Where(t => !t.Extended) : VisibleTools();

    void RegisterCatalogTools()
    {
        _tools.Add(new Tool
        {
            Name = "burak_tools", Title = "Find BURAK BIM tools", Toolset = "core", ReadOnly = true,
            Description = "Search ALL BURAK BIM tools (Revit, Navisworks, AutoCAD, Archicad, Robot, Rhino, ... - about 1000) by keywords and/or toolset, " +
                          "and get their input schemas. Use it whenever the tool you need is not in your list, then call it with burak_run.",
            InputSchema = S.Obj(S.O("query", S.Str("Keywords, e.g. 'room finishes', 'clash report', 'create walls'")), S.O("toolset", S.Str("revit, navisworks, archicad, autocad, robot, rhino, inventor, dynamo, scan, inspector, ...")),
                S.O("limit", S.Int("Max results (default 15)")), S.O("schemas", S.Bool("Include input schemas (default true)"))),
            Handler = (a, ctx) =>
            {
                var words = (Json.Str(a, "query") ?? "").ToLowerInvariant().Split(new[] { ' ', ',', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                var set = Json.Str(a, "toolset");
                var hits = VisibleTools().Where(t => t.Name is not "burak_tools" and not "burak_run")
                    .Where(t => set == null || string.Equals(t.Toolset, set, StringComparison.OrdinalIgnoreCase))
                    .Select(t =>
                    {
                        var hay = (t.Name + " " + t.Title + " " + t.Description).ToLowerInvariant();
                        var score = words.Length == 0 ? 1 : words.Sum(w => (t.Name.Contains(w) ? 3 : 0) + (t.Title.ToLowerInvariant().Contains(w) ? 2 : 0) + (hay.Contains(w) ? 1 : 0));
                        var all = words.All(w => hay.Contains(w));
                        return (t, score: score + (all ? 5 : 0));
                    })
                    .Where(x => x.score > 0).OrderByDescending(x => x.score).ThenBy(x => x.t.Name)
                    .Take((int)Json.Int(a, "limit", 15)).ToList();
                var schemas = Json.Bool(a, "schemas", true);
                return Json.O("found", hits.Count, "tools", hits.Select(x => (object)Json.O("name", x.t.Name, "toolset", x.t.Toolset, "title", x.t.Title,
                    "description", x.t.Description, "readOnly", x.t.ReadOnly, "inputSchema", schemas ? x.t.InputSchema : null)).ToList(),
                    "next", "Call the chosen tool with burak_run {\"tool\": name, \"arguments\": {...}} (or directly by name if your client lists it).");
            }
        });
        _tools.Add(new Tool
        {
            Name = "burak_run", Title = "Run any BURAK BIM tool", Toolset = "core", ReadOnly = false,
            Description = "Run any BURAK BIM tool by name (found with burak_tools) with its arguments. Same safety as a direct call: read-only mode, audit trail, one undo step.",
            InputSchema = S.Obj(S.R("tool", S.Str("Tool name, e.g. revit_room_finishes")), S.O("arguments", S.AnyObj("Arguments for that tool"))),
            Handler = (a, ctx) => throw new ToolException("burak_run is handled by the server")
        });
    }

    object CallTool(Dictionary<string, object> p)
    {
        var name = Json.Str(p, "name", "");
        var args = Json.Obj(p, "arguments") ?? new Dictionary<string, object>();
        if (name == "burak_run")
        {
            var inner = Json.Str(args, "tool") ?? "";
            if (inner == "burak_run") return ToolError("burak_run cannot run itself.");
            if (_tools.Get(inner) is { } it && !VisibleTools().Contains(it))
                return ToolError($"Tool '{inner}' belongs to toolset '{it.Toolset}', which is switched off in config/burakbim.json \"toolsets\".");
            return CallTool(Json.O("name", inner, "arguments", Json.Obj(args, "arguments") ?? new Dictionary<string, object>()));
        }
        var tool = _tools.Get(name);
        if (tool == null)
            return ToolError($"Unknown tool '{name}'.");
        var ctx = new ToolContext { ToolName = name, Session = _session, Client = _client };
        var sw = Stopwatch.StartNew();
        try
        {
            if (!tool.ReadOnly && Config.ReadOnly)
                throw new ToolException("BURAK BIM is in READ-ONLY mode (config/burakbim.json \"readOnly\": true). Only read tools are allowed.");
            var result = tool.Handler(args, ctx);
            Audit.Write(ctx, args, true, sw.ElapsedMilliseconds, result, null, !tool.ReadOnly);
            if (result is ImageResult img)
            {
                var content = new List<object> { Json.O("type", "image", "data", img.Base64, "mimeType", img.MimeType) };
                if (img.Caption != null) content.Add(Json.O("type", "text", "text", img.Caption));
                return Json.O("content", content, "isError", false);
            }
            var text = result is string s ? s : Json.Serialize(result, true);
            return Json.O("content", new List<object> { Json.O("type", "text", "text", text) }, "isError", false);
        }
        catch (Exception ex)
        {
            var e = ex is AggregateException ae && ae.InnerException != null ? ae.InnerException : ex;
            if (e is System.Reflection.TargetInvocationException tie && tie.InnerException != null) e = tie.InnerException;
            var message = e is ToolException ? e.Message : $"{e.GetType().Name}: {e.Message}";
            if (e is not ToolException) Log($"{name} error: {e}");
            Audit.Write(ctx, args, false, sw.ElapsedMilliseconds, null, message, !tool.ReadOnly);
            return ToolError(message);
        }
    }

    static object ToolError(string message) =>
        Json.O("content", new List<object> { Json.O("type", "text", "text", message) }, "isError", true);
}
