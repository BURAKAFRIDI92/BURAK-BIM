namespace BurakBim.Server.Core;

/// <summary>BURAK BIM company logo, embedded once and reused (MCP server icon, HTML reports, ...).</summary>
public static class Brand
{
    static readonly Dictionary<string, string> Cache = new();

    public static string Base64(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var b)) return b;
            using var s = typeof(Brand).Assembly.GetManifestResourceStream("brand." + name);
            if (s == null) return Cache[name] = "";
            using var ms = new MemoryStream(); s.CopyTo(ms);
            return Cache[name] = Convert.ToBase64String(ms.ToArray());
        }
    }

    public static string DataUri(string name) { var b = Base64(name); return b.Length == 0 ? "" : "data:image/png;base64," + b; }

    /// <summary>Square mark used as the MCP server icon in AI clients.</summary>
    public static string Icon => DataUri("burakbim-64.png");
    /// <summary>Full logo (mark + BURAK-BIM wordmark + tagline) for reports.</summary>
    public static string Logo => DataUri("burakbim-logo-220.png");
    public const string Tagline = "Building a Smarter Tomorrow";
}
