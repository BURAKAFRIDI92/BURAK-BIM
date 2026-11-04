using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Mcp;

/// <summary>
/// Model Context Protocol server (JSON-RPC 2.0) over stdio (newline-delimited) or local Streamable HTTP.
/// Works with Claude Desktop, Claude Code, Codex / ChatGPT, Cursor, Windsurf, Gemini CLI, Google Antigravity, VS Code,
/// Visual Studio, Cline, Roo, Zed, LM Studio, Continue, Kiro and any MCP client.
///
/// 1.3 robustness:
///  - stdout is reserved for protocol messages; any stray Console.Write goes to stderr (a single stray line used to
///    break the connection in some clients).
///  - per-client profile (tool count limits, schema strictness, name length) - see ClientProfile.
///  - protocol 2025-11-25 / 2025-06-18 / 2025-03-26 / 2024-11-05.
///  - server-to-client requests (elicitation) for clients that support them.
///  - Capture mode: changes in Revit / AutoCAD / Navisworks / any app come back with a picture of the result.
/// </summary>
public sealed class McpServer
{
    static readonly string[] SupportedProtocols = { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" };
    readonly ToolRegistry _tools;
    readonly object _out = new();
    TextWriter? _stdout;
    string _client = "unknown client";
    ClientProfile _profile = ClientProfile.For("");
    Dictionary<string, object> _clientCaps = new();
    readonly string _session = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
    readonly ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, object>>> _pending = new();
    long _nextOutId;

    public static McpServer? Current { get; private set; }
    public string ClientName => _client;
    public ClientProfile Profile => _profile;
    public bool ClientSupportsElicitation => _clientCaps.ContainsKey("elicitation");

    public McpServer(ToolRegistry tools)
    {
        _tools = tools;
        Current = this;
        RegisterCatalogTools();
    }

    // ======================================================================================== stdio transport
    public int Run()
    {
        // stdout belongs to the protocol. Everything else that writes to Console.Out is redirected to stderr.
        var raw = Console.OpenStandardOutput();
        _stdout = new StreamWriter(raw, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        Console.SetOut(Console.Error);

        var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        Log($"BURAK BIM {BurakInfo.Version} MCP server ready (root: {BurakPaths.Root}, session {_session}, pid {Environment.ProcessId})");
        // warm the software detection cache in the background so the first tools/list is instant
        Task.Run(() => { try { Detection.SoftwareDetector.Detect(); } catch { } });
        string? line;
        var pending = new List<Task>();
        while ((line = stdin.ReadLine()) != null)
        {
            line = line.Trim().TrimStart('﻿');
            if (line.Length == 0) continue;
            object parsed;
            try { parsed = Json.Parse(line); }
            catch (Exception ex) { Send(Error(null, -32700, "Parse error: " + ex.Message)); continue; }

            // JSON-RPC batch (2025-03-26)
            if (parsed is List<object> batch)
            {
                var replies = new List<object>();
                foreach (var m in batch.OfType<Dictionary<string, object>>())
                {
                    var r = Dispatch(m);
                    if (r != null) replies.Add(r);
                }
                if (replies.Count > 0) lock (_out) _stdout.WriteLine(Json.Serialize(replies));
                continue;
            }
            if (parsed is not Dictionary<string, object> msg) { Send(Error(null, -32600, "Invalid request")); continue; }

            // a response to one of OUR requests (elicitation)
            if (!msg.ContainsKey("method") && msg.ContainsKey("id") && (msg.ContainsKey("result") || msg.ContainsKey("error")))
            {
                var key = Convert.ToString(Json.Get(msg, "id"), System.Globalization.CultureInfo.InvariantCulture) ?? "";
                if (_pending.TryRemove(key, out var tcs)) tcs.TrySetResult(msg);
                continue;
            }
            // tool calls run concurrently (different apps can work in parallel, and a tool may wait for the user)
            if (Json.Str(msg, "method") == "tools/call")
                pending.Add(Task.Run(() => { var r = Dispatch(msg); if (r != null) Send(r); }));
            else
            {
                var r = Dispatch(msg);
                if (r != null) Send(r);
            }
            pending.RemoveAll(t => t.IsCompleted);
        }
        Task.WaitAll(pending.ToArray(), TimeSpan.FromSeconds(30));
        return 0;
    }

    static void Log(string s) { try { Console.Error.WriteLine("[BURAK BIM] " + s); } catch { } }

    void Send(Dictionary<string, object> msg)
    {
        if (_stdout == null) return; // HTTP mode: responses are returned to the HTTP handler
        var text = Json.Serialize(msg);
        lock (_out) _stdout.WriteLine(text);
    }

    /// <summary>Send a request to the client and wait for its answer (elicitation/create etc.). stdio only.</summary>
    public Dictionary<string, object>? RequestClient(string method, Dictionary<string, object> parameters, TimeSpan timeout)
    {
        if (_stdout == null) return null;
        var id = "burak-" + Interlocked.Increment(ref _nextOutId);
        var tcs = new TaskCompletionSource<Dictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        Send(Json.O("jsonrpc", "2.0", "id", id, "method", method, "params", parameters));
        if (!tcs.Task.Wait(timeout)) { _pending.TryRemove(id, out _); return null; }
        return tcs.Task.Result;
    }

    public void Notify(string method, Dictionary<string, object>? parameters = null)
    {
        var m = Json.O("jsonrpc", "2.0", "method", method);
        if (parameters != null) m["params"] = parameters;
        Send(m);
    }

    static Dictionary<string, object> Result(object? id, object result) => Json.O("jsonrpc", "2.0", "id", id, "result", result);
    static Dictionary<string, object> Error(object? id, int code, string message) =>
        Json.O("jsonrpc", "2.0", "id", id, "error", Json.O("code", code, "message", message));

    /// <summary>Handle one JSON-RPC message; returns the response, or null for notifications. Used by stdio and HTTP.</summary>
    public Dictionary<string, object>? Dispatch(Dictionary<string, object> msg)
    {
        var id = Json.Get(msg, "id");
        var method = Json.Str(msg, "method", "") ?? "";
        var p = Json.Obj(msg, "params") ?? new Dictionary<string, object>();
        bool isNotification = !msg.ContainsKey("id");
        try
        {
            object? result = method switch
            {
                "initialize" => Initialize(p),
                "ping" => Json.O(),
                "tools/list" => Json.O("tools", ListedToolsJson()),
                "tools/call" => CallTool(p),
                "resources/list" => Json.O("resources", Skills.SkillLibrary.Resources()),
                "resources/read" => Skills.SkillLibrary.Resource(Json.Str(p, "uri") ?? "") ?? throw new Exception("Unknown resource " + Json.Str(p, "uri")),
                "resources/templates/list" => Json.O("resourceTemplates", new List<object>()),
                "prompts/list" => Json.O("prompts", Prompts.List().Concat(Skills.SkillLibrary.Prompts()).ToList()),
                "prompts/get" => Skills.SkillLibrary.Prompt(Json.Str(p, "name", "") ?? "", Json.Obj(p, "arguments")) ?? Prompts.Get(Json.Str(p, "name", "") ?? "", Json.Obj(p, "arguments")),
                "logging/setLevel" => Json.O(),
                "completion/complete" => Json.O("completion", Json.O("values", new List<object>(), "hasMore", false)),
                _ when method.StartsWith("notifications/") => null,
                _ => throw new McpMethodNotFound(method)
            };
            if (isNotification) return null;
            return Result(id, result ?? Json.O());
        }
        catch (McpMethodNotFound)
        {
            return isNotification ? null : Error(id, -32601, "Method not found: " + method);
        }
        catch (Exception ex)
        {
            Log($"{method} failed: {ex}");
            return isNotification ? null : Error(id, -32603, ex.Message);
        }
    }

    sealed class McpMethodNotFound(string m) : Exception(m);

    object Initialize(Dictionary<string, object> p)
    {
        var requested = Json.Str(p, "protocolVersion", SupportedProtocols[1]) ?? SupportedProtocols[1];
        var version = SupportedProtocols.Contains(requested) ? requested : SupportedProtocols[1];
        var ci = Json.Obj(p, "clientInfo");
        if (ci != null) _client = $"{Json.Str(ci, "name", "client")} {Json.Str(ci, "version", "")}".Trim();
        _clientCaps = Json.Obj(p, "capabilities") ?? new Dictionary<string, object>();
        _profile = ClientProfile.For(_client);
        _listed = null;
        Log($"client connected: {_client} (protocol {version}, profile {_profile.Name}, maxTools {(_profile.MaxTools == 0 ? "all" : _profile.MaxTools.ToString())})");
        return Json.O(
            "protocolVersion", version,
            "capabilities", Json.O("tools", Json.O("listChanged", false), "prompts", Json.O("listChanged", false), "resources", Json.O("listChanged", false), "logging", Json.O()),
            "serverInfo", Json.O("name", "BURAK BIM", "title", "BURAK BIM - BIM & AI Connector", "version", BurakInfo.Version,
                "websiteUrl", "https://github.com/BURAKAFRIDI92/BURAK-BIM",
                "icons", new List<object> { Json.O("src", Core.Brand.Icon, "mimeType", "image/png", "sizes", new List<object> { "64x64" }) }),
            "instructions", Guide.ServerInstructions());
    }

    // ======================================================================================== tool listing
    public IEnumerable<Tool> VisibleTools()
    {
        var sets = Config.Toolsets.Select(s => s.ToLowerInvariant()).ToList();
        if (sets.Count == 0 || sets.Contains("all")) return _tools.All;
        var allowed = new HashSet<string>(sets.Where(s => s != "auto"));
        if (sets.Contains("auto"))
        {
            allowed.UnionWith(new[] { "core", "com", "ui", "api", "capture", "computer" });
            foreach (var f in Detection.SoftwareDetector.Detect().Select(x => x.Family).Distinct())
            {
                allowed.Add(f);
                if (f == "revit") { allowed.Add("scan"); allowed.Add("inspector"); allowed.Add("dynamo"); }
                if (f == "recap") allowed.Add("scan");
                if (f is "sap2000" or "etabs" or "csibridge" or "safe") allowed.Add("csi");
                if (f == "bluebeam") allowed.Add("pdf");
            }
        }
        return _tools.All.Where(t => allowed.Contains(t.Toolset));
    }

    static readonly string[] Essential =
    {
        "burak_status", "burak_skills", "burak_tools", "burak_run", "burak_guide", "burak_launch", "burak_connect_clients",
        "capture", "capture_mode", "revit_capture", "computer_request_control", "computer_screenshot", "computer_click",
        "computer_type", "computer_key", "computer_scroll", "computer_drag", "computer_release_control"
    };

    // toolset order when a client can only take a limited number of tools: running apps first, then installed ones
    static readonly string[] ToolsetOrder = { "core", "capture", "computer", "revit", "csi", "autocad", "navisworks", "api", "ui", "archicad", "bluebeam", "planswift", "pdf", "inspector", "scan", "robot", "inventor", "rhino", "dynamo", "com", "recap" };

    List<Tool>? _listed;

    public List<Tool> ListedTools()
    {
        if (_listed != null) return _listed;
        var visible = VisibleTools().Where(t => SchemaSanitizer.ValidName(t.Name, _profile.MaxNameLength)).ToList();
        if (_profile.MaxTools <= 0 || visible.Count <= _profile.MaxTools) return _listed = visible;

        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var d in Bridges.BridgeClient.Discover()) running.Add(d.Host);
            foreach (var f in Detection.SoftwareDetector.RunningFamilies()) running.Add(f);
        }
        catch { }
        int Rank(Tool t)
        {
            var ei = Array.IndexOf(Essential, t.Name);
            if (ei >= 0) return 10000 - ei;
            int r = t.Priority > 0 ? t.Priority * 10 : 0;
            if (!t.Extended) r += 500;                                   // hand-written tools before pack/catalog tools
            if (running.Contains(t.Toolset) || (t.Toolset == "csi" && running.Overlaps(new[] { "sap2000", "etabs", "csibridge", "safe" }))) r += 2000;
            var i = Array.IndexOf(ToolsetOrder, t.Toolset);
            r += i < 0 ? 0 : (ToolsetOrder.Length - i) * 10;
            if (t.ReadOnly) r += 3;                                      // read tools first inside a set (status/info/list)
            return r;
        }
        _listed = visible.OrderByDescending(Rank).ThenBy(t => t.Name).Take(_profile.MaxTools).ToList();
        Log($"listing {_listed.Count} of {visible.Count} tools for {_profile.Name}; the rest via burak_tools + burak_run");
        return _listed;
    }

    List<object> ListedToolsJson()
    {
        var list = new List<object>();
        foreach (var t in ListedTools())
        {
            var j = t.ToJson();
            try { j["inputSchema"] = SchemaSanitizer.Clean(t.InputSchema, _profile.StrictSchema); }
            catch (Exception ex) { Log($"schema of {t.Name} could not be cleaned: {ex.Message}"); j["inputSchema"] = Json.O("type", "object", "properties", Json.O()); }
            if (j.TryGetValue("description", out var d) && d is string ds && ds.Length > 1024 && _profile.StrictSchema) j["description"] = ds[..1024];
            list.Add(j);
        }
        return list;
    }

    void RegisterCatalogTools()
    {
        _tools.Add(new Tool
        {
            Name = "burak_tools", Title = "Find BURAK BIM tools", Toolset = "core", ReadOnly = true,
            Description = "Search ALL BURAK BIM tools (Revit, AutoCAD, Civil 3D, Navisworks, SAP2000, ETABS, CSiBridge, SAFE, Archicad, Bluebeam, PlanSwift, Robot, Rhino ... about 1100) by keywords and/or toolset, " +
                          "and get their input schemas. Use it whenever the tool you need is not in your list, then call it with burak_run.",
            InputSchema = S.Obj(S.O("query", S.Str("Keywords, e.g. 'room finishes', 'clash report', 'frame forces', 'markups'")), S.O("toolset", S.Str("revit, autocad, navisworks, csi, archicad, bluebeam, planswift, robot, rhino, inventor, dynamo, scan, inspector, capture, computer ...")),
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
                var listed = new HashSet<string>(ListedTools().Select(t => t.Name));
                return Json.O("found", hits.Count, "tools", hits.Select(x => (object)Json.O("name", x.t.Name, "toolset", x.t.Toolset, "title", x.t.Title,
                    "description", x.t.Description, "readOnly", x.t.ReadOnly, "inYourToolList", listed.Contains(x.t.Name),
                    "inputSchema", schemas ? SchemaSanitizer.Clean(x.t.InputSchema, false) : null)).ToList(),
                    "next", "Call the chosen tool with burak_run {\"tool\": name, \"arguments\": {...}} (or directly by name if it is in your tool list).");
            }
        });
        _tools.Add(new Tool
        {
            Name = "burak_run", Title = "Run any BURAK BIM tool", Toolset = "core", ReadOnly = false,
            Description = "Run any BURAK BIM tool by name (found with burak_tools) with its arguments. Same safety as a direct call: read-only mode, audit trail, one undo step, capture mode.",
            InputSchema = S.Obj(S.R("tool", S.Str("Tool name, e.g. revit_room_finishes")), S.O("arguments", S.AnyObj("Arguments for that tool"))),
            Handler = (a, ctx) => throw new ToolException("burak_run is handled by the server")
        });
    }

    // ======================================================================================== tool calls
    object CallTool(Dictionary<string, object> p)
    {
        var name = Json.Str(p, "name", "") ?? "";
        var args = Json.Obj(p, "arguments") ?? new Dictionary<string, object>();
        if (name == "burak_run")
        {
            var inner = Json.Str(args, "tool") ?? "";
            if (inner == "burak_run") return ToolError("burak_run cannot run itself.");
            if (_tools.Get(inner) is { } it && !VisibleTools().Contains(it))
                return ToolError($"Tool '{inner}' belongs to toolset '{it.Toolset}', which is switched off in config/burakbim.json \"toolsets\".");
            var innerArgs = Json.Obj(args, "arguments");
            // some clients send nested arguments as a JSON string
            if (innerArgs == null && Json.Get(args, "arguments") is string argText && argText.TrimStart().StartsWith("{"))
            {
                try { innerArgs = Json.ParseObject(argText); } catch { }
            }
            return CallTool(Json.O("name", inner, "arguments", innerArgs ?? new Dictionary<string, object>()));
        }
        var tool = _tools.Get(name);
        if (tool == null)
        {
            var close = _tools.All.Select(t => t.Name).OrderBy(n => Levenshtein(n, name)).Take(3);
            return ToolError($"Unknown tool '{name}'. Did you mean: {string.Join(", ", close)}? Search with burak_tools.");
        }
        var ctx = new ToolContext { ToolName = name, Session = _session, Client = _client };
        var sw = Stopwatch.StartNew();
        try
        {
            if (!tool.ReadOnly && Config.ReadOnly)
                throw new ToolException("BURAK BIM is in READ-ONLY mode (config/burakbim.json \"readOnly\": true). Only read tools are allowed.");
            var result = tool.Handler(args, ctx);
            Audit.Write(ctx, args, true, sw.ElapsedMilliseconds, result is RichResult rr0 ? rr0.Data : result, null, !tool.ReadOnly);

            var content = new List<object>();
            switch (result)
            {
                case ImageResult img:
                    content.Add(Json.O("type", "image", "data", img.Base64, "mimeType", img.MimeType));
                    if (img.Caption != null) content.Add(Json.O("type", "text", "text", img.Caption));
                    break;
                case RichResult rich:
                    if (rich.Data != null) content.Add(Json.O("type", "text", "text", rich.Data is string rs ? rs : Json.Serialize(rich.Data, true)));
                    foreach (var im in rich.Images)
                    {
                        content.Add(Json.O("type", "image", "data", im.Base64, "mimeType", im.MimeType));
                        if (im.Caption != null) content.Add(Json.O("type", "text", "text", im.Caption));
                    }
                    break;
                default:
                    content.Add(Json.O("type", "text", "text", result is string s ? s : Json.Serialize(result, true)));
                    break;
            }
            // Capture mode: after a change, show the AI what the model looks like now
            if (!tool.ReadOnly && result is not ImageResult && result is not RichResult)
            {
                try
                {
                    var shot = Tools.CaptureTools.AutoCapture(tool, args, ctx);
                    if (shot != null)
                    {
                        content.Add(Json.O("type", "image", "data", shot.Base64, "mimeType", shot.MimeType));
                        content.Add(Json.O("type", "text", "text", "[capture mode] " + (shot.Caption ?? "result after the change")));
                    }
                }
                catch (Exception ex) { Log("auto-capture skipped: " + ex.Message); }
            }
            return Json.O("content", content, "isError", false);
        }
        catch (Exception ex)
        {
            var e = ex is AggregateException ae && ae.InnerException != null ? ae.InnerException : ex;
            if (e is System.Reflection.TargetInvocationException tie && tie.InnerException != null) e = tie.InnerException;
            var message = e is ToolException ? e.Message : $"{e.GetType().Name}: {e.Message}";
            if (e is not ToolException) Log($"{name} error: {e}");
            Audit.Write(ctx, args, false, sw.ElapsedMilliseconds, null, message, !tool.ReadOnly);
            return ToolError(message + Hint(tool, message));
        }
    }

    /// <summary>When a tool cannot reach a command, tell the agent about the universal fallbacks (incl. computer control).</summary>
    static string Hint(Tool tool, string message)
    {
        if (tool.Toolset is "computer" or "core") return "";
        var m = message.ToLowerInvariant();
        if (m.Contains("read-only")) return "";
        if (m.Contains("not running") || m.Contains("no running") || m.Contains("not registered") || m.Contains("not supported") || m.Contains("no api") || m.Contains("cannot") || m.Contains("not available"))
            return "\n\nIf no API route can do this, you may operate the application's user interface directly: call computer_request_control with a short reason (the user must approve on screen), then use computer_screenshot / computer_click / computer_type / computer_key, and computer_release_control when done.";
        return "";
    }

    static int Levenshtein(string a, string b)
    {
        a = a.ToLowerInvariant(); b = b.ToLowerInvariant();
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    static object ToolError(string message) =>
        Json.O("content", new List<object> { Json.O("type", "text", "text", message) }, "isError", true);
}
