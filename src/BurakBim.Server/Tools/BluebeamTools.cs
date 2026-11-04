using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using BurakBim.Server.Core;
using BurakBim.Server.Detection;
using BurakBim.Shared;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Tools;

/// <summary>
/// 1.3: Bluebeam Revu connector (Revu 2017 - 21+, Standard / CAD / eXtreme / Core / Basics / Complete).
///  - bluebeam_markups / bluebeam_takeoff_summary read markups and measurements straight from the PDF
///    (Bluebeam stores them as PDF annotations with subject, author, layer, custom columns), so they work with every
///    Revu edition and even without Revu running.
///  - bluebeam_script runs Bluebeam Script commands through ScriptEngine.exe (eXtreme / Complete editions).
///  - bluebeam_open opens a PDF in Revu; anything else in Revu's UI: capture + computer_* tools (with permission).
/// PDF tools (pdf_info, pdf_text) work for any PDF (drawings, specs, submittals).
/// </summary>
public static class BluebeamTools
{
    static void Add(ToolRegistry r, string set, string name, string title, string desc, bool ro, Dictionary<string, object> schema, Func<Dictionary<string, object>, object?> h, int prio = 0)
        => r.Add(new Tool { Name = name, Title = title, Toolset = set, Description = desc, ReadOnly = ro, InputSchema = schema, Handler = (a, c) => h(a), Priority = prio });

    static Product? Revu() => SoftwareDetector.Detect().Where(p => p.Family == "bluebeam" && p.Exe != null).OrderByDescending(p => p.Year).FirstOrDefault();

    static string? ScriptEngine()
    {
        var revu = Revu();
        if (revu?.InstallDir == null) return null;
        foreach (var d in new[] { revu.InstallDir, Path.GetDirectoryName(revu.InstallDir) ?? "" })
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
            var f = Directory.EnumerateFiles(d, "ScriptEngine.exe", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).FirstOrDefault();
            if (f != null) return f;
        }
        return null;
    }

    static string PdfPath(Dictionary<string, object> a)
    {
        var p = Json.Str(a, "path") ?? throw new ToolException("path (PDF file) required.");
        if (!File.Exists(p)) throw new ToolException("File not found: " + p);
        return p;
    }

    public static void Register(ToolRegistry r)
    {
        Add(r, "bluebeam", "bluebeam_status", "Bluebeam Revu status",
            "Installed Revu versions/editions, whether Revu is running and which PDFs are open (window titles), and whether scripting (ScriptEngine) is available.",
            true, Obj(), a =>
            {
                var installed = SoftwareDetector.Detect().Where(p => p.Family == "bluebeam").Select(p => (object)Json.O("name", p.Name, "version", p.Version, "exe", p.Exe)).ToList();
                var open = Process.GetProcessesByName("Revu").Select(p => { try { return p.MainWindowTitle; } catch { return ""; } }).Where(t => t.Length > 0).Cast<object>().ToList();
                return Json.O("installed", installed, "running", open.Count > 0, "windows", open, "scriptEngine", ScriptEngine(),
                    "tip", "Markups/measurements: bluebeam_markups and bluebeam_takeoff_summary (read the PDF). UI steps: capture app=bluebeam, then computer_* with the user's permission.");
            }, 80);

        Add(r, "bluebeam", "bluebeam_open", "Open a PDF in Bluebeam Revu",
            "Open a PDF (drawing set, spec) in Bluebeam Revu so the user and you can work on it.", false,
            Obj(R("path", Str("PDF path"))), a =>
            {
                var pdf = PdfPath(a);
                var exe = Revu()?.Exe ?? throw new ToolException("Bluebeam Revu is not installed (or not detected; run burak_status refresh=true).");
                Process.Start(new ProcessStartInfo(exe, "\"" + pdf + "\"") { UseShellExecute = false });
                Thread.Sleep(2500);
                return Json.O("opened", pdf, "with", exe);
            });

        Add(r, "bluebeam", "bluebeam_markups", "Bluebeam markups and measurements (from the PDF)",
            "Every markup in a PDF: page, type, subject, author, label/comment, measurement text and value, layer, color, date, status and Bluebeam custom columns. " +
            "Works for any Revu edition (reads the PDF directly - save in Revu first). Filters: pages, subject, author, type.",
            true, Obj(R("path", Str("PDF path")), O("pages", Arr("Page numbers (1-based)", Json.O("type", "integer"))), O("subject", Str("Subject contains")),
                O("author", Str("Author contains")), O("type", Str("Annotation type contains, e.g. Polygon, PolyLine, Square, Line, FreeText, Stamp")), O("limit", Int("Max markups (default 2000)"))),
            a => Markups(a), 85);

        Add(r, "bluebeam", "bluebeam_takeoff_summary", "Bluebeam takeoff summary (quantities by subject)",
            "Quantity takeoff from Bluebeam measurement markups: totals of length / area / volume / count grouped by subject (and optionally by page or layer) with units, like Revu's Markups List summary.",
            true, Obj(R("path", Str("PDF path")), O("groupBy", Enum("Second grouping", "none", "page", "layer", "author")), O("pages", Arr("Page numbers", Json.O("type", "integer")))),
            a =>
            {
                var res = (Dictionary<string, object>)Markups(Json.O("path", Json.Get(a, "path"), "pages", Json.Get(a, "pages"), "limit", 100000));
                var by = Json.Str(a, "groupBy") ?? "none";
                var groups = new Dictionary<string, Dictionary<string, object>>();
                foreach (var m in (Json.Arr(res, "markups") ?? new List<object>()).OfType<Dictionary<string, object>>())
                {
                    var subj = Json.Str(m, "subject") ?? Json.Str(m, "type") ?? "?";
                    var g2 = by switch { "page" => "page " + Json.Int(m, "page"), "layer" => Json.Str(m, "layer") ?? "-", "author" => Json.Str(m, "author") ?? "-", _ => "" };
                    var key = g2.Length > 0 ? subj + " | " + g2 : subj;
                    if (!groups.TryGetValue(key, out var g)) groups[key] = g = Json.O("subject", subj, "group", g2.Length > 0 ? g2 : null, "count", 0L, "totals", new Dictionary<string, object>());
                    g["count"] = Json.Int(g, "count") + 1;
                    if (Json.Get(m, "value") is double v && Json.Str(m, "unit") is { } u)
                    {
                        var t = (Dictionary<string, object>)g["totals"];
                        t[u] = Math.Round(Json.Num(t, u) + v, 4);
                    }
                }
                return Json.O("file", Json.Get(res, "file"), "groups", groups.Values.OrderBy(x => Json.Str(x, "subject")).Cast<object>().ToList(),
                    "note", "Totals come from the measurement values stored in each markup (Bluebeam's units). Count markups have count only.");
            }, 80);

        Add(r, "bluebeam", "bluebeam_script", "Run a Bluebeam Script (eXtreme / Complete)",
            "Run Bluebeam Script commands with ScriptEngine.exe on a PDF: e.g. Flatten(), PageRotate(...), Stamp(...), HeaderAndFooter(...), Export(...), Import('markups.bax'), PrintToFile(...), ReduceFileSize(...). " +
            "path is opened first and saved afterwards unless save=false. Needs Revu eXtreme/Complete; otherwise use the UI with computer_* tools.",
            false, Obj(R("commands", Arr("Script commands in order, e.g. [\"Flatten()\"]", Json.O("type", "string"))), O("path", Str("PDF to process")),
                O("saveAs", Str("Save to this path instead of overwriting")), O("save", Bool("Save the PDF after the commands (default true)")), O("timeoutSeconds", Int("Default 300"))),
            a =>
            {
                var engine = ScriptEngine() ?? throw new ToolException("ScriptEngine.exe was not found (Bluebeam scripting needs Revu eXtreme or Complete). Use capture + computer_* tools to do this in Revu's UI.");
                var cmds = Json.StrList(a, "commands");
                if (cmds.Count == 0) throw new ToolException("commands required.");
                var parts = new List<string>();
                if (Json.Str(a, "path") is { } p) parts.Add($"Open('{p.Replace("'", "\\'")}')");
                parts.AddRange(cmds);
                if (Json.Str(a, "path") != null && Json.Bool(a, "save", true))
                {
                    parts.Add(Json.Str(a, "saveAs") is { } sa ? $"Save('{sa.Replace("'", "\\'")}')" : "Save()");
                    parts.Add("Close()");
                }
                var bci = Path.Combine(BurakPaths.ScratchDir, "bluebeam-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bci");
                File.WriteAllLines(bci, parts);
                var psi = new ProcessStartInfo(engine, $"\"Script('{bci}')\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var proc = Process.Start(psi)!;
                var outTask = proc.StandardOutput.ReadToEndAsync(); var errTask = proc.StandardError.ReadToEndAsync();
                if (!proc.WaitForExit((int)Json.Int(a, "timeoutSeconds", 300) * 1000)) { try { proc.Kill(); } catch { } throw new ToolException("Bluebeam script timed out."); }
                return Json.O("exitCode", proc.ExitCode, "output", outTask.Result.Trim(), "errors", errTask.Result.Trim(), "script", parts);
            });

        // ------------------------------------------------------------------ generic PDF (drawings, specs)
        Add(r, "pdf", "pdf_info", "PDF info: pages, sizes, title",
            "Page count, page sizes (points, inches, likely sheet size), metadata and number of markups per page of any PDF.", true,
            Obj(R("path", Str("PDF path"))), a =>
            {
                using var doc = PdfDocument.Open(PdfPath(a));
                var pages = new List<object>();
                for (int i = 1; i <= doc.NumberOfPages; i++)
                {
                    var pg = doc.GetPage(i);
                    int ann = 0; try { ann = pg.ExperimentalAccess.GetAnnotations().Count(); } catch { }
                    pages.Add(Json.O("page", i, "widthPt", Math.Round(pg.Width, 1), "heightPt", Math.Round(pg.Height, 1),
                        "inches", $"{pg.Width / 72.0:0.##} x {pg.Height / 72.0:0.##}", "markups", ann));
                    if (pages.Count >= 500) break;
                }
                return Json.O("pages", doc.NumberOfPages, "title", doc.Information.Title, "author", doc.Information.Author, "creator", doc.Information.Creator,
                    "producer", doc.Information.Producer, "pageList", pages);
            });

        Add(r, "pdf", "pdf_text", "PDF text of pages (specs, title blocks, schedules)",
            "Extract the text of PDF pages (drawing title blocks, notes, specifications). search returns only pages containing the text.", true,
            Obj(R("path", Str("PDF path")), O("pages", Arr("Page numbers (default first 20)", Json.O("type", "integer"))), O("search", Str("Only pages containing this text")), O("maxChars", Int("Max characters per page (default 6000)"))),
            a =>
            {
                using var doc = PdfDocument.Open(PdfPath(a));
                var want = Json.IntList(a, "pages").Select(x => (int)x).ToList();
                if (want.Count == 0) want = Enumerable.Range(1, Math.Min(20, doc.NumberOfPages)).ToList();
                var search = Json.Str(a, "search"); int max = (int)Json.Int(a, "maxChars", 6000);
                var outp = new List<object>();
                foreach (var i in want.Where(i => i >= 1 && i <= doc.NumberOfPages))
                {
                    var text = string.Join(" ", doc.GetPage(i).GetWords().Select(w => w.Text));
                    if (search != null && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    outp.Add(Json.O("page", i, "text", text.Length > max ? text[..max] + " ..." : text));
                }
                return Json.O("pages", outp);
            });
    }

    // ================================================================================ PDF markup reader
    static readonly Regex MeasureRx = new(@"(-?\d[\d,]*(?:\.\d+)?)\s*(sq\s?ft|sf|ft²|ft2|sq\s?m|m²|m2|sq\s?in|in²|cu\s?yd|cy|yd³|cu\s?ft|cf|ft³|m³|m3|lf|ft|feet|'|in|""|mm|cm|m|ea|count)(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex FeetInchRx = new(@"(-?\d+)'\s*-?\s*(\d+(?:\.\d+)?)(?:\s*(\d+)/(\d+))?""", RegexOptions.Compiled);

    static object Markups(Dictionary<string, object> a)
    {
        var path = PdfPath(a);
        var pages = Json.IntList(a, "pages").Select(x => (int)x).ToHashSet();
        string? subj = Json.Str(a, "subject"), author = Json.Str(a, "author"), type = Json.Str(a, "type");
        int limit = (int)Json.Int(a, "limit", 2000);
        var list = new List<object>();
        int total = 0;
        using var doc = PdfDocument.Open(path);
        for (int i = 1; i <= doc.NumberOfPages && list.Count < limit; i++)
        {
            if (pages.Count > 0 && !pages.Contains(i)) continue;
            var page = doc.GetPage(i);
            IEnumerable<UglyToad.PdfPig.Annotations.Annotation> anns;
            try { anns = page.ExperimentalAccess.GetAnnotations().ToList(); } catch { continue; }
            foreach (var an in anns)
            {
                var t = an.Type.ToString();
                if (t is "Popup" or "Link" or "Widget") continue;
                total++;
                var d = an.AnnotationDictionary;
                var s = Tok(d, "Subj") ?? t;
                var au = Tok(d, "T");
                if (subj != null && s.IndexOf(subj, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (author != null && (au ?? "").IndexOf(author, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (type != null && t.IndexOf(type, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var contents = an.Content ?? Tok(d, "Contents");
                var m = Json.O("page", i, "type", t, "subject", s, "author", au, "contents", contents, "label", Tok(d, "BSILabel") ?? Tok(d, "NM"),
                    "layer", Tok(d, "BSILayer") ?? Tok(d, "OC"), "status", Tok(d, "BSIStatus"), "date", an.ModifiedDate,
                    "intent", Tok(d, "IT"), "color", Tok(d, "C"),
                    "rect", new List<object> { Math.Round(an.Rectangle.Left, 1), Math.Round(an.Rectangle.Bottom, 1), Math.Round(an.Rectangle.Width, 1), Math.Round(an.Rectangle.Height, 1) });
                var cols = Tok(d, "BSIColumnData");
                if (cols != null) m["columns"] = cols;
                ParseMeasure(contents, m);
                list.Add(m);
                if (list.Count >= limit) break;
            }
        }
        return Json.O("file", path, "pages", doc.NumberOfPages, "markupsTotal", total, "returned", list.Count, "markups", list);
    }

    static void ParseMeasure(string? text, Dictionary<string, object> m)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var fi = FeetInchRx.Match(text);
        if (fi.Success)
        {
            double ft = double.Parse(fi.Groups[1].Value, CultureInfo.InvariantCulture);
            double inch = double.Parse(fi.Groups[2].Value, CultureInfo.InvariantCulture);
            if (fi.Groups[3].Success) inch += double.Parse(fi.Groups[3].Value, CultureInfo.InvariantCulture) / Math.Max(1, double.Parse(fi.Groups[4].Value, CultureInfo.InvariantCulture));
            m["value"] = Math.Round(ft + Math.Sign(ft == 0 ? 1 : ft) * inch / 12.0, 4); m["unit"] = "ft";
            return;
        }
        var mm = MeasureRx.Match(text);
        if (!mm.Success) return;
        if (!double.TryParse(mm.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
        var u = mm.Groups[2].Value.ToLowerInvariant().Replace(" ", "");
        u = u switch
        {
            "sqft" or "ft²" or "ft2" => "sf", "sqm" or "m²" => "m2", "sqin" or "in²" => "in2", "cuyd" or "yd³" => "cy", "cuft" or "ft³" => "cf", "m³" => "m3",
            "feet" or "'" => "ft", "\"" => "in", "count" => "ea", _ => u
        };
        m["value"] = v; m["unit"] = u;
    }

    /// <summary>Annotation dictionary value as text (strings, names, numbers, arrays).</summary>
    static string? Tok(DictionaryToken d, string key)
    {
        try
        {
            if (!d.TryGet(NameToken.Create(key), out var tok) || tok == null) return null;
            return TokText(tok, 0);
        }
        catch { return null; }
    }

    static string? TokText(IToken tok, int depth) => tok switch
    {
        StringToken st => st.Data,
        HexToken ht => ht.Data,
        NameToken nt => nt.Data,
        NumericToken num => num.Double.ToString("0.###", CultureInfo.InvariantCulture),
        BooleanToken bt => bt.Data ? "true" : "false",
        ArrayToken at when depth < 3 => string.Join(" | ", at.Data.Select(x => TokText(x, depth + 1)).Where(x => !string.IsNullOrEmpty(x))),
        _ => null
    };
}
