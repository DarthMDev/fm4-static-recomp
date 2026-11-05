// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using Fm4StaticRecomp.Launcher.Core.Settings;

namespace Fm4StaticRecomp.Launcher.Tests.Settings;

public class KeyNamesTests
{
    [Theory]
    [InlineData("W")]
    [InlineData("Numpad7")]
    [InlineData("Escape")]
    [InlineData("F24")]
    [InlineData("Backtick")]
    public void Knows_the_games_key_names(string name) => Assert.True(KeyNames.IsKey(name));

    [Theory]
    [InlineData("w")]
    [InlineData("LMB")]
    [InlineData("")]
    [InlineData("Shift+W")]
    public void Refuses_what_the_game_cannot_match_as_one_key(string name) => Assert.False(KeyNames.IsKey(name));

    [Theory]
    [InlineData("Shift+W", true)]
    [InlineData("Ctrl+Alt+F5", true)]
    [InlineData("Space", true)]
    [InlineData("Foo+W", false)]
    [InlineData("Shift+", false)]
    [InlineData("Shift+w", false)]
    [InlineData("LMB", true)]
    [InlineData("Ctrl+RMB", true)]
    public void Binds_are_keys_with_optional_modifiers(string token, bool expected) =>
        Assert.Equal(expected, KeyNames.IsBind(token));

    [Fact]
    public void Splits_a_list_and_drops_blanks() =>
        Assert.Equal(["W", "Up"], KeyNames.Split(" W , Up ,,"));

    [Theory]
    [InlineData("D3", "3")]
    [InlineData("NumPad4", "Numpad4")]
    [InlineData("LeftShift", "Shift")]
    [InlineData("RightCtrl", "Control")]
    [InlineData("OemTilde", "Backtick")]
    [InlineData("Back", "Backspace")]
    [InlineData("A", "A")]
    [InlineData("Oem1", "Semicolon")]
    [InlineData("Oem2", "Slash")]
    [InlineData("Oem3", "Backtick")]
    [InlineData("Oem4", "LBracket")]
    [InlineData("Oem5", "Backslash")]
    [InlineData("Oem6", "RBracket")]
    [InlineData("Oem7", "Quote")]
    [InlineData("LWin", null)]
    [InlineData("MediaPlayPause", null)]
    public void Maps_avalonia_keys(string avalonia, string? expected) =>
        Assert.Equal(expected, KeyNames.FromAvalonia(avalonia));

    [Fact]
    public void Everything_it_maps_to_is_a_known_key()
    {
        foreach (var name in new[]
                 {
                     "A", "Z", "D0", "D9", "NumPad0", "NumPad9", "F1", "F12", "F24", "Back", "Prior", "PageUp", "Next", "PageDown",
                     "LeftShift", "RightShift", "LeftCtrl", "RightCtrl", "LeftAlt", "RightAlt", "OemTilde", "OemMinus", "OemPlus",
                     "OemComma", "OemPeriod", "OemSemicolon", "OemQuestion", "OemPipe", "OemOpenBrackets", "OemCloseBrackets",
                     "OemQuotes", "Add", "Subtract", "Multiply", "Divide", "Snapshot", "Capital", "CapsLock", "Scroll", "Escape",
                     "Return", "Space", "Tab", "Delete", "Insert", "Home", "End", "Left", "Right", "Up", "Down", "Pause", "NumLock",
                 })
            Assert.True(KeyNames.IsKey(KeyNames.FromAvalonia(name) ?? "?"), name);
    }

    [Theory]
    [InlineData("Shift", true)]
    [InlineData("Control", true)]
    [InlineData("Alt", true)]
    [InlineData("W", false)]
    public void Knows_the_modifiers(string name, bool expected) => Assert.Equal(expected, KeyNames.IsModifier(name));
}
