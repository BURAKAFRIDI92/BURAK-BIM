# Test checklist (run before every release / on a new version)

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
