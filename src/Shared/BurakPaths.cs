// BURAK BIM - install-root resolution and discovery records shared by server and bridges.
using System;
using System.Collections.Generic;
using System.IO;

namespace BurakBim.Shared
{
    public static class BurakInfo
    {
        public const string ProductName = "BURAK BIM";
        public const string Version = "1.2.0";
        public const string Vendor = "BURAK BIM";
        public const string RegistryKey = @"Software\BURAK BIM";
        /// <summary>Wire protocol version between server and host bridges.</summary>
        public const int BridgeProtocol = 1;
    }

    public static class BurakPaths
    {
        static string _root;

        /// <summary>
        /// Install root (default D:\BURAK BIM). Resolution order:
        /// BURAK_BIM_HOME env var, HKCU\Software\BURAK BIM\InstallDir, then a folder walk
        /// up from the calling assembly looking for the "burakbim.root" marker file.
        /// </summary>
        public static string Root
        {
            get
            {
                if (_root != null) return _root;
                string r = Environment.GetEnvironmentVariable("BURAK_BIM_HOME");
                if (string.IsNullOrEmpty(r) || !Directory.Exists(r)) r = ReadRegistryRoot();
                if (string.IsNullOrEmpty(r) || !Directory.Exists(r)) r = WalkForMarker();
                if (string.IsNullOrEmpty(r))
                    r = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BURAK BIM");
                _root = r;
                return r;
            }
            set { _root = value; }
        }

        static string ReadRegistryRoot()
        {
            try
            {
                // Late-bound so this file also compiles for non-Windows test builds.
                var regType = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry", false)
                              ?? Type.GetType("Microsoft.Win32.Registry, mscorlib", false);
                if (regType == null) return null;
                var hkcu = regType.GetField("CurrentUser").GetValue(null);
                var open = hkcu.GetType().GetMethod("OpenSubKey", new[] { typeof(string) });
                var key = open.Invoke(hkcu, new object[] { BurakInfo.RegistryKey });
                if (key == null) return null;
                var getValue = key.GetType().GetMethod("GetValue", new[] { typeof(string) });
                var v = getValue.Invoke(key, new object[] { "InstallDir" }) as string;
                ((IDisposable)key).Dispose();
                return v;
            }
            catch { return null; }
        }

        static string WalkForMarker()
        {
            try
            {
                var dir = Path.GetDirectoryName(typeof(BurakPaths).Assembly.Location);
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
                {
                    if (File.Exists(Path.Combine(dir, "burakbim.root"))) return dir;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }
            return null;
        }

        public static string Sub(params string[] parts)
        {
            var all = new List<string> { Root };
            all.AddRange(parts);
            var p = Path.Combine(all.ToArray());
            try { Directory.CreateDirectory(p); } catch { }
            return p;
        }

        public static string RuntimeDir { get { return Sub("runtime"); } }
        public static string LogsDir { get { return Sub("logs"); } }
        public static string AuditDir { get { return Sub("audit"); } }
        public static string ReportsDir { get { return Sub("reports"); } }
        public static string ConfigDir { get { return Sub("config"); } }
        public static string ScratchDir { get { return Sub("scratch"); } }
    }

    /// <summary>
    /// A running host bridge advertises itself with runtime\&lt;host&gt;-&lt;year&gt;-&lt;pid&gt;.json.
    /// </summary>
    public sealed class BridgeDescriptor
    {
        public string Host;      // revit | autocad | navisworks
        public string Product;   // e.g. "Autodesk Revit 2024", "Autodesk Civil 3D 2024"
        public int Year;
        public int Pid;
        public int Port;
        public string Token;
        public DateTime StartedUtc;
        public int Protocol;
        public string BridgeVersion;
        public string FilePath;  // not serialized

        public Dictionary<string, object> ToJson()
        {
            return Json.O("host", Host, "product", Product, "year", Year, "pid", Pid, "port", Port,
                "token", Token, "startedUtc", StartedUtc, "protocol", Protocol, "bridgeVersion", BridgeVersion);
        }

        public static BridgeDescriptor FromJson(Dictionary<string, object> o)
        {
            DateTime started;
            DateTime.TryParse(Json.Str(o, "startedUtc", ""), null, System.Globalization.DateTimeStyles.RoundtripKind, out started);
            return new BridgeDescriptor
            {
                Host = Json.Str(o, "host"),
                Product = Json.Str(o, "product"),
                Year = (int)Json.Int(o, "year"),
                Pid = (int)Json.Int(o, "pid"),
                Port = (int)Json.Int(o, "port"),
                Token = Json.Str(o, "token"),
                StartedUtc = started,
                Protocol = (int)Json.Int(o, "protocol"),
                BridgeVersion = Json.Str(o, "bridgeVersion")
            };
        }

        public string Id { get { return Host + "-" + Year + "-" + Pid; } }
    }
}
