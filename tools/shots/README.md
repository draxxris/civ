# Capture tools

Everything here is for looking at the game, not for playing it. The full list of
switches is in `src/Ariel.Godot/README.md`, under "Screenshots and smoke checks". The
code is in `src/Ariel.Godot/Debug/DebugOptions.cs`.

## One capture

```sh
mise run shot -- --focus hills --clean --pitch 30 --distance 9 --out-dir /tmp/shots --screenshot hills --stats
```

- `--focus hills|forest|jungle|road|peak|river|city` centres on the best patch of that
  kind. `--focus-tile col,row` centres on a tile.
- `--pitch`, `--yaw`, `--distance` set the camera. The defaults are 62, 0 and 18. A low
  pitch shows height.
- `--clean` removes the HUD, tooltips, hover, path preview, units, cities and labels.
- `--terrain-debug relief|bands|slope|lattice|cavity` (or a number) changes the ground
  view. `--wireframe` is the same as `lattice`.
- `--stats` writes `<name>.json` next to the PNG: props per set before and after
  thinning, triangles per layer, height range per relief class, FPS.
- `--seed`, `--world tiny|...` choose the map.

## Looking around by hand

Drop `--screenshot` and add `--inspect` to start on the same scene with the same pose
and no capture, then drive the camera: WASD or arrows to pan, Q and E to turn, Z and X
to tilt, the wheel to zoom.

```sh
mise run shot -- --inspect --stage world --clean \
  --scenario tools/shots/scenarios/cliffs-land-ramp.json --focus-tile 10,10 \
  --yaw 0 --pitch 20 --distance 6
```

Without `--inspect` (or a capture switch) the run opens the mod manager and the setup
screen as a player sees them. Note that the wheel cannot go closer than a distance of 6,
while a capture pose can reach 1, so set `--distance` for close work.

## Many captures, one launch

`--shots tools/shots/poses.json` takes a list of poses. A missing camera field keeps the
previous shot's value. Each shot has a `name` and optional `focus`, `focusTile`
`[col,row]`, `pitch`, `yaw`, `distance`, `terrainDebug` and `wireframe`.

## Tuning without rebuilding

- `--tuning HillsRise=0.4,HillsRadius=0.7` overrides any `TerrainTuning` value. An
  unknown name lists the valid names.
- `--tuning-file file.json` takes `{"HillsRise": 0.4}`.
- `--scatter-density-scale 0.5` scales forest and jungle density.
- `--scatter-keep tree=1,other=0.5` sets the fraction of props drawn after thinning.

Run twice with different values, then `mise run shot:compare -- a.png b.png ab.png`.

## Hand-made scenes

A `--scenario` run sizes the map to the scene: bounding box plus two tiles of margin, no
wrapping, no opponents, every other tile cleared. Nothing the generator made survives,
so a capture shows the scene and nothing else, and the same file frames the same way on
any seed. Add `--world tiny` to go back to a generated world with the scene laid over
it. `--focus-tile` takes the coordinates the file was written with.

`--scenario tools/shots/scenarios/hills-forest-roads.json --world tiny` replaces the
generated map with a fixed scene. Use `tools/shots/scenarios/improvements.json` to
preview improvement icons, and `tools/shots/scenarios/resources.json --world small` to
see every map resource model on a tile it may spawn on. The format is in
`src/Ariel.Godot/Debug/DebugScenario.cs`. Every tile is revealed and visible. Use
`--clean` to hide the starting units. Road paths can be point arrays for the default
road, or objects with a `path` and optional `route`. Set `routes` instead to assign one
route name per point:

```json
{"roads":[{"routes":["road","railroad"],"path":[[2,7],[3,7]]}]}
```

The generated start can leave owned land under the scene. Set `"owner": null` in `fill`
(or on a tile) to clear it.

A tile can create a city through the `city` field:

```json
{"at":[5,5],"city":{"owner":0,"name":"Testburg","population":5,"buildings":["cities:walls"]}}
```

Owner and name are required. Population defaults to 1, and buildings default to an empty
list. The city clears the plot improvement and sets its owner. Unknown building ids stop
the run and appear in the error. A second city on the same tile stops the run. Use
`cities:high_walls` for the higher wall tier. A city with both buildings shows only High
Walls. Wall models remain visible with `--clean`. Omit `--clean` to inspect the city
token, label, and units inside the ring.

## Scenario maps as PNGs

`tools/shots/scenario_map.py` draws a scenario as a plan view, so a layout can be
checked without launching the client. It needs Python 3 and Pillow, like the scripts in
`tools/art`.

```sh
python3 tools/shots/scenario_map.py tools/shots/scenarios/*.json --out /tmp/maps
```

Each file becomes a PNG named after the scenario. Files that are not scenarios, such as
the `*-poses.json` camera lists, are skipped so a wildcard run still exits clean. The
map is the content's Tiny size, 52 by 32 tiles, unless `--world` or `--size` says
otherwise. The picture is trimmed to the tiles the scenario changes; `--full-map` keeps
the whole grid. Hexes are labelled with their column,row unless `--no-coords` is given,
and `--hex` sets the hex radius in pixels.

Grassland is green, plains yellow, desert pale yellow, tundra light grey, snow light
blue, marsh blue-green, every water tile the same blue and peaks very dark grey. `T`
marks forest or jungle, `C` a city, and `H` heightened ground: a tile that is the raised
side of a cliff. Heightening is independent of hills and peaks, so a hill on flat ground
has no `H` and a raised grass tile does. Rivers are blue hex borders, and cliff rock a
thick grey line on the shared edge. Roads are dark brown lines between the centres of
neighbouring tiles that both carry a route, which is how the client links them, so
junctions and tile-by-tile chains come out right. Route types are not distinguished. A
cliffed edge always drops towards water, and every heightened tile shows rock on its
water edges.

## Units

Set `"clearUnits": true` to remove the generated starting units. Use `units` to place
units:

```json
{"clearUnits":true,"units":[{"at":[7,5],"type":"warrior","owner":2,"health":40,"count":2}]}
```

`type` accepts a full id or a short name. `owner` defaults to 0, `health` to 100 and
`count` to 1. Several entries on one tile make a stack.
`tools/shots/scenarios/unit-banners.json` shows banners for four owners and damaged
stacks:

```sh
mise run shot -- --stage map --scenario tools/shots/scenarios/unit-banners.json --world tiny --pitch 50 --distance 8 --out-dir /tmp/banners --screenshot banners
```

## City buildings

A scenario city can set `"era"` (a full id, or a short name such as `"modern"`). This
sets the owner's current era, which picks the building style and the ground under the
buildings. The era belongs to the owner, so cities of different eras need different
owners.

`tools/shots/scenarios/cities.json` shows four eras (ancient, renaissance, industrial
and modern) at sizes 3, 8, 13 and 20. The left block has walls and the right block has
none. Capture it with
`--shots tools/shots/cities-poses.json --scenario tools/shots/scenarios/cities.json --world tiny --clean --stats`.
The layout code is `src/Ariel.Client.Core/Terrain/CityLayout.cs`. `--stats` reports
`city_buildings` and `city_ground` triangles.

## Routes

`tools/shots/scenarios/roads-all.json` lays one straight row of each land route type.
`roads-transitions.json` has rows that change type every three tiles, a winding
railroad, two mixed junctions, and a railroad and a maglev over hills.
`roads-junctions-ground.json` and `roads-junctions-raised.json` have junctions of three
(Y and T), four and six arms for each type from paved road up. `roads-hills.json` has
railroads over two and three adjacent hills, over two hills with a valley between them,
and over a lone hill next to a highway. `roads-bridges.json` has every land route type
crossing one river.

```sh
mise run shot -- --shots tools/shots/roads-poses.json --scenario tools/shots/scenarios/roads-all.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-transitions-poses.json --scenario tools/shots/scenarios/roads-transitions.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-junctions-ground-poses.json --scenario tools/shots/scenarios/roads-junctions-ground.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-junctions-raised-poses.json --scenario tools/shots/scenarios/roads-junctions-raised.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-hills-poses.json --scenario tools/shots/scenarios/roads-hills.json --world tiny --clean --out-dir /tmp/roads
mise run shot -- --shots tools/shots/roads-bridges-poses.json --scenario tools/shots/scenarios/roads-bridges.json --world tiny --clean --out-dir /tmp/roads
```

`--stats` reports `roads` (draped ribbons) and `route_parts` (decks, supports and masts)
triangles. The curves come from `src/Ariel.Client.Core/Terrain/RoutePlan.cs` and the
heights from `RouteProfile.cs` in the same folder.

## Improvement models

`tools/shots/scenarios/improvements-all.json` places every improvement that has a 3D
model (land models in rows, water models on an ocean strip). Capture it with
`--shots tools/shots/improvements-all-poses.json --scenario tools/shots/scenarios/improvements-all.json --world tiny --clean`.
It takes about two minutes. The models come from
`python3 tools/art/improvement_models.py`.

`tools/shots/scenarios/watermills.json` puts watermills beside rivers of every shape: a
long meandering river, a lone edge, a river through a tile, a bend and a river on hills.
The wheel must stand in the water, parallel to the river. Capture it with
`--shots tools/shots/watermills-poses.json --scenario tools/shots/scenarios/watermills.json --world tiny --clean`.
