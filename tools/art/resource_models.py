"""Build the map resource (bonus) models into src/Mods/World/art/models/<id suffix>.glb. Runs inside Blender:

    blender -b --factory-startup -P tools/art/resource_models.py -- [--only NAME ...] [--out DIR]

Each module in tools/art/resources/ (besides kit) defines MODELS: a dict from the bonus id suffix (the part after
"world:") to a builder that returns a list of parts, or to (builder, options) where options may set "sea": True (the
model floats at sea level and gets no ground in its baked occlusion). Conventions are in resources/kit.py; the
terrain improvements are built separately by improvement_models.py.
"""
from __future__ import annotations

import argparse
import importlib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

GROUPS = ["plants", "animals", "minerals", "sea"]


def models() -> dict:
    found = {}
    for group in GROUPS:
        module = importlib.import_module(f"resources.{group}")
        for name, entry in module.MODELS.items():
            if name in found:
                raise ValueError(f"{name} is defined twice")
            build, options = entry if isinstance(entry, tuple) else (entry, {})
            found[name] = (build, options)
    return found


def main(argv):
    from resources import kit

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="*", help="build only these id suffixes (exact names)")
    parser.add_argument("--out", type=Path, default=kit.OUT, help="output folder (default: the World mod's models)")
    args = parser.parse_args(argv)
    for name, (build, options) in models().items():
        if args.only and name not in args.only:
            continue
        kit.clear_scene()
        parts = build()
        reach = max(part.extent() for part in parts)
        objects = kit.build(name, parts, sea=options.get("sea", False))
        size, triangles = kit.export(args.out / f"{name}.glb", objects, {"clear": "tile"})
        print(f"{name}.glb: {triangles} triangles, {size} bytes, reach {reach:.3f}")


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
