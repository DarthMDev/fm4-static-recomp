// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fm4StaticRecomp.Launcher.Core.Pfp4;

// config/pfp4-1.3.json (tools/make-pfp4-manifest.py): every file a Project Forza Plus 4 install
// takes from the mod. Sources are relative to the mod's root, targets to the game folder, the
// content folder `00000002`, a package or a save folder, as each list says.
public sealed record Pfp4Entry(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record Pfp4HarderAi(
    [property: JsonPropertyName("game_db")] Pfp4Entry GameDb,
    [property: JsonPropertyName("tunables")] Pfp4Entry Tunables);

public sealed record Pfp4SkipIntro(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("files")] IReadOnlyList<Pfp4Entry> Files);

public sealed record Pfp4Mode(
    [property: JsonPropertyName("game_db")] Pfp4Entry GameDb,
    [property: JsonPropertyName("harder_ai")] Pfp4HarderAi HarderAi,
    [property: JsonPropertyName("dlc_dbs")] IReadOnlyList<Pfp4Entry> DlcDbs,
    [property: JsonPropertyName("skip_intro")] IReadOnlyList<Pfp4SkipIntro> SkipIntro);

public sealed record Pfp4Package(
    [property: JsonPropertyName("folder")] string Folder,
    [property: JsonPropertyName("offer")] int Offer,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("license_mask")] uint? LicenseMask,
    [property: JsonPropertyName("files")] IReadOnlyList<Pfp4Entry> Files);

public sealed record Pfp4Replace(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To);

public sealed record Pfp4Patch(
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("replace")] IReadOnlyList<Pfp4Replace> Replace,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record Pfp4Manifest(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("mod_version")] string ModVersion,
    [property: JsonPropertyName("game_files")] IReadOnlyList<Pfp4Entry> GameFiles,
    [property: JsonPropertyName("modes")] IReadOnlyDictionary<string, Pfp4Mode> Modes,
    [property: JsonPropertyName("packages")] IReadOnlyList<Pfp4Package> Packages,
    [property: JsonPropertyName("patches")] IReadOnlyList<Pfp4Patch> Patches)
{
    public const int SupportedSchema = 1;
    public const string Tunables = @"Media\GameTunableSettings\GameTunableSettings.ini";

    public static Pfp4Manifest Load(string path)
    {
        Pfp4Manifest? manifest;
        try { manifest = JsonSerializer.Deserialize<Pfp4Manifest>(File.ReadAllBytes(path)); }
        catch (JsonException ex) { throw new InvalidDataException($"{path} is not a Project Forza Plus 4 manifest: {ex.Message}", ex); }
        if (manifest is null) throw new InvalidDataException($"{path} is empty.");
        if (manifest.SchemaVersion != SupportedSchema)
            throw new InvalidDataException($"{path} has schema {manifest.SchemaVersion}; this launcher reads schema {SupportedSchema}.");
        return manifest;
    }

    public Pfp4Entry MainTunables => GameFiles.Single(f => string.Equals(f.Target, Tunables, StringComparison.OrdinalIgnoreCase));
}
