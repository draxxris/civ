# Client

## Build and run

Run these commands from the repository root:

```sh
mise exec -- dotnet build src/Civ.Godot
mise exec -- godot-mono --path src/Civ.Godot
```

The build copies the eight feature mods into the client output directory under `mods/<Name>`.
Each folder contains a DLL, `mod.json`, data, and available PNG icons.
The build also copies the AI mod if its project exists.
The client discovers these folders and freezes their content once at startup.
The client does not reference the legacy ruleset assembly.

Set `CIV_MODS` to use a different mods directory.
Mods must supply compatible manifests and all required content providers.

## Controls

- WASD or arrow keys: move the camera.
- Mouse wheel: change the zoom.
- Middle-button drag: move the camera.
- Left click: select a unit or city. Repeated clicks cycle through a stack.
- Right click: move the selected unit along the preview path, or attack an enemy at war.
- Tab: select the next awake unit with movement remaining.
- B: found a city with the selected settler.
- F: fortify the selected unit.
- R: open the research panel.
- C: open the civics panel.
- Enter or Space: end the turn.
- Esc: close a panel or clear the selection.

The unit panel lists legal worker builds and earned promotions.
Move orders continue at the next turn boundary until the unit arrives or the route becomes invalid.
Opponents use a registered AI service. Without one, the client ends their turns automatically.

The city screen shows food, yields, commerce, happiness, health, specialists, buildings, and the production queue.
Click a city tile to change its citizen assignment.
The queue buttons add, remove, or move entries.
Unavailable production choices show requirement failures in tooltips.
The civics panel stages one choice per option and previews the complete revolution before adoption.

## Screenshots and smoke checks

The screenshot option saves a viewport capture after the requested number of ready frames, then quits.
Use an absolute output path. The client creates its parent directory.

```sh
timeout 30 xvfb-run -a godot-mono --headless --path src/Civ.Godot --quit-after 200

xvfb-run -a -s "-screen 0 1600x900x24" env LIBGL_ALWAYS_SOFTWARE=1 \
  godot-mono --rendering-driver opengl3 --path src/Civ.Godot --resolution 1600x900 \
  -- --screenshot /tmp/civ-setup.png --stage setup --frames 60
```

Screenshot stages: `setup`, `map`, `map-city`, `city`, `research`, `civics`, `path`, `world`, and `terrain`.
`map-city` founds a city before capture. `city` also opens its city screen and adds three eligible queue entries.
`terrain` showcases merged mountain ridges, softened ground transitions, and scattered nature props.
`--screenshots` runs through all stages and captures each screenshot to `docs/screenshots/<stage>.png`.
`--fps-probe` runs on a rendered display with VSync disabled and reports average FPS over 100 frames.
`--turns N` advances the game before capture.
`--world small`, `--opponents 2`, and `--seed 3` override debug game settings.
`--reveal` explores the map for terrain inspection without making distant units visible.
`--zoom 14` increases the initial camera distance.
`--terrain-debug N` sets the terrain shader debug view: 1 blend weights, 2 hidden weight, 3 colour only, 4 lighting only.
These inspection switches apply only to screenshot runs.

## Rendering and limits

The terrain is a lit 3D height field in chunks of 4 × 4 tiles, with a translucent water surface at sea level.
Its shader blends tile textures with height-modulated blending and draws fog, unexplored clouds, borders, rivers, the hover outline and the work-tile
overlay from two per-tile data textures, so these changes do not rebuild meshes.
A world change rebuilds only the chunks whose tiles changed.
Feature marks, routes, and icons use batched triangle meshes and an icon atlas.
The renderer shares each mesh across three horizontal copies for the east-west seam.
Camera movement does not rebuild the map. Commands rebuild the changed presentation layers.
The Standard map contains 84 × 52 hexes without one scene node per hex.

- Unit stacks show one representative icon and a count. Selection still cycles through every unit.
- The map dims explored tiles outside current sight. The rules do not store historical terrain snapshots.
- Production eligibility and city ledgers can cause a short delay when opening or changing a city screen.
- Queue reordering uses existing production commands. It rejects entries that are no longer eligible.
- The unavailable-production filter shows the first 40 blocked choices per category.
- The client has no save/load screen, diplomacy screen, pedia, or sound controls yet.
- Go-to orders belong to the client session and do not enter simulation saves.
