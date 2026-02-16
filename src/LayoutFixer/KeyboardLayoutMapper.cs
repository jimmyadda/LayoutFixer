using System.Text;
using Forms = System.Windows.Forms;

namespace LayoutFixer;

public sealed class KeyboardLayoutMapper
{
    private readonly object _lock = new();

    public ConvertResult ConvertAuto(string text, string langA, string langB, out string direction)
    {
        direction = $"{langA} -> {langB}";

        if (string.IsNullOrEmpty(text)) return new ConvertResult(text, Changed: false);

        // Normalize incoming text (kills weird presentation forms if already there)
        text = text.Normalize(NormalizationForm.FormKC);

        var mapA = BuildLayoutMaps(langA);
        var mapB = BuildLayoutMaps(langB);

        // Score which layout the text resembles more
        int scoreA = Score(text, mapA.CharToKey);
        int scoreB = Score(text, mapB.CharToKey);

        if (scoreA == 0 && scoreB == 0)
        {
            direction = "none";
            return new ConvertResult(text, Changed: false);
        }

        if (scoreA >= scoreB)
        {
            direction = $"{langA} -> {langB}";
            return Convert(text, mapA.CharToKey, mapB.KeyToChar);
        }
        else
        {
            direction = $"{langB} -> {langA}";
            return Convert(text, mapB.CharToKey, mapA.KeyToChar);
        }
    }

    private ConvertResult Convert(string text,
        Dictionary<string, KeyStroke> fromCharToKey,
        Dictionary<KeyStroke, string> toKeyToChar)
    {
        var sb = new StringBuilder(text.Length);
        bool changed = false;

        foreach (var rune in text.EnumerateRunes())
        {
            var s = rune.ToString();

            if (fromCharToKey.TryGetValue(s, out var ks))
            {
                if (toKeyToChar.TryGetValue(ks, out var outChar) && !string.IsNullOrEmpty(outChar))
                {
                    sb.Append(outChar);
                    if (outChar != s) changed = true;
                }
                else
                {
                    sb.Append(s);
                }
            }
            else
            {
                sb.Append(s);
            }
        }

        // Final normalization (prevents Hebrew/Arabic odd forms)
        var final = sb.ToString().Normalize(NormalizationForm.FormKC);
        return new ConvertResult(final, changed);
    }

    private int Score(string text, Dictionary<string, KeyStroke> charToKey)
    {
        int score = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (charToKey.ContainsKey(rune.ToString())) score++;
        }
        return score;
    }

    private LayoutMaps BuildLayoutMaps(string cultureName)
    {
        var hkl = LayoutLookup.FindHKLByCulture(cultureName);

        // Build mapping for a useful set of VKs (letters, digits, punctuation, space)
        // We map both unshifted and shifted.
        var keyToChar = new Dictionary<KeyStroke, string>();
        var charToKey = new Dictionary<string, KeyStroke>();

        // Key state buffers
        var stateNoShift = new byte[256];
        var stateShift = new byte[256];
        stateShift[0x10] = 0x80; // VK_SHIFT

        // VKs to include:
        var vks = new List<uint>();

        // A-Z
        for (uint vk = 0x41; vk <= 0x5A; vk++) vks.Add(vk);

        // 0-9
        for (uint vk = 0x30; vk <= 0x39; vk++) vks.Add(vk);

        // Space
        vks.Add(0x20);

        // Common OEM punctuation keys
        // US: ;=,-./` and [\]'
        vks.AddRange(new uint[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE });

        // Build (vk,shift) -> char and char -> (vk,shift)
        foreach (var vk in vks)
        {
            AddStroke(vk, shift: false, stateNoShift);
            AddStroke(vk, shift: true, stateShift);
        }

        return new LayoutMaps(keyToChar, charToKey);

        void AddStroke(uint vk, bool shift, byte[] state)
        {
            var scan = NativeMethods.MapVirtualKeyEx(vk, 0, hkl);
            var sb = new StringBuilder(16);

            int rc = NativeMethods.ToUnicodeEx(vk, scan, state, sb, sb.Capacity, 0, hkl);
            if (rc <= 0) return;

            var ch = sb.ToString().Substring(0, 1).Normalize(NormalizationForm.FormKC);
            var ks = new KeyStroke(vk, shift);

            if (!keyToChar.ContainsKey(ks))
                keyToChar[ks] = ch;

            // first-come wins for char->key
            if (!charToKey.ContainsKey(ch))
                charToKey[ch] = ks;
        }
    }

    private sealed record LayoutMaps(
        Dictionary<KeyStroke, string> KeyToChar,
        Dictionary<string, KeyStroke> CharToKey);

    internal readonly record struct KeyStroke(uint Vk, bool Shift);

    public readonly record struct ConvertResult(string Text, bool Changed);
}

internal static class LayoutLookup
{
    public static IntPtr FindHKLByCulture(string cultureName)
    {
        foreach (Forms.InputLanguage l in Forms.InputLanguage.InstalledInputLanguages)
        {
            if (string.Equals(l.Culture?.Name, cultureName, StringComparison.OrdinalIgnoreCase))
                return l.Handle;
        }
        throw new InvalidOperationException($"Culture '{cultureName}' not found in installed input languages.");
    }
}
