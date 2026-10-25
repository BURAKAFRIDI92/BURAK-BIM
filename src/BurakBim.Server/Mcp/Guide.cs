using BurakBim.Shared;

namespace BurakBim.Server.Mcp;

public static class Guide
{
    public static string ServerInstructions() => $@"BURAK BIM {BurakInfo.Version} connects you to Autodesk software on this PC: Revit, AutoCAD and every AutoCAD-based product (Civil 3D, Plant 3D, Map 3D, MEP, Architecture, Electrical, Advance Steel), Navisworks Manage/Simulate, Inventor, ReCap Pro, Robot Structural Analysis (2022-2027), plus Graphisoft Archicad, Rhino + Grasshopper and Dynamo. BURAK-BIM - Building a Smarter Tomorrow.

How to work:
1. Start with burak_status: what is installed, what is running, which bridges are live.
2. Prefer the specific tools (revit_*, navis_*/navisworks_*, autocad_*, archicad_*, robot_*, rhino_*, inventor_*, scan_*, inspector_*). There are about 1000; if the one you need is not in your tool list, search with burak_tools (keywords, e.g. 'room finishes' or 'clash report') and run it with burak_run.
3. Anything without a specific tool is still reachable, in this order:
   a. ribbon_list + ribbon_invoke - press ANY ribbon button on ANY tab (Architecture, Structure, Steel, Systems, Insert, Annotate, Analyze, Massing & Site, Collaborate, View, Manage, Add-Ins, Modify, contextual and third-party tabs).
   b. revit_command / autocad_command / inventor_command - run any native command by name.
   c. api_inspect / api_get / api_call / api_set - the full .NET API of Revit, AutoCAD, Civil 3D and Navisworks by reflection.
   d. script_run - C# against the live API (one undo step).
   e. com_* - full COM API (Inventor, AutoCAD, Robot ...).
   f. ui_* + screenshot - drive any dialog/window (also ReCap) and look at the result.
4. Revit units on the wire are millimetres. AutoCAD uses drawing units. Navisworks uses model units.
5. Every change made through BURAK BIM is one undo step, stamped as AI-created (who/when/tool) and logged in the audit trail. BIM inspectors review with inspector_review_view, inspector_mark and burak_inspection_report. Never claim LOD 500 without an inspector's verified sign-off.
6. Interactive commands (picks, dialogs) start when the call returns: finish them with ui_* tools or ask the user.
7. Scan-to-BIM: scan_link_pointcloud -> scan_to_bim (dryRun first, then create) -> scan_deviation -> inspector review.";

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
        _ => ServerInstructions() + "\n\nTopics: scan, inspector, ribbon, api."
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
