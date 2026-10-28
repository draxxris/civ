# Rendering reference

The user supplied `civ6-rivers.jpg` as a visual reference for river rendering. The image
is a Civilization VI screenshot from the game's Steam store page.

Source:
<https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/289070/ss_7f598198526afc260d939a98af4d76d95f5349e4.jpg>

The game does not load this reference image or copy assets from it.

## Widgets

The user supplied `ui.png` as the target look for all UI widgets: disabled and normal
buttons, a progress bar, a tab bar, check boxes, radio buttons, a dropdown and a
scrollable list with a scroll bar. Each widget is brushed slate-blue or charcoal paint
inside a hand-painted brass rim, lit from the top left. The rims are uneven, so no two
edges match.

`src/Ariel.Godot/Skin/BrassSkin.cs` paints this look at startup. It does not cut pixels
from the reference. To see every widget, open the gallery scene
(`mise run start:ui-gallery`).

## Buttons (superseded)

The painted-brass skin from `ui.png` replaces the earlier steel-lapis button. The files
in this section document that earlier look.

The user supplied `button.png` as the target look for UI buttons: an inner glow, a
slight chamfer, a crisp bevel, and a slightly chipped, stone-like border.

`button-disabled.png` is the target look for disabled buttons: charcoal stone with a dim
grey lip and almost no glow.

`button-pcg.png` compares three procedural options in normal, hover and pressed states.
`tools/ui-mockups/button_pcg.py` generates it.
