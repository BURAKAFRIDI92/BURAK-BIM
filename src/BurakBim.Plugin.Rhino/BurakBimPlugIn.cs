// BURAK BIM panel for Rhino: Rhino's closest match to a ribbon tab. It carries the same Connection and About sections
// as the tab in Revit, AutoCAD and Navisworks. AI tools reach Rhino separately through COM, so this plug-in only adds the panel.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BurakBim.Shared;
using Eto.Forms;
using Rhino;
using Rhino.Commands;
using Rhino.PlugIns;
using Rhino.UI;

[assembly: Guid("edb65c1a-2487-4f58-bf59-8ee76d7a40c7")]
[assembly: AssemblyTitle("BURAK BIM")]
[assembly: PlugInDescription(DescriptionType.Organization, "BURAK BIM")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "https://github.com/BURAKAFRIDI92/BURAK-BIM")]

namespace BurakBim.Plugin.RhinoTab
{
    public sealed class BurakBimPlugIn : PlugIn
    {
        public override PlugInLoadTime LoadTime { get { return PlugInLoadTime.AtStartup; } }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            System.Drawing.Icon icon = null;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("brand.burakbim.ico"))
                if (s != null) icon = new System.Drawing.Icon(s);
            Panels.RegisterPanel(this, typeof(BurakPanel), "BURAK BIM", icon);
            // show the panel the first time only: after that Rhino remembers what the user chose
            if (!Settings.GetBool("PanelShown", false))
                RhinoApp.Idled += OpenOnce;
            return LoadReturnCode.Success;
        }

        void OpenOnce(object sender, EventArgs e)
        {
            RhinoApp.Idled -= OpenOnce;
            Settings.SetBool("PanelShown", true);
            Panels.OpenPanel(typeof(BurakPanel).GUID);
        }
    }

    [Guid("99068322-7dd8-4b37-bb31-189afc384bf2")]
    public sealed class BurakPanel : Panel
    {
        public BurakPanel(uint documentSerialNumber)
        {
            var layout = new DynamicLayout { Padding = 8, Spacing = new Eto.Drawing.Size(4, 4) };
            layout.AddRow(new Label { Text = "Connection" });
            layout.AddRow(Button("Status", Actions.ShowStatus));
            layout.AddRow(Button("Reports", () => Actions.OpenFolder(BurakPaths.ReportsDir)));
            layout.AddRow(Button("Logs", () => Actions.OpenFolder(BurakPaths.LogsDir)));
            layout.AddRow(new Label { Text = "About" });
            layout.AddRow(Button("About", Actions.ShowAbout));
            layout.AddRow(null);
            Content = layout;
        }

        static Button Button(string text, Action action)
        {
            var b = new Button { Text = text };
            b.Click += (s, e) => action();
            return b;
        }
    }

    static class Actions
    {
        public static void ShowAbout() { MessageBox.Show(BurakInfo.AboutText, "BURAK BIM"); }

        public static void ShowStatus()
        {
            MessageBox.Show("BURAK BIM " + BurakInfo.Version + "\n\nProgram: Rhinoceros " + RhinoApp.ExeVersion +
                "\nInstall folder: " + BurakPaths.Root + "\n\nAsk your AI app: Use BURAK BIM: burak_status", "BURAK BIM");
        }

        public static void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            Process.Start("explorer.exe", "\"" + path + "\"");
        }
    }

    public sealed class BurakBimCommand : Command
    {
        public override string EnglishName { get { return "BurakBim"; } }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Panels.OpenPanel(typeof(BurakPanel).GUID);
            return Result.Success;
        }
    }

    public sealed class BurakBimAboutCommand : Command
    {
        public override string EnglishName { get { return "BurakBimAbout"; } }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Actions.ShowAbout();
            return Result.Success;
        }
    }
}
