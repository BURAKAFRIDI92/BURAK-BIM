# Connecting AI clients

The installer configures every client it finds and backs up each file to `*.bak` first. Use this page to connect a client by hand. In every example, replace `D:\BURAK BIM` with your own install folder.

**Server command:** `D:\BURAK BIM\server\BurakBim.Server.exe`. Transport is **stdio** and no arguments are needed.

## Claude Desktop
File: `%APPDATA%\Claude\claude_desktop_config.json`. For the Microsoft Store version: `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\claude_desktop_config.json`.
```json
{ "mcpServers": { "BURAK-BIM": { "command": "D:\\BURAK BIM\\server\\BurakBim.Server.exe", "args": [] } } }
```
Fully quit Claude Desktop from the tray icon, then start it again.

## Claude Code
```powershell
claude mcp add-json BURAK-BIM '{"type":"stdio","command":"D:\\BURAK BIM\\server\\BurakBim.Server.exe","args":[]}' --scope user
```

## ChatGPT Codex (CLI / IDE)
File: `%USERPROFILE%\.codex\config.toml`
```toml
[mcp_servers.BURAK-BIM]
command = 'D:\BURAK BIM\server\BurakBim.Server.exe'
args = []
startup_timeout_sec = 30
tool_timeout_sec = 600
```

## Google Antigravity
Agent panel › … › MCP Servers › Manage › *View raw config*. The file is `%USERPROFILE%\.gemini\antigravity\mcp_config.json`:
```json
{ "mcpServers": { "BURAK-BIM": { "command": "D:\\BURAK BIM\\server\\BurakBim.Server.exe", "args": [] } } }
```
Antigravity limits how many tools can be active at once. Keep `"toolsets": ["auto"]`, or list only the toolsets you need in `config\burakbim.json`.

## Gemini CLI
File: `%USERPROFILE%\.gemini\settings.json`. Add the same `mcpServers` entry as above.

## Cursor / Windsurf
- Cursor: `%USERPROFILE%\.cursor\mcp.json`
- Windsurf: `%USERPROFILE%\.codeium\windsurf\mcp_config.json`

Both use the same `mcpServers` entry. Cursor warns above about 40 active tools, so restrict toolsets if needed.

## VS Code (GitHub Copilot agent mode)
File: `%APPDATA%\Code\User\mcp.json`
```json
{ "servers": { "BURAK-BIM": { "type": "stdio", "command": "D:\\BURAK BIM\\server\\BurakBim.Server.exe", "args": [] } } }
```

## Check the connection
Ask the AI to *"call burak_status"*. You can also run `BurakBim.Server.exe --doctor` in a terminal.
