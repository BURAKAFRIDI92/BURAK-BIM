# BURAK BIM 1.3.1

**This release makes BURAK BIM work in every version on every PC, lets the AI see the model, and adds a way to reach every command.**

## Download
- **`BURAK-BIM-Installer-1.3.1-win-x64.zip`**: start here. It contains `BURAK-BIM-Installer.exe`, `BURAK-BIM-Uninstaller.exe`, a short guide and checksums. No internet, no .NET and no admin rights are needed.
- `BURAK-BIM-Setup-1.3.1.exe`: the same installer on its own.
- **`BURAK-BIM-1.3.1.mcpb`**: a one-click Claude Desktop extension. Double-click it.
- `BURAK-BIM-1.3.1.zip`: the same package, for `installer\install.ps1`.

## New in 1.3.1
- **BURAK BIM tab in Navisworks and AutoCAD** (and every AutoCAD-based product), next to the one Revit already has.
- **Clash Detective works on Navisworks 2027.** Autodesk removed `DocumentClashTests.Tests` in 2027, which stopped every clash tool.
- **Clash status, assignee and NWD publishing work on Navisworks 2026 and 2027**, where Autodesk changed those calls.
- **Image exports work on Navisworks 2024 and later**, including the HTML clash report.
- **Read-only mode now blocks 30 Navisworks tools** that change the model or view but were marked as read-only.

## Highlights
- **Prebuilt bridges** for Revit and AutoCAD (and Civil 3D and every vertical) 2022 to 2027, and Navisworks 2022 to 2026, with C# scripting. Navisworks 2027 is compiled during setup against your installed copy. Nothing is downloaded at setup.
- **Connects to 19 AI apps automatically:** Claude Desktop (classic and Store), Claude Code, Codex/ChatGPT, Cursor, Windsurf, Antigravity, Gemini, VS Code, Visual Studio, Cline, Roo, Kilo, LM Studio, Kiro, Amazon Q, Trae, Continue, opencode and Zed. Each app gets a tool list within its limits.
- **27 BIM and VDC workflow skills:** project setup, model QA gate, 4D scheduling, 5D quantity reconciliation, drawing review, handover and a router that maps any responsibility to its skill.
- **Capture mode:** `revit_capture` renders any plan, section, elevation, 3D view or sheet. `capture` shows any window. Every change comes back with a picture.
- **Computer control with your permission:** mouse and keyboard work only after you click Yes on screen. Ctrl+Alt+Q stops it instantly.
- **New connectors:**
  - SAP2000, ETABS, CSiBridge and SAFE, including every OAPI function through `csi_call`.
  - Bluebeam Revu: markups, takeoff summary and scripts.
  - PlanSwift: item tree, quantities and properties.

## Before you run it
If this build is not code-signed, Windows SmartScreen may show "Windows protected your PC". Compare the file with `SHA256SUMS.txt`, then choose **More info > Run anyway**. BURAK BIM runs on Windows only.

## What was tested
- **Tested automatically on a clean Windows machine:** the server, the MCP handshake for each AI client profile, compiling the bridges, install, upgrade cleanup and uninstall.
- **Not yet tested live in this release:** the bridges loading inside real Revit, AutoCAD and Navisworks; Navisworks 2027; Bluebeam Revu and PlanSwift against the real programs. Please report problems on the Issues page.

## Upgrading from 1.2
1. Close Revit, AutoCAD, Navisworks and your AI apps.
2. Run the new Setup.exe over the old installation. Your audit trail, reports and settings are kept.
3. Restart your AI app.

See CHANGELOG.md for the full list.
