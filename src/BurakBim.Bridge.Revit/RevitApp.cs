// BURAK BIM - Revit bridge add-in (Revit 2022-2027).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using BurakBim.Shared;

namespace BurakBim.Bridge.Revit
{
    public sealed class RevitApp : IExternalApplication
    {
        internal static BridgeServer Server;
        internal static RevitHost Host;
        internal static UIControlledApplication UiCtrl;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                UiCtrl = application;
                BridgeLog.Name = "revit-" + application.ControlledApplication.VersionNumber;
                var here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                BridgeAssemblyResolver.Register(here);
                Host = new RevitHost(application);
                Server = new BridgeServer(Host);
                RevitCommands.Register(Server, Host);
                RevitScan.Register(Server, Host);
                RevitInspector.Register(Server, Host);
                application.ControlledApplication.DocumentChanged += Host.OnDocumentChanged;
                CreateRibbon(application, here);
                if (BurakConfig.AutoStart) Server.Start();
            }
            catch (Exception ex)
            {
                BridgeLog.Write("Startup failed: " + ex);
            }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try { if (Server != null) Server.Stop(); } catch { }
            return Result.Succeeded;
        }

        static void CreateRibbon(UIControlledApplication app, string here)
        {
            const string tab = "BURAK BIM";
            try { app.CreateRibbonTab(tab); } catch { }
            var panel = app.CreateRibbonPanel(tab, "AI Connector");
            var asm = Assembly.GetExecutingAssembly().Location;
            var items = new List<RibbonItem>();
            items.Add(panel.AddItem(new PushButtonData("BurakBimToggle", "Start /\nStop", asm, typeof(ToggleCommand).FullName)
            { ToolTip = "Start or stop the BURAK BIM connector so AI agents (Claude, Codex, Antigravity...) can work in this Revit session." }));
            items.Add(panel.AddItem(new PushButtonData("BurakBimStatus", "Status", asm, typeof(StatusCommand).FullName)
            { ToolTip = "Show connector status, port and install folder." }));
            var review = app.CreateRibbonPanel(tab, "BIM Inspector");
            items.Add(review.AddItem(new PushButtonData("BurakBimReview", "AI Review\nView", asm, typeof(ReviewCommand).FullName)
            { ToolTip = "Create/refresh a 3D view that colors every element created by AI: red = unverified, green = verified, orange = rejected." }));
            items.Add(review.AddItem(new PushButtonData("BurakBimVerify", "Verify\nSelected", asm, typeof(VerifyCommand).FullName)
            { ToolTip = "Mark the selected AI-created elements as verified by you (inspector sign-off)." }));
            items.Add(review.AddItem(new PushButtonData("BurakBimReject", "Reject\nSelected", asm, typeof(RejectCommand).FullName)
            { ToolTip = "Mark the selected AI-created elements as rejected (needs rework)." }));
            // BURAK-BIM company logo on every button (loaded late-bound so the bridge builds without WPF reference assemblies)
            var large = BrandImage("brand.burakbim-32.png"); var small = BrandImage("brand.burakbim-16.png");
            foreach (var it in items)
            {
                try
                {
                    var t = it.GetType();
                    if (large != null) t.GetProperty("LargeImage").SetValue(it, large, null);
                    if (small != null) t.GetProperty("Image").SetValue(it, small, null);
                }
                catch { }
            }
        }

        /// <summary>WPF BitmapImage from an embedded PNG, created by reflection (PresentationCore is always loaded in Revit).</summary>
        internal static object BrandImage(string resource)
        {
            try
            {
                var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
                if (s == null) return null;
                var t = Type.GetType("System.Windows.Media.Imaging.BitmapImage, PresentationCore", false)
                        ?? AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "PresentationCore").Select(a => a.GetType("System.Windows.Media.Imaging.BitmapImage")).FirstOrDefault(x => x != null);
                if (t == null) return null;
                var img = Activator.CreateInstance(t);
                t.GetMethod("BeginInit").Invoke(img, null);
                t.GetProperty("StreamSource").SetValue(img, s, null);
                var cache = t.GetProperty("CacheOption");
                cache.SetValue(img, Enum.Parse(cache.PropertyType, "OnLoad"), null);
                t.GetMethod("EndInit").Invoke(img, null);
                var freeze = t.GetMethod("Freeze", Type.EmptyTypes); if (freeze != null) freeze.Invoke(img, null);
                return img;
            }
            catch { return null; }
        }
    }

    public static class BurakConfig
    {
        public static bool AutoStart
        {
            get
            {
                try
                {
                    var f = Path.Combine(BurakPaths.ConfigDir, "burakbim.json");
                    if (!File.Exists(f)) return true;
                    return Json.Bool(Json.ParseObject(File.ReadAllText(f)), "bridgeAutoStart", true);
                }
                catch { return true; }
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class ToggleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            if (RevitApp.Server == null) { message = "BURAK BIM failed to load. See logs."; return Result.Failed; }
            if (RevitApp.Server.IsRunning) RevitApp.Server.Stop(); else RevitApp.Server.Start();
            TaskDialog.Show("BURAK BIM", RevitApp.Server.IsRunning
                ? "Connector is RUNNING on port " + RevitApp.Server.Port + ".\nAI agents can now work in this Revit session."
                : "Connector STOPPED. AI agents cannot reach this Revit session.");
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class StatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var s = RevitApp.Server;
            TaskDialog.Show("BURAK BIM " + BurakInfo.Version,
                "Status: " + (s != null && s.IsRunning ? "RUNNING (port " + s.Port + ")" : "STOPPED") +
                "\nRevit: " + data.Application.Application.VersionName +
                "\nInstall folder: " + BurakPaths.Root +
                "\nScripting (C#): " + (ScriptRunner.Available ? "enabled" : "not built") +
                "\nAudit log: " + BurakPaths.AuditDir);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class ReviewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                var r = RevitInspector.BuildReviewView(data.Application, true, false);
                TaskDialog.Show("BURAK BIM - AI Review", Json.Serialize(r, true));
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class VerifyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            return RevitInspector.MarkSelection(data.Application, "verified", ref message);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class RejectCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            return RevitInspector.MarkSelection(data.Application, "rejected", ref message);
        }
    }

    /// <summary>IHostAdapter for Revit: ExternalEvent main-thread pump, transaction groups with AI stamping.</summary>
    public sealed class RevitHost : IHostAdapter, IExternalEventHandler
    {
        readonly UIControlledApplication _ctrl;
        readonly ExternalEvent _event;
        readonly MainThreadQueue _queue;
        readonly IntPtr _mainWindow;
        internal UIApplication UiApp;
        readonly Dictionary<string, Func<object>> _roots;

        // element-change capture for AI stamping
        internal bool Capturing;
        internal readonly List<ElementId> Added = new List<ElementId>();
        internal readonly List<ElementId> Modified = new List<ElementId>();

        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);

        public RevitHost(UIControlledApplication ctrl)
        {
            _ctrl = ctrl;
            _event = ExternalEvent.Create(this);
            try { _mainWindow = ctrl.MainWindowHandle; } catch { _mainWindow = IntPtr.Zero; }
            _queue = new MainThreadQueue(() =>
            {
                _event.Raise();
                // nudge Revit's message loop so the event is handled even when the mouse is idle
                if (_mainWindow != IntPtr.Zero) PostMessage(_mainWindow, 0, IntPtr.Zero, IntPtr.Zero);
            });
            _roots = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase)
            {
                { "uiapp", () => UiApp },
                { "app", () => UiApp == null ? null : UiApp.Application },
                { "uidoc", () => UiApp == null ? null : UiApp.ActiveUIDocument },
                { "doc", () => Doc },
                { "view", () => Doc == null ? null : Doc.ActiveView },
                { "selection", () => UiApp == null || UiApp.ActiveUIDocument == null ? null : UiApp.ActiveUIDocument.Selection.GetElementIds() }
            };
        }

        public string Host { get { return "revit"; } }
        public string Product { get { return "Autodesk Revit " + _ctrl.ControlledApplication.VersionNumber; } }
        public int Year { get { int y; int.TryParse(_ctrl.ControlledApplication.VersionNumber, out y); return y; } }
        public IDictionary<string, Func<object>> Roots { get { return _roots; } }

        public Document Doc
        {
            get
            {
                if (UiApp == null || UiApp.ActiveUIDocument == null) return null;
                return UiApp.ActiveUIDocument.Document;
            }
        }

        public Document RequireDoc()
        {
            var d = Doc;
            if (d == null) throw new BridgeException("No Revit model is open. Open a project (or use revit_open) first.");
            return d;
        }

        public object InvokeOnMain(Func<object> work, int timeoutMs) { return _queue.Invoke(work, timeoutMs); }

        // IExternalEventHandler
        public void Execute(UIApplication app)
        {
            UiApp = app;
            _queue.Drain();
        }
        public string GetName() { return "BURAK BIM bridge"; }

        public void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            if (!Capturing) return;
            try
            {
                Added.AddRange(e.GetAddedElementIds());
                Modified.AddRange(e.GetModifiedElementIds());
            }
            catch { }
        }

        /// <summary>
        /// One undo step per AI action: TransactionGroup { work transaction ; stamp transaction } assimilated.
        /// Warnings are auto-dismissed and returned; unresolvable errors roll everything back.
        /// </summary>
        public object RunWrite(string label, Func<object> work)
        {
            var doc = Doc;
            if (doc == null) return work(); // e.g. opening a document
            if (doc.IsModifiable) return work(); // already inside a transaction (nested call)
            if (doc.IsReadOnly) throw new BridgeException("The active document is read-only.");
            var collector = new FailureCollector();
            using (var group = new TransactionGroup(doc, label))
            {
                group.Start();
                object result;
                Added.Clear(); Modified.Clear();
                using (var t = new Transaction(doc, label))
                {
                    var opts = t.GetFailureHandlingOptions();
                    opts.SetFailuresPreprocessor(collector);
                    opts.SetClearAfterRollback(true);
                    t.SetFailureHandlingOptions(opts);
                    t.Start();
                    Capturing = true;
                    try
                    {
                        result = work();
                    }
                    catch
                    {
                        Capturing = false;
                        if (t.HasStarted() && !t.HasEnded()) t.RollBack();
                        group.RollBack();
                        throw;
                    }
                    var status = t.Commit();
                    Capturing = false;
                    if (status != TransactionStatus.Committed)
                    {
                        group.RollBack();
                        throw new BridgeException("Revit rolled the change back: " + string.Join("; ", collector.Errors.ToArray()));
                    }
                }
                var created = new List<ElementId>(Added);
                if (created.Count > 0)
                {
                    using (var stamp = new Transaction(doc, "BURAK BIM stamp"))
                    {
                        stamp.Start();
                        RevitInspector.Stamp(doc, created, label);
                        stamp.Commit();
                    }
                }
                group.Assimilate();
                if (collector.Warnings.Count == 0 && created.Count == 0) return result;
                return Json.O("result", result,
                    "createdIds", created.Where(x => RevitInspector.IsReviewable(doc.GetElement(x))).Select(x => (object)Ids.Of(x)).ToList(),
                    "warnings", collector.Warnings);
            }
        }

        public ScriptGlobals CreateScriptGlobals()
        {
            return new RevitScriptGlobals
            {
                uiapp = UiApp,
                app = UiApp == null ? null : UiApp.Application,
                uidoc = UiApp == null ? null : UiApp.ActiveUIDocument,
                doc = Doc
            };
        }

        public string[] ScriptImports
        {
            get
            {
                return new[] { "System", "System.Linq", "System.Collections.Generic", "Autodesk.Revit.DB", "Autodesk.Revit.UI",
                    "Autodesk.Revit.DB.Architecture", "Autodesk.Revit.DB.Structure", "Autodesk.Revit.DB.Mechanical",
                    "Autodesk.Revit.DB.Plumbing", "Autodesk.Revit.DB.Electrical" };
            }
        }

        public object TryInvokeRibbonItem(string id, object item)
        {
            if (string.IsNullOrEmpty(id)) return null;
            RevitCommandId cid = null;
            try { cid = RevitCommandId.LookupCommandId(id); } catch { }
            if (cid == null) return null;
            if (!UiApp.CanPostCommand(cid)) return null; // toggles/check boxes etc.: use the ribbon item's own handler
            UiApp.PostCommand(cid);
            return Json.O("posted", id, "via", "Revit PostCommand",
                "note", "The command starts as soon as this call returns. If it needs picks or a dialog, finish it in Revit or drive it with ui_* tools.");
        }
    }

    /// <summary>Collects warnings (auto-dismissed) and errors (resolved when Revit offers a default resolution).</summary>
    public sealed class FailureCollector : IFailuresPreprocessor
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor fa)
        {
            bool hadError = false;
            foreach (var f in fa.GetFailureMessages())
            {
                var text = f.GetDescriptionText();
                if (f.GetSeverity() == FailureSeverity.Warning)
                {
                    Warnings.Add(text);
                    fa.DeleteWarning(f);
                }
                else
                {
                    Errors.Add(text);
                    if (f.HasResolutions()) { fa.ResolveFailure(f); hadError = true; }
                    else return FailureProcessingResult.ProceedWithRollBack;
                }
            }
            return hadError ? FailureProcessingResult.ProceedWithCommit : FailureProcessingResult.Continue;
        }
    }

    /// <summary>ElementId helpers that work on Revit 2022-2027 (int ids before 2024, long ids from 2024).</summary>
    public static class Ids
    {
        public static long Of(ElementId id)
        {
#if REVIT_LONG_IDS
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId Make(long v)
        {
#if REVIT_LONG_IDS
            return new ElementId(v);
#else
            return new ElementId((int)v);
#endif
        }
    }
}
