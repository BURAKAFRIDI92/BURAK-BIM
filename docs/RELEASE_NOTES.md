# BURAK BIM 1.3.0

**This release makes BURAK BIM work in every version on every PC, lets the AI see the model, and adds a way to reach every command.**

## Download
- **`BURAK-BIM-Installer-1.3.0-win-x64.zip`**: start here. It contains `BURAK-BIM-Installer.exe`, `BURAK-BIM-Uninstaller.exe`, a short guide and checksums. No internet, no .NET and no admin rights are needed.
- `BURAK-BIM-Setup-1.3.0.exe`: the same installer on its own.
- **`BURAK-BIM-1.3.0.mcpb`**: a one-click Claude Desktop extension. Double-click it.
- `BURAK-BIM-1.3.0.zip`: the same package, for `installer\install.ps1`.

## Highlights
- **Prebuilt bridges** for Revit, AutoCAD (and Civil 3D and every vertical) and Navisworks, 2022 to 2027, with C# scripting. Nothing is compiled or downloaded at setup.
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

## Upgrading from 1.2
1. Close Revit, AutoCAD, Navisworks and your AI apps.
2. Run the new Setup.exe over the old installation. Your audit trail, reports and settings are kept.
3. Restart your AI app.

See CHANGELOG.md for the full list.
