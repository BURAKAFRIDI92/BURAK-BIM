using BurakBim.Shared;

namespace BurakBim.Server.Core;

/// <summary>Append-only audit trail of every AI action: audit\YYYY-MM-DD.jsonl (one JSON object per line).</summary>
public static class Audit
{
    static readonly object Gate = new();

    public static void Write(ToolContext ctx, Dictionary<string, object> args, bool ok, long ms, object? result, string? error, bool writes)
    {
        try
        {
            var argText = Json.Serialize(args);
            if (argText.Length > 4000) argText = argText[..4000] + "...";
            string? summary = null;
            if (result != null && result is not ImageResult)
            {
                summary = Json.Serialize(result);
                if (summary.Length > 800) summary = summary[..800] + "...";
            }
            var rec = Json.O(
                "ts", DateTime.UtcNow,
                "local", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "user", Environment.UserName,
                "machine", Environment.MachineName,
                "client", ctx.Client,
                "session", ctx.Session,
                "tool", ctx.ToolName,
                "writes", writes,
                "ok", ok,
                "ms", ms,
                "args", new JsonRaw(argText.StartsWith("{") ? argText : Json.Serialize(argText)),
                "result", summary,
                "error", error);
            var line = Json.Serialize(rec);
            lock (Gate)
                File.AppendAllText(Path.Combine(BurakPaths.AuditDir, DateTime.Now.ToString("yyyy-MM-dd") + ".jsonl"), line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[BURAK BIM] audit write failed: " + ex.Message);
        }
    }

    public static List<Dictionary<string, object>> Read(DateTime day, Func<Dictionary<string, object>, bool>? filter, int limit)
    {
        var file = Path.Combine(BurakPaths.AuditDir, day.ToString("yyyy-MM-dd") + ".jsonl");
        var list = new List<Dictionary<string, object>>();
        if (!File.Exists(file)) return list;
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            try
            {
                var o = Json.ParseObject(line);
                if (filter == null || filter(o)) list.Add(o);
            }
            catch { }
        }
        return list.Count > limit ? list.Skip(list.Count - limit).ToList() : list;
    }
}
