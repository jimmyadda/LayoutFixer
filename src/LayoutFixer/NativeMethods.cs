using System.Runtime.InteropServices;
using System.Text;

namespace LayoutFixer;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    [DllImport("user32.dll")]
    internal static extern int ToUnicodeEx(
        uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
        int cchBuff, uint wFlags, IntPtr dwhkl);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
