Custom item icons
=================

7 Days To Die automatically loads PNG files placed in this folder
(<mod>/UIAtlases/ItemIconAtlas/) into the item icon atlas. The sprite name is the
file name without extension.

To give the mod's items their own icons:

1. Create/obtain square PNGs (transparent background), ideally 160x160 px:
     BombVestDetonator.png
     BombVest.png
2. Put them in this folder.
3. In Config/items.xml, DELETE the <property name="CustomIcon" .../> line for that item
   (or set its value to the PNG name). With the CustomIcon override removed, the item
   falls back to a sprite matching its own name, which your PNG now provides.
4. Restart the game.

Until you add PNGs here, the items reuse existing vanilla icons via CustomIcon
(flashlight icon for the detonator, commando-armor icon for the vest) so nothing is
missing.

Licensing: only use images you created or that are licensed for reuse/redistribution
(e.g. CC0 or CC-BY with credit). Do not use ripped/copyrighted icons.
