using System.Text;
using Fm4StaticRecomp.Launcher.Core.Content;
using Fm4StaticRecomp.Launcher.Core.Install;
using Fm4StaticRecomp.Launcher.Core.Platform;
using Fm4StaticRecomp.Launcher.Core.Settings;
using Fm4StaticRecomp.Launcher.Core.Setup;

namespace Fm4StaticRecomp.Launcher.Core.Profiles;

public sealed class ProfileStore(InstallLayout layout, IPlatform platform, string installGame)
{
    // The title-wide (XUID 0) content folder the SDK installs Disc 2 and DLC into.
    public const string ContentFolderName = "0000000000000000";
    // The XUID the runtime signs in with; Project Forza Plus 4's Skip Intro saves were made with it too.
    public const string SaveXuid = "B13EBABEBABEBABE";
    public const string Pfp4 = "pfp4";
    private const string Vanilla = "vanilla";
    private const string FileName = "profile.toml";

    public IReadOnlyList<Profile> List() =>
        Directory.Exists(layout.Profiles)
            ? Directory.GetDirectories(layout.Profiles)
                .Where(d => File.Exists(Path.Combine(d, FileName)))
                .Select(Read).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
            : [];

    public Profile Create(string name)
    {
        var (mode, harderAi) = CurrentMode();
        return Create(name, mode, harderAi);
    }

    private Profile Create(string name, string? mode, bool? harderAi)
    {
        var id = UniqueId(name);
        var folder = Path.Combine(layout.Profiles, id);
        Directory.CreateDirectory(Path.Combine(folder, "user"));
        try
        {
            Write(folder, name.Trim(), GamertagFrom(name), null);
            Stamp(folder, mode, harderAi);
            LinkContent(folder);
        }
        catch
        {
            Directory.Delete(folder, recursive: true);
            throw;
        }
        return Read(folder);
    }

    public Profile Rename(string id, string name, string gamertag)
    {
        if (!IsValidGamertag(gamertag)) throw new ArgumentException($"{gamertag} is not a valid gamertag.");
        var profile = Get(id);
        Write(profile.Folder, name.Trim(), gamertag, profile.LastBackedUpVersion);
        return Read(profile.Folder);
    }

    public void MarkBackedUp(string id, string version)
    {
        var profile = Get(id);
        Write(profile.Folder, profile.Name, profile.Gamertag, version);
    }

    public void Delete(string id)
    {
        var profile = Get(id);
        Directory.CreateDirectory(layout.Deleted);
        Directory.Move(profile.Folder, Path.Combine(layout.Deleted, $"{id}-{DateTime.Now:yyyyMMdd-HHmmss}"));
    }

    // A new profile holding one of Project Forza Plus 4's Skip Intro saves (a cached
    // skip-intro folder), laid out as the game writes its own save, header included.
    public Profile CreateFromSkipIntro(string name, string saveFolder)
    {
        var profile = Create(name);
        try
        {
            var title = Path.Combine(profile.User, SaveXuid, "4D530910");
            CopyDirectory(saveFolder, Path.Combine(title, "00000001"));
            var headers = Path.Combine(title, "Headers", "00000001");
            Directory.CreateDirectory(headers);
            foreach (var content in Directory.GetDirectories(saveFolder).Select(d => Path.GetFileName(d)))
                File.WriteAllBytes(Path.Combine(headers, content + ".header"), ContentHeader.Build(ContentHeader.SaveGame, "User", content, null));
        }
        catch
        {
            Remove(profile);
            throw;
        }
        return profile;
    }

    public void RecordMode(string id, string mode, bool harderAi) => Stamp(Get(id).Folder, mode, harderAi);

    // Null when the profile's mode is unknown (an imported save) or the same as the install's.
    public static string? ModeWarning(Profile profile, string mode, bool harderAi)
    {
        if (profile.Pfp4Mode is null || (profile.Pfp4Mode == mode && (profile.Pfp4HarderAi ?? false) == harderAi)) return null;
        return $"This career was started in {Describe(profile.Pfp4Mode, profile.Pfp4HarderAi ?? false)}; this install now runs " +
               $"{Describe(mode, harderAi)}. The career loads and plays, but its events and prices follow the current mode.";
    }

    private static string Describe(string mode, bool harderAi) =>
        $"{char.ToUpperInvariant(mode[0])}{mode[1..]} mode{(harderAi ? " with Harder AI" : "")}";

    // The game of a source that is one of our profiles (its profile.toml, beside the user folder),
    // null for any other save folder. A profile without `game` comes from a vanilla install.
    public static string? StampedGame(string sourceStateOrUser) =>
        StampFile(sourceStateOrUser) is { } file ? TomlFile.Load(file).Get("game") ?? Vanilla : null;

    private static string? StampFile(string source)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        var own = Path.Combine(folder, FileName);
        if (File.Exists(own)) return own;
        var parent = Path.Combine(Path.GetDirectoryName(folder)!, FileName);
        return Path.GetFileName(folder).Equals("user", StringComparison.OrdinalIgnoreCase) && File.Exists(parent) ? parent : null;
    }

    // Copies a state's (or a user folder's) saves into a new profile. Installed content is
    // left behind: the profile links to the shared content instead. A source that is one of our
    // profiles is judged by its stamp; any other by the game the player declared.
    public Profile Import(string sourceStateOrUser, string name, string declaredGame)
    {
        var user = Directory.Exists(Path.Combine(sourceStateOrUser, "user"))
            ? Path.Combine(sourceStateOrUser, "user")
            : sourceStateOrUser;
        // The copy would run into the profile it creates.
        if (IsUnder(layout.Profiles, user))
            throw new IOException($"{sourceStateOrUser} holds this launcher's own profiles. Choose another profile's folder or an older state folder.");
        if (!HoldsSave(user))
            throw new IOException($"{sourceStateOrUser} holds no FM4 save. Choose a state folder or its user folder.");
        var stamp = StampFile(sourceStateOrUser) is { } stampFile ? Read(Path.GetDirectoryName(stampFile)!) : null;
        if ((stamp?.Game ?? declaredGame) != installGame)
            throw new IOException(installGame == Pfp4
                ? "This is a Project Forza Plus 4 install; vanilla saves cannot be used here. Import them into a vanilla install."
                : "This is a vanilla install; Project Forza Plus 4 saves cannot be used here. Import them into a Project Forza Plus 4 install.");
        var profile = Create(name, stamp?.Pfp4Mode, stamp?.Pfp4HarderAi);
        try
        {
            foreach (var directory in Directory.GetDirectories(user))
            {
                var folderName = Path.GetFileName(directory);
                if (folderName.Equals(ContentFolderName, StringComparison.OrdinalIgnoreCase)) continue;
                CopyDirectory(directory, Path.Combine(profile.User, folderName));
            }
            foreach (var file in Directory.GetFiles(user)) File.Copy(file, Path.Combine(profile.User, Path.GetFileName(file)));
        }
        catch
        {
            Remove(profile);
            throw;
        }
        return profile;
    }

    private static void Remove(Profile profile)
    {
        // The link first: a recursive delete does not remove a junction it meets.
        Directory.Delete(Path.Combine(profile.User, ContentFolderName));
        Directory.Delete(profile.Folder, recursive: true);
    }

    private (string? Mode, bool? HarderAi) CurrentMode()
    {
        if (installGame != Pfp4) return (null, null);
        var state = SetupState.Load(layout.SetupState);
        return (state.Pfp4Mode, state.Pfp4HarderAi);
    }

    // A save is a 16-digit XUID folder with FM4's title folder in it.
    private static bool HoldsSave(string user) =>
        Directory.Exists(user) && Directory.GetDirectories(user).Any(d =>
            Path.GetFileName(d) is { Length: 16 } xuid && xuid.All(char.IsAsciiHexDigit) &&
            !xuid.Equals(ContentFolderName, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(d, "4D530910")));

    private static bool IsUnder(string path, string folder)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        return (Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar)
            .StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidGamertag(string gamertag) =>
        gamertag.Length is >= 1 and <= 15 && char.IsAsciiLetter(gamertag[0]) &&
        gamertag.All(c => char.IsAsciiLetterOrDigit(c) || c == ' ');

    public Profile Get(string id) => List().SingleOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException(id);

    private void LinkContent(string folder)
    {
        Directory.CreateDirectory(layout.Content);
        platform.CreateDirectoryLink(Path.Combine(folder, "user", ContentFolderName), layout.Content);
    }

    private static Profile Read(string folder)
    {
        var file = TomlFile.Load(Path.Combine(folder, FileName));
        var id = Path.GetFileName(folder);
        return new Profile(id, folder, file.Get("name") ?? id, file.Get("gamertag") ?? "Player",
            file.Get("last_backed_up_version"), file.Get("game") ?? Vanilla, file.Get("pfp4_mode"),
            bool.TryParse(file.Get("pfp4_harder_ai"), out var harderAi) ? harderAi : null);
    }

    private void Stamp(string folder, string? mode, bool? harderAi)
    {
        var path = Path.Combine(folder, FileName);
        var file = TomlFile.Load(path);
        file.Set("game", installGame, quoted: true);
        if (mode is null) file.Remove("pfp4_mode");
        else file.Set("pfp4_mode", mode, quoted: true);
        if (harderAi is null) file.Remove("pfp4_harder_ai");
        else file.Set("pfp4_harder_ai", harderAi.Value ? "true" : "false", quoted: false);
        file.Save(path);
    }

    private static void Write(string folder, string name, string gamertag, string? backedUp)
    {
        var path = Path.Combine(folder, FileName);
        var file = TomlFile.Load(path);
        file.Set("name", name, quoted: true);
        file.Set("gamertag", gamertag, quoted: true);
        if (backedUp is not null) file.Set("last_backed_up_version", backedUp, quoted: true);
        file.Save(path);
    }

    private string UniqueId(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        var id = string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (id.Length == 0) id = "profile";
        var candidate = id;
        for (var n = 2; Directory.Exists(Path.Combine(layout.Profiles, candidate)); n++) candidate = $"{id}-{n}";
        return candidate;
    }

    internal static string GamertagFrom(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name)
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : ' ');
        var tag = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        tag = tag.Length > 15 ? tag[..15].TrimEnd() : tag;
        return IsValidGamertag(tag) ? tag : "Player";
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source))
            if (new DirectoryInfo(directory).LinkTarget is null)
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
}
