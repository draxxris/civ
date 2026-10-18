# Capture tools

Everything here is for looking at the game, not for playing it.
The full list of switches is in `src/Civ.Godot/README.md`, under "Screenshots and smoke checks". The code is in `src/Civ.Godot/Debug/DebugOptions.cs`.

## One capture

```sh
mise run shot -- --focus hills --clean --pitch 30 --distance 9 --out-dir /tmp/shots --screenshot hills --stats
```

- `--focus hills|forest|jungle|road|peak|river|city` centres on the best patch of that kind. `--focus-tile col,row` centres on a tile.
- `--pitch`, `--yaw`, `--distance` set the camera. The defaults are 62, 0 and 18. A low pitch shows height.
- `--clean` removes the HUD, tooltips, hover, path preview, units, cities and labels.
- `--terrain-debug relief|bands|slope|lattice|cavity` (or a number) changes the ground view. `--wireframe` is the same as `lattice`.
- `--stats` writes `<name>.json` next to the PNG: props per set before and after thinning, triangles per layer, height range per relief class, FPS.
- `--seed`, `--world tiny|...` choose the map.

## Many captures, one launch

`--shots tools/shots/poses.json` takes a list of poses. A missing camera field keeps the previous shot's value.
Each shot has a `name` and optional `focus`, `focusTile` `[col,row]`, `pitch`, `yaw`, `distance`, `terrainDebug` and `wireframe`.

## Tuning without rebuilding

- `--tuning HillsRise=0.4,HillsRadius=0.7` overrides any `TerrainTuning` value. An unknown name lists the valid names.
- `--tuning-file file.json` takes `{"HillsRise": 0.4}`.
- `--scatter-density-scale 0.5` scales forest and jungle density.
- `--scatter-keep tree=1,other=0.5` sets the fraction of props drawn after thinning.

Run twice with different values, then `mise run shot:compare -- a.png b.png ab.png`.

## Hand-made scenes

`--scenario tools/shots/scenarios/hills-forest-roads.json --world tiny` replaces the generated map with a fixed scene.
Use `tools/shots/scenarios/improvements.json` to preview improvement icons. The format is in `src/Civ.Godot/Debug/DebugScenario.cs`.
Every tile is revealed and visible. Use `--clean` to hide the starting units.
Road paths can be point arrays for the default road, or objects with a `path` and optional `route`. Set `routes` instead to assign one route name per point:

```json
{"roads":[{"routes":["road","railroad"],"path":[[2,7],[3,7]]}]}
```

The generated start can leave owned land under the scene. Set `"owner": null` in `fill` (or on a tile) to clear it.

A tile can create a city through the `city` field:

```json
{"at":[5,5],"city":{"owner":0,"name":"Testburg","population":5,"buildings":["cities:walls"]}}
```

Owner and name are required. Population defaults to 1, and buildings default to an empty list.
The city clears the plot improvement and sets its owner. Unknown building ids stop the run and appear in the error.
A second city on the same tile stops the run.
Use `cities:high_walls` for the higher wall tier. A city with both buildings shows only High Walls.
Wall models remain visible with `--clean`. Omit `--clean` to inspect the city token, label, and units inside the ring.

## City buildings

A scenario city can set `"era"` (a full id, or a short name such as `"modern"`). This sets the owner's current era, which picks the building style and the ground under the buildings.
The era belongs to the owner, so cities of different eras need different owners.

`tools/shots/scenarios/cities.json` shows four eras (ancient, renaissance, industrial and modern) at sizes 3, 8, 13 and 20. The left block has walls and the right block has none.
Capture it with `--shots tools/shots/cities-poses.json --scenario tools/shots/scenarios/cities.json --world tiny --clean --stats`.
The layout code is `src/Civ.Client.Core/Terrain/CityLayout.cs`. `--stats` reports `city_buildings` and `city_ground` triangles.

## Routes

`tools/shots/scenarios/roads-all.json` lays one straight row of each land route type. `roads-transitions.json` has rows that
change type every three tiles, a winding railroad, two mixed junctions, and a railroad and a maglev over hills. `roads-junctions-ground.json` and
`roads-junctions-raised.json` have junctions of three (Y and T), four and six arms for each type from paved road up.
`roads-hills.json` has railroads over two and three adjacent hills, over two hills with a valley between them, and over a
lone hill next to a highway. `roads-bridges.json` has every land route type crossing one river.

```sh
mise run shot -- --shots tools/shots/roads-poses.json --scenario tools/shots/scenarios/roads-all.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-transitions-poses.json --scenario tools/shots/scenarios/roads-transitions.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-junctions-ground-poses.json --scenario tools/shots/scenarios/roads-junctions-ground.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-junctions-raised-poses.json --scenario tools/shots/scenarios/roads-junctions-raised.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-hills-poses.json --scenario tools/shots/scenarios/roads-hills.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-bridges-poses.json --scenario tools/shots/scenarios/roads-bridges.json --world tiny --clean --out-dir /tmp/roads
```

`--stats` reports `roads` (draped ribbons) and `route_parts` (decks, supports and masts) triangles.
The curves come from `src/Civ.Client.Core/Terrain/RoutePlan.cs` and the heights from `RouteProfile.cs` in the same folder.

## Improvement models

`tools/shots/scenarios/improvements-all.json` places every improvement that has a 3D model (land models in rows, water models on an ocean strip).
Capture it with `--shots tools/shots/improvements-all-poses.json --scenario tools/shots/scenarios/improvements-all.json --world tiny --clean`.
It takes about two minutes. The models come from `python3 tools/art/improvement_models.py`.
