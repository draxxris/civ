#!/usr/bin/env python3
"""Build the procedural terrain improvement models into src/Mods/World/art/models/<id suffix>.glb.

Run: python3 tools/art/improvement_models.py [--only NAME ...]

Each module in tools/art/improvements/ (besides kit) defines MODELS: a dict from the improvement id suffix (the part
after "world:") to a builder that returns a list of parts, or to a (builder, clear) tuple where clear is "tile"
(the default: no scatter props on the tile) or "centre" (props stay, only the middle is cleared). A third tuple item
may be a dict of scene extras, e.g. {"faceRiver": True}: the game turns the model so its +X side faces the river. Conventions are in
improvements/kit.py.
"""
from __future__ import annotations

import argparse
import importlib

from improvements.kit import OUT, write

GROUPS = ["settlements", "extraction", "farming", "industry", "forestry", "military", "landmarks", "water"]


def models() -> dict:
    found = {}
    for group in GROUPS:
        try:
            module = importlib.import_module(f"improvements.{group}")
        except ModuleNotFoundError as e:
            if e.name != f"improvements.{group}":
                raise
            continue
        for name, entry in module.MODELS.items():
            if name in found:
                raise ValueError(f"{name} is defined twice")
            found[name] = (entry if isinstance(entry, tuple) else (entry, "tile"))
            found[name] += ({},) * (3 - len(found[name]))
    return found


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="*", help="build only these id suffixes (substring match)")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    for name, (build, clear, extras) in models().items():
        if args.only and not any(o in name for o in args.only):
            continue
        size, triangles = write(OUT / f"{name}.glb", build(), clear, **extras)
        print(f"{name}.glb: {triangles} triangles, {size} bytes, clear={clear}" + (f", extras={extras}" if extras else ""))


if __name__ == "__main__":
    main()
