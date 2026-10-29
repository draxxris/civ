#!/usr/bin/env python3
"""Draw a top-down hex map of a capture scenario as a PNG.

    python3 tools/shots/scenario_map.py tools/shots/scenarios/*.json --out /tmp/maps
    python3 tools/shots/scenario_map.py tools/shots/scenarios/cliffs-land.json --world tiny --hex 40

The scene files are the ones the Godot client loads with `--scenario` (format in
src/Ariel.Godot/Debug/DebugScenario.cs). Only the layout fields are read: `fill`, `tiles`
(terrain, kind, feature, lake, river, cliff, city) and `roads`. Units, improvements,
resources and owners are ignored.

The picture is a plan of the map, not a copy of the renderer:

- Grassland is green, plains yellow, desert pale yellow, tundra light grey, snow light
  blue, marsh blue-green, and every water tile the same blue. Peaks (the mountain terrain,
  or Peak relief) are very dark grey.
- A `T` marks forest or jungle.
- An `H` marks heightened ground: a tile that is the raised side of a cliff. Heightening is
  independent of hills and peaks, so a hill on flat ground has no `H` and a raised grass
  tile does. Letters turn white on dark ground so the peak grey stays readable.
- A `C` marks a tile that founds a city.
- Every hex is labelled with its column,row (--no-coords leaves the labels out).
- A river is a blue hex border. A road is a dark brown line between the centres of two
  neighbouring tiles that both carry a route, so junctions and tile-by-tile connections come
  out as the client draws them.
- Cliff rock is a thick grey line on the shared edge, following the client's rule: a cliff
  is drawn when the edge has a cliff bit and exactly one side is lifted, a land tile is
  always the high side of a land/water cliff, and every heightened tile that touches water
  shows rock on that edge.

Hexes are the game's pointy-top grid with odd rows shifted right. The map is the content's
world size (Tiny by default, scaled by hex_overrides.sourceGridScale), trimmed to the tiles
the scenario changes unless --full-map is given.
"""
from __future__ import annotations

import argparse
import functools
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO = Path(__file__).resolve().parents[2]
SQRT3 = math.sqrt(3)

# Fill by the part of the terrain id after the mod prefix.
TERRAIN_FILL = {
    "coast": "#3f7fd0", "ocean": "#3f7fd0", "slimy_coast": "#3f7fd0", "slimy_ocean": "#3f7fd0",
    "grass": "#5c9e46", "plains": "#d9c85c", "desert": "#efe8a8", "tundra": "#cfcfcf",
    "snow": "#cfe6f5", "marsh": "#3f8f80", "peak": "#2e2e33", "hill": "#8a9a58",
    "none": "#808080",
}
WATER_FILL = "#3f7fd0"
UNKNOWN_FILL = "#777777"
TREES = {"forest", "jungle"}

RIVER_COLOUR = "#2657a8"
ROAD_COLOUR = "#46301f"
CLIFF_COLOUR = "#8a8a90"
GRID_COLOUR = "#28282c"
TREE_INK = "#123a0f"
INK = "#141414"
PALE_INK = "#ffffff"
COORD_INK = "#3c3c3c"

# Corner c sits at 60c + 30 degrees measured with +Y up, so corner 0 is the upper right of a
# pointy-top hex. Edge d runs between corners d and d - 1, which puts edge 0 east, 1 north-east,
# 2 north-west, 3 west, 4 south-west and 5 south-east, the order of Hex.Directions.
CORNERS = [(math.cos(math.radians(60 * c + 30)), -math.sin(math.radians(60 * c + 30))) for c in range(6)]

# Odd-row offset neighbours (column, row) in Hex.Directions order, per row parity.
NEIGHBOURS = {
    0: ((1, 0), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1)),
    1: ((1, 0), (1, -1), (0, -1), (-1, 0), (0, 1), (1, 1)),
}


def short_id(value: str) -> str:
    """The part of a content id after the mod prefix: world:slimy_coast -> slimy_coast."""
    return value.rsplit(":", 1)[-1]


class Grid:
    """Row-major odd-row offset map; east-west wrap does not matter for a plan view."""

    def __init__(self, width: int, height: int):
        self.width, self.height = width, height

    @property
    def count(self) -> int:
        return self.width * self.height

    def tile(self, col: int, row: int) -> int | None:
        return row * self.width + col if 0 <= row < self.height and 0 <= col < self.width else None

    def offset(self, tile: int) -> tuple[int, int]:
        return tile % self.width, tile // self.width

    def neighbour(self, tile: int, direction: int) -> int | None:
        col, row = self.offset(tile)
        dc, dr = NEIGHBOURS[row & 1][direction]
        return self.tile(col + dc, row + dr)

    def centre(self, tile: int) -> tuple[float, float]:
        col, row = self.offset(tile)
        return SQRT3 * (col + 0.5 * (row & 1)), 1.5 * row


class Tile:
    __slots__ = ("terrain", "kind", "feature", "lake", "rivers", "cliffs", "lifts", "route", "city")

    def __init__(self, terrain: str = "", kind: str = "Flat", feature: str | None = None, lake: bool = False,
                 rivers: int = 0):
        self.terrain, self.kind, self.feature, self.lake = terrain, kind, feature, lake
        self.rivers = rivers
        self.cliffs = 0
        self.lifts = 0
        self.route = False
        self.city = False

    def state(self) -> tuple:
        return (self.terrain, self.kind, self.feature, self.lake, self.rivers, self.cliffs, self.lifts,
                self.route, self.city)


class Scenario:
    def __init__(self, grid: Grid, tiles: list[Tile], fill_state: tuple, water: dict[str, bool], warnings: list[str]):
        self.grid, self.tiles, self.fill_state = grid, tiles, fill_state
        self.water, self.warnings = water, warnings

    def is_water(self, tile: int) -> bool:
        plot = self.tiles[tile]
        if plot.lake:
            return True
        return self.water.get(short_id(plot.terrain), False)


def load_content(root: Path) -> tuple[dict[str, bool], dict[str, tuple[int, int]]]:
    """Terrain is-water by short id, and world size name -> (columns, rows), from the content data."""
    terrain_file = root / "World/data/terrain.json"
    size_file = root / "Foundation/data/world-size.json"
    override_file = root / "Foundation/data/hex-overrides.json"
    water = {short_id(entry["id"]): bool(entry.get("isWater")) for entry in json.loads(terrain_file.read_text())}
    scale = next(e for e in json.loads(override_file.read_text()) if e.get("sourceGridScale"))["sourceGridScale"]
    sizes = {entry["name"].lower(): (entry["gridWidth"] * scale, entry["gridHeight"] * scale)
             for entry in json.loads(size_file.read_text())}
    return water, sizes


def targets(grid: Grid, entry: dict, warnings: list[str], name: str) -> list[int]:
    """Every tile an entry addresses, through `at` or `rect`."""
    result: list[int] = []
    if "at" in entry:
        col, row = entry["at"]
        tile = grid.tile(col, row)
        if tile is None:
            warnings.append(f"{name}: tile {col},{row} is outside the {grid.width}x{grid.height} map")
        else:
            result.append(tile)
    if "rect" in entry:
        x1, y1, x2, y2 = entry["rect"]
        for row in range(y1, y2 + 1):
            for col in range(x1, x2 + 1):
                if (tile := grid.tile(col, row)) is not None:
                    result.append(tile)
    return result


def set_edge(grid: Grid, tiles: list[Tile], tile: int, direction: int, present: bool) -> None:
    """Copy GameState.SetCliff: mirror the edge, lift the named tile, refuse off-map edges."""
    other = grid.neighbour(tile, direction)
    if other is None:
        tiles[tile].cliffs &= ~(1 << direction)
        tiles[tile].lifts &= ~(1 << direction)
        return
    mirror = (direction + 3) % 6
    for target, edge in ((tile, direction), (other, mirror)):
        bit = 1 << edge
        tiles[target].cliffs = (tiles[target].cliffs | bit) if present else (tiles[target].cliffs & ~bit)
    bit = 1 << direction
    tiles[tile].lifts = (tiles[tile].lifts | bit) if present else (tiles[tile].lifts & ~bit)
    tiles[other].lifts &= ~(1 << mirror)


def parse_scenario(path: Path, width: int, height: int, water: dict[str, bool]) -> Scenario | None:
    doc = json.loads(path.read_text())
    if isinstance(doc, list):
        return None  # a shots pose list (tools/shots/scenarios/*-poses.json), not a scenario
    if not isinstance(doc, dict):
        raise ValueError("not a scenario: a scenario is a JSON object with fill and tiles")
    grid, warnings = Grid(width, height), []
    fill = doc.get("fill", {})
    defaults = (fill.get("terrain", ""), str(fill.get("kind", "Flat")).capitalize(), fill.get("feature") or None,
                bool(fill.get("lake", False)), int(fill.get("river", 0)))
    fill_state = Tile(*defaults).state()
    tiles = [Tile(*defaults) for _ in range(grid.count)]

    def edit(tile: int, entry: dict) -> None:
        plot = tiles[tile]
        if "terrain" in entry:
            plot.terrain = entry["terrain"]
        if "kind" in entry:
            plot.kind = str(entry["kind"]).capitalize()
        if "feature" in entry:
            plot.feature = entry["feature"] or None
        if "lake" in entry:
            plot.lake = bool(entry["lake"])
        if "river" in entry:
            plot.rivers = int(entry["river"])
        if "city" in entry:
            plot.city = True

    entries = doc.get("tiles", [])
    for entry in entries:
        for tile in targets(grid, entry, warnings, path.name):
            edit(tile, entry)

    # Cliffs go on after every tile has its final terrain, exactly as DebugScenario does.
    for entry in entries:
        if "cliff" not in entry:
            continue
        value = entry["cliff"]
        mask = 63 if value is True else 0 if value is False else int(value)
        for tile in targets(grid, entry, warnings, path.name):
            if tiles[tile].lake or water.get(short_id(tiles[tile].terrain), False):
                continue
            for direction in range(6):
                other = grid.neighbour(tile, direction)
                if other is not None and tiles[other].lake:
                    continue  # edges to lakes never carry a cliff
                set_edge(grid, tiles, tile, direction, bool(mask & (1 << direction)))

    # A road entry only says which tiles carry a route; the client links a tile to every neighbour
    # that has any route, so the order of the points and the gaps between them do not matter.
    for entry in doc.get("roads", []):
        for point in entry if isinstance(entry, list) else entry.get("path", []):
            if (tile := grid.tile(point[0], point[1])) is not None:
                tiles[tile].route = True
    return Scenario(grid, tiles, fill_state, water, warnings)


def fill_colour(plot: Tile, water: dict[str, bool], warned: set[str]) -> str:
    if plot.lake or water.get(short_id(plot.terrain), False):
        return WATER_FILL
    if plot.kind == "Peak" or short_id(plot.terrain) == "peak":
        return TERRAIN_FILL["peak"]
    name = short_id(plot.terrain)
    if name in TERRAIN_FILL:
        return TERRAIN_FILL[name]
    if name not in warned:
        warned.add(name)
        print(f"warning: no colour for terrain '{plot.terrain}', using grey", file=sys.stderr)
    return UNKNOWN_FILL


def is_dark(colour: str) -> bool:
    red, green, blue = (int(colour[index:index + 2], 16) / 255 for index in (1, 3, 5))
    return 0.299 * red + 0.587 * green + 0.114 * blue < 0.4


@functools.lru_cache(maxsize=16)
def font(size: int):
    for candidate in (
        "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/liberation-sans-fonts/LiberationSans-Bold.ttf",
    ):
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default(size)


def markers(plot: Tile) -> list[str]:
    letters = []
    if plot.feature and short_id(plot.feature) in TREES:
        letters.append("T")
    if plot.lifts:
        letters.append("H")
    if plot.city:
        letters.append("C")
    return letters


def render(scenario: Scenario, hexes: int, supersample: int, full_map: bool, coords: bool) -> Image.Image:
    grid, tiles = scenario.grid, scenario.tiles
    drawn = [tile for tile in range(grid.count) if tiles[tile].state() != scenario.fill_state] if not full_map else None
    if drawn:
        cols = [tile % grid.width for tile in drawn]
        rows = [tile // grid.width for tile in drawn]
        col_range, row_range = range(min(cols), max(cols) + 1), range(min(rows), max(rows) + 1)
    else:
        col_range, row_range = range(grid.width), range(grid.height)
    drawn = [grid.tile(col, row) for row in row_range for col in col_range]
    shown = set(drawn)

    scale = hexes * supersample
    centres = {tile: grid.centre(tile) for tile in drawn}
    margin = 0.62
    left = min(x for x, _ in centres.values()) - 0.866 - margin
    top = min(y for _, y in centres.values()) - 1 - margin
    right = max(x for x, _ in centres.values()) + 0.866 + margin
    bottom = max(y for _, y in centres.values()) + 1 + margin
    out_width, out_height = math.ceil((right - left) * hexes), math.ceil((bottom - top) * hexes)
    width, height = out_width * supersample, out_height * supersample
    where = {tile: ((x - left) * scale, (y - top) * scale) for tile, (x, y) in centres.items()}

    def corner(tile: int, index: int) -> tuple[float, float]:
        cx, cy = where[tile]
        dx, dy = CORNERS[index % 6]
        return cx + dx * scale, cy + dy * scale

    def edge(tile: int, direction: int) -> list[tuple[float, float]]:
        return [corner(tile, direction), corner(tile, direction - 1)]

    image = Image.new("RGB", (width, height), "#f2f0ea")
    draw = ImageDraw.Draw(image)
    warned: set[str] = set()
    colour = {tile: fill_colour(tiles[tile], scenario.water, warned) for tile in drawn}
    grid_width = max(1, round(0.055 * scale))
    for tile in drawn:
        draw.polygon([corner(tile, c) for c in range(6)], fill=colour[tile])

    for tile in drawn:
        outline = [corner(tile, c) for c in range(6)]
        draw.line(outline + [outline[0]], fill=GRID_COLOUR, width=grid_width, joint="curve")

    # A road joins neighbouring tiles that both carry a route, which is how the client plans links.
    road_width = max(2, round(0.22 * scale))
    for tile in drawn:
        if not tiles[tile].route:
            continue
        for direction in range(6):
            other = grid.neighbour(tile, direction)
            if other is None or other < tile or not tiles[other].route or other not in where:
                continue
            cx, cy = where[tile]
            ox, oy = where[other]
            draw.line([(cx, cy), (ox, oy)], fill=ROAD_COLOUR, width=road_width)

    # Cliff rock, resolved like TerrainField.CliffHigh: exactly one lifted side names the high tile,
    # an unlifted land/water edge drops towards the water, and a lifted/lifted edge draws nothing.
    rock = max(3, round(0.38 * scale))
    for tile in drawn:
        for direction in range(6):
            other = grid.neighbour(tile, direction)
            if other is None or (other < tile and other in shown):
                continue
            mirror = (direction + 3) % 6
            lifted = bool(tiles[tile].lifts & (1 << direction)) != bool(tiles[other].lifts & (1 << mirror))
            cliffed = bool(tiles[tile].cliffs & (1 << direction) or tiles[other].cliffs & (1 << mirror))
            # A cliffed land/water edge always drops towards the water, and heightened ground
            # always shows rock on its water edges even without a cliff bit.
            show = (lifted if cliffed else False)
            show = show or (cliffed and scenario.is_water(tile) != scenario.is_water(other))
            show = show or (tiles[tile].lifts != 0 and scenario.is_water(other))
            show = show or (tiles[other].lifts != 0 and scenario.is_water(tile))
            if show:
                draw.line(edge(tile, direction), fill=CLIFF_COLOUR, width=rock)

    # Rivers run along hex edges, never through a tile: a river bit is an edge, mirrored on both
    # plots. Drawn over the rock so a river that leaves over a cliff still reads as water.
    water_width = max(2, round(0.16 * scale))
    for tile in drawn:
        for direction in range(6):
            other, mirror = grid.neighbour(tile, direction), (direction + 3) % 6
            if other is None or (other < tile and other in shown):
                continue
            if not (tiles[tile].rivers & (1 << direction) or tiles[other].rivers & (1 << mirror)):
                continue
            draw.line(edge(tile, direction), fill=RIVER_COLOUR, width=water_width)

    if coords:
        for tile in drawn:
            col, row = grid.offset(tile)
            cx, cy = where[tile]
            draw.text((cx, cy - 0.62 * scale), f"{col},{row}", font=font(round(0.26 * scale)),
                      fill=PALE_INK if is_dark(colour[tile]) else COORD_INK, anchor="mm")

    for tile in drawn:
        letters = markers(tiles[tile])
        if not letters:
            continue
        cx, cy = where[tile]
        dark = is_dark(colour[tile])
        size = {1: 1.05, 2: 0.75, 3: 0.6}[len(letters)] * scale
        step = {1: 0.0, 2: 0.4, 3: 0.55}[len(letters)] * scale
        first = -step * (len(letters) - 1) / 2
        for index, letter in enumerate(letters):
            ink = PALE_INK if dark else (TREE_INK if letter == "T" else INK)
            draw.text((cx + first + index * step, cy), letter, font=font(round(size)), fill=ink, anchor="mm")

    if supersample > 1:
        image = image.resize((out_width, out_height), Image.Resampling.LANCZOS)
    return image


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=__doc__.splitlines()[0],
        formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("scenarios", nargs="+", type=Path, help="scenario JSON files (see tools/shots/scenarios/)")
    parser.add_argument("--out", type=Path, default=Path("/tmp/scenario-maps"),
                        help="output directory (default: /tmp/scenario-maps)")
    parser.add_argument("--world", default="tiny", help="world size: duel, tiny, small, standard, large, huge, giant, gigantic")
    parser.add_argument("--size", help="grid size as COLUMNSxROWS, overriding --world")
    parser.add_argument("--hex", type=int, default=32, help="hex radius in output pixels (default: 32)")
    parser.add_argument("--supersample", type=int, default=2, help="draw at this multiple, then scale down (default: 2)")
    parser.add_argument("--full-map", action="store_true", help="keep the whole map instead of trimming the fill border")
    parser.add_argument("--no-coords", dest="coords", action="store_false", help="leave out the column,row labels")
    parser.add_argument("--content", type=Path, default=REPO / "src/Mods",
                        help="mod directory holding the terrain and world size data")
    args = parser.parse_args(argv)

    if args.hex < 3 or args.supersample < 1:
        parser.error("--hex must be at least 3 and --supersample at least 1")
    water, sizes = load_content(args.content)
    if args.size:
        parts = args.size.lower().split("x")
        if len(parts) != 2 or not all(part.isdigit() and int(part) > 0 for part in parts):
            parser.error(f"invalid --size '{args.size}', expected COLUMNSxROWS with positive integers")
        width, height = (int(part) for part in parts)
    elif args.world.lower() in sizes:
        width, height = sizes[args.world.lower()]
    else:
        parser.error(f"unknown world size '{args.world}'. Have: {', '.join(sorted(sizes))}")

    failed = 0
    for path in args.scenarios:
        try:
            scenario = parse_scenario(path, width, height, water)
        except (OSError, ValueError, KeyError, TypeError) as error:
            print(f"{path}: {error}", file=sys.stderr)
            failed += 1
            continue
        if scenario is None:
            print(f"{path.name}: skipped, not a scenario")
            continue
        image = render(scenario, args.hex, args.supersample, args.full_map, args.coords)
        args.out.mkdir(parents=True, exist_ok=True)
        target = args.out / f"{path.stem}.png"
        image.save(target)
        print(f"{target}  {image.width}x{image.height}  {width}x{height} map")
        for warning in scenario.warnings:
            print(f"  warning: {warning}", file=sys.stderr)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
