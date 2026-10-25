using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using BurakBim.Shared;
using Microsoft.Win32;

namespace BurakBim.Server.Detection;

public sealed class Product
{
    public required string Name { get; init; }      // "Autodesk Revit 2024", "Autodesk Civil 3D 2024"
    public required string Family { get; init; }    // revit | autocad | navisworks | inventor | recap | robot | other
    public int Year { get; init; }
    public string? Exe { get; init; }
    public string? InstallDir { get; init; }
    public string? Version { get; init; }
    public string? AcadRegKey { get; init; }        // HKLM\SOFTWARE\Autodesk\AutoCAD\R24.3\ACAD-7101:409 (AutoCAD family)
    public List<string> Control { get; init; } = new();

    public Dictionary<string, object> ToJson(IEnumerable<Process>? running = null)
    {
        var d = Json.O("name", Name, "family", Family, "year", Year, "exe", Exe, "version", Version, "control", Control.Cast<object>().ToList());
        d["bridgeInstalled"] = BridgeInstalled;
        if (running != null && Exe != null)
            d["running"] = running.Count(p => { try { return string.Equals(p.MainModule?.FileName, Exe, StringComparison.OrdinalIgnoreCase); } catch { return false; } });
        return d;
    }

    public bool BridgeInstalled => Family is "revit" or "autocad" or "navisworks"
        && Directory.Exists(Path.Combine(BurakPaths.Root, "bridges", Family, Year.ToString()))
        && Directory.GetFiles(Path.Combine(BurakPaths.Root, "bridges", Family, Year.ToString()), "BurakBim.Bridge.*.dll").Length > 0;
}

/// <summary>
/// Finds every Autodesk product on this PC (any drive): registry (Uninstall + AutoCAD product keys) plus
/// the standard Program Files layout. Versions 2022-2027 get full control; others are listed with UI control.
/// </summary>
public static class SoftwareDetector
{
    static List<Product>? _cache;
    static DateTime _cacheTime;

    public static List<Product> Detect(bool refresh = false)
    {
        if (!refresh && _cache != null && DateTime.UtcNow - _cacheTime < TimeSpan.FromMinutes(2)) return _cache;
        var list = new List<Product>();
        if (OperatingSystem.IsWindows())
        {
            try { DetectWindows(list); } catch (Exception ex) { Console.Error.WriteLine("[BURAK BIM] detection error: " + ex.Message); }
        }
        _cache = list
            .GroupBy(p => (p.Family, p.Year, (p.Exe ?? p.Name).ToLowerInvariant(), p.Name))
            .Select(g => g.First())
            .OrderBy(p => p.Family).ThenBy(p => p.Year).ThenBy(p => p.Name).ToList();
        _cacheTime = DateTime.UtcNow;
        return _cache;
    }

    static readonly Regex YearRx = new(@"\b(20\d\d)\b", RegexOptions.Compiled);
    static int YearOf(string? s) { var m = YearRx.Match(s ?? ""); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }
    static bool Supported(int y) => y >= 2022 && y <= 2027;

    [SupportedOSPlatform("windows")]
    static void DetectWindows(List<Product> list)
    {
        // 1) AutoCAD family (AutoCAD, Civil 3D, Plant 3D, Map 3D, MEP, Architecture, Electrical, Mechanical, Advance Steel)
        using (var acad = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD"))
        {
            if (acad != null)
                foreach (var rel in acad.GetSubKeyNames().Where(n => n.StartsWith("R")))
                    using (var relKey = acad.OpenSubKey(rel))
                        foreach (var prod in relKey?.GetSubKeyNames().Where(n => n.StartsWith("ACAD-")) ?? Enumerable.Empty<string>())
                            using (var pk = relKey!.OpenSubKey(prod))
                            {
                                var name = pk?.GetValue("ProductName") as string;
                                var loc = pk?.GetValue("AcadLocation") as string ?? pk?.GetValue("Location") as string;
                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(loc)) continue;
                                var exe = Path.Combine(loc, "acad.exe");
                                if (!File.Exists(exe)) continue;
                                var year = YearOf(name);
                                list.Add(new Product
                                {
                                    Name = name.StartsWith("Autodesk") || name.StartsWith("AutoCAD") ? name : "Autodesk " + name,
                                    Family = "autocad", Year = year, Exe = exe, InstallDir = loc,
                                    Version = FileVer(exe), AcadRegKey = $@"SOFTWARE\Autodesk\AutoCAD\{rel}\{prod}",
                                    Control = Supported(year) ? new() { "bridge", "com", "ui" } : new() { "com", "ui" }
                                });
                            }
        }

        // 2) Uninstall entries (machine 64/32-bit + user) for everything else
        var entries = new List<(string name, string? loc, string? ver)>();
        foreach (var (hive, path) in new[] {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall") })
        {
            using var k = hive.OpenSubKey(path);
            if (k == null) continue;
            foreach (var sub in k.GetSubKeyNames())
            {
                using var e = k.OpenSubKey(sub);
                var n = e?.GetValue("DisplayName") as string;
                if (string.IsNullOrEmpty(n)) continue;
                var pub = e!.GetValue("Publisher") as string ?? "";
                if (!pub.Contains("Autodesk", StringComparison.OrdinalIgnoreCase) && !n.Contains("Autodesk", StringComparison.OrdinalIgnoreCase)) continue;
                entries.Add((n, e.GetValue("InstallLocation") as string, e.GetValue("DisplayVersion") as string));
            }
        }

        // 3) Every Autodesk exe, wherever it is installed (custom folders such as D:\ALL PROGRAMS\NAVISWORK\... included)
        foreach (var exe in ExeCandidates(entries.Select(e => e.loc)))
        {
            var dir = Path.GetDirectoryName(exe)!;
            var leaf = Path.GetFileName(exe).ToLowerInvariant();
            var (year, pname) = ExeYear(exe);
            Product? p = null;
            switch (leaf)
            {
                case "revit.exe":
                    if (File.Exists(Path.Combine(dir, "RevitAPI.dll")))
                        p = new Product { Name = "Autodesk Revit " + year, Family = "revit", Year = year, Exe = exe, InstallDir = dir,
                            Control = Supported(year) ? new() { "bridge", "ui" } : new() { "ui" } };
                    break;
                case "roamer.exe":
                    if (!File.Exists(Path.Combine(dir, "Autodesk.Navisworks.Api.dll"))) break;
                    var freedom = (pname + dir).Contains("Freedom", StringComparison.OrdinalIgnoreCase);
                    var sim = (pname + dir).Contains("Simulate", StringComparison.OrdinalIgnoreCase);
                    p = new Product { Name = "Autodesk Navisworks " + (freedom ? "Freedom " : sim ? "Simulate " : "Manage ") + year, Family = "navisworks", Year = year, Exe = exe, InstallDir = dir,
                        Control = freedom ? new() { "ui" } : Supported(year) ? new() { "bridge", "ui" } : new() { "ui" } };
                    break;
                case "inventor.exe":
                    p = new Product { Name = "Autodesk Inventor " + year, Family = "inventor", Year = year, Exe = exe, InstallDir = Path.GetDirectoryName(dir), Control = new() { "com", "ui" } };
                    break;
                case "recap.exe":
                    p = new Product { Name = "Autodesk ReCap Pro", Family = "recap", Year = year, Exe = exe, InstallDir = dir, Control = new() { "launch", "ui" } };
                    break;
                case "robot.exe":
                    p = new Product { Name = "Autodesk Robot Structural Analysis " + year, Family = "robot", Year = year, Exe = exe, InstallDir = Path.GetDirectoryName(Path.GetDirectoryName(dir)), Control = new() { "com", "ui" } };
                    break;
                case "acad.exe":
                    if (!list.Any(x => x.Family == "autocad" && string.Equals(x.InstallDir?.TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                        p = new Product { Name = string.IsNullOrEmpty(pname) ? "AutoCAD " + year : pname, Family = "autocad", Year = year, Exe = exe, InstallDir = dir,
                            Control = Supported(year) ? new() { "bridge", "com", "ui" } : new() { "com", "ui" } };
                    break;
            }
            if (p != null) list.Add(WithVersion(p));
        }

        // 4) Non-Autodesk BIM apps: McNeel Rhino (+ Grasshopper), Graphisoft Archicad, Dynamo Sandbox
        foreach (var drv in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var pf = Path.Combine(drv.RootDirectory.FullName, "Program Files");
            foreach (var dir in SafeDirs(pf, "Rhino *"))
            {
                var exe = Path.Combine(dir, "System", "Rhino.exe");
                if (!File.Exists(exe)) continue;
                int v = int.TryParse(Path.GetFileName(dir).Replace("Rhino ", ""), out var rv) ? rv : 0;
                list.Add(WithVersion(new Product { Name = "Rhinoceros " + v + " (+ Grasshopper)", Family = "rhino", Year = v, Exe = exe, InstallDir = dir, Control = new() { "com", "ui" } }));
            }
            foreach (var dir in SafeDirs(Path.Combine(pf, "Graphisoft"), "Archicad*").Concat(SafeDirs(Path.Combine(pf, "GRAPHISOFT"), "ARCHICAD*")).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var exe = Path.Combine(dir, "Archicad.exe");
                if (!File.Exists(exe)) continue;
                var m = Regex.Match(Path.GetFileName(dir), @"(\d{2})");
                int v = m.Success ? int.Parse(m.Value) : 0;
                list.Add(WithVersion(new Product { Name = "Graphisoft " + Path.GetFileName(dir), Family = "archicad", Year = v, Exe = exe, InstallDir = dir,
                    Control = v >= 24 ? new() { "jsonapi", "ui" } : new() { "ui" } }));
            }
            foreach (var dir in SafeDirs(Path.Combine(pf, @"Autodesk\Dynamo\Core"), "*"))
                if (File.Exists(Path.Combine(dir, "DynamoCLI.exe")) || File.Exists(Path.Combine(dir, "DynamoSandbox.exe")))
                    list.Add(new Product { Name = "Dynamo Core " + Path.GetFileName(dir), Family = "dynamo", Year = 0, Exe = Path.Combine(dir, "DynamoSandbox.exe"), InstallDir = dir, Control = new() { "cli", "ui" } });
        }

        // 5) Remaining Autodesk products from the uninstall list (listed; controllable through UI automation)
        var known = new[] { "Revit 20", "AutoCAD", "Civil 3D", "Navisworks", "Inventor", "ReCap", "Robot", "Advance Steel", "Plant 3D", "Map 3D" };
        foreach (var e in entries)
        {
            var y = YearOf(e.name);
            if (known.Any(k => e.name.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            if (!Regex.IsMatch(e.name, @"(3ds Max|Maya|Dynamo|FormIt|Vault|Fabrication|InfraWorks|Forma|Alias|Fusion|Advance Concrete|Structural Bridge|Vehicle Tracking|Storm|River|Docs|Desktop Connector)", RegexOptions.IgnoreCase)) continue;
            list.Add(new Product { Name = e.name, Family = "other", Year = y, InstallDir = e.loc, Version = e.ver, Control = new() { "ui" } });
        }
    }


    static readonly string[] ExeNames = { "Revit.exe", "Roamer.exe", "Inventor.exe", "ReCap.exe", "robot.exe", "acad.exe" };
    static readonly Regex SkipDir = new(@"^(Windows|\$Recycle\.Bin|System Volume Information|ProgramData|Users|Recovery|PerfLogs|node_modules|\.git)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Extra folders to search (config "extraSearchPaths" + config\searchpaths.txt written by the installer).</summary>
    public static List<string> SearchRoots()
    {
        var l = new List<string>();
        try { l.AddRange(Json.StrList(Core.Config.Data, "extraSearchPaths") ?? new()); } catch { }
        try { var f = Path.Combine(BurakPaths.ConfigDir, "searchpaths.txt"); if (File.Exists(f)) l.AddRange(File.ReadAllLines(f).Where(x => x.Trim().Length > 0)); } catch { }
        return l.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    [SupportedOSPlatform("windows")]
    static IEnumerable<string> ExeCandidates(IEnumerable<string?> uninstallLocations)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddExe(string? f) { try { if (!string.IsNullOrWhiteSpace(f)) { f = f.Trim().Trim('"'); if (File.Exists(f)) set.Add(Path.GetFullPath(f)); } } catch { } }
        void AddDir(string? d, int depth)
        {
            if (string.IsNullOrWhiteSpace(d)) return;
            d = d.Trim().Trim('"').TrimEnd('\\');
            if (d.Length == 2 && d[1] == ':') d += "\\";
            if (!Directory.Exists(d)) return;
            foreach (var n in ExeNames) { AddExe(Path.Combine(d, n)); AddExe(Path.Combine(d, "Bin", n)); AddExe(Path.Combine(d, "System", "Exe", n)); }
            if (depth <= 0) return;
            string[] subs; try { subs = Directory.GetDirectories(d); } catch { return; }
            foreach (var sd in subs) if (!SkipDir.IsMatch(Path.GetFileName(sd))) AddDir(sd, depth - 1);
        }
        foreach (var drv in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
            foreach (var sub in new[] { @"Program Files\Autodesk", @"Program Files (x86)\Autodesk", "Autodesk" })
                AddDir(Path.Combine(drv.RootDirectory.FullName, sub), 2);
        foreach (var loc in uninstallLocations) AddDir(loc, 2);
        // Uninstall DisplayIcon values point at the exe even when InstallLocation is empty
        foreach (var (hive, path) in new[] {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall") })
        {
            try
            {
                using var k = hive.OpenSubKey(path); if (k == null) continue;
                foreach (var sub in k.GetSubKeyNames())
                {
                    using var e = k.OpenSubKey(sub);
                    if (!((e?.GetValue("Publisher") as string ?? "") + (e?.GetValue("DisplayName") as string ?? "")).Contains("Autodesk", StringComparison.OrdinalIgnoreCase)) continue;
                    var ico = (e!.GetValue("DisplayIcon") as string ?? "").Split(',')[0].Trim('"');
                    if (ico.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { AddExe(ico); AddDir(Path.GetDirectoryName(ico), 1); }
                }
            }
            catch { }
        }
        // App Paths
        foreach (var n in ExeNames)
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                try { using var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + n); AddExe(k?.GetValue(null) as string); } catch { }
        // path-like values anywhere under HKLM\SOFTWARE\Autodesk (Revit / Navisworks / Inventor product keys)
        void Walk(RegistryKey key, int depth)
        {
            foreach (var vn in key.GetValueNames())
            {
                if (key.GetValue(vn) is not string v || v.Length < 4 || v[1] != ':' || v[2] != '\\') continue;
                if (!Regex.IsMatch(vn, "Install|Location|Path|Dir", RegexOptions.IgnoreCase)) continue;
                if (v.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) AddExe(v); else AddDir(v, 1);
            }
            if (depth <= 0) return;
            foreach (var sn in key.GetSubKeyNames()) { try { using var sk = key.OpenSubKey(sn); if (sk != null) Walk(sk, depth - 1); } catch { } }
        }
        try { using var ad = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk"); if (ad != null) Walk(ad, 4); } catch { }
        // Start-menu shortcuts (.lnk target path, read without COM)
        foreach (var sm in new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) })
        {
            try
            {
                foreach (var lnk in Directory.EnumerateFiles(sm, "*.lnk", SearchOption.AllDirectories))
                    foreach (Match m in Regex.Matches(File.ReadAllText(lnk, System.Text.Encoding.Unicode) + File.ReadAllText(lnk, System.Text.Encoding.Latin1), @"[A-Za-z]:\\[^\0""<>|?*]{3,240}?\\(Revit|Roamer|Inventor|ReCap|robot|acad)\.exe", RegexOptions.IgnoreCase))
                        AddExe(m.Value);
            }
            catch { }
        }
        // running applications
        foreach (var pn in new[] { "Revit", "Roamer", "Inventor", "ReCap", "robot", "acad" })
            foreach (var pr in Process.GetProcessesByName(pn)) { try { AddExe(pr.MainModule?.FileName); } catch { } }
        // user folders (e.g. D:\ALL PROGRAMS)
        foreach (var r in SearchRoots()) AddDir(r, 4);
        return set;
    }

    /// <summary>Product year from the exe version resource / folder names, so any install folder works.</summary>
    static (int year, string name) ExeYear(string exe)
    {
        string name = ""; int y = 0;
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(exe);
            name = vi.ProductName ?? "";
            y = YearOf(name); if (y == 0) y = YearOf(vi.FileDescription);
            var dir = Path.GetDirectoryName(exe);
            for (int i = 0; i < 3 && y == 0 && dir != null; i++) { y = YearOf(Path.GetFileName(dir)); dir = Path.GetDirectoryName(dir); }
            if (y == 0 && vi.ProductMajorPart > 0)
                y = Path.GetFileName(exe).ToLowerInvariant() switch
                {
                    "roamer.exe" => 2003 + vi.ProductMajorPart,
                    "revit.exe" => vi.ProductMajorPart > 2000 ? vi.ProductMajorPart : 2000 + vi.ProductMajorPart,
                    "inventor.exe" => 1996 + vi.ProductMajorPart,
                    _ => 0
                };
        }
        catch { }
        return (y, name);
    }

    static IEnumerable<string> SafeDirs(string root, string pattern)
    {
        try { return Directory.Exists(root) ? Directory.GetDirectories(root, pattern) : Array.Empty<string>(); } catch { return Array.Empty<string>(); }
    }

    // fills Version from the exe file version when missing
    static Product WithVersion(Product p) => p.Version != null || p.Exe == null ? p : new Product
    {
        Name = p.Name, Family = p.Family, Year = p.Year, Exe = p.Exe, InstallDir = p.InstallDir, AcadRegKey = p.AcadRegKey,
        Control = p.Control, Version = FileVer(p.Exe)
    };

    static string? FileVer(string exe)
    {
        try { return FileVersionInfo.GetVersionInfo(exe).ProductVersion; } catch { return null; }
    }

    public static Dictionary<string, string[]> ProcessNames => new()
    {
        ["revit"] = new[] { "Revit" },
        ["autocad"] = new[] { "acad" },
        ["navisworks"] = new[] { "Roamer" },
        ["inventor"] = new[] { "Inventor" },
        ["recap"] = new[] { "ReCap" },
        ["robot"] = new[] { "robot" },
        ["rhino"] = new[] { "Rhino" },
        ["archicad"] = new[] { "Archicad", "ARCHICAD" }
    };

    public static List<Process> Running()
    {
        var r = new List<Process>();
        foreach (var names in ProcessNames.Values)
            foreach (var n in names)
                try { r.AddRange(Process.GetProcessesByName(n)); } catch { }
        return r;
    }
}
