using System.Windows.Interop;

namespace LayoutFixer;

public sealed class HotkeyWindow : IDisposable
{
    private readonly HwndSource _source;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 1;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public HotkeyWindow()
    {
        var parameters = new HwndSourceParameters("LayoutFixerHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = unchecked((int)0x80000000) // WS_POPUP
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public void RegisterHotkey(string hotkey)
    {
        UnregisterHotkey();

        var parsed = HotkeyParser.Parse(hotkey);
        _registered = NativeMethods.RegisterHotKey(_source.Handle, HOTKEY_ID, parsed.Modifiers, parsed.Vk);

        if (!_registered)
        {
            throw new InvalidOperationException("RegisterHotKey failed.");
        }
    }

    public void UnregisterHotkey()
    {
        if (!_registered) return;
        NativeMethods.UnregisterHotKey(_source.Handle, HOTKEY_ID);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        try { UnregisterHotkey(); } catch { }
        try { _source.RemoveHook(WndProc); } catch { }
        try { _source.Dispose(); } catch { }
    }
}

internal static class HotkeyParser
{
    // Modifier flags for RegisterHotKey
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    public static (uint Modifiers, uint Vk) Parse(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return (0, (uint)System.Windows.Forms.Keys.F9);

        var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        uint mods = 0;

        string keyPart = parts[^1];

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var p = parts[i].ToLowerInvariant();
            if (p == "alt") mods |= MOD_ALT;
            else if (p == "ctrl" || p == "control") mods |= MOD_CONTROL;
            else if (p == "shift") mods |= MOD_SHIFT;
            else if (p == "win" || p == "windows") mods |= MOD_WIN;
            else throw new InvalidOperationException($"Unknown modifier: {parts[i]}");
        }

        if (!Enum.TryParse<System.Windows.Forms.Keys>(keyPart, ignoreCase: true, out var key))
            throw new InvalidOperationException($"Unknown key: {keyPart}");

        return (mods, (uint)key);
    }
}
