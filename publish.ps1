param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die",
    [string]$Repo = "",
    [ValidateSet("", "patch", "minor", "major")]
    [string]$Bump = "",
    [switch]$Clobber,
    [switch]$DryRun,
    [string]$Notes = "",
    [string]$NotesFile = ""
)

# Builds every mod in this repo (using the LOCAL game DLLs), packages them, and publishes
# a public GitHub Release (mods.zip + manifest.json) that the Mod Updater downloads.
#
#   .\publish.ps1 -DryRun                 # build + stage + zip, no release (test)
#   .\publish.ps1                         # publish current VERSION as a release
#   .\publish.ps1 -Bump patch             # bump VERSION, then publish
#   .\publish.ps1 -Clobber                # overwrite an existing release of the same version

$ErrorActionPreference = "Stop"
# Native (gh) non-zero exits should NOT throw; we check $LASTEXITCODE ourselves.
$PSNativeCommandUseErrorActionPreference = $false
$root = $PSScriptRoot

# --- pack version (VERSION is the source of truth) ---
$versionFile = Join-Path $root "VERSION"
if (!(Test-Path -LiteralPath $versionFile)) { throw "VERSION file missing at $versionFile" }
$version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ($Bump) {
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION '$version' is not Major.Minor.Patch" }
    $p = $version.Split('.') | ForEach-Object { [int]$_ }
    switch ($Bump) {
        "major" { $p[0]++; $p[1] = 0; $p[2] = 0 }
        "minor" { $p[1]++; $p[2] = 0 }
        "patch" { $p[2]++ }
    }
    $version = "$($p[0]).$($p[1]).$($p[2])"
    Set-Content -LiteralPath $versionFile -Value $version -NoNewline
    Write-Host "Bumped version -> $version"
}
$tag = "v$version"

if (Get-Process -Name "7DaysToDie*" -ErrorAction SilentlyContinue) {
    Write-Warning "7 Days To Die is running. Building is fine, but players must close it before installing (locked DLLs)."
}

# --- discover mods (same predicate as install-all-mods.ps1) ---
$mods = Get-ChildItem -LiteralPath $root -Directory |
    Where-Object {
        (Test-Path -LiteralPath (Join-Path $_.FullName "install.ps1")) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName "ModInfo.xml"))
    } |
    Sort-Object Name
if (!$mods) { throw "No mods found (subfolders with install.ps1 + ModInfo.xml)." }

# --- fresh staging ---
$dist = Join-Path $root "dist"
$stage = Join-Path $dist "mods"
if (Test-Path -LiteralPath $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

$manifestMods = @()
foreach ($mod in $mods) {
    $folder = $mod.Name
    Write-Host ""
    Write-Host "=== Building $folder ==="
    & (Join-Path $mod.FullName "build.ps1") -GamePath $GamePath

    $dll = "$folder.dll"
    $dllPath = Join-Path $mod.FullName $dll
    if (!(Test-Path -LiteralPath $dllPath)) { throw "Expected $dll not found after building $folder." }

    [xml]$mi = Get-Content -LiteralPath (Join-Path $mod.FullName "ModInfo.xml")
    $modVersion = $mi.xml.Version.value
    $modName = $mi.xml.Name.value

    $target = Join-Path $stage $folder
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -LiteralPath (Join-Path $mod.FullName "ModInfo.xml") -Destination $target -Force
    Copy-Item -LiteralPath $dllPath -Destination $target -Force
    foreach ($d in @("Config", "Audio", "UIAtlases", "Resources")) {
        $srcDir = Join-Path $mod.FullName $d
        if (Test-Path -LiteralPath $srcDir) {
            Copy-Item -LiteralPath $srcDir -Destination $target -Recurse -Force
        }
    }

    $manifestMods += [pscustomobject]@{ folder = $folder; name = $modName; version = $modVersion; dll = $dll }
    Write-Host "  staged $folder (mod v$modVersion)"
}

# --- manifest ---
$manifest = [pscustomobject]@{
    packVersion  = $version
    tag          = $tag
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    asset        = "mods.zip"
    mods         = @($manifestMods)
}
$manifestPath = Join-Path $dist "manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

# --- zip (archive root = the mod folders, ready to extract into <game>\Mods) ---
$zipPath = Join-Path $dist "mods.zip"
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Packed $($mods.Count) mod(s) -> $zipPath (pack $version)"

if ($DryRun) {
    Write-Host "DryRun: skipping GitHub release."
    return
}

# --- resolve repo ---
if (!$Repo) {
    $Repo = (gh repo view --json nameWithOwner -q .nameWithOwner)
    if ($LASTEXITCODE -ne 0 -or !$Repo) { throw "Could not resolve repo; pass -Repo owner/name." }
    $Repo = $Repo.Trim()
}

# --- release (create or clobber) ---
gh release view $tag -R $Repo *> $null
$exists = ($LASTEXITCODE -eq 0)
if ($exists) {
    if ($Clobber) {
        Write-Host "Release $tag exists -> deleting (clobber)."
        gh release delete $tag -R $Repo --yes --cleanup-tag
        if ($LASTEXITCODE -ne 0) { throw "Failed to delete existing release $tag." }
    }
    else {
        throw "Release $tag already exists. Re-run with -Clobber (overwrite) or -Bump patch (new version)."
    }
}

$createArgs = @($tag, $zipPath, $manifestPath, "-R", $Repo, "-t", "Mod pack $version", "--latest", "--target", "main")
if ($NotesFile) { $createArgs += @("-F", $NotesFile) }
elseif ($Notes) { $createArgs += @("-n", $Notes) }
else { $createArgs += @("-n", "Mod pack $version. Install/update with the 7DTD Mod Updater (see README).") }

gh release create @createArgs
if ($LASTEXITCODE -ne 0) { throw "gh release create failed." }

Write-Host ""
Write-Host "Released $tag to $Repo"
gh release view $tag -R $Repo --json url -q .url
