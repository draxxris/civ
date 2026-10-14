"""Read the extraction's active XML trees and resolve case-insensitive asset paths."""
import json
import struct
from pathlib import Path


def descendants(node, name):
    if node[0] == name:
        yield node
    if isinstance(node[1], list):
        for child in node[1]:
            yield from descendants(child, name)


def field(node, name, default=""):
    return next((c[1] for c in node[1] if c[0] == name), default)


def read_tree(path, data):
    values = {}
    for line in path.read_text().splitlines():
        key, sep, value = line.partition(":")
        if sep and key in ("tree", "treeYaml"):
            values[key] = json.loads(value)
    if "tree" in values:
        return values["tree"]
    return read_tree(data / values["treeYaml"], data)


def core_files(data):
    core = {}
    for layer in ("base", "warlords", "bts", "and"):
        folder = data / "layers" / layer / "XML"
        for p in sorted(folder.rglob("*.xml.yaml")):
            if "UnitInfos" in p.name or "ArtDefines_Unit." in p.name:
                core[p.relative_to(folder).as_posix()] = p
    return [core[k] for k in sorted(core)]


def profile_files(data):
    """Match ContentImport's whole-file fallback and nested default MLF order."""
    yield from core_files(data)
    text = "\n".join(l for l in (data / "module-controls.yaml").read_text().splitlines()
                     if not l.startswith("#"))
    controls = {str(Path(c["path"]).parent): c for c in json.loads(text)["controls"]
                if c["layer"] == "and"}

    for directory in active_modules("Modules", controls):
        for p in sorted((data / "layers" / "and" / directory).rglob("*.xml.yaml")):
            if ("UnitInfos" in p.name or "ArtDefines_Unit" in p.name) and "Schema" not in p.name:
                yield p


def active_modules(directory, controls):
    control = controls.get(directory)
    if control is None:
        yield directory
        return
    config = next(c for c in control["configurations"]
                  if c["type"] == control["defaultConfiguration"])
    for module in config["modules"]:
        if module["load"] in ("1", "true"):
            yield from active_modules(directory + "/" + module["directory"], controls)


def definitions(data):
    units, art = {}, {}
    for path in profile_files(data):
        tree = read_tree(path, data)
        for kind, table in (("UnitInfo", units), ("UnitArtInfo", art)):
            for node in descendants(tree, kind):
                if field(node, "bForceDelete") in ("1", "true"):
                    continue
                table[field(node, "Type")] = node
    return units, art


class Assets:
    def __init__(self, root):
        self.root = root.resolve()
        self.paths = {p.relative_to(self.root).as_posix().lower(): p
                      for p in sorted(self.root.rglob("*")) if p.is_file()}
        self.basenames = {}
        for p in self.paths.values():
            self.basenames.setdefault(p.name.lower(), []).append(p)

    def resolve(self, name, folders=()):
        name = name.decode("utf-8", errors="replace") if isinstance(name, bytes) else name
        name = name.replace("\\", "/").strip().lower()
        if not name or name == "none":
            return None
        for folder in folders:
            key = (folder / name).resolve().relative_to(self.root).as_posix().lower() if not name.startswith("/") else ""
            if key in self.paths:
                return self.paths[key]
            key = (folder / Path(name).name).relative_to(self.root).as_posix().lower()
            if key in self.paths:
                return self.paths[key]
        if name in self.paths:
            return self.paths[name]
        # NIF texture names sometimes contain an artist's absolute Windows path.
        if "/art/" in name:
            suffix = "art/" + name.split("/art/", 1)[1]
            if suffix in self.paths:
                return self.paths[suffix]
        matches = self.basenames.get(Path(name).name, [])
        return matches[0] if len(matches) == 1 else None


def kfm_references(path):
    """Read length-prefixed filenames, without interpreting the KFM transition graph.

    Both 1.2.4b and 2.0.0.0b use uint32-length strings. Requiring a complete,
    printable NIF/KF filename prevents transition integers from becoming paths.
    """
    if path is None:
        return []
    data = path.read_bytes()
    if not data.startswith(b";Gamebryo KFM File Version "):
        raise ValueError("Invalid KFM header")
    result = []
    for pos in range(len(data) - 4):
        size = struct.unpack_from("<I", data, pos)[0]
        if not 4 < size < 512 or pos + 4 + size > len(data):
            continue
        raw = data[pos + 4:pos + 4 + size]
        if raw.lower().endswith((b".kf", b".nif")) and all(32 <= b < 127 for b in raw):
            result.append(raw.decode("ascii"))
    return list(dict.fromkeys(result))
