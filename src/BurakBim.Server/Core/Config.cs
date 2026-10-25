using BurakBim.Shared;

namespace BurakBim.Server.Core;

/// <summary>
/// &lt;root&gt;\config\burakbim.json. Edited by admins/BIM managers; re-read on every call (no restart needed).
/// </summary>
public static class Config
{
    static DateTime _stamp;
    static Dictionary<string, object> _data = new();

    public static string FilePath => Path.Combine(BurakPaths.ConfigDir, "burakbim.json");

    public static Dictionary<string, object> Defaults() => Json.O(
        "readOnly", false,
        "allowScripts", true,
        "allowComAutomation", true,
        "allowUiAutomation", true,
        "toolsets", new List<object> { "auto" },
        "bridgeAutoStart", true,
        "requestTimeoutSeconds", 180,
        "maxPointsPerRequest", 400000,
        "inspectorName", "",
        "extraSearchPaths", new List<object>(),
        "company", "BURAK BIM");

    public static Dictionary<string, object> Data
    {
        get
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, Json.Serialize(Defaults(), true));
                }
                var t = File.GetLastWriteTimeUtc(FilePath);
                if (t != _stamp)
                {
                    var d = Defaults();
                    foreach (var kv in Json.ParseObject(File.ReadAllText(FilePath))) d[kv.Key] = kv.Value;
                    _data = d;
                    _stamp = t;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[BURAK BIM] config error: " + ex.Message);
                if (_data.Count == 0) _data = Defaults();
            }
            return _data;
        }
    }

    public static bool ReadOnly => Json.Bool(Data, "readOnly");
    public static bool AllowScripts => Json.Bool(Data, "allowScripts", true);
    public static bool AllowCom => Json.Bool(Data, "allowComAutomation", true);
    public static bool AllowUi => Json.Bool(Data, "allowUiAutomation", true);
    public static int TimeoutMs => (int)Json.Int(Data, "requestTimeoutSeconds", 180) * 1000;
    public static List<string> Toolsets => Json.StrList(Data, "toolsets");

    public static void Set(string key, object? value)
    {
        var d = new Dictionary<string, object>(Data) { [key] = value! };
        File.WriteAllText(FilePath, Json.Serialize(d, true));
    }
}
