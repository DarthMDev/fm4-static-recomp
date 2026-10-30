using Fm4StaticRecomp.Launcher.Core.Profiles;

namespace Fm4StaticRecomp.Launcher.Tests.Profiles;

public class SaveOriginTests
{
    private const string Dlc = @"0000000000000000\4D530910\00000002\94C659329A894CFA993A7812251D576B679DA1894D\Media\DLCZips";

    private static string Xenia(TempDir temp)
    {
        temp.File(@"xenia\E03000004331734B\4D530910\00000001\ForzaProfile\ForzaProfile", [1]);
        return Path.Combine(temp.Path, "xenia");
    }

    [Fact]
    public void SaveOrigin_reads_a_xenia_folder_with_the_mods_dlc()
    {
        using var temp = new TempDir();
        var source = Xenia(temp);
        temp.File($@"xenia\{Dlc}\PhysicsTrack_pri_65.zip", [2]);
        temp.File($@"xenia\{Dlc}\0015_pri_65\Media\Tracks\a.xml", [3]);
        Assert.Equal("pfp4", SaveOrigin.Guess(source));
    }

    [Fact]
    public void SaveOrigin_reads_a_xenia_folder_with_vanilla_zips()
    {
        using var temp = new TempDir();
        var source = Xenia(temp);
        // Vanilla's pack 15 keeps a loose folder beside its zip.
        temp.File($@"xenia\{Dlc}\0015_pri_65.zip", [2]);
        temp.File($@"xenia\{Dlc}\0015_pri_65\Media\AI\a.bin", [3]);
        Assert.Equal("vanilla", SaveOrigin.Guess(source));
    }

    [Fact]
    public void SaveOrigin_reads_a_xenia_folder_without_dlc()
    {
        using var temp = new TempDir();
        Assert.Null(SaveOrigin.Guess(Xenia(temp)));
    }
}
