// BURAK BIM bridge core: loopback JSON-line RPC server that runs inside an Autodesk host.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BurakBim.Shared;

namespace BurakBim.Bridge
{
    public delegate object BridgeHandler(Dictionary<string, object> p);

    public sealed class BridgeCommand
    {
        public string Name;
        public string Description;
        /// <summary>True when the handler touches the host API and must run on the host's main thread.</summary>
        public bool MainThread = true;
        /// <summary>True when the handler can change the model (blocked in read-only mode).</summary>
        public bool Writes;
        public BridgeHandler Handler;
    }

    /// <summary>Thrown for user-facing errors (bad arguments, missing elements). Message is returned as-is.</summary>
    public sealed class BridgeException : Exception
    {
        public BridgeException(string message) : base(message) { }
    }

    public sealed class BridgeServer
    {
        readonly IHostAdapter _host;
        readonly Dictionary<string, BridgeCommand> _commands = new Dictionary<string, BridgeCommand>(StringComparer.OrdinalIgnoreCase);
        TcpListener _listener;
        Thread _acceptThread;
        volatile bool _running;
        BridgeDescriptor _descriptor;
        int _requests;

        public static BridgeServer Current { get; private set; }

        public BridgeServer(IHostAdapter host)
        {
            _host = host;
            Current = this;
            Register(new BridgeCommand
            {
                Name = "bridge.ping", MainThread = false, Description = "Liveness check.",
                Handler = p => Json.O("pong", true, "host", _host.Host, "product", _host.Product, "year", _host.Year,
                    "pid", System.Diagnostics.Process.GetCurrentProcess().Id, "requests", _requests, "bridgeVersion", BurakInfo.Version)
            });
            Register(new BridgeCommand
            {
                Name = "bridge.methods", MainThread = false, Description = "List every bridge method.",
                Handler = p =>
                {
                    var list = new List<object>();
                    foreach (var c in _commands.Values)
                        list.Add(Json.O("name", c.Name, "description", c.Description, "writes", c.Writes));
                    return list;
                }
            });
            CoreCommands.RegisterAll(this, host);
        }

        public IHostAdapter Host { get { return _host; } }
        public bool IsRunning { get { return _running; } }
        public int Port { get { return _descriptor != null ? _descriptor.Port : 0; } }

        public void Register(BridgeCommand cmd) { _commands[cmd.Name] = cmd; }

        public void Start()
        {
            if (_running) return;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _descriptor = new BridgeDescriptor
            {
                Host = _host.Host,
                Product = _host.Product,
                Year = _host.Year,
                Pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                Port = port,
                Token = NewToken(),
                StartedUtc = DateTime.UtcNow,
                Protocol = BurakInfo.BridgeProtocol,
                BridgeVersion = BurakInfo.Version
            };
            var file = Path.Combine(BurakPaths.RuntimeDir, _descriptor.Id + ".json");
            File.WriteAllText(file, Json.Serialize(_descriptor.ToJson(), true));
            _descriptor.FilePath = file;
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "BURAK BIM bridge" };
            _acceptThread.Start();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Stop();
            BridgeLog.Write("Bridge started on 127.0.0.1:" + port + " for " + _host.Product);
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener.Stop(); } catch { }
            try { if (_descriptor != null && File.Exists(_descriptor.FilePath)) File.Delete(_descriptor.FilePath); } catch { }
            BridgeLog.Write("Bridge stopped");
        }

        static string NewToken()
        {
            var b = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            var sb = new StringBuilder();
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { if (!_running) return; continue; }
                var t = new Thread(() => ClientLoop(client)) { IsBackground = true, Name = "BURAK BIM client" };
                t.Start();
            }
        }

        void ClientLoop(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.NoDelay = true;
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, new UTF8Encoding(false));
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                    string line;
                    while (_running && (line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0) continue;
                        var response = HandleLine(line);
                        writer.WriteLine(response);
                    }
                }
                catch (Exception ex)
                {
                    BridgeLog.Write("Client loop ended: " + ex.Message);
                }
            }
        }

        string HandleLine(string line)
        {
            object id = null;
            try
            {
                var req = Json.ParseObject(line);
                id = Json.Get(req, "id");
                if (!string.Equals(Json.Str(req, "token"), _descriptor.Token, StringComparison.Ordinal))
                    return Error(id, "unauthorized", "Bad bridge token.");
                var method = Json.Str(req, "method", "");
                BridgeCommand cmd;
                if (!_commands.TryGetValue(method, out cmd))
                    return Error(id, "unknown_method", "Unknown bridge method '" + method + "'. Call bridge.methods.");
                var p = Json.Obj(req, "params") ?? new Dictionary<string, object>();
                int timeout = (int)Json.Int(req, "timeoutMs", 120000);
                Interlocked.Increment(ref _requests);
                var meta = Json.Obj(req, "meta") ?? new Dictionary<string, object>();
                Func<object> run = () => { RequestContext.Meta = meta; return cmd.Handler(p); };
                object result = cmd.MainThread ? _host.InvokeOnMain(run, timeout) : run();
                return Json.Serialize(Json.O("id", id, "ok", true, "result", result));
            }
            catch (Exception ex)
            {
                var inner = Unwrap(ex);
                if (!(inner is BridgeException)) BridgeLog.Write("Handler error: " + inner);
                return Error(id, inner is BridgeException ? "bad_request" : inner.GetType().Name, inner.Message);
            }
        }

        public static Exception Unwrap(Exception ex)
        {
            while ((ex is System.Reflection.TargetInvocationException || ex is AggregateException) && ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }

        static string Error(object id, string code, string message)
        {
            return Json.Serialize(Json.O("id", id, "ok", false, "error", Json.O("code", code, "message", message)));
        }
    }

    /// <summary>
    /// Work queue drained on the host's main thread. Each host decides how to "kick" it
    /// (Revit: ExternalEvent; AutoCAD/Navisworks: UI SynchronizationContext).
    /// A timed-out item is cancelled so it never runs late against the model.
    /// </summary>
    public sealed class MainThreadQueue
    {
        sealed class Item
        {
            public Func<object> Work;
            public object Result;
            public Exception Error;
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public int State; // 0 pending, 1 running, 2 cancelled
        }

        readonly Queue<Item> _q = new Queue<Item>();
        readonly Action _kick;

        public MainThreadQueue(Action kick) { _kick = kick; }

        public object Invoke(Func<object> work, int timeoutMs)
        {
            var item = new Item { Work = work };
            lock (_q) _q.Enqueue(item);
            _kick();
            if (!item.Done.WaitOne(timeoutMs <= 0 ? 120000 : timeoutMs))
            {
                if (Interlocked.CompareExchange(ref item.State, 2, 0) == 0)
                    throw new BridgeException("The application is busy (a dialog, command or edit mode is active). The request was cancelled and not run. Finish or cancel the active command in the application, then retry.");
                item.Done.WaitOne(); // already running: wait for it to finish
            }
            if (item.Error != null) throw new System.Reflection.TargetInvocationException(item.Error);
            return item.Result;
        }

        /// <summary>Call on the main thread.</summary>
        public void Drain()
        {
            while (true)
            {
                Item item;
                lock (_q)
                {
                    if (_q.Count == 0) return;
                    item = _q.Dequeue();
                }
                if (Interlocked.CompareExchange(ref item.State, 1, 0) != 0) continue; // cancelled
                try { item.Result = item.Work(); }
                catch (Exception ex) { item.Error = BridgeServer.Unwrap(ex); }
                finally { item.Done.Set(); }
            }
        }

        public int Pending { get { lock (_q) return _q.Count; } }
    }

    /// <summary>Metadata of the request being executed (AI client, session, tool) - used to stamp created elements.</summary>
    public static class RequestContext
    {
        public static Dictionary<string, object> Meta = new Dictionary<string, object>();
        public static string Get(string key, string def) { return Json.Str(Meta, key, def); }
    }

    public static class BridgeLog
    {
        static readonly object Gate = new object();
        public static string Name = "bridge";

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    var file = Path.Combine(BurakPaths.LogsDir, Name + ".log");
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
