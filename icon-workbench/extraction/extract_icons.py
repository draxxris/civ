#!/usr/bin/env python3
"""Extract square icons from a known grid using the vendored splitter pipeline."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Mapping

from PIL import Image, ImageDraw

from split_icons_base import (
    cleanup_background_mask,
    crop_box_for_center,
    debug_background_image,
    estimate_edge_background,
    make_background_mask,
    optimize_center,
    postprocess_icon,
    score_candidate,
)

OffsetMap = Mapping[tuple[int, int], tuple[int, int]]


def extract_sheet(
    input_path: str | Path,
    output_dir: str | Path,
    *,
    rows: int,
    cols: int,
    crop_size: int,
    cut_edges: int = 0,
    manual_offsets: OffsetMap | None = None,
    search_radius: int = 28,
    coarse_step: int = 4,
    tolerance: float = 0.03,
    cleanup: int = 3,
    balance_depth: int = 12,
) -> dict:
    """Extract a known grid as 64x64 PNGs and save geometry/debug evidence.

    Offsets use (row, column) keys and (dx, dy) values. Positive dx moves
    right; positive dy moves up, matching the source splitter's convention.
    Set search_radius=0 to use nominal equal-cell centers before offsets.
    """
    if rows <= 0 or cols <= 0:
        raise ValueError("rows and cols must be positive")
    if crop_size <= 0:
        raise ValueError("crop_size must be positive")
    if cut_edges < 0 or cut_edges * 2 >= crop_size:
        raise ValueError("cut_edges must leave a non-empty crop")
    if search_radius < 0:
        raise ValueError("search_radius must be >= 0")
    if coarse_step <= 0:
        raise ValueError("coarse_step must be positive")
    if balance_depth <= 0:
        raise ValueError("balance_depth must be positive")

    offsets = dict(manual_offsets or {})
    for (row, col), (dx, dy) in offsets.items():
        if not (0 <= row < rows and 0 <= col < cols):
            raise ValueError(f"manual offset index ({row},{col}) is outside {rows}x{cols}")
        if not isinstance(dx, int) or not isinstance(dy, int):
            raise ValueError(f"manual offset ({row},{col}) must contain integer pixels")

    source_path = Path(input_path).expanduser().resolve()
    output_path = Path(output_dir).expanduser().resolve()
    if not source_path.is_file():
        raise FileNotFoundError(source_path)

    with Image.open(source_path) as opened:
        source = opened.convert("RGBA")
    rgb = source.convert("RGB")
    width, height = source.size
    if width < cols or height < rows:
        raise ValueError(f"sheet {width}x{height} is too small for {rows}x{cols}")

    cell_w = width / cols
    cell_h = height / rows
    if crop_size > width or crop_size > height:
        raise ValueError("crop_size cannot exceed either sheet dimension")

    background = estimate_edge_background(rgb)
    mask = make_background_mask(rgb, background, tolerance)
    mask = cleanup_background_mask(mask, cleanup)
    output_path.mkdir(parents=True, exist_ok=True)
    icons_path = output_path / "icons"
    icons_path.mkdir(exist_ok=True)

    geometry = {
        "source": str(source_path),
        "source_size": [width, height],
        "grid": {"rows": rows, "cols": cols, "cell_width": cell_w, "cell_height": cell_h},
        "parameters": {
            "crop_size": crop_size,
            "cut_edges": cut_edges,
            "output_size": 64,
            "search_radius": search_radius,
            "coarse_step": coarse_step,
            "tolerance": tolerance,
            "cleanup": cleanup,
            "balance_depth": balance_depth,
            "manual_offsets": {
                f"{row},{col}": [dx, dy]
                for (row, col), (dx, dy) in sorted(offsets.items())
            },
        },
        "centering_note": (
            "Automatic centering uses the vendored outer-edge background mask. "
            "Silver frames can enclose blue cell backgrounds, so the mask may "
            "not describe each cell interior. Check the debug images and adjust "
            "manual offsets or use search_radius=0 when automatic scores are not useful."
        ),
        "icons": [],
    }

    debug = source.copy()
    draw = ImageDraw.Draw(debug)

    for row in range(rows):
        for col in range(cols):
            nominal_cx = (col + 0.5) * cell_w
            nominal_cy = (row + 0.5) * cell_h
            if search_radius == 0:
                auto_cx, auto_cy = nominal_cx, nominal_cy
                score = score_candidate(mask, auto_cx, auto_cy, crop_size, balance_depth)
            else:
                auto_cx, auto_cy, score = optimize_center(
                    mask,
                    nominal_cx,
                    nominal_cy,
                    crop_size,
                    search_radius,
                    coarse_step,
                    balance_depth,
                )

            dx, dy = offsets.get((row, col), (0, 0))
            final_cx = auto_cx + dx
            final_cy = auto_cy - dy
            box = crop_box_for_center(final_cx, final_cy, crop_size)
            left, top, right, bottom = box
            if left < 0 or top < 0 or right > width or bottom > height:
                raise ValueError(
                    f"crop for cell ({row},{col}) is outside the sheet: {box}; "
                    f"center=({final_cx:.2f},{final_cy:.2f})"
                )

            crop = source.crop(box)
            final = postprocess_icon(crop, cut_edges, 64, None, None)
            filename = f"icon_r{row:02d}_c{col:02d}.png"
            final.save(icons_path / filename, "PNG")

            trimmed_box = [
                left + cut_edges,
                top + cut_edges,
                right - cut_edges,
                bottom - cut_edges,
            ]
            geometry["icons"].append({
                "row": row,
                "column": col,
                "display_row": row + 1,
                "display_column": col + 1,
                "file": f"icons/{filename}",
                "nominal_center": [nominal_cx, nominal_cy],
                "auto_center": [auto_cx, auto_cy],
                "manual_offset": [dx, dy],
                "final_center": [final_cx, final_cy],
                "center_score": score,
                "crop_box": list(box),
                "trimmed_source_box": trimmed_box,
                "output_size": [64, 64],
            })

            draw.rectangle((left, top, right - 1, bottom - 1), outline=(255, 255, 255, 255), width=2)
            if cut_edges:
                trim_left, trim_top, trim_right, trim_bottom = trimmed_box
                draw.rectangle(
                    (trim_left, trim_top, trim_right - 1, trim_bottom - 1),
                    outline=(255, 168, 0, 255),
                    width=2,
                )
            draw.line((final_cx - 5, final_cy, final_cx + 5, final_cy), fill=(255, 0, 255, 255), width=2)
            draw.line((final_cx, final_cy - 5, final_cx, final_cy + 5), fill=(255, 0, 255, 255), width=2)

    geometry_path = output_path / "geometry.json"
    geometry_path.write_text(json.dumps(geometry, indent=2) + "\n", encoding="utf-8")
    debug.save(output_path / "_debug_crop_boxes.png", "PNG")
    debug_background_image(rgb, mask).save(output_path / "_debug_background_mask.png", "PNG")
    return geometry


def _parse_offsets(path: Path | None) -> dict[tuple[int, int], tuple[int, int]]:
    if path is None:
        return {}
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError("offsets JSON must be an object with 'row,col' keys")
    offsets = {}
    for key, value in raw.items():
        try:
            row, col = (int(part.strip()) for part in key.split(","))
        except (AttributeError, TypeError, ValueError) as error:
            raise ValueError(f"invalid offset key {key!r}; expected 'row,col'") from error
        if not isinstance(value, list) or len(value) != 2:
            raise ValueError(f"offset {key!r} must be a two-element JSON array")
        offsets[(row, col)] = (int(value[0]), int(value[1]))
    return offsets


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="source grid image")
    parser.add_argument("output", type=Path, help="output directory")
    parser.add_argument("--rows", type=int, required=True)
    parser.add_argument("--cols", type=int, required=True)
    parser.add_argument("--crop-size", type=int, required=True, help="square source crop before trimming")
    parser.add_argument("--cut-edges", type=int, default=0, help="trim this many source pixels from each side")
    parser.add_argument("--offsets", type=Path, help="JSON object keyed by zero-based 'row,col'")
    parser.add_argument("--search-radius", type=int, default=28)
    parser.add_argument("--coarse-step", type=int, default=4)
    parser.add_argument("--tolerance", type=float, default=0.03)
    parser.add_argument("--cleanup", type=int, choices=(0, 3, 5, 7), default=3)
    parser.add_argument("--balance-depth", type=int, default=12)
    args = parser.parse_args()
    result = extract_sheet(
        args.input,
        args.output,
        rows=args.rows,
        cols=args.cols,
        crop_size=args.crop_size,
        cut_edges=args.cut_edges,
        manual_offsets=_parse_offsets(args.offsets),
        search_radius=args.search_radius,
        coarse_step=args.coarse_step,
        tolerance=args.tolerance,
        cleanup=args.cleanup,
        balance_depth=args.balance_depth,
    )
    print(f"Extracted {len(result['icons'])} icons into {args.output.resolve()}")
    print(f"Geometry: {(args.output.resolve() / 'geometry.json')}")
    print(f"Crop-box debug: {(args.output.resolve() / '_debug_crop_boxes.png')}")


if __name__ == "__main__":
    main()
