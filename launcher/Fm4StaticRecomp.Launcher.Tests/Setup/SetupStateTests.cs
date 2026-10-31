// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using Fm4StaticRecomp.Launcher.Core.Setup;

namespace Fm4StaticRecomp.Launcher.Tests.Setup;

public class SetupStateTests
{
    [Fact]
    public void Round_trips()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "setup-state.json");
        var state = SetupState.Load(path);
        state.Disc1Release = "1.0 USA";
        state.Disc1ProblemsAccepted = 2;
        state.Disc2Source = @"D:\Дампы\FM4 Disc 2.iso";
        state.Disc2Later = true;
        state.Disc2Installed = true;
        state.BuiltVersions.Add("0.2.0");
        state.Save();
        Assert.Contains("\"disc1_release\"", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
        var read = SetupState.Load(path);
        Assert.Equal(1, read.SchemaVersion);
        Assert.True(read.Disc1Installed);
        Assert.Equal(("1.0 USA", 2, @"D:\Дампы\FM4 Disc 2.iso", true, true),
            (read.Disc1Release, read.Disc1ProblemsAccepted, read.Disc2Source, read.Disc2Later, read.Disc2Installed));
        Assert.Equal(["0.2.0"], read.BuiltVersions);
    }

    [Fact]
    public void A_newer_file_loads_what_this_launcher_knows()
    {
        using var temp = new TempDir();
        var path = temp.File("setup-state.json",
            """{"schema_version": 2, "built_versions": ["0.1.0", "0.2.0"], "something_new": {"x": 1}}"""u8.ToArray());
        Assert.Equal(["0.1.0", "0.2.0"], SetupState.Load(path).BuiltVersions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_missing_file_is_a_fresh_setup()
    {
        using var temp = new TempDir();
        var state = SetupState.Load(Path.Combine(temp.Path, "none.json"));
        Assert.False(state.Disc1Installed);
        Assert.Empty(state.BuiltVersions);
    }

    [Fact]
    public void A_state_without_game_is_vanilla()
    {
        using var temp = new TempDir();
        var path = temp.File("setup-state.json", """{"schema_version": 1, "disc1_release": "1.0 USA"}"""u8.ToArray());
        var state = SetupState.Load(path);
        Assert.Null(state.Game);
        Assert.False(state.Pfp4Applied);
    }

    [Fact]
    public void Round_trips_a_pfp4_install()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "setup-state.json");
        var state = SetupState.Load(path);
        (state.Game, state.Pfp4Mode, state.Pfp4HarderAi, state.Pfp4Applied) = ("pfp4", "hard", true, true);
        state.Save();
        var read = SetupState.Load(path);
        Assert.Equal(("pfp4", "hard", true, true), (read.Game, read.Pfp4Mode, read.Pfp4HarderAi, read.Pfp4Applied));
        Assert.Contains("\"pfp4_harder_ai\"", File.ReadAllText(path));
    }
}
