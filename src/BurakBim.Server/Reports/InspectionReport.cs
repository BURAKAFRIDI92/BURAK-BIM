using System.Net;
using System.Text;
using BurakBim.Server.Bridges;
using BurakBim.Server.Core;
using BurakBim.Shared;

namespace BurakBim.Server.Reports;

/// <summary>HTML report for BIM inspectors: AI-created elements, review status, scan deviation, audit trail.</summary>
public static class InspectionReport
{
    static string H(object? s) => WebUtility.HtmlEncode(Convert.ToString(s, System.Globalization.CultureInfo.InvariantCulture) ?? "");

    public static object Build(Dictionary<string, object> a, ToolContext ctx)
    {
        var target = Json.Str(a, "target");
        var info = BridgeClient.Call("revit", target, "revit.info", null, ctx) as Dictionary<string, object> ?? new();
        var list = BridgeClient.Call("revit", target, "revit.inspector.list", Json.O("limit", 20000), ctx) as Dictionary<string, object> ?? new();
        var elements = (Json.Arr(list, "elements") ?? new()).OfType<Dictionary<string, object>>().ToList();
        var counts = Json.Obj(list, "counts") ?? new();
        var audit = Audit.Read(DateTime.Now, o => Json.Bool(o, "writes"), 500);
        var model = Json.Str(info, "activeDocument", "model");
        var title = Json.Str(a, "title") ?? $"BURAK BIM Inspection Report - {model}";
        var inspector = Json.Str(a, "inspector") ?? Json.Str(Config.Data, "inspectorName", "");

        int outTol = elements.Count(e => Json.Str(Json.Obj(Json.Obj(e, "stamp"), "scanDeviation"), "status") == "out-of-tolerance");
        int lod500 = elements.Count(e => Json.Str(Json.Obj(e, "stamp"), "status") == "verified" && Json.Str(Json.Obj(e, "stamp"), "lod") == "500");

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(H(title)).Append("</title><style>")
          .Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#1b1f24}h1{margin:0 0 4px}small{color:#666}")
          .Append(".k{display:inline-block;margin:12px 12px 12px 0;padding:12px 18px;border-radius:8px;background:#f2f4f7}.k b{display:block;font-size:26px}")
          .Append("table{border-collapse:collapse;width:100%;font-size:13px;margin-top:8px}td,th{border:1px solid #ddd;padding:5px 7px;text-align:left}th{background:#1b1f24;color:#fff}")
          .Append(".unverified{color:#c62828;font-weight:600}.verified{color:#2e7d32;font-weight:600}.rejected{color:#ef6c00;font-weight:600}.bad{color:#7b1fa2;font-weight:600}")
          .Append("</style></head><body>");
        sb.Append($"<header style='display:flex;align-items:center;gap:18px;border-bottom:3px solid #1f6b4f;padding-bottom:10px;margin-bottom:10px'><img src='{Core.Brand.Logo}' alt='BURAK-BIM' style='height:110px'><div><h1>{H(title)}</h1><div style='color:#1f6b4f;letter-spacing:2px;font-size:12px'>{Core.Brand.Tagline.ToUpperInvariant()}</div></div></header>");
        sb.Append($"<small>Generated {DateTime.Now:yyyy-MM-dd HH:mm} by BURAK BIM {BurakInfo.Version} - {H(Json.Str(info, "product"))} - inspector: {H(inspector)}</small>");
        sb.Append("<div>");
        sb.Append($"<div class='k'><b>{elements.Count}</b>AI-created elements</div>");
        foreach (var s in new[] { "unverified", "verified", "rejected" })
            sb.Append($"<div class='k'><b class='{s}'>{(counts.TryGetValue(s, out var c) ? c : 0)}</b>{s}</div>");
        sb.Append($"<div class='k'><b class='bad'>{outTol}</b>out of scan tolerance</div><div class='k'><b>{lod500}</b>verified at LOD 500</div></div>");

        sb.Append("<h2>Elements</h2><table><tr><th>Id</th><th>Category</th><th>Name</th><th>Status</th><th>Created by</th><th>Created</th><th>Action</th><th>Scan p95 (mm)</th><th>Inspector</th><th>LOD</th><th>Note</th></tr>");
        foreach (var e in elements.OrderBy(e => Json.Str(Json.Obj(e, "stamp"), "status")).ThenBy(e => Json.Str(e, "category")))
        {
            var st = Json.Obj(e, "stamp") ?? new();
            var status = Json.Str(st, "status", "unverified");
            var dev = Json.Obj(st, "scanDeviation");
            var devTxt = dev == null ? "" : $"{Json.Str(dev, "p95Mm", "-")} ({Json.Str(dev, "status")})";
            sb.Append($"<tr><td>{H(Json.Str(e, "id"))}</td><td>{H(Json.Str(e, "category"))}</td><td>{H(Json.Str(e, "name"))}</td>")
              .Append($"<td class='{H(status)}'>{H(status)}</td><td>{H(Json.Str(st, "client"))}</td><td>{H(Json.Str(st, "createdUtc"))}</td><td>{H(Json.Str(st, "action"))}</td>")
              .Append($"<td class='{(dev != null && Json.Str(dev, "status") == "out-of-tolerance" ? "bad" : "")}'>{H(devTxt)}</td><td>{H(Json.Str(st, "inspector"))}</td><td>{H(Json.Str(st, "lod"))}</td><td>{H(Json.Str(st, "note"))}</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<h2>Today's AI actions that changed models</h2><table><tr><th>Time</th><th>User</th><th>AI client</th><th>Tool</th><th>OK</th><th>ms</th><th>Arguments</th></tr>");
        foreach (var r in audit)
        {
            var args = Json.Serialize(Json.Get(r, "args"));
            if (args.Length > 300) args = args[..300] + "...";
            sb.Append($"<tr><td>{H(Json.Str(r, "local"))}</td><td>{H(Json.Str(r, "user"))}</td><td>{H(Json.Str(r, "client"))}</td><td>{H(Json.Str(r, "tool"))}</td><td>{(Json.Bool(r, "ok") ? "yes" : "<span class='rejected'>no</span>")}</td><td>{H(Json.Str(r, "ms"))}</td><td><code>{H(args)}</code></td></tr>");
        }
        sb.Append("</table><p><small>LOD note: elements generated automatically from scans are LOD 300-350 geometry. Only inspector-verified elements within scan tolerance may be reported as LOD 500 (field-verified as-built).</small></p></body></html>");

        var safe = string.Concat((model ?? "model").Split(Path.GetInvalidFileNameChars()));
        var file = Path.Combine(BurakPaths.ReportsDir, $"Inspection-{safe}-{DateTime.Now:yyyyMMdd-HHmm}.html");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        return Json.O("report", file, "aiElements", elements.Count, "byStatus", counts, "outOfTolerance", outTol, "auditEntries", audit.Count);
    }
}
