namespace Fm4StaticRecomp.Launcher.Core.Profiles;

// A hint at the game a save folder ran, from the DLC folder Xenia keeps beside the saves
// (`<xuid>\4D530910\00000002`): Project Forza Plus 4 ships its DLC packs' `<n>_pri_65` archives
// extracted and deletes the zip, vanilla keeps the zips (pack 15's loose folder beside its zip
// included). Null when the folder holds no DLC to tell by.
public static class SaveOrigin
{
    public static string? Guess(string sourceStateOrUser)
    {
        var user = Directory.Exists(Path.Combine(sourceStateOrUser, "user")) ? Path.Combine(sourceStateOrUser, "user") : sourceStateOrUser;
        if (!Directory.Exists(user)) return null;
        var archives = Directory.GetDirectories(user)
            .Select(xuid => Path.Combine(xuid, "4D530910", "00000002"))
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Select(package => Path.Combine(package, "Media", "DLCZips"))
            .Where(Directory.Exists)
            .ToList();
        if (archives.Any(a => Directory.GetDirectories(a, "*_pri_65").Any(folder => !File.Exists(folder + ".zip"))))
            return ProfileStore.Pfp4;
        return archives.Any(a => Directory.GetFiles(a, "*_pri_65.zip").Length > 0) ? "vanilla" : null;
    }
}
