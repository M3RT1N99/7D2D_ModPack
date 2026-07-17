using System.IO;
using System.Text.Json;

namespace ModManager;

// Persisted settings next to the exe. Ships with the correct repo baked in as defaults,
// but everything can be overridden via modmanager.config.json.
public sealed class AppConfig
{
    public const string DefaultOwner = "M3RT1N99";
    public const string DefaultRepo = "7D2D_ModPack";

    public string? gamePath { get; set; }
    public string owner { get; set; } = DefaultOwner;
    public string repo { get; set; } = DefaultRepo;
    public string? installedTag { get; set; }

    // AppContext.BaseDirectory (not Assembly.Location, which is empty in a single-file exe).
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "modmanager.config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                if (cfg != null)
                {
                    if (string.IsNullOrWhiteSpace(cfg.owner)) cfg.owner = DefaultOwner;
                    if (string.IsNullOrWhiteSpace(cfg.repo)) cfg.repo = DefaultRepo;
                    return cfg;
                }
            }
        }
        catch { /* fall through to defaults */ }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}
