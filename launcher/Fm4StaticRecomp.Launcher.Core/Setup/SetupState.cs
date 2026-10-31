// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fm4StaticRecomp.Launcher.Core.Setup;

// Completed setup steps. Every launcher reads the file of any older payload (spec 13), so
// properties are only ever added; unknown ones are ignored.
public sealed class SetupState
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private string _path = "";

    public int SchemaVersion { get; set; } = 1;
    public string? Disc1Release { get; set; }
    public int Disc1ProblemsAccepted { get; set; }
    public string? Disc2Source { get; set; }
    public bool Disc2Later { get; set; }
    public bool Disc2Installed { get; set; }
    public HashSet<string> BuiltVersions { get; set; } = [];
    // "pfp4" for a Project Forza Plus 4 install, fixed at setup; null is vanilla.
    public string? Game { get; set; }
    public string? Pfp4Mode { get; set; }
    public bool Pfp4HarderAi { get; set; }
    public bool Pfp4Applied { get; set; }

    [JsonIgnore]
    public bool Disc1Installed => Disc1Release is not null;

    public static SetupState Load(string path)
    {
        var state = File.Exists(path)
            ? JsonSerializer.Deserialize<SetupState>(File.ReadAllBytes(path), Options) ?? new SetupState()
            : new SetupState();
        state._path = path;
        return state;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Options));
        File.Move(temporary, _path, overwrite: true);
    }
}
