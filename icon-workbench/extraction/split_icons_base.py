#!/usr/bin/env python3
"""
spell_split.py

Split a roughly gridded icon sheet into individually centered PNG icons.

CLI:
    python spell_split.py -c data.yaml
    python spell_split.py -c data.yaml -i input-grid.png -o ./output_dir/

If -i/-o are omitted and config is data/imagesets/001.yml:
    input  -> data/imagesets/001.png
    output -> data/imagesets/001/

Dependencies:
    pip install pillow pyyaml

Example config:

rows: 7
cols: 7
crop_size: 158
debug: true
coarse_step: 4
search_radius: 28
tolerance: 0.03
balance_depth: 12

# Optional post-processing, applied in this order:
cut_edges: 2
rescale_size: 124
border_replacement: border.png

# Optional. Defaults to os.cpu_count().
workers: 8

# Manual per-icon correction, applied AFTER automatic center optimization.
# index = row,col
# value = dx,dy (positive dy moves up; negative dy moves down)
overrides:
  - index: 0,2
    value: 0,3
  - index: 1,4
    value: -1,6

Important:
- The background tolerance/mask is used ONLY for center optimization.
- Exported icons preserve their original source pixels exactly.
- In debug mode, a separate pink-background visualization is created.
- Automatic centering uses only the OUTER N opposing 1-pixel slices.
- Post-processing order is: cut_edges -> rescale_size -> border_replacement.
- border_replacement preserves alpha and may enlarge the final canvas.
"""

from __future__ import annotations

import argparse
import os
from collections import deque
from concurrent.futures import ProcessPoolExecutor, as_completed
from pathlib import Path
from statistics import median

import yaml
from PIL import Image, ImageDraw, ImageFilter


DEFAULTS = {
    "rows": 7,
    "cols": 7,
    "crop_size": None,
    "debug": False,
    "coarse_step": 4,
    "search_radius": 28,
    "tolerance": 0.03,
    "min_spacing": 36,
    "balance_depth": 12,
    "cut_edges": 0,
    "rescale_size": None,
    "border_replacement": None,
    "cleanup": 3,
    "workers": None,
    "overrides": [],
}


def parse_args():
    p = argparse.ArgumentParser(
        description="Split a quasi-grid icon sheet into individually centered PNG icons."
    )
    p.add_argument("-c", "--config", required=True, type=Path, help="YAML config file")
    p.add_argument(
        "-i",
        "--input",
        type=Path,
        default=None,
        help=(
            "Input grid image. If omitted, uses <config-stem>.png "
            "next to the YAML file. Explicit relative paths use the current working directory."
        ),
    )
    p.add_argument(
        "-o",
        "--output",
        type=Path,
        default=None,
        help=(
            "Output directory. If omitted, uses <config-stem>/ "
            "next to the YAML file. Explicit relative paths use the current working directory."
        ),
    )
    return p.parse_args()


def parse_pair(value, field_name):
    """
    Accept:
        "0,2"
    or:
        [0, 2]
    """
    if isinstance(value, str):
        parts = [p.strip() for p in value.split(",")]
    elif isinstance(value, (list, tuple)):
        parts = list(value)
    else:
        raise ValueError(f"{field_name} must be 'x,y' or [x, y]")

    if len(parts) != 2:
        raise ValueError(f"{field_name} must contain exactly two values")

    try:
        return int(parts[0]), int(parts[1])
    except (TypeError, ValueError):
        raise ValueError(f"{field_name} values must be integers")


def load_config(path: Path):
    with path.open("r", encoding="utf-8") as f:
        loaded = yaml.safe_load(f) or {}

    if not isinstance(loaded, dict):
        raise ValueError("Top-level YAML config must be a mapping/object")

    cfg = dict(DEFAULTS)
    cfg.update(loaded)

    cfg["rows"] = int(cfg["rows"])
    cfg["cols"] = int(cfg["cols"])
    cfg["debug"] = bool(cfg["debug"])
    cfg["coarse_step"] = int(cfg["coarse_step"])
    cfg["search_radius"] = int(cfg["search_radius"])
    cfg["tolerance"] = float(cfg["tolerance"])
    cfg["min_spacing"] = int(cfg["min_spacing"])
    cfg["balance_depth"] = int(cfg["balance_depth"])
    cfg["cut_edges"] = int(cfg["cut_edges"])
    cfg["cleanup"] = int(cfg["cleanup"])

    if cfg["crop_size"] is not None:
        cfg["crop_size"] = int(cfg["crop_size"])

    if cfg["rescale_size"] is not None:
        cfg["rescale_size"] = int(cfg["rescale_size"])

    if cfg["border_replacement"] is not None:
        cfg["border_replacement"] = str(cfg["border_replacement"])

    workers = cfg.get("workers")
    cfg["workers"] = (os.cpu_count() or 1) if workers is None else max(1, int(workers))

    override_map = {}
    raw_overrides = cfg.get("overrides") or []

    if not isinstance(raw_overrides, list):
        raise ValueError("overrides must be a YAML list")

    for n, item in enumerate(raw_overrides):
        if not isinstance(item, dict):
            raise ValueError(f"overrides[{n}] must be a mapping")

        if "index" not in item or "value" not in item:
            raise ValueError(f"overrides[{n}] must contain index and value")

        row, col = parse_pair(item["index"], f"overrides[{n}].index")
        dx, dy = parse_pair(item["value"], f"overrides[{n}].value")

        if not (0 <= row < cfg["rows"] and 0 <= col < cfg["cols"]):
            raise ValueError(
                f"overrides[{n}].index ({row},{col}) is outside "
                f"{cfg['rows']}x{cfg['cols']} grid"
            )

        override_map[(row, col)] = (dx, dy)

    cfg["override_map"] = override_map
    return cfg


def color_distance_sq(a, b):
    dr = a[0] - b[0]
    dg = a[1] - b[1]
    db = a[2] - b[2]
    return dr * dr + dg * dg + db * db


def estimate_edge_background(img):
    """
    Estimate the background color from thin outer-edge bands using the median.
    This value is ONLY used to build the fuzzy background mask.
    """
    w, h = img.size
    px = img.load()
    band = max(2, min(w, h) // 100)

    samples = []

    for y in range(h):
        for x in range(band):
            samples.append(px[x, y])
            samples.append(px[w - 1 - x, y])

    for x in range(w):
        for y in range(band):
            samples.append(px[x, y])
            samples.append(px[x, h - 1 - y])

    return (
        int(median([c[0] for c in samples])),
        int(median([c[1] for c in samples])),
        int(median([c[2] for c in samples])),
    )


def make_background_mask(img, background_rgb, tolerance):
    """
    Fuzzy flood-fill from all image edges.

    The mask is used ONLY for center optimization and debug visualization.
    It never modifies exported icon pixels.
    """
    w, h = img.size
    px = img.load()

    max_rgb_distance_sq = 3 * (255 ** 2)
    threshold_sq = (tolerance ** 2) * max_rgb_distance_sq

    mask = Image.new("L", (w, h), 0)
    mp = mask.load()
    q = deque()

    def maybe_seed(x, y):
        if mp[x, y] == 0 and color_distance_sq(px[x, y], background_rgb) <= threshold_sq:
            mp[x, y] = 255
            q.append((x, y))

    for x in range(w):
        maybe_seed(x, 0)
        maybe_seed(x, h - 1)

    for y in range(h):
        maybe_seed(0, y)
        maybe_seed(w - 1, y)

    while q:
        x, y = q.popleft()

        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if not (0 <= nx < w and 0 <= ny < h):
                continue
            if mp[nx, ny] != 0:
                continue
            if color_distance_sq(px[nx, ny], background_rgb) > threshold_sq:
                continue

            mp[nx, ny] = 255
            q.append((nx, ny))

    return mask


def cleanup_background_mask(mask, kernel_size):
    if kernel_size == 0:
        return mask

    if kernel_size not in (3, 5, 7):
        raise ValueError("cleanup must be one of: 0, 3, 5, 7")

    return (
        mask
        .filter(ImageFilter.MaxFilter(kernel_size))
        .filter(ImageFilter.MinFilter(kernel_size))
    )


def crop_box_for_center(cx, cy, crop_size):
    half = crop_size / 2.0
    left = int(round(cx - half))
    top = int(round(cy - half))
    return (left, top, left + crop_size, top + crop_size)


def box_inside_image(box, w, h):
    l, t, r, b = box
    return l >= 0 and t >= 0 and r <= w and b <= h


def score_candidate(bg_mask, cx, cy, crop_size, balance_depth):
    """
    Score a candidate crop using opposing 1-pixel background slices.

    For each inward step k from 0..balance_depth-1:
      - compare top row (k) vs bottom row (k)
      - compare left col (k) vs right col (k)

    Lower score is better:
      perfect balance => score 0
    """
    w, h = bg_mask.size
    box = crop_box_for_center(cx, cy, crop_size)

    if not box_inside_image(box, w, h):
        return float("inf")

    l, t, r, b = box
    mp = bg_mask.load()

    depth = min(balance_depth, crop_size // 2)
    score = 0

    for k in range(depth):
        top_y = t + k
        bottom_y = b - 1 - k

        top_count = 0
        bottom_count = 0
        for x in range(l, r):
            if mp[x, top_y] != 0:
                top_count += 1
            if mp[x, bottom_y] != 0:
                bottom_count += 1

        left_x = l + k
        right_x = r - 1 - k

        left_count = 0
        right_count = 0
        for y in range(t, b):
            if mp[left_x, y] != 0:
                left_count += 1
            if mp[right_x, y] != 0:
                right_count += 1

        score += abs(top_count - bottom_count)
        score += abs(left_count - right_count)

    return score


def optimize_center(
    bg_mask,
    nominal_cx,
    nominal_cy,
    crop_size,
    search_radius,
    coarse_step,
    balance_depth,
):
    def score(cx, cy):
        return score_candidate(
            bg_mask=bg_mask,
            cx=cx,
            cy=cy,
            crop_size=crop_size,
            balance_depth=balance_depth,
        )

    best_cx = nominal_cx
    best_cy = nominal_cy
    best_score = score(best_cx, best_cy)

    for dy in range(-search_radius, search_radius + 1, coarse_step):
        for dx in range(-search_radius, search_radius + 1, coarse_step):
            cx = nominal_cx + dx
            cy = nominal_cy + dy
            candidate_score = score(cx, cy)

            if candidate_score < best_score:
                best_score = candidate_score
                best_cx = cx
                best_cy = cy

    fine_radius = max(2, coarse_step)
    coarse_best_cx = best_cx
    coarse_best_cy = best_cy

    for dy in range(-fine_radius, fine_radius + 1):
        for dx in range(-fine_radius, fine_radius + 1):
            cx = coarse_best_cx + dx
            cy = coarse_best_cy + dy
            candidate_score = score(cx, cy)

            if candidate_score < best_score:
                best_score = candidate_score
                best_cx = cx
                best_cy = cy

    return best_cx, best_cy, best_score



def postprocess_icon(crop, cut_edges, rescale_size, border_bytes, border_size):
    """
    Post-processing order:
      1. trim cut_edges pixels from all four sides
      2. force-resize to rescale_size x rescale_size
      3. center under an RGBA border overlay
    """
    out = crop

    if cut_edges:
        if cut_edges < 0:
            raise ValueError("cut_edges must be >= 0")

        new_w = out.width - (cut_edges * 2)
        new_h = out.height - (cut_edges * 2)

        if new_w <= 0 or new_h <= 0:
            raise ValueError(
                f"cut_edges={cut_edges} removes the entire icon "
                f"({out.width}x{out.height})"
            )

        out = out.crop((
            cut_edges,
            cut_edges,
            out.width - cut_edges,
            out.height - cut_edges,
        ))

    if rescale_size is not None:
        if rescale_size <= 0:
            raise ValueError("rescale_size must be > 0")
        out = out.resize(
            (rescale_size, rescale_size),
            Image.Resampling.LANCZOS,
        )

    if border_bytes is not None:
        border = Image.frombytes("RGBA", border_size, border_bytes)
        icon = out.convert("RGBA")

        canvas_w = max(icon.width, border.width)
        canvas_h = max(icon.height, border.height)
        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))

        icon_x = (canvas_w - icon.width) // 2
        icon_y = (canvas_h - icon.height) // 2
        border_x = (canvas_w - border.width) // 2
        border_y = (canvas_h - border.height) // 2

        canvas.alpha_composite(icon, (icon_x, icon_y))
        canvas.alpha_composite(border, (border_x, border_y))
        out = canvas

    return out


def debug_background_image(img, bg_mask):
    """
    Debug-only visualization:
    replace detected background pixels with hot pink.

    This never affects exported icon images.
    """
    out = img.copy()
    op = out.load()
    mp = bg_mask.load()

    for y in range(out.height):
        for x in range(out.width):
            if mp[x, y] != 0:
                op[x, y] = (255, 0, 255)

    return out


def debug_boxes_image(img, centers, crop_size):
    out = img.copy()
    draw = ImageDraw.Draw(out)

    for row, col, cx, cy, score, override in centers:
        box = crop_box_for_center(cx, cy, crop_size)
        l, t, r, b = box

        draw.rectangle(box, outline=(255, 255, 255), width=2)

        draw.line((cx - 5, cy, cx + 5, cy), fill=(255, 0, 255), width=2)
        draw.line((cx, cy - 5, cx, cy + 5), fill=(255, 0, 255), width=2)

        if override != (0, 0):
            draw.rectangle((l + 2, t + 2, l + 10, t + 10), fill=(255, 255, 0))

        label = f"{row},{col}  ({int(round(cx))},{int(round(cy))})"
        bbox = draw.textbbox((0, 0), label)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]

        tx = l + 4
        ty = t + 4

        if tx + tw + 4 > out.width:
            tx = max(0, out.width - tw - 4)
        if ty + th + 4 > out.height:
            ty = max(0, out.height - th - 4)

        draw.rectangle(
            (tx - 2, ty - 2, tx + tw + 2, ty + th + 2),
            fill=(0, 0, 0),
        )
        draw.text((tx, ty), label, fill=(255, 255, 0))

    return out


def process_row_worker(payload):
    (
        row,
        image_bytes,
        image_size,
        mask_bytes,
        rows,
        cols,
        crop_size,
        search_radius,
        coarse_step,
        balance_depth,
        cut_edges,
        rescale_size,
        border_bytes,
        border_size,
        output_dir,
        overrides,
    ) = payload

    w, h = image_size
    img = Image.frombytes("RGB", (w, h), image_bytes)
    bg_mask = Image.frombytes("L", (w, h), mask_bytes)

    cell_w = w / cols
    cell_h = h / rows

    results = []

    for col in range(cols):
        nominal_cx = (col + 0.5) * cell_w
        nominal_cy = (row + 0.5) * cell_h

        auto_cx, auto_cy, best_score = optimize_center(
            bg_mask=bg_mask,
            nominal_cx=nominal_cx,
            nominal_cy=nominal_cy,
            crop_size=crop_size,
            search_radius=search_radius,
            coarse_step=coarse_step,
            balance_depth=balance_depth,
        )

        override_dx, override_dy = overrides.get((row, col), (0, 0))

        final_cx = auto_cx + override_dx

        # User-facing Y override is inverted relative to image coordinates:
        # positive dy moves UP, negative dy moves DOWN.
        final_cy = auto_cy - override_dy

        box = crop_box_for_center(final_cx, final_cy, crop_size)

        if not box_inside_image(box, w, h):
            raise ValueError(
                f"Final crop for icon ({row},{col}) is outside the input image. "
                f"Auto center=({auto_cx:.1f},{auto_cy:.1f}), "
                f"override=({override_dx},{override_dy}), "
                f"crop_size={crop_size}"
            )

        crop = img.crop(box)
        crop = postprocess_icon(
            crop=crop,
            cut_edges=cut_edges,
            rescale_size=rescale_size,
            border_bytes=border_bytes,
            border_size=border_size,
        )

        filename = f"icon_r{row:02d}_c{col:02d}.png"
        crop.save(Path(output_dir) / filename, "PNG")

        results.append(
            (
                row,
                col,
                nominal_cx,
                nominal_cy,
                auto_cx,
                auto_cy,
                final_cx,
                final_cy,
                best_score,
                override_dx,
                override_dy,
            )
        )

    return results


def main():
    args = parse_args()

    config_path = args.config.expanduser().resolve()
    cfg = load_config(config_path)

    config_dir = config_path.parent
    config_stem = config_path.stem

    if args.input is None:
        input_path = config_dir / f"{config_stem}.png"
    else:
        input_path = args.input

    if args.output is None:
        output_path = config_dir / config_stem
    else:
        output_path = args.output

    input_path = input_path.expanduser().resolve()
    output_path = output_path.expanduser().resolve()

    if not input_path.exists():
        raise SystemExit(f"Input does not exist: {input_path}")

    output_path.mkdir(parents=True, exist_ok=True)

    img = Image.open(input_path).convert("RGB")
    w, h = img.size

    estimated_bg = estimate_edge_background(img)

    print(f"Image size: {w}x{h}")
    print(f"Grid: {cfg['rows']}x{cfg['cols']}")
    print(f"Estimated background (masking only): {estimated_bg}")
    print(f"Workers: {cfg['workers']}")

    bg_mask = make_background_mask(
        img,
        estimated_bg,
        cfg["tolerance"],
    )

    bg_mask = cleanup_background_mask(
        bg_mask,
        cfg["cleanup"],
    )

    cell_w = w / cfg["cols"]
    cell_h = h / cfg["rows"]

    if cfg["crop_size"] is None:
        crop_size = int(round(min(cell_w, cell_h) - cfg["min_spacing"]))
    else:
        crop_size = cfg["crop_size"]

    if crop_size <= 0:
        raise SystemExit(
            "Computed crop_size is <= 0. Set crop_size explicitly or lower min_spacing."
        )

    if cfg["balance_depth"] <= 0:
        raise SystemExit("balance_depth must be >= 1")

    print(f"Nominal cell: {cell_w:.2f} x {cell_h:.2f}")
    print(f"Crop size: {crop_size}x{crop_size}")
    print(f"Search radius: +/-{cfg['search_radius']}px")
    print(f"Balance depth: {cfg['balance_depth']} slice pairs")

    if cfg["override_map"]:
        print(f"Manual overrides: {len(cfg['override_map'])}")

    if cfg["cut_edges"] < 0:
        raise SystemExit("cut_edges must be >= 0")

    if cfg["rescale_size"] is not None and cfg["rescale_size"] <= 0:
        raise SystemExit("rescale_size must be > 0")

    border_bytes = None
    border_size = None

    if cfg["border_replacement"]:
        border_path = Path(cfg["border_replacement"]).expanduser()
        if not border_path.is_absolute():
            border_path = config_dir / border_path
        border_path = border_path.resolve()

        if not border_path.exists():
            raise SystemExit(f"Border replacement image does not exist: {border_path}")

        border_img = Image.open(border_path).convert("RGBA")
        border_size = border_img.size
        border_bytes = border_img.tobytes()

        print(f"Border replacement: {border_path} ({border_size[0]}x{border_size[1]})")

    if cfg["cut_edges"]:
        print(f"Cut edges: {cfg['cut_edges']}px per side")

    if cfg["rescale_size"] is not None:
        print(f"Rescale size: {cfg['rescale_size']}x{cfg['rescale_size']}")

    image_bytes = img.tobytes()
    mask_bytes = bg_mask.tobytes()

    payloads = [
        (
            row,
            image_bytes,
            img.size,
            mask_bytes,
            cfg["rows"],
            cfg["cols"],
            crop_size,
            cfg["search_radius"],
            cfg["coarse_step"],
            cfg["balance_depth"],
            cfg["cut_edges"],
            cfg["rescale_size"],
            border_bytes,
            border_size,
            str(output_path),
            cfg["override_map"],
        )
        for row in range(cfg["rows"])
    ]

    all_results = []

    if cfg["workers"] == 1:
        for payload in payloads:
            all_results.extend(process_row_worker(payload))
    else:
        with ProcessPoolExecutor(max_workers=cfg["workers"]) as executor:
            futures = [executor.submit(process_row_worker, p) for p in payloads]
            for future in as_completed(futures):
                all_results.extend(future.result())

    all_results.sort(key=lambda r: (r[0], r[1]))

    debug_centers = []

    for (
        row,
        col,
        nominal_cx,
        nominal_cy,
        auto_cx,
        auto_cy,
        final_cx,
        final_cy,
        best_score,
        override_dx,
        override_dy,
    ) in all_results:

        auto_dx = auto_cx - nominal_cx
        auto_dy = auto_cy - nominal_cy

        print(
            f"[{row},{col}] "
            f"auto_offset=({auto_dx:+.1f},{auto_dy:+.1f}) "
            f"override=({override_dx:+d},{override_dy:+d}) "
            f"final_center=({final_cx:.1f},{final_cy:.1f}) "
            f"score={best_score}"
        )

        debug_centers.append(
            (
                row,
                col,
                final_cx,
                final_cy,
                best_score,
                (override_dx, override_dy),
            )
        )

    if cfg["debug"]:
        debug_background_image(img, bg_mask).save(
            output_path / "_debug_background_mask.png",
            "PNG",
        )

        debug_boxes_image(img, debug_centers, crop_size).save(
            output_path / "_debug_crop_boxes.png",
            "PNG",
        )

    print(f"\nExported {cfg['rows'] * cfg['cols']} icons to: {output_path}")


if __name__ == "__main__":
    main()
