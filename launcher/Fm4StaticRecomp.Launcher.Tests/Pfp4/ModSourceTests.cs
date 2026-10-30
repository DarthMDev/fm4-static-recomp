using System.IO.Compression;
using System.Text;
using Fm4StaticRecomp.Launcher.Core.Pfp4;

namespace Fm4StaticRecomp.Launcher.Tests.Pfp4;

public class ModSourceTests
{
    private const string Xex = @"Main Game Changes\default.xex";
    private const string Ini = @"Main Game Changes\Media\physics\PhysicsSettings.ini";

    private static string Zip(TempDir temp, string prefix)
    {
        var path = temp.File("mod.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        zip.CreateEntry(prefix + "Project Forza Plus 4/");
        foreach (var (name, text) in new[] { (Xex, "xex"), (Ini, "physics") })
        {
            using var writer = new StreamWriter(zip.CreateEntry(prefix + "Project Forza Plus 4/" + name.Replace('\\', '/')).Open());
            writer.Write(text);
        }
        return path;
    }

    private static string Folder(TempDir temp)
    {
        temp.File(Path.Combine("mod", "Project Forza Plus 4", Xex), "xex"u8.ToArray());
        temp.File(Path.Combine("mod", "Project Forza Plus 4", Ini), "physics"u8.ToArray());
        return Path.Combine(temp.Path, "mod");
    }

    [Fact]
    public void Finds_the_root_in_a_zip_with_a_wrapper_folder()
    {
        using var temp = new TempDir();
        using var mod = ModSource.Open(Zip(temp, "wrap/"));
        Assert.Equal([Ini, Xex], mod.Files.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Finds_the_root_when_given_its_parent_folder()
    {
        using var temp = new TempDir();
        using var mod = ModSource.Open(Folder(temp));
        Assert.Equal([Ini, Xex], mod.Files.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Finds_the_root_when_given_the_root_itself()
    {
        using var temp = new TempDir();
        using var mod = ModSource.Open(Path.Combine(Folder(temp), "Project Forza Plus 4"));
        Assert.Equal([Ini, Xex], mod.Files.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Refuses_a_folder_without_the_mod()
    {
        using var temp = new TempDir();
        var inside = Path.Combine(Folder(temp), "Project Forza Plus 4", "Main Game Changes");
        var error = Assert.Throws<InvalidDataException>(() => ModSource.Open(inside));
        Assert.Contains("Project Forza Plus 4", error.Message);
    }

    [Fact]
    public void Reads_an_entry_by_relative_path()
    {
        using var temp = new TempDir();
        foreach (var source in new[] { Zip(temp, ""), Folder(temp) })
        {
            using var mod = ModSource.Open(source);
            Assert.Equal(7, mod.Size(Ini.ToUpperInvariant()));
            using var reader = new StreamReader(mod.OpenFile(Ini), Encoding.UTF8);
            Assert.Equal("physics", reader.ReadToEnd());
        }
    }
}
