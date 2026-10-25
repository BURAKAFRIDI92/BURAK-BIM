using System.Collections.Generic;
using BurakBim.Shared;

namespace BurakBim.Server.Core;

/// <summary>Context for one tools/call.</summary>
public sealed class ToolContext
{
    public required string ToolName { get; init; }
    public required string Session { get; init; }
    public required string Client { get; init; }
}

/// <summary>Returned by a tool when it wants to send an image (screenshots) instead of JSON text.</summary>
public sealed class ImageResult
{
    public required string Base64 { get; init; }
    public string MimeType { get; init; } = "image/png";
    public string? Caption { get; init; }
}

public sealed class Tool
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    /// <summary>Toolset id used by the "toolsets" config: core, revit, scan, inspector, autocad, navisworks, inventor, recap, com, ui.</summary>
    public required string Toolset { get; init; }
    public Dictionary<string, object> InputSchema { get; init; } = S.Obj();
    public bool ReadOnly { get; init; }
    public bool Destructive { get; init; }
    public bool OpenWorld { get; init; }
    /// <summary>Tool-pack / generated catalog tool: listed in full exposure, found via burak_tools + burak_run in compact exposure.</summary>
    public bool Extended { get; init; }
    public required Func<Dictionary<string, object>, ToolContext, object?> Handler { get; init; }

    public Dictionary<string, object> ToJson() => Json.O(
        "name", Name,
        "title", Title,
        "description", Description,
        "inputSchema", InputSchema,
        "annotations", Json.O("title", Title, "readOnlyHint", ReadOnly, "destructiveHint", Destructive,
            "idempotentHint", ReadOnly, "openWorldHint", OpenWorld));
}

/// <summary>Tiny JSON-schema builder.</summary>
public static class S
{
    public static Dictionary<string, object> Obj(params (string name, Dictionary<string, object> schema, bool required)[] props)
    {
        var p = new Dictionary<string, object>();
        var req = new List<object>();
        foreach (var (name, schema, required) in props)
        {
            p[name] = schema;
            if (required) req.Add(name);
        }
        var o = Json.O("type", "object", "properties", p);
        if (req.Count > 0) o["required"] = req;
        return o;
    }

    public static Dictionary<string, object> Str(string d) => Json.O("type", "string", "description", d);
    public static Dictionary<string, object> Enum(string d, params string[] values) => Json.O("type", "string", "description", d, "enum", values.Cast<object>().ToList());
    public static Dictionary<string, object> Num(string d) => Json.O("type", "number", "description", d);
    public static Dictionary<string, object> Int(string d) => Json.O("type", "integer", "description", d);
    public static Dictionary<string, object> Bool(string d) => Json.O("type", "boolean", "description", d);
    public static Dictionary<string, object> Arr(string d, Dictionary<string, object>? items = null) =>
        Json.O("type", "array", "description", d, "items", items ?? Json.O());
    public static Dictionary<string, object> Pt(string d) => Json.O("type", "array", "description", d + " [x, y, z]", "items", Json.O("type", "number"), "minItems", 2, "maxItems", 3);
    public static Dictionary<string, object> AnyObj(string d) => Json.O("type", "object", "description", d, "additionalProperties", true);
    public static Dictionary<string, object> Any(string d) => Json.O("description", d);

    public static (string, Dictionary<string, object>, bool) R(string name, Dictionary<string, object> s) => (name, s, true);
    public static (string, Dictionary<string, object>, bool) O(string name, Dictionary<string, object> s) => (name, s, false);

    /// <summary>Optional 'target' parameter shared by host tools.</summary>
    public static (string, Dictionary<string, object>, bool) Target =>
        ("target", Str("Which running instance when several are open: year (\"2024\"), process id, or instance id from burak_status. Omit for the most recent."), false);
}

public sealed class ToolRegistry
{
    readonly Dictionary<string, Tool> _tools = new(StringComparer.OrdinalIgnoreCase);
    public void Add(Tool t) => _tools[t.Name] = t;
    public IEnumerable<Tool> All => _tools.Values;
    public Tool? Get(string name) => _tools.TryGetValue(name, out var t) ? t : null;
}

/// <summary>User-facing error: message goes back to the AI agent as-is.</summary>
public sealed class ToolException(string message) : Exception(message);
