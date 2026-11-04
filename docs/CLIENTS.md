# Connecting AI clients

**The easy way:** the installer connects every AI app it finds. For apps installed later, run:

```powershell
"D:\BURAK BIM\server\BurakBim.Server.exe" --connect-clients            # every detected app
"D:\BURAK BIM\server\BurakBim.Server.exe" --connect-clients cursor,vscode
"D:\BURAK BIM\server\BurakBim.Server.exe" --list-clients               # what is installed / connected
"D:\BURAK BIM\server\BurakBim.Server.exe" --disconnect-clients
```

You can also ask in any connected chat: *"burak_connect_clients action=connect"*.

Every edited file gets a `*.burakbim.bak` backup first. The file is written as strict UTF-8 JSON without a BOM, then read back to verify it.

The rest of this page explains how to connect an app by hand. In every example, replace `D:\BURAK BIM` with your install folder.

**Server command:** `D:\BURAK BIM\server\BurakBim.Server.exe`. The transport is **stdio** and no arguments are needed. Setting `BURAK_BIM_HOME` is optional.

| App | Config file | Key |
|---|---|---|
| Claude Desktop | `%APPDATA%\Claude\claude_desktop_config.json`. Microsoft Store version: `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\claude_desktop_config.json` | `mcpServers` |
| Claude Code | `%USERPROFILE%\.claude.json` (user scope), or `claude mcp add-json` | `mcpServers` |
| OpenAI Codex (CLI, IDE, ChatGPT desktop Codex) | `%USERPROFILE%\.codex\config.toml` | `[mcp_servers.BURAK-BIM]` |
| Cursor | `%USERPROFILE%\.cursor\mcp.json` | `mcpServers` |
| Windsurf | `%USERPROFILE%\.codeium\windsurf\mcp_config.json` | `mcpServers` |
| Google Antigravity | `%USERPROFILE%\.gemini\antigravity\mcp_config.json` | `mcpServers` |
| Gemini CLI | `%USERPROFILE%\.gemini\settings.json` | `mcpServers` |
| VS Code (Copilot agent) | `%APPDATA%\Code\User\mcp.json` (Insiders: `Code - Insiders`) | `servers` (with `"type": "stdio"`) |
| Visual Studio 2022+ | `%USERPROFILE%\.mcp.json` | `servers` |
| Cline / Roo Code / Kilo Code | `%APPDATA%\<editor>\User\globalStorage\<extension>\settings\*mcp_settings.json` | `mcpServers` |
| LM Studio | `%USERPROFILE%\.lmstudio\mcp.json` | `mcpServers` |
| Kiro | `%USERPROFILE%\.kiro\settings\mcp.json` | `mcpServers` |
| Amazon Q Developer | `%USERPROFILE%\.aws\amazonq\mcp.json` | `mcpServers` |
| Trae | `%APPDATA%\Trae\User\mcp.json` | `mcpServers` |
| Continue | `%USERPROFILE%\.continue\mcpServers\burak-bim.yaml` | YAML block |
| opencode | `%USERPROFILE%\.config\opencode\opencode.json` | `mcp` (`"type": "local"`) |
| Zed | `%APPDATA%\Zed\settings.json` (connected only when you ask, because the file may hold comments) | `context_servers` |

## Claude Desktop

The **one-click** way: double-click `D:\BURAK BIM\BURAK-BIM.mcpb`, or use *Start menu > BURAK BIM > Add to Claude Desktop*, and confirm in Claude.

The manual way, in `claude_desktop_config.json`:

```json
{ "mcpServers": { "BURAK-BIM": { "command": "D:\\BURAK BIM\\server\\BurakBim.Server.exe", "args": [] } } }
```

After editing, **fully quit** Claude Desktop (tray icon > Quit) and start it again. Closing the window is not enough.

## Claude Code
```powershell
claude mcp add-json BURAK-BIM '{"type":"stdio","command":"D:\\BURAK BIM\\server\\BurakBim.Server.exe","args":[]}' --scope user
```

## OpenAI Codex
```toml
[mcp_servers.BURAK-BIM]
command = 'D:\BURAK BIM\server\BurakBim.Server.exe'
args = []
startup_timeout_sec = 60
tool_timeout_sec = 900
```

**ChatGPT (the chat app)** only accepts *remote HTTPS* connectors. Use Codex, or run `BurakBim.Server.exe --http 8765` behind your own secure tunnel, with `httpToken` set in `config\burakbim.json`.

## VS Code (GitHub Copilot agent mode)
```json
{ "servers": { "BURAK-BIM": { "type": "stdio", "command": "D:\\BURAK BIM\\server\\BurakBim.Server.exe", "args": [] } } }
```

## Apps that only speak HTTP
```powershell
"D:\BURAK BIM\server\BurakBim.Server.exe" --http 8765      # endpoint: http://127.0.0.1:8765/mcp (Streamable HTTP, JSON responses)
```
It listens on loopback only. To require a bearer token, set `"httpToken": "<secret>"` in `config\burakbim.json`.

## Tool limits per app
BURAK BIM recognizes the app from its MCP `clientInfo` and lists as many tools as that app accepts. The rest are always reachable through `burak_tools` and `burak_run`.

| App | Tools listed | Schemas |
|---|---|---|
| Claude Code | all | standard |
| Claude Desktop | 160 | standard |
| VS Code, Visual Studio | 90 | standard |
| Codex / ChatGPT | 90 | strict |
| Gemini CLI | 90 | strict |
| Antigravity | 60 | strict |
| Windsurf, Continue, Kiro | 60 | standard / strict |
| Cursor, Trae, LM Studio | 40 | standard / strict |
| unknown apps | 60 | standard |

To change this, set `"maxTools"`, `"exposure": "full"` or `"strictSchemas"` in `config\burakbim.json`. You can also set the `BURAK_BIM_MAX_TOOLS` environment variable in one app's config.

## Check the connection
Ask the AI to *"call burak_status"*, or run `BurakBim.Server.exe --doctor` in a terminal. `--doctor` lists your software, the bridges and which AI apps are connected.

## Troubleshooting
| Symptom | Fix |
|---|---|
| The app shows BURAK-BIM as failed / disconnected | Run `BurakBim.Server.exe --doctor`. If Windows Defender or company antivirus quarantined the exe, allow `D:\BURAK BIM\server\BurakBim.Server.exe`. |
| Claude Desktop shows no tools | Fully quit Claude Desktop from the tray, then restart it. Check `%APPDATA%\Claude\logs\mcp-server-BURAK-BIM.log`. |
| "Too many tools" errors in VS Code or Cursor | Update BURAK BIM to 1.3. If other MCP servers also add tools, lower `maxTools`. |
| Revit has no BURAK BIM tab | Run setup again with Revit closed, and check `D:\BURAK BIM\logs\revit-<year>.log`. |
