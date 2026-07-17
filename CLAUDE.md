# 7 Days To Die — C# Modding Guide

Practical, battle-tested notes for building 7 Days To Die (7DTD) code mods in this repo.
Worked example: the `DeathSound/` mod. Everything here was verified against the actual
game assembly/config during development — **prefer verifying over guessing** (see §3).

---

## 0. Environment (this machine)

| Thing | Value |
|---|---|
| Game dir | `C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die` |
| Managed DLLs | `<GameDir>\7DaysToDie_Data\Managed\` (Assembly-CSharp.dll etc.) |
| Game XML config | `<GameDir>\Data\Config\` (items.xml, recipes.xml, …) |
| **Active mods folder** | `<GameDir>\Mods\` — **the game loads from here** (confirm in the log!) |
| Logs | `%APPDATA%\7DaysToDie\logs\output_log_client__*.txt` (newest) |
| **Unity version** | **2022.3.62f2** — required for building AssetBundles |
| Decompiler | `ilspycmd` (dotnet global tool) |

---

## 1. What a mod is

A folder under `<GameDir>\Mods\<ModName>\` containing:

```
ModInfo.xml                       manifest (required)
<ModName>.dll                     compiled C# assembly (for code mods)
Config/
  items.xml, recipes.xml, ...     XPath PATCHES to same-named game XML (auto-applied)
  <ModName>.xml                   your OWN config (must NOT match a game xml name)
Audio/                            .wav/.ogg/.mp3 ...
UIAtlases/ItemIconAtlas/          custom item icons (<name>.png)
Resources/                        Unity AssetBundles (custom models)
```

**Key rule:** a `Config/*.xml` whose name matches a game XML (`items.xml`, `recipes.xml`,
`buffs.xml`, …) is auto-applied as an XPath patch. A file with a *non-game* name (e.g.
`DeathSound.xml`) is ignored by the patcher — read it yourself with `XmlDocument`.

---

## 2. Build (`build.ps1`)

Compile with Roslyn (`csc.dll` from the dotnet SDK), targeting a library, referencing the
game's Managed DLLs. Essentials:

```
dotnet exec <sdk>/Roslyn/bincore/csc.dll /noconfig /nostdlib+ /target:library
  /optimize+ /langversion:latest /out:<ModName>.dll @refs @src
```

Reference these from `<GameDir>\7DaysToDie_Data\Managed\`:
`mscorlib, System, System.Core, System.Xml, netstandard, System.Runtime,
Assembly-CSharp, Assembly-CSharp-firstpass, LogLibrary, UnityEngine` and the **Unity
module DLLs you actually use**, e.g.:
`UnityEngine.CoreModule, UnityEngine.AudioModule, UnityEngine.ParticleSystemModule,
UnityEngine.UnityWebRequestModule, UnityEngine.UnityWebRequestAudioModule`
(+ `Mods\0_TFP_Harmony\0Harmony.dll` if you use Harmony).

**Gotcha — CS1069 "type forwarded to assembly X":** a Unity type lives in a module DLL you
haven't referenced. The error names the assembly — add that `UnityEngine.*Module.dll`.
Example this session: `ParticleSystem` → add `UnityEngine.ParticleSystemModule.dll`.

---

## 3. THE GOLDEN RULE: decompile, don't fabricate

Most bugs here come from inventing API that doesn't exist (the original code called a
made-up `UnityLoader.LoadAssetBundle`). Always verify names/signatures against the real
assembly.

```bash
# one-time (latest 10.x had a packaging bug on .NET 9; pin a 9.x):
dotnet tool install --global ilspycmd --version 9.1.0.7988
export PATH="$PATH:$HOME/.dotnet/tools"

# dump a type (searches the whole assembly by name):
ilspycmd "<GameDir>/7DaysToDie_Data/Managed/Assembly-CSharp.dll" -t GameManager
```

Also `grep`/read the game's `Data/Config/*.xml` for real item/recipe/property names and
examples. Every type, method, field, item name, and ingredient you use must be one you
actually located.

---

## 4. Code entry point

```csharp
public sealed class ModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        // modInstance.Path = this mod's folder
        ModEvents.EntityKilled.RegisterHandler(OnEntityKilled);
        Log.Out("[MyMod] loaded");
    }
}
```

- `ModEvents.*` — game events (EntityKilled, GameStartDone, PlayerSpawnedInWorld, …).
  `EntityKilled` fires **on the server**.
- Logging: `Log.Out / Log.Warning / Log.Error` → the output_log.

---

## 5. Deploy

Copy the mod folder into `<GameDir>\Mods\<ModName>\`. See `DeathSound/install.ps1`.

**Multiple mods in this repo.** Each mod is its own folder with its own `build.ps1`
(compile) and `install.ps1` (build + copy). Install one with its own script, or all at
once from the repo root:

```
.\install-all-mods.ps1              # build + install every mod into <GameDir>\Mods
.\install-all-mods.ps1 -UserMods    # ... into %APPDATA%\7DaysToDie\Mods
.\install-all-mods.ps1 -GamePath "D:\Games\7 Days To Die"
```

`install-all-mods.ps1` auto-discovers every subfolder that has an `install.ps1` +
`ModInfo.xml` (new mods are picked up automatically) and prints a per-mod OK/FAILED
summary. Each mod's own `install.ps1` also accepts `-GamePath` / `-UserMods`.

**Gotchas (all hit this session):**
- **Verify the load path from the log**, don't assume. Look for
  `[MODS] Loaded assembly <Mod> (in <path>)`. On this machine it's the Steam `Mods\`
  folder; `%APPDATA%\7DaysToDie\Mods\` was NOT the one being loaded.
- **The DLL is locked while the game runs** → close the game before copying. Mods load
  only at **startup**, so restart to apply any change.
- **PowerShell folder-copy nesting:** `Copy-Item -Recurse Config <target>\Config` nests to
  `Config\Config` when the target already exists. Copy the *contents*:
  `Copy-Item (Join-Path Config '*') -Destination <target>\Config -Recurse -Force`.
- Keep the built `.dll` out of git (`.gitignore` must use a **forward slash**:
  `MyMod/MyMod.dll` — a backslash is treated as an escape and won't match).

---

## 6. Key APIs & gotchas

### Floating Origin — world vs render coordinates ⚠️
`entity.transform.position` is **render space** (offset by `Origin.position`).
The authoritative **world** position is `entity.position`. All world logic (explosions,
damage, `World.worldToBlockPos`) uses world coords. Using `transform.position` put a blast
~750 m off. **Use `entity.position`.**

### Explosions
```csharp
GameManager.Instance.ExplosionServer(
    worldPos, World.worldToBlockPos(worldPos), Quaternion.identity,
    explosionData, /*attackerEntityId*/ -1, /*delaySec*/ 2f, /*removeBlock*/ false);
```
- Handles the delay + client/server networking internally.
- `ExplosionData` is a struct of public fields; `new ExplosionData()` zero-inits — but
  **`BlockTags` must be set to `string.Empty`** (AttackBlocks reads `.Length` → NPE if null).
- `ParticleIndex` indexes `WorldStaticData.prefabExplosions[0..99]` (0 = no visual).
- It only has ONE radius with linear falloff — for custom tiers, do entity damage yourself.

### Entity damage (server-authoritative)
```csharp
var src = new DamageSource(EnumDamageSource.External, EnumDamageTypes.Heat);
// snapshot: DamageEntity can kill/remove entities mid-iteration
foreach (var e in new List<Entity>(world.Entities.list)) {
    if (e is EntityAlive a && !a.IsDead()
        && Vector3.Distance(a.position, center) <= radius)
        a.DamageEntity(src, Mathf.CeilToInt(a.GetMaxHealth() * frac), false, 1f);
}
```

### Server authority & multiplayer
World changes must run on the server:
`SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer`.
To show a *client-side* effect on everyone, define a custom `NetPackage` — 7DTD
auto-discovers subclasses in mod assemblies
(`ReflectionHelpers.FindTypesImplementingBase` over `ModManager.GetLoadedAssemblies()`).

```csharp
[UnityEngine.Scripting.Preserve]
public class NetPackageX : NetPackage {
    Vector3 pos;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient; // or ToServer
    public NetPackageX Setup(Vector3 p){ pos = p; return this; }
    public override void read(PooledBinaryReader r){ pos = StreamUtils.ReadVector3(r); }
    public override void write(PooledBinaryWriter w){ base.write(w); StreamUtils.Write(w, pos); }
    public override void ProcessPackage(World w, GameManager gm){ /* runs on receiver */ }
    public override int GetLength() => 32;
}
// send:  ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageX>().Setup(pos));
//        ConnectionManager.Instance.SendToServer(...)   // client -> server
```
Pattern: server applies damage + spawns visual locally + broadcasts the package so remote
clients spawn the visual too. Template to copy: `NetPackageExplosionClient`.

### Custom items (XML patch)
```xml
<config>
  <append xpath="/items">
    <item name="myItem">
      <property name="Extends" value="meleeToolFlashlight02"/>  <!-- inherit mesh/slot/sounds -->
      <property name="CustomIcon" value="meleeToolFlashlight02"/>
      <property name="CreativeMode" value="Player"/>
      <property class="Action0">
        <property name="Class" value="MyNamespace.ItemActionMyThing, MyModDll"/>
      </property>
    </item>
  </append>
</config>
```
`Extends` inherits a base item; redefining a property/action overrides it.

**⚠️ XML comment gotcha (a whole patch file silently fails to load):** an XML comment
must NOT contain a double dash `--` (and must not end a comment line with `-`). 7DTD's
`XmlPatcher` parses each `Config/*.xml` with `XDocument.Parse`, which throws
`An XML comment cannot contain '--'` and then **rejects the entire file** — so none of
your items/recipes get added and `giveself <name>` reports "item not found". Use single
dashes or `=` for separators inside `<!-- ... -->` (e.g. `==== Section ====`), never `--`.
Confirm in the log: `ERR XML loader: Loading XML patch file '<file>' from mod '<mod>' failed:`
followed by the `EXC An XML comment cannot contain '--' ...` line.

### Custom item actions (C#)
Extend `ItemAction`; the **only** abstract member is `ExecuteAction`:
```csharp
[UnityEngine.Scripting.Preserve]
public class ItemActionMyThing : ItemAction {
    public override void ExecuteAction(ItemActionData d, bool released) {
        if (released || d?.invData == null) return;
        EntityAlive holder = d.invData.holdingEntity;   // the player using it
        // ... do the thing (guard IsServer / send a NetPackage for MP) ...
    }
}
```
XML `Class` value → resolved by `ReflectionHelpers.GetTypeWithPrefix("ItemAction", value)`,
which falls back to `Type.GetType(value)`. So use the **assembly-qualified name**:
`"MyNamespace.ItemActionMyThing, MyModDll"`. Add `[Preserve]`.

### Equipment / armor
```csharp
Equipment eq = entityAlive.equipment;            // public field
for (int i = 0; i < eq.GetSlotCount(); i++) {
    ItemValue iv = eq.GetSlotItem(i);            // null/empty when nothing
    if (iv != null && !iv.IsEmpty() && iv.ItemClass?.GetItemName() == "myArmor") { /*...*/ }
}
eq.SetSlotItem(i, null);                          // unequip + destroy (syncs in MP)
```
Slots: Head=0, Chest=1, Hands=2, Feet=3. Armor item = `Class=ItemClassArmor` +
`EquipSlot=Chest` + armor tags + an SDCS worn-prefab (extend e.g. `armorCommandoOutfit`).

### Icons
PNGs in `<mod>/UIAtlases/ItemIconAtlas/<name>.png` (sprite name = filename, ~160×160).
If an item shows a **missing icon**, it's because the icon defaults to the item's own name
which isn't in the atlas — either add `<name>.png` or set `CustomIcon` to a vanilla sprite
(an existing item name). Make sure `install.ps1` copies the `UIAtlases` folder.

### Custom 3D models (advanced)
7DTD loads models as **Unity AssetBundles built with Unity 2022.3.62f2**. Raw `.fbx` is NOT
loadable directly. Pipeline: `.fbx` + textures → Unity → prefab+material → build AssetBundle
→ `Mods/<Mod>/Resources/` → reference via
`Meshfile = "#@modfolder:Resources/<bundle>?<PrefabName>"`.
Held items = static mesh (easy). Worn armor must be **skinned to the player rig** (hard) —
otherwise extend a vanilla armor for the worn look.
Asset licensing: only CC0 / CC-BY (credit) / purchased-with-redistribution models — never
ripped/other-game assets.

---

## 7. Debugging & testing

- **Log first.** Newest `output_log_client__*.txt`; grep for your `[MyMod]` tag, `ERR`,
  `WRN`, `Exception`, `NullReference`, and `[MODS] Loaded assembly`.
- **Spawn items:** console (`F1`) → `giveself <itemName>` (case-sensitive; works without
  cheat mode). Or Creative menu (`U`, needs dev/creative mode); `CreativeMode=Player` makes
  the item appear there.
- `Initialize engine version:` (log line 6) tells you the Unity version for AssetBundles.
- If `giveself` says "item not found" or the item is missing → the XML patch didn't load;
  check the log around item loading for the error.

---

## 8. Workflow checklist

1. Decompile/verify the exact API against `Assembly-CSharp.dll` (§3).
2. Write code / XML using real names only.
3. `build.ps1` — fix CS errors (esp. CS1069 → add module ref).
4. Close the game (DLL lock), deploy to `<GameDir>\Mods\<Mod>\` (§5).
5. Restart the game; test; read the log; iterate.
