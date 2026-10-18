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
- `--terrain-debug relief|bands|slope|lattice` (or a number) changes the ground view. `--wireframe` is the same as `lattice`.
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

A tile can create a city through the `city` field:

```json
{"at":[5,5],"city":{"owner":0,"name":"Testburg","population":5,"buildings":["cities:walls"]}}
```

Owner and name are required. Population defaults to 1, and buildings default to an empty list.
The city clears the plot improvement and sets its owner. Unknown building ids stop the run and appear in the error.
A second city on the same tile stops the run.
Use `cities:high_walls` for the higher wall tier. A city with both buildings shows only High Walls.
Wall models remain visible with `--clean`. Omit `--clean` to inspect the city token, label, and units inside the ring.

## Improvement models

`tools/shots/scenarios/improvements-all.json` places every improvement that has a 3D model (land models in rows, water models on an ocean strip).
Capture it with `--shots tools/shots/improvements-all-poses.json --scenario tools/shots/scenarios/improvements-all.json --world tiny --clean`.
It takes about two minutes. The models come from `python3 tools/art/improvement_models.py`.
