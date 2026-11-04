# Test checklist (run before every release / on a new version)

## 1.3 additions (run these first)

### Automated (CI, every push)
- [ ] `--selftest`: ALL TESTS PASSED.
- [ ] `--mcp-selftest`: ALL MCP TESTS PASSED. This covers 14 AI-client profiles, tool limits, unique and valid names, and portable schemas.
- [ ] stdio smoke test: every stdout line is valid JSON-RPC.
- [ ] The prebuilt-bridges summary shows OK for Revit 2022-2027 (required). AutoCAD 2022-2027 and Navisworks are best effort: for a missing year, the installer compiles the bridge on the user's PC.

### Install on a clean PC: no internet, no .NET, normal user
- [ ] Unplug the network, run `BURAK-BIM-Setup-1.3.0.exe`, and check that the log shows *prebuilt bridge installed* for every detected version and *MCP handshake OK*.
- [ ] Extract the ZIP with Explorer, so the files carry the download mark, then run `installer\install.ps1`. Revit 2022-2024 must still load the add-in.
- [ ] `--list-clients` shows every installed AI app as connected. Open each config file: it is valid JSON, has no BOM, and has a `.burakbim.bak` next to it.

### Each Revit version 2022, 2023, 2024, 2025, 2026, 2027
- [ ] The BURAK BIM tab is present, and *Status* shows RUNNING and *Scripting: enabled*.
- [ ] `revit_capture` (active view), `revit_capture view="Level 1"`, a section, a 3D view and a sheet all return images.
- [ ] `revit_create kind=wall ...` returns the result **plus a capture** (capture mode on).
- [ ] `script_run code="return doc.Title;"`.
- [ ] With Dynamo opened first and then `script_run`, there is no MissingMethodException.

### AI apps
- [ ] Claude Desktop: `burak_status` and `revit_capture` work, and the image is shown in the chat.
- [ ] Claude Desktop via `BURAK-BIM.mcpb`: install it, then call `burak_status`.
- [ ] VS Code Copilot with other MCP servers enabled shows no "more than 128 tools" error.
- [ ] Codex, Cursor, Antigravity, Gemini CLI and Windsurf each run `burak_status`, then `burak_tools query="clash"` and `burak_run`.

### Computer control
- [ ] Without permission, `computer_click` is refused with a clear message.
- [ ] `computer_request_control`: the dialog appears on top, Yes shows the red banner, and No reports a decline to the AI.
- [ ] `computer_screenshot grid=true` gives coordinates that `computer_click` hits exactly, at 100 %, 125 % and 150 % scaling and on a second monitor.
- [ ] `computer_type` with non-English text, then Ctrl+Alt+Q: control stops and the next click is refused.

### CSI / Bluebeam / PlanSwift
- [ ] ETABS and SAP2000: `csi_status`, `csi_frames action=list`, `csi_analyze`, `csi_results kind=base_reactions`, `csi_call path=FrameObj.GetSection args=["1"]`.
- [ ] CSiBridge and SAFE: `csi_status`, `csi_call path=GetModelFilename`.
- [ ] Bluebeam: `bluebeam_markups` on a PDF with area, length and count markups, and `bluebeam_takeoff_summary` totals that match Revu's Markups List.
- [ ] PlanSwift: `planswift_status`, `planswift_tree`, `planswift_quantities`.


Run these in order. Record each result as PASS or FAIL, together with the product version.

## 0. Status of v1.0.0

| Part | Status |
|---|---|
| Server, MCP protocol, audit, scan-to-BIM algorithms | Built and self-tested (`--selftest`: levels, 4 walls with 200 mm thickness, 400×400 column, hull) |
| Revit bridge | **Compiles against the real Revit 2024 API**. Live test in Revit 2024: pending |
| AutoCAD-family bridge | Syntax-checked. Compiled by the installer against your installed AutoCAD; live test pending |
| Navisworks bridge | Syntax-checked. Compiled by the installer against your installed Navisworks; live test pending |
| Inventor / Robot (COM) | Live test pending |
| UI automation and screenshots (Windows build) | Live test pending |

## 1. Install
- [ ] Run `BURAK-BIM-Setup-x.y.z.exe` (or `install.ps1`) with Revit, AutoCAD and Navisworks closed.
- [ ] Confirm `D:\BURAK BIM\server\BurakBim.Server.exe` exists.
- [ ] Confirm `D:\BURAK BIM\bridges\revit\2024\BurakBim.Bridge.Revit.dll` exists, and the same for each other detected version.
- [ ] Check that `logs\install-*.log` contains no errors.
- [ ] Run `BurakBim.Server.exe --doctor`: it lists your products, and each one shows `bridge=installed`.
- [ ] Run `BurakBim.Server.exe --selftest`: it prints ALL TESTS PASSED.

## 2. Revit 2024
- [ ] Revit starts with no add-in error, and the **BURAK BIM** tab is present. *Status* shows RUNNING.
- [ ] In the AI client, run `burak_status`. It lists the revit-2024 bridge with ping OK.
- [ ] Run `revit_info`, `revit_levels`, `revit_query category=Walls` and `revit_views`.
- [ ] Run `revit_create kind=wall start=[0,0,0] end=[5000,0,0] height=3000`. The wall appears, one Ctrl+Z removes it, and `inspector_list` shows it.
- [ ] Run `revit_create kind=floor boundary=[[0,0],[5000,0],[5000,4000],[0,4000]]`.
- [ ] Run `revit_set_parameters` (Comments) on that wall.
- [ ] Run `ribbon_list app=revit tab=Analyze`. It lists the buttons.
- [ ] Run `ribbon_invoke item="Manage > Settings > Project Units"`. The dialog opens. Run `ui_keys keys={ESC}`.
- [ ] Run `revit_command command=SectionBox` (in a 3D view), and `revit_commands search=Rebar`.
- [ ] Run `api_get path=doc.ProjectInformation.Name`, `api_inspect type=Autodesk.Revit.DB.Wall`, and `api_call target=type:Autodesk.Revit.DB.XYZ method=new args=[1,2,3]`.
- [ ] Run `script_run code="return new FilteredElementCollector(doc).OfClass(typeof(Wall)).GetElementCount();"`.
- [ ] Run `revit_export format=ifc` and `format=pdf`.
- [ ] Check inspector actions: *AI Review View* (red), *Verify Selected* (green), and `burak_inspection_report` opens as HTML.
- [ ] Point cloud: run `scan_link_pointcloud` (rcp), `scan_detect_levels`, `scan_to_bim dryRun=true` (check the SVG preview), then `dryRun=false`, then `scan_deviation`.
- [ ] Busy test: open a Revit dialog, then call `revit_info`. You should get the "application is busy" message after the timeout, and nothing should run late.

## 3. Navisworks Manage 2024
- [ ] Navisworks starts, and `burak_status` shows navisworks-2024.
- [ ] Run `navisworks_file op=append paths=[...nwc/rvt]`, then `navisworks_info` and `navisworks_tree`.
- [ ] Run `navisworks_search category=Item property=Layer value=...`, then `navisworks_properties`.
- [ ] Run `navisworks_visibility op=isolate`, then `op=unhide_all`.
- [ ] Run `navisworks_clash_create` (Walls vs Pipes), `navisworks_clash_tests` and `navisworks_clash_results`.
- [ ] Run `navisworks_clash_update status=Reviewed`.
- [ ] Run `navisworks_viewpoints op=save name=Test`, then `op=list`.
- [ ] Run `ribbon_list app=navisworks`, then `ribbon_invoke` on Clash Detective.

## 4. AutoCAD / Civil 3D
- [ ] With no security prompt, the command line shows "BURAK BIM connector running". `BURAKBIM` prints the status.
- [ ] Run `autocad_draw kind=rectangle corner1=[0,0] corner2=[1000,500] layer=TEST`, then `autocad_query types=[LWPOLYLINE]`.
- [ ] Run `autocad_command command="_.ZOOM _E "`, then `autocad_lisp expr="(getvar \"DWGNAME\")"`.
- [ ] In Civil 3D, run `civil3d_objects kind=alignments`.

## 5. Inventor (COM)
- [ ] Run `inventor_info launch=true`, `inventor_documents op=new kind=part` and `inventor_commands search=Extrude`.
- [ ] Run `inventor_parameters`, then `com_members progId=Inventor.Application path=ActiveDocument`.

## 6. Clients
- [ ] Claude Desktop, Claude Code, Codex and Antigravity each list the BURAK-BIM tools, and `burak_status` works in each.
