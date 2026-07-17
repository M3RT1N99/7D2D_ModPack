param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die",
    [switch]$UserMods
)

$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $modRoot "build.ps1") -GamePath $GamePath

if ($UserMods) {
    $modsPath = Join-Path $env:APPDATA "7DaysToDie\Mods"
} else {
    $modsPath = Join-Path $GamePath "Mods"
}

$target = Join-Path $modsPath "DeathSound"
New-Item -ItemType Directory -Force -Path $target | Out-Null

# Top-level files: copy straight into the target.
foreach ($file in @("ModInfo.xml", "DeathSound.dll")) {
    Copy-Item -LiteralPath (Join-Path $modRoot $file) -Destination (Join-Path $target $file) -Force
}

# Folders: mirror the *contents* into the destination folder. Copying the folder
# itself with -Recurse would nest it (Config\Config) whenever the target exists.
foreach ($dir in @("Config", "Audio")) {
    $destDir = Join-Path $target $dir
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Copy-Item -Path (Join-Path (Join-Path $modRoot $dir) "*") -Destination $destDir -Recurse -Force
}

Write-Host "Installed to $target"
