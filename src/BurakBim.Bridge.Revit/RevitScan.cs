// BURAK BIM - Scan-to-BIM support inside Revit: link RCP/RCS scans, stream points out for
// feature detection (done in the BURAK BIM server), and measure as-built deviation of elements.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using BurakBim.Shared;

namespace BurakBim.Bridge.Revit
{
    public static class RevitScan
    {
        static RevitHost H;

        public static void Register(BridgeServer s, RevitHost host)
        {
            H = host;
            s.Register(new BridgeCommand { Name = "revit.pointcloud.list", Description = "Point cloud instances in the model with extents (mm).", Handler = List });
            s.Register(new BridgeCommand { Name = "revit.pointcloud.link", Writes = true, Description = "Link a ReCap scan (.rcp project or .rcs scan) origin-to-origin.", Handler = Link });
            s.Register(new BridgeCommand { Name = "revit.pointcloud.sample", Description = "Stream points (mm) from a point cloud inside a box.", Handler = Sample });
            s.Register(new BridgeCommand { Name = "revit.scan.deviation", Writes = true, Description = "As-built deviation of elements against the scan; stores the result on each element for inspectors.", Handler = Deviation });
        }

        static PointCloudInstance Instance(Document doc, Dictionary<string, object> p)
        {
            var id = Json.Get(p, "instanceId");
            var all = new FilteredElementCollector(doc).OfClass(typeof(PointCloudInstance)).Cast<PointCloudInstance>().ToList();
            if (all.Count == 0) throw new BridgeException("No point cloud in this model. Link one with scan_link_pointcloud (.rcp/.rcs).");
            if (id == null) return all[0];
            var hit = all.FirstOrDefault(x => Ids.Of(x.Id) == (long)Json.ToDouble(id));
            if (hit == null) throw new BridgeException("Point cloud instance " + id + " not found.");
            return hit;
        }

        static object List(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            return new FilteredElementCollector(doc).OfClass(typeof(PointCloudInstance)).Cast<PointCloudInstance>().Select(pc =>
            {
                var bb = pc.get_BoundingBox(null);
                var type = doc.GetElement(pc.GetTypeId());
                return (object)Json.O("id", Ids.Of(pc.Id), "name", pc.Name, "type", type != null ? type.Name : null,
                    "min", bb == null ? null : U.Out(bb.Min), "max", bb == null ? null : U.Out(bb.Max));
            }).ToList();
        }

        static object Link(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var path = Json.Str(p, "path");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new BridgeException("Scan file not found: " + path);
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            if (ext != "rcp" && ext != "rcs") throw new BridgeException("Revit links ReCap .rcp projects or .rcs scans. Convert other formats (e57, las, pts...) in ReCap first.");
            return H.RunWrite("BURAK BIM: link point cloud", () =>
            {
                var type = PointCloudType.Create(doc, ext, path);
                var inst = PointCloudInstance.Create(doc, type.Id, Transform.Identity);
                return Json.O("instanceId", Ids.Of(inst.Id), "typeId", Ids.Of(type.Id), "name", inst.Name);
            });
        }

        /// <summary>Box filter in point-cloud coordinates (normals point inward).</summary>
        static PointCloudFilter BoxFilter(XYZ min, XYZ max, Transform toCloud)
        {
            var planes = new List<Plane>
            {
                Plane.CreateByNormalAndOrigin(toCloud.OfVector(XYZ.BasisX), toCloud.OfPoint(min)),
                Plane.CreateByNormalAndOrigin(toCloud.OfVector(-XYZ.BasisX), toCloud.OfPoint(max)),
                Plane.CreateByNormalAndOrigin(toCloud.OfVector(XYZ.BasisY), toCloud.OfPoint(min)),
                Plane.CreateByNormalAndOrigin(toCloud.OfVector(-XYZ.BasisY), toCloud.OfPoint(max)),
                Plane.CreateByNormalAndOrigin(toCloud.OfVector(XYZ.BasisZ), toCloud.OfPoint(min)),
                Plane.CreateByNormalAndOrigin(toCloud.OfVector(-XYZ.BasisZ), toCloud.OfPoint(max))
            };
            return PointCloudFilterFactory.CreateMultiPlaneFilter(planes);
        }

        internal static List<XYZ> Points(PointCloudInstance pc, XYZ min, XYZ max, double spacingFt, int maxPoints)
        {
            var toModel = pc.GetTransform();
            var toCloud = toModel.Inverse;
            var filter = BoxFilter(min, max, toCloud);
            var result = new List<XYZ>();
            var col = pc.GetPoints(filter, spacingFt, maxPoints);
            foreach (CloudPoint cp in col)
                result.Add(toModel.OfPoint(new XYZ(cp.X, cp.Y, cp.Z)));
            return result;
        }

        static object Sample(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var pc = Instance(doc, p);
            var bb = pc.get_BoundingBox(null);
            var min = Json.Get(p, "min") != null ? U.P(Json.Get(p, "min")) : bb.Min;
            var max = Json.Get(p, "max") != null ? U.P(Json.Get(p, "max")) : bb.Max;
            int maxPoints = (int)Math.Min(Json.Int(p, "maxPoints", 50000), 1000000);
            double spacing = U.Ft(Json.Num(p, "spacing", 20));
            var pts = Points(pc, min, max, spacing, maxPoints);
            // compact flat array [x,y,z,x,y,z,...] in whole millimetres
            var flat = new List<object>(pts.Count * 3);
            foreach (var x in pts) { flat.Add(Math.Round(x.X * U.MmPerFt)); flat.Add(Math.Round(x.Y * U.MmPerFt)); flat.Add(Math.Round(x.Z * U.MmPerFt)); }
            return Json.O("instanceId", Ids.Of(pc.Id), "count", pts.Count, "units", "mm", "format", "flat xyz",
                "boxMin", U.Out(min), "boxMax", U.Out(max), "points", flat);
        }

        static object Deviation(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var pc = Instance(doc, p);
            double tolMm = Json.Num(p, "toleranceMm", 25);
            double bandMm = Json.Num(p, "searchBandMm", Math.Max(60, tolMm * 3));
            int maxPoints = (int)Json.Int(p, "maxPoints", 4000);
            var ids = Json.IntList(p, "ids");
            if (ids.Count == 0) throw new BridgeException("Pass ids of elements to check against the scan.");
            var report = new List<object>();
            var updates = new List<KeyValuePair<Element, Dictionary<string, object>>>();
            foreach (var idv in ids)
            {
                var e = doc.GetElement(Ids.Make(idv));
                if (e == null) { report.Add(Json.O("id", idv, "error", "not found")); continue; }
                var faces = Faces(e);
                var bb = e.get_BoundingBox(null);
                if (faces.Count == 0 || bb == null) { report.Add(Json.O("id", idv, "error", "no solid geometry")); continue; }
                var pad = new XYZ(U.Ft(bandMm), U.Ft(bandMm), U.Ft(bandMm));
                var pts = Points(pc, bb.Min - pad, bb.Max + pad, U.Ft(10), maxPoints);
                var d = new List<double>();
                foreach (var pt in pts)
                {
                    double best = double.MaxValue;
                    foreach (var f in faces)
                    {
                        var ir = f.Project(pt);
                        if (ir != null && ir.Distance < best) best = ir.Distance;
                    }
                    var mm = best * U.MmPerFt;
                    if (mm <= bandMm) d.Add(mm);
                }
                d.Sort();
                Dictionary<string, object> stats;
                if (d.Count < 20)
                    stats = Json.O("id", idv, "pointsNear", d.Count, "status", "insufficient-scan-coverage");
                else
                {
                    double p95 = d[(int)(0.95 * (d.Count - 1))];
                    stats = Json.O("id", idv, "category", e.Category != null ? e.Category.Name : null, "pointsNear", d.Count,
                        "meanMm", Math.Round(d.Average(), 1), "medianMm", Math.Round(d[d.Count / 2], 1),
                        "p95Mm", Math.Round(p95, 1), "maxMm", Math.Round(d[d.Count - 1], 1),
                        "toleranceMm", tolMm, "status", p95 <= tolMm ? "within-tolerance" : "out-of-tolerance");
                }
                stats["checkedUtc"] = DateTime.UtcNow;
                report.Add(stats);
                updates.Add(new KeyValuePair<Element, Dictionary<string, object>>(e, stats));
            }
            if (Json.Bool(p, "store", true) && updates.Count > 0)
            {
                using (var t = new Transaction(doc, "BURAK BIM: store scan deviation"))
                {
                    t.Start();
                    foreach (var u in updates) RevitInspector.Update(u.Key, Json.O("scanDeviation", u.Value));
                    t.Commit();
                }
            }
            int bad = report.OfType<Dictionary<string, object>>().Count(x => Json.Str(x, "status") == "out-of-tolerance");
            return Json.O("checked", report.Count, "outOfTolerance", bad, "toleranceMm", tolMm, "results", report);
        }

        static List<Face> Faces(Element e)
        {
            var faces = new List<Face>();
            var opt = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false, IncludeNonVisibleObjects = false };
            Collect(e.get_Geometry(opt), faces, 0);
            return faces;
        }

        static void Collect(GeometryElement g, List<Face> faces, int depth)
        {
            if (g == null || depth > 4) return;
            foreach (GeometryObject o in g)
            {
                var s = o as Solid;
                if (s != null && s.Volume > 1e-9) { foreach (Face f in s.Faces) faces.Add(f); continue; }
                var gi = o as GeometryInstance;
                if (gi != null) Collect(gi.GetInstanceGeometry(), faces, depth + 1);
            }
        }
    }
}
