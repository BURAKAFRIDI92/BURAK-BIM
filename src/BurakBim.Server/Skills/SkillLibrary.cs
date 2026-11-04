using System.Text.RegularExpressions;
using BurakBim.Server.Core;
using BurakBim.Shared;
using static BurakBim.Server.Core.S;

namespace BurakBim.Server.Skills;

/// <summary>
/// 1.3 BIM WORKFLOW SKILLS - "the connector gives the AI the tools, the skills give it the method".
/// Each skill (skills\&lt;name&gt;\SKILL.md, embedded in the server) is the step-by-step method an experienced BIM / VDC
/// professional follows: model health, documentation, ISO 19650 document control, architecture / structure / MEP
/// modelling, scan-to-BIM, coordination, CSI analysis, Bluebeam / PlanSwift takeoff, AutoCAD cut files, ...
/// Delivered to EVERY AI app three ways: the burak_skills tool, MCP prompts and MCP resources (burak://skills/...),
/// plus as Claude Code skills (copied by the installer). Company skills: drop SKILL.md folders into &lt;install&gt;\skills.
/// </summary>
public static class SkillLibrary
{
    public sealed record Skill(string Name, string Description, string Discipline, string Markdown, string Body, bool Custom);

    static List<Skill>? _cache;
    static readonly Regex Front = new(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline | RegexOptions.Compiled);

    public static List<Skill> All()
    {
        if (_cache != null) return _cache;
        var list = new List<Skill>();
        var asm = typeof(SkillLibrary).Assembly;
        foreach (var res in asm.GetManifestResourceNames().Where(n => n.StartsWith("skills/", StringComparison.OrdinalIgnoreCase) && n.EndsWith("SKILL.md", StringComparison.OrdinalIgnoreCase)))
        {
            using var s = asm.GetManifestResourceStream(res);
            if (s == null) continue;
            using var r = new StreamReader(s);
            var md = r.ReadToEnd();
            var folder = res.Substring(7).Split('\\', '/')[0];
            var sk = Parse(md, folder, false);
            if (sk != null) list.Add(sk);
        }
        // company / user skills: <install>\skills\<name>\SKILL.md (override built-ins with the same name)
        try
        {
            var dir = Path.Combine(BurakPaths.Root, "skills");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "SKILL.md", SearchOption.AllDirectories))
                {
                    var sk = Parse(File.ReadAllText(f), Path.GetFileName(Path.GetDirectoryName(f)!) ?? "custom", true);
                    if (sk == null) continue;
                    list.RemoveAll(x => x.Name.Equals(sk.Name, StringComparison.OrdinalIgnoreCase));
                    list.Add(sk);
                }
        }
        catch { }
        return _cache = list.OrderBy(x => x.Name).ToList();
    }

    static Skill? Parse(string md, string folder, bool custom)
    {
        md = md.Replace("\r\n", "\n");
        var m = Front.Match(md);
        string name = folder, desc = "", disc = "";
        var body = md;
        if (m.Success)
        {
            foreach (var line in m.Groups[1].Value.Split('\n'))
            {
                var t = line.Trim();
                string Val(string k) => t.Substring(k.Length).Trim().Trim('"');
                if (t.StartsWith("name:")) name = Val("name:");
                else if (t.StartsWith("description:")) desc = Val("description:");
                else if (t.StartsWith("discipline:")) disc = Val("discipline:");
            }
            body = md[m.Length..];
        }
        return new Skill(name, desc, disc, md, body.Trim(), custom);
    }

    public static Skill? Get(string name) =>
        All().FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? All().FirstOrDefault(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Best skills for a task description (keyword match on name, description, discipline).</summary>
    public static List<Skill> Suggest(string task, int max = 3)
    {
        var words = task.ToLowerInvariant().Split(new[] { ' ', ',', '.', '-', '/', '_' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToList();
        return All().Select(s =>
        {
            var hay = (s.Name + " " + s.Description + " " + s.Discipline).ToLowerInvariant();
            return (s, score: words.Sum(w => hay.Contains(w) ? (s.Name.Contains(w) ? 3 : 1) : 0));
        }).Where(x => x.score > 0).OrderByDescending(x => x.score).Take(max).Select(x => x.s).ToList();
    }

    // ------------------------------------------------------------------ MCP surfaces
    public static List<object> Prompts() => All().Select(s => (object)Json.O(
        "name", "skill-" + s.Name, "title", "BIM skill: " + s.Name, "description", s.Description,
        "arguments", new List<object> { Json.O("name", "task", "description", "What to do (project specifics)", "required", false) })).ToList();

    public static object? Prompt(string promptName, Dictionary<string, object>? args)
    {
        if (!promptName.StartsWith("skill-")) return null;
        var s = Get(promptName[6..]);
        if (s == null) return null;
        var task = Json.Str(args, "task");
        var text = "Use BURAK BIM and follow this skill exactly.\n\n" + s.Body + (string.IsNullOrWhiteSpace(task) ? "" : "\n\nTASK: " + task);
        return Json.O("description", s.Description, "messages", new List<object> { Json.O("role", "user", "content", Json.O("type", "text", "text", text)) });
    }

    public static List<object> Resources() => All().Select(s => (object)Json.O(
        "uri", "burak://skills/" + s.Name, "name", s.Name, "title", "BIM skill: " + s.Name, "description", s.Description, "mimeType", "text/markdown")).ToList();

    public static object? Resource(string uri)
    {
        if (!uri.StartsWith("burak://skills/")) return null;
        var s = Get(uri["burak://skills/".Length..]);
        return s == null ? null : Json.O("contents", new List<object> { Json.O("uri", uri, "mimeType", "text/markdown", "text", s.Markdown) });
    }

    public static void Register(ToolRegistry r)
    {
        r.Add(new Tool
        {
            Name = "burak_skills", Title = "BIM workflow skills (the method)", Toolset = "core", ReadOnly = true,
            Description = "Step-by-step professional methods for BIM tasks - read the matching skill BEFORE multi-step work: bim-base (always), bim-vdc-router (maps any BIM/VDC responsibility to its skill), bim-project-setup, bim-qa-gate, bim-handover, vdc-4d-scheduling, vdc-5d-quantities, drawing-review-cycle, rhino-grasshopper-computational, revit-model-health, revit-documentation, " +
                          "revit-document-control-iso19650, revit-architecture-modeling, revit-structure-modeling, revit-mep-modeling, revit-scan-to-bim, revit-family-creation, bim-coordination (Navisworks clashes), " +
                          "csi-structural-analysis (ETABS/SAP2000), bluebeam-takeoff, planswift-takeoff, autocad-civil3d-base, autocad-cut-files, civil3d-site, archicad-modeling, computer-control. " +
                          "action=list | get (name) | suggest (task text).",
            InputSchema = Obj(O("action", Enum("Action (default suggest when task is given, else list)", "list", "get", "suggest")), O("name", Str("Skill name for get")), O("task", Str("Task description for suggest"))),
            Handler = (a, ctx) =>
            {
                var action = Json.Str(a, "action") ?? (Json.Str(a, "task") != null ? "suggest" : Json.Str(a, "name") != null ? "get" : "list");
                switch (action)
                {
                    case "get":
                        var s = Get(Json.Str(a, "name") ?? "") ?? throw new ToolException("No skill '" + Json.Str(a, "name") + "'. Use action=list.");
                        return s.Markdown;
                    case "suggest":
                        var hits = Suggest(Json.Str(a, "task") ?? "");
                        if (hits.Count == 0) return Json.O("suggested", new List<object>(), "next", "No specific skill matched - follow bim-base (burak_skills action=get name=bim-base).");
                        return Json.O("suggested", hits.Select(h => (object)Json.O("name", h.Name, "description", h.Description)).ToList(),
                            "best", hits[0].Markdown);
                    default:
                        return Json.O("skills", All().Select(x => (object)Json.O("name", x.Name, "discipline", x.Discipline, "description", x.Description, "custom", x.Custom)).ToList(),
                            "next", "burak_skills action=get name=<skill> - then follow it step by step.",
                            "custom", "Add company skills as <install>\\skills\\<name>\\SKILL.md (Markdown with name/description front matter).");
                }
            }
        });
    }
}
