using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Fm4StaticRecomp.Launcher.Core.Content;
using Fm4StaticRecomp.Launcher.Core.Discs;
using Fm4StaticRecomp.Launcher.Core.Install;

namespace Fm4StaticRecomp.Launcher.Core.Pfp4;

// Puts Project Forza Plus 4 over an extracted Disc 1: the mod's executable and game files, the
// chosen mode's databases, every mode cached for later switches (InstallLayout.Pfp4Modes), and
// all 20 content packages with their headers. Everything is written to <content>.partial and
// <pfp4>.partial first and checked against the manifest while copying; only a complete, matching
// mod is moved into place, so an interrupted run is simply run again.
public static class Pfp4Installer
{
    private const string GameDb = @"Media\db\gamedb.slt";
    private const string Packages = @"4D530910\00000002";
    private const string Headers = @"4D530910\Headers\00000002";
    // ContentManager::WriteContentHeaderFile's XCONTENT_AGGREGATE_DATA, big-endian; a licence
    // mask, when the package has one, follows as a little-endian u32.
    private const int HeaderSize = 0x148;
    private const int DisplayNameOffset = 0x08;
    private const int DisplayNameChars = 128;
    private const int FileNameOffset = 0x108;
    private const int FileNameChars = 42;
    private const int TitleIdOffset = 0x140;
    private const uint DeviceId = 1;
    private const uint ContentType = 2;
    private const uint TitleId = 0x4D530910;

    public static long ContentBytes(Pfp4Manifest manifest) => Entries(manifest).Sum(e => e.Size);

    private static IEnumerable<Pfp4Entry> Entries(Pfp4Manifest manifest) =>
        manifest.GameFiles
            .Concat(manifest.Modes.Values.SelectMany(m => (IEnumerable<Pfp4Entry>)
                [m.GameDb, m.HarderAi.GameDb, m.HarderAi.Tunables, .. m.DlcDbs, .. m.SkipIntro.SelectMany(s => s.Files)]))
            .Concat(manifest.Packages.SelectMany(p => p.Files));

    public static async Task<IReadOnlyList<FileProblem>> InstallAsync(ModSource mod, Pfp4Manifest manifest,
        InstallLayout layout, string mode, bool harderAi, IProgress<double>? progress, CancellationToken cancellation)
    {
        var chosen = manifest.Modes[mode];
        var content = Path.TrimEndingDirectorySeparator(Path.GetFullPath(layout.Content)) + ".partial";
        var cache = Path.TrimEndingDirectorySeparator(Path.GetFullPath(layout.Pfp4)) + ".partial";
        var staged = Path.Combine(cache, "game");
        DeleteIfPresent(content);
        DeleteIfPresent(cache);
        try
        {
            var copies = new List<(Pfp4Entry Entry, string Destination)>();
            copies.AddRange(manifest.GameFiles.Select(f => (f, Under(staged, f.Target))));
            foreach (var (name, m) in manifest.Modes)
            {
                var folder = Path.Combine(cache, "modes", name);
                copies.Add((m.GameDb, Path.Combine(folder, "gamedb.slt")));
                copies.Add((m.HarderAi.GameDb, Path.Combine(folder, "harder-ai", "gamedb.slt")));
                copies.Add((m.HarderAi.Tunables, Path.Combine(folder, "harder-ai", "GameTunableSettings.ini")));
                copies.AddRange(m.DlcDbs.Select(f => (f, Under(Path.Combine(folder, "dlc"), f.Target))));
                foreach (var save in m.SkipIntro)
                    copies.AddRange(save.Files.Select(f => (f, Under(Path.Combine(folder, "skip-intro", save.Name), f.Target))));
            }
            foreach (var package in manifest.Packages)
                copies.AddRange(package.Files.Select(f => (f, Under(Path.Combine(Under(content, Packages), package.Folder), f.Target))));

            var problems = await CopyAsync(mod, copies, ContentBytes(manifest), progress, cancellation);
            problems.AddRange(manifest.Patches.Where(p => !Patch(Under(Under(content, Packages), p.Target), p))
                .Select(p => new FileProblem(p.Target, FileProblemKind.HashMismatch)));
            if (problems.Count > 0)
            {
                DeleteIfPresent(content);
                DeleteIfPresent(cache);
                return problems;
            }

            Copy(Under(staged, Pfp4Manifest.Tunables), Path.Combine(cache, "GameTunableSettings.ini"));
            var modeFolder = Path.Combine(cache, "modes", mode);
            Copy(Path.Combine(modeFolder, harderAi ? "harder-ai" : "", "gamedb.slt"), Under(staged, GameDb));
            if (harderAi) Copy(Path.Combine(modeFolder, "harder-ai", "GameTunableSettings.ini"), Under(staged, Pfp4Manifest.Tunables));
            foreach (var db in chosen.DlcDbs)
                Copy(Under(Path.Combine(modeFolder, "dlc"), db.Target), Under(Under(content, Packages), db.Target));
            WriteHeaders(manifest, content);
            cancellation.ThrowIfCancellationRequested();

            foreach (var file in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories).ToList())
            {
                var target = Path.Combine(layout.Game, Path.GetRelativePath(staged, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(file, target, overwrite: true);
            }
            Directory.Delete(staged, recursive: true);
            Disc1Step.Swap(content, layout.Content);
            Disc1Step.Swap(cache, layout.Pfp4);
            return [];
        }
        catch
        {
            DeleteIfPresent(content);
            DeleteIfPresent(cache);
            throw;
        }
    }

    // Reads every mod file once, hashing while it writes; a file of the wrong size is not copied.
    private static async Task<List<FileProblem>> CopyAsync(ModSource mod, IReadOnlyList<(Pfp4Entry Entry, string Destination)> copies,
        long total, IProgress<double>? progress, CancellationToken cancellation)
    {
        var present = new HashSet<string>(mod.Files, StringComparer.OrdinalIgnoreCase);
        var problems = new List<FileProblem>();
        var buffer = new byte[1 << 20];
        long done = 0;
        total = Math.Max(1, total);
        foreach (var (entry, destination) in copies)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!present.Contains(entry.Source))
            {
                problems.Add(new FileProblem(entry.Source, FileProblemKind.Missing));
                continue;
            }
            if (mod.Size(entry.Source) != entry.Size)
            {
                problems.Add(new FileProblem(entry.Source, FileProblemKind.SizeMismatch));
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var input = mod.OpenFile(entry.Source))
            await using (var output = File.Create(destination))
            {
                int read;
                while ((read = await input.ReadAsync(buffer, cancellation)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    done += read;
                }
            }
            progress?.Report((double)done / total);
            if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                problems.Add(new FileProblem(entry.Source, FileProblemKind.HashMismatch));
        }
        return problems;
    }

    // Latin-1 maps every byte to one char, so the file's bytes outside the replaced text survive.
    private static bool Patch(string path, Pfp4Patch patch)
    {
        if (!File.Exists(path)) return false;
        var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        foreach (var replace in patch.Replace) text = text.Replace(replace.From, replace.To, StringComparison.Ordinal);
        var bytes = Encoding.Latin1.GetBytes(text);
        if (bytes.LongLength != patch.Size ||
            !Convert.ToHexString(SHA256.HashData(bytes)).Equals(patch.Sha256, StringComparison.OrdinalIgnoreCase))
            return false;
        File.WriteAllBytes(path, bytes);
        return true;
    }

    private static void WriteHeaders(Pfp4Manifest manifest, string content)
    {
        var folder = Under(content, Headers);
        Directory.CreateDirectory(folder);
        var names = Settings.TomlFile.Load(Path.Combine(content, "names.toml"));
        foreach (var package in manifest.Packages)
        {
            var header = new byte[HeaderSize + (package.LicenseMask is null ? 0 : 4)];
            BinaryPrimitives.WriteUInt32BigEndian(header, DeviceId);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), ContentType);
            var name = package.DisplayName.Length > DisplayNameChars ? package.DisplayName[..DisplayNameChars] : package.DisplayName;
            Encoding.BigEndianUnicode.GetBytes(name, header.AsSpan(DisplayNameOffset));
            Encoding.ASCII.GetBytes(package.Folder.AsSpan(0, Math.Min(package.Folder.Length, FileNameChars)), header.AsSpan(FileNameOffset));
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(TitleIdOffset), TitleId);
            if (package.LicenseMask is { } mask) BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(HeaderSize), mask);
            File.WriteAllBytes(Path.Combine(folder, package.Folder + ".header"), header);
            names.Set(ContentInstaller.Key(package.Folder), package.DisplayName, quoted: true);
        }
        names.Save(Path.Combine(content, "names.toml"));
    }

    private static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static string Under(string root, string relative) =>
        Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar));

    private static void DeleteIfPresent(string folder)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
