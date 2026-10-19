# Client

## Build and run

Run these commands from the repository root:

```sh
mise exec -- dotnet build src/Civ.Godot
mise exec -- godot-mono --path src/Civ.Godot
```

The build copies the installed mods into the client output directory under `mods/<Name>`.
Each folder contains a DLL, `mod.json`, data, and available art.
The client reads these manifests before it loads mod assemblies.
After selection, the client loads the selected mods and freezes their content once.
The client does not reference the legacy ruleset assembly.

The client merges manifests from all available mods directories. Missing directories are harmless.
Mods must supply compatible manifests and all required content providers.

For each stable manifest ID, the first copy in this priority order wins:

1. The optional `CIV_MODS` directory.
2. The `mods` directory inside `OS.GetUserDataDir()` (`user://mods`).
3. Bundled candidates, in this order:
   - `mods` beside the client assembly.
   - `mods` inside `AppContext.BaseDirectory`.
   - The project directory `res://mods`.

Equivalent paths, including directory links, count as one root at their highest priority.
Within one root, ordinal folder order resolves duplicate IDs. Bundled candidate order resolves ties between bundled roots.
The output list uses ordinal manifest ID order.

A winning copy replaces the entire mod: its manifest, assembly, data, and art.
The client never fills missing files from a losing copy.
Only the winning manifest supplies dependencies and content references.
Unrelated IDs from lower-priority roots remain available.
`CIV_MODS` adds or replaces mods by ID. It does not hide other installed mods.
The engine-free `GameCatalog.Load(string)` API still loads one directory without these host search rules.

## Mod manager

1. Start the client normally to open **Mods** before any mod assembly loads.
2. Select the mods that you trust.
3. Select **Continue** to save the profile and open game setup.

Each row shows the mod name, version, stable ID, selection state, chosen source, and full source path.
For duplicate IDs, the row also lists overridden sources and paths. Those copies do not load.
Required dependencies and content providers enter the selection automatically.
The **Required by** text explains each requirement.
To disable a required mod, first disable the mods that require it.
Content references can form cycles. Ordered dependencies cannot form cycles.
Missing providers and dependency cycles block Continue until you correct the selection.
Invalid manifests appear as diagnostics with their full paths. Other valid mods remain selectable.
An invalid winning manifest reserves every readable ID, even when metadata after the ID has a syntax error.
The reserved IDs stay visible as blocked sources. The client does not substitute lower-priority code.
To continue, repair the manifest or disable those IDs and the mods that require them.
An invalid lower-priority copy does not block a valid winner.
If no ID is readable, the client reports the invalid folder but cannot associate it with an installed ID.

The profile is `user://mod-profile.json` in the Godot user-data directory, separate from saved games.
It stores explicit choices as manifest IDs, not folder names.
With no profile, all installed mods start selected, as before.
With a profile, new mods stay disabled unless another selected mod requires them.
Missing saved IDs remain visible until you disable them or reset the selection.
A broken profile blocks Continue. Either reset button replaces its choices without silently enabling mod code.
The client saves the profile before it loads assemblies. A save error leaves the manager open.

Mod selection controls which code loads. Game options control features within the loaded ruleset.
Card Research and Tech Tree remain default-off game options even when their mods load.

Game setup offers **Mods (restart required)** after the catalog loads.
This manager saves the next selection and quits. Start the client again to apply that selection.
It does not unload assemblies or rebuild the active catalog.
A load error also returns to this restart-only manager because some assemblies can remain in memory.
A dependency-valid selection can still lack the content for game setup. The client reports this error without starting a game.

Headless runs, screenshot runs, FPS probes, and `--skip-mod-manager` bypass the startup screen but honor the profile.
They use all installed mods when no profile exists. Invalid profiles or selections make these runs exit with code 1.
For an isolated smoke run, use a separate Godot user-data directory through `XDG_DATA_HOME` on Linux.

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

Screenshot stages: `setup`, `map`, `map-city`, `city`, `research`, `civics`, `path`, `world`, `terrain`, `rivers`, `peak` and `wrap`.
`map-city` founds a city before capture. `city` also opens its city screen and adds three eligible queue entries.
`terrain` showcases a snow-capped mountain range, softened ground transitions, and scattered nature props.
`rivers` focuses on generated river bends and mouths without changing the map.
`peak` focuses on the first mountain, and `wrap` on the east-west seam.

The `espionage` stage runs a ten-capture intelligence tour with real buttons, city selection, a spy mission, and turn advancement.
Use `--world tiny --opponents 1 --stage espionage --screenshot /tmp/espionage.png`.
The tour checks locks, separate player treasuries, queues, directed protection, expiry, and stale selections.
It changes debug state between captures. It does not represent an ordinary saved game.

These switches apply only to capture runs and go after `--` on the Godot command line.
`mise run shot -- <switches>` builds and runs with them. See also `tools/shots/README.md` for worked examples.

### Run and output

- `--screenshot <path|name>`: save one capture, then quit. A bare name (no slash) goes to `--out-dir`. Use an absolute path otherwise.
- `--screenshots`: run every stage above in turn. Each goes to `docs/screenshots/<stage>.png`, or to `--out-dir`.
- `--shots <file.json>`: take several captures in one launch. Each entry has a `name` and optional `focus`, `focusTile` `[col,row]`,
  `pitch`, `yaw`, `distance`, `terrainDebug` and `wireframe`. A missing camera field keeps the previous shot's value.
- `--out-dir <dir>`: where captures and stats go. The default is `docs/screenshots`, which holds the committed images, so set this for test captures.
- `--stage <name>`: the stage for `--screenshot`. It defaults to `setup`, or to `map` when a scene switch below is used.
- `--frames N`: ready frames to wait before each capture. The default is 60, or 30 with a scene switch, and 90 in the stage tour.
- `--stats`: print and save `<name>.json` beside each capture: props per scatter set before and after thinning, triangles per layer,
  height minimum, maximum and mean per relief class, and FPS.
- `--fps-probe`: needs a rendered display. It uses a huge map, VSync off and a 1600 × 900 window, and reports average and 1%-low FPS
  at the default zoom and at maximum zoom-out (120 warm-up frames, then 300 sampled frames each).

### Game setup

- `--world <size>`: world size by id or name, for example `tiny`, `small`, `huge`.
- `--map-generator <generator>`: registered generator by id, short id, or display name. The default comes from the loaded ruleset.
  The setup screen also offers a **World layout** selector that discovers registered maps automatically.
  `continents` selects Continental, the existing fractal layout with several landmasses.
  `inland_sea` selects land around a central sea.
  `king_of_the_hill` selects a central land hub, eight spokes, and an outer land rim with water between the spokes.
  `world:new_world` (`new_world`) starts all players on the western continent. The eastern continent starts unsettled.
  `world:twin_continents` (`twin_continents`) creates two large continents separated by ocean.
  `world:great_plains` (`great_plains`) creates broad, flat land with scattered lakes, fewer hills, and no peaks.
  All layouts reuse the same climate, resource, river, relief, and start-position rules.
  The new layouts retain east–west wrapping and vary with the seed.
- `--seed N`: map seed. The default is 1.
- `--opponents N`: number of opponents. The default is 3.
- `--turns N`: end this many turns before capture.
- `--reveal`: explore the whole map for the human team without making distant units visible.
- `--scenario <file.json>`: replace the generated map with a hand-made scene. The format is described in
`src/Civ.Godot/Debug/DebugScenario.cs` and examples are `tools/shots/scenarios/hills-forest-roads.json` and
  `tools/shots/scenarios/improvements.json`. It reveals every tile and makes every tile visible.
  A tile can contain `"city":{"owner":0,"name":"Testburg","population":5,"buildings":["cities:walls"]}`.
  Owner and name are required. Population defaults to 1, and buildings default to an empty list.
  The city clears the plot improvement and sets its owner. Unknown building ids stop the run and appear in the error.
  A second city on the same tile stops the run. Without `--clean`, city tokens, labels, and units remain visible.

### Camera and view

- `--focus <kind>`: centre on the best patch of `hills`, `forest`, `jungle`, `road`, `peak`, `river` or `city`.
  "Best" is the matching tile with the most matching tiles within two steps. Ties go to the lowest tile id.
- `--focus-tile col,row`: centre on a tile, in offset coordinates.
- `--pitch DEG`, `--yaw DEG`, `--distance N`: camera pose. The defaults are 62, 0 (looking north) and 18.
  Pitch is clamped to 5–89 and distance to 1–120, which is wider than the player's zoom limits.
- `--zoom D`: change the initial camera distance by D, for example `--zoom -8` to move closer. `--distance` is applied after it.
- `--clean`: hide the HUD, tooltips, hover outline, path preview, units, cities and labels.

### Terrain debug views

- `--terrain-debug <mode>`: a number or a name. 0 `normal`, 1 `weights` (blend weights as colour), 2 `hidden` (hidden weight),
  3 colour only, 4 `relief` (white ground, lighting only), 5 `bands` (height colour with contour lines every 0.05),
  6 `slope` (green flat to red steep), 7 `lattice` (the mesh triangles), 8 `cavity` (blue valleys, red ridges).
  A lattice cavity of ±4 shows full colour.
- `--wireframe`: the same as `--terrain-debug lattice`. The compatibility renderer has no wireframe mode, so the shader draws the lines.
  It assumes the default lattice of 9 steps per tile column.

### Tuning overrides

- `--tuning Name=value,Name=value`: replace values in `TerrainTuning` (`src/Civ.Client.Core/Terrain/TerrainField.cs`), for example
  `HillsRise=0.4,HillsRadius=0.7`. Names are not case sensitive. An unknown name stops the run and lists the valid names.
- `--tuning-file <file.json>`: the same, as `{"HillsRise": 0.4}`. `--tuning` wins when both set a name.
- `--scatter-density-scale F`: multiply forest, jungle and other feature scatter density. Rock density is not affected.
- `--scatter-keep tree=F,other=F`: the fraction of props drawn after the deterministic thinning that protects the frame rate.
  The defaults are `tree=0.667` and `other=0.5`.

## Rendering and limits

The terrain is a lit 3D height field in chunks of 4 × 4 tiles, with a translucent water surface at sea level.
Each mountain tile has a pointed summit, a lower shoulder summit and erosion gullies between sharp spurs.
The height, footprint, flank steepness and gully pattern change from tile to tile, so no two mountains look the same.
Neighboring mountain tiles join with a sharp ridge that dips to a saddle between their summits, so a line of mountains
reads as one range.
Near a river, the terrain is drawn down to the river's valley floor. The water is level from bank to bank, and a
mountain beside a river stands a little lower and slopes more steeply from its summit to the bank.
The shader adds snow above a ragged snow line and leaves steep faces as bare rock.
The snow frays into patches and rills, fills rock crevices first and reaches further down the gullies.
A per-vertex cavity value from the mesh darkens gullies and lightens ridges on mountains.
Scatter foliage textures get a muted olive grade at load time.
Terrain shaders blend tile textures and draw fog, unexplored clouds, borders, hover outlines, and work-tile overlays from two per-tile data textures.
Visibility, borders, and hover changes update these textures without rebuilding meshes.

River meshes follow curved channels and use a separate shader for water, ripples, and wet banks.
Visual connectors join disconnected river marks without changing gameplay river masks.
A world change rebuilds only the chunks whose tiles changed.
Feature marks, routes, and icons use batched triangle meshes and an icon atlas.
Each route type has its own look (`RouteLooks` in `src/Civ.Client.Core/Terrain/RoutePlan.cs`).
Cart paths, roads, paved roads, highways and both railroads are ribbons draped on the terrain (`shaders/road.gdshader`).
The maglev and the jumplane are 3D decks on piers or legs. Electric railroads get portal masts and a contact wire
(`MapRenderer.RouteParts.cs`, `shaders/route_structure.gdshader`).
Cart paths and roads meet at the tile centre, each path drawn only in its own wedge around the centre so the
translucent paths never stack where they overlap.
The other types run in one smooth curve through a tile, and at a junction the side links merge into that curve.
Where the paths of a junction overlap, each point belongs to the path whose centre line is nearest (`RouteJunction`).
Each path draws only its own points, so the paths join as one surface and edge lines and curbs stop at the mouth of each
spur. The rails and the through road's centre line are drawn again on top, so they run through the junction.
Where two types meet, the width eases from one to the other and the surfaces crossfade along a ragged front at the shared edge.
`RouteProfile` (`src/Civ.Client.Core/Terrain/RouteProfile.cs`) sets the height of every route path over the terrain:
- Railroads and electric railroads keep a gentle grade (`RailBed.cs`). Before a hill they climb onto a concrete viaduct
  on steel legs, in one straight ramp up to the hill's apex. Between two neighbouring hills they run in one straight
  span from apex to apex. Beyond the last hill they come back down in one straight ramp. The bend at the foot of each
  ramp is rounded.
- Where a ground route meets a maglev or a jumplane, the ground route climbs and the deck comes down. They meet halfway
  at the shared edge, and the deck outline changes into the viaduct outline across the crossfade.
- A ground route that crosses a river goes onto a bridge. Cart paths and roads get a wooden trestle, paved roads and
  highways a stone arch bridge, and railroads a steel truss bridge. Maglevs and jumplanes need no bridge.
An improvement with a `.glb` in its mod's `art/models` folder is drawn as 3D parts seated on the terrain, instead of its icon.
City building models with a positive `cityRing` scene extra form a ring around the city token.
Only the highest completed tier appears, with zero yaw and the tile's fog shade. Hidden tiles show no ring.
The model's `clear` extra controls scatter removal. Units stand closer to the center inside a ring.
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
