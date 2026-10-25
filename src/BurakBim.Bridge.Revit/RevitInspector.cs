// BURAK BIM - BIM Inspector workflow in Revit.
// Every element created by an AI agent through BURAK BIM gets an invisible, tamper-evident stamp
// (Revit Extensible Storage) with who/when/which tool. Inspectors review, verify or reject it.
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using BurakBim.Shared;

namespace BurakBim.Bridge.Revit
{
    public static class RevitInspector
    {
        static readonly Guid SchemaGuid = new Guid("6f1b3c2e-8a4d-4b7e-9c11-5b2a7d3e9f01");
        const string Field = "Data";
        public const string ReviewViewName = "BURAK BIM - AI Review";
        static RevitHost H;

        public static void Register(BridgeServer s, RevitHost host)
        {
            H = host;
            s.Register(new BridgeCommand { Name = "revit.inspector.list", Description = "Every AI-created element with its stamp and review status.", Handler = List });
            s.Register(new BridgeCommand { Name = "revit.inspector.mark", Writes = true, Description = "Inspector sign-off: verified / rejected / unverified, with inspector name, note and LOD.", Handler = Mark });
            s.Register(new BridgeCommand { Name = "revit.inspector.review_view", Writes = true, Description = "Create the color-coded AI Review 3D view.", Handler = p => BuildReviewView(H.UiApp, Json.Bool(p, "activate", true), Json.Bool(p, "isolate")) });
        }

        static Schema GetSchema(bool create)
        {
            var s = Schema.Lookup(SchemaGuid);
            if (s != null || !create) return s;
            var b = new SchemaBuilder(SchemaGuid);
            b.SetSchemaName("BurakBimStamp");
            b.SetDocumentation("BURAK BIM AI provenance and inspector review data.");
            b.SetReadAccessLevel(AccessLevel.Public);
            b.SetWriteAccessLevel(AccessLevel.Public);
            b.AddSimpleField(Field, typeof(string));
            return b.Finish();
        }

        public static Dictionary<string, object> ReadStamp(Element e)
        {
            try
            {
                var s = GetSchema(false);
                if (s == null) return null;
                var ent = e.GetEntity(s);
                if (ent == null || !ent.IsValid()) return null;
                var txt = ent.Get<string>(s.GetField(Field));
                return string.IsNullOrEmpty(txt) ? null : Json.ParseObject(txt);
            }
            catch { return null; }
        }

        static void Write(Element e, Dictionary<string, object> data)
        {
            var s = GetSchema(true);
            var ent = new Entity(s);
            ent.Set(s.GetField(Field), Json.Serialize(data));
            e.SetEntity(ent);
        }

        /// <summary>Called inside an open transaction.</summary>
        public static void Stamp(Document doc, IEnumerable<ElementId> ids, string label)
        {
            foreach (var id in ids)
            {
                var e = doc.GetElement(id);
                if (!IsReviewable(e)) continue;
                try
                {
                    if (ReadStamp(e) != null) continue;
                    Write(e, Json.O(
                        "by", "BURAK BIM",
                        "action", label,
                        "client", RequestContext.Get("client", "unknown AI client"),
                        "session", RequestContext.Get("session", ""),
                        "tool", RequestContext.Get("tool", ""),
                        "user", Environment.UserName,
                        "createdUtc", DateTime.UtcNow,
                        "status", "unverified"));
                }
                catch { /* some elements cannot carry extensible storage */ }
            }
        }

        /// <summary>Only real building elements and datums are stamped - not sketches, sketch planes or sketch lines.</summary>
        internal static bool IsReviewable(Element e)
        {
            if (e == null || e is ElementType || e.Category == null) return false;
            if (e is Level || e is Grid || e is TextNote) return true;
            if (e.ViewSpecific) return false;
            if (e is Sketch || e is SketchPlane || e is CurveElement) return false;
            return e.Category.CategoryType == CategoryType.Model;
        }

        /// <summary>Merge fields into an element's stamp (creates one if missing). Inside a transaction.</summary>
        public static void Update(Element e, Dictionary<string, object> fields)
        {
            var d = ReadStamp(e) ?? Json.O("by", "manual/unknown", "status", "unverified");
            foreach (var kv in fields) d[kv.Key] = kv.Value;
            Write(e, d);
        }

        static IEnumerable<Element> Stamped(Document doc)
        {
            if (GetSchema(false) == null) return Enumerable.Empty<Element>();
            return new FilteredElementCollector(doc).WherePasses(new ExtensibleStorageFilter(SchemaGuid));
        }

        static object List(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var status = Json.Str(p, "status");
            var rows = new List<object>();
            var counts = new Dictionary<string, object>();
            foreach (var e in Stamped(doc))
            {
                var st = ReadStamp(e);
                if (st == null) continue;
                var s = Json.Str(st, "status", "unverified");
                counts[s] = (counts.ContainsKey(s) ? (int)counts[s] : 0) + 1;
                if (!string.IsNullOrEmpty(status) && !string.Equals(s, status, StringComparison.OrdinalIgnoreCase)) continue;
                rows.Add(Json.O("id", Ids.Of(e.Id), "category", e.Category != null ? e.Category.Name : null, "name", e.Name, "stamp", st));
            }
            return Json.O("model", doc.Title, "counts", counts, "elements", rows.Take((int)Json.Int(p, "limit", 5000)).ToList(), "total", rows.Count);
        }

        static object Mark(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var status = (Json.Str(p, "status") ?? "").ToLowerInvariant();
            if (status != "verified" && status != "rejected" && status != "unverified")
                throw new BridgeException("status must be verified, rejected or unverified.");
            var ids = Json.IntList(p, "ids");
            if (ids.Count == 0) ids = H.UiApp.ActiveUIDocument.Selection.GetElementIds().Select(Ids.Of).ToList();
            if (ids.Count == 0) throw new BridgeException("Pass ids or select elements.");
            var fields = Json.O("status", status, "inspector", Json.Str(p, "inspector", Environment.UserName),
                "note", Json.Str(p, "note", ""), "reviewedUtc", DateTime.UtcNow);
            var lod = Json.Str(p, "lod");
            if (!string.IsNullOrEmpty(lod)) fields["lod"] = lod;
            int n = 0;
            using (var t = new Transaction(doc, "BURAK BIM: inspector " + status))
            {
                t.Start();
                foreach (var id in ids)
                {
                    var e = doc.GetElement(Ids.Make(id));
                    if (e == null) continue;
                    Update(e, fields);
                    n++;
                }
                t.Commit();
            }
            return Json.O("marked", n, "status", status);
        }

        public static Result MarkSelection(UIApplication uiapp, string status, ref string message)
        {
            var uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) { message = "Open a model first."; return Result.Failed; }
            var ids = uidoc.Selection.GetElementIds();
            if (ids.Count == 0) { message = "Select elements to mark as " + status + "."; return Result.Failed; }
            using (var t = new Transaction(uidoc.Document, "BURAK BIM: inspector " + status))
            {
                t.Start();
                foreach (var id in ids)
                    Update(uidoc.Document.GetElement(id), Json.O("status", status, "inspector", Environment.UserName, "reviewedUtc", DateTime.UtcNow));
                t.Commit();
            }
            TaskDialog.Show("BURAK BIM", ids.Count + " element(s) marked " + status + " by " + Environment.UserName + ".");
            return Result.Succeeded;
        }

        public static Dictionary<string, object> BuildReviewView(UIApplication uiapp, bool activate, bool isolate)
        {
            var uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) throw new BridgeException("Open a model first.");
            var doc = uidoc.Document;
            var counts = new Dictionary<string, object>();
            View3D view;
            var stamped = Stamped(doc).ToList();
            using (var t = new Transaction(doc, "BURAK BIM: AI review view"))
            {
                t.Start();
                view = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate && v.Name == ReviewViewName);
                if (view == null)
                {
                    var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(x => x.ViewFamily == ViewFamily.ThreeDimensional);
                    view = View3D.CreateIsometric(doc, vft.Id);
                    view.Name = ReviewViewName;
                }
                if (view.ViewTemplateId != ElementId.InvalidElementId) view.ViewTemplateId = ElementId.InvalidElementId;
                var solid = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
                    .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
                foreach (var e in stamped)
                {
                    var st = ReadStamp(e);
                    var s = st == null ? "unverified" : Json.Str(st, "status", "unverified");
                    counts[s] = (counts.ContainsKey(s) ? (int)counts[s] : 0) + 1;
                    var c = s == "verified" ? new Color(40, 170, 70) : s == "rejected" ? new Color(240, 140, 0) : new Color(220, 40, 40);
                    var dev = st == null ? null : Json.Obj(st, "scanDeviation");
                    if (s != "verified" && dev != null && Json.Str(dev, "status") == "out-of-tolerance") c = new Color(160, 0, 160);
                    var ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(c);
                    if (solid != null)
                    {
                        ogs.SetSurfaceForegroundPatternId(solid.Id);
                        ogs.SetSurfaceForegroundPatternColor(c);
                        ogs.SetCutForegroundPatternId(solid.Id);
                        ogs.SetCutForegroundPatternColor(c);
                    }
                    try { view.SetElementOverrides(e.Id, ogs); } catch { }
                }
                t.Commit();
            }
            if (activate) uidoc.RequestViewChange(view);
            if (isolate && stamped.Count > 0 && uidoc.ActiveView.Id == view.Id)
            {
                using (var t = new Transaction(doc, "BURAK BIM: isolate AI elements"))
                {
                    t.Start();
                    view.IsolateElementsTemporary(stamped.Select(x => x.Id).ToList());
                    t.Commit();
                }
            }
            return Json.O("view", ReviewViewName, "viewId", Ids.Of(view.Id), "aiElements", stamped.Count, "byStatus", counts,
                "legend", "red = unverified, green = verified, orange = rejected, purple = out of scan tolerance");
        }
    }
}
