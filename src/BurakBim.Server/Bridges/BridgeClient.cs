using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Bridges;

/// <summary>Finds running host bridges (runtime\*.json) and sends them requests.</summary>
public static class BridgeClient
{
    sealed class Conn
    {
        public required BridgeDescriptor D;
        public TcpClient? Tcp;
        public StreamReader? Reader;
        public StreamWriter? Writer;
        public readonly object Lock = new();
        public long NextId;
    }

    static readonly Dictionary<string, Conn> Conns = new();

    public static List<BridgeDescriptor> Discover(string? host = null)
    {
        var list = new List<BridgeDescriptor>();
        foreach (var f in Directory.GetFiles(BurakPaths.RuntimeDir, "*.json"))
        {
            try
            {
                var d = BridgeDescriptor.FromJson(Json.ParseObject(File.ReadAllText(f)));
                d.FilePath = f;
                if (!Alive(d.Pid)) { TryDelete(f); continue; }
                if (host == null || string.Equals(d.Host, host, StringComparison.OrdinalIgnoreCase)) list.Add(d);
            }
            catch { }
        }
        return list.OrderByDescending(d => d.StartedUtc).ToList();
    }

    static bool Alive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch { return false; }
    }

    static void TryDelete(string f) { try { File.Delete(f); } catch { } }

    public static BridgeDescriptor Resolve(string host, string? target)
    {
        var all = Discover(host);
        if (all.Count == 0)
        {
            var installed = Detection.SoftwareDetector.Detect().Where(p => p.Family == host).Select(p => p.Name).ToList();
            var hint = host switch
            {
                "revit" => "Open Revit and a model. The BURAK BIM tab > Start/Stop must show RUNNING.",
                "autocad" => "Start AutoCAD / Civil 3D (BURAK BIM loads automatically; type BURAKBIM to check). If a security prompt asked to load BurakBim, choose 'Always Load'.",
                "navisworks" => "Start Navisworks Manage/Simulate (BURAK BIM loads automatically).",
                _ => ""
            };
            throw new ToolException($"No running {host} instance with the BURAK BIM bridge. {hint} " +
                (installed.Count > 0 ? "Installed: " + string.Join(", ", installed) + ". burak_launch can start it." : "No " + host + " installation detected on this PC."));
        }
        if (string.IsNullOrWhiteSpace(target)) return all[0];
        var t = target.Trim();
        var hit = all.FirstOrDefault(d => d.Id.Equals(t, StringComparison.OrdinalIgnoreCase))
               ?? all.FirstOrDefault(d => d.Pid.ToString() == t)
               ?? all.FirstOrDefault(d => d.Year.ToString() == t)
               ?? all.FirstOrDefault(d => (d.Product ?? "").Contains(t, StringComparison.OrdinalIgnoreCase));
        return hit ?? throw new ToolException($"No {host} instance matches '{target}'. Running: " + string.Join(", ", all.Select(d => d.Id + " (" + d.Product + ")")));
    }

    public static object? Call(string host, string? target, string method, Dictionary<string, object>? args, ToolContext ctx, int? timeoutMs = null)
    {
        var d = Resolve(host, target);
        return Call(d, method, args, ctx, timeoutMs);
    }

    public static object? Call(BridgeDescriptor d, string method, Dictionary<string, object>? args, ToolContext? ctx, int? timeoutMs = null)
    {
        Conn c;
        lock (Conns)
        {
            if (!Conns.TryGetValue(d.Id, out c!) || c.D.Token != d.Token)
            {
                c = new Conn { D = d };
                Conns[d.Id] = c;
            }
        }
        var timeout = timeoutMs ?? Config.TimeoutMs;
        lock (c.Lock)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    if (c.Tcp == null || !c.Tcp.Connected)
                    {
                        c.Tcp?.Dispose();
                        c.Tcp = new TcpClient { NoDelay = true };
                        c.Tcp.Connect("127.0.0.1", d.Port);
                        var s = c.Tcp.GetStream();
                        c.Reader = new StreamReader(s, new UTF8Encoding(false));
                        c.Writer = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                    }
                    c.Tcp.ReceiveTimeout = timeout + 15000;
                    var id = ++c.NextId;
                    var req = Json.O("id", id, "token", d.Token, "method", method, "params", args ?? new Dictionary<string, object>(),
                        "timeoutMs", timeout,
                        "meta", Json.O("client", ctx?.Client ?? "", "session", ctx?.Session ?? "", "tool", ctx?.ToolName ?? ""));
                    c.Writer!.WriteLine(Json.Serialize(req));
                    var line = c.Reader!.ReadLine() ?? throw new IOException("bridge closed the connection");
                    var resp = Json.ParseObject(line);
                    if (Json.Bool(resp, "ok")) return Json.Get(resp, "result");
                    var err = Json.Obj(resp, "error");
                    throw new ToolException($"[{d.Product}] " + Json.Str(err, "message", "bridge error"));
                }
                catch (ToolException) { throw; }
                catch (Exception ex) when (attempt == 0 && ex is IOException or SocketException or ObjectDisposedException)
                {
                    c.Tcp?.Dispose(); c.Tcp = null;
                }
                catch (Exception ex)
                {
                    c.Tcp?.Dispose(); c.Tcp = null;
                    throw new ToolException($"Lost connection to {d.Product} (pid {d.Pid}): {ex.Message}. If the application is frozen in a dialog, close the dialog and retry.");
                }
            }
        }
        throw new ToolException("Bridge call failed.");
    }
}
