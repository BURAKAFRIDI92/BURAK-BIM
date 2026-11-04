using BurakBim.Shared;

namespace BurakBim.Server.Mcp;

public static class Guide
{
    public static string ServerInstructions() => $@"BURAK BIM {BurakInfo.Version} connects you to the BIM, CAD, structural and takeoff software on this PC:
Autodesk Revit, AutoCAD and every AutoCAD-based product (Civil 3D, Plant 3D, Map 3D, MEP, Architecture, Electrical, Advance Steel), Navisworks Manage/Simulate (2022-2027), Inventor, ReCap Pro, Robot;
CSI SAP2000, ETABS, CSiBridge, SAFE; Graphisoft Archicad; Bluebeam Revu; PlanSwift; Rhino + Grasshopper; Dynamo. BURAK-BIM - Building a Smarter Tomorrow.

How to work (like a careful BIM engineer):
1. Start with burak_status: what is installed, what is running, which bridges are live. Then, for any multi-step task, read the matching
   professional method with burak_skills (task: what you are about to do) - e.g. revit-model-health, revit-documentation, revit-mep-modeling, bim-coordination,
   csi-structural-analysis, bluebeam-takeoff - and follow it: counts before detail, focused queries, bulk changes, preview + user approval, verify.
2. LOOK before and after you change things: revit_capture renders any Revit view (plans, sections, elevations, 3D, sheets); capture shows any application window or dialog. Capture mode adds a picture to every change automatically (capture_mode to switch off).
3. Prefer the specific tools (revit_*, autocad_*, navisworks_*, csi_*, archicad_*, bluebeam_*, planswift_*, robot_*, rhino_*, inventor_*, scan_*, inspector_*). If a tool you need is not in your list, find it with burak_tools and run it with burak_run (about 1100 tools exist).
4. Anything without a specific tool is still reachable, in this order:
   a. ribbon_list + ribbon_invoke - any ribbon button of Revit, AutoCAD/Civil 3D, Navisworks (built-in, contextual and add-in tabs).
   b. revit_command / autocad_command / inventor_command - any native command.
   c. api_inspect / api_get / api_call / api_set / script_run - the full .NET API (Revit, AutoCAD, Civil 3D, Navisworks); csi_call - every SAP2000/ETABS/CSiBridge/SAFE API function; com_* - any COM API; planswift_call.
   d. ui_* - drive dialogs through Windows UI Automation.
   e. LAST RESORT computer control: computer_request_control (the user approves on screen) -> computer_screenshot / computer_click / computer_type / computer_key / computer_drag -> computer_release_control. Tell the user what you will do first, check every step with the returned screenshot, and stop if something unexpected appears.
5. Units: Revit tools use millimetres. AutoCAD uses drawing units. CSI tools use the model's present units (csi_units). Navisworks uses model units.
6. Accuracy: read back what you created (ids, parameters, results) and confirm it visually. Never guess element ids, section names or load case names - list them first.
7. Every change through BURAK BIM is one undo step, stamped as AI-created and logged in the audit trail. Never claim LOD 500 without an inspector's verified sign-off.
8. Read-only mode (config) blocks all changes - do not try to work around it.";

    public static string Topic(string topic) => topic.ToLowerInvariant() switch
    {
        "scan" or "scan-to-bim" => @"SCAN-TO-BIM WORKFLOW (Revit + ReCap point clouds)
1. scan_pointclouds - is a scan linked? If not: scan_link_pointcloud path=...\project.rcp (or .rcs).
2. scan_detect_levels - horizontal planes (floors/slab undersides) -> suggested level elevations.
3. scan_to_bim dryRun=true - per level: wall faces by RANSAC, paired into walls (centreline+thickness), columns, slab outlines. Review the plan (confidence, single-face walls).
4. scan_to_bim dryRun=false wallType=... columnType=... - creates levels, walls, columns, floors (one undo step per level), every element stamped AI/unverified.
5. scan_deviation ids=[...] toleranceMm=25 - p95 distance of scan points to each element's faces; stored on the element.
6. inspector_review_view - red = unverified, purple = out of tolerance, green = verified. Inspectors fix and inspector_mark verified (with lod).
LOD: automated output is geometry at LOD 300-350. LOD 500 = field-verified as-built: only inspector-verified elements within tolerance qualify.",
        "inspector" => @"BIM INSPECTOR WORKFLOW
- inspector_list: all AI-created elements with status and provenance (AI client, session, tool, user, time).
- inspector_review_view: color-coded 3D view (red unverified, green verified, orange rejected, purple out-of-scan-tolerance).
- inspector_mark ids=[...] status=verified|rejected inspector=""Name"" note=""..."" lod=""400"".
- burak_inspection_report: HTML report (summary, every AI element, deviations, audit trail) in the reports folder.
- burak_audit_log: every AI action with arguments, result and duration.
Revit ribbon: BURAK BIM tab > BIM Inspector > AI Review / Verify Selected / Reject Selected.",
        "ribbon" or "commands" => @"EVERY COMMAND / EVERY TAB
- ribbon_list app=revit tab=""Analyze"" -> every button (id, path, enabled).
- ribbon_invoke app=revit item=""Analyze > Energy Optimization > Create Energy Model"" (or the id).
- revit_commands search=""Section"" -> PostableCommand names; revit_command command=""Section"".
- AutoCAD/Civil 3D: autocad_command command=""_.CORRIDOR "" or any macro; ribbon_invoke app=autocad item=...
- Inventor: inventor_commands search=""Extrude""; inventor_command name=""PartExtrudeCmd"".
- Navisworks: ribbon_invoke app=navisworks item=""Clash Detective"".
Commands that need picks/dialogs: continue with ui_tree / ui_click / ui_type, check with screenshot.",
        "api" => @"FULL API ACCESS
- api_types app=revit search=""Rebar"" -> type names.
- api_inspect app=revit type=""Autodesk.Revit.DB.Structure.Rebar"" -> ctors, methods, properties.
- api_get app=revit path=""doc.ActiveView.Name"" | ""doc.ProjectInformation"" -> values + $h handles.
- api_call app=revit target=""type:Autodesk.Revit.DB.XYZ"" method=""new"" args=[0,0,0]
- api_call app=revit target=""$h12"" method=""LookupParameter"" args=[""Comments""]
- script_run app=revit code=""return doc.ActiveView.Name;"" (C#, globals doc/uidoc/uiapp/app; Print(x) for output).
- AutoCAD roots: doc, db, ed, docs, civil. Navisworks roots: doc, clash, timeliner, selection.",
        "capture" => @"CAPTURE MODE - SEE THE MODEL
- revit_capture: active view, or view=<id|name>, views=[...] (max 8), allOpenViews=true; pixelSize up to 6000. Sheets by number. Returns images + facts (scale, level, element counts).
- revit_capture_views: list views that can be captured.
- capture app=<revit|autocad|navisworks|etabs|sap2000|csibridge|safe|archicad|bluebeam|planswift|...> (window=dialog title, grid=true for click coordinates), fullScreen=true, monitor=2, region=[x,y,w,h].
- capture_mode mode=changes|off: automatic picture after every change (default changes, 1024 px).",
        "computer" => @"COMPUTER CONTROL (last resort, needs the user's approval)
1. Explain in the chat what you will do and why no API can.
2. computer_request_control reason=""..."" minutes=15 -> the user clicks Yes on their screen.
3. computer_screenshot (grid=true) -> read SCREEN coordinates from the grid labels / caption mapping.
4. computer_click x y | computer_type text | computer_key keys=""ctrl+s"" | computer_drag from to | computer_scroll. Each returns a new screenshot: verify before the next step.
5. computer_release_control when done. The user can stop anytime with Ctrl+Alt+Q.
Never type passwords, never confirm purchases or licence agreements, never delete files without the user's explicit OK.",
        "csi" or "etabs" or "sap2000" => @"CSI (ETABS / SAP2000 / CSiBridge / SAFE)
- csi_status -> attach, file, units, counts. csi_register_api once if the API is not registered.
- csi_units set=kN_m_C. csi_file new|open|save|saveas. csi_unlock before editing an analysed model.
- csi_points / csi_frames / csi_areas / csi_sections / csi_loads (patterns, combos, point/frame/area loads).
- csi_analyze -> csi_results kind=base_reactions|joint_reactions|joint_displacements|frame_forces|modal_periods|story_drifts -> csi_design type=steel|concrete action=start|summary.
- csi_members path=FrameObj (signatures) and csi_call path=FrameObj.GetLocalAxes args=[""B1""] reach EVERY OAPI function; by-ref outputs come back by name, result tables as rows.",
        "bluebeam" or "planswift" or "takeoff" => @"TAKEOFF (Bluebeam Revu / PlanSwift)
- bluebeam_markups path=...pdf -> every markup with subject, author, page, measurement value/unit, layer, custom columns.
- bluebeam_takeoff_summary -> totals by subject (and page/layer/author). pdf_info / pdf_text read sheets and specs.
- bluebeam_script (Revu eXtreme/Complete): Flatten, Stamp, HeaderAndFooter, PageRotate, Import/Export markups...
- planswift_status / planswift_tree / planswift_quantities / planswift_item / planswift_set / planswift_new_item / planswift_call.
- Drawing takeoffs on the canvas: capture app=bluebeam|planswift grid=true + computer_* (with permission).",
        "clients" => @"CONNECTING AI APPS
- burak_connect_clients action=list|connect|disconnect clients=auto|all|cursor,vscode,...
- Claude Desktop: install BURAK-BIM.mcpb (double-click) or let the installer write claude_desktop_config.json, then fully quit Claude from the tray and restart.
- Apps with tool limits (VS Code/Copilot, Codex/ChatGPT, Cursor, Antigravity, Gemini) get the most useful tools first; all others via burak_tools + burak_run.
- HTTP clients: BurakBim.Server.exe --http 8765 -> http://127.0.0.1:8765/mcp",
        _ => ServerInstructions() + "\n\nTopics: capture, computer, csi, bluebeam, planswift, clients, scan, inspector, ribbon, api."
    };
}

public static class Prompts
{
    public static List<object> List() => new()
    {
        Json.O("name", "scan_to_bim", "description", "Model an existing building in Revit from a ReCap point cloud and prepare it for inspection.",
            "arguments", new List<object> { Json.O("name", "scan_path", "description", ".rcp/.rcs path (optional if already linked)", "required", false) }),
        Json.O("name", "clash_coordination", "description", "Run Navisworks clash detection and produce a coordination summary.", "arguments", new List<object>()),
        Json.O("name", "inspection_review", "description", "Review all AI-created elements in the open Revit model and report.", "arguments", new List<object>()),
        Json.O("name", "model_audit", "description", "Audit the open Revit model: warnings, unplaced rooms, naming, AI elements.", "arguments", new List<object>())
    };

    public static object Get(string name, Dictionary<string, object>? args)
    {
        string text = name switch
        {
            "scan_to_bim" => "Use BURAK BIM to do scan-to-BIM in the open Revit model. " +
                (Json.Str(args, "scan_path") is { } sp ? $"Link the scan {sp} if not linked. " : "") +
                "Follow burak_guide topic=scan: detect levels, run scan_to_bim with dryRun=true, show me the plan, then create, run scan_deviation and open inspector_review_view. Report what needs an inspector.",
            "clash_coordination" => "Use BURAK BIM with Navisworks: list clash tests, run all, summarize results by test and status, list the top 20 new clashes with both items and locations, and save a viewpoint for the worst ones.",
            "inspection_review" => "Use BURAK BIM: inspector_list, inspector_review_view, then burak_inspection_report. Summarize unverified/rejected/out-of-tolerance items for the BIM inspectors.",
            "model_audit" => "Use BURAK BIM on the open Revit model: revit_info, revit_warnings (group by type), rooms without area, views not on sheets, AI-created elements still unverified. Produce a short audit with fixes.",
            _ => throw new Exception("Unknown prompt " + name)
        };
        return Json.O("description", name, "messages", new List<object> { Json.O("role", "user", "content", Json.O("type", "text", "text", text)) });
    }
}
