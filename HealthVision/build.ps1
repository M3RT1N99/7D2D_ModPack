param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
)

$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$managed = Join-Path $GamePath "7DaysToDie_Data\Managed"
$out = Join-Path $modRoot "HealthVision.dll"

if (!(Test-Path -LiteralPath (Join-Path $managed "Assembly-CSharp.dll"))) {
    throw "Assembly-CSharp.dll not found. Pass -GamePath with your 7 Days To Die folder."
}

$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
$sdkRoot = Join-Path $dotnetRoot "sdk"
$candidates = Get-ChildItem -LiteralPath $sdkRoot -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "Roslyn\bincore\csc.dll") } |
    Sort-Object Name -Descending

if (!$candidates) {
    throw "Could not find Roslyn csc.dll in dotnet SDK."
}

$csc = Join-Path $candidates[0].FullName "Roslyn\bincore\csc.dll"
$src = Get-ChildItem -LiteralPath (Join-Path $modRoot "src") -Recurse -Filter "*.cs" |
    ForEach-Object { $_.FullName }

$refs = @(
    "mscorlib.dll",
    "System.dll",
    "System.Core.dll",
    "System.Xml.dll",
    "netstandard.dll",
    "System.Runtime.dll",
    "Assembly-CSharp.dll",
    "Assembly-CSharp-firstpass.dll",
    "LogLibrary.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.TextRenderingModule.dll"
) | ForEach-Object { "/reference:" + (Join-Path $managed $_) }

& dotnet exec $csc `
    /noconfig `
    /nostdlib+ `
    /target:library `
    /optimize+ `
    /langversion:latest `
    /out:$out `
    @refs `
    @src

if ($LASTEXITCODE -ne 0) {
    throw "Compiler failed with exit code $LASTEXITCODE."
}

Write-Host "Built $out"
