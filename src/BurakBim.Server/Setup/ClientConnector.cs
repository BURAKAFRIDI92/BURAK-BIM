using System.Text;
using System.Text.RegularExpressions;
using BurakBim.Shared;

namespace BurakBim.Server.Setup;

/// <summary>
/// 1.3: connects BURAK BIM to every AI client installed on this PC (and disconnects it on uninstall).
/// Replaces the PowerShell JSON editing of 1.2 (Windows PowerShell 5.1 could mangle client configs and wrote files that
/// some clients refused). Every file is backed up (*.burakbim.bak), written as UTF-8 WITHOUT BOM, re-read to verify,
/// and restored from the backup if verification fails.
///
///   BurakBim.Server.exe --connect-clients [auto|all|id,id,...]     (default auto = every detected client)
///   BurakBim.Server.exe --disconnect-clients
///   BurakBim.Server.exe --list-clients
/// </summary>
public static class ClientConnector
{
    public const string ServerKey = "BURAK-BIM";

    public sealed class Target
    {
        public required string Id;
        public required string Label;
        public required string Kind;            // mcpServers | servers | codex | continue | opencode | zed
        public required Func<List<string>> Files;
        public required Func<bool> Installed;
        public bool AutoConnect = true;          // false = only when asked by id (files with user comments)
        public string? Note;
    }

    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string P(params string[] parts) => Path.Combine(parts);
    static bool D(string p) => Directory.Exists(p);
    static bool F(string p) => File.Exists(p);

    static List<string> MsixClaudeDirs()
    {
        var l = new List<string>();
        try
        {
            var pk = P(LocalAppData, "Packages");
            if (D(pk)) foreach (var d in Directory.GetDirectories(pk, "Claude_*")) l.Add(P(d, "LocalCache", "Roaming", "Claude"));
            if (D(pk)) foreach (var d in Directory.GetDirectories(pk, "AnthropicPBC.Claude*")) l.Add(P(d, "LocalCache", "Roaming", "Claude"));
        }
        catch { }
        return l;
    }

    /// <summary>
    /// Newer Claude Desktop keeps its data in %LOCALAPPDATA%\Claude-Data\claude_&lt;id&gt; instead of %APPDATA%\Claude.
    /// Claude has moved it before, so any existing config in a Claude folder there counts, one level down at most.
    /// Claude-3p is a separate mode with its own settings and is left alone.
    /// </summary>
    static List<string> ClaudeDataDirs()
    {
        var dirs = new List<string>();
        try
        {
            var data = P(LocalAppData, "Claude-Data");
            if (D(data)) dirs.AddRange(Directory.GetDirectories(data, "claude*"));
            foreach (var top in Directory.GetDirectories(LocalAppData, "Claude*"))
            {
                var name = Path.GetFileName(top);
                if (name.Equals("Claude-Data", StringComparison.OrdinalIgnoreCase) || name.Equals("Claude-3p", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var d in new[] { top }.Concat(Directory.GetDirectories(top)))
                    if (F(P(d, "claude_desktop_config.json"))) dirs.Add(d);
            }
        }
        catch { }
        return dirs;
    }

    /// <summary>globalStorage folders of VS Code-family editors (for Cline / Roo / Kilo extensions).</summary>
    static IEnumerable<string> EditorUserDirs()
    {
        foreach (var e in new[] { "Code", "Code - Insiders", "Cursor", "Windsurf", "VSCodium", "Trae", "Antigravity", "Kiro" })
            yield return P(AppData, e, "User");
    }

    static List<string> Ext(string extId, string file) =>
        EditorUserDirs().Select(u => P(u, "globalStorage", extId)).Where(D).Select(g => P(g, "settings", file)).ToList();

    public static List<Target> Targets() => new()
    {
        new Target { Id = "claude-desktop", Label = "Claude Desktop", Kind = "mcpServers",
            Files = () =>
            {
                var older = new List<string> { P(AppData, "Claude", "claude_desktop_config.json") };
                older.AddRange(MsixClaudeDirs().Select(d => P(d, "claude_desktop_config.json")));
                var current = ClaudeDataDirs().Select(d => P(d, "claude_desktop_config.json")).ToList();
                // a PC on the newer location only needs older files kept in step if they already exist
                return current.Count == 0 ? older : current.Concat(older.Where(F)).ToList();
            },
            Installed = () => ClaudeDataDirs().Count > 0 || D(P(AppData, "Claude")) || D(P(LocalAppData, "Claude")) || D(P(LocalAppData, "AnthropicClaude")) || MsixClaudeDirs().Count > 0,
            Note = "Fully quit Claude Desktop (tray icon > Quit) and start it again. Or install the one-click extension BURAK-BIM.mcpb." },
        new Target { Id = "claude-code", Label = "Claude Code (user scope)", Kind = "mcpServers",
            Files = () => new() { P(Home, ".claude.json") },
            Installed = () => F(P(Home, ".claude.json")) || D(P(Home, ".claude")) },
        new Target { Id = "codex", Label = "OpenAI Codex (CLI, IDE extension, ChatGPT desktop Codex)", Kind = "codex",
            Files = () => new() { P(Home, ".codex", "config.toml") },
            Installed = () => D(P(Home, ".codex")) },
        new Target { Id = "cursor", Label = "Cursor", Kind = "mcpServers",
            Files = () => new() { P(Home, ".cursor", "mcp.json") },
            Installed = () => D(P(Home, ".cursor")) || D(P(LocalAppData, "Programs", "cursor")) },
        new Target { Id = "windsurf", Label = "Windsurf", Kind = "mcpServers",
            Files = () => new() { P(Home, ".codeium", "windsurf", "mcp_config.json") },
            Installed = () => D(P(Home, ".codeium", "windsurf")) || D(P(LocalAppData, "Programs", "Windsurf")) },
        new Target { Id = "antigravity", Label = "Google Antigravity", Kind = "mcpServers",
            Files = () => new() { P(Home, ".gemini", "antigravity", "mcp_config.json") },
            Installed = () => D(P(Home, ".gemini", "antigravity")) || D(P(LocalAppData, "Programs", "Antigravity")) },
        new Target { Id = "gemini", Label = "Gemini CLI", Kind = "mcpServers",
            Files = () => new() { P(Home, ".gemini", "settings.json") },
            Installed = () => F(P(Home, ".gemini", "settings.json")) },
        new Target { Id = "vscode", Label = "VS Code (GitHub Copilot agent mode)", Kind = "servers",
            Files = () => new[] { "Code", "Code - Insiders" }.Where(e => D(P(AppData, e, "User"))).Select(e => P(AppData, e, "User", "mcp.json")).ToList(),
            Installed = () => D(P(AppData, "Code", "User")) || D(P(AppData, "Code - Insiders", "User")) },
        new Target { Id = "visualstudio", Label = "Visual Studio 2022+ (Copilot agent)", Kind = "servers",
            Files = () => new() { P(Home, ".mcp.json") },
            Installed = () => D(P(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio")) || F(P(Home, ".mcp.json")) },
        new Target { Id = "cline", Label = "Cline", Kind = "mcpServers",
            Files = () => Ext("saoudrizwan.claude-dev", "cline_mcp_settings.json"),
            Installed = () => Ext("saoudrizwan.claude-dev", "cline_mcp_settings.json").Count > 0 },
        new Target { Id = "roo", Label = "Roo Code", Kind = "mcpServers",
            Files = () => Ext("rooveterinaryinc.roo-cline", "mcp_settings.json"),
            Installed = () => Ext("rooveterinaryinc.roo-cline", "mcp_settings.json").Count > 0 },
        new Target { Id = "kilo", Label = "Kilo Code", Kind = "mcpServers",
            Files = () => Ext("kilocode.kilo-code", "mcp_settings.json"),
            Installed = () => Ext("kilocode.kilo-code", "mcp_settings.json").Count > 0 },
        new Target { Id = "lmstudio", Label = "LM Studio", Kind = "mcpServers",
            Files = () => new() { P(Home, ".lmstudio", "mcp.json") },
            Installed = () => D(P(Home, ".lmstudio")) },
        new Target { Id = "kiro", Label = "Kiro", Kind = "mcpServers",
            Files = () => new() { P(Home, ".kiro", "settings", "mcp.json") },
            Installed = () => D(P(Home, ".kiro")) },
        new Target { Id = "amazonq", Label = "Amazon Q Developer", Kind = "mcpServers",
            Files = () => new() { P(Home, ".aws", "amazonq", "mcp.json") },
            Installed = () => D(P(Home, ".aws", "amazonq")) },
        new Target { Id = "trae", Label = "Trae", Kind = "mcpServers",
            Files = () => new() { P(AppData, "Trae", "User", "mcp.json") },
            Installed = () => D(P(AppData, "Trae", "User")) },
        new Target { Id = "continue", Label = "Continue", Kind = "continue",
            Files = () => new() { P(Home, ".continue", "mcpServers", "burak-bim.yaml") },
            Installed = () => D(P(Home, ".continue")) },
        new Target { Id = "opencode", Label = "opencode", Kind = "opencode",
            Files = () => new() { P(Home, ".config", "opencode", "opencode.json") },
            Installed = () => D(P(Home, ".config", "opencode")) },
        new Target { Id = "zed", Label = "Zed", Kind = "zed", AutoConnect = false,
            Files = () => new() { P(AppData, "Zed", "settings.json") },
            Installed = () => D(P(AppData, "Zed")),
            Note = "Zed settings may contain comments; connect explicitly with --connect-clients zed (a backup is kept)." },
    };

    public static string ServerExe()
    {
        var self = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(self) && self.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return self;
        return Path.Combine(BurakPaths.Root, "server", "BurakBim.Server.exe");
    }

    /// <summary>Connect clients. which: "auto" (detected), "all", or comma list of ids. Returns a report per file.</summary>
    public static List<Dictionary<string, object>> Connect(string which = "auto", string? exe = null)
    {
        exe ??= ServerExe();
        var report = new List<Dictionary<string, object>>();
        var want = Want(which);
        foreach (var t in Targets())
        {
            bool explicitly = want != null && want.Contains(t.Id);
            bool auto = want == null && which != "all" && t.AutoConnect && SafeInstalled(t);
            bool all = which == "all" && t.AutoConnect;
            if (!(explicitly || auto || all)) continue;
            foreach (var f in t.Files())
            {
                var r = Json.O("client", t.Label, "id", t.Id, "file", f);
                try
                {
                    Write(t.Kind, f, exe, add: true);
                    r["ok"] = true;
                    if (t.Note != null) r["note"] = t.Note;
                }
                catch (Exception ex) { r["ok"] = false; r["error"] = ex.Message; }
                report.Add(r);
            }
        }
        // a ready-to-paste snippet for any other client
        try
        {
            var snip = Path.Combine(BurakPaths.ConfigDir, "mcp-client-snippet.json");
            File.WriteAllText(snip, Json.Serialize(Json.O("mcpServers", Json.O(ServerKey, Entry(exe, false))), true), new UTF8Encoding(false));
        }
        catch { }
        return report;
    }

    public static List<Dictionary<string, object>> Disconnect()
    {
        var report = new List<Dictionary<string, object>>();
        foreach (var t in Targets())
            foreach (var f in t.Files())
            {
                if (!File.Exists(f)) continue;
                try
                {
                    if (Write(t.Kind, f, ServerExe(), add: false)) report.Add(Json.O("client", t.Label, "file", f, "removed", true));
                }
                catch (Exception ex) { report.Add(Json.O("client", t.Label, "file", f, "error", ex.Message)); }
            }
        return report;
    }

    public static List<Dictionary<string, object>> List()
    {
        var l = new List<Dictionary<string, object>>();
        foreach (var t in Targets())
        {
            var files = t.Files();
            var connected = files.Any(f => { try { return File.Exists(f) && File.ReadAllText(f).Contains(ServerKey); } catch { return false; } });
            l.Add(Json.O("id", t.Id, "client", t.Label, "installed", SafeInstalled(t), "connected", connected, "files", files.Cast<object>().ToList()));
        }
        return l;
    }

    static bool SafeInstalled(Target t) { try { return t.Installed(); } catch { return false; } }

    static HashSet<string>? Want(string which)
    {
        if (string.IsNullOrWhiteSpace(which) || which is "auto" or "all") return null;
        return new HashSet<string>(which.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLowerInvariant()));
    }

    static Dictionary<string, object> Env() => Json.O("BURAK_BIM_HOME", BurakPaths.Root);

    static Dictionary<string, object> Entry(string exe, bool typed)
    {
        var e = new Dictionary<string, object>();
        if (typed) e["type"] = "stdio";
        e["command"] = exe;
        e["args"] = new List<object>();
        e["env"] = Env();
        return e;
    }

    /// <summary>Add or remove the BURAK-BIM entry in one config file. Returns true when the file changed.</summary>
    static bool Write(string kind, string file, string exe, bool add)
    {
        var dir = Path.GetDirectoryName(file)!;
        if (add) Directory.CreateDirectory(dir);
        string original = File.Exists(file) ? File.ReadAllText(file) : "";
        string updated;
        switch (kind)
        {
            case "codex": updated = CodexToml(original, exe, add); break;
            case "continue":
                if (!add) { if (File.Exists(file)) { Backup(file); File.Delete(file); return true; } return false; }
                updated = "name: BURAK BIM\nversion: " + BurakInfo.Version + "\nschema: v1\nmcpServers:\n  - name: " + ServerKey + "\n    command: '" + exe.Replace("'", "''") + "'\n    args: []\n    env:\n      BURAK_BIM_HOME: '" + BurakPaths.Root.Replace("'", "''") + "'\n";
                break;
            default:
                var start = add ? SeedStoreConfig(file, original) : original;
                var root = start.Trim().Length == 0 ? new Dictionary<string, object>() : ParseLenient(start, file);
                var container = kind switch { "servers" => "servers", "opencode" => "mcp", "zed" => "context_servers", _ => "mcpServers" };
                if (Json.Get(root, container) is not Dictionary<string, object> servers)
                {
                    if (!add) return false;
                    servers = new Dictionary<string, object>();
                    root[container] = servers;
                }
                if (!add)
                {
                    if (!servers.Remove(ServerKey)) return false;
                }
                else
                {
                    servers.Remove(ServerKey);
                    servers[ServerKey] = kind switch
                    {
                        "servers" => Entry(exe, true),
                        "opencode" => Json.O("type", "local", "command", new List<object> { exe }, "enabled", true, "environment", Env()),
                        "zed" => Json.O("source", "custom", "command", exe, "args", new List<object>(), "env", Env()),
                        _ when file.Contains("claude-dev") || file.Contains("roo-cline") || file.Contains("kilo-code") =>
                            Json.O("command", exe, "args", new List<object>(), "env", Env(), "disabled", false, "autoApprove", new List<object>(), "timeout", 600),
                        _ when file.EndsWith(".claude.json", StringComparison.OrdinalIgnoreCase) => Entry(exe, true),
                        _ => Entry(exe, false)
                    };
                }
                updated = Json.Serialize(root, true) + "\n";
                break;
        }
        if (updated == original) return false;
        if (File.Exists(file)) Backup(file);
        var tmp = file + ".burakbim.tmp";
        File.WriteAllText(tmp, updated, new UTF8Encoding(false));
        // verify before replacing
        if (kind is not ("codex" or "continue")) Json.ParseObject(File.ReadAllText(tmp));
        File.Copy(tmp, file, true);
        File.Delete(tmp);
        return true;
    }

    /// <summary>
    /// The Microsoft Store app prefers a config in its private folder over the one in %APPDATA%. A new private config must
    /// therefore start from the real one, or it hides the user's other servers and preferences. 1.3.0 left a private config
    /// holding only our entry, so that exact case is repaired too. A private config with anything else in it is left alone.
    /// </summary>
    static string SeedStoreConfig(string file, string original)
    {
        if (file.IndexOf(@"\LocalCache\Roaming\Claude\", StringComparison.OrdinalIgnoreCase) < 0) return original;
        var real = P(AppData, "Claude", "claude_desktop_config.json");
        if (!File.Exists(real)) return original;
        if (original.Trim().Length == 0) return File.ReadAllText(real);
        var current = ParseLenient(original, file);
        bool onlyOurs = current.Count == 1 && Json.Get(current, "mcpServers") is Dictionary<string, object> s && s.Count == 1 && s.ContainsKey(ServerKey);
        return onlyOurs ? File.ReadAllText(real) : original;
    }

    static void Backup(string file)
    {
        try { File.Copy(file, file + ".burakbim.bak", true); } catch { }
    }

    static Dictionary<string, object> ParseLenient(string text, string file)
    {
        try { return Json.ParseObject(text.TrimStart('﻿')); }
        catch (Exception ex) { throw new InvalidOperationException($"{file} is not valid JSON ({ex.Message}). It was left unchanged - fix it or add BURAK-BIM by hand (see config\\mcp-client-snippet.json)."); }
    }

    /// <summary>Insert/replace/remove the [mcp_servers.BURAK-BIM] table (and its .env sub-table) in Codex config.toml.</summary>
    static string CodexToml(string text, string exe, bool add)
    {
        // remove any existing BURAK-BIM tables (quoted or bare key)
        var rx = new Regex(@"(?ms)^\[mcp_servers\.(""?)BURAK-BIM\1(\.env)?\]\s*.*?(?=^\[|\z)");
        var cleaned = rx.Replace(text, "").TrimEnd();
        if (!add) return cleaned.Length == 0 ? "" : cleaned + "\n";
        var lit = exe.Contains('\'') ? "\"" + exe.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : "'" + exe + "'";
        var home = BurakPaths.Root;
        var homeLit = home.Contains('\'') ? "\"" + home.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : "'" + home + "'";
        var block = new StringBuilder();
        block.Append("[mcp_servers.BURAK-BIM]\n");
        block.Append("command = ").Append(lit).Append('\n');
        block.Append("args = []\n");
        block.Append("startup_timeout_sec = 60\n");
        block.Append("tool_timeout_sec = 900\n");
        block.Append("\n[mcp_servers.BURAK-BIM.env]\n");
        block.Append("BURAK_BIM_HOME = ").Append(homeLit).Append('\n');
        return (cleaned.Length > 0 ? cleaned + "\n\n" : "") + block;
    }
}
