using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace ModManager;

public sealed class MainForm : Form
{
    private readonly AppConfig _cfg = AppConfig.Load();

    private TextBox _gamePathBox = null!;
    private Label _statusLabel = null!;
    private Label _versionLabel = null!;
    private TextBox _log = null!;
    private Button _updateAndPlay = null!;
    private Button _updateOnly = null!;
    private Button _playOnly = null!;

    public MainForm()
    {
        Text = "7DTD Mod Updater";
        ClientSize = new Size(700, 500);
        MinimumSize = new Size(560, 380);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        BuildUi();

        var gp = _cfg.gamePath;
        if (!SteamLocator.IsValidGamePath(gp)) gp = SteamLocator.Detect();
        _gamePathBox.Text = gp ?? SteamLocator.DefaultGamePath;
        UpdatePathValidity();

        Log($"Repo: {_cfg.owner}/{_cfg.repo}");
        Log($"Installed pack: {_cfg.installedTag ?? "(none)"}");
    }

    private void BuildUi()
    {
        var pathLabel = new Label { Text = "Game folder:", Left = 12, Top = 16, Width = 78, AutoSize = false };

        _gamePathBox = new TextBox
        {
            Left = 92, Top = 13, Width = 470,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _gamePathBox.TextChanged += (_, _) => UpdatePathValidity();

        var browse = new Button
        {
            Text = "Browse…", Left = 568, Top = 12, Width = 100,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        browse.Click += (_, _) => BrowsePath();

        _statusLabel = new Label
        {
            Left = 92, Top = 40, Width = 576, AutoSize = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _versionLabel = new Label
        {
            Left = 12, Top = 62, Width = 656, AutoSize = false, ForeColor = Color.DimGray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _updateAndPlay = new Button
        {
            Text = "Update Mods && Start Game", Left = 12, Top = 86, Width = 330, Height = 46,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        _updateAndPlay.Click += async (_, _) => await RunAsync(update: true, play: true);

        _updateOnly = new Button { Text = "Update Only", Left = 350, Top = 86, Width = 150, Height = 46 };
        _updateOnly.Click += async (_, _) => await RunAsync(update: true, play: false);

        _playOnly = new Button
        {
            Text = "Start Game", Left = 508, Top = 86, Width = 160, Height = 46,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _playOnly.Click += async (_, _) => await RunAsync(update: false, play: true);

        _log = new TextBox
        {
            Left = 12, Top = 144, Width = 656, Height = 344,
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White, Font = new Font("Consolas", 9f),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.AddRange(new Control[]
        {
            pathLabel, _gamePathBox, browse, _statusLabel, _versionLabel,
            _updateAndPlay, _updateOnly, _playOnly, _log
        });
    }

    private void BrowsePath()
    {
        using var d = new FolderBrowserDialog { Description = "Select your '7 Days To Die' game folder" };
        if (Directory.Exists(_gamePathBox.Text)) d.SelectedPath = _gamePathBox.Text;
        if (d.ShowDialog(this) == DialogResult.OK) _gamePathBox.Text = d.SelectedPath;
    }

    private void UpdatePathValidity()
    {
        bool ok = SteamLocator.IsValidGamePath(_gamePathBox.Text);
        _statusLabel.Text = ok ? "Game folder OK  ✓" : "7DaysToDie.exe not found in this folder  ✗";
        _statusLabel.ForeColor = ok ? Color.SeaGreen : Color.Firebrick;
    }

    private void Log(string msg)
    {
        if (_log.InvokeRequired) { _log.BeginInvoke(() => Log(msg)); return; }
        _log.AppendText(msg + Environment.NewLine);
    }

    private void SetBusy(bool busy)
    {
        if (InvokeRequired) { BeginInvoke(() => SetBusy(busy)); return; }
        _updateAndPlay.Enabled = _updateOnly.Enabled = _playOnly.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private async Task RunAsync(bool update, bool play)
    {
        SetBusy(true);
        try
        {
            var gamePath = _gamePathBox.Text.Trim();
            if (!SteamLocator.IsValidGamePath(gamePath))
            {
                MessageBox.Show(this,
                    "The game folder is not valid (7DaysToDie.exe not found).\nUse Browse to pick your '7 Days To Die' folder.",
                    "7DTD Mod Updater", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _cfg.gamePath = gamePath;
            _cfg.Save();

            if (update)
            {
                if (IsGameRunning())
                {
                    var r = MessageBox.Show(this,
                        "7 Days To Die is running. Mod DLLs are locked and cannot be replaced.\n\nClose the game, then click OK to continue (or Cancel).",
                        "Game is running", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (r != DialogResult.OK) { Log("Update cancelled (game running)."); return; }
                }
                await Task.Run(() => UpdateModsAsync(gamePath));
            }

            if (play) LaunchGame(gamePath);
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "7DTD Mod Updater — error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static bool IsGameRunning() =>
        Process.GetProcessesByName("7DaysToDie").Length > 0 ||
        Process.GetProcessesByName("7DaysToDie_EAC").Length > 0;

    private async Task UpdateModsAsync(string gamePath)
    {
        Log("Checking latest release…");
        var rel = await GitHubClient.GetLatestAsync(_cfg.owner, _cfg.repo);
        Log($"Latest pack: {rel.Tag}");
        UpdateVersionLabel(rel.Tag);

        if (!string.IsNullOrEmpty(rel.Tag) && rel.Tag == _cfg.installedTag)
        {
            Log("Already up to date.");
            return;
        }

        var tmpZip = Path.Combine(Path.GetTempPath(), "7dtd_mods_" + Guid.NewGuid().ToString("N") + ".zip");
        Log("Downloading mods…");
        await GitHubClient.DownloadAsync(rel.ZipUrl, tmpZip);

        var modsDir = Path.Combine(gamePath, "Mods");
        Directory.CreateDirectory(modsDir);

        var folders = TopLevelFolders(tmpZip);
        Log($"Installing {folders.Count} mod folder(s) into {modsDir} …");
        foreach (var f in folders)
        {
            var target = Path.Combine(modsDir, f);
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
        ZipFile.ExtractToDirectory(tmpZip, modsDir, overwriteFiles: true);
        try { File.Delete(tmpZip); } catch { /* temp cleanup best-effort */ }

        _cfg.installedTag = rel.Tag;
        _cfg.Save();
        Log($"Installed: {string.Join(", ", folders)}  (pack {rel.Tag})");
    }

    private static List<string> TopLevelFolders(string zipPath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var z = ZipFile.OpenRead(zipPath);
        foreach (var e in z.Entries)
        {
            var name = e.FullName.Replace('\\', '/');
            var idx = name.IndexOf('/');
            if (idx > 0) set.Add(name.Substring(0, idx));
        }
        return set.ToList();
    }

    private void LaunchGame(string gamePath)
    {
        var exe = Path.Combine(gamePath, "7DaysToDie.exe"); // direct = EAC OFF (required for DLL mods)
        if (!File.Exists(exe)) throw new FileNotFoundException("7DaysToDie.exe not found in " + gamePath);
        Log("Launching 7 Days To Die (EAC off)…");
        Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = gamePath, UseShellExecute = true });
    }

    private void UpdateVersionLabel(string latest)
    {
        if (InvokeRequired) { BeginInvoke(() => UpdateVersionLabel(latest)); return; }
        _versionLabel.Text = $"Installed: {_cfg.installedTag ?? "(none)"}    |    Latest: {latest}";
    }
}
