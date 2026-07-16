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

foreach ($entry in @("ModInfo.xml", "DeathSound.dll", "Config", "Audio")) {
    $source = Join-Path $modRoot $entry
    $dest = Join-Path $target $entry
    Copy-Item -LiteralPath $source -Destination $dest -Recurse -Force
}

Write-Host "Installed to $target"
