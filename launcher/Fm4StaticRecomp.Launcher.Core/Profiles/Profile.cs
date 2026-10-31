// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
namespace Fm4StaticRecomp.Launcher.Core.Profiles;

// Game is the install's game the profile was made in ("vanilla" or "pfp4"); a Project Forza Plus 4
// profile also records the career mode and Harder AI it last ran under, null when unknown. Xuid is the
// profile a save imported from Xenia was made with (the game ties a save to it), null for the runtime's own.
public sealed record Profile(string Id, string Folder, string Name, string Gamertag, string? LastBackedUpVersion,
    string Game = "vanilla", string? Pfp4Mode = null, bool? Pfp4HarderAi = null, string? ImportWarning = null,
    string? Xuid = null)
{
    public string User => Path.Combine(Folder, "user");
}
