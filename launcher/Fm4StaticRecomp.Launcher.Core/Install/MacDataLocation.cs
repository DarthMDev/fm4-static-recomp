// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
namespace Fm4StaticRecomp.Launcher.Core.Install;

public sealed class MacDataLocation(string supportDirectory)
{
    public string DefaultRoot => Path.Combine(supportDirectory, "data");
    private string Preference => Path.Combine(supportDirectory, "data-location");

    public static MacDataLocation Current => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "FM4 Static Recomp"));

    public static bool UsesApplicationSupport(string launcherDirectory, RootEnvironment environment)
    {
        var folder = InstallLayout.DataDirectory(launcherDirectory);
        return environment.Protected.Append(environment.Temp).Any(root => RootCheck.IsUnder(folder, root))
            || folder.Contains("/AppTranslocation/", StringComparison.Ordinal);
    }

    public string? Read()
    {
        try
        {
            var path = File.ReadAllText(Preference);
            return Path.IsPathFullyQualified(path) ? path : throw new IOException("The saved game data path is invalid.");
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void Save(string root)
    {
        Directory.CreateDirectory(supportDirectory);
        var temporary = Path.Combine(supportDirectory, $".data-location-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(temporary, root);
            File.Move(temporary, Preference, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public static bool ExternalVolumeMissing(string path)
    {
        const string volumes = "/Volumes/";
        if (!path.StartsWith(volumes, StringComparison.OrdinalIgnoreCase)) return false;
        var separator = path.IndexOf('/', volumes.Length);
        var volume = separator < 0 ? path : path[..separator];
        return !Directory.Exists(volume);
    }
}
