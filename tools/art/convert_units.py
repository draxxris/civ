#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11,<3.12"
# dependencies = ["pyffi==2.2.3", "numpy==2.4.6", "Pillow==12.3.0"]
# ///
"""Convert the active unit registry's NIF/KFM/KF/DDS art to self-contained GLBs.

Run: python3 tools/art/convert_units.py
Spike: python3 tools/art/convert_units.py --units warrior archer axeman

Python 3.11 with PyFFI, NumPy, and Pillow is sufficient. If dependencies are
missing, uv runs the pinned environment from the script metadata. No Blender,
source changes, external textures, or runtime Python dependency is required.
"""
import argparse
import hashlib
import importlib.util
import json
import logging
import os
from pathlib import Path
import shutil
import sys
import time

# PyFFI 2.2.3 still calls the removed time.clock function during XML loading.
time.clock = time.perf_counter
sys.dont_write_bytecode = True
if any(importlib.util.find_spec(m) is None for m in ("pyffi", "numpy", "PIL")):
    uv = shutil.which("uv")
    if not uv:
        raise SystemExit("Install uv, or install pyffi==2.2.3, numpy==2.4.6, and Pillow==12.3.0 in Python 3.11.")
    os.execv(uv, [uv, "run", "--python", "3.11", "--script", str(Path(__file__).resolve()), *sys.argv[1:]])

from pyffi.formats.nif import NifFormat as N
from animation_tracks import choose_clips, convert_animation
from source_assets import Assets, definitions, descendants, field, kfm_references
from unit_geometry import geometry
import visibility

ROOT = Path(__file__).resolve().parents[2]


def read_nif(path):
    data = N.Data()
    with path.open("rb") as stream:
        data.read(stream)
    return data


def source_symbols(root):
    path = root / "tools/Civ.ContentImport/reports/id-map.json"
    if not path.exists():
        return {}
    return {r["id"]: r["sourceType"] for r in json.loads(path.read_text())["records"]
            if r["class"] == "UnitInfo"}


def resolve_art(unit, symbols, units, art):
    name = unit["id"].split(":", 1)[1]
    symbol = symbols.get(unit["id"], "UNIT_" + name.upper())
    source = units.get(symbol)
    if source is None:
        raise ValueError(f"No source UnitInfo for {symbol}")
    groups = list(descendants(source, "EarlyArtDefineTag"))
    if not groups:
        raise ValueError("UnitInfo has no early art group")
    tag = groups[0][1]
    if tag not in art:
        raise ValueError("No UnitArtInfo for " + tag)
    return tag, art[tag], len(groups)


def animation_files(assets, kfm, nif):
    references = kfm_references(kfm)
    folders = [kfm.parent] if kfm else [nif.parent]
    files = [assets.resolve(r, folders) for r in references if r.lower().endswith(".kf")]
    if not files:
        files = sorted(folders[0].glob("*.kf"))
    return [p for p in files if p is not None]


def provenance(path, assets):
    return path.relative_to(assets.root).as_posix()


def convert_unit(unit, context):
    assets, units, art, symbols, output = context
    tag, definition, group_count = resolve_art(unit, symbols, units, art)
    nif_name = field(definition, "NIF")
    nif = assets.resolve(nif_name)
    if nif is None:
        nif = assets.resolve(field(definition, "SHADERNIF"))
    if nif is None:
        raise ValueError("Missing NIF: " + nif_name)
    kfm_name = field(definition, "KFM")
    kfm = assets.resolve(kfm_name)
    warnings = []
    if kfm is None and kfm_name not in ("", "None", "NONE"):
        warnings.append("Missing KFM: " + kfm_name)
    clip_data = read_clips(choose_clips(animation_files(assets, kfm, nif)), warnings)
    tracks = {name: visibility.clip_tracks(data) for name, (_, data) in clip_data.items()}
    controlled = frozenset(n for clip in tracks.values() for n in clip)
    idle = tracks.get("idle", {})
    glb, node_names, triangles, bounds, vis = geometry(
        read_nif(nif), assets, [nif.parent] + ([kfm.parent] if kfm else []), warnings,
        controlled, lambda n: visibility.visible_at(idle[n], 0) if n in idle else True)
    animation_sources, channels = append_animations(glb, clip_data, tracks, node_names, vis, assets, warnings)
    if not glb.doc["images"]:
        warnings.append("No diffuse texture resolved")
    if group_count > 1:
        warnings.append(f"First of {group_count} formation art groups exported")
    name = unit["id"].split(":", 1)[1]
    if not name or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789_" for c in name):
        raise ValueError("Unsafe unit output name")
    path = output / (name + ".glb")
    size = glb.write(path)
    return {"id": unit["id"], "status": "converted", "art": tag,
            "nif": provenance(nif, assets), "kfm": provenance(kfm, assets) if kfm else None,
            "sourceScale": field(definition, "fScale"), "bounds": bounds,
            "triangles": triangles, "skins": len(glb.doc["skins"]),
            "joints": sum(len(s["joints"]) for s in glb.doc["skins"]),
            "textures": len(glb.doc["images"]), "clips": channels,
            "animationSources": animation_sources, "bytes": size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "warnings": list(dict.fromkeys(warnings))}


def read_clips(clips, warnings):
    result = {}
    for name, path in clips.items():
        try:
            result[name] = (path, read_nif(path))
        except (ValueError, AttributeError, IndexError, TypeError, ZeroDivisionError) as error:
            warnings.append(f"{name}: {type(error).__name__}: {error}")
    return result


def append_animations(glb, clip_data, tracks, node_names, vis, assets, warnings):
    animation_sources, channels = {}, {}
    for name, (path, data) in clip_data.items():
        def add_visibility(animation, length, clip=tracks[name]):
            visibility.append_channels(glb, animation, vis.nodes, clip, vis.defaults, length)
        try:
            count = convert_animation(glb, data, name, node_names, warnings, add_visibility)
            if count:
                channels[name] = count
                animation_sources[name] = provenance(path, assets)
        except (ValueError, AttributeError, IndexError, TypeError, ZeroDivisionError) as error:
            warnings.append(f"{name}: {type(error).__name__}: {error}")
    return animation_sources, channels


def write_report(path, rows, source, selected):
    converted = [r for r in rows if r["status"] == "converted"]
    animated = [r for r in converted if r["clips"]]
    skinned = [r for r in converted if r["skins"]]
    textured = [r for r in converted if r["textures"]]
    lines = ["# Unit art conversion report", "", "## Run", "",
             f"- Registry: `src/Mods/Military/data/unit.json` ({len(rows)} units in this run).",
             f"- Source: `{source}`.",
             f"- Converted: **{len(converted)}**. Failed: **{len(rows)-len(converted)}**.",
             f"- Animated: **{len(animated)}**. Skinned: **{len(skinned)}**. Textured: **{len(textured)}**.",
             f"- Output: `src/Mods/Military/art/models/<unit-id-suffix>.glb`.",
             f"- Total GLB size: {sum(r['bytes'] for r in converted):,} bytes.",
             "- The report itself is the art registry. Each unit section identifies its source and output status.",
             "", "## Reproduction", "", "```sh", "python3 tools/art/convert_units.py",
             "# Three-unit spike:", "python3 tools/art/convert_units.py --units warrior archer axeman", "```", "",
             "The script uses Python 3.11, PyFFI 2.2.3, NumPy 2.4.6, and Pillow 12.3.0.",
             "If dependencies are absent, uv creates the pinned environment. The first run needs network access.",
             "The converter reads active core files with whole-file layer fallback and the default nested MLF module profile.",
             "The importer ID map resolves source symbols when content IDs differ from source symbols.",
             "", "## Conversion contract", "",
             "- NIF node hierarchies remain intact. Skins use source bone offsets, weights, and joint transforms.",
             "- Triangle strips become indexed triangles. Missing normals come from triangle geometry.",
             "- DDS diffuse textures become embedded RGBA PNG images. No GLB references an external file.",
             "- The root rotates source Z-up coordinates into Y-up coordinates. Source -Y faces output +Z.",
             "- The bind-pose bounds set feet at Y=0 and center the model horizontally.",
             "- Height is at most 0.60 world units. Horizontal radius is at most 0.48 world units.",
             "- Wide ships and vehicles can be shorter than 0.45 units to fit the hex.",
             "- Source fScale values appear in this report. Per-model bounds replace these source scene scale values.",
             "- KFM filenames select KF sequences. Output names are idle, run, fortify, attack, hurt, and die.",
             "- Missing source clips remain absent. No procedural or copied animation replaces a missing source clip.",
             "- Transform keys preserve source timing. Quaternion keys use normalized, shortest-path rotations.",
             "- Compressed and uncompressed cubic B-spline controls evaluate at 30 samples per second.",
             "- Quadratic vector keys evaluate with Hermite interpolation. TBC and quaternion curves use linear segments.",
             "", "## Limits", "",
             "The converter exports the first early-era art group, not the full multi-figure formation or civilization variants.",
             "Material, particle, sound, and effect-controller animations do not become glTF transform tracks.",
             "Visibility controllers become STEP scale channels on inserted _Vis nodes and skinned-mesh proxy joints. Hidden subtrees scale to 1e-6.",
             "Hidden geometry does not export. Runtime projectiles and source shaders need separate renderer support.",
             "Team-color decal textures composite over a neutral blue-gray background. This conversion does not implement dynamic team colors.",
             "Zero-scale nodes use a scale of 0.000001 because Godot cannot decompose singular bases.",
             "The report lists missing assets and unsupported bindings. Failed units do not receive substitute models.",
             "A source clip without matching transform targets does not count as an exported animation.", "",
             "## Per-unit registry", ""]
    if selected:
        lines.insert(4, "- This is a selected-unit spike, not a complete registry conversion.")
    for row in rows:
        lines.extend(report_unit(row))
    path.write_text("\n".join(lines))


def report_unit(row):
        name = row["id"].split(":", 1)[1]
        lines = [f"### {row['id']}", ""]
        if row["status"] != "converted":
            return lines + ["- Failed: " + row["error"].replace("\n", " "), ""]
        b = row["bounds"]
        lines += [f"- Model: `src/Mods/Military/art/models/{name}.glb` ({row['bytes']:,} bytes).",
                  f"- Art: `{row['art']}`. Source scale: {row['sourceScale']}.",
                  f"- NIF: `{row['nif']}`.", f"- KFM: `{row['kfm'] or 'none'}`.",
                  f"- Geometry: {row['triangles']} triangles, {row['skins']} skins, {row['joints']} joint bindings, {row['textures']} textures.",
                  f"- Bounds: height {b['height']:.4f}, radius {b['radius']:.4f}, feet Y=0.",
                  "- Clips: " + (", ".join(f"{k} ({v} channels)" for k, v in row['clips'].items()) or "none") + ".",
                  f"- SHA-256: `{row['sha256']}`."]
        for clip, asset in row["animationSources"].items():
            lines.append(f"- {clip}: `{asset}`.")
        for warning in row["warnings"]:
            lines.append("- Note: " + warning.replace("\n", " "))
        lines.append("")
        return lines


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / "AND-extraction")
    parser.add_argument("--units", nargs="*", help="Unit ID suffixes for a conversion spike")
    args = parser.parse_args()
    logging.getLogger("pyffi").setLevel(logging.ERROR)
    registry = ROOT / "src/Mods/Military/data/unit.json"
    units_to_convert = json.loads(registry.read_text())
    if args.units:
        requested = {u.removeprefix("military:") for u in args.units}
        units_to_convert = [u for u in units_to_convert if u["id"].split(":", 1)[1] in requested]
        unknown = requested - {u["id"].split(":", 1)[1] for u in units_to_convert}
        if unknown:
            parser.error("Unknown units: " + ", ".join(sorted(unknown)))
    units, art = definitions(args.source / "data")
    assets = Assets(args.source / "media/assets")
    output = ROOT / "src/Mods/Military/art/models"
    context = assets, units, art, source_symbols(ROOT), output
    rows = []
    for unit in units_to_convert:
        try:
            row = convert_unit(unit, context)
            print(f"{unit['id']}: {row['triangles']} triangles, {row['skins']} skins, clips={','.join(row['clips'])}", flush=True)
        except Exception as error:
            row = {"id": unit["id"], "status": "failed", "error": f"{type(error).__name__}: {error}"}
            # A prior success must not survive as a stale model after a failed run.
            (output / (unit["id"].split(":", 1)[1] + ".glb")).unlink(missing_ok=True)
            print(unit["id"] + ": FAILED: " + row["error"], flush=True)
        rows.append(row)
    write_report(ROOT / "tools/art/report.md", rows, args.source, bool(args.units))
    failures = sum(r["status"] == "failed" for r in rows)
    print(f"Converted {len(rows)-failures}/{len(rows)} units. Report: tools/art/report.md")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
