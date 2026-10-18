"""Prepare safe 64x64 review crops from completed 8x8 batches."""

import json
from pathlib import Path

from PIL import Image

WORKBENCH = Path(__file__).resolve().parent.parent
REPOSITORY = WORKBENCH.parent
MODS_ROOT = (REPOSITORY / "src/Mods").resolve()
GRID_ROWS = 8
GRID_COLUMNS = 8
ICON_SIZE = 64
GEOMETRY = {
    "size": 816, "icon_size": 90, "gutter": 10,
    "outer_margin": 13,
}


def edit_bounds(index):
    left = 13 + (index % GRID_COLUMNS) * 100
    top = 13 + (index // GRID_COLUMNS) * 100
    return [left, top, left + 90, top + 90]


def crop_bounds(edit_bounds):
    left, top, right, bottom = edit_bounds
    return [left + 1, top + 1, right - 1, bottom - 1]




def resolve_original(relative_path):
    if not isinstance(relative_path, str) or not relative_path.startswith("src/Mods/"):
        raise ValueError(f"Invalid original path: {relative_path!r}")
    path = (REPOSITORY / relative_path).resolve()
    if not path.is_relative_to(MODS_ROOT) or not path.is_file():
        raise ValueError(f"Original is missing or outside src/Mods: {relative_path}")
    return path


def batch_directory(batch_name):
    if not batch_name or Path(batch_name).name != batch_name or batch_name in (".", ".."):
        raise ValueError("The batch name must be a single directory name")
    batches_root = WORKBENCH / "batches"
    batch_dir = batches_root / batch_name
    if not batch_dir.resolve().is_relative_to(batches_root.resolve()):
        raise ValueError("The batch directory is outside batches")
    if not batches_root.resolve().is_relative_to(WORKBENCH.resolve()):
        raise ValueError("The batches directory is outside the workbench")
    return batch_dir


def read_manifest(batch_name):
    batch_dir = batch_directory(batch_name)
    manifest_path = batch_dir / "cells.json"
    if not manifest_path.resolve().is_relative_to(batch_dir.resolve()):
        raise ValueError("The batch manifest is outside the batch")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if not isinstance(manifest, dict):
        raise ValueError(f"Invalid manifest: {manifest_path}")
    return batch_dir, manifest


def discover_batch_names():
    names = []
    for path in sorted((WORKBENCH / "batches").glob("*/cells.json")):
        _, manifest = read_manifest(path.parent.name)
        if manifest.get("rows") != GRID_ROWS or manifest.get("columns") != GRID_COLUMNS:
            continue
        # Dry runs and failed requests have no sheet to review yet.
        if manifest.get("status") == "prepared_for_generation" and "sheet_file" not in manifest:
            continue
        names.append(path.parent.name)
    return names


def resolve_sheet(batch_dir, manifest):
    filename = manifest.get("sheet_file")
    if not isinstance(filename, str) or Path(filename).name != filename:
        raise ValueError(f"Expected a sheet_file filename in {batch_dir}")
    sheet = (batch_dir / filename).resolve()
    if not sheet.is_relative_to(batch_dir.resolve()) or not sheet.is_file():
        raise ValueError(f"Sheet is missing or outside {batch_dir}")
    with Image.open(sheet) as image:
        if image.size != (816, 816):
            raise ValueError(f"Expected an 816x816 sheet: {sheet}")
    return sheet


def validate_mapping(cell, catalog_ids, original_paths):
    catalog_id, source_path = cell.get("catalog_id"), cell.get("path")
    if not isinstance(catalog_id, str) or not catalog_id or catalog_id in catalog_ids:
        raise ValueError("Duplicate or invalid catalog ID")
    if not isinstance(source_path, str) or source_path in original_paths:
        raise ValueError("Duplicate or invalid original path")
    if any(not isinstance(cell.get(key), str) for key in ("label", "subject")):
        raise ValueError("Missing label or subject")
    with Image.open(resolve_original(source_path)) as image:
        if image.size != (ICON_SIZE, ICON_SIZE):
            raise ValueError(f"Original is not 64x64: {source_path}")
    catalog_ids.add(catalog_id)
    original_paths.add(source_path)


def validate_cell(cell, index, catalog_ids, original_paths):
    if not isinstance(cell, dict):
        raise ValueError("Invalid cell")
    if (cell.get("row"), cell.get("column")) != (index // 8 + 1, index % 8 + 1):
        raise ValueError("Cell order does not match the manifest")
    if cell.get("edit_bounds") != edit_bounds(index):
        raise ValueError("Cell edit_bounds do not match the 8x8 layout")
    validate_mapping(cell, catalog_ids, original_paths)


def load_batch(batch_name, catalog_ids, original_paths):
    batch_dir, manifest = read_manifest(batch_name)
    if (manifest.get("rows"), manifest.get("columns")) != (GRID_ROWS, GRID_COLUMNS):
        raise ValueError(f"Unexpected grid size in {batch_dir}")
    geometry = manifest.get("geometry", {})
    if not isinstance(geometry, dict) or any(geometry.get(k) != v for k, v in GEOMETRY.items()):
        raise ValueError(f"Unexpected geometry in {batch_dir}")
    cells = manifest.get("cells")
    if not isinstance(cells, list) or not 1 <= len(cells) <= 64:
        raise ValueError(f"Expected 1 to 64 mapped cells in {batch_dir}")
    sheet = resolve_sheet(batch_dir, manifest)
    for index, cell in enumerate(cells):
        validate_cell(cell, index, catalog_ids, original_paths)
    return batch_dir, sheet, cells


def create_crops(batch_name, batch_dir, sheet_path, cells):
    crop_dir = batch_dir / "review-crops"
    if not crop_dir.resolve().is_relative_to(batch_dir.resolve()):
        raise ValueError("The review crop directory is outside the batch")
    crop_dir.mkdir(exist_ok=True)
    with Image.open(sheet_path) as source_sheet:
        if source_sheet.size != (816, 816):
            raise ValueError(f"Expected an 816x816 sheet: {sheet_path}")
        sheet = source_sheet.convert("RGB")
        for cell in cells:
            crop_path = crop_dir / f"r{cell['row']:02}-c{cell['column']:02}.png"
            if crop_path.is_symlink():
                raise ValueError("A review crop cannot be a symbolic link")
            sheet.crop(crop_bounds(cell["edit_bounds"])).resize(
                (ICON_SIZE, ICON_SIZE), Image.Resampling.LANCZOS
            ).save(crop_path, format="PNG", optimize=True)
    geometry = {
        "source_sheet": sheet_path.name, "source_size": [816, 816],
        "rows": GRID_ROWS, "columns": GRID_COLUMNS,
        "crop_rule": "One-pixel inset on every side of edit_bounds, excluding edge bleed, gutters, and margins.",
        "output_size": [ICON_SIZE, ICON_SIZE],
    }
    geometry_path = crop_dir / "geometry.json"
    if geometry_path.is_symlink():
        raise ValueError("Crop geometry cannot be a symbolic link")
    geometry_path.write_text(json.dumps(geometry, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(cells)} crops for {batch_name} from 816x816.")


def main():
    catalog_ids, original_paths = set(), set()
    batches = [(name, *load_batch(name, catalog_ids, original_paths))
               for name in discover_batch_names()]
    for batch_name, batch_dir, sheet_path, cells in batches:
        create_crops(batch_name, batch_dir, sheet_path, cells)
    print(f"Validated {len(catalog_ids)} unique catalog mappings.")


if __name__ == "__main__":
    main()
