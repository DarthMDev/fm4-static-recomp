// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
namespace Fm4StaticRecomp.Launcher.Core.Settings;

// The key names the game's keyboard driver understands (the SDK's kKeyNames, src\ui\keybinds.cpp),
// without the mouse buttons the panel does not offer.
public static class KeyNames
{
    private static readonly HashSet<string> Keys = new(
        new[]
        {
            "Backtick", "Minus", "Plus", "Comma", "Period", "Semicolon", "Slash", "Backslash", "LBracket", "RBracket", "Quote",
            "Escape", "Return", "Space", "Tab", "Backspace", "Delete", "Insert", "Home", "End", "PageUp", "PageDown",
            "Left", "Right", "Up", "Down", "Shift", "Control", "Alt",
            "NumpadEnter", "NumpadPlus", "NumpadMinus", "NumpadStar", "NumpadSlash", "PrintScreen", "Pause", "CapsLock", "NumLock", "ScrollLock",
        }
        .Concat(Enumerable.Range(1, 24).Select(n => $"F{n}"))
        .Concat(Enumerable.Range('A', 26).Select(c => ((char)c).ToString()))
        .Concat(Enumerable.Range(0, 10).Select(n => n.ToString()))
        .Concat(Enumerable.Range(0, 10).Select(n => $"Numpad{n}")),
        StringComparer.Ordinal);

    private static readonly string[] Modifiers = ["Shift", "Ctrl", "Control", "Alt"];

    // The game binds them too; the panel keeps them from a hand-edited file but does not capture them.
    private static readonly string[] Mouse = ["LMB", "RMB", "MMB"];

    private static readonly Dictionary<string, string> Avalonia = new(StringComparer.Ordinal)
    {
        ["Back"] = "Backspace", ["Prior"] = "PageUp", ["PageUp"] = "PageUp", ["Next"] = "PageDown", ["PageDown"] = "PageDown",
        ["LeftShift"] = "Shift", ["RightShift"] = "Shift", ["LeftCtrl"] = "Control", ["RightCtrl"] = "Control",
        ["LeftAlt"] = "Alt", ["RightAlt"] = "Alt",
        ["OemTilde"] = "Backtick", ["OemMinus"] = "Minus", ["OemPlus"] = "Plus", ["OemComma"] = "Comma", ["OemPeriod"] = "Period",
        ["OemSemicolon"] = "Semicolon", ["OemQuestion"] = "Slash", ["OemPipe"] = "Backslash", ["OemOpenBrackets"] = "LBracket",
        ["OemCloseBrackets"] = "RBracket", ["OemQuotes"] = "Quote",
        // Key has these as aliases of the Oem names above; ToString() may return either.
        ["Oem1"] = "Semicolon", ["Oem2"] = "Slash", ["Oem3"] = "Backtick", ["Oem4"] = "LBracket", ["Oem5"] = "Backslash",
        ["Oem6"] = "RBracket", ["Oem7"] = "Quote",
        ["Add"] = "NumpadPlus", ["Subtract"] = "NumpadMinus", ["Multiply"] = "NumpadStar", ["Divide"] = "NumpadSlash",
        ["Snapshot"] = "PrintScreen", ["Capital"] = "CapsLock", ["CapsLock"] = "CapsLock", ["Scroll"] = "ScrollLock",
    };

    public static bool IsKey(string name) => Keys.Contains(name);

    public static bool IsModifier(string name) => name is "Shift" or "Control" or "Alt";

    // A key or mouse button, with optional modifiers joined by '+'.
    public static bool IsBind(string token)
    {
        var rest = token;
        for (var plus = rest.IndexOf('+'); plus > 0; plus = rest.IndexOf('+'))
        {
            if (!Modifiers.Contains(rest[..plus])) return false;
            rest = rest[(plus + 1)..];
        }
        return IsKey(rest) || Mouse.Contains(rest);
    }

    public static List<string> Split(string value) =>
        value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

    // The name for Avalonia.Input.Key.ToString(), or null for a key the game cannot bind.
    public static string? FromAvalonia(string key)
    {
        if (Avalonia.TryGetValue(key, out var mapped)) return mapped;
        if (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1])) return key[1].ToString();
        if (key.StartsWith("NumPad", StringComparison.Ordinal)) return IsKey("Numpad" + key[6..]) ? "Numpad" + key[6..] : null;
        return IsKey(key) ? key : null;
    }
}
