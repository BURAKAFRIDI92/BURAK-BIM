using System.Text;
using BurakBim.Server.Detection;
using BurakBim.Shared;
using Microsoft.Win32;

namespace BurakBim.Server.Setup;

/// <summary>
/// 1.3: the whole installation in plain C#, run by Setup.exe as "BurakBim.Server.exe --install".
/// No PowerShell, no hidden windows, no scheduled tasks, no downloads, no admin rights - the patterns antivirus
/// heuristics dislike are gone. (installer\install.ps1 remains for advanced/offline-compile cases.)
///
///   --install   [--home DIR] [--clients auto|none|id,id] [--skills-for-claude-code]
///   --uninstall [--home DIR] [--keep-clients]
///   --rescan    (connect versions installed later; same as --install without touching AI clients)
/// </summary>
public static class Installer
{
    const string RevitAddInId = "8E3B2F7A-4C1D-4B9E-A2F5-6D7C8B9A0E1F";
    static readonly StringBuilder LogText = new();
    static int _warnings;

    static void Say(string s) { Console.WriteLine(s); LogText.AppendLine(s); }
    static void Ok(string s) => Say("    [OK] " + s);
    static void Warn(string s) { _warnings++; Say("    [!]  " + s); }
    static void Step(string s) => Say("\n==> " + s);

    static string Setup => Path.Combine(BurakPaths.Root, "setup");

    static string? Arg(string[] args, string name) => args.SkipWhile(a => !a.Equals(name, StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault(a => !a.StartsWith("--"));

    public static int Install(string[] args, bool rescan = false)
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("BURAK BIM installs on Windows (the BIM applications it connects to are Windows programs)."); return 1; }
        var root = BurakPaths.Root;
        Say($"BURAK BIM {BurakInfo.Version} - {(rescan ? "rescan" : "install")} - folder {root}");
        foreach (var sub in new[] { "server", "bridges", "config", "runtime", "logs", "audit", "reports", "exports", "scratch", "captures", "toolpacks" })
            Directory.CreateDirectory(Path.Combine(root, sub));
        File.WriteAllText(Path.Combine(root, "burakbim.root"), $"BURAK BIM {BurakInfo.Version} install root");
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(@"Software\BURAK BIM");
            k.SetValue("InstallDir", root); k.SetValue("Version", BurakInfo.Version);
        }
        catch (Exception ex) { Warn("registry: " + ex.Message); }

        RemoveOldSentinel();

        Step("Removing 'downloaded from the internet' marks (so Revit / AutoCAD / Navisworks can load the bridges)");
        int unblocked = 0;
        foreach (var dir in new[] { Path.Combine(root, "server"), Setup, Path.Combine(root, "bridges") })
            unblocked += Unblock(dir);
        Ok($"{unblocked} file(s) unblocked");

        Step("Detecting software");
        var products = SoftwareDetector.Detect(true);
        if (products.Count == 0) Warn("No supported software found. BURAK BIM is installed anyway; run 'BurakBim.Server.exe --rescan' after installing your BIM software.");
        foreach (var p in products.OrderBy(p => p.Family).ThenBy(p => p.Year))
            Say($"    {p.Name,-50} {string.Join("+", p.Control)}");

        Step("Installing bridges (prebuilt for every version, no internet needed)");
        var installed = new List<string>();
        var missing = new List<string>();
        foreach (var g in products.Where(p => p.Family is "revit" or "autocad" or "navisworks" && p.Year >= 2022 && p.Year <= 2030)
                                  .GroupBy(p => (p.Family, p.Year)))
        {
            var (fam, year) = g.Key;
            var src = Path.Combine(Setup, "prebuilt", fam, year.ToString());
            var dst = Path.Combine(root, "bridges", fam, year.ToString());
            if (!Directory.Exists(src) || Directory.GetFiles(src, "BurakBim.Bridge.*.dll").Length == 0)
            {
                // no prebuilt bridge for this version: compile it here against the installed product's own API
                if (CompileBridge(fam, year, g.First(), dst, out var why)) { installed.Add($"{fam}-{year}"); Ok($"{fam} {year}: bridge compiled on this PC"); }
                else if (Directory.Exists(dst) && Directory.GetFiles(dst, "BurakBim.Bridge.*.dll").Length > 0) { installed.Add($"{fam}-{year}"); Warn($"{fam} {year}: could not compile a new bridge, the existing one was kept ({why})"); }
                else { missing.Add($"{fam}-{year}"); Warn($"{fam} {year}: could not compile a bridge ({why})"); }
                continue;
            }
            try
            {
                CopyDir(src, dst);
                var shared = Path.Combine(Setup, "prebuilt", "_shared");
                if (Directory.Exists(shared)) CopyDir(shared, Path.Combine(root, "bridges", "_shared"));
                installed.Add($"{fam}-{year}");
                Ok($"{fam} {year}: bridge installed");
            }
            catch (IOException ex) { Warn($"{fam} {year}: files are in use - close {fam} and run setup again ({ex.Message})"); }
        }
        foreach (var m in missing)
            Warn($"{m}: no bridge is installed. Capture + computer control still work for it; update BURAK BIM to connect it fully.");

        Step("Registering with the applications");
        foreach (var register in new Action[] { () => RegisterRevit(installed), () => RegisterAutoCad(installed, products), () => RegisterNavisworks(installed) })
        {
            try { register(); }
            catch (IOException ex) { Warn("A program is using BURAK BIM files. Close Revit, AutoCAD and Navisworks, then run setup again (" + ex.Message + ")"); }
        }
        InstallRhinoPanel(products);
        InstallInventorTab(products);

        if (!File.Exists(Path.Combine(root, "config", "burakbim.json")))
            File.WriteAllText(Path.Combine(root, "config", "burakbim.json"), Json.Serialize(Core.Config.Defaults(), true), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "config", "installed.json"), Json.Serialize(Json.O("version", BurakInfo.Version, "updated", DateTime.Now.ToString("s"),
            "installDir", root, "bridges", installed.Cast<object>().ToList(), "missing", missing.Cast<object>().ToList()), true), new UTF8Encoding(false));

        if (!rescan)
        {
            var clients = Arg(args, "--clients") ?? "auto";
            if (clients != "none")
            {
                Step("Connecting AI apps");
                var rep = ClientConnector.Connect(clients);
                foreach (var r in rep)
                    if (Json.Bool(r, "ok")) Ok($"{Json.Str(r, "client")} -> {Json.Str(r, "file")}");
                    else Warn($"{Json.Str(r, "client")}: {Json.Str(r, "error")}");
                if (rep.Count == 0) Say("    No AI apps found. Install one, then run 'BurakBim.Server.exe --connect-clients'.");
                if (!rep.Any(r => Json.Str(r, "id") == "claude-desktop"))
                    Say("    Claude Desktop was not found. If you install it later, use Start menu > BURAK BIM > Connect AI apps again.");
                // Claude Desktop rewrites its config when it quits, which can remove what was just added
                if (System.Diagnostics.Process.GetProcessesByName("Claude").Length > 0)
                    Warn("Claude Desktop is open. Quit it from the tray icon, then use Start menu > BURAK BIM > Connect AI apps again, and open Claude Desktop.");
            }
            Step("Installing BIM workflow skills");
            InstallSkills();
        }

        Step("Self-check");
        Say("    " + (SelfCheck() ? "MCP handshake OK - AI apps can start BURAK BIM" : "MCP handshake FAILED - see logs"));

        Say(_warnings == 0 ? $"\nBURAK BIM {BurakInfo.Version} installed." : $"\nBURAK BIM {BurakInfo.Version} installed with {_warnings} note(s) - see above.");
        Say("Next: fully quit and restart your AI app, open your BIM software, and ask: \"Use BURAK BIM: burak_status\".");
        try { File.WriteAllText(Path.Combine(root, "logs", $"install-{DateTime.Now:yyyyMMdd-HHmmss}.log"), LogText.ToString()); } catch { }
        return 0;
    }

    public static int Uninstall(string[] args)
    {
        var root = BurakPaths.Root;
        Say("BURAK BIM uninstall - " + root);
        RemoveOldSentinel();
        if (!args.Contains("--keep-clients"))
            foreach (var r in ClientConnector.Disconnect()) Ok("removed from " + Json.Str(r, "client"));
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        for (int y = 2022; y <= 2030; y++)
        {
            var f = Path.Combine(appData, "Autodesk", "Revit", "Addins", y.ToString(), "BurakBim.addin");
            try { if (File.Exists(f) && File.ReadAllText(f).Contains(RevitAddInId)) { File.Delete(f); Ok("removed " + f); } } catch { }
        }
        foreach (var b in new[] { "BurakBim.AutoCAD.bundle", "BurakBim.Navisworks.bundle" })
        {
            var p = Path.Combine(appData, "Autodesk", "ApplicationPlugins", b);
            try { if (Directory.Exists(p)) { Directory.Delete(p, true); Ok("removed " + p); } } catch (Exception ex) { Warn(p + ": " + ex.Message); }
        }
        for (int y = 2022; y <= 2030; y++)
        {
            var f = Path.Combine(appData, "Autodesk", "Inventor " + y, "Addins", "BurakBim.addin");
            try { if (File.Exists(f)) { File.Delete(f); Ok("removed " + f); } } catch (Exception ex) { Warn(f + ": " + ex.Message); }
        }
        try
        {
            var rhino = Path.Combine(appData, "McNeel", "Rhinoceros", "packages");
            if (Directory.Exists(rhino))
                foreach (var d in Directory.GetDirectories(rhino, "burak-bim", SearchOption.AllDirectories)) { Directory.Delete(d, true); Ok("removed " + d); }
        }
        catch (Exception ex) { Warn("Rhino panel: " + ex.Message); }
        try
        {
            var skills = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");
            if (Directory.Exists(skills)) foreach (var d in Directory.GetDirectories(skills, "burak-bim-*")) Directory.Delete(d, true);
        }
        catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\BURAK BIM", false); } catch { }
        foreach (var sub in new[] { "bridges", "runtime", "scratch", "cache", "tools" })
            try { var d = Path.Combine(root, sub); if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        Say("Done. Audit trail, reports, exports, config and logs were kept in " + root);
        return 0;
    }

    // ------------------------------------------------------------------ bridge for a version without a prebuilt one
    /// <summary>Compiles one bridge with the bundled Roslyn compiler (needs only .NET Framework) against the installed product's API DLLs.</summary>
    static bool CompileBridge(string fam, int year, Product p, string dst, out string why)
    {
        why = "";
        try
        {
            var csc = Path.Combine(Setup, "tools", "roslyn", "csc.exe");
            var shared = Path.Combine(Setup, "prebuilt", "_shared");
            bool inventor = fam == "inventor";
            var hostDir = inventor ? p.InstallDir ?? "" : (p.Exe != null ? Path.GetDirectoryName(p.Exe) : null) ?? p.InstallDir ?? "";
            var proj = fam switch { "revit" => "BurakBim.Bridge.Revit", "autocad" => "BurakBim.Bridge.AutoCAD", "inventor" => "BurakBim.Plugin.Inventor", _ => "BurakBim.Bridge.Navisworks" };
            bool netCore = fam != "navisworks" && year >= 2025;
            var roslyn = Path.Combine(shared, netCore ? "netcore" : "net48");
            if (!File.Exists(csc)) { why = "the bundled compiler is missing"; return false; }
            if (!inventor && !Directory.Exists(roslyn)) { why = "the shared libraries are missing: " + roslyn; return false; }

            var defines = new List<string> { "TRACE", "BURAK_ROSLYN" };
            var refs = new List<string>();
            switch (fam)
            {
                case "revit":
                    defines.Add("REVIT" + year); if (year >= 2024) defines.Add("REVIT_LONG_IDS");
                    refs.Add(Path.Combine(hostDir, "RevitAPI.dll")); refs.Add(Path.Combine(hostDir, "RevitAPIUI.dll"));
                    break;
                case "inventor":
                    var interop = Path.Combine(hostDir, "Bin", "Public Assemblies", "Autodesk.Inventor.Interop.dll");
                    if (!File.Exists(interop) && Directory.Exists(hostDir))
                        interop = Directory.EnumerateFiles(hostDir, "Autodesk.Inventor.Interop.dll", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).FirstOrDefault() ?? interop;
                    refs.Add(interop);
                    break;
                case "autocad":
                    defines.Add("ACAD" + year);
                    foreach (var d in new[] { "accoremgd.dll", "acdbmgd.dll", "acmgd.dll" }) refs.Add(Path.Combine(hostDir, d));
                    break;
                default:
                    defines.Add("NAVIS" + year);
                    refs.Add(Path.Combine(hostDir, "Autodesk.Navisworks.Api.dll"));
                    break;
            }
            var absent = refs.FirstOrDefault(r => !File.Exists(r));
            if (absent != null) { why = "not found: " + absent; return false; }
            if (!inventor) refs.AddRange(Directory.GetFiles(roslyn, "*.dll"));

            if (netCore)
            {
                int major = year >= 2027 ? 10 : 8;
                var dotnetShared = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared");
                foreach (var fw in new[] { "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App" })
                {
                    var root = Path.Combine(dotnetShared, fw);
                    var dir = Directory.Exists(root) ? Directory.GetDirectories(root, major + ".*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase).FirstOrDefault() : null;
                    if (dir == null) { why = $".NET {major} desktop runtime not found"; return false; }
                    refs.AddRange(Directory.GetFiles(dir, "*.dll").Where(f => IsManaged(f)));
                }
                defines.Add("NETCOREAPP"); defines.Add("NET");
            }
            else
            {
                var fx = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319");
                refs.AddRange(new[]
                {
                    "mscorlib.dll", "System.dll", "System.Core.dll", "System.Xml.dll", "System.Xml.Linq.dll", "System.Data.dll", "Microsoft.CSharp.dll", "System.Drawing.dll",
                    "System.Windows.Forms.dll", "System.Xaml.dll", "System.Net.Http.dll", "System.Numerics.dll", "System.Runtime.Serialization.dll", "netstandard.dll", "System.Runtime.dll",
                    "System.Collections.dll", "System.Linq.dll", "System.Threading.Tasks.dll", "System.Reflection.dll", "System.IO.dll", "System.Threading.dll", "System.Runtime.Extensions.dll",
                    Path.Combine("WPF", "WindowsBase.dll"), Path.Combine("WPF", "PresentationCore.dll"), Path.Combine("WPF", "PresentationFramework.dll")
                }.Select(n => Path.Combine(fx, n)).Where(File.Exists));
            }

            Directory.CreateDirectory(dst);
            var lines = new List<string> { "/nologo", "/target:library", "/platform:x64", "/optimize+", "/langversion:latest", "/nowarn:CS0618,CS0219,CS1701,CS1702,CS8632,CS0168", "/nostdlib+" };
            lines.Add("/define:" + string.Join(";", defines));
            lines.Add($"/out:\"{Path.Combine(dst, proj + ".dll")}\"");
            lines.AddRange(refs.Distinct(StringComparer.OrdinalIgnoreCase).Select(r => $"/reference:\"{r}\""));
            foreach (var png in new[] { "burakbim-16.png", "burakbim-32.png" })
            {
                var pf = Path.Combine(Setup, "assets", png);
                if (File.Exists(pf)) lines.Add($"/resource:\"{pf}\",brand.{png}");
            }
            foreach (var folder in inventor ? new[] { "Shared", proj } : new[] { "Shared", "BurakBim.Bridge.Core", proj })
                lines.AddRange(Directory.GetFiles(Path.Combine(Setup, "src", folder), "*.cs").Select(f => $"\"{f}\""));

            var rsp = Path.Combine(BurakPaths.Root, "logs", $"csc-{fam}-{year}.rsp");
            File.WriteAllLines(rsp, lines, new UTF8Encoding(false));
            var psi = new System.Diagnostics.ProcessStartInfo(csc, $"/noconfig \"@{rsp}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var err = proc.StandardError.ReadToEndAsync();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            var log = Path.Combine(BurakPaths.Root, "logs", $"csc-{fam}-{year}.log");
            File.WriteAllText(log, output + err.Result);
            if (proc.ExitCode != 0) { why = "the compiler reported errors, see " + log; return false; }
            if (!inventor) CopyDir(shared, Path.Combine(BurakPaths.Root, "bridges", "_shared"));
            return File.Exists(Path.Combine(dst, proj + ".dll"));
        }
        catch (Exception ex) { why = ex.Message; return false; }
    }

    /// <summary>--compile-bridge FAMILY YEAR API_FOLDER OUTPUT_FOLDER: the compile step of setup on its own (used by CI).</summary>
    public static int CompileBridgeCommand(string[] args)
    {
        var a = args.SkipWhile(x => x != "--compile-bridge").Skip(1).ToArray();
        if (a.Length < 4 || !int.TryParse(a[1], out var year)) { Console.WriteLine("Usage: --compile-bridge FAMILY YEAR API_FOLDER OUTPUT_FOLDER"); return 2; }
        var product = new Product { Name = a[0] + " " + year, Family = a[0], Year = year, InstallDir = a[2] };
        if (CompileBridge(a[0], year, product, a[3], out var why)) { Console.WriteLine($"OK {a[0]} {year}"); return 0; }
        Console.WriteLine($"FAILED {a[0]} {year}: {why}");
        return 1;
    }

    static bool IsManaged(string file)
    {
        try { System.Reflection.AssemblyName.GetAssemblyName(file); return true; }
        catch { return false; }
    }

    // ------------------------------------------------------------------ registration
    static void RegisterRevit(List<string> installed)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var key in installed.Where(k => k.StartsWith("revit-")))
        {
            var year = key[6..];
            var dll = Path.Combine(BurakPaths.Root, "bridges", "revit", year, "BurakBim.Bridge.Revit.dll");
            var dir = Path.Combine(appData, "Autodesk", "Revit", "Addins", year);
            Directory.CreateDirectory(dir);
            var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<RevitAddIns>
  <AddIn Type=""Application"">
    <Name>BURAK BIM</Name>
    <Assembly>{System.Security.SecurityElement.Escape(dll)}</Assembly>
    <AddInId>{RevitAddInId}</AddInId>
    <FullClassName>BurakBim.Bridge.Revit.RevitApp</FullClassName>
    <VendorId>BURAKBIM</VendorId>
    <VendorDescription>BURAK BIM - AI connector for Revit</VendorDescription>
  </AddIn>
</RevitAddIns>
";
            File.WriteAllText(Path.Combine(dir, "BurakBim.addin"), xml, new UTF8Encoding(false));
            Ok($"Revit {year}: add-in registered (BURAK BIM tab)");
        }
    }

    static readonly Dictionary<int, string> AcadRelease = new() { [2022] = "R24.1", [2023] = "R24.2", [2024] = "R24.3", [2025] = "R25.0", [2026] = "R25.1", [2027] = "R26.0", [2028] = "R26.1" };

    static void RegisterAutoCad(List<string> installed, List<Product> products)
    {
        var years = installed.Where(k => k.StartsWith("autocad-")).Select(k => int.Parse(k[8..])).OrderBy(y => y).ToList();
        if (years.Count == 0) return;
        var bundle = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins", "BurakBim.AutoCAD.bundle");
        var comps = new StringBuilder();
        foreach (var y in years)
        {
            var contents = Path.Combine(bundle, "Contents", y.ToString());
            Directory.CreateDirectory(contents);
            var bridgeDir = Path.Combine(BurakPaths.Root, "bridges", "autocad", y.ToString());
            File.Copy(Path.Combine(bridgeDir, "BurakBim.Bridge.AutoCAD.dll"), Path.Combine(contents, "BurakBim.Bridge.AutoCAD.dll"), true);
            File.WriteAllText(Path.Combine(contents, "burakbim.probe"), bridgeDir);
            // the registry release (e.g. R24.3) of every installed AutoCAD-based product of that year
            var releases = products.Where(p => p.Family == "autocad" && p.Year == y && p.AcadRegKey != null)
                .Select(p => p.AcadRegKey!.Split('\\').FirstOrDefault(s => s.StartsWith("R") && s.Contains('.')))
                .Where(r => r != null).Distinct().ToList();
            if (releases.Count == 0 && AcadRelease.TryGetValue(y, out var rel)) releases.Add(rel);
            foreach (var r in releases)
                comps.Append($@"  <Components Description=""{r}"">
    <RuntimeRequirements OS=""Win64"" Platform=""AutoCAD*"" SeriesMin=""{r}"" SeriesMax=""{r}"" />
    <ComponentEntry AppName=""BurakBim{y}"" ModuleName=""./Contents/{y}/BurakBim.Bridge.AutoCAD.dll"" AppDescription=""BURAK BIM AI connector"" LoadOnAutoCADStartup=""True"" />
  </Components>
");
            Ok($"AutoCAD-family {y}: bridge staged (AutoCAD, Civil 3D, Plant 3D, Map 3D, MEP, Advance Steel ...)");
        }
        File.WriteAllText(Path.Combine(bundle, "PackageContents.xml"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ApplicationPackage SchemaVersion=""1.0"" AutodeskProduct=""AutoCAD"" ProductType=""Application"" Name=""BURAK BIM"" Description=""BURAK BIM - AI connector for AutoCAD and AutoCAD-based products"" AppVersion=""{BurakInfo.Version}"" Author=""BURAK BIM"" ProductCode=""{{5F2C9A41-7B3E-4D2A-9C61-1E8B0D4F7A22}}"" UpgradeCode=""{{0B7E4D19-3A6C-4F58-8E2D-6C1A9F3B5D70}}"">
  <CompanyDetails Name=""BURAK BIM"" />
{comps}</ApplicationPackage>
", new UTF8Encoding(false));
    }

    static void RegisterNavisworks(List<string> installed)
    {
        var years = installed.Where(k => k.StartsWith("navisworks-")).Select(k => int.Parse(k[11..])).OrderBy(y => y).ToList();
        if (years.Count == 0) return;
        var bundle = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins", "BurakBim.Navisworks.bundle");
        var comps = new StringBuilder();
        foreach (var y in years)
        {
            var contents = Path.Combine(bundle, "Contents", y.ToString());
            Directory.CreateDirectory(contents);
            var bridgeDir = Path.Combine(BurakPaths.Root, "bridges", "navisworks", y.ToString());
            File.Copy(Path.Combine(bridgeDir, "BurakBim.Bridge.Navisworks.dll"), Path.Combine(contents, "BurakBim.Bridge.Navisworks.dll"), true);
            File.WriteAllText(Path.Combine(contents, "burakbim.probe"), bridgeDir);
            var series = "Nw" + (y - 2003);
            foreach (var plat in new[] { "NAVMAN", "NAVSIM" })
                comps.Append($@"  <Components>
    <RuntimeRequirements OS=""Win64"" Platform=""{plat}"" SeriesMin=""{series}"" SeriesMax=""{series}"" />
    <ComponentEntry AppName=""BurakBim.Navisworks.{y}.{plat}"" AppType=""ManagedPlugin"" ModuleName=""./Contents/{y}/BurakBim.Bridge.Navisworks.dll"" />
  </Components>
");
            Ok($"Navisworks {y}: plug-in staged");
        }
        File.WriteAllText(Path.Combine(bundle, "PackageContents.xml"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ApplicationPackage SchemaVersion=""1.0"" AppVersion=""{BurakInfo.Version}"" AutodeskProduct=""Navisworks"" Name=""BURAK BIM"" Description=""BURAK BIM - AI connector for Navisworks"" ProductCode=""{{A3D1E6B2-58C4-4F0B-9E7A-2D4C6B8F1E33}}"">
  <CompanyDetails Name=""BURAK BIM"" />
{comps}</ApplicationPackage>
", new UTF8Encoding(false));
    }

    // ------------------------------------------------------------------ tabs in programs without a bridge
    /// <summary>Rhino 7+ loads anything in its per-user packages folder. Rhino has no ribbon, so the tab is a panel.</summary>
    static void InstallRhinoPanel(List<Product> products)
    {
        var plugin = Path.Combine(Setup, "prebuilt", "rhino", "BurakBim.Rhino.rhp");
        foreach (var major in products.Where(p => p.Family == "rhino" && p.Year >= 7).Select(p => p.Year).Distinct())
        {
            try
            {
                if (!File.Exists(plugin)) { Warn("Rhino " + major + ": the BURAK BIM panel is not in this package."); continue; }
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "McNeel", "Rhinoceros", "packages", major + ".0", "burak-bim", BurakInfo.Version);
                Directory.CreateDirectory(dir);
                File.Copy(plugin, Path.Combine(dir, "BurakBim.Rhino.rhp"), true);
                File.WriteAllText(Path.Combine(dir, "manifest.yml"), "name: burak-bim\nversion: " + BurakInfo.Version + "\nauthors:\n- BURAK BIM\ndescription: BURAK BIM panel with connection and contact details\nurl: https://github.com/BURAKAFRIDI92/BURAK-BIM\n", new UTF8Encoding(false));
                Ok("Rhino " + major + ": BURAK BIM panel installed (open it with the command BurakBim)");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warn("Rhino " + major + ": close Rhino and run setup again (" + ex.Message + ")"); }
        }
    }

    /// <summary>Autodesk's Inventor interop is not on NuGet, so the add-in is compiled here against the installed one.</summary>
    static void InstallInventorTab(List<Product> products)
    {
        foreach (var g in products.Where(p => p.Family == "inventor" && p.Year >= 2022).GroupBy(p => p.Year))
        {
            var year = g.Key;
            var dst = Path.Combine(BurakPaths.Root, "bridges", "inventor", year.ToString());
            if (!CompileBridge("inventor", year, g.First(), dst, out var why))
            {
                Warn($"Inventor {year}: the BURAK BIM tab could not be built ({why}). AI tools still reach Inventor without it.");
                continue;
            }
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Inventor " + year, "Addins");
                Directory.CreateDirectory(dir);
                var dll = System.Security.SecurityElement.Escape(Path.Combine(dst, "BurakBim.Plugin.Inventor.dll"));
                File.WriteAllText(Path.Combine(dir, "BurakBim.addin"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Addin Type=""Standard"">
  <ClassId>{{2c53dc5f-5e0b-4cf4-b9db-7c263e34cf41}}</ClassId>
  <ClientId>{{2c53dc5f-5e0b-4cf4-b9db-7c263e34cf41}}</ClientId>
  <DisplayName>BURAK BIM</DisplayName>
  <Description>BURAK BIM tab with connection and contact details</Description>
  <Assembly>{dll}</Assembly>
  <LoadOnStartUp>1</LoadOnStartUp>
  <UserUnloadable>1</UserUnloadable>
  <Hidden>0</Hidden>
</Addin>
", new UTF8Encoding(false));
                Ok($"Inventor {year}: BURAK BIM tab compiled and registered");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warn($"Inventor {year}: close Inventor and run setup again ({ex.Message})"); }
        }
    }

    // ------------------------------------------------------------------ skills for Claude Code / Claude Desktop skill folders
    static void InstallSkills()
    {
        var all = Skills.SkillLibrary.All();
        Ok($"{all.Count} skills built into the server (every AI app: burak_skills tool, MCP prompts and resources)");
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!Directory.Exists(Path.Combine(home, ".claude"))) return;
            var dir = Path.Combine(home, ".claude", "skills");
            foreach (var s in all)
            {
                var d = Path.Combine(dir, "burak-bim-" + s.Name);
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, "SKILL.md"), s.Markdown, new UTF8Encoding(false));
            }
            Ok($"Claude Code: skills copied to {dir}");
        }
        catch (Exception ex) { Warn("skills for Claude Code: " + ex.Message); }
    }

    // ------------------------------------------------------------------ helpers
    /// <summary>Versions before 1.3 registered a logon task that ran PowerShell. 1.3 has none, so an upgrade removes it.</summary>
    static void RemoveOldSentinel()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", "/Delete /TN \"BURAK BIM Sentinel\" /F") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            if (p.HasExited && p.ExitCode == 0) Ok("Old logon task 'BURAK BIM Sentinel' removed");
        }
        catch (Exception ex) { Warn("Could not remove the old logon task: " + ex.Message); }
    }

    static int Unblock(string dir)
    {
        int n = 0;
        if (!Directory.Exists(dir)) return 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { var ads = f + ":Zone.Identifier"; if (File.Exists(ads)) { File.Delete(ads); n++; } } catch { }
        }
        return n;
    }

    static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    static bool SelfCheck()
    {
        try
        {
            var server = new Mcp.McpServer(Program.BuildRegistry());
            var init = server.Dispatch(Json.O("jsonrpc", "2.0", "id", 1, "method", "initialize", "params", Json.O("protocolVersion", "2025-06-18", "clientInfo", Json.O("name", "burak-installer"))));
            var list = server.Dispatch(Json.O("jsonrpc", "2.0", "id", 2, "method", "tools/list"));
            return init != null && list != null && Json.Obj(list, "result") != null;
        }
        catch { return false; }
    }
}
