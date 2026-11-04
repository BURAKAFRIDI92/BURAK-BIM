using System.Runtime.InteropServices;
using BurakBim.Shared;

namespace BurakBim.Server.Core;

/// <summary>
/// 1.3: the user's permission for computer control (mouse + keyboard), used when no API can reach a command.
///  - The AI must ask with a reason; a dialog appears ON THE USER'S SCREEN (topmost) - the AI cannot click it itself
///    because computer-control tools are blocked until the user answers.
///  - Permission is time-limited (default 15 min, max 120) and only for this BURAK BIM session.
///  - While control is active a small banner is shown; Ctrl+Alt+Q (or computer_release_control) stops it instantly.
///  - "allowComputerControl": false in config\burakbim.json disables the feature completely.
/// </summary>
public static class Consent
{
    static readonly object Gate = new();
    static DateTime _until = DateTime.MinValue;
    static string _reason = "";
    static string _client = "";
    static int _actions;

    public static bool Active { get { lock (Gate) return DateTime.UtcNow < _until; } }
    public static DateTime Until { get { lock (Gate) return _until; } }
    public static string Reason { get { lock (Gate) return _reason; } }
    public static int Actions => _actions;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxTimeoutW")]
    static extern int MessageBoxTimeout(IntPtr h, string text, string caption, uint type, short lang, int ms);

    const uint MB_YESNO = 0x4, MB_ICONQUESTION = 0x20, MB_DEFBUTTON2 = 0x100, MB_SYSTEMMODAL = 0x1000, MB_SETFOREGROUND = 0x10000, MB_TOPMOST = 0x40000;
    const int IDYES = 6;

    public static Dictionary<string, object> Request(string reason, int minutes, ToolContext ctx)
    {
        if (!Json.Bool(Config.Data, "allowComputerControl", true))
            throw new ToolException("Computer control is disabled by the administrator (allowComputerControl=false in burakbim.json). Ask the user to do this step manually.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ToolException("Give a short reason the user will read, e.g. 'Revit has no API for the Material Browser asset tab - I will click through it'.");
        minutes = Math.Clamp(minutes <= 0 ? 15 : minutes, 1, 120);
        if (Active) { Extend(minutes); return Status("already granted"); }
        if (!OperatingSystem.IsWindows()) throw new ToolException("Computer control needs Windows.");

        var text =
            $"{ctx.Client} (via BURAK BIM) asks to CONTROL YOUR MOUSE AND KEYBOARD for up to {minutes} minutes.\n\n" +
            $"Why: {reason.Trim()}\n\n" +
            "While it works, please don't use the mouse or keyboard.\n" +
            "Stop at any time with Ctrl+Alt+Q.\n\n" +
            "Allow BURAK BIM to control this computer?";
        int answer;
        try { answer = MessageBoxTimeout(IntPtr.Zero, text, "BURAK BIM - permission to control your computer", MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 | MB_SYSTEMMODAL | MB_SETFOREGROUND | MB_TOPMOST, 0, 180_000); }
        catch { answer = Win32.MessageBoxW(IntPtr.Zero, text, "BURAK BIM - permission to control your computer", MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 | MB_SYSTEMMODAL | MB_SETFOREGROUND | MB_TOPMOST); }
        if (answer != IDYES)
        {
            Audit.Write(ctx, Json.O("reason", reason, "minutes", minutes), false, 0, null, answer == 32000 ? "no answer (timeout)" : "user declined", true);
            throw new ToolException(answer == 32000
                ? "The user did not answer the permission dialog within 3 minutes. Ask them in the chat to approve it, then call computer_request_control again - or explain the manual steps."
                : "The user DECLINED computer control. Do not retry without asking them in the chat first. Explain the manual steps instead.");
        }
        lock (Gate) { _until = DateTime.UtcNow.AddMinutes(minutes); _reason = reason.Trim(); _client = ctx.Client; _actions = 0; }
        Overlay.Show($"BURAK BIM: {Short(ctx.Client)} is controlling this PC - Ctrl+Alt+Q to stop");
        return Status("granted");
    }

    static string Short(string c) => c.Split(' ').FirstOrDefault() ?? "AI";

    static void Extend(int minutes) { lock (Gate) { var t = DateTime.UtcNow.AddMinutes(minutes); if (t > _until) _until = t; } }

    public static void Revoke(string why = "released")
    {
        lock (Gate) { _until = DateTime.MinValue; }
        Overlay.Hide();
        Console.Error.WriteLine("[BURAK BIM] computer control ended: " + why);
    }

    /// <summary>Called by every input tool. Throws a clear instruction when there is no permission.</summary>
    public static void Require()
    {
        if (!Active)
        {
            Overlay.Hide();
            throw new ToolException("No permission to control the computer (not granted, expired, or stopped by the user with Ctrl+Alt+Q). Call computer_request_control with a reason first; the user must click Yes on their screen.");
        }
        Interlocked.Increment(ref _actions);
    }

    public static Dictionary<string, object> Status(string state) => Json.O(
        "state", state, "active", Active,
        "expiresLocal", Active ? Until.ToLocalTime().ToString("HH:mm:ss") : null,
        "reason", Reason, "actions", _actions,
        "howToStop", "User: Ctrl+Alt+Q. Agent: computer_release_control.");
}

/// <summary>Small always-on-top banner while the AI controls the PC + the Ctrl+Alt+Q stop hotkey. Excluded from screenshots.</summary>
public static class Overlay
{
#if WINDOWS
    static Thread? _thread;
    static BannerForm? _form;
    static readonly ManualResetEventSlim Ready = new(false);

    public static void Show(string text)
    {
        try
        {
            if (_thread == null)
            {
                _thread = new Thread(() =>
                {
                    System.Windows.Forms.Application.EnableVisualStyles();
                    _form = new BannerForm();
                    _ = _form.Handle;            // create the window (hidden) so the hotkey and BeginInvoke work
                    Ready.Set();
                    System.Windows.Forms.Application.Run();
                }) { IsBackground = true, Name = "BURAK BIM overlay" };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
                Ready.Wait(5000);
            }
            var f = _form;
            if (f != null && f.IsHandleCreated) f.BeginInvoke(new Action(() => f.ShowBanner(text)));
        }
        catch (Exception ex) { Console.Error.WriteLine("[BURAK BIM] overlay: " + ex.Message); }
    }

    public static void Hide()
    {
        try { var f = _form; if (f != null && f.IsHandleCreated) f.BeginInvoke(new Action(() => f.HideBanner())); } catch { }
    }

    sealed class BannerForm : System.Windows.Forms.Form
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
        const int WM_HOTKEY = 0x0312; const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;
        readonly System.Windows.Forms.Label _label = new();
        readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

        public BannerForm()
        {
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true; StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            BackColor = System.Drawing.Color.FromArgb(200, 20, 60); Opacity = 0.88;
            Width = 640; Height = 30;
            _label.Dock = System.Windows.Forms.DockStyle.Fill; _label.ForeColor = System.Drawing.Color.White;
            _label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            _label.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
            Controls.Add(_label);
            _timer.Tick += (s, e) => { if (!Consent.Active) HideBanner(); };
        }

        // never take focus / clicks
        protected override bool ShowWithoutActivation => true;
        protected override System.Windows.Forms.CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x00000080 /*TOOLWINDOW*/ | 0x00000020 /*TRANSPARENT*/ | 0x00080000 /*LAYERED*/; return cp; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { RegisterHotKey(Handle, 0xB0B, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)'Q'); } catch { }
            try { SetWindowDisplayAffinity(Handle, 0x11 /* WDA_EXCLUDEFROMCAPTURE */); } catch { }
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == 0xB0B) { Consent.Revoke("user pressed Ctrl+Alt+Q"); return; }
            base.WndProc(ref m);
        }

        public void ShowBanner(string text)
        {
            _label.Text = text;
            var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            Left = wa.Left + (wa.Width - Width) / 2; Top = wa.Top + 2;
            Visible = true; TopMost = true;
            _timer.Start();
        }

        public void HideBanner() { _timer.Stop(); Visible = false; }

        protected override void Dispose(bool disposing)
        {
            try { UnregisterHotKey(Handle, 0xB0B); } catch { }
            base.Dispose(disposing);
        }
    }
#else
    public static void Show(string text) { }
    public static void Hide() { }
#endif
}
