# 7 Days To Die — Mod Pack

Custom C# mods for **7 Days To Die**, plus a one‑click **Mod Updater** so you and your friends always run the exact same version.

## Mods

| Mod | What it does |
|-----|--------------|
| **DeathSound** | A "commander‑style" explosion with a custom sound. Craft a **C4 bomb vest** + **detonator**: wear the vest, click the detonator, and after a short delay it detonates — a big composite fireball, two‑tier area damage (inner fireball / outer shockwave), and a block crater. The sound plays instantly on trigger. Fully configurable; damage is server‑authoritative and the visual/sound are replicated to all clients. |
| **HealthVision** | Wearable **goggles** (Scavenger cap + goggles). While worn, a health bar + number floats over every nearby living entity (zombies, animals, players). Pure client‑side overlay; range/size/text are configurable. |

## For players — install & update (the easy way)

1. Download **`7DTD Mod Updater.exe`** from the [latest Release](../../releases/latest).
2. Run it — it auto‑detects your *7 Days To Die* folder (Browse if needed).
3. Click **Update Mods & Start Game**.

It downloads the current mods, installs them into `…\7 Days To Die\Mods\`, and launches the game **with EasyAntiCheat OFF** (required for code mods). Buttons for *Update Only* and *Start Game* are there too.

> ⚠️ **Multiplayer:** the server **and every client** must run the **same mod version**, or joining fails with `Unknown package type …, can not proceed connecting to server`. The updater keeps everyone in sync. **EAC must be off** on the server and all clients (DLL mods can't load with EAC on).

## For the maintainer — build & publish

Requires the game installed locally, the **.NET SDK**, and the **`gh`** CLI (authenticated).

```powershell
# Build + install every mod into your own game (for testing)
.\install-all-mods.ps1

# Cut a new mod-pack release (built LOCALLY with your game DLLs, uploaded to GitHub)
.\publish.ps1 -Bump patch        # bump the pack version, then publish a Release
.\publish.ps1 -DryRun            # build + stage + zip only (no release)
.\publish.ps1 -Clobber           # re-publish the current version (overwrite)

# Build the updater .exe (single self-contained file, no .NET runtime needed by friends)
dotnet publish .\ModManager\ModManager.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
# -> ModManager\bin\Release\net9.0-windows\win-x64\publish\7DTD Mod Updater.exe   (attach to a Release)
```

## Why no GitHub cloud build?

The mods compile against the game's proprietary `Assembly-CSharp.dll` (and Unity modules), which must **not** be committed to a repo. So building happens **locally** (each mod's `build.ps1` uses your installed game), and `publish.ps1` uploads only the finished mods to a public Release. No copyrighted game files ever touch the repo.

## Repo layout

```
DeathSound/          # mod — explosion / detonator / C4 vest + custom sound
HealthVision/        # mod — health-bar goggles
ModManager/          # C# WinForms "7DTD Mod Updater" (the .exe)
publish.ps1          # build all mods locally -> create a GitHub Release
install-all-mods.ps1 # build + install all mods into the local game
VERSION              # mod-pack version (source of truth for release tags)
CLAUDE.md            # 7DTD modding guide (hard-won notes)
```

## Notes

- Built DLLs are gitignored — players get them from Releases, not the repo.
- The game's DLLs are reference‑only and never distributed here; install *7 Days To Die* to build.
