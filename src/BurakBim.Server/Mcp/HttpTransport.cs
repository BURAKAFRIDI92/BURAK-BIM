using System.Net;
using System.Text;
using BurakBim.Shared;

namespace BurakBim.Server.Mcp;

/// <summary>
/// 1.3: local Streamable-HTTP transport for MCP clients that cannot start a stdio process
/// (LM Studio, Open WebUI / mcpo, n8n, custom agents, a ChatGPT developer-mode connector through a tunnel...).
///   BurakBim.Server.exe --http            -> http://127.0.0.1:8765/mcp
///   BurakBim.Server.exe --http 9000
/// Loopback only. A bearer token is required when "httpToken" is set in config\burakbim.json.
/// </summary>
public static class HttpTransport
{
    public static int Run(McpServer server, int port)
    {
        var prefix = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try { listener.Start(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[BURAK BIM] cannot listen on {prefix}: {ex.Message}");
            return 1;
        }
        var session = Guid.NewGuid().ToString("N");
        Console.Error.WriteLine($"[BURAK BIM] MCP Streamable HTTP endpoint: {prefix}mcp  (Ctrl+C to stop)");
        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); } catch { break; }
            _ = Task.Run(() => Handle(server, ctx, session));
        }
        return 0;
    }

    static void Handle(McpServer server, HttpListenerContext ctx, string session)
    {
        var req = ctx.Request; var res = ctx.Response;
        try
        {
            res.Headers["Access-Control-Allow-Origin"] = "http://localhost";
            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (path is not ("/mcp" or ""))
            { res.StatusCode = 404; res.Close(); return; }
            // DNS-rebinding protection: only loopback origins
            var origin = req.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin) && !(origin.StartsWith("http://localhost") || origin.StartsWith("http://127.0.0.1") || origin.StartsWith("https://localhost")))
            { res.StatusCode = 403; res.Close(); return; }
            var token = Json.Str(Core.Config.Data, "httpToken");
            if (!string.IsNullOrEmpty(token) && req.Headers["Authorization"] != "Bearer " + token)
            { res.StatusCode = 401; res.Close(); return; }
            if (req.HttpMethod == "GET") { res.StatusCode = 405; res.Close(); return; } // no server-initiated SSE stream
            if (req.HttpMethod == "DELETE") { res.StatusCode = 200; res.Close(); return; }
            if (req.HttpMethod != "POST") { res.StatusCode = 405; res.Close(); return; }

            string body;
            using (var sr = new StreamReader(req.InputStream, Encoding.UTF8)) body = sr.ReadToEnd();
            var parsed = Json.Parse(body);
            object? reply = null;
            if (parsed is List<object> batch)
            {
                var list = new List<object>();
                foreach (var m in batch.OfType<Dictionary<string, object>>()) { var r = server.Dispatch(m); if (r != null) list.Add(r); }
                reply = list.Count > 0 ? list : null;
            }
            else if (parsed is Dictionary<string, object> msg) reply = server.Dispatch(msg);

            res.Headers["Mcp-Session-Id"] = session;
            if (reply == null) { res.StatusCode = 202; res.Close(); return; }
            var bytes = Encoding.UTF8.GetBytes(Json.Serialize(reply));
            res.StatusCode = 200;
            res.ContentType = "application/json";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();
        }
        catch (Exception ex)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(Json.Serialize(Json.O("jsonrpc", "2.0", "id", null, "error", Json.O("code", -32700, "message", ex.Message))));
                res.StatusCode = 400; res.ContentType = "application/json";
                res.OutputStream.Write(bytes, 0, bytes.Length); res.Close();
            }
            catch { }
        }
    }
}
