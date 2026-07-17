param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die",
    [switch]$UserMods
)

# Builds and installs EVERY mod in this repo (each subfolder that has its own
# install.ps1 + ModInfo.xml). New mods are picked up automatically.
#
#   .\install-all-mods.ps1                 # install into <GamePath>\Mods
#   .\install-all-mods.ps1 -UserMods       # install into %APPDATA%\7DaysToDie\Mods
#   .\install-all-mods.ps1 -GamePath "D:\Games\7 Days To Die"

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (Get-Process -Name "7DaysToDie*" -ErrorAction SilentlyContinue) {
    Write-Warning "7 Days To Die is running. Loaded mod DLLs are locked, so their copy will fail. Close the game first (mods only load at startup anyway)."
}

$mods = Get-ChildItem -LiteralPath $root -Directory |
    Where-Object {
        (Test-Path -LiteralPath (Join-Path $_.FullName "install.ps1")) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName "ModInfo.xml"))
    } |
    Sort-Object Name

if (!$mods) {
    throw "No mods found (looked for subfolders containing install.ps1 + ModInfo.xml)."
}

Write-Host ("Installing {0} mod(s): {1}" -f $mods.Count, ($mods.Name -join ", "))

$results = @()
foreach ($mod in $mods) {
    Write-Host ""
    Write-Host ("=== {0} ===" -f $mod.Name)
    try {
        $modArgs = @{ GamePath = $GamePath }
        if ($UserMods) { $modArgs.UserMods = $true }
        & (Join-Path $mod.FullName "install.ps1") @modArgs
        $results += [pscustomobject]@{ Mod = $mod.Name; Status = "OK" }
    }
    catch {
        Write-Warning ("{0} failed: {1}" -f $mod.Name, $_.Exception.Message)
        $results += [pscustomobject]@{ Mod = $mod.Name; Status = ("FAILED: " + $_.Exception.Message) }
    }
}

Write-Host ""
Write-Host "=== Summary ==="
$results | Format-Table -AutoSize | Out-String | Write-Host

if ($results.Status -contains "OK" -and -not ($results.Status -match "FAILED")) {
    Write-Host "All mods installed. Restart 7 Days To Die (with EAC disabled) to load them."
}
