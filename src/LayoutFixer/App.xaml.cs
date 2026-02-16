using System.Windows;
using ClipboardWpf= System.Windows.Clipboard;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LayoutFixer;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _notify;
    private HotkeyWindow? _hotkey;
    private KeyboardLayoutMapper? _mapper;
    private Settings _settings = SettingsService.Load();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mapper = new KeyboardLayoutMapper();

        // Create hidden settings window (we show it from tray menu)
        var win = new MainWindow(_settings);
        win.Hide();

        // Tray icon
        _notify = new Forms.NotifyIcon();
        _notify.Icon = System.Drawing.SystemIcons.Application;
        _notify.Visible = true;
        _notify.Text = "LayoutFixer";

        var menu = new Forms.ContextMenuStrip();

        var convertNow = new Forms.ToolStripMenuItem("Convert now (Ctrl+C → convert → Ctrl+V)");
        convertNow.Click += (_, _) => TryConvertSelection(pasteBack: true);
        menu.Items.Add(convertNow);

        var convertClipboard = new Forms.ToolStripMenuItem("Convert CLIPBOARD text (no selection)");
        convertClipboard.Click += (_, _) =>
        {
            if (_mapper == null) return;

            string txt;
            try { txt = System.Windows.Clipboard.GetText(); }
            catch { ShowBalloon("LayoutFixer", "Clipboard read failed."); return; }

            if (string.IsNullOrWhiteSpace(txt))
            {
                ShowBalloon("LayoutFixer", "Clipboard is empty (copy text first).");
                return;
            }

            var a = _settings.LangA ?? "he-IL";
            var b = _settings.LangB ?? "en-US";

            try
            {
                var result = _mapper.ConvertAuto(txt, a, b, out var dir);
                if (!result.Changed) { ShowBalloon("LayoutFixer", "Nothing to convert."); return; }

                System.Windows.Clipboard.SetText(result.Text);
                ShowBalloon("LayoutFixer", $"Converted clipboard ({dir}). Now paste (Ctrl+V).");
            }
            catch (Exception ex)
            {
                ShowBalloon("LayoutFixer", $"Convert failed: {ex.Message}");
            }
        };
        menu.Items.Add(convertClipboard);
         
         /* debug
         var debug = new Forms.ToolStripMenuItem("DEBUG: show clipboard + conversion");
            debug.Click += (_, _) =>
            {
                try
                {
                    // 1) Read clipboard
                    var clip = System.Windows.Clipboard.ContainsText()
                        ? System.Windows.Clipboard.GetText()
                        : "(no text in clipboard)";

                    // 2) Try convert
                    var a = _settings.LangA ?? "he-IL";
                    var b = _settings.LangB ?? "en-US";
                    string dir = "(n/a)";
                    string converted = "(mapper is null)";

                    if (_mapper != null && !string.IsNullOrWhiteSpace(clip) && clip != "(no text in clipboard)")
                    {
                        var r = _mapper.ConvertAuto(clip, a, b, out dir);
                        converted = $"Changed={r.Changed}\r\nText={r.Text}";
                    }

                    System.Windows.MessageBox.Show(
                        $"CLIP:\r\n{clip}\r\n\r\nDIR:\r\n{dir}\r\n\r\nRESULT:\r\n{converted}",
                        "LayoutFixer DEBUG",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.ToString(), "LayoutFixer DEBUG ERROR");
                }
            };
            menu.Items.Add(debug); */


        var openSettings = new Forms.ToolStripMenuItem("Settings…");
        openSettings.Click += (_, _) =>
        {
            win.ReloadInstalledLanguages();
            win.Show();
            win.Activate();
        };
        menu.Items.Add(openSettings);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ShutdownApp();
        menu.Items.Add(exit);

        _notify.ContextMenuStrip = menu;

        // Hotkey window + registration
        _hotkey = new HotkeyWindow();
        _hotkey.HotkeyPressed += (_, _) => TryConvertSelection(pasteBack: true);

        RegisterHotkeyOrBalloon();

        // React to settings saved
        win.SettingsSaved += (_, newSettings) =>
        {
            _settings = newSettings;
            SettingsService.Save(_settings);

            // Re-register hotkey
            try { _hotkey.UnregisterHotkey(); } catch { /* ignore */ }
            RegisterHotkeyOrBalloon();
        };

        ShowBalloon("LayoutFixer", $"Running. Select text and press {_settings.Hotkey}.");
    }

    private void RegisterHotkeyOrBalloon()
    {
        if (_hotkey is null) return;

        try
        {
            _hotkey.RegisterHotkey(_settings.Hotkey);
            UpdateTrayText();
        }
        catch
        {
            ShowBalloon("LayoutFixer", $"Could not register hotkey '{_settings.Hotkey}'. Try e.g. F10 or Ctrl+Alt+L.");
        }
    }

    private void UpdateTrayText()
    {
        if (_notify == null) return;

        var a = _settings.LangA ?? "he-IL";
        var b = _settings.LangB ?? "en-US";
        _notify.Text = $"LayoutFixer ({_settings.Hotkey}) {a} ↔ {b}";
    }

    private void ShowBalloon(string title, string text)
    {
        try
        {
            if (_notify == null) return;
            _notify.BalloonTipTitle = title;
            _notify.BalloonTipText = text;
            _notify.ShowBalloonTip(1500);
        }
        catch { /* ignore */ }
    }

    private void TryConvertSelection(bool pasteBack)
    {
        if (_mapper == null) return;

        // Load current settings each time (safe if user changed)
        var a = _settings.LangA ?? "he-IL";
        var b = _settings.LangB ?? "en-US";

        // 1) Backup clipboard (text only)
        var oldClipboard = ClipboardBackup.TryReadText();

        // 2) Copy selection
            // 2) Copy selection (reliable)
            try { System.Windows.Clipboard.Clear(); } catch { }
            Thread.Sleep(60);
            Forms.SendKeys.SendWait("^c");
            Thread.Sleep(180);

            string txt = "";
            try { txt = System.Windows.Clipboard.GetText(); } catch { txt = ""; }

        // 3) Convert
        string outText;
        string dir;

        try
        {
            var result = _mapper.ConvertAuto(txt, a, b, out dir);
            if (!result.Changed)
            {
                ClipboardBackup.TryRestoreText(oldClipboard);
                ShowBalloon("LayoutFixer", "Nothing to convert (text doesn't match either layout).");
                return;
            }
            outText = result.Text;
        }
        catch (Exception ex)
        {
            ClipboardBackup.TryRestoreText(oldClipboard);
            ShowBalloon("LayoutFixer", $"Convert failed: {ex.Message}");
            return;
        }

        // 4) Paste back
        try
        {
            ClipboardWpf.SetText(outText);

            Thread.Sleep(80);

            if (pasteBack)
            {
                Forms.SendKeys.SendWait("^v");
            }

            // Important: wait before restoring clipboard, otherwise target app may paste the old clipboard
            Thread.Sleep(250);
            ClipboardBackup.TryRestoreText(oldClipboard);
        }
        catch
        {
            ClipboardBackup.TryRestoreText(oldClipboard);
            ShowBalloon("LayoutFixer", "Failed to set clipboard.");
            return;
        }

        Thread.Sleep(30);
        if (pasteBack)
        {
            SendInputHelper.SendCtrlCombo('V');
        }

        Thread.Sleep(40);
        ClipboardBackup.TryRestoreText(oldClipboard);

        UpdateTrayText();
        ShowBalloon("LayoutFixer", $"Converted ({dir}).");
    }

    private void ShutdownApp()
    {
        try { _hotkey?.Dispose(); } catch { }
        try { _notify!.Visible = false; _notify.Dispose(); } catch { }
        Shutdown();
    }
}
