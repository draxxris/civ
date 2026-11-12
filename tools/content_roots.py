"""Where content modules live, for the Python tools that read or write per-module folders.

The base game's modules (Foundation, World, Cities, ...) are folders under src/Ariel.Rules; every other mod is a
folder under src/Mods. Both keep the same layout, <root>/<Module>/{mod.json, data/, art/}, and folder names are
unique across the two roots, so a tool can address a module by folder name alone.
"""
from __future__ import annotations

from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
RULES = REPO / "src" / "Ariel.Rules"
MODS = REPO / "src" / "Mods"
ROOTS = (RULES, MODS)


def module_dirs() -> list[Path]:
    """Every module folder (one holding a mod.json) under either root, sorted by folder name."""
    return sorted((manifest.parent for root in ROOTS for manifest in root.glob("*/mod.json")), key=lambda p: p.name)


def module_dir(name: str) -> Path:
    """The folder of the module named `name` (e.g. "World", "Trade"), under whichever root holds it."""
    for root in ROOTS:
        if (root / name / "mod.json").is_file():
            return root / name
    raise FileNotFoundError(f"no module folder {name!r} under {', '.join(str(r.relative_to(REPO)) for r in ROOTS)}")
