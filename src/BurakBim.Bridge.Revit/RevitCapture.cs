// BURAK BIM 1.3 - Capture mode for Revit: render any view (plan, ceiling plan, section, elevation, 3D, drafting, sheet)
// to an image so the AI agent can SEE the model. Works on Revit 2022-2027 (ImageExportOptions API).
// The image is written to <install>\captures and the path is returned; the MCP server sends it to the AI as an image.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using BurakBim.Shared;

namespace BurakBim.Bridge.Revit
{
    public static class RevitCapture
    {
        static RevitHost H;

        public static void Register(BridgeServer s, RevitHost host)
        {
            H = host;
            s.Register(new BridgeCommand
            {
                Name = "revit.capture",
                Description = "Render views to PNG/JPG (active view by default) and return file paths + view facts.",
                Writes = false,
                Handler = Capture
            });
            s.Register(new BridgeCommand
            {
                Name = "revit.capture.views",
                Description = "Views that can be captured (printable graphical views and sheets).",
                Writes = false,
                Handler = ListCapturable
            });
        }

        static bool Capturable(View v)
        {
            if (v == null || v.IsTemplate) return false;
            if (!v.CanBePrinted) return false;
            switch (v.ViewType)
            {
                case ViewType.Schedule:
                case ViewType.ColumnSchedule:
                case ViewType.PanelSchedule:
                case ViewType.Report:
                case ViewType.ProjectBrowser:
                case ViewType.SystemBrowser:
                case ViewType.Internal:
                case ViewType.Undefined:
                    return false;
            }
            return true;
        }

        static object ListCapturable(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var search = Json.Str(p, "search");
            var list = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(Capturable)
                .Where(v => string.IsNullOrEmpty(search) || v.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name)
                .Take((int)Json.Int(p, "limit", 300))
                .Select(v => (object)Json.O("id", Ids.Of(v.Id), "name", v.Name, "type", v.ViewType.ToString()))
                .ToList();
            return Json.O("count", list.Count, "views", list);
        }

        static View FindView(Document doc, object key)
        {
            if (key == null) return null;
            var s = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture).Trim();
            long id;
            if (long.TryParse(s, out id))
            {
                var byId = doc.GetElement(Ids.Make(id)) as View;
                if (byId != null) return byId;
            }
            var all = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).ToList();
            return all.FirstOrDefault(v => string.Equals(v.Name, s, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(v => v is ViewSheet && string.Equals(((ViewSheet)v).SheetNumber, s, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(v => v.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static object Capture(Dictionary<string, object> p)
        {
            var doc = H.RequireDoc();
            var views = new List<View>();
            var keys = Json.Arr(p, "views") ?? new List<object>();
            if (Json.Get(p, "view") != null) keys.Insert(0, Json.Get(p, "view"));
            if (Json.Get(p, "viewId") != null) keys.Insert(0, Json.Get(p, "viewId"));
            foreach (var k in keys)
            {
                var v = FindView(doc, k);
                if (v == null) throw new BridgeException("View '" + k + "' not found. Use revit_capture_views to list capturable views.");
                views.Add(v);
            }
            if (Json.Bool(p, "allOpenViews") && H.UiApp.ActiveUIDocument != null)
            {
                foreach (var uv in H.UiApp.ActiveUIDocument.GetOpenUIViews())
                {
                    var v = doc.GetElement(uv.ViewId) as View;
                    if (v != null && !views.Any(x => x.Id == v.Id)) views.Add(v);
                }
            }
            if (views.Count == 0) views.Add(doc.ActiveView);
            views = views.Where(v => v != null).ToList();
            foreach (var v in views)
                if (!Capturable(v))
                    throw new BridgeException("View '" + v.Name + "' (" + v.ViewType + ") cannot be rendered as an image. Schedules: use revit_schedule; browser views: open a graphical view.");
            if (views.Count > 8) throw new BridgeException("Capture at most 8 views per call.");

            var fmt = (Json.Str(p, "format", "png") ?? "png").ToLowerInvariant();
            int pixels = (int)Math.Max(256, Math.Min(6000, Json.Int(p, "pixelSize", 1600)));
            var dir = Path.Combine(BurakPaths.Sub("captures"), "revit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(dir);

            var results = new List<object>();
            int n = 0;
            foreach (var v in views)
            {
                n++;
                var sub = Path.Combine(dir, n.ToString("00"));
                Directory.CreateDirectory(sub);
                var opt = new ImageExportOptions
                {
                    FilePath = Path.Combine(sub, "view"),
                    ExportRange = ExportRange.SetOfViews,
                    HLRandWFViewsFileType = fmt == "jpg" || fmt == "jpeg" ? ImageFileType.JPEGMedium : ImageFileType.PNG,
                    ShadowViewsFileType = fmt == "jpg" || fmt == "jpeg" ? ImageFileType.JPEGMedium : ImageFileType.PNG,
                    PixelSize = pixels,
                    FitDirection = FitDirectionType.Horizontal,
                    ZoomType = ZoomFitType.FitToPage,
                    ImageResolution = ImageResolution.DPI_150
                };
                opt.SetViewsAndSheets(new List<ElementId> { v.Id });
                string file = null; string error = null;
                try
                {
                    doc.ExportImage(opt);
                    file = Directory.GetFiles(sub).Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
                    if (file == null) error = "Revit produced no image for this view.";
                }
                catch (Exception ex) { error = ex.Message; }
                results.Add(Json.O("viewId", Ids.Of(v.Id), "view", v.Name, "type", v.ViewType.ToString(), "file", file, "error", error, "facts", Facts(doc, v)));
            }
            return Json.O("images", results, "folder", dir,
                "note", "Images show the view exactly as Revit renders it (visibility/graphics, crop, detail level, phase).");
        }

        /// <summary>Short context so the AI can interpret the picture (scale, level, discipline, crop, element counts).</summary>
        static Dictionary<string, object> Facts(Document doc, View v)
        {
            var d = Json.O();
            try { d["scale"] = v.Scale; } catch { }
            try { d["detailLevel"] = v.DetailLevel.ToString(); } catch { }
            try { d["displayStyle"] = v.DisplayStyle.ToString(); } catch { }
            try { d["discipline"] = v.Discipline.ToString(); } catch { }
            try { d["cropBoxActive"] = v.CropBoxActive; } catch { }
            try { if (v.GenLevel != null) d["level"] = v.GenLevel.Name; } catch { }
            try
            {
                var sheet = v as ViewSheet;
                if (sheet != null) { d["sheetNumber"] = sheet.SheetNumber; d["viewports"] = sheet.GetAllViewports().Count; }
            }
            catch { }
            try
            {
                if (!(v is ViewSheet))
                {
                    var counts = new FilteredElementCollector(doc, v.Id).WhereElementIsNotElementType().ToElements()
                        .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                        .GroupBy(e => e.Category.Name).OrderByDescending(g => g.Count()).Take(12)
                        .ToDictionary(g => g.Key, g => (object)g.Count());
                    d["visibleModelElements"] = counts;
                }
            }
            catch { }
            return d;
        }
    }
}
