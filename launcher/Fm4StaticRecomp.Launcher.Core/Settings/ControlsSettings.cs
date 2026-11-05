// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.Globalization;

namespace Fm4StaticRecomp.Launcher.Core.Settings;

// controls.toml as the panel edits it. Edits are held until Save, which writes only the rows
// that were edited: a value equal to the game's default removes its line (so a later release's
// new default reaches players who never touched the row), anything else sets it, and every
// other line stays as it was.
public sealed class ControlsSettings(string tomlPath)
{
    public const double RampLow = 0.02, RampHigh = 0.60;

    // A primary key and an optional secondary one. A hand-edited file may list more: they are shown and kept, not added.
    public const int MaxKeys = 2;

    private sealed record Edit(string Value, string Default);

    private readonly Dictionary<string, Edit> _edited = [];
    private TomlFile _file = TomlFile.Load(tomlPath);

    public bool FileIsMalformed => _file.IsMalformed;

    public bool Changed => _edited.Count > 0;

    public IReadOnlyList<string> Keys(ControlSet set, string id) =>
        _edited.TryGetValue(ControlsCatalog.FileKey(set, id), out var edit) ? KeyNames.Split(edit.Value) : SavedKeys(set, id);

    public void SetKey(ControlSet set, string id, int index, string key)
    {
        if (!KeyNames.IsKey(key)) throw new ArgumentException($"{key} is not a key the game can bind.", nameof(key));
        var keys = Keys(set, id).ToList();
        var other = keys.IndexOf(key);
        if (other >= 0 && other != index) keys[other] = keys[index];
        keys[index] = key;
        Store(set, id, keys);
    }

    public void AddKey(ControlSet set, string id, string key)
    {
        if (!KeyNames.IsKey(key)) throw new ArgumentException($"{key} is not a key the game can bind.", nameof(key));
        var keys = Keys(set, id).ToList();
        if (keys.Contains(key)) return;
        if (keys.Count >= MaxKeys) throw new InvalidOperationException($"A row has at most {MaxKeys} keys.");
        keys.Add(key);
        Store(set, id, keys);
    }

    public void RemoveKey(ControlSet set, string id, int index)
    {
        var keys = Keys(set, id).ToList();
        if (keys.Count <= 1) throw new InvalidOperationException("A row keeps at least one key.");
        keys.RemoveAt(index);
        Store(set, id, keys);
    }

    public void Reset(ControlSet set, string id) => Store(set, id, KeyNames.Split(ControlsCatalog.DefaultKeys(set, id)));

    public void ResetAll(ControlSet set)
    {
        foreach (var row in ControlsCatalog.Rows(set)) Reset(set, row.Id);
    }

    public bool IsDefault(ControlSet set, string id) =>
        string.Join(",", Keys(set, id)) == ControlsCatalog.DefaultKeys(set, id);

    // Labels of the other rows of the set that one key press can fire together with this one. The
    // game does not match modifiers exactly (mnk_exact_modifiers is off), so a bind fires whenever
    // its keys are held: Shift+W also fires W and a bare Shift.
    public IReadOnlyList<string> ConflictsWith(ControlSet set, string id)
    {
        var mine = Keys(set, id).Select(Pressed).ToList();
        return ControlsCatalog.Rows(set)
            .Where(r => r.Id != id && Keys(set, r.Id).Select(Pressed).Any(other => mine.Any(m => m.IsSubsetOf(other) || other.IsSubsetOf(m))))
            .Select(r => r.Label).ToList();
    }

    public double Ramp(string key) =>
        _edited.TryGetValue(key, out var edit) ? ParseRamp(edit.Value)!.Value : SavedRamp(key);

    public void SetRamp(string key, double seconds)
    {
        var row = RampRowOf(key);
        StoreRamp(row, Math.Round(Math.Clamp(seconds, RampLow, RampHigh), 2));
    }

    public void ResetRamp(string key)
    {
        var row = RampRowOf(key);
        StoreRamp(row, row.Default);
    }

    public bool RampIsDefault(string key) => FormatRamp(Ramp(key)) == FormatRamp(RampRowOf(key).Default);

    // The game may have written the file since it was read: only this session's edits are applied on top.
    public void Save()
    {
        if (!Changed) return;
        var file = TomlFile.Load(tomlPath);
        foreach (var (key, edit) in _edited)
        {
            if (edit.Value == edit.Default) file.Remove(key);
            else file.Set(key, edit.Value, quoted: !key.StartsWith("ramp_", StringComparison.Ordinal));
        }
        file.Save(tomlPath);
        _file = file;
        _edited.Clear();
    }

    // 0.2.0 is the first release with the defaults the panel shows (0.1.3 read the file with other ramps);
    // an unreadable version is assumed to.
    public static bool Supports(string version) => !Version.TryParse(version, out var parsed) || parsed >= new Version(0, 2, 0);

    private List<string> SavedKeys(ControlSet set, string id)
    {
        var stored = _file.Get(ControlsCatalog.FileKey(set, id));
        var keys = stored is null ? [] : KeyNames.Split(stored).Where(KeyNames.IsBind).ToList();
        return keys.Count > 0 ? keys : KeyNames.Split(ControlsCatalog.DefaultKeys(set, id));
    }

    private void Store(ControlSet set, string id, List<string> keys)
    {
        var file = ControlsCatalog.FileKey(set, id);
        var value = string.Join(",", keys);
        if (value == string.Join(",", SavedKeys(set, id))) _edited.Remove(file);
        else _edited[file] = new Edit(value, ControlsCatalog.DefaultKeys(set, id));
    }

    private void StoreRamp(RampRow row, double seconds)
    {
        var value = FormatRamp(seconds);
        if (value == FormatRamp(SavedRamp(row.Key))) _edited.Remove(row.Key);
        else _edited[row.Key] = new Edit(value, FormatRamp(row.Default));
    }

    private double SavedRamp(string key) =>
        _file.Get(key) is { } stored && ParseRamp(stored) is { } seconds ? seconds : RampRowOf(key).Default;

    // The keys held down to fire a bind, as the game names them once: Ctrl is Control, NumpadEnter is Return.
    private static HashSet<string> Pressed(string bind) =>
        bind.Split('+').Select(part => part switch { "Ctrl" => "Control", "NumpadEnter" => "Return", _ => part }).ToHashSet();

    private static RampRow RampRowOf(string key) =>
        ControlsCatalog.Ramps.FirstOrDefault(r => r.Key == key) ?? throw new ArgumentException($"{key} is not a ramp.", nameof(key));

    // The game accepts any number of seconds >= 0 (LoadRate in src\input\controls.cpp).
    private static double? ParseRamp(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 ? seconds : null;

    private static string FormatRamp(double seconds) => seconds.ToString("0.##", CultureInfo.InvariantCulture);
}
