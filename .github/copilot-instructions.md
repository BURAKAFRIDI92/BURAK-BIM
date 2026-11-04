# Instructions for GitHub Copilot (BURAK BIM)

BURAK BIM is a C# MCP (Model Context Protocol) server that lets AI assistants control BIM / CAD / structural / takeoff software
(Revit, AutoCAD family, Navisworks 2022-2027, SAP2000, ETABS, CSiBridge, SAFE, Archicad, Bluebeam Revu, PlanSwift, Inventor, ReCap, Robot, Rhino).
Owner: BURAKAFRIDI92. License: Apache-2.0.

## Layout
- `src/BurakBim.Server`        MCP server (stdio JSON-RPC). Tools live in `Tools/`, tool packs in `Packs/*.pack`.
- `src/BurakBim.Bridge.Core`   shared bridge code, compiled into every host bridge.
- `src/BurakBim.Bridge.Revit | Navisworks | AutoCAD`   in-process host add-ins (need the host's API DLLs).
- `src/Shared`                 code linked into server and bridges.
- `installer/`                 `BurakBim.nsi` (NSIS), `install.ps1`, `uninstall.ps1`.
- `build/build-release.ps1`    packages `dist/` (ZIP + Setup.exe). CI calls it with `-NoBuild`.
- `prebuilt/`                  filled by CI: bridges for every product year + shared Roslyn (empty in the repo).
- `installer/mcpb/`            Claude Desktop extension manifest (BURAK-BIM.mcpb is built by build-release.ps1).
- `docs/`, `README.md`, `CHANGELOG.md`, `SECURITY.md`, `AGENTS.md`.

## Build facts
- Server target: `net8.0-windows` (WPF + WinForms). `ImplicitUsings` is enabled.
- Cross-platform compile check without Windows: `dotnet build src/BurakBim.Server/BurakBim.Server.csproj -c Release -p:OfflineCheck=true`
  (targets plain `net8.0`; UI tools compile out). Self-test: `dotnet run --project src/BurakBim.Server/BurakBim.Server.csproj -c Release -p:OfflineCheck=true -- --selftest`
- Real CI (`.github/workflows/build.yml`) runs on `windows-latest`: publish server, `--selftest`, `--mcp-selftest` (every AI-client
  profile), stdio smoke test, PREBUILT bridges (Revit 2022-2027 via Nice3point, AutoCAD 2022-2027 via AutoCAD.NET, Navisworks via
  Chuongmep.Navis.Api), offline csc for the installer, then `build/build-release.ps1` (ZIP, Setup.exe, .mcpb).
- Revit bridge: `net48` for 2022-2024, `net8.0-windows` for 2025-2026, `net10.0-windows` for 2027. AutoCAD: same split (2027 = net10).
- Server dependency: UglyToad.PdfPig 0.1.9 (PDF / Bluebeam markups). Everything else is dependency-free.

## Rules
1. Make the smallest change that fixes the problem. Do not refactor or change tool behaviour unless asked.
2. Never weaken safety controls: `readOnly`, `allowScripts`, `allowComAutomation`, `allowUiAutomation`, the audit log,
   and the localhost-only, token-protected bridge. See `SECURITY.md`.
3. Never commit secrets, tokens, personal paths, or binaries (`*.exe`, `bin/`, `obj/`, `dist/`, `out/`). Installers go to GitHub Releases.
4. When the version changes, update it everywhere: each `.csproj` `<Version>`, `$BurakVersion` in `installer/install.ps1`, and `CHANGELOG.md`.
5. Keep documentation accurate: if a tool is added or removed, update `docs/TOOLS.md`.
6. Before finishing, run the cross-platform compile check and self-test above, and report the results honestly, including anything you could not run.
