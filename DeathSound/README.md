# DeathSound

7 Days to Die C# mod for 3.x-style ModAPI installs. It plays a local audio file when a player dies.

## Use

1. Put your file at `DeathSound/Audio/death.mp3`.
2. Or edit `DeathSound/Config/DeathSound.xml` and point `AudioFile` to another mp3, ogg, wav, or aiff.
3. Run `.\DeathSound\install.ps1` from PowerShell.
4. Start 7 Days to Die with EAC disabled when loading client-side DLL mods.

The default config plays on any player death. Set `PlayForRemotePlayers` to `false` to play only when your own local player dies.

If the file is missing, the mod plays a short generated test tone so you can see that the death hook works.

## Rebuild after game updates

The DLL is compiled against the assemblies in your local game folder. After updating 7DTD, run:

```powershell
.\DeathSound\build.ps1
```

If your game is not in the default Steam folder:

```powershell
.\DeathSound\install.ps1 -GamePath "D:\SteamLibrary\steamapps\common\7 Days To Die"
```

## Install location

By default `install.ps1` installs into the game folder:

```text
<7 Days To Die>\Mods\DeathSound
```

For the per-user mods folder instead:

```powershell
.\DeathSound\install.ps1 -UserMods
```
