// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.IO.Compression;
using System.Text.RegularExpressions;
using Fm4StaticRecomp.Launcher.Core.Platform;
using Fm4StaticRecomp.Launcher.Core.Profiles;

namespace Fm4StaticRecomp.Launcher.Core.Updates;

public static class SaveBackup
{
    // The profile's own data only: the content link would add gigabytes of Disc 2.
    public static string Create(Profile profile, string backupsFolder, string version, IPlatform platform)
    {
        Directory.CreateDirectory(backupsFolder);
        var zip = Path.Combine(backupsFolder, $"{profile.Id}-{version}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            Add(archive, profile.User, "", platform);
        Prune(backupsFolder, profile.Id);
        return zip;
    }

    public static void Prune(string backupsFolder, string profileId, int keep = 3)
    {
        var mine = Directory.GetFiles(backupsFolder, "*.zip")
            .Where(f => IsBackupOf(Path.GetFileName(f), profileId))
            .OrderByDescending(File.GetLastWriteTimeUtc).ToList();
        foreach (var old in mine.Skip(keep)) File.Delete(old);
    }

    // Exact names, so profile "main" never claims the backups of a profile "main-2".
    public static bool IsBackupOf(string fileName, string profileId) =>
        Regex.IsMatch(fileName, $@"^{Regex.Escape(profileId)}-\d+\.\d+\.\d+-\d{{8}}-\d{{6}}\.zip$");

    private static void Add(ZipArchive archive, string folder, string prefix, IPlatform platform)
    {
        foreach (var file in Directory.GetFiles(folder))
            archive.CreateEntryFromFile(file, prefix + Path.GetFileName(file));
        foreach (var directory in Directory.GetDirectories(folder))
            if (!platform.IsDirectoryLink(directory))
                Add(archive, directory, prefix + Path.GetFileName(directory) + "/", platform);
    }
}
