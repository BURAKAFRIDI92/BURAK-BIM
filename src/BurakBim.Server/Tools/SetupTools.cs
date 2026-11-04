using BurakBim.Server.Core;
using BurakBim.Server.Setup;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>1.3: connect BURAK BIM to other AI apps on this PC from inside any AI chat.</summary>
public static class SetupTools
{
    public static void Register(ToolRegistry r)
    {
        r.Add(new Tool
        {
            Name = "burak_connect_clients", Title = "Connect BURAK BIM to AI apps", Toolset = "core", ReadOnly = false,
            Description = "List the AI apps on this PC (Claude Desktop, Claude Code, Codex/ChatGPT, Cursor, Windsurf, Antigravity, Gemini CLI, VS Code, Visual Studio, Cline, Roo, Kilo, LM Studio, Kiro, Amazon Q, Trae, Continue, opencode, Zed) " +
                          "and whether BURAK BIM is connected; connect them (backups are kept) or disconnect. The app must be restarted afterwards.",
            InputSchema = Obj(R("action", Enum("Action", "list", "connect", "disconnect")), O("clients", Str("auto (detected), all, or comma list of ids, e.g. 'cursor,vscode'"))),
            Handler = (a, ctx) => (Json.Str(a, "action") ?? "list") switch
            {
                "connect" => Json.O("results", ClientConnector.Connect(Json.Str(a, "clients") ?? "auto").Cast<object>().ToList(), "next", "Restart each connected app (fully quit Claude Desktop from the tray)."),
                "disconnect" => Json.O("results", ClientConnector.Disconnect().Cast<object>().ToList()),
                _ => Json.O("clients", ClientConnector.List().Cast<object>().ToList(), "thisClient", Mcp.McpServer.Current?.ClientName, "profile", Mcp.McpServer.Current?.Profile.Name,
                    "toolsListedHere", Mcp.McpServer.Current?.ListedTools().Count)
            }
        });
    }
}
