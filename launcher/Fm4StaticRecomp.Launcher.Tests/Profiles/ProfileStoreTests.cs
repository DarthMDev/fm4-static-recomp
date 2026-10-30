using Fm4StaticRecomp.Launcher.Core.Install;
using Fm4StaticRecomp.Launcher.Core.Platform;
using Fm4StaticRecomp.Launcher.Core.Profiles;
using Fm4StaticRecomp.Launcher.Core.Setup;

namespace Fm4StaticRecomp.Launcher.Tests.Profiles;

public class ProfileStoreTests
{
    private static (TempDir Temp, ProfileStore Store, InstallLayout Layout) Make(string installGame = "vanilla")
    {
        var temp = new TempDir();
        var layout = new InstallLayout(temp.Path);
        Directory.CreateDirectory(layout.Content);
        return (temp, new ProfileStore(layout, Platforms.Current, installGame), layout);
    }

    [Fact]
    public void Creates_a_profile_with_the_content_link()
    {
        var (temp, store, layout) = Make();
        using var _ = temp;
        File.WriteAllText(Path.Combine(layout.Content, "probe"), "x");
        var profile = store.Create("Ilya's Garage");
        Assert.Equal("ilya-s-garage", profile.Id);
        Assert.Equal("Ilya s Garage", profile.Gamertag);
        Assert.True(File.Exists(Path.Combine(profile.User, ProfileStore.ContentFolderName, "probe")));
        Assert.Equal(["ilya-s-garage"], store.List().Select(p => p.Id));
    }

    [Fact]
    public void A_profile_whose_link_fails_is_not_left_behind()
    {
        using var temp = new TempDir();
        var layout = new InstallLayout(temp.Path);
        var store = new ProfileStore(layout, new NoLinks(), "vanilla");
        Assert.Throws<IOException>(() => store.Create("Main"));
        Assert.Empty(store.List());
        Assert.Empty(Directory.GetDirectories(layout.Profiles));
    }

    private sealed class NoLinks : IPlatform
    {
        public bool SupportsDxgiPresentation => false;
        public string ExecutableName(string baseName) => baseName;
        public void CreateDirectoryLink(string link, string target) => throw new IOException("The drive holds no links.");
        public bool IsDirectoryLink(string path) => false;
        public System.Text.Encoding ConsoleEncoding => System.Text.Encoding.UTF8;
    }

    [Fact]
    public void Ids_stay_unique_and_fixed_on_rename()
    {
        var (temp, store, _) = Make();
        using var _t = temp;
        var a = store.Create("Main");
        var b = store.Create("Main");
        Assert.NotEqual(a.Id, b.Id);
        var renamed = store.Rename(a.Id, "Career", "Racer1");
        Assert.Equal(a.Id, renamed.Id);
        Assert.Equal("Career", store.List().Single(p => p.Id == a.Id).Name);
    }

    [Fact]
    public void Delete_moves_the_profile_aside()
    {
        var (temp, store, layout) = Make();
        using var _ = temp;
        var profile = store.Create("Old");
        store.Delete(profile.Id);
        Assert.Empty(store.List());
        Assert.Single(Directory.GetDirectories(layout.Deleted));
        Assert.True(Directory.Exists(layout.Content));
    }

    [Fact]
    public void Import_copies_saves_but_not_installed_content()
    {
        var (temp, store, _) = Make();
        using var _t = temp;
        temp.File("old/user/B13EBABEBABEBABE/4D530910/00000001/ForzaProfile/ForzaProfile", [1]);
        temp.File("old/user/0000000000000000/4D530910/00000002/pack/big.bin", [2]);
        var profile = store.Import(Path.Combine(temp.Path, "old"), "Imported", "vanilla");
        Assert.True(File.Exists(Path.Combine(profile.User, "B13EBABEBABEBABE", "4D530910", "00000001", "ForzaProfile", "ForzaProfile")));
        Assert.False(File.Exists(Path.Combine(profile.User, ProfileStore.ContentFolderName, "4D530910", "00000002", "pack", "big.bin")));
    }

    [Fact]
    public void Import_refuses_a_folder_without_a_save()
    {
        var (temp, store, layout) = Make();
        using var _t = temp;
        temp.File("old/user/0000000000000000/4D530910/00000002/pack/big.bin", [2]);
        temp.File("old/notes.txt", [3]);
        Assert.Throws<IOException>(() => store.Import(Path.Combine(temp.Path, "old"), "Imported", "vanilla"));
        Assert.Empty(store.List());
        Assert.False(Directory.Exists(layout.Profiles));
    }

    [Fact]
    public void Import_refuses_the_install_s_own_folder()
    {
        var (temp, store, layout) = Make();
        using var _t = temp;
        temp.File("B13EBABEBABEBABE/4D530910/00000001/ForzaProfile/ForzaProfile", [1]);
        Assert.Throws<IOException>(() => store.Import(temp.Path, "Imported", "vanilla"));
        Assert.Empty(store.List());
        Assert.False(Directory.Exists(layout.Profiles));
    }

    [Fact]
    public void An_import_that_fails_midway_leaves_no_profile()
    {
        var (temp, store, layout) = Make();
        using var _t = temp;
        File.WriteAllText(Path.Combine(layout.Content, "probe"), "x");
        temp.File("old/user/B13EBABEBABEBABE/4D530910/00000001/ForzaProfile/ForzaProfile", [1]);
        var locked = temp.File("old/user/B13EBABEBABEBABE/4D530910/00000001/ForzaProfile/Locked", [2]);
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => store.Import(Path.Combine(temp.Path, "old"), "Imported", "vanilla"));
        Assert.Empty(store.List());
        Assert.Empty(Directory.GetDirectories(layout.Profiles));
        Assert.True(File.Exists(Path.Combine(layout.Content, "probe")));
    }

    [Fact]
    public void A_name_of_symbols_still_makes_a_profile()
    {
        var (temp, store, _) = Make();
        using var _t = temp;
        var profile = store.Create("!!!");
        Assert.Equal("profile", profile.Id);
        Assert.Equal("Player", profile.Gamertag);
        Assert.Equal("profile-2", store.Create("???").Id);
    }

    [Theory]
    [InlineData("Racer", true)]
    [InlineData("Racer 2", true)]
    [InlineData("2Fast", false)]
    [InlineData("", false)]
    [InlineData("SixteenCharsLong", false)]
    [InlineData("Ünïcode", false)]
    public void Validates_gamertags(string gamertag, bool valid) =>
        Assert.Equal(valid, ProfileStore.IsValidGamertag(gamertag));

    [Fact]
    public void A_cyrillic_name_keeps_its_name_and_gets_a_player_gamertag()
    {
        var (temp, store, _) = Make();
        using var _t = temp;
        var profile = store.Create("Илья");
        Assert.Equal(("Илья", "profile", "Player"), (profile.Name, profile.Id, profile.Gamertag));
        Assert.Equal("Илья", store.List().Single().Name);
    }

    private static ProfileStore Pfp4Store(InstallLayout layout, string mode = "normal", bool harderAi = false)
    {
        var state = SetupState.Load(layout.SetupState);
        (state.Game, state.Pfp4Mode, state.Pfp4HarderAi, state.Pfp4Applied) = ("pfp4", mode, harderAi, true);
        state.Save();
        return new ProfileStore(layout, Platforms.Current, "pfp4");
    }

    private static void Save(TempDir temp, string user) =>
        temp.File(Path.Combine(user, "B13EBABEBABEBABE", "4D530910", "00000001", "ForzaProfile", "ForzaProfile"), [1]);

    [Fact]
    public void A_profile_without_game_is_vanilla()
    {
        var (temp, store, layout) = Make();
        using var _t = temp;
        temp.File(Path.Combine(layout.Profiles, "old", "profile.toml"), "name = \"Old\"\ngamertag = \"Old\"\n"u8.ToArray());
        var old = store.Get("old");
        Assert.Equal(("vanilla", (string?)null, (bool?)null), (old.Game, old.Pfp4Mode, old.Pfp4HarderAi));
    }

    [Fact]
    public void Create_stamps_the_install_game_and_mode()
    {
        var (temp, store, layout) = Make();
        using var _t = temp;
        var plain = store.Create("Plain");
        Assert.Equal(("vanilla", (string?)null), (plain.Game, plain.Pfp4Mode));
        var created = Pfp4Store(layout, "hard", true).Create("Modded");
        Assert.Equal(("pfp4", "hard", (bool?)true), (created.Game, created.Pfp4Mode, created.Pfp4HarderAi));
        var text = File.ReadAllText(Path.Combine(created.Folder, "profile.toml"));
        Assert.Contains("game = \"pfp4\"", text);
        Assert.Contains("pfp4_mode = \"hard\"", text);
    }

    [Fact]
    public void Import_refuses_a_vanilla_save_in_a_pfp4_install()
    {
        var (temp, _, layout) = Make();
        using var _t = temp;
        var store = Pfp4Store(layout);
        Save(temp, Path.Combine("old", "user"));
        var error = Assert.Throws<IOException>(() => store.Import(Path.Combine(temp.Path, "old"), "Imported", "vanilla"));
        Assert.Contains("Project Forza Plus 4", error.Message);
        Assert.Empty(store.List());
    }

    [Fact]
    public void Import_refuses_a_stamped_profile_of_the_other_game_whatever_was_declared()
    {
        var (temp, _, layout) = Make();
        using var _t = temp;
        var other = new InstallLayout(Path.Combine(temp.Path, "other"));
        var source = new ProfileStore(other, Platforms.Current, "vanilla").Create("Vanilla career");
        Save(temp, source.User);
        var pfp4 = Pfp4Store(layout);
        Assert.Contains("vanilla saves", Assert.Throws<IOException>(() => pfp4.Import(source.Folder, "Imported", "pfp4")).Message);
        Assert.Contains("vanilla saves", Assert.Throws<IOException>(() => pfp4.Import(source.User, "Imported", "pfp4")).Message);
        Assert.Empty(pfp4.List());
    }

    [Fact]
    public void Import_carries_the_stamped_mode()
    {
        var (temp, _, layout) = Make();
        using var _t = temp;
        var source = Pfp4Store(new InstallLayout(Path.Combine(temp.Path, "other")), "sandbox").Create("Sandbox career");
        Save(temp, source.User);
        var imported = Pfp4Store(layout, "normal").Import(source.Folder, "Copy", "vanilla");
        Assert.Equal(("pfp4", "sandbox", (bool?)false), (imported.Game, imported.Pfp4Mode, imported.Pfp4HarderAi));
    }

    [Fact]
    public void Import_without_stamp_leaves_the_mode_unknown()
    {
        var (temp, _, layout) = Make();
        using var _t = temp;
        var store = Pfp4Store(layout, "hard");
        temp.File(@"xenia\E03000004331734B\4D530910\00000001\ForzaProfile\ForzaProfile", [1]);
        var imported = store.Import(Path.Combine(temp.Path, "xenia"), "Xenia", "pfp4");
        Assert.Equal(("pfp4", (string?)null, (bool?)null), (imported.Game, imported.Pfp4Mode, imported.Pfp4HarderAi));
    }

    [Fact]
    public void A_skip_intro_profile_holds_the_save_and_its_mode()
    {
        var (temp, _, layout) = Make();
        using var _t = temp;
        var store = Pfp4Store(layout, "vanilla");
        var save = Path.Combine(layout.Pfp4Modes, "vanilla", "skip-intro", "Fox");
        temp.File(Path.Combine(save, "ForzaProfile", "ForzaProfile"), [7]);
        temp.File(Path.Combine(save, "ForzaProfile", "Liveries", "6.liv"), [8]);
        var profile = store.CreateFromSkipIntro("Fox start", save);
        var title = Path.Combine(profile.User, "B13EBABEBABEBABE", "4D530910");
        Assert.Equal([7], File.ReadAllBytes(Path.Combine(title, "00000001", "ForzaProfile", "ForzaProfile")));
        Assert.Equal([8], File.ReadAllBytes(Path.Combine(title, "00000001", "ForzaProfile", "Liveries", "6.liv")));
        var header = File.ReadAllBytes(Path.Combine(title, "Headers", "00000001", "ForzaProfile.header"));
        Assert.Equal(0x148, header.Length);
        Assert.Equal(1u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4)));
        Assert.Equal("User", System.Text.Encoding.BigEndianUnicode.GetString(header, 8, 0x100).TrimEnd('\0'));
        Assert.Equal("ForzaProfile", System.Text.Encoding.ASCII.GetString(header, 0x108, 42).TrimEnd('\0'));
        Assert.Equal(("pfp4", "vanilla", (bool?)false), (profile.Game, profile.Pfp4Mode, profile.Pfp4HarderAi));
        Assert.True(Directory.Exists(Path.Combine(profile.User, ProfileStore.ContentFolderName)));
    }

    [Fact]
    public void Mode_warning_names_the_profiles_mode()
    {
        var profile = new Profile("a", "a", "A", "A", null, "pfp4", "hard", false);
        Assert.Contains("Hard", ProfileStore.ModeWarning(profile, "normal", false)!);
        Assert.Contains("Harder AI", ProfileStore.ModeWarning(profile, "hard", true)!);
        Assert.Null(ProfileStore.ModeWarning(profile, "hard", false));
    }

    [Fact]
    public void No_warning_for_an_unknown_mode()
    {
        Assert.Null(ProfileStore.ModeWarning(new Profile("a", "a", "A", "A", null, "pfp4", null, null), "normal", false));
    }

    [Fact]
    public void Recording_the_mode_ends_the_warning()
    {
        var (temp, _, layout) = Make();
        using var _t = temp;
        var store = Pfp4Store(layout, "hard");
        var profile = store.Create("Career");
        store.RecordMode(profile.Id, "normal", true);
        var read = store.Get(profile.Id);
        Assert.Equal(("normal", (bool?)true), (read.Pfp4Mode, read.Pfp4HarderAi));
        Assert.Null(ProfileStore.ModeWarning(read, "normal", true));
        store.Rename(profile.Id, "Renamed", "Racer");
        Assert.Equal(("pfp4", "normal"), (store.Get(profile.Id).Game, store.Get(profile.Id).Pfp4Mode));
    }
}
