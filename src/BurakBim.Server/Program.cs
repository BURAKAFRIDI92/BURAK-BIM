using BurakBim.Server.Bridges;
using BurakBim.Server.Core;
using BurakBim.Server.Detection;
using BurakBim.Server.Mcp;
using BurakBim.Server.ScanToBim;
using BurakBim.Server.Tools;
using BurakBim.Shared;

namespace BurakBim.Server;

public static class Program
{
    public static List<string> PackErrors = new();

    public static ToolRegistry BuildRegistry()
    {
        var r = new ToolRegistry();
        CoreTools.Register(r);
        HostTools.Register(r);
        ScanTools.Register(r);
        ComTools.Register(r);
        UiTools.Register(r);
        AppTools.Register(r);
        AcadComTools.Register(r);
        ComMoreTools.Register(r);
        ArchicadCatalog.Register(r);
        // 1.3
        CaptureTools.Register(r);
        ComputerTools.Register(r);
        CsiTools.Register(r);
        BluebeamTools.Register(r);
        PlanSwiftTools.Register(r);
        SetupTools.Register(r);
        Skills.SkillLibrary.Register(r);
        PackErrors = new List<string>();
        Packs.PackLoader.Register(r, out var errs);
        PackErrors = errs;
        return r;
    }

    public static int Main(string[] args)
    {
        try { Console.InputEncoding = System.Text.Encoding.UTF8; } catch { }
        // 1.3: physical-pixel coordinates for screenshots and clicks on scaled (125 % / 150 %) and multi-monitor displays
        Core.NativeDpi.MakeProcessDpiAware();
        var home = args.SkipWhile(a => a != "--home").Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(home)) BurakPaths.Root = home;

        var cmd = args.FirstOrDefault(a => a.StartsWith("--") && a != "--home") ?? "";
        switch (cmd)
        {
            case "--version":
                Console.WriteLine($"BURAK BIM {BurakInfo.Version}");
                return 0;
            case "--detect":
                Console.WriteLine(Json.Serialize(SoftwareDetector.Detect(true).Select(p => (object)p.ToJson(SoftwareDetector.Running())).ToList(), true));
                return 0;
            case "--tools":
                Console.WriteLine(Json.Serialize(BuildRegistry().All.OrderBy(t => t.Toolset).ThenBy(t => t.Name)
                    .Select(t => (object)Json.O("name", t.Name, "toolset", t.Toolset, "title", t.Title, "readOnly", t.ReadOnly, "description", t.Description)).ToList(), true));
                return 0;
            case "--tools-md":
                Console.WriteLine(ToolsMarkdown());
                return 0;
            case "--packs-json":
                Console.WriteLine(Json.Serialize(Packs.PackLoader.LoadAll(out var perrs).Select(t => (object)Json.O("name", t.Name, "host", t.Host, "tx", t.Tx, "code", t.Code, "source", t.Source)).ToList(), true));
                foreach (var e in perrs) Console.Error.WriteLine("PACK ERROR: " + e);
                return perrs.Count == 0 ? 0 : 1;
            case "--doctor":
                return Doctor();
            case "--selftest":
                return SelfTest.Run();
            case "--install":
                return Setup.Installer.Install(args);
            case "--rescan":
                return Setup.Installer.Install(args, rescan: true);
            case "--compile-bridge":
                return Setup.Installer.CompileBridgeCommand(args);
            case "--uninstall":
                return Setup.Installer.Uninstall(args);
            case "--mcp-selftest":
                return McpSelfTest.Run();
            case "--connect-clients":
            {
                var which = args.SkipWhile(a => a != "--connect-clients").Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? "auto";
                var rep = Setup.ClientConnector.Connect(which);
                foreach (var r in rep)
                    Console.WriteLine((Json.Bool(r, "ok") ? "  [OK] " : "  [!]  ") + Json.Str(r, "client") + " -> " + Json.Str(r, "file") + (Json.Str(r, "error") is { } e ? "  (" + e + ")" : ""));
                if (rep.Count == 0) Console.WriteLine("  No AI clients detected. Add BURAK-BIM by hand using config\\mcp-client-snippet.json.");
                return rep.Any(r => !Json.Bool(r, "ok")) ? 2 : 0;
            }
            case "--disconnect-clients":
                foreach (var r in Setup.ClientConnector.Disconnect()) Console.WriteLine("  removed BURAK-BIM from " + Json.Str(r, "client") + " (" + Json.Str(r, "file") + ")" + (Json.Str(r, "error") is { } de ? " ERROR " + de : ""));
                return 0;
            case "--list-clients":
                Console.WriteLine(Json.Serialize(Setup.ClientConnector.List().Cast<object>().ToList(), true));
                return 0;
            case "--http":
            {
                var portText = args.SkipWhile(a => a != "--http").Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
                int port = int.TryParse(portText, out var pp) ? pp : (int)Json.Int(Core.Config.Data, "httpPort", 8765);
                return HttpTransport.Run(new McpServer(BuildRegistry()), port);
            }
            case "--help":
                Console.WriteLine("BURAK BIM MCP server " + BurakInfo.Version +
                    "\n  (no args)               run MCP over stdio (used by AI clients)" +
                    "\n  --http [port]           run MCP over local Streamable HTTP (http://127.0.0.1:8765/mcp)" +
                    "\n  --connect-clients [ids] connect AI clients (auto | all | claude-desktop,claude-code,codex,cursor,windsurf,antigravity,gemini,vscode,visualstudio,cline,roo,kilo,lmstudio,kiro,amazonq,trae,continue,opencode,zed)" +
                    "\n  --disconnect-clients    remove BURAK-BIM from every AI client config" +
                    "\n  --list-clients          AI clients found on this PC and whether they are connected" +
                    "\n  --install / --rescan / --uninstall   set up bridges, AI apps and skills (used by Setup.exe; no PowerShell)" +
                    "\n  --doctor                check installation, detected software, live bridges" +
                    "\n  --detect                JSON list of installed software" +
                    "\n  --tools                 JSON list of all tools" +
                    "\n  --selftest              scan-to-BIM math self tests" +
                    "\n  --mcp-selftest          protocol + schema checks for every AI-client profile" +
                    "\n  --home DIR              override install folder");
                return 0;
        }
        return new McpServer(BuildRegistry()).Run();
    }

    static int Doctor()
    {
        Console.WriteLine($"BURAK BIM {BurakInfo.Version}");
        Console.WriteLine($"Install folder : {BurakPaths.Root}");
        Console.WriteLine($"Config         : {Config.FilePath} (readOnly={Config.ReadOnly}, scripts={Config.AllowScripts})");
        Console.WriteLine("\nDetected Autodesk software:");
        var running = SoftwareDetector.Running();
        foreach (var p in SoftwareDetector.Detect(true))
            Console.WriteLine($"  [{p.Family,-10}] {p.Name,-45} control={string.Join("+", p.Control),-18} bridge={(p.BridgeInstalled ? "installed" : "-"),-9} {p.Exe}");
        Console.WriteLine("\nLive bridges:");
        var bridges = BridgeClient.Discover();
        if (bridges.Count == 0) Console.WriteLine("  (none - open Revit / AutoCAD / Navisworks)");
        foreach (var d in bridges)
        {
            string state;
            try { BridgeClient.Call(d, "bridge.ping", null, null, 5000); state = "OK"; } catch (Exception ex) { state = "ERROR " + ex.Message; }
            Console.WriteLine($"  {d.Id,-28} {d.Product,-35} port {d.Port}  {state}");
        }
        Console.WriteLine("\nAI clients:");
        foreach (var c in Setup.ClientConnector.List())
            if (Json.Bool(c, "installed") || Json.Bool(c, "connected"))
                Console.WriteLine($"  {Json.Str(c, "client"),-50} {(Json.Bool(c, "connected") ? "connected" : "NOT connected (run --connect-clients)")}");
        var reg = BuildRegistry();
        Console.WriteLine($"\nTools available: {reg.All.Count()}  (" + string.Join(", ", reg.All.GroupBy(t => t.Toolset).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count())) + ")");
        foreach (var e in PackErrors) Console.WriteLine("  pack warning: " + e);
        return 0;
    }

    static string ToolsMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var g in BuildRegistry().All.GroupBy(t => t.Toolset).OrderBy(g => g.Key))
        {
            sb.AppendLine($"\n### {g.Key}\n\n| Tool | What it does | Changes model |\n|---|---|---|");
            foreach (var t in g.OrderBy(t => t.Name))
                sb.AppendLine($"| `{t.Name}` | {t.Description.Replace("|", "\\|").Replace("\n", " ")} | {(t.ReadOnly ? "no" : "yes")} |");
        }
        return sb.ToString();
    }
}

/// <summary>Synthetic-scan tests for the scan-to-BIM algorithms (run: BurakBim.Server.exe --selftest).</summary>
public static class SelfTest
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var rnd = new Random(3);
        double N(double s) => (rnd.NextDouble() - 0.5) * 2 * s;
        var pts = new List<PointCloudMath.P3>();
        // 10 x 6 m room, 200 mm walls (faces at 0/200 and 9800/10000 in X, 0/200 and 5800/6000 in Y), 2 storeys of 3200 mm, slab 250
        foreach (var z0 in new[] { 0.0, 3200.0 })
        {
            for (double z = z0 + 50; z < z0 + 2900; z += 40)
            {
                for (double x = 200; x <= 9800; x += 25) { pts.Add(new(x, 200 + N(3), z)); pts.Add(new(x, 5800 + N(3), z)); pts.Add(new(x, 0 + N(3), z)); pts.Add(new(x, 6000 + N(3), z)); }
                for (double y = 200; y <= 5800; y += 25) { pts.Add(new(200 + N(3), y, z)); pts.Add(new(9800 + N(3), y, z)); pts.Add(new(0 + N(3), y, z)); pts.Add(new(10000 + N(3), y, z)); }
                // a 400x400 column
                for (double t = -200; t <= 200; t += 20) { pts.Add(new(5000 + t, 3000 - 200, z)); pts.Add(new(5000 + t, 3000 + 200, z)); pts.Add(new(5000 - 200, 3000 + t, z)); pts.Add(new(5000 + 200, 3000 + t, z)); }
            }
            for (double x = 200; x < 9800; x += 60) for (double y = 200; y < 5800; y += 60)
                { pts.Add(new(x, y, z0 + N(2))); pts.Add(new(x, y, z0 + 2950 + N(2))); }
        }
        var (planes, levels) = PointCloudMath.DetectLevels(pts);
        Check(levels.Count == 2 && Math.Abs(levels[0]) < 30 && Math.Abs(levels[1] - 3200) < 30, $"levels detected: {string.Join(", ", levels)}");
        var slice = PointCloudMath.Slice(pts, 900, 1500, 10);
        var segs = PointCloudMath.ExtractSegments(slice);
        var walls = PointCloudMath.CleanJoins(PointCloudMath.PairWalls(segs));
        var two = walls.Where(w => w.Kind == "two-face").ToList();
        Check(two.Count == 4, $"4 two-face walls found ({two.Count})");
        Check(two.All(w => Math.Abs(w.ThicknessMm - 200) <= 10), "wall thickness 200 mm: " + string.Join(",", two.Select(w => w.ThicknessMm)));
        var cols = PointCloudMath.DetectColumns(slice, segs);
        Check(cols.Count == 1 && Math.Abs(cols[0].Center.X - 5000) < 30 && Math.Abs(cols[0].SizeX - 400) <= 30, $"column found: {string.Join(";", cols.Select(c => $"{c.Center.X:0},{c.Center.Y:0} {c.SizeX}x{c.SizeY}"))}");
        var hull = PointCloudMath.ConvexHull(new() { new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(5, 5) });
        Check(hull.Count == 4, "convex hull");
        Console.WriteLine(fails == 0 ? "ALL TESTS PASSED" : $"{fails} FAILED");
        return fails == 0 ? 0 : 1;
    }
}
