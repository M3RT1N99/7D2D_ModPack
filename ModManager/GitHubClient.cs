using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace ModManager;

public sealed record ReleaseInfo(string Tag, string ZipUrl, string? ManifestUrl);

// Reads the latest public GitHub Release and downloads its mods.zip. No auth/token needed.
public static class GitHubClient
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var h = new HttpClient();
        h.DefaultRequestHeaders.UserAgent.ParseAdd("7DTD-Mod-Updater/1.0"); // GitHub requires a User-Agent
        h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        h.Timeout = TimeSpan.FromMinutes(5);
        return h;
    }

    public static async Task<ReleaseInfo> GetLatestAsync(string owner, string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var json = await Http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        string? zip = null, manifest = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var dl = a.GetProperty("browser_download_url").GetString();
                if (name == "mods.zip") zip = dl;
                else if (name == "manifest.json") manifest = dl;
            }
        }

        if (string.IsNullOrEmpty(zip))
            throw new Exception("The latest release has no 'mods.zip' asset.");

        return new ReleaseInfo(tag, zip!, manifest);
    }

    public static async Task DownloadAsync(string url, string destPath)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        await using var src = await resp.Content.ReadAsStreamAsync();
        await using var dst = File.Create(destPath);
        await src.CopyToAsync(dst);
    }
}
