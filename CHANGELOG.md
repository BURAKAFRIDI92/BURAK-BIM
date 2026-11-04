# Changelog

## 1.3.0 (2026-11-04) - works everywhere, sees the model, reaches every command

### Installer ZIP and fixes
- **`BURAK-BIM-Installer-1.3.0-win-x64.zip`** is the download for most users: `BURAK-BIM-Installer.exe`, `BURAK-BIM-Uninstaller.exe`, a short guide, the Claude Desktop extension and SHA-256 checksums.
- **Standalone uninstaller.** It finds the install folder from the registry and runs the uninstaller of the installed version, so it also removes older versions.
- **Upgrades remove the old logon task.** The "BURAK BIM Sentinel" task from older versions ran hidden PowerShell at logon. 1.3 no longer creates it, and both install and uninstall now delete it.
- **`bluebeam_script` works in folders with spaces**, including the default `D:\BURAK BIM`. The script path was passed without quotes.
- **Setup honours `/D=<folder>`** and never waits on a message box during a silent install.
- **CI now installs and uninstalls the ZIP** on a clean Windows machine, and fails if a skill names a tool that does not exist.

### BIM workflow skills: the method, not just the tools
- There are **28 professional workflow skills**. New for BIM management and VDC: `bim-vdc-router`, `bim-project-setup`, `bim-qa-gate`, `bim-handover`, `vdc-4d-scheduling`, `vdc-5d-quantities`, `drawing-review-cycle`, `rhino-grasshopper-computational`, plus each the step-by-step method an experienced BIM/VDC engineer follows:
  - **Base methods:** `bim-base`, `revit-base`, `computer-control`.
  - **Revit:** `revit-model-health`, `revit-documentation`, `revit-document-control-iso19650`, `revit-architecture-modeling`, `revit-structure-modeling`, `revit-mep-modeling` (HVAC, plumbing/piping, electrical), `revit-scan-to-bim`, `revit-family-creation`.
  - **Coordination:** `bim-coordination` (Navisworks clash cycle).
  - **Structural analysis:** `csi-structural-analysis` (ETABS/SAP2000).
  - **Takeoff:** `bluebeam-takeoff`, `planswift-takeoff`.
  - **Other applications:** `autocad-civil3d-base`, `autocad-cut-files`, `civil3d-site`, `archicad-modeling`.
- Each skill applies the same rules: counts before detail, focused queries, bulk changes, a preview with user approval, and verification in the application.
- Skills reach **every** AI app in three ways: the `burak_skills` tool (list, get, or suggest for a task), MCP **prompts** (`skill-<name>`) and MCP **resources** (`burak://skills/<name>`).
- The installer also copies the skills into Claude Code (`~/.claude/skills`).
- Company-specific skills: drop `SKILL.md` folders into `<install>\skills`.

### Installs cleanly: no antivirus alarms
- Setup.exe now runs `BurakBim.Server.exe --install`, which is plain C#. It uses no PowerShell, no hidden windows and no "ExecutionPolicy Bypass", installs no logon scheduled task, downloads nothing and needs no admin rights.
- The server ships as a normal self-contained **folder**, instead of a self-extracting single file that unpacked itself into %TEMP%.
- Full version information and an application manifest (asInvoker, per-monitor DPI, long paths) are included.
- **Code signing is built into the release pipeline.** Add a certificate as GitHub secrets and every exe, DLL and Setup.exe is signed. This removes the SmartScreen "unknown publisher" warning.
- The release ZIP now contains `BURAK-BIM-Setup-1.3.0.exe`, `BURAK-BIM.mcpb` and the full package.
- New commands: `--install`, `--rescan`, `--uninstall`. Start menu: *Repair - detect new software*.

### Reliability: the "it doesn't work in my Revit version / it doesn't connect" fixes
- **Prebuilt bridges for every version.** Revit 2022-2027, AutoCAD and all AutoCAD-based products 2022-2027, and Navisworks are compiled in CI against each year's own API, with C# scripting included. The installer no longer downloads a .NET SDK or compiles anything on the user's PC.
  - 1.2 needed internet access to dot.net and nuget.org during setup, so company firewalls made it fall back to a Revit-2024-only bridge and a server that required a separate .NET runtime.
- **Self-contained server.** `BurakBim.Server.exe` runs on any Windows 10/11 x64 PC with no .NET installed.
- **Offline fallback compiler.** A version without a prebuilt bridge, for example a future service pack, is compiled on the PC with the bundled Roslyn compiler (`tools\roslyn`) against the installed product's DLLs. No internet is needed.
- **Download marks removed.** Files extracted from a downloaded ZIP are blocked by Windows, and .NET Framework hosts (Revit 2022-2024, AutoCAD 2022-2024, Navisworks) refused to load them (0x80131515). The installer now unblocks everything it installs.
- **Revit add-in starts the connector before building the ribbon.** A ribbon problem (tab-name clash with another add-in, localized Revit) can no longer leave the AI without a connection.
- **Version-aware dependency loading.** If another add-in (Dynamo, pyRevit...) already loaded an older Roslyn, BURAK BIM loads its own copy instead of failing.
- **AutoCAD 2027 bridge targets .NET 10.** AutoCAD 2025-2026 stays on .NET 8, which also loads in AutoCAD 2026 Update 1.2 and later.

### AI apps: Claude, ChatGPT/Codex, Antigravity, Cursor, VS Code and more
- **Per-client compatibility profiles.** VS Code/Copilot and OpenAI-based clients reject more than 128 tools, Cursor and Windsurf cap active tools, and Gemini/Antigravity reject some JSON-schema keywords. Each client now gets a tool list and schemas it accepts: running applications' tools come first, and everything else stays reachable through `burak_tools` + `burak_run`.
- **Schema sanitizer.** It inlines `$ref`, flattens `oneOf`/`anyOf`/`allOf` and type arrays, and in strict clients removes `additionalProperties`, `format` and `default`.
- **Protocol cleanup.** stdout is reserved for the protocol; stray output goes to stderr. The server supports MCP 2025-11-25, JSON-RPC batches and `completion/complete`.
- **Faster start.** Software detection is cached on disk, so `tools/list` answers instantly.
- **`--connect-clients`.** The server exe itself connects Claude Desktop (classic + Microsoft Store), Claude Code, Codex (CLI / IDE / ChatGPT desktop Codex), Cursor, Windsurf, Google Antigravity, Gemini CLI, VS Code (+ Insiders), Visual Studio, Cline, Roo Code, Kilo Code, LM Studio, Kiro, Amazon Q, Trae, Continue, opencode and Zed. It writes strict JSON without a BOM, verifies each file and keeps `*.burakbim.bak` backups. Also available as `--disconnect-clients`, `--list-clients` and the tool `burak_connect_clients`.
- **Claude Desktop extension.** `BURAK-BIM.mcpb` installs with a double-click.
- **Local Streamable HTTP transport.** `--http [port]` serves clients that cannot start a process (LM Studio, Open WebUI/mcpo, n8n, custom agents).
- **The installer checks the real MCP handshake**, the same one every AI client performs.
- `--mcp-selftest` checks every client profile, tool limit and schema in CI.

### Capture mode: the AI sees what is going on
- `revit_capture` renders any Revit view (floor/ceiling plans, sections, elevations, 3D, drafting views, legends, sheets) through the API. It works even when the view is not open or the window is covered, and returns view facts: scale, detail level, level and element counts.
- `revit_capture_views` lists the views that can be captured.
- `capture` takes a picture of any application window or dialog (Revit, AutoCAD/Civil 3D, Navisworks, ETABS, SAP2000, CSiBridge, SAFE, Archicad, Bluebeam, PlanSwift, Excel...), a monitor, the whole desktop or a region. It uses PrintWindow, so covered windows work, and has an optional screen-coordinate grid.
- `capture_mode` is on by default: every change returns a picture of the result.

### Computer control with the user's permission
- `computer_request_control` shows a Yes/No dialog on the user's screen with the AI's reason. Permission is time-limited.
- After approval: `computer_screenshot`, `computer_click`, `computer_type` (exact Unicode, independent of keyboard layout), `computer_key`, `computer_drag`, `computer_scroll`, `computer_move`, `computer_focus`, `computer_windows`, `computer_open`, `computer_wait` and `computer_release_control`. Each action returns a screenshot.
- A red banner shows while control is active, and **Ctrl+Alt+Q** stops it instantly.
- The process is per-monitor DPI-aware, so clicks land on the exact pixel on scaled displays and multi-monitor setups.
- `"allowComputerControl": false` disables it for the whole company.
- When a tool fails because no API exists, the error tells the agent how to fall back to computer control.

### New connectors
- **CSI SAP2000 / ETABS / CSiBridge / SAFE** (OAPI v1, any version): `csi_status`, `csi_units`, `csi_file`, `csi_unlock`, `csi_points`, `csi_frames`, `csi_areas`, `csi_sections`, `csi_loads`, `csi_analyze`, `csi_results` (reactions, displacements, frame/area forces, modal, story drifts/forces), `csi_design`, `csi_select`, `csi_register_api`, `csi_members`, and `csi_call`. `csi_call` reaches every OAPI function: by-reference outputs are created and returned automatically from the type library.
- **Bluebeam Revu** (every edition): `bluebeam_status`, `bluebeam_open`, `bluebeam_markups` (subject, author, page, measurement value and unit, layer, status, custom columns, read straight from the PDF), `bluebeam_takeoff_summary`, and `bluebeam_script` (ScriptEngine, eXtreme/Complete).
- **PDF tools:** `pdf_info` and `pdf_text`.
- **PlanSwift** (COM): `planswift_status`, `planswift_tree`, `planswift_quantities`, `planswift_item`, `planswift_set`, `planswift_new_item` and `planswift_call`.
- **Detection** of SAP2000, ETABS, CSiBridge, SAFE, Bluebeam Revu, PlanSwift, Archicad (any folder), Tekla Structures and SketchUp.
- **`ComTyped`:** type-library-aware COM calls with by-reference parameters and enum names, for any COM application.

## 1.2.0 (2026-10-21) - about 1,000 tools, company logo, installs anywhere

- **Company logo everywhere:** the BURAK-BIM logo is now the MCP server icon shown by AI clients, the Revit ribbon button images, the Setup / uninstaller icon, the installer pages, the Apps & Features icon, the header of the HTML inspection reports and the README.
- **Software installed in custom folders (e.g. Navisworks Manage 2027 under `D:\ALL PROGRAMS`)** is now detected. The version is read from each program file, and the installer and server also look in the uninstall icons, App Paths, every path under the Autodesk registry keys, Start-menu shortcuts and running programs. New `-SearchPaths` installer option, `extraSearchPaths` setting, and `searchPaths` on `burak_rescan`.
- **Archicad: 406 tools.** One tool per official Archicad JSON API command (AC24-AC30) and per Tapir add-on command, generated from the publishers' own JSON schemas.
- **AutoCAD and every vertical: +38 COM tools.** Drawings, selection filters, editing, 2D/3D creation, dimensions, hatches, tables, blocks and attribute extraction, xrefs, images, text find & replace, layer reports and standards, styles, layouts, batch PDF plotting, export / wblock, drawing properties, quantities by layer, named views, groups, plus Civil 3D surfaces / alignments / COGO points.
- **Revit: 332 tools** (+19): worksharing (reload latest, relinquish), conduit and cable-tray routing, placing instances at points, areas and area boundaries, parts, dependent views, IFC GUID search, copying from links, floor openings, loading a family folder, exporting all schedules, QA checks (view templates, in-place families, fake dimensions), grid bubbles, rooms from linked models.
- **Navisworks: 113 tools** (+14): appearance profiles, visibility by property, comparing model revisions, proximity search, item distances, plan cuts, issue viewpoints, clash triage (select items, auto-approve small clashes, rename by level/grid), TimeLiner CSV import/export, search sets per level, moving a model.
- **Inventor +13, Robot +3, Rhino +5** COM tools.
- **`burak_tools` and `burak_run`**: search all tools and run any tool by name. Clients with small context windows get a compact list automatically (`"exposure"` setting).
- AutoCAD COM calls retry automatically while AutoCAD is busy.

## 1.1.0 (2026-10-21) - about 500 tools, more software

- **Revit: 313 tools** (was 25). Nine embedded tool packs: modelling, architecture, structure, MEP, views, documentation, manage, analysis/QA, export, extras (element history, door/room QA, sheets and revisions, view graphics and phases, global parameters, scope boxes, point clouds, lines-to-walls).
- **Navisworks: 99 tools** (was 14). Packs for model/selection/appearance, coordination (viewpoints, sections, sets, clash matrix/grouping/assignment/reports, TimeLiner), and QA/data (missing properties, duplicates, alignment, CSV/JSON export, grids and levels, exporters, plug-ins).
- **New connectors:** Robot Structural Analysis (12 tools via RobotOM), Rhino 7/8 + Grasshopper (8 tools), Archicad 24+ JSON API + Tapir (12 tools), Dynamo graphs (inspect, set inputs, headless DynamoCLI).
- `com_constants`: read enum constants from any COM application's type library.
- Detection now finds Rhino, Archicad and Dynamo Core on every drive.
- Tool packs: user `*.pack` files in `<install>\toolpacks` add tools without recompiling. Compiled scripts are cached.
- Script helpers (`S`, `N`, `I`, `B`, `L`, `SL`, `A`, `R`, `Fail`, plus Revit/Navisworks helpers) for short, safe snippets.

## 1.0.1 (2026-10-21) - fixes from the first live test in Revit 2024
- Installer: builds run through Start-Process, so stderr output from dotnet can no longer abort the build in Windows PowerShell 5.1 (the most likely reason 1.0.0 fell back to the limited prebuilt binaries: no C# scripting, no UI tools or screenshots). Failed builds now show the tail of the build log.
- Installer: detects Autodesk products installed in custom folders via the Uninstall registry (e.g. D:\ALL PROGRAMS\NAVISWORK\Autodesk\Navisworks Manage 2027).
- revit_create wall: when no type is given, the default is now a Basic wall instead of a Curtain Wall.
- createdIds lists only real elements, not sketch lines or sketch planes.
- Element parameters: duplicates are removed, and category-type ElementIds show the category name.
- ribbon_invoke: toggles and check boxes that Revit cannot post now fall back to the button's own handler.
- revit_command: suggests the closest names, e.g. DefaultThreeDView -> Default3DView.
- Ribbon paths: no more empty segments, and repeated spaces are collapsed.

## 1.0.0 (2026-10-21)
The first release: one connector for the Autodesk AEC stack.

- **Single MCP server**, `BurakBim.Server.exe`:
  - Zero dependencies.
  - Supports MCP protocols 2024-11-05, 2025-03-26 and 2025-06-18.
  - About 96 tools and 4 prompts.
  - Toolset filtering (`auto` = tools for installed software only).
- **Detection** of Revit, AutoCAD and every AutoCAD vertical (Civil 3D, Plant 3D, Map 3D, MEP, Architecture, Electrical, Mechanical, Advance Steel), Navisworks Manage and Simulate, Inventor, ReCap Pro and Robot, on any drive.
- **Bridges** for Revit, the AutoCAD family and Navisworks, 2022–2027. Each is built on the user's machine against the installed version.
- **Universal access layers:**
  - every ribbon button (`ribbon_list` / `ribbon_invoke`);
  - every native command;
  - the full .NET API by reflection (`api_*`);
  - Roslyn C# scripting;
  - COM for any ProgID;
  - Windows UI Automation and screenshots.
- **Scan-to-BIM:**
  - level detection;
  - RANSAC wall-face extraction and pairing into walls (thickness, orthogonal snapping, join cleanup);
  - column detection;
  - slab outline;
  - creation in Revit with one undo step per storey;
  - scan deviation QA;
  - SVG plan previews.
- **BIM Inspector workflow:**
  - provenance stamps (Revit Extensible Storage, AutoCAD XData);
  - AI Review 3D view;
  - Verify and Reject ribbon buttons;
  - `inspector_*` tools;
  - HTML inspection report;
  - JSONL audit trail.
- **Governance:** read-only mode, per-layer switches, loopback-only bridges with tokens, and timed-out requests cancelled rather than run late.
- **Installer:**
  - Setup.exe, a per-user install with `D:\BURAK BIM` as the default folder;
  - private .NET SDK and caches on the install drive;
  - autoloader bundles;
  - client wiring for Claude Desktop, Claude Code, Codex, Cursor, Windsurf, Antigravity, Gemini CLI and VS Code;
  - the logon Sentinel that connects newly installed versions;
  - an uninstaller that keeps inspector records.
