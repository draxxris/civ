#!/usr/bin/env python3
"""Build the procedural terrain improvement models into src/Mods/<mod>/art/models/<id suffix>.glb (World by default).

Run: python3 tools/art/improvement_models.py [--only NAME ...]

Each module in tools/art/improvements/ (besides kit) defines MODELS: a dict from the content id suffix (the part
after "world:", or after the prefix of the mod named by the "mod" extra below) to a builder that returns a list of
parts, or to a (builder, clear) tuple where clear is "tile" (the default: no scatter props on the tile) or "centre"
(props stay, only the middle is cleared). A third tuple item may be a dict of scene extras, e.g. {"faceRiver": True}:
the game puts the model's origin on the near waterline of the river, +X straight across the water. The extra "mod" is not written: it names the mod that owns
the content id (default "World"), so the model goes to that mod's art/models folder, e.g. {"mod": "Cities"} for a
cities:<suffix> building. Conventions are in improvements/kit.py.
"""
from __future__ import annotations

import argparse
import importlib

from improvements.kit import OUT, REPO_ROOT, write

GROUPS = ["settlements", "extraction", "farming", "industry", "forestry", "military", "landmarks", "water", "walls"]


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
    for name, (build, clear, extras) in models().items():
        if args.only and not any(o in name for o in args.only):
            continue
        extras = dict(extras)
        mod = extras.pop("mod", "World")
        out = OUT if mod == "World" else REPO_ROOT / f"src/Mods/{mod}/art/models"
        out.mkdir(parents=True, exist_ok=True)
        size, triangles = write(out / f"{name}.glb", build(), clear, **extras)
        print(f"{out.relative_to(REPO_ROOT)}/{name}.glb: {triangles} triangles, {size} bytes, clear={clear}"
              + (f", extras={extras}" if extras else ""))


if __name__ == "__main__":
    main()
