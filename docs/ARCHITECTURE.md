# BURAK BIM architecture

```
 AI client (Claude Desktop / Claude Code / Codex / Antigravity / Cursor / Gemini / VS Code ...)
        │  MCP over stdio (JSON-RPC 2.0, protocol 2024-11-05 … 2025-06-18)
        ▼
 BurakBim.Server.exe  ──────────────────────────────────────────────────────────────┐
   Mcp/            protocol, tools/list (toolsets: auto = installed software only), │
                   prompts, server instructions                                    │
   Tools/          core · api · revit · scan · inspector · autocad · navisworks ·  │
                   inventor · recap · com · ui                                     │
   Detection/      registry + every fixed drive → products 2022-2027 (+ others)    │
   Bridges/        discovers runtime\*.json, loopback TCP + token, retries          │
   Com/            GetActiveObject / ProgID, IDispatch type info (com_members)     │
   ScanToBim/      levels (Z histogram), RANSAC walls, pairing, joins, columns,    │
                   convex slab outline - deterministic                             │
   Core/Audit      audit\YYYY-MM-DD.jsonl for every call                           │
   Reports/        HTML inspection report                                          │
        │                       │                          │                        │
        │ loopback TCP          │ COM (out-of-process)     │ Windows UI Automation  │
        ▼                       ▼                          ▼                        │
 In-process bridges        Inventor, AutoCAD,          any window/dialog:          │
 (one per product/year)    Robot, any ProgID           ReCap Pro, dialogs, …       │
   Revit 2022-2027          (no add-in needed)          + screenshots               │
   AutoCAD family 2022-2027                                                         │
   Navisworks 2022-2027                                                             │
 └──────────────────────────────────────────────────────────────────────────────────┘
```

## Bridge core (shared by every host)
`src/BurakBim.Bridge.Core` is compiled into every bridge:

- **BridgeServer**: listens on `127.0.0.1:<random>`. Each request carries a 24-byte random token from the discovery file `runtime\<host>-<year>-<pid>.json`. Requests are JSON lines.
- **MainThreadQueue**: runs work on the host's API thread. Revit kicks it with an `ExternalEvent` plus a `WM_NULL` nudge; AutoCAD and Navisworks use the UI dispatcher or SynchronizationContext. A request that times out is **cancelled**, so it can never run late.
- **Reflector**: path evaluation (`doc.ActiveView.Name`, `[i]`, `Method(args)`, `type:Full.Name`), overload resolution, JSON → CLR conversion (handles, enums, `[x,y,z]` → XYZ/Point3d, numbers → ElementId/ObjectId, lists → arrays/List<T>), and object handles (`$h12`).
- **RibbonAutomation**: late-bound `Autodesk.Windows.ComponentManager.Ribbon`. It walks tabs › panels › items, including split buttons, row panels and slide-outs. Invocation goes through the host first (Revit `PostCommand` via `RevitCommandId.LookupCommandId`, AutoCAD macro), then the item's own `ICommand`.
- **ScriptRunner**: Roslyn C# scripting with all loaded assemblies referenced. Each host supplies typed globals.
- **RequestContext**: the AI client, session and tool for each request, used to stamp created elements.

## Revit specifics
- `RunWrite` = TransactionGroup { work Transaction (failure preprocessor: warnings dismissed and returned, errors resolved or rolled back) ; stamp Transaction } → **Assimilate**, so each AI action is one undo step.
- Created elements are captured through `DocumentChanged` and stamped in Extensible Storage (schema `6f1b3c2e-…`, public read/write).
- ElementId compatibility: `REVIT_LONG_IDS` (2024+, `.Value`) and `IntegerValue` (2022–2023).
- Point clouds: multi-plane box filter in cloud coordinates, then `GetPoints`, then transformation to model coordinates. Points travel as a flat mm array.

## Why the bridges are built on the user's machine
Autodesk API assemblies may not be redistributed, and their APIs differ slightly between years. The installer builds each bridge against the exact installed version, keeping all build tools on the install drive. Inventor and Robot need no bridge: their COM APIs are complete and version-independent.

## Adding a new product
1. Add detection in `Detection/SoftwareDetector.cs` and in `installer/install.ps1`.
2. For a .NET-API product, add a bridge project that implements `IHostAdapter` and links `Bridge.Core`.
3. For a COM product, add tools in `Tools/ComTools.cs` (or simply use the `com_*` tools with its ProgID).
