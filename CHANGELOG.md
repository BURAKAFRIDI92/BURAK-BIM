# Changelog

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
