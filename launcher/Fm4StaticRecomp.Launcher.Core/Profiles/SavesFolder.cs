// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using Fm4StaticRecomp.Launcher.Core.Install;
using Fm4StaticRecomp.Launcher.Core.Platform;
using Fm4StaticRecomp.Launcher.Core.Settings;
using Fm4StaticRecomp.Launcher.Core.Updates;

namespace Fm4StaticRecomp.Launcher.Core.Profiles;

// SETTINGS → SAVES FOLDER (#20): the profiles, their backups and deleted profiles move together, so
// a delete or a backup never crosses drives. A folder that already holds saves (an earlier install's,
// or one synced from another PC) is taken over: this launcher's saves are merged into it.
public static class SavesFolder
{
    private static readonly string[] Parts = ["profiles", "backups", "deleted"];

    // Why the saves cannot move to this folder; null when they can (or are there already).
    public static string? Problem(InstallLayout layout, string folder)
    {
        if (ProfileStore.SamePath(folder, layout.SavesRoot)) return null;
        if (ProfileStore.IsUnder(folder, layout.Root) && !ProfileStore.SamePath(folder, layout.Root))
            return $"{folder} is inside the launcher's data folder. Choose a folder outside it; DEFAULT keeps the saves in the data folder.";
        if (new[] { layout.Profiles, layout.Backups, layout.Deleted }.Any(part => ProfileStore.IsUnder(folder, part)) || ReachesCurrentSaves(layout, folder))
            return $"{folder} is the current saves folder, or inside it, reached by another path. Choose another folder.";
        if (OtherInstall(layout, folder) is { } install)
            return $"{folder} belongs to another launcher install ({install}); its saves stay with it. " +
                   "To play them here, use PROFILE → IMPORT SAVE, which copies one into a new profile.";
        if (Parts.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase) || ProfilesIn(folder).Any())
            return $"{folder} is a profiles folder itself. Choose the folder above it, {Path.GetDirectoryName(folder)}.";
        return null;
    }

    // A path that differs in text can still be the same folder (a junction, a subst drive): a
    // marker written into the current saves shows up in the chosen folder or one above it.
    private static bool ReachesCurrentSaves(InstallLayout layout, string folder)
    {
        var name = $".fm4-saves-check-{Guid.NewGuid():N}";
        var markers = new[] { layout.SavesRoot, layout.Profiles, layout.Backups, layout.Deleted }
            .Where(Directory.Exists).Select(d => Path.Combine(d, name)).ToList();
        try
        {
            foreach (var marker in markers) File.WriteAllBytes(marker, []);
            for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, name))) return true;
            return false;
        }
        finally
        {
            foreach (var marker in markers.Where(File.Exists)) File.Delete(marker);
        }
    }

    // The data folder of another install at or above the folder: its launcher would still use the saves.
    private static string? OtherInstall(InstallLayout layout, string folder)
    {
        for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
            if (!ProfileStore.SamePath(directory.FullName, layout.Root) &&
                (File.Exists(Path.Combine(directory.FullName, "setup-state.json")) ||
                 File.Exists(Path.Combine(directory.FullName, "config", "launcher.toml"))))
                return directory.FullName;
        return null;
    }

    // The names of the profiles in a saves folder that are of the other game than this install's.
    // A profile without `game` comes from a vanilla install (0.3.0 and older stamped none).
    public static IReadOnlyList<string> OtherGameProfiles(string folder, string installGame) =>
        ProfilesIn(Path.Combine(folder, "profiles"))
            .Select(d => TomlFile.Load(Path.Combine(d, ProfileStore.FileName)))
            .Where(toml => (toml.Get("game") ?? "vanilla") != installGame)
            .Select(toml => toml.Get("name") ?? "?").Order(StringComparer.CurrentCultureIgnoreCase).ToList();

    public static string OtherGameWarning(string installGame, IReadOnlyList<string> names)
    {
        var (theirs, ours) = installGame == ProfileStore.Pfp4
            ? ("vanilla FM4", "Project Forza Plus 4")
            : ("Project Forza Plus 4", "vanilla FM4");
        return $"These saves are from {theirs}: {string.Join(", ", names)}. On this {ours} install they are not guaranteed to work: " +
               "a career can hang or break races on parts the other game does not have. Starting a fresh career is recommended.";
    }

    // Kept in each such profile, as an import past a warning is, for whoever reads its log after a problem.
    public static void NoteOtherGame(string folder, string installGame, string note)
    {
        foreach (var profile in ProfilesIn(Path.Combine(folder, "profiles")))
        {
            var path = Path.Combine(profile, ProfileStore.FileName);
            var toml = TomlFile.Load(path);
            if ((toml.Get("game") ?? "vanilla") == installGame) continue;
            toml.Set("import_warning", note, quoted: true);
            toml.Save(path);
        }
    }

    // Merges the saves into the new folder and links each profile there to this install's content;
    // the old ones stay until Remove, after the setting is saved. A profile whose id the folder has
    // already gets the next id free there and among the profiles moving, "Main (2)"; one without a
    // save yet (the profile setup makes) goes to deleted instead. A failure takes back what was added
    // and leaves the folder's own saves.
    public static SavesMove Copy(InstallLayout from, InstallLayout to, IPlatform platform)
    {
        Directory.CreateDirectory(to.SavesRoot);
        ProbeLinks(to, platform);
        // Each entry is copied here first and renamed into place: a copy cut short never looks like a profile.
        var staging = Path.Combine(to.SavesRoot, $".moving-{Guid.NewGuid():N}");
        var added = new List<string>();
        var taken = new List<string>();
        var renamed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var theirs = Directory.Exists(to.Profiles)
                ? Directory.GetFileSystemEntries(to.Profiles).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string?>(StringComparer.OrdinalIgnoreCase);
            var ours = Entries(from.Profiles).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Entries(from.Profiles))
            {
                var id = Path.GetFileName(entry);
                taken.Add(entry);
                if (!File.Exists(Path.Combine(entry, ProfileStore.FileName)) || !theirs.Contains(id))
                {
                    CopyEntry(entry, to.Profiles, id, staging, platform, added);
                    continue;
                }
                if (!ProfileStore.HoldsSave(Path.Combine(entry, "user")))
                {
                    CopyEntry(entry, to.Deleted, $"{id}-{DateTime.Now:yyyyMMdd-HHmmss}", staging, platform, added);
                    continue;
                }
                var n = 2;
                while (theirs.Contains($"{id}-{n}") || ours.Contains($"{id}-{n}") || renamed.ContainsValue($"{id}-{n}")) n++;
                renamed[id] = $"{id}-{n}";
                var copy = CopyEntry(entry, to.Profiles, renamed[id], staging, platform, added);
                var toml = TomlFile.Load(Path.Combine(copy, ProfileStore.FileName));
                toml.Set("name", $"{toml.Get("name") ?? id} ({n})", quoted: true);
                toml.Save(Path.Combine(copy, ProfileStore.FileName));
            }
            foreach (var entry in Entries(from.Backups))
            {
                var name = Path.GetFileName(entry);
                taken.Add(entry);
                // A backup's name starts with its profile's id (SaveBackup), so it follows a renamed profile.
                var old = renamed.Keys.SingleOrDefault(id => SaveBackup.IsBackupOf(name, id));
                CopyEntry(entry, to.Backups, old is null ? name : renamed[old] + name[old.Length..], staging, platform, added);
            }
            foreach (var entry in Entries(from.Deleted))
            {
                taken.Add(entry);
                CopyEntry(entry, to.Deleted, Path.GetFileName(entry), staging, platform, added);
            }
            foreach (var profile in ProfilesIn(to.Profiles))
                ProfileStore.EnsureContentLink(platform, Path.Combine(profile, "user", ProfileStore.ContentFolderName), to.Content);
            return new SavesMove(from, added, taken, renamed);
        }
        catch
        {
            Undo(added);
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) DeleteTree(staging);
        }
    }

    // Takes back a copy whose move did not complete (the setting could not be saved).
    public static void Undo(SavesMove move) => Undo(move.Added);

    private static void Undo(IEnumerable<string> added)
    {
        foreach (var path in added.Reverse())
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) DeleteTree(path);
    }

    // Deletes what Copy took from the old folder, each profile's profile.toml first: a copy left by
    // a delete that fails part way is no longer a profile, so a later move back cannot merge it in.
    public static void Remove(SavesMove move)
    {
        foreach (var stamp in move.Taken.Where(e => new DirectoryInfo(e).LinkTarget is null)
                     .Select(e => Path.Combine(e, ProfileStore.FileName)).Where(File.Exists))
            File.Delete(stamp);
        foreach (var entry in move.Taken)
            if (new DirectoryInfo(entry).LinkTarget is not null) Directory.Delete(entry);
            else if (Directory.Exists(entry)) DeleteTree(entry);
            else if (File.Exists(entry)) File.Delete(entry);
        foreach (var part in Parts.Select(p => Path.Combine(move.From.SavesRoot, p)))
            if (Directory.Exists(part) && !Directory.EnumerateFileSystemEntries(part).Any()) Directory.Delete(part);
    }

    // A link must not only be made but lead to the content: on some network shares it is made and
    // resolves on the server.
    private static void ProbeLinks(InstallLayout to, IPlatform platform)
    {
        Directory.CreateDirectory(to.Content);
        var name = $".link-test-{Guid.NewGuid():N}";
        var probe = Path.Combine(to.SavesRoot, name);
        var mark = Path.Combine(to.Content, name);
        try
        {
            File.WriteAllBytes(mark, []);
            platform.CreateDirectoryLink(probe, to.Content);
            if (!File.Exists(Path.Combine(probe, name))) throw new IOException("the link does not lead to it.");
        }
        catch (IOException error)
        {
            throw new IOException($"{to.SavesRoot} cannot hold the saves: Windows could not link Disc 2 and DLC into it ({error.Message}) " +
                                  "Choose a folder on a drive inside this PC; a network drive or a USB stick formatted FAT32 or exFAT cannot hold them.", error);
        }
        finally
        {
            if (Directory.Exists(probe)) Directory.Delete(probe);
            if (File.Exists(mark)) File.Delete(mark);
        }
    }

    private static IEnumerable<string> Entries(string folder) =>
        Directory.Exists(folder) ? Directory.GetFileSystemEntries(folder) : [];

    // Under the name asked for, or with -2, -3... when the folder has it already.
    private static string CopyEntry(string source, string folder, string name, string staging, IPlatform platform, List<string> added)
    {
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(staging);
        var target = Path.Combine(folder, name);
        for (var n = 2; Path.Exists(target); n++)
            target = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + $"-{n}" + Path.GetExtension(name));
        var temporary = Path.Combine(staging, Path.GetFileName(target));
        if (File.Exists(source))
        {
            File.Copy(source, temporary);
            File.Move(temporary, target);
        }
        else
        {
            CopyTree(source, temporary, platform);
            Directory.Move(temporary, target);
        }
        added.Add(target);
        return target;
    }

    // Links are copied as links to the same target: a save kept elsewhere through one stays there.
    private static void CopyTree(string source, string target, IPlatform platform)
    {
        if (new DirectoryInfo(source).LinkTarget is { } link)
        {
            platform.CreateDirectoryLink(target, Path.GetFullPath(link, Path.GetDirectoryName(source)!));
            return;
        }
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source)) CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)), platform);
    }

    private static IEnumerable<string> ProfilesIn(string folder) =>
        Directory.Exists(folder) ? Directory.GetDirectories(folder).Where(d => File.Exists(Path.Combine(d, ProfileStore.FileName))) : [];

    // Never walks into a link: a link is removed, what it points at stays.
    private static void DeleteTree(string folder)
    {
        if (new DirectoryInfo(folder).LinkTarget is not null)
        {
            Directory.Delete(folder);
            return;
        }
        foreach (var directory in Directory.GetDirectories(folder)) DeleteTree(directory);
        foreach (var file in Directory.GetFiles(folder))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        Directory.Delete(folder);
    }
}

public sealed record SavesMove(InstallLayout From, IReadOnlyList<string> Added, IReadOnlyList<string> Taken,
    IReadOnlyDictionary<string, string> Renamed);
