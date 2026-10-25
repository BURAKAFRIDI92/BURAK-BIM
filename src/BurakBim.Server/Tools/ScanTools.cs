using System.Globalization;
using System.Text;
using BurakBim.Server.Bridges;
using BurakBim.Server.Core;
using BurakBim.Server.ScanToBim;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;
using static BurakBim.Server.ScanToBim.PointCloudMath;

namespace BurakBim.Server.Tools;

/// <summary>Scan-to-BIM: point cloud in Revit -> levels, walls, columns, slabs -> Revit elements -> deviation QA.</summary>
public static class ScanTools
{
    const string Ts = "scan";

    public static void Register(ToolRegistry r)
    {
        HostTools.Fwd(r, "scan_pointclouds", "Point clouds in model", "Point cloud instances in the open Revit model with extents (mm).", Ts, "revit", "revit.pointcloud.list", Obj(Target), true);
        HostTools.Fwd(r, "scan_link_pointcloud", "Link a ReCap scan", "Link a ReCap .rcp project or .rcs scan into the open Revit model (origin to origin).", Ts, "revit", "revit.pointcloud.link",
            Obj(R("path", Str("Full path to .rcp or .rcs")), Target), false, false, 900000);
        HostTools.Fwd(r, "scan_deviation", "As-built deviation check", "Measure how far scan points are from each element's faces (mean/median/p95/max mm), pass/fail against tolerance; stored on the element for inspectors.", Ts, "revit", "revit.scan.deviation",
            Obj(R("ids", Arr("Element ids", Json.O("type", "integer"))), O("toleranceMm", Num("Allowed p95 deviation (default 25)")), O("searchBandMm", Num("Points within this distance count (default 3x tolerance)")),
                O("maxPoints", Int("Points per element (default 4000)")), O("instanceId", Int("Point cloud instance")), O("store", Bool("Store result on element (default true)")), Target), false, false, 900000);

        r.Add(new Tool
        {
            Name = "scan_sample_points", Title = "Sample scan points", Toolset = Ts, ReadOnly = true,
            Description = "Read points from the linked point cloud inside a box (mm). Returns statistics and a preview; full points are saved to an .xyz file (path returned).",
            InputSchema = Obj(O("min", Pt("Box min mm")), O("max", Pt("Box max mm")), O("spacing", Num("Point spacing mm (default 20)")), O("maxPoints", Int("Max points")), O("instanceId", Int("Point cloud instance")), Target),
            Handler = (a, ctx) =>
            {
                var pts = Sample(a, ctx, Json.Get(a, "min"), Json.Get(a, "max"), Json.Num(a, "spacing", 20), (int)Json.Int(a, "maxPoints", 100000));
                var file = Path.Combine(BurakPaths.ScratchDir, "scan-sample-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".xyz");
                using (var w = new StreamWriter(file))
                    foreach (var p in pts) w.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{p.X:0} {p.Y:0} {p.Z:0}"));
                return Json.O("count", pts.Count, "file", file, "bounds", Bounds(pts), "preview", pts.Take(20).Select(p => (object)new List<object> { p.X, p.Y, p.Z }).ToList());
            }
        });

        r.Add(new Tool
        {
            Name = "scan_detect_levels", Title = "Detect floors / levels from scan", Toolset = Ts, ReadOnly = true,
            Description = "Find horizontal surfaces (floor tops, slab undersides, roof) in the point cloud and suggest level elevations; compares with existing Revit levels.",
            InputSchema = Obj(O("instanceId", Int("Point cloud instance")), O("spacing", Num("Sampling spacing mm (default 40)")), O("binMm", Num("Histogram bin (default 20)")), Target),
            Handler = (a, ctx) =>
            {
                var pts = Sample(a, ctx, null, null, Json.Num(a, "spacing", 40), MaxPoints());
                var (planes, levels) = DetectLevels(pts, Json.Num(a, "binMm", 20));
                var existing = RevitLevels(a, ctx);
                return Json.O("points", pts.Count,
                    "planes", planes.Select(p => (object)Json.O("elevationMm", p.Z, "points", p.Points, "role", p.Role)).ToList(),
                    "suggestedLevels", levels.Select((z, i) => (object)Json.O("elevationMm", z, "existingLevel", existing.FirstOrDefault(e => Math.Abs(e.z - z) < 150).name)).ToList(),
                    "existingRevitLevels", existing.Select(e => (object)Json.O("name", e.name, "elevationMm", e.z)).ToList());
            }
        });

        r.Add(new Tool
        {
            Name = "scan_detect_walls", Title = "Detect walls & columns from scan", Toolset = Ts, ReadOnly = true,
            Description = "Horizontal slice of the scan above a level -> wall faces (RANSAC), paired into walls with centreline + thickness, plus columns (rectangular/round). Writes an SVG plan preview for inspectors.",
            InputSchema = Obj(R("level", Any("Revit level name or elevation in mm")), O("sliceLow", Num("Slice bottom above level mm (default 900)")), O("sliceHigh", Num("Slice top above level mm (default 1500)")),
                O("spacing", Num("Sampling spacing mm (default 15)")), O("minLength", Num("Min wall length mm (default 500)")), O("tolerance", Num("Face fit tolerance mm (default 15)")),
                O("snap", Bool("Snap to dominant orthogonal directions (default true)")), O("instanceId", Int("Point cloud instance")), Target),
            Handler = (a, ctx) =>
            {
                var z = LevelZ(a, ctx, Json.Get(a, "level"));
                var det = DetectStorey(a, ctx, z);
                return Json.O("levelElevationMm", z, "slice", det.Slice, "points", det.Points,
                    "walls", det.Walls.Select(WallJson).ToList(), "columns", det.Columns.Select(ColJson).ToList(),
                    "preview", det.Svg, "note", "Two-face walls are reliable; single-face walls (only one side scanned) need inspector review of thickness/offset.");
            }
        });

        r.Add(new Tool
        {
            Name = "scan_to_bim", Title = "Scan to BIM (automatic modelling)", Toolset = Ts, ReadOnly = false,
            Description = "Full scan-to-BIM in Revit: detect levels -> per storey detect walls, columns and slab outline -> (dryRun=false) create levels, walls, columns, floors (one undo step per storey, all stamped AI/unverified) -> deviation QA against the scan. ALWAYS run dryRun=true first and show the plan. Output geometry is LOD 300-350; LOD 500 requires inspector verification.",
            InputSchema = Obj(O("dryRun", Bool("Plan only, create nothing (default true)")), O("levels", Arr("Elevations mm to model (default: auto-detected)", Json.O("type", "number"))),
                O("wallType", Str("Revit wall type name")), O("columnType", Str("Structural column type name")), O("floorType", Str("Floor type name")),
                O("createFloors", Bool("Create slab outlines (convex, review needed) - default false")), O("createColumns", Bool("default true")),
                O("minConfidence", Num("Skip walls below this confidence 0-1 (default 0.3)")), O("defaultHeight", Num("Wall height mm when no level above (default 3000)")),
                O("checkDeviation", Bool("Run scan_deviation on created elements (default true)")), O("toleranceMm", Num("Deviation tolerance (default 25)")),
                O("instanceId", Int("Point cloud instance")), Target),
            Handler = ScanToBimRun
        });
    }

    static int MaxPoints() => (int)Json.Int(Config.Data, "maxPointsPerRequest", 400000);

    static List<P3> Sample(Dictionary<string, object> a, ToolContext ctx, object? min, object? max, double spacing, int maxPoints)
    {
        var args = Json.O("spacing", spacing, "maxPoints", Math.Min(maxPoints, MaxPoints()));
        if (Json.Get(a, "instanceId") != null) args["instanceId"] = Json.Get(a, "instanceId");
        if (min != null) args["min"] = min;
        if (max != null) args["max"] = max;
        var res = BridgeClient.Call("revit", Json.Str(a, "target"), "revit.pointcloud.sample", args, ctx, 900000) as Dictionary<string, object>
                  ?? throw new ToolException("Unexpected point cloud response.");
        return FromFlat(Json.Arr(res, "points") ?? new List<object>());
    }

    static Dictionary<string, object> Bounds(List<P3> pts) => pts.Count == 0 ? Json.O() : Json.O(
        "min", new List<object> { pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z) },
        "max", new List<object> { pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z) });

    static List<(string name, double z)> RevitLevels(Dictionary<string, object> a, ToolContext ctx)
    {
        var res = BridgeClient.Call("revit", Json.Str(a, "target"), "revit.levels.list", null, ctx) as List<object> ?? new();
        return res.OfType<Dictionary<string, object>>().Select(l => (Json.Str(l, "name", ""), Json.Num(l, "elevation"))).ToList();
    }

    static double LevelZ(Dictionary<string, object> a, ToolContext ctx, object? level)
    {
        if (level is double or long) return Json.ToDouble(level);
        var s = Convert.ToString(level, CultureInfo.InvariantCulture) ?? "";
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return z;
        var lv = RevitLevels(a, ctx).FirstOrDefault(l => l.name.Equals(s, StringComparison.OrdinalIgnoreCase));
        if (lv.name == null) throw new ToolException($"Level '{s}' not found in Revit. Pass an elevation in mm instead.");
        return lv.z;
    }

    sealed record StoreyResult(List<WallCandidate> Walls, List<ColumnCandidate> Columns, List<P2> SlabOutline, int Points, object Slice, string Svg);

    static StoreyResult DetectStorey(Dictionary<string, object> a, ToolContext ctx, double z, bool withSlab = false)
    {
        double lo = Json.Num(a, "sliceLow", 900), hi = Json.Num(a, "sliceHigh", 1500);
        var clouds = BridgeClient.Call("revit", Json.Str(a, "target"), "revit.pointcloud.list", null, ctx) as List<object> ?? new();
        var cloud = clouds.OfType<Dictionary<string, object>>().FirstOrDefault(c => Json.Get(a, "instanceId") == null || Json.Int(c, "id") == Json.Int(a, "instanceId"))
                    ?? throw new ToolException("No point cloud linked. Use scan_link_pointcloud first.");
        var cmin = Json.Vec(Json.Get(cloud, "min"))!; var cmax = Json.Vec(Json.Get(cloud, "max"))!;
        var pts3 = Sample(a, ctx, new List<object> { cmin[0], cmin[1], z + lo }, new List<object> { cmax[0], cmax[1], z + hi }, Json.Num(a, "spacing", 15), MaxPoints());
        var slice = Slice(pts3, z + lo, z + hi, 10);
        var segs = ExtractSegments(slice, Json.Num(a, "tolerance", 15), minLengthMm: Json.Num(a, "minLength", 500));
        if (Json.Bool(a, "snap", true))
        {
            var dom = DominantAngle(segs);
            segs = segs.Select(s => Snap(s, dom)).ToList();
        }
        var walls = CleanJoins(PairWalls(segs));
        var cols = DetectColumns(slice, segs);
        var outline = new List<P2>();
        if (withSlab)
        {
            var slab = Sample(a, ctx, new List<object> { cmin[0], cmin[1], z - 40 }, new List<object> { cmax[0], cmax[1], z + 40 }, 60, 150000);
            outline = ConvexHull(Slice(slab, z - 40, z + 40, 50));
        }
        var svg = WriteSvg(z, slice, walls, cols, outline);
        return new StoreyResult(walls, cols, outline, slice.Count, Json.O("fromMm", z + lo, "toMm", z + hi), svg);
    }

    static object WallJson(WallCandidate w) => Json.O(
        "start", new List<object> { Math.Round(w.Start.X), Math.Round(w.Start.Y) }, "end", new List<object> { Math.Round(w.End.X), Math.Round(w.End.Y) },
        "lengthMm", Math.Round(w.Length), "thicknessMm", w.ThicknessMm, "kind", w.Kind, "confidence", w.Confidence, "support", w.Support);

    static object ColJson(ColumnCandidate c) => Json.O(
        "center", new List<object> { Math.Round(c.Center.X), Math.Round(c.Center.Y) }, "sizeXmm", c.SizeX, "sizeYmm", c.SizeY, "shape", c.Shape, "support", c.Support);

    static string WriteSvg(double z, List<P2> pts, List<WallCandidate> walls, List<ColumnCandidate> cols, List<P2> outline)
    {
        var file = Path.Combine(BurakPaths.ReportsDir, $"scan-plan-{z:0}mm-{DateTime.Now:yyyyMMdd-HHmmss}.svg");
        if (pts.Count == 0) { File.WriteAllText(file, "<svg xmlns='http://www.w3.org/2000/svg'/>"); return file; }
        double x0 = pts.Min(p => p.X) - 500, x1 = pts.Max(p => p.X) + 500, y0 = pts.Min(p => p.Y) - 500, y1 = pts.Max(p => p.Y) + 500;
        double scale = 1600 / Math.Max(x1 - x0, y1 - y0);
        string X(double x) => ((x - x0) * scale).ToString("0.0", CultureInfo.InvariantCulture);
        string Y(double y) => ((y1 - y) * scale).ToString("0.0", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{X(x1)}' height='{Y(y0)}' style='background:#fff;font-family:Segoe UI,Arial'>");
        sb.Append($"<text x='10' y='22' font-size='16'>BURAK BIM scan-to-BIM preview - level {z:0} mm - grey: scan slice, red: walls (two-face), orange: single-face, blue: columns, green: slab outline</text>");
        var rnd = new Random(1);
        foreach (var p in pts.Count > 30000 ? pts.OrderBy(_ => rnd.Next()).Take(30000) : pts)
            sb.Append($"<circle cx='{X(p.X)}' cy='{Y(p.Y)}' r='0.7' fill='#999'/>");
        if (outline.Count > 2) sb.Append("<polygon fill='none' stroke='#2a2' stroke-width='2' points='" + string.Join(" ", outline.Select(p => X(p.X) + "," + Y(p.Y))) + "'/>");
        foreach (var w in walls)
        {
            var col = w.Kind.StartsWith("two") ? "#d22" : "#f90";
            var sw = Math.Max(1.5, w.ThicknessMm * scale).ToString("0.0", CultureInfo.InvariantCulture);
            sb.Append($"<line x1='{X(w.Start.X)}' y1='{Y(w.Start.Y)}' x2='{X(w.End.X)}' y2='{Y(w.End.Y)}' stroke='{col}' stroke-opacity='0.75' stroke-width='{sw}'/>");
        }
        foreach (var c in cols)
            sb.Append($"<rect x='{X(c.Center.X - c.SizeX / 2)}' y='{Y(c.Center.Y + c.SizeY / 2)}' width='{(c.SizeX * scale).ToString("0.0", CultureInfo.InvariantCulture)}' height='{(c.SizeY * scale).ToString("0.0", CultureInfo.InvariantCulture)}' fill='#36c' fill-opacity='0.7'/>");
        sb.Append("</svg>");
        File.WriteAllText(file, sb.ToString());
        return file;
    }

    static List<object> CreatedIds(object? res)
    {
        var ids = new List<object>();
        if (res is Dictionary<string, object> d && Json.Arr(d, "createdIds") is { } c) ids.AddRange(c);
        return ids;
    }

    static object ScanToBimRun(Dictionary<string, object> a, ToolContext ctx)
    {
        bool dry = Json.Bool(a, "dryRun", true);
        var target = Json.Str(a, "target");
        double minConf = Json.Num(a, "minConfidence", 0.3);
        // 1) levels
        List<double> levels = Json.Arr(a, "levels")?.Select(x => Json.ToDouble(x)).OrderBy(x => x).ToList() ?? new();
        object? planes = null;
        if (levels.Count == 0)
        {
            var pts = Sample(a, ctx, null, null, 40, MaxPoints());
            var (pl, lv) = DetectLevels(pts);
            levels = lv;
            planes = pl.Select(p => (object)Json.O("elevationMm", p.Z, "role", p.Role, "points", p.Points)).ToList();
        }
        if (levels.Count == 0) throw new ToolException("No floor planes found in the scan. Pass levels=[elevations mm] explicitly.");
        var existing = RevitLevels(a, ctx);
        var plan = new List<object>();
        var created = new List<object>();
        var storeys = new List<(double z, string name, bool exists, StoreyResult det)>();
        for (int i = 0; i < levels.Count; i++)
        {
            var z = levels[i];
            var match = existing.FirstOrDefault(e => Math.Abs(e.z - z) < 150);
            var name = match.name ?? $"Scan Level {i + 1} ({z / 1000:0.00} m)";
            var det = DetectStorey(a, ctx, match.name != null ? match.z : z, Json.Bool(a, "createFloors"));
            storeys.Add((match.name != null ? match.z : z, name, match.name != null, det));
        }
        for (int i = 0; i < storeys.Count; i++)
        {
            var s = storeys[i];
            double height = i + 1 < storeys.Count ? storeys[i + 1].z - s.z : Json.Num(a, "defaultHeight", 3000);
            var walls = s.det.Walls.Where(w => w.Confidence >= minConf).ToList();
            var entry = Json.O("level", s.name, "elevationMm", s.z, "levelExists", s.exists, "wallHeightMm", Math.Round(height),
                "walls", walls.Count, "wallsSkippedLowConfidence", s.det.Walls.Count - walls.Count,
                "singleFaceWalls", walls.Count(w => !w.Kind.StartsWith("two")), "columns", s.det.Columns.Count,
                "slabOutlinePoints", s.det.SlabOutline.Count, "preview", s.det.Svg,
                "wallList", walls.Select(WallJson).ToList(), "columnList", s.det.Columns.Select(ColJson).ToList());
            plan.Add(entry);
            if (dry) continue;

            if (!s.exists)
            {
                var lr = BridgeClient.Call("revit", target, "revit.create", Json.O("kind", "level", "elevation", s.z, "name", s.name), ctx);
                created.AddRange(CreatedIds(lr));
            }
            var topName = i + 1 < storeys.Count ? storeys[i + 1].name : null;
            if (walls.Count > 0)
            {
                var items = walls.Select(w => (object)Json.O("start", new List<object> { w.Start.X, w.Start.Y, s.z }, "end", new List<object> { w.End.X, w.End.Y, s.z },
                    "height", height, "comments", $"BURAK BIM scan-to-BIM: {w.Kind}, t={w.ThicknessMm}mm, conf={w.Confidence}")).ToList();
                var wr = BridgeClient.Call("revit", target, "revit.create", Json.O("kind", "wall", "level", s.name, "type", Json.Str(a, "wallType"), "items", items), ctx, 900000);
                created.AddRange(CreatedIds(wr));
            }
            if (Json.Bool(a, "createColumns", true) && s.det.Columns.Count > 0)
            {
                var items = s.det.Columns.Select(c => (object)Json.O("point", new List<object> { c.Center.X, c.Center.Y, s.z },
                    "comments", $"BURAK BIM scan-to-BIM: {c.Shape} {c.SizeX}x{c.SizeY}mm")).ToList();
                var args = Json.O("kind", "column", "level", s.name, "type", Json.Str(a, "columnType"), "items", items);
                if (topName != null) args["topLevel"] = topName;
                created.AddRange(CreatedIds(BridgeClient.Call("revit", target, "revit.create", args, ctx, 900000)));
            }
            if (Json.Bool(a, "createFloors") && s.det.SlabOutline.Count >= 3)
            {
                var boundary = s.det.SlabOutline.Select(p => (object)new List<object> { p.X, p.Y }).ToList();
                created.AddRange(CreatedIds(BridgeClient.Call("revit", target, "revit.create",
                    Json.O("kind", "floor", "level", s.name, "type", Json.Str(a, "floorType"), "boundary", boundary, "comments", "BURAK BIM scan-to-BIM slab (convex outline - review)"), ctx)));
            }
        }
        object? deviation = null;
        if (!dry && Json.Bool(a, "checkDeviation", true) && created.Count > 0)
        {
            try
            {
                deviation = BridgeClient.Call("revit", target, "revit.scan.deviation",
                    Json.O("ids", created, "toleranceMm", Json.Num(a, "toleranceMm", 25), "instanceId", Json.Get(a, "instanceId")!), ctx, 1800000);
                if (deviation is Dictionary<string, object> dd) dd.Remove("results");
            }
            catch (Exception ex) { deviation = "deviation check failed: " + ex.Message; }
        }
        return Json.O("dryRun", dry, "detectedPlanes", planes, "storeys", plan,
            "createdCount", created.Count, "createdIds", created, "deviationSummary", deviation,
            "next", dry
                ? "Review the plan and SVG previews. Then call scan_to_bim with dryRun=false (choose wallType/columnType with revit_types)."
                : "Open inspector_review_view; inspectors verify/reject elements with inspector_mark. Only verified + within-tolerance elements may be reported as LOD 500 (field-verified).");
    }
}
