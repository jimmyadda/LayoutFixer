using System.Windows;
using ClipboardWpf= System.Windows.Clipboard;

namespace LayoutFixer;

public static class ClipboardBackup
{
    public static string? TryReadText()
    {
        try
        {
            return ClipboardWpf.ContainsText() ? ClipboardWpf.GetText() : null;
        }
        catch { return null; }
    }

    public static void TryRestoreText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { ClipboardWpf.SetText(text); } catch { }
    }
}
