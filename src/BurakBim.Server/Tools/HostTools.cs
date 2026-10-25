using BurakBim.Server.Bridges;
using BurakBim.Server.Core;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>Tools served by the in-process host bridges (Revit, AutoCAD family, Navisworks).</summary>
public static class HostTools
{
    static readonly string[] BridgeApps = { "revit", "autocad", "navisworks" };

    static string App(Dictionary<string, object> a)
    {
        var app = (Json.Str(a, "app") ?? "revit").ToLowerInvariant();
        app = app switch { "civil3d" or "civil 3d" or "acad" or "plant3d" or "advancesteel" => "autocad", "navis" or "nwd" => "navisworks", "rvt" => "revit", _ => app };
        if (!BridgeApps.Contains(app)) throw new ToolException("app must be revit, autocad (incl. Civil 3D and other AutoCAD verticals) or navisworks. Inventor/Robot: use com_* or inventor_* tools.");
        return app;
    }

    static Dictionary<string, object> Strip(Dictionary<string, object> a, params string[] keys)
    {
        var d = new Dictionary<string, object>(a);
        foreach (var k in keys.Concat(new[] { "app" })) d.Remove(k);
        return d;
    }

    /// <summary>Register a tool that forwards its arguments to one bridge method.</summary>
    public static void Fwd(ToolRegistry r, string name, string title, string desc, string toolset, string host, string method,
        Dictionary<string, object> schema, bool readOnly, bool destructive = false, int? timeoutMs = null)
    {
        r.Add(new Tool
        {
            Name = name, Title = title, Description = desc, Toolset = toolset, InputSchema = schema,
            ReadOnly = readOnly, Destructive = destructive,
            Handler = (a, ctx) => BridgeClient.Call(host, Json.Str(a, "target"), method, Strip(a, "target"), ctx, timeoutMs)
        });
    }

    public static void Register(ToolRegistry r)
    {
        var appParam = O("app", Enum("Application", "revit", "autocad", "navisworks"));

        // ---------------- universal: ribbon, API, scripting ----------------
        r.Add(new Tool
        {
            Name = "ribbon_list", Title = "List every ribbon tab, panel and button", Toolset = "api", ReadOnly = true,
            Description = "Lists EVERY ribbon item (built-in, contextual and third-party add-in tabs: Architecture, Structure, Steel, Precast, Systems, Insert, Annotate, Analyze, Massing & Site, Collaborate, View, Manage, Add-Ins, Modify...) of Revit, AutoCAD/Civil 3D or Navisworks. Filter with tab and search. Use the id or path with ribbon_invoke.",
            InputSchema = Obj(appParam, O("tab", Str("Tab title, e.g. 'Analyze', 'Manage', 'Add-Ins'")), O("search", Str("Text to find in path/id/tooltip")),
                O("includeHidden", Bool("Include hidden/contextual items")), O("limit", Int("Max items (default 400)")), Target),
            Handler = (a, ctx) => BridgeClient.Call(App(a), Json.Str(a, "target"), "ribbon.list", Strip(a, "target"), ctx)
        });
        r.Add(new Tool
        {
            Name = "ribbon_invoke", Title = "Press any ribbon button", Toolset = "api", ReadOnly = false,
            Description = "Presses any ribbon button exactly like a user click: by id (from ribbon_list), by path 'Tab > Panel > Button', or by button text. Revit buttons (incl. add-ins) run through Revit's own command system; AutoCAD buttons run their macro.",
            InputSchema = Obj(appParam, R("item", Str("Ribbon item id, 'Tab > Panel > Button' path, or button text")), Target),
            Handler = (a, ctx) => BridgeClient.Call(App(a), Json.Str(a, "target"), "ribbon.invoke", Strip(a, "target"), ctx)
        });
        r.Add(new Tool
        {
            Name = "api_types", Title = "Search API types", Toolset = "api", ReadOnly = true,
            Description = "Search loaded .NET API types by name in Revit / AutoCAD / Civil 3D / Navisworks (e.g. 'Rebar', 'Alignment', 'ClashTest').",
            InputSchema = Obj(appParam, R("search", Str("Part of a type name")), O("limit", Int("Max results")), Target),
            Handler = (a, ctx) => BridgeClient.Call(App(a), Json.Str(a, "target"), "api.types", Strip(a, "target"), ctx)
        });
        r.Add(new Tool
        {
            Name = "api_inspect", Title = "Inspect API object or type", Toolset = "api", ReadOnly = true,
            Description = "Constructors, properties, methods, fields and enum values of an object (path) or a type (full name). Use it to discover the API before api_call/script_run.",
            InputSchema = Obj(appParam, O("path", Str("Object path: doc, doc.ActiveView, $h12 ...")), O("type", Str("Full type name, e.g. Autodesk.Revit.DB.Wall")),
                O("search", Str("Filter member names")), O("limit", Int("Max members per group")), Target),
            Handler = (a, ctx) => BridgeClient.Call(App(a), Json.Str(a, "target"), "api.inspect", Strip(a, "target"), ctx)
        });
        r.Add(new Tool
        {
            Name = "api_get", Title = "Read any API value", Toolset = "api", ReadOnly = true,
            Description = "Evaluate an API path and return the value. Roots - Revit: doc, uidoc, uiapp, app, view, selection. AutoCAD: doc, db, ed, docs, civil. Navisworks: doc, clash, timeliner, selection. Also $h<handle> and type:Full.Type.Name for statics. Path can include [index] and Method() calls without args or with JSON args.",
            InputSchema = Obj(appParam, R("path", Str("e.g. doc.ProjectInformation.Name, doc.ActiveView.Scale, type:Autodesk.Revit.DB.BuiltInCategory")), O("maxItems", Int("Max list items")), Target),
            Handler = (a, ctx) => BridgeClient.Call(App(a), Json.Str(a, "target"), "api.get", Strip(a, "target"), ctx)
        });
        r.Add(new Tool
        {
            Name = "api_call", Title = "Call any API method", Toolset = "api", ReadOnly = false,
            Description = "Call any API method or constructor ('new'). target = object path/handle or type:Full.Name for statics. args are JSON: numbers, strings, enum names, [x,y,z] for XYZ/Point3d, \"$h12\" handles, {\"$new\":\"Type\",\"args\":[...]}. In Revit the call runs inside a transaction (one undo step) unless transaction=false.",
            InputSchema = Obj(appParam, R("target", Str("Object path, $handle, or type:Full.Type.Name")), R("method", Str("Method name, or 'new'")),
                O("args", Arr("Arguments")), O("transaction", Bool("Wrap in a transaction (default true)")), O("maxItems", Int("Max list items")),
                ("instance", Str("Which running instance (year/pid/id)"), false)),
            Handler = (a, ctx) => BridgeClient.Call(App(a), Json.Str(a, "instance"), "api.call", Strip(a, "instance"), ctx)
        });
        r.Add(new Tool
        {
            Name = "api_set", Title = "Set any API property", Toolset = "api", ReadOnly = false,
            Description = "Set a writable property or field on an API object (inside a transaction in Revit).",
            InputSchema = Obj(appParam, R("target", Str("Owner object path/handle (or type:Name for static)")), R("member", Str("Property/field name")), R("value", Any("New value")),
                ("instance", Str("Which running instance"), false)),
            Handler = (a, ctx) => BridgeClient.Call(App(a), Json.Str(a, "instance"), "api.set", Strip(a, "instance"), ctx)
        });
        r.Add(new Tool
        {
            Name = "script_run", Title = "Run C# in the application", Toolset = "api", ReadOnly = false,
            Description = "Compile and run C# against the live API (Roslyn). Revit globals: doc, uidoc, uiapp, app. AutoCAD: doc, db, ed, civil. Navisworks: doc. Args: Args dictionary. Print(x) streams text. The value of the last expression / return is returned. Revit: runs in one transaction (single undo) unless transaction=false. No 'await'.",
            InputSchema = Obj(appParam, R("code", Str("C# statements, e.g. var walls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).Count(); return walls;")),
                O("args", AnyObj("Values available as Args[\"name\"]")), O("transaction", Bool("Wrap in a transaction (default true)")), O("label", Str("Undo label")), Target),
            Handler = (a, ctx) =>
            {
                if (!Config.AllowScripts) throw new ToolException("C# scripting is disabled by the administrator (allowScripts=false).");
                return BridgeClient.Call(App(a), Json.Str(a, "target"), "script.run", Strip(a, "target"), ctx);
            }
        });

        RegisterRevit(r);
        RegisterAutoCad(r);
        RegisterNavisworks(r);
    }

    static void RegisterRevit(ToolRegistry r)
    {
        var ids = O("ids", Arr("Element ids (omit to use the current selection)", Json.O("type", "integer")));
        Fwd(r, "revit_info", "Revit status", "Revit version, open models, active view.", "revit", "revit", "revit.info", Obj(Target), true);
        Fwd(r, "revit_commands", "List Revit commands", "Every runnable Revit command (PostableCommand) for this Revit version, filterable.", "revit", "revit", "revit.commands.list",
            Obj(O("search", Str("Filter, e.g. 'Section', 'Rebar', 'Energy'")), Target), true);
        Fwd(r, "revit_command", "Run a Revit command", "Run any Revit command: PostableCommand name (Wall, Door, Align, SectionBox, StructuralRebar, EnergySettings, Purge...), internal id (ID_OBJECTS_WALL) or add-in button id. Interactive commands then wait for user picks.", "revit", "revit", "revit.command.run",
            Obj(R("command", Str("Command name or id")), Target), false);
        Fwd(r, "revit_query", "Find Revit elements", "Find elements by category (Walls, OST_Doors...), class, level, type name, name, parameter conditions (where: [{param, op (=,!=,contains,>,<,>=,<=,empty,notempty), value}]).", "revit", "revit", "revit.elements.query",
            Obj(O("category", Str("Category name or OST_ name")), O("class", Str("API class, e.g. Wall, FamilyInstance, Pipe")), O("level", Str("Level name")),
                O("typeName", Str("Type name contains")), O("name", Str("Element name contains")), O("where", Arr("Parameter filters", AnyObj("{param, op, value}"))),
                O("activeViewOnly", Bool("Only visible in active view")), O("typesOnly", Bool("Return element types instead of instances")),
                O("includeParams", Bool("Include all parameters")), O("limit", Int("Max results (default 500)")), Target), true);
        Fwd(r, "revit_get_elements", "Element details", "Full details, bounding box, instance + type parameters, BURAK BIM stamp of elements.", "revit", "revit", "revit.elements.get", Obj(ids, Target), true);
        Fwd(r, "revit_set_parameters", "Set parameters", "Set instance (or type with onType=true) parameters. Lengths given as numbers are mm; strings use project units ('3000 mm', '10 ft').", "revit", "revit", "revit.elements.set_params",
            Obj(ids, R("values", AnyObj("{\"Comments\": \"...\", \"Mark\": \"W1\", \"Unconnected Height\": 3200}")), O("onType", Bool("Set on the element type")), Target), false);
        Fwd(r, "revit_delete", "Delete elements", "Delete elements by id (one undo step).", "revit", "revit", "revit.elements.delete", Obj(R("ids", Arr("Element ids", Json.O("type", "integer"))), Target), false, true);
        Fwd(r, "revit_move", "Move / copy elements", "Move or copy elements by an offset in mm.", "revit", "revit", "revit.elements.move",
            Obj(ids, R("offset", Pt("Offset in mm")), O("copy", Bool("Copy instead of move")), Target), false);
        Fwd(r, "revit_rotate", "Rotate elements", "Rotate elements about a vertical axis.", "revit", "revit", "revit.elements.rotate",
            Obj(ids, R("center", Pt("Rotation centre mm")), R("degrees", Num("Angle, counter-clockwise")), Target), false);
        r.Add(new Tool
        {
            Name = "revit_selection", Title = "Get / set Revit selection", Toolset = "revit", ReadOnly = true,
            Description = "Without ids: returns the current selection. With ids: selects (and zooms to) those elements.",
            InputSchema = Obj(O("ids", Arr("Element ids to select", Json.O("type", "integer"))), O("zoom", Bool("Zoom to selection (default true)")), Target),
            Handler = (a, ctx) => Json.Arr(a, "ids") is { Count: > 0 }
                ? BridgeClient.Call("revit", Json.Str(a, "target"), "revit.selection.set", Strip(a, "target"), ctx)
                : BridgeClient.Call("revit", Json.Str(a, "target"), "revit.selection.get", Strip(a, "target"), ctx)
        });
        Fwd(r, "revit_views", "List views and sheets", "All views and sheets (id, name, type, sheet number).", "revit", "revit", "revit.views.list", Obj(O("search", Str("Name filter")), Target), true);
        Fwd(r, "revit_activate_view", "Open a view", "Open/activate a view or sheet by id, name or sheet number.", "revit", "revit", "revit.view.activate", Obj(R("view", Str("Id, name or sheet number")), Target), true);
        Fwd(r, "revit_create_view", "Create a view / sheet", "Create a 3d, plan, ceiling, structural plan, section (start/end/depth/height mm) or sheet.", "revit", "revit", "revit.view.create",
            Obj(R("kind", Enum("View kind", "3d", "plan", "ceiling", "structural", "section", "sheet")), O("name", Str("View name")), O("level", Str("Level for plans")),
                O("start", Pt("Section line start mm")), O("end", Pt("Section line end mm")), O("depth", Num("Section depth mm")), O("height", Num("Section height mm")), O("number", Str("Sheet number")), Target), false);
        Fwd(r, "revit_levels", "List levels", "Levels with elevations in mm.", "revit", "revit", "revit.levels.list", Obj(Target), true);
        Fwd(r, "revit_types", "List loaded types", "Family types / system types loaded in the model, by category or class.", "revit", "revit", "revit.types.list",
            Obj(O("category", Str("Category, e.g. Walls, Doors, Structural Columns, Pipes")), O("class", Str("e.g. WallType, FloorType, PipeType")), O("search", Str("Filter")), O("limit", Int("Max")), Target), true);
        Fwd(r, "revit_categories", "List categories", "All model/annotation categories.", "revit", "revit", "revit.categories.list", Obj(Target), true);
        Fwd(r, "revit_create", "Create Revit elements", "Create (batch with items[]) in one undo step - ARCH: wall, floor, ceiling, roof, room, family_instance (doors/windows with hostId, furniture, any category); STR: column, beam, brace, foundation; MEP: pipe, duct, cable_tray, conduit; DATUM: grid, level; DOC: text, model_line, detail_line. Coordinates in mm. Every element is stamped AI-created for inspectors.", "revit", "revit", "revit.create",
            Obj(R("kind", Str("wall | floor | ceiling | roof | room | column | beam | brace | foundation | family_instance | pipe | duct | cable_tray | conduit | grid | level | text | model_line | detail_line")),
                O("start", Pt("Start mm")), O("end", Pt("End mm")), O("point", Pt("Insertion point mm")), O("boundary", Arr("Closed outline [[x,y],...] mm")),
                O("level", Str("Level name")), O("topLevel", Str("Top level name")), O("type", Str("Type name (see revit_types)")), O("height", Num("Height mm")),
                O("system", Str("MEP system type name")), O("diameter", Num("mm")), O("width", Num("mm")), O("hostId", Int("Host element id (doors/windows)")),
                O("category", Str("Category for family_instance")), O("rotation", Num("Degrees")), O("structural", Bool("Structural wall")), O("name", Str("Name (grid/level/room)")),
                O("elevation", Num("Level elevation mm")), O("text", Str("Text")), O("mark", Str("Mark")), O("comments", Str("Comments")),
                O("items", Arr("Batch: list of objects with the same fields", AnyObj("item"))), Target), false);
        Fwd(r, "revit_load_family", "Load a family", "Load an .rfa family file.", "revit", "revit", "revit.family.load", Obj(R("path", Str("Full path to .rfa")), Target), false);
        Fwd(r, "revit_open", "Open a Revit model", "Open and activate a .rvt/.rfa (detach=true for central files).", "revit", "revit", "revit.doc.open", Obj(R("path", Str("Model path")), O("detach", Bool("Detach from central")), Target), false);
        Fwd(r, "revit_save", "Save the model", "Save, or Save As with path.", "revit", "revit", "revit.doc.save", Obj(O("path", Str("Save As path")), O("overwrite", Bool("Overwrite")), Target), false);
        Fwd(r, "revit_new", "New project", "New project from a template (default template if omitted), optionally saved to path.", "revit", "revit", "revit.doc.new", Obj(O("template", Str(".rte path")), O("path", Str("Save to")), Target), false);
        Fwd(r, "revit_export", "Export", "Export IFC (ifcVersion), DWG, NWC, PDF, FBX, gbXML or PNG/JPG images. Default folder: <BURAK BIM>\\exports.", "revit", "revit", "revit.export",
            Obj(R("format", Enum("Format", "ifc", "dwg", "nwc", "pdf", "fbx", "gbxml", "png", "jpg")), O("folder", Str("Output folder")), O("name", Str("File name")),
                O("viewIds", Arr("Views/sheets to export", Json.O("type", "integer"))), O("ifcVersion", Str("IFC2x3CV2, IFC4 ...")), O("combine", Bool("PDF: one file")), O("pixels", Int("Image width")), Target), false, false, 600000);
        Fwd(r, "revit_warnings", "Model warnings", "Revit warnings with failing element ids.", "revit", "revit", "revit.warnings", Obj(O("limit", Int("Max")), Target), true);
        Fwd(r, "revit_schedule", "Read a schedule", "Rows of a schedule view.", "revit", "revit", "revit.schedule.data", Obj(R("schedule", Str("Schedule name or id")), O("limit", Int("Max rows")), Target), true);
        Fwd(r, "revit_undo", "Undo", "Undo the last change in Revit.", "revit", "revit", "revit.undo", Obj(Target), false);

        // inspector
        Fwd(r, "inspector_list", "AI-created elements", "Every element created by AI through BURAK BIM, with provenance (AI client, session, tool, user, time), review status and scan deviation.", "inspector", "revit", "revit.inspector.list",
            Obj(O("status", Enum("Filter", "unverified", "verified", "rejected")), O("limit", Int("Max")), Target), true);
        Fwd(r, "inspector_mark", "Inspector sign-off", "Mark elements verified / rejected / unverified with inspector name, note and LOD (e.g. 400, 500 for field-verified as-built).", "inspector", "revit", "revit.inspector.mark",
            Obj(R("status", Enum("Review status", "verified", "rejected", "unverified")), ids, O("inspector", Str("Inspector name")), O("note", Str("Comment")), O("lod", Str("LOD after review, e.g. 350, 400, 500")), Target), false);
        Fwd(r, "inspector_review_view", "AI review 3D view", "Create/refresh the 'BURAK BIM - AI Review' 3D view: red unverified, green verified, orange rejected, purple out of scan tolerance.", "inspector", "revit", "revit.inspector.review_view",
            Obj(O("activate", Bool("Open the view (default true)")), O("isolate", Bool("Temporarily isolate AI elements")), Target), false);
    }

    static void RegisterAutoCad(ToolRegistry r)
    {
        const string ts = "autocad";
        Fwd(r, "autocad_info", "AutoCAD / Civil 3D status", "Product (AutoCAD, Civil 3D, Plant 3D, Advance Steel...), open drawings, units.", ts, "autocad", "acad.info", Obj(Target), true);
        Fwd(r, "autocad_command", "Run any AutoCAD command", "Send ANY command-line input to AutoCAD or a vertical (Civil 3D, Plant 3D, Map 3D, MEP, Advance Steel, LISP commands, add-ins). Use '_.' prefixes and spaces/newlines as Enter: '_.LINE 0,0 1000,0 ', '_.-LAYER _M WALLS ', '_.ZOOM _E '. Queued; runs when AutoCAD is idle.", ts, "autocad", "acad.command",
            Obj(R("command", Str("Command line input or macro")), O("cancelFirst", Bool("Cancel running command first (default true)")), O("echo", Bool("Echo to command line")), Target), false);
        Fwd(r, "autocad_sysvar", "System variable", "Get (no value) or set a system variable.", ts, "autocad", "acad.sysvar", Obj(R("name", Str("e.g. INSUNITS, OSMODE")), O("value", Any("New value")), Target), false);
        Fwd(r, "autocad_query", "Find drawing entities", "Entities in model/paper space by DXF type (LINE, LWPOLYLINE, INSERT, TEXT, MTEXT, CIRCLE, AECC_ALIGNMENT...) and layer, with geometry.", ts, "autocad", "acad.entities.query",
            Obj(O("types", Arr("DXF names", Json.O("type", "string"))), O("layer", Str("Layer")), O("space", Enum("Space", "model", "paper")), O("geometry", Bool("Include geometry")), O("limit", Int("Max")), Target), true);
        Fwd(r, "autocad_modify", "Modify entities", "Change layer/color, move by [dx,dy,dz] or delete entities by handle.", ts, "autocad", "acad.entities.modify",
            Obj(R("handles", Arr("Entity handles", Json.O("type", "string"))), O("layer", Str("New layer")), O("color", Int("ACI color")), O("move", Pt("Displacement")), O("delete", Bool("Erase")), Target), false);
        Fwd(r, "autocad_draw", "Draw entities", "Create line, polyline, rectangle, circle, arc, text, mtext or block insert (with attributes). Batch with items[]. Stamped as AI-created (xdata BURAKBIM).", ts, "autocad", "acad.draw",
            Obj(R("kind", Str("line | polyline | rectangle | circle | arc | text | mtext | insert")), O("start", Pt("")), O("end", Pt("")), O("center", Pt("")), O("radius", Num("")),
                O("points", Arr("Polyline points")), O("closed", Bool("")), O("corner1", Pt("")), O("corner2", Pt("")), O("position", Pt("")), O("text", Str("")), O("height", Num("")),
                O("block", Str("Block name")), O("attributes", AnyObj("Tag: value")), O("layer", Str("")), O("color", Int("ACI")), O("rotation", Num("Degrees")), O("scale", Num("")),
                O("items", Arr("Batch", AnyObj("item"))), Target), false);
        Fwd(r, "autocad_layers", "Layers", "List layers; with name: create/modify (color, on, frozen, locked, current).", ts, "autocad", "acad.layers",
            Obj(O("name", Str("Layer name")), O("color", Int("ACI")), O("on", Bool("")), O("frozen", Bool("")), O("locked", Bool("")), O("current", Bool("")), Target), false);
        Fwd(r, "autocad_blocks", "Block definitions", "Blocks with insert counts.", ts, "autocad", "acad.blocks", Obj(Target), true);
        Fwd(r, "autocad_document", "Drawing files", "list | open | new | save | saveas | close.", ts, "autocad", "acad.doc",
            Obj(R("op", Enum("Operation", "list", "open", "new", "save", "saveas", "close")), O("path", Str("File path")), O("template", Str(".dwt")), O("readOnly", Bool("")), O("save", Bool("Save on close")), Target), false);
        Fwd(r, "civil3d_objects", "Civil 3D objects", "Alignments, profiles, surfaces (elevation stats), sites, pipe networks, corridors, COGO points.", ts, "autocad", "acad.civil",
            Obj(R("kind", Enum("Kind", "alignments", "profiles", "surfaces", "sites", "pipenetworks", "corridors", "points")), O("limit", Int("Max")), Target), true);
        Fwd(r, "autocad_ai_entities", "AI-created AutoCAD entities", "Entities created by AI through BURAK BIM (inspector review).", "inspector", "autocad", "acad.inspector.list", Obj(Target), true);
    }

    static void RegisterNavisworks(ToolRegistry r)
    {
        const string ts = "navisworks";
        var search = AnyObj("{category, property, value, op: equals|contains|wildcard|defined} e.g. {\"category\":\"Element\",\"property\":\"Category\",\"value\":\"Walls\"}");
        var items = O("handles", Arr("Item handles ($h...) from search/tree; omit to use current selection", Json.O("type", "string")));
        Fwd(r, "navisworks_info", "Navisworks status", "Open file, appended models, units, selection.", ts, "navisworks", "navis.info", Obj(Target), true);
        Fwd(r, "navisworks_file", "Navisworks files", "open | append (paths[] of rvt/nwc/dwg/ifc/rcp...) | save (.nwd/.nwf) | new | refresh.", ts, "navisworks", "navis.file",
            Obj(R("op", Enum("Operation", "open", "append", "save", "new", "refresh")), O("path", Str("File")), O("paths", Arr("Files to append", Json.O("type", "string"))), Target), false, false, 900000);
        Fwd(r, "navisworks_tree", "Selection tree", "Model item tree to a depth.", ts, "navisworks", "navis.tree", Obj(O("depth", Int("Depth (default 2)")), O("limit", Int("Max nodes")), items, Target), true);
        Fwd(r, "navisworks_search", "Find items by property", "Search model items by property (Find Items), optionally select them.", ts, "navisworks", "navis.search",
            Obj(R("category", Str("Property category display name, e.g. Item, Element")), R("property", Str("Property display name, e.g. Layer, Category, Type")), O("value", Str("Value")),
                O("op", Enum("Match", "equals", "contains", "wildcard", "defined")), O("select", Bool("Select results (default true)")), O("limit", Int("Max listed")), Target), true);
        Fwd(r, "navisworks_properties", "Item properties", "All properties of selected items (or handles / search).", ts, "navisworks", "navis.properties",
            Obj(items, O("search", search), O("category", Str("Only this property category")), O("limit", Int("Max items")), Target), true);
        Fwd(r, "navisworks_selection", "Selection", "get | clear | set (from handles or search).", ts, "navisworks", "navis.selection",
            Obj(R("op", Enum("Operation", "get", "clear", "set")), items, O("search", search), Target), true);
        Fwd(r, "navisworks_visibility", "Hide / isolate", "hide | unhide | isolate | unhide_all for items (handles, search or selection).", ts, "navisworks", "navis.visibility",
            Obj(R("op", Enum("Operation", "hide", "unhide", "isolate", "unhide_all")), items, O("search", search), Target), false);
        Fwd(r, "navisworks_viewpoints", "Viewpoints", "list | apply (name) | save (name) the current view.", ts, "navisworks", "navis.viewpoints",
            Obj(R("op", Enum("Operation", "list", "apply", "save")), O("name", Str("Viewpoint name")), Target), false);
        Fwd(r, "navisworks_clash_tests", "Clash tests", "Clash Detective tests with result counts by status.", ts, "navisworks", "navis.clash.tests", Obj(Target), true);
        Fwd(r, "navisworks_clash_run", "Run clash tests", "Run one test (name) or all tests.", ts, "navisworks", "navis.clash.run", Obj(O("test", Str("Test name, omit or 'all' for all")), Target), false, false, 1800000);
        Fwd(r, "navisworks_clash_results", "Clash results", "Results of a test: status, distance, centre, both clashing items. Filter by status.", ts, "navisworks", "navis.clash.results",
            Obj(R("test", Str("Test name")), O("status", Enum("Filter", "New", "Active", "Reviewed", "Approved", "Resolved")), O("limit", Int("Max")), Target), true);
        Fwd(r, "navisworks_clash_create", "Create a clash test", "Create (and run) a clash test between two property searches, e.g. Structural Framing vs Ducts.", ts, "navisworks", "navis.clash.create",
            Obj(O("name", Str("Test name")), R("selectionA", search), R("selectionB", search), O("type", Enum("Test type", "Hard", "HardConservative", "Clearance", "Duplicate")),
                O("tolerance", Num("Tolerance in model units")), O("run", Bool("Run now (default true)")), Target), false, false, 1800000);
        Fwd(r, "navisworks_clash_update", "Update clash results", "Set status and/or assigned-to on clash results (handles from navisworks_clash_results).", ts, "navisworks", "navis.clash.update",
            Obj(R("handles", Arr("Result handles", Json.O("type", "string"))), O("status", Enum("Status", "New", "Active", "Reviewed", "Approved", "Resolved")), O("assignedTo", Str("Person")), Target), false);
        Fwd(r, "navisworks_timeliner", "TimeLiner tasks", "4D TimeLiner tasks with planned/actual dates.", ts, "navisworks", "navis.timeliner", Obj(O("limit", Int("Max")), Target), true);
    }
}
