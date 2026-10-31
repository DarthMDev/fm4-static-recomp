// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
namespace Fm4StaticRecomp.Launcher.Core.Updates;

public sealed record ReleaseInfo(Version Version, string Tag, bool Prerelease, string Notes, IReadOnlyDictionary<string, Uri> Assets);
