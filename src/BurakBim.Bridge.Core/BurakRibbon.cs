// BURAK BIM ribbon tab for hosts built on Autodesk's AdWindows ribbon (AutoCAD and its verticals, Navisworks).
// Late-bound on purpose, like RibbonAutomation: no compile-time reference to AdWindows.dll, so one build fits every year.
// The ribbon may not exist yet when a plug-in loads, and a workspace change can rebuild it, so the tab is checked
// on a timer and added again whenever it is missing.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BurakBim.Shared;

namespace BurakBim.Bridge
{
    public sealed class RibbonAction
    {
        public string Panel, Text, ToolTip;
        /// <summary>Runs on the host's main thread. A returned text is shown to the user.</summary>
        public Func<string> Run;
    }

    public static class BurakRibbon
    {
        const string TabId = "BURAK_BIM_TAB";
        const string TabTitle = "BURAK BIM";

        static Timer _timer;
        static IHostAdapter _host;
        static List<RibbonAction> _actions;
        static Assembly _brand;
        static int _busy;
        static bool _logged;

        public static void Keep(IHostAdapter host, IEnumerable<RibbonAction> actions, Assembly brand)
        {
            _host = host;
            _actions = actions.ToList();
            _brand = brand;
            _timer = new Timer(Tick, null, 2000, 5000);
        }

        public static void Stop()
        {
            var t = _timer;
            _timer = null;
            if (t != null) t.Dispose();
        }

        /// <summary>The buttons every host shares.</summary>
        public static List<RibbonAction> CommonActions(IHostAdapter host, BridgeServer server)
        {
            return new List<RibbonAction>
            {
                new RibbonAction
                {
                    Panel = "Connection", Text = "Status", ToolTip = "Is BURAK BIM connected to this program, and how do you use it from your AI app?",
                    Run = () => "BURAK BIM " + BurakInfo.Version + "\n\nProgram: " + host.Product +
                        "\nConnector: " + (server.IsRunning ? "running on port " + server.Port : "stopped") +
                        "\n\nAsk your AI app: Use BURAK BIM: burak_status"
                },
                new RibbonAction { Panel = "Connection", Text = "Reports", ToolTip = "Open the folder with BURAK BIM reports.", Run = () => OpenFolder(BurakPaths.ReportsDir) },
                new RibbonAction { Panel = "Connection", Text = "Logs", ToolTip = "Open the folder with BURAK BIM logs.", Run = () => OpenFolder(BurakPaths.LogsDir) }
            };
        }

        static string OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            Process.Start("explorer.exe", "\"" + path + "\"");
            return null;
        }

        static void Tick(object state)
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                _host.InvokeOnMain(() =>
                {
                    if (Ensure())
                    {
                        var t = _timer;
                        if (t != null) t.Change(30000, 30000);
                    }
                    return null;
                }, 60000);
            }
            catch (Exception ex)
            {
                if (!_logged) { _logged = true; BridgeLog.Write("BURAK BIM tab: " + ex.Message); }
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        /// <summary>True when the tab is on the ribbon. False while the ribbon does not exist yet.</summary>
        static bool Ensure()
        {
            var cm = Reflector.FindType("Autodesk.Windows.ComponentManager");
            var prop = cm == null ? null : cm.GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static);
            var ribbon = prop == null ? null : prop.GetValue(null, null);
            if (ribbon == null) return false;
            var tabs = Get(ribbon, "Tabs");
            foreach (var t in (IEnumerable)tabs)
                if (Convert.ToString(Get(t, "Id")) == TabId) return true;
            AddTo(tabs, BuildTab());
            BridgeLog.Write("BURAK BIM tab added to the ribbon");
            return true;
        }

        static object BuildTab()
        {
            var tab = New("Autodesk.Windows.RibbonTab");
            Set(tab, "Title", TabTitle);
            Set(tab, "Id", TabId);
            var large = Image("brand.burakbim-32.png");
            var small = Image("brand.burakbim-16.png");
            foreach (var group in _actions.GroupBy(a => a.Panel))
            {
                var source = New("Autodesk.Windows.RibbonPanelSource");
                Set(source, "Title", group.Key);
                var panel = New("Autodesk.Windows.RibbonPanel");
                Set(panel, "Source", source);
                foreach (var action in group)
                {
                    var button = New("Autodesk.Windows.RibbonButton");
                    Set(button, "Text", action.Text);
                    Set(button, "Id", "BURAK_BIM_" + action.Panel.Replace(" ", "") + "_" + action.Text.Replace(" ", ""));
                    Set(button, "ShowText", true);
                    Set(button, "ToolTip", action.ToolTip);
                    Set(button, "Size", Enum.Parse(Reflector.FindType("Autodesk.Windows.RibbonItemSize"), "Large"));
                    var orientation = Reflector.FindType("System.Windows.Controls.Orientation");
                    if (orientation != null) Set(button, "Orientation", Enum.Parse(orientation, "Vertical"));
                    if (large != null) { Set(button, "LargeImage", large); Set(button, "ShowImage", true); }
                    if (small != null) Set(button, "Image", small);
                    Set(button, "CommandHandler", new ActionCommand(action));
                    AddTo(Get(source, "Items"), button);
                }
                AddTo(Get(tab, "Panels"), panel);
            }
            return tab;
        }

        static object New(string type)
        {
            var t = Reflector.FindType(type);
            if (t == null) throw new BridgeException(type + " is not available in this host.");
            return Activator.CreateInstance(t);
        }

        static object Get(object o, string name) { return o.GetType().GetProperty(name).GetValue(o, null); }

        static void Set(object o, string name, object value)
        {
            var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(o, value, null);
        }

        static void AddTo(object collection, object item)
        {
            var list = collection as IList;
            if (list != null) { list.Add(item); return; }
            collection.GetType().GetMethod("Add", new[] { item.GetType() }).Invoke(collection, new[] { item });
        }

        /// <summary>WPF image from an embedded PNG, created by reflection. A button without a picture still works.</summary>
        static object Image(string resource)
        {
            try
            {
                var type = Reflector.FindType("System.Windows.Media.Imaging.BitmapImage");
                var stream = _brand == null ? null : _brand.GetManifestResourceStream(resource);
                if (type == null || stream == null) return null;
                var image = Activator.CreateInstance(type);
                type.GetMethod("BeginInit").Invoke(image, null);
                Set(image, "StreamSource", stream);
                Set(image, "CacheOption", Enum.Parse(Reflector.FindType("System.Windows.Media.Imaging.BitmapCacheOption"), "OnLoad"));
                type.GetMethod("EndInit").Invoke(image, null);
                type.GetMethod("Freeze", Type.EmptyTypes).Invoke(image, null);
                return image;
            }
            catch { return null; }
        }

        static void Show(string text)
        {
            var box = Reflector.FindType("System.Windows.MessageBox");
            if (box != null) box.GetMethod("Show", new[] { typeof(string), typeof(string) }).Invoke(null, new object[] { text, TabTitle });
        }

        sealed class ActionCommand : System.Windows.Input.ICommand
        {
            readonly RibbonAction _action;
            public ActionCommand(RibbonAction action) { _action = action; }
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) { return true; }

            public void Execute(object parameter)
            {
                try
                {
                    var text = _action.Run();
                    if (!string.IsNullOrEmpty(text)) Show(text);
                }
                catch (Exception ex)
                {
                    BridgeLog.Write("BURAK BIM tab, " + _action.Text + ": " + ex);
                    Show(_action.Text + " could not finish: " + ex.Message);
                }
            }
        }
    }
}
