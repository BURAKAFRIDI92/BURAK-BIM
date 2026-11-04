// BURAK BIM tab for Autodesk Inventor. Autodesk's interop is not on NuGet, so setup compiles this file on the user's PC
// against the installed Autodesk.Inventor.Interop.dll. AI tools reach Inventor separately through COM.
// Everything except the add-in interface and the button event is late-bound, so one source fits every year.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BurakBim.Shared;

namespace BurakBim.Plugin.InventorTab
{
    [ComVisible(true)]
    [Guid("2c53dc5f-5e0b-4cf4-b9db-7c263e34cf41")]
    public sealed class BurakAddIn : global::Inventor.ApplicationAddInServer
    {
        const string ClientId = "{2c53dc5f-5e0b-4cf4-b9db-7c263e34cf41}";
        static readonly string[] Ribbons = { "ZeroDoc", "Part", "Assembly", "Drawing", "Presentation" };

        dynamic _app;
        // COM events stop firing if the button objects are collected
        readonly List<object> _keep = new List<object>();

        public object Automation { get { return null; } }
        public void ExecuteCommand(int commandID) { }

        public void Activate(global::Inventor.ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _app = addInSiteObject.Application;
            try
            {
                var buttons = new[]
                {
                    Button("Connection", "Status", "Is BURAK BIM installed, and how do you use it from your AI app?", ShowStatus),
                    Button("Connection", "Reports", "Open the folder with BURAK BIM reports.", () => OpenFolder(BurakPaths.ReportsDir)),
                    Button("Connection", "Logs", "Open the folder with BURAK BIM logs.", () => OpenFolder(BurakPaths.LogsDir)),
                    Button("About", "About", "About BURAK BIM and contact details.", ShowAbout)
                };
                foreach (var ribbonName in Ribbons)
                {
                    dynamic ribbon;
                    try { ribbon = _app.UserInterfaceManager.Ribbons[ribbonName]; }
                    catch { continue; } // this ribbon does not exist in every Inventor year
                    dynamic tab = TabOf(ribbon, ribbonName);
                    foreach (var b in buttons) AddToTab(tab, ribbonName, b);
                }
            }
            catch (Exception ex) { Log("Activate failed: " + ex); }
        }

        public void Deactivate()
        {
            _keep.Clear();
            _app = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        sealed class TabButton { public string Panel; public object Definition; }

        TabButton Button(string panel, string text, string tip, Action action)
        {
            dynamic definitions = _app.CommandManager.ControlDefinitions;
            var definition = (global::Inventor.ButtonDefinition)definitions.AddButtonDefinition(text, "BurakBim_" + text,
                global::Inventor.CommandTypesEnum.kQueryOnlyCmdType, ClientId, tip, tip);
            definition.OnExecute += context =>
            {
                try { action(); }
                catch (Exception ex) { Log(text + ": " + ex); System.Windows.Forms.MessageBox.Show(text + " could not finish: " + ex.Message, "BURAK BIM"); }
            };
            _keep.Add(definition);
            return new TabButton { Panel = panel, Definition = definition };
        }

        // the ribbon keeps a tab between sessions, so reuse it when it is already there
        static dynamic TabOf(dynamic ribbon, string ribbonName)
        {
            var id = "BurakBim_Tab_" + ribbonName;
            try { return ribbon.RibbonTabs[id]; }
            catch { return ribbon.RibbonTabs.Add("BURAK BIM", id, ClientId); }
        }

        static void AddToTab(dynamic tab, string ribbonName, TabButton button)
        {
            var panelId = "BurakBim_" + button.Panel + "_" + ribbonName;
            dynamic panel;
            try { panel = tab.RibbonPanels[panelId]; }
            catch { panel = tab.RibbonPanels.Add(button.Panel, panelId, ClientId); }
            try { panel.CommandControls.AddButton((dynamic)button.Definition, true); }
            catch (COMException) { } // already on the panel from an earlier session
        }

        void ShowStatus()
        {
            string version = "";
            try { version = " " + (string)_app.SoftwareVersion.DisplayVersion; } catch { }
            System.Windows.Forms.MessageBox.Show("BURAK BIM " + BurakInfo.Version + "\n\nProgram: Autodesk Inventor" + version +
                "\nInstall folder: " + BurakPaths.Root + "\n\nAsk your AI app: Use BURAK BIM: burak_status", "BURAK BIM");
        }

        static void ShowAbout() { System.Windows.Forms.MessageBox.Show(BurakInfo.AboutText, "BURAK BIM"); }

        static void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            Process.Start("explorer.exe", "\"" + path + "\"");
        }

        static void Log(string text)
        {
            try
            {
                Directory.CreateDirectory(BurakPaths.LogsDir);
                File.AppendAllText(Path.Combine(BurakPaths.LogsDir, "inventor-addin.log"), DateTime.Now.ToString("s") + "  " + text + Environment.NewLine);
            }
            catch { }
        }
    }
}
