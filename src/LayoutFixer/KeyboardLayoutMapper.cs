using System.Text;
using Forms = System.Windows.Forms;

namespace LayoutFixer;

public sealed class KeyboardLayoutMapper
{
    private readonly object _lock = new();

    public ConvertResult ConvertAuto(string text, string langA, string langB, out string direction)
    {
        direction = $"{langA} -> {langB}";

        if (string.IsNullOrEmpty(text))
            return new ConvertResult(text, Changed: false);

        StringBuilder lowerCaseString = new StringBuilder();

        foreach (char c in text)
        {
            if (char.IsUpper(c))
            {
                // If it's uppercase, convert to lowercase and append
                lowerCaseString.Append(char.ToLower(c));
            }
            else
            {
                // Otherwise, append the character as is
                lowerCaseString.Append(c);
            }
        }            

        text = lowerCaseString.ToString();
        // Normalize incoming text (fixes Hebrew presentation forms etc.)
        text = text.Normalize(NormalizationForm.FormKC);


        

        LayoutMaps mapA;
        LayoutMaps mapB;

        lock (_lock)
        {
            mapA = BuildLayoutMaps(langA);
            mapB = BuildLayoutMaps(langB);
        }

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

    private static ConvertResult Convert(
        string text,
        Dictionary<string, KeyStroke> fromCharToKey,
        Dictionary<KeyStroke, string> toKeyToChar)
    {
        var sb = new StringBuilder(text.Length);
        bool changed = false;

        foreach (var ch in text)
        {
            // Ignore control chars
            if (char.IsControl(ch))
            {
                sb.Append(ch);
                continue;
            }

            // Try to resolve to a keystroke in the SOURCE layout
            if (TryGetStroke(fromCharToKey, ch, out var ks))
            {
                // Convert that keystroke to a character in the DEST layout
                if (toKeyToChar.TryGetValue(ks, out var mapped) && !string.IsNullOrEmpty(mapped))
                {
                    // Normalize output char (important for Hebrew/Arabic presentation forms)
                    mapped = mapped.Normalize(NormalizationForm.FormKC);

                    // Only mark changed if it really changed
                    if (mapped.Length != 1 || mapped[0] != ch) changed = true;

                    sb.Append(mapped);
                    continue;
                }
            }

            // If not mapped: keep original (special chars like @#! stay)
            sb.Append(ch);
        }

        var final = sb.ToString().Normalize(NormalizationForm.FormKC);
        return new ConvertResult(final, Changed: changed);
    }

    private static int Score(string text, Dictionary<string, KeyStroke> charToKey)
    {
        int score = 0;

        foreach (var ch in text)
        {
            if (char.IsControl(ch)) continue;

            // Exact hit
            if (charToKey.ContainsKey(ch.ToString()))
            {
                score++;
                continue;
            }

            // CapsLock support:
            // If we see 'A'..'Z', treat it like 'a'..'z' for scoring too.
            if (ch >= 'A' && ch <= 'Z')
            {
                var lower = ((char)(ch + 32)).ToString(); // fast ToLowerInvariant for ASCII
                if (charToKey.ContainsKey(lower)) score++;
            }
        }

        return score;
    }

    private static bool TryGetStroke(Dictionary<string, KeyStroke> map, char ch, out KeyStroke ks)
    {
        var s = ch.ToString();

        // Exact match first
        if (map.TryGetValue(s, out ks))
            return true;

        // CapsLock support:
        // If input is Latin uppercase A-Z, prefer mapping as lowercase (no Shift).
        // This makes "AKUO" behave like "akuo" and converts to Hebrew correctly.
        if (ch >= 'A' && ch <= 'Z')
        {
            var lower = ((char)(ch + 32)).ToString();
            
            if (map.TryGetValue(lower, out ks))
                return true;
        }

        ks = default;
        return false;
    }

    private LayoutMaps BuildLayoutMaps(string cultureName)
    {
        var hkl = LayoutLookup.FindHKLByCulture(cultureName);

        var keyToChar = new Dictionary<KeyStroke, string>();
        var charToKey = new Dictionary<string, KeyStroke>();

        // Key state buffers
        var stateNoShift = new byte[256];
        var stateShift = new byte[256];
        stateShift[0x10] = 0x80; // VK_SHIFT down

        var vks = new List<uint>();

        // A-Z
        for (uint vk = 0x41; vk <= 0x5A; vk++) vks.Add(vk);

        // 0-9
        for (uint vk = 0x30; vk <= 0x39; vk++) vks.Add(vk);

        // Space
        vks.Add(0x20);

        // Common OEM punctuation keys
        vks.AddRange(new uint[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE });

        // IMPORTANT: add unshifted first, then shifted (so lower-case wins first-come)
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

            // first-come wins for char->key (unshifted added first)
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
