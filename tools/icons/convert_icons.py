#!/usr/bin/env python3
"""Convert mod icons to 64x64 RGBA PNGs and write tools/icons/report.md.

Each definition with a non-empty "icon" in src/Mods/<Mod>/data/*.json gets one PNG at
src/Mods/<Mod>/art/icons/<name>.png, where <name> is the part of the id after the colon.

Resolution order for each definition:
  1. Button tuples (",direct,atlas,col,row"): the atlas cell named by the tuple, under CONVENTION below, if the
     atlas exists under AND-extraction/media/assets (case-insensitive), the cell lies in its grid, and the cell
     has visible pixels.
  2. Otherwise the direct DDS, if it exists under AND-extraction/media/assets (case-insensitive) and has visible
     pixels. Plain direct paths use only this step. A fully transparent direct file (Art/Interface/Buttons/Empty.dds)
     counts as absent: AND's own script pairs Empty.dds with an atlas cell in a message icon.
  3. Otherwise the definition is missing and listed in the report.

Before anything is written, the atlas convention is re-checked on every definition that has both a visible
direct image and an atlas cell. The run stops if another convention now fits the data better. Each run first
deletes the existing art/icons/*.png files of every mod, so no stale icon survives a data change.

Run from anywhere:  python3 tools/icons/convert_icons.py
"""

from __future__ import annotations

import hashlib
import json
import re
import statistics
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path
from typing import NamedTuple

from PIL import Image, ImageChops, ImageStat

REPO = Path(__file__).resolve().parents[2]
MEDIA = REPO / "AND-extraction" / "media" / "assets"
ARCHIVE_INDEX = REPO / "AND-extraction" / "media" / "archive-index.json"
MODS = REPO / "src" / "Mods"
REPORT = Path(__file__).resolve().parent / "report.md"

ICON = 64  # output size in px, and the default atlas cell size
AGREE_MAD = 20.0  # mean abs RGBA difference (0-255) below which two images show the same icon
MIN_PAIRS = 10  # with fewer pairs than this, the convention cannot be checked
SWEEP_CELLS = (32, 48, 60, 62, 66, 96, 128)  # other cell sizes, to show ICON is the best fit
EXAMPLE_ID = "cities:palace"  # pair reported as a worked example
LIST_LIMIT = 50  # missing ids listed per mod
DECODE_ERRORS = (OSError, ValueError, SyntaxError, NotImplementedError)
SAFE_NAME = re.compile(r"[A-Za-z0-9_.\-]+")
BLANK = "direct blank"


class Convention(NamedTuple):
    cell: int  # px per cell; the grid is atlas width // cell columns by atlas height // cell rows
    base: int  # index of the first column and row: 1 means (1, 1) is the top-left cell
    origin: str  # "top": row 1 is the top band of the image, as in a DDS texture
    inset: int  # px trimmed from each side of a cell


# Verified by verify_convention() on the current data, and re-checked before every conversion.
# On the 358 pairs checked: 1-based top-left agrees on 197. 0-based agrees on 1 and puts 53 pairs outside the
# grid. Bottom-left agrees on none. A 1, 2 or 4 px border agrees on 9 or fewer.
CONVENTION = Convention(cell=ICON, base=1, origin="top", inset=0)


class Missing(NamedTuple):
    ident: str
    reason: str
    absent: tuple[str, ...]  # referenced files that are not in the media tree


@dataclass(frozen=True)
class Icon:
    mod: str
    ident: str
    direct: str
    atlas: str
    col: int | None
    row: int | None


@dataclass
class Resolution:
    image: Image.Image | None = None
    source: str = ""  # "direct" or "atlas"
    reason: str = ""  # why there is no image
    fallback: str = ""  # for a direct result from a button tuple: why the atlas cell was not used
    resized: bool = False  # the direct image was not ICON x ICON and was resized


@dataclass
class Dual:
    """A definition with a visible direct image and an atlas cell: one pair for the convention check."""

    ident: str
    direct_name: str
    atlas_name: str
    col: int
    row: int
    direct: Image.Image  # ICON x ICON
    atlas: Image.Image


@dataclass
class Scored:
    conv: Convention
    mads: list[float | None]  # one per Dual; None when the cell lies outside the grid

    @property
    def in_grid(self) -> int:
        return sum(m is not None for m in self.mads)

    @property
    def agreeing(self) -> list[float]:
        return [m for m in self.mads if m is not None and m < AGREE_MAD]

    @property
    def disagreeing(self) -> list[float]:
        return [m for m in self.mads if m is not None and m >= AGREE_MAD]

    @property
    def mean(self) -> float:
        return statistics.mean([255.0 if m is None else m for m in self.mads])

    @property
    def median(self) -> float:
        return statistics.median([255.0 if m is None else m for m in self.mads])


@dataclass
class ModStats:
    icons: int = 0  # definitions with a non-empty icon
    direct: int = 0
    atlas: int = 0
    fallback: Counter[str] = field(default_factory=Counter)  # tuple icons that used the direct image, by why
    resized: int = 0
    no_icon: int = 0
    png_bytes: int = 0
    missing: list[Missing] = field(default_factory=list)


# ---------------------------------------------------------------- media and parsing


def norm(ref: str) -> str:
    return ref.replace("\\", "/").lstrip("/").lower()


class Media:
    """Read-only view of the media tree: case-insensitive lookup and cached RGBA decoding."""

    def __init__(self, root: Path) -> None:
        self._paths: dict[str, Path] = {}
        for path in sorted(root.rglob("*")):
            if path.is_file():
                self._paths.setdefault(norm(path.relative_to(root).as_posix()), path)
        self._images: dict[Path, Image.Image] = {}

    def find(self, ref: str) -> Path | None:
        return self._paths.get(norm(ref)) if ref else None

    def image(self, path: Path) -> Image.Image:
        if path not in self._images:
            with Image.open(path) as im:
                self._images[path] = im.convert("RGBA")
        return self._images[path]


def _int_or_none(text: str) -> int | None:
    return int(text) if text.isdecimal() else None


def parse_icon(mod: str, ident: str, raw: str) -> Icon:
    """Split a non-empty icon into its direct path, atlas path and 1-based cell."""
    raw = raw.strip()
    if not raw.startswith(","):
        return Icon(mod, ident, raw, "", None, None)
    fields = (raw.split(",")[1:] + ["", "", "", ""])[:4]
    direct, atlas, col, row = (f.strip() for f in fields)
    return Icon(mod, ident, direct, atlas, _int_or_none(col), _int_or_none(row))


def load_definitions() -> list[tuple[str, str, str]]:
    """(mod, id, icon) for every definition, in sorted file and array order."""
    rows = []
    for path in sorted(MODS.glob("*/data/*.json")):
        mod = path.parent.parent.name
        for item in json.loads(path.read_text(encoding="utf-8-sig")):
            rows.append((mod, str(item["id"]), str(item.get("icon") or "")))
    return rows


def archived_paths() -> set[str]:
    """Normalised path of every entry in the archive index, whether or not it was exported to media."""
    if not ARCHIVE_INDEX.exists():
        return set()
    entries = json.loads(ARCHIVE_INDEX.read_text(encoding="utf-8-sig"))
    return {norm(str(e["Path"])) for e in entries}


def shared_images() -> list[list[str]]:
    """Groups of two or more icon files with identical PNG bytes, largest first."""
    groups: dict[str, list[str]] = {}
    for path in sorted(MODS.glob("*/art/icons/*.png")):
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        groups.setdefault(digest, []).append(f"{path.parents[2].name}:{path.stem}")
    return sorted((g for g in groups.values() if len(g) > 1), key=len, reverse=True)


# ---------------------------------------------------------------- pixels


def to_icon(im: Image.Image) -> Image.Image:
    """Return an ICON x ICON RGBA image with no metadata."""
    if im.size != (ICON, ICON):
        im = im.resize((ICON, ICON), Image.Resampling.LANCZOS)
    return Image.frombytes("RGBA", im.size, im.tobytes())


def has_pixels(im: Image.Image) -> bool:
    return im.getchannel("A").getextrema()[1] > 0


def mad(a: Image.Image, b: Image.Image) -> float:
    """Mean absolute difference over the four RGBA channels, 0-255."""
    return sum(ImageStat.Stat(ImageChops.difference(a, b)).mean) / 4.0


def atlas_cell(atlas: Image.Image, col: int, row: int, conv: Convention = CONVENTION) -> Image.Image | None:
    """Crop one cell as ICON x ICON RGBA, or return None when the cell lies outside the grid."""
    cols, rows = atlas.width // conv.cell, atlas.height // conv.cell
    ci, ri = col - conv.base, row - conv.base
    if not (0 <= ci < cols and 0 <= ri < rows):
        return None
    x0 = ci * conv.cell
    y0 = ri * conv.cell if conv.origin == "top" else atlas.height - (ri + 1) * conv.cell
    i, c = conv.inset, conv.cell
    return to_icon(atlas.crop((x0 + i, y0 + i, x0 + c - i, y0 + c - i)))


# ---------------------------------------------------------------- resolution


def direct_image(icon: Icon, media: Media) -> tuple[Image.Image | None, str]:
    if not icon.direct:
        return None, "no direct path"
    path = media.find(icon.direct)
    if path is None:
        return None, "direct missing"
    try:
        im = media.image(path)
    except DECODE_ERRORS:
        return None, "direct unreadable"
    return (im, "") if has_pixels(im) else (None, BLANK)


def atlas_cell_of(icon: Icon, media: Media) -> tuple[Image.Image | None, str]:
    if not icon.atlas:
        return None, ""
    if icon.col is None or icon.row is None:
        return None, "atlas cell not numeric"
    path = media.find(icon.atlas)
    if path is None:
        return None, "atlas missing"
    try:
        atlas = media.image(path)
    except DECODE_ERRORS:
        return None, "atlas unreadable"
    cell = atlas_cell(atlas, icon.col, icon.row)
    if cell is None:
        return None, "cell outside atlas grid"
    return (cell, "") if has_pixels(cell) else (None, "atlas cell blank")


def resolve(icon: Icon, media: Media) -> Resolution:
    cell, atlas_note = atlas_cell_of(icon, media)
    if cell is not None:
        return Resolution(cell, "atlas")
    direct, direct_note = direct_image(icon, media)
    if direct is not None:
        return Resolution(to_icon(direct), "direct", resized=direct.size != (ICON, ICON), fallback=atlas_note)
    return Resolution(reason="; ".join(n for n in (direct_note, atlas_note) if n) or "no image")


def dual_of(icon: Icon, media: Media) -> Dual | None:
    if not (icon.direct and icon.atlas and icon.col is not None and icon.row is not None):
        return None
    d_path, a_path = media.find(icon.direct), media.find(icon.atlas)
    if d_path is None or a_path is None:
        return None
    try:
        direct, atlas = media.image(d_path), media.image(a_path)
    except DECODE_ERRORS:
        return None
    if not has_pixels(direct):
        return None
    return Dual(icon.ident, icon.direct, icon.atlas, icon.col, icon.row, to_icon(direct), atlas)


# ---------------------------------------------------------------- convention check


def candidates(cell: int) -> list[Convention]:
    return [
        Convention(cell, base, origin, inset)
        for base in (1, 0)
        for origin in ("top", "bottom")
        for inset in (0, 1, 2, 4)
    ]


def describe(c: Convention) -> str:
    return f"{c.cell}px {c.base}-based {c.origin}-left inset {c.inset}"


def score(duals: list[Dual], conv: Convention) -> Scored:
    mads: list[float | None] = []
    for d in duals:
        cell = atlas_cell(d.atlas, d.col, d.row, conv)
        mads.append(None if cell is None else mad(cell, d.direct))
    return Scored(conv, mads)


def gap_text(s: Scored) -> str:
    return (
        f"largest agreeing MAD {max(s.agreeing, default=float('nan')):.1f}, "
        f"smallest disagreeing MAD {min(s.disagreeing, default=float('nan')):.1f}"
    )


def verify_convention(duals: list[Dual]) -> tuple[Scored, Scored]:
    """Print the evidence; stop unless CONVENTION is still the best fit. Returns (chosen, runner-up)."""
    if len(duals) < MIN_PAIRS:
        raise SystemExit(f"Only {len(duals)} pairs have both images; cannot verify the atlas convention.")
    results = [score(duals, c) for c in candidates(ICON)]
    print(f"Atlas convention check: {len(duals)} definitions with a visible direct image and an atlas cell.")
    print(f"  {'convention':<34}{'in grid':>8}{'agree':>7}{'mean MAD':>10}{'median':>9}")
    for s in results:
        print(f"  {describe(s.conv):<34}{s.in_grid:>8}{len(s.agreeing):>7}{s.mean:>10.1f}{s.median:>9.1f}")
    best = max(results, key=lambda s: (len(s.agreeing), -s.mean))
    if best.conv != CONVENTION:
        raise SystemExit(f"Best fit is now [{describe(best.conv)}]. Review the evidence, then update CONVENTION.")
    chosen = next(s for s in results if s.conv == CONVENTION)
    runner_up = max((s for s in results if s.conv != CONVENTION), key=lambda s: len(s.agreeing))
    print(f"  chosen [{describe(CONVENTION)}]: {gap_text(chosen)}")
    print(f"  runner-up [{describe(runner_up.conv)}]: agrees on {len(runner_up.agreeing)}")
    print_sweep(duals)
    example = palace_example(duals)
    if example:
        print(f"  {example}")
    return chosen, runner_up


def print_sweep(duals: list[Dual]) -> None:
    print("  other cell sizes, best of 16 conventions each:")
    for cell in SWEEP_CELLS:
        best = max((score(duals, c) for c in candidates(cell)), key=lambda s: len(s.agreeing))
        print(f"    {cell:>4}px: agrees on {len(best.agreeing)} ({describe(best.conv)})")


def palace_example(duals: list[Dual]) -> str | None:
    d = next((d for d in duals if d.ident == EXAMPLE_ID), None)
    if d is None:
        return None
    parts = []
    for conv in (CONVENTION, Convention(ICON, 0, "top", 0)):
        cell = atlas_cell(d.atlas, d.col, d.row, conv)
        value = "outside grid" if cell is None else f"{mad(cell, d.direct):.1f}"
        parts.append(f"{describe(conv)}: {value}")
    return f"{EXAMPLE_ID} ({d.direct_name} vs {d.atlas_name} cell {d.col},{d.row}): " + "; ".join(parts)


def per_atlas(duals: list[Dual], chosen: Scored) -> list[tuple[str, int, int]]:
    """(atlas, pairs, pairs that agree under the chosen convention), most pairs first."""
    pairs: Counter[str] = Counter()
    agree: Counter[str] = Counter()
    for d, m in zip(duals, chosen.mads):
        pairs[d.atlas_name] += 1
        agree[d.atlas_name] += m is not None and m < AGREE_MAD
    return [(name, n, agree[name]) for name, n in pairs.most_common()]


def missing_atlases(icons: list[Icon], media: Media) -> list[tuple[str, int]]:
    """Missing atlas files, counted once per case-insensitive path and shown with the first spelling seen."""
    spelled: dict[str, str] = {}
    counts: Counter[str] = Counter()
    for icon in icons:
        if icon.atlas and media.find(icon.atlas) is None:
            spelled.setdefault(norm(icon.atlas), icon.atlas)
            counts[norm(icon.atlas)] += 1
    return [(spelled[key], n) for key, n in counts.most_common()]


# ---------------------------------------------------------------- writing


def output_path(mod: str, ident: str) -> Path | None:
    name = ident.split(":", 1)[-1]
    if ":" not in ident or not SAFE_NAME.fullmatch(name):
        return None
    return MODS / mod / "art" / "icons" / f"{name}.png"


def save_png(im: Image.Image, path: Path) -> int:
    im.save(path, format="PNG", optimize=False)
    return path.stat().st_size


def _clear_icons(mod: str) -> None:
    for old in (MODS / mod / "art" / "icons").glob("*.png"):
        old.unlink()


def _write_one(ms: ModStats, mod: str, ident: str, raw: str, media: Media, written: set[Path]) -> None:
    icon = parse_icon(mod, ident, raw)
    res = resolve(icon, media)
    if res.image is None:
        absent = tuple(r for r in (icon.direct, icon.atlas) if r and media.find(r) is None)
        ms.missing.append(Missing(ident, res.reason, absent))
        return
    out = output_path(mod, ident)
    if out is None or out in written:
        ms.missing.append(Missing(ident, "output name unsafe or already used", ()))
        return
    written.add(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    ms.png_bytes += save_png(res.image, out)
    ms.resized += res.resized
    if res.source == "direct":
        ms.direct += 1
        if res.fallback:
            ms.fallback[res.fallback] += 1
    else:
        ms.atlas += 1


def write_icons(defs: list[tuple[str, str, str]], media: Media) -> dict[str, ModStats]:
    stats = {mod: ModStats() for mod in sorted({mod for mod, _, _ in defs})}
    for mod in stats:
        _clear_icons(mod)
    written: set[Path] = set()
    for mod, ident, raw in defs:
        ms = stats[mod]
        if not raw.strip():
            ms.no_icon += 1
        else:
            ms.icons += 1
            _write_one(ms, mod, ident, raw, media, written)
    return stats


# ---------------------------------------------------------------- report


def _sum(stats: dict[str, ModStats], attr: str) -> int:
    return sum(getattr(s, attr) for s in stats.values())


def _sum_missing(stats: dict[str, ModStats]) -> int:
    return sum(len(s.missing) for s in stats.values())


def _summary_lines(stats: dict[str, ModStats]) -> list[str]:
    direct, atlas, size = _sum(stats, "direct"), _sum(stats, "atlas"), _sum(stats, "png_bytes")
    fallback = sum((s.fallback for s in stats.values()), Counter())
    fallback_text = ", ".join(f"{k}: {v}" for k, v in fallback.most_common()) or "none"
    return [
        f"- Definitions with an icon: {_sum(stats, 'icons')}. From direct DDS: {direct}. "
        f"From atlas cell: {atlas}. Missing: {_sum_missing(stats)}.",
        f"- Definitions without an icon (skipped): {_sum(stats, 'no_icon')}.",
        f"- Direct images used for button tuples, by why the atlas cell was not used: {fallback_text}.",
        f"- Direct images not {ICON}x{ICON} and resized: {_sum(stats, 'resized')}.",
        f"- Output: {direct + atlas} PNG files, {size:,} bytes ({size / 2**20:.2f} MiB), {ICON}x{ICON} RGBA.",
    ]


def _missing_summary_lines(stats: dict[str, ModStats], archived: set[str]) -> list[str]:
    everything = [m for s in stats.values() for m in s.missing]
    by_reason = Counter(m.reason for m in everything)
    listed = sum(any(norm(r) in archived for r in m.absent) for m in everything)
    lines = ["| Reason | Icons |", "|---|---:|"]
    lines += [f"| {reason} | {n} |" for reason, n in by_reason.most_common()]
    lines += [
        "",
        f"- {listed} of {len(everything)} missing icons reference a file that archive-index.json lists. Those files "
        "are in the game archives but were not exported to media. "
        f"The other {len(everything) - listed} reference files the index does not list.",
    ]
    return lines


def _convention_lines(duals: list[Dual], chosen: Scored, runner_up: Scored) -> list[str]:
    lines = [
        f"Hard-coded: {describe(CONVENTION)}. A pair agrees when the mean absolute RGBA difference is below "
        f"{AGREE_MAD:g}.",
        f"Checked on {len(duals)} definitions with a visible direct image and an atlas cell.",
        f"- Agreeing: {len(chosen.agreeing)} ({gap_text(chosen)}).",
        f"- Runner-up [{describe(runner_up.conv)}]: {len(runner_up.agreeing)} agreeing.",
    ]
    example = palace_example(duals)
    if example:
        lines.append(f"- Example, {example}")
    lines += ["", "| Atlas | Pairs | Agree | Disagree |", "|---|---:|---:|---:|"]
    lines += [f"| `{name}` | {n} | {k} | {n - k} |" for name, n, k in per_atlas(duals, chosen)]
    return lines


def _precedence_lines(duals: list[Dual], chosen: Scored) -> list[str]:
    return [
        "- Button tuples: the atlas cell, if the atlas exists, the cell lies in its grid and the cell has visible "
        "pixels. Otherwise the direct file.",
        "- Plain direct paths: the direct file. A fully transparent direct file counts as absent.",
        f"- {len(duals) - len(chosen.agreeing)} of {len(duals)} pairs show different art.",
    ]


def _shared_lines(groups: list[list[str]]) -> list[str]:
    files = sum(len(g) for g in groups)
    lines = [
        f"{files} icon files share their exact image with another icon file, in {len(groups)} groups. "
        "Groups of ids that show the same picture:",
        "",
    ]
    lines += [f"- {len(g)} files: {', '.join(g[:4])}{', ...' if len(g) > 4 else ''}" for g in groups[:5]]
    return lines


def _missing_lines(stats: dict[str, ModStats]) -> list[str]:
    lines = [f"## Missing icons (first {LIST_LIMIT} per mod)", ""]
    for mod, s in stats.items():
        lines.append(f"### {mod} ({len(s.missing)})")
        lines += [f"- `{m.ident}`: {m.reason}" for m in s.missing[:LIST_LIMIT]]
        if len(s.missing) > LIST_LIMIT:
            lines.append(f"- ... and {len(s.missing) - LIST_LIMIT} more")
        lines.append("")
    return lines


def render_report(
    stats: dict[str, ModStats],
    duals: list[Dual],
    chosen: Scored,
    runner_up: Scored,
    atlases_missing: list[tuple[str, int]],
    archived: set[str],
    shared: list[list[str]],
) -> str:
    lines = [
        "# Icon conversion report",
        "",
        "Generated by `tools/icons/convert_icons.py`. Rerun the script rather than editing this file.",
        "",
        "## Summary",
        "",
        *_summary_lines(stats),
        "",
        "## Counts per mod",
        "",
        "| Mod | Icons | Direct | Atlas | Missing | No icon | PNG bytes |",
        "|---|---:|---:|---:|---:|---:|---:|",
        *[
            f"| {mod} | {s.icons} | {s.direct} | {s.atlas} | {len(s.missing)} | {s.no_icon} | {s.png_bytes:,} |"
            for mod, s in stats.items()
        ],
        "",
        "## Why icons are missing",
        "",
        *_missing_summary_lines(stats, archived),
        "",
        "## Atlas convention",
        "",
        *_convention_lines(duals, chosen, runner_up),
        "",
        "## Image precedence",
        "",
        *_precedence_lines(duals, chosen),
        "",
        "## Identical images across ids",
        "",
        *_shared_lines(shared),
        "",
        "## Atlas files missing from media",
        "",
        *([f"- `{path}`: referenced by {n} definitions" for path, n in atlases_missing] or ["None."]),
        "",
        *_missing_lines(stats),
    ]
    return "\n".join(lines).rstrip() + "\n"


def print_summary(stats: dict[str, ModStats], differ: int, pairs: int) -> None:
    print()
    print(f"{'mod':<12}{'icons':>7}{'direct':>8}{'atlas':>7}{'missing':>9}{'no icon':>9}")
    for mod, s in stats.items():
        print(f"{mod:<12}{s.icons:>7}{s.direct:>8}{s.atlas:>7}{len(s.missing):>9}{s.no_icon:>9}")
    size = _sum(stats, "png_bytes")
    print(f"PNG bytes: {size:,} ({size / 2**20:.2f} MiB)")
    print(f"{differ} of {pairs} pairs show different art; button tuples use the atlas cell")
    print(f"report: {REPORT}")


def main() -> None:
    media = Media(MEDIA)
    defs = load_definitions()
    icons = [parse_icon(mod, ident, raw) for mod, ident, raw in defs if raw.strip()]
    duals = [d for d in (dual_of(icon, media) for icon in icons) if d is not None]
    chosen, runner_up = verify_convention(duals)
    stats = write_icons(defs, media)
    report = render_report(
        stats, duals, chosen, runner_up, missing_atlases(icons, media), archived_paths(), shared_images()
    )
    REPORT.write_text(report, encoding="utf-8")
    print_summary(stats, len(duals) - len(chosen.agreeing), len(duals))


if __name__ == "__main__":
    main()
