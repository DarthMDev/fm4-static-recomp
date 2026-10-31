// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
namespace Fm4StaticRecomp.Launcher.Core.Discs;

public abstract record Disc1Outcome
{
    public sealed record Installed(string ReleaseName, int ProblemsAccepted) : Disc1Outcome;
    public sealed record Refused(RequiredCheck Check) : Disc1Outcome;
    public sealed record Declined : Disc1Outcome;
}

public static class Disc1Step
{
    // Extracts into <game>.partial and renames it only when the player has accepted the
    // result, so an interrupted or declined install never looks installed.
    public static async Task<Disc1Outcome> InstallAsync(DiscInput input, DiscManifest manifest, string gameFolder,
        Func<IReadOnlyList<FileProblem>, Task<bool>> acceptProblems, IProgress<double>? progress,
        CancellationToken cancellation)
    {
        gameFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameFolder));
        using var files = input.OpenFiles();
        var check = await Disc1Installer.CheckRequiredAsync(files, manifest, input.HeaderMediaId, cancellation);
        if (!check.Matches) return new Disc1Outcome.Refused(check);
        var partial = gameFolder + ".partial";
        if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
        try
        {
            var problems = await Task.Run(() => Disc1Installer.ExtractAsync(files, manifest, check.Release!, partial, progress,
                cancellation), cancellation);
            if (problems.Count > 0 && !await acceptProblems(problems))
            {
                Directory.Delete(partial, recursive: true);
                return new Disc1Outcome.Declined();
            }
            cancellation.ThrowIfCancellationRequested();
            Swap(partial, gameFolder);
            return new Disc1Outcome.Installed(check.Release!.Name, problems.Count);
        }
        catch
        {
            if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
            throw;
        }
    }

    public static string RefusalText(RequiredCheck check, DiscManifest manifest)
    {
        if (manifest.ModRelease.Files.Any(f => f.Path == Disc1Installer.Executable &&
                                               f.Sha256.Equals(check.FoundSha256, StringComparison.OrdinalIgnoreCase)))
            return "This folder already has Project Forza Plus 4 over it. Choose the unmodified Disc 1 (its ISO image, " +
                   "GOD folder or extracted folder) here, and the mod's zip or folder separately.";
        var found = check.FoundSha256.Length > 0 ? check.FoundSha256 : "no default.xex";
        return check.Release is null
            ? $"This disc's media ID is {(check.MediaId.Length > 0 ? check.MediaId : "unknown")} and its default.xex has SHA-256 {found}; " +
              $"the launcher supports {string.Join(", ", manifest.DiscReleases.Select(r => r.Name))}."
            : $"{check.Release.Name}'s default.xex should have SHA-256 {check.ExpectedSha256}; this one has {found}.";
    }

    // The launcher calls this at start: a replacement cut short between its two renames
    // leaves the old game in <game>.old.
    public static void RecoverInterruptedReplace(string gameFolder)
    {
        var old = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameFolder)) + ".old";
        if (!Directory.Exists(old)) return;
        if (Directory.Exists(gameFolder)) DeleteQuietly(old);
        else Directory.Move(old, gameFolder);
    }

    // Renames instead of deleting first: a file held open by a virus scanner must not leave a
    // half-deleted game that setup-state.json still counts as installed.
    internal static void Swap(string partial, string gameFolder)
    {
        var old = gameFolder + ".old";
        if (Directory.Exists(gameFolder))
        {
            if (Directory.Exists(old)) Directory.Delete(old, recursive: true);
            Directory.Move(gameFolder, old);
        }
        try
        {
            Directory.Move(partial, gameFolder);
        }
        catch
        {
            if (Directory.Exists(old) && !Directory.Exists(gameFolder)) Directory.Move(old, gameFolder);
            throw;
        }
        DeleteQuietly(old);
    }

    private static void DeleteQuietly(string folder)
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
