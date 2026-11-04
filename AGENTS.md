<!-- mcp-name: BURAK-BIM -->
# AGENTS.md: installing and using BURAK BIM (for AI agents)

This guide is for an AI agent that can run PowerShell on the user's Windows PC, such as Claude Code, Codex or Antigravity. If you cannot execute commands, give the user these steps instead.

## Install
1. Check the prerequisites:
   - Windows x64 (`$env:OS -eq 'Windows_NT'`).
   - At least one supported program installed (Revit, AutoCAD/Civil 3D, Navisworks, SAP2000, ETABS, CSiBridge, SAFE, Archicad, Bluebeam, PlanSwift, ...).
   - No internet, .NET or admin rights are needed (1.3 ships prebuilt binaries).
2. Ask the user to **close Revit, AutoCAD and Navisworks**.
3. Preview first: `powershell -ExecutionPolicy Bypass -File installer\install.ps1 -WhatIf`. Show the output to the user.
4. Install. The default folder is `D:\BURAK BIM`. The user can pick another with `-InstallDir "E:\BURAK BIM"`. The client list is optional:
   `powershell -ExecutionPolicy Bypass -File installer\install.ps1 -Clients claude-desktop,claude-code,codex,antigravity`
   - `-Clients auto` (the default) configures every client it detects.
   - `-Clients none` leaves client configs untouched.
   - Every client config edit keeps a `.bak` backup.
5. Verify:
   - `"D:\BURAK BIM\server\BurakBim.Server.exe" --doctor` lists the products, the installed bridges and the connected AI apps.
   - `--selftest` must print ALL TESTS PASSED, and `--mcp-selftest` must print ALL MCP TESTS PASSED.
   - Connect AI apps installed later with `--connect-clients`.
6. Ask the user to restart the target AI client, then call `burak_status` in that client.

## Using the tools (summary)
- Always call `burak_status` first.
- There are about 1,000 tools. If the one you need is not in your list, search with `burak_tools` and run it with `burak_run`.
- Software installed in unusual folders: run `burak_rescan` with `searchPaths` (e.g. `["D:\\ALL PROGRAMS"]`).
- Revit coordinates and lengths are **millimetres**. CSI tools use the model's present units (`csi_units`).
- **Look before and after** you change things: `revit_capture` (any Revit view), `capture` (any window or dialog). Capture mode adds a picture to every change.
- Computer control is the **last resort**. First explain to the user what you will do, then call `computer_request_control` with a reason; the user approves on screen. Check every step with the returned screenshot, and call `computer_release_control` when done. Never type passwords and never accept licences or purchases.
- For anything without a dedicated tool, work through these in order:
  1. `ribbon_list` / `ribbon_invoke`
  2. `revit_command` / `autocad_command` / `inventor_command`
  3. `api_inspect` / `api_get` / `api_call`
  4. `script_run`
  5. `com_*` / `csi_call` / `planswift_call`
  6. `ui_*` + `capture`
  7. `computer_*`, with the user's approval
- **Scan-to-BIM:** always run `scan_to_bim dryRun=true` first and show the plan to the user.
- **LOD 500:** never claim LOD 500 unless an inspector has verified the elements with `inspector_mark`.
- **Interactive commands:** these start when your call returns. Finish them with `ui_*`, check the result with `screenshot`, or ask the user.
- **Read-only mode:** if the result says READ-ONLY, do not try to work around it.

## Uninstall
`powershell -ExecutionPolicy Bypass -File "D:\BURAK BIM\setup\uninstall.ps1"`

This keeps the audit trail and reports. Add `-Purge` to remove everything, and only with the user's explicit consent.
