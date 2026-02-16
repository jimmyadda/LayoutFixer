using System.Runtime.InteropServices;

namespace LayoutFixer;

public static class SendInputHelper
{
    // Minimal SendInput helper to send Ctrl+C / Ctrl+V reliably.

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const ushort VK_CONTROL = 0x11;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void SendCtrlCombo(char key)
    {
        // key expected: 'C' or 'V'
        ushort vk = (ushort)char.ToUpperInvariant(key);

        var inputs = new List<INPUT>();

        // Ctrl down
        inputs.Add(KeyDown(VK_CONTROL));
        // key down
        inputs.Add(KeyDown(vk));
        // key up
        inputs.Add(KeyUp(vk));
        // Ctrl up
        inputs.Add(KeyUp(VK_CONTROL));

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyDown(ushort vk) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 }
        }
    };

    private static INPUT KeyUp(ushort vk) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP }
        }
    };
}
