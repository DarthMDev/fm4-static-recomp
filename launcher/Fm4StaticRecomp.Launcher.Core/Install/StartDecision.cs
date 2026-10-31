// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using Fm4StaticRecomp.Launcher.Core.Setup;

namespace Fm4StaticRecomp.Launcher.Core.Install;

public enum StartKind { Setup, BuildVersion, Ready }

public static class StartDecision
{
    // Ready whenever the payload's version is built, active or not: after "Go back" nothing
    // switches by itself.
    // A missing saves folder (its drive not connected) lists no profiles; setup would make a new one there.
    public static StartKind Decide(SetupState state, int profileCount, string payloadVersion, bool savesFolderMissing = false)
    {
        if (!state.Disc1Installed || state.BuiltVersions.Count == 0 || (profileCount == 0 && !savesFolderMissing)) return StartKind.Setup;
        if (state.Game == "pfp4" && !state.Pfp4Applied) return StartKind.Setup;
        return state.BuiltVersions.Contains(payloadVersion) ? StartKind.Ready : StartKind.BuildVersion;
    }
}
