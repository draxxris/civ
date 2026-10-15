"""Prepare safe 64x64 review crops from the four mapped sheets."""

import json
from pathlib import Path

from PIL import Image

WORKBENCH = Path(__file__).resolve().parent
REPOSITORY = WORKBENCH.parent
MODS_ROOT = (REPOSITORY / "src/Mods").resolve()
BATCH_NAMES = ("cities-001", "cities-002", "cities-003", "cities-004")
GRID_ROWS = 12
GRID_COLUMNS = 12
ICON_SIZE = 64


def resolve_original(relative_path):
    if not isinstance(relative_path, str) or not relative_path.startswith("src/Mods/"):
        raise ValueError(f"Invalid original path: {relative_path!r}")

    path = (REPOSITORY / relative_path).resolve()
    if not path.is_relative_to(MODS_ROOT) or not path.is_file():
        raise ValueError(f"Original is missing or outside src/Mods: {relative_path}")
    return path


def load_batch(batch_name, catalog_ids, original_paths):
    batch_dir = WORKBENCH / "batches" / batch_name
    manifest_path = batch_dir / "cells.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    cells = manifest.get("cells")
    if manifest.get("rows") != GRID_ROWS or manifest.get("columns") != GRID_COLUMNS:
        raise ValueError(f"Unexpected grid size in {manifest_path}")
    if not isinstance(cells, list) or len(cells) != GRID_ROWS * GRID_COLUMNS:
        raise ValueError(f"Expected 144 cells in {manifest_path}")

    sheets = sorted(batch_dir.glob("sheet-*.png"))
    if len(sheets) != 1 or not sheets[0].resolve().is_relative_to(batch_dir.resolve()):
        raise ValueError(f"Expected one sheet in {batch_dir}")

    for index, cell in enumerate(cells):
        row = index // GRID_COLUMNS + 1
        column = index % GRID_COLUMNS + 1
        if cell.get("row") != row or cell.get("column") != column:
            raise ValueError(f"Cell order does not match the manifest in {manifest_path}")

        catalog_id = cell.get("catalog_id")
        source_path = cell.get("path")
        if not isinstance(catalog_id, str) or catalog_id in catalog_ids:
            raise ValueError(f"Duplicate or invalid catalog ID in {manifest_path}")
        if not isinstance(source_path, str) or source_path in original_paths:
            raise ValueError(f"Duplicate or invalid original path in {manifest_path}")
        catalog_ids.add(catalog_id)
        original_paths.add(source_path)

        original = resolve_original(source_path)
        with Image.open(original) as source_icon:
            if source_icon.size != (ICON_SIZE, ICON_SIZE):
                raise ValueError(f"Original is not 64x64: {source_path}")

        for key in ("label", "subject"):
            if not isinstance(cell.get(key), str):
                raise ValueError(f"Missing {key} in {manifest_path}")

    return batch_dir, sheets[0], cells


def create_crops(batch_name, batch_dir, sheet_path, cells):
    crop_dir = batch_dir / "review-crops"
    crop_dir.mkdir(exist_ok=True)

    with Image.open(sheet_path) as source_sheet:
        width, height = source_sheet.size
        if width == 0 or height == 0:
            raise ValueError(f"Invalid sheet dimensions: {sheet_path}")
        sheet = source_sheet.convert("RGB")

        for index, cell in enumerate(cells):
            row_index = index // GRID_COLUMNS
            column_index = index % GRID_COLUMNS
            left = column_index * width // GRID_COLUMNS
            right = (column_index + 1) * width // GRID_COLUMNS
            top = row_index * height // GRID_ROWS
            bottom = (row_index + 1) * height // GRID_ROWS
            crop = sheet.crop((left, top, right, bottom))
            crop.resize((ICON_SIZE, ICON_SIZE), Image.Resampling.LANCZOS).save(
                crop_dir / f"r{cell['row']:02}-c{cell['column']:02}.png",
                format="PNG",
                optimize=True,
            )

    geometry = {
        "source_sheet": sheet_path.name,
        "source_size": [width, height],
        "rows": GRID_ROWS,
        "columns": GRID_COLUMNS,
        "crop_rule": "Equal divisions use floor(index * dimension / cell count).",
        "output_size": [ICON_SIZE, ICON_SIZE],
    }
    (crop_dir / "geometry.json").write_text(
        json.dumps(geometry, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Prepared {len(cells)} crops for {batch_name} from {width}x{height}.")


def main():
    catalog_ids = set()
    original_paths = set()
    batches = [
        (name, *load_batch(name, catalog_ids, original_paths))
        for name in BATCH_NAMES
    ]
    for batch_name, batch_dir, sheet_path, cells in batches:
        create_crops(batch_name, batch_dir, sheet_path, cells)
    print(f"Validated {len(catalog_ids)} unique catalog mappings.")


if __name__ == "__main__":
    main()
