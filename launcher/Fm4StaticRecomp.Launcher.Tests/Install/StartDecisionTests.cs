// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using Fm4StaticRecomp.Launcher.Core.Install;
using Fm4StaticRecomp.Launcher.Core.Setup;

namespace Fm4StaticRecomp.Launcher.Tests.Install;

public class StartDecisionTests
{
    [Fact]
    public void A_new_install_starts_setup() =>
        Assert.Equal(StartKind.Setup, StartDecision.Decide(new SetupState(), 0, "0.1.0"));

    [Fact]
    public void An_unfinished_setup_resumes_setup() =>
        Assert.Equal(StartKind.Setup, StartDecision.Decide(new SetupState { Disc1Release = "1.0 USA" }, 0, "0.1.0"));

    [Fact]
    public void A_new_launcher_version_builds_side_by_side() =>
        Assert.Equal(StartKind.BuildVersion, StartDecision.Decide(
            new SetupState { Disc1Release = "1.0 USA", BuiltVersions = ["0.1.0"] }, 1, "0.2.0"));

    [Fact]
    public void A_missing_saves_folder_keeps_a_built_install_out_of_setup() =>
        Assert.Equal(StartKind.Ready, StartDecision.Decide(
            new SetupState { Disc1Release = "1.0 USA", BuiltVersions = ["0.2.0"] }, 0, "0.2.0", savesFolderMissing: true));

    [Fact]
    public void A_built_version_is_ready() =>
        Assert.Equal(StartKind.Ready, StartDecision.Decide(
            new SetupState { Disc1Release = "1.0 USA", BuiltVersions = ["0.1.0", "0.2.0"] }, 1, "0.2.0"));

    [Fact]
    public void After_going_back_the_built_payload_stays_ready() =>
        Assert.Equal(StartKind.Ready, StartDecision.Decide(
            new SetupState { Disc1Release = "1.0 USA", BuiltVersions = ["0.1.0", "0.2.0"] }, 2, "0.2.0"));

    // An update's Disc 1 extraction replaced the mod's files: setup puts them back before a start.
    [Fact]
    public void A_project_forza_plus_install_without_the_mod_resumes_setup() =>
        Assert.Equal(StartKind.Setup, StartDecision.Decide(
            new SetupState { Disc1Release = "1.0 PAL", BuiltVersions = ["0.2.0"], Game = "pfp4" }, 1, "0.2.0"));

    [Fact]
    public void A_project_forza_plus_install_with_the_mod_is_ready() =>
        Assert.Equal(StartKind.Ready, StartDecision.Decide(
            new SetupState { Disc1Release = "1.0 PAL", BuiltVersions = ["0.2.0"], Game = "pfp4", Pfp4Applied = true }, 1, "0.2.0"));
}
