using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ModManager;

// Finds the 7 Days To Die install folder via Steam (registry + libraryfolders.vdf).
public static class SteamLocator
{
    private const string AppId = "251570";
    private const string GameSubPath = @"steamapps\common\7 Days To Die";
    public const string DefaultGamePath = @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die";

    public static bool IsValidGamePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "7DaysToDie.exe"));

    public static string? Detect()
    {
        try
        {
            var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steam))
            {
                steam = steam.Replace('/', '\\');

                var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    var lib = FindLibraryWithApp(File.ReadAllText(vdf), AppId);
                    if (lib != null)
                    {
                        var game = Path.Combine(lib, GameSubPath);
                        if (IsValidGamePath(game)) return game;
                    }
                }

                // Fallback: the game in Steam's own library.
                var main = Path.Combine(steam, GameSubPath);
                if (IsValidGamePath(main)) return main;
            }
        }
        catch { /* ignore and try the default path */ }

        return IsValidGamePath(DefaultGamePath) ? DefaultGamePath : null;
    }

    // libraryfolders.vdf lists blocks each with a "path" and an "apps" set. Return the
    // "path" of the block whose apps contain the given appId.
    private static string? FindLibraryWithApp(string vdf, string appId)
    {
        var paths = Regex.Matches(vdf, "\"path\"\\s*\"([^\"]+)\"");
        for (int i = 0; i < paths.Count; i++)
        {
            int start = paths[i].Index;
            int end = (i + 1 < paths.Count) ? paths[i + 1].Index : vdf.Length;
            var chunk = vdf.Substring(start, end - start);
            if (Regex.IsMatch(chunk, "\"" + Regex.Escape(appId) + "\""))
                return paths[i].Groups[1].Value.Replace("\\\\", "\\");
        }
        return null;
    }
}
