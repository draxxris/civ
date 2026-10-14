"""Small, deterministic binary glTF 2.0 writer. Matrices use column vectors."""
import io
import json
import re
import struct

import numpy as np
from PIL import Image


def text(value):
    return value.decode("utf-8", errors="replace") if isinstance(value, bytes) else str(value)


def matrix(value):
    # PyFFI stores matrices for row-vector multiplication.
    return np.array(value.as_list(), dtype=float).T


def quaternion(value):
    q = np.array([value.x, value.y, value.z, value.w], dtype=float)
    norm = np.linalg.norm(q)
    if not np.isfinite(norm) or norm < 1e-10:
        raise ValueError("Invalid quaternion")
    return q / norm


def rotation_quaternion(r):
    # Symmetric eigensystem avoids divisions at 180-degree rotations.
    m = np.array([
        [r[0, 0]-r[1, 1]-r[2, 2], r[0, 1]+r[1, 0], r[0, 2]+r[2, 0], r[2, 1]-r[1, 2]],
        [r[0, 1]+r[1, 0], r[1, 1]-r[0, 0]-r[2, 2], r[1, 2]+r[2, 1], r[0, 2]-r[2, 0]],
        [r[0, 2]+r[2, 0], r[1, 2]+r[2, 1], r[2, 2]-r[0, 0]-r[1, 1], r[1, 0]-r[0, 1]],
        [r[2, 1]-r[1, 2], r[0, 2]-r[2, 0], r[1, 0]-r[0, 1], r.trace()]], dtype=float)
    _, vectors = np.linalg.eigh(m)
    q = vectors[:, -1]
    q /= np.linalg.norm(q)
    q = np.clip(q, -1, 1)
    return q if q[3] >= 0 else -q


def decompose(m):
    scale = np.linalg.norm(m[:3, :3], axis=0)
    if np.linalg.det(m[:3, :3]) < 0:
        scale[0] *= -1
    r = m[:3, :3] / np.where(np.abs(scale) > 1e-12, scale, 1)
    u, _, vt = np.linalg.svd(r)
    if np.linalg.det(u @ vt) < 0:
        u[:, -1] *= -1
    # Godot cannot decompose singular zero-scale bases during scene generation.
    scale = np.where(np.abs(scale) < 1e-6, 1e-6, scale)
    return {"translation": m[:3, 3].tolist(),
            "rotation": rotation_quaternion(u @ vt).tolist(), "scale": scale.tolist()}


def transform_points(m, points):
    p = np.asarray(points)
    return p @ m[:3, :3].T + m[:3, 3]


class Glb:
    def __init__(self):
        self.doc = {"asset": {"version": "2.0", "generator": "unit-art-converter"},
                    "scene": 0, "scenes": [{"nodes": [0]}],
                    "nodes": [{"name": "Unit", "children": []}],
                    "meshes": [], "materials": [], "skins": [], "animations": [],
                    "accessors": [], "bufferViews": [], "images": [], "textures": [],
                    "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]}
        self.binary = bytearray()
        self.texture_cache = {}

    def view(self, data, target=None):
        self.binary.extend(b"\0" * (-len(self.binary) % 4))
        item = {"buffer": 0, "byteOffset": len(self.binary), "byteLength": len(data)}
        if target:
            item["target"] = target
        self.binary.extend(data)
        self.doc["bufferViews"].append(item)
        return len(self.doc["bufferViews"]) - 1

    def accessor(self, values, kind, dtype="<f4", target=None, bounds=False):
        arr = np.asarray(values, dtype=dtype)
        if not arr.size or not np.all(np.isfinite(arr)):
            raise ValueError("Empty or non-finite accessor")
        types = {"<f4": 5126, "<u2": 5123, "<u4": 5125}
        item = {"bufferView": self.view(arr.tobytes(), target), "componentType": types[dtype],
                "count": len(arr), "type": kind}
        if bounds:
            a = arr.reshape(len(arr), -1)
            item.update(min=a.min(axis=0).tolist(), max=a.max(axis=0).tolist())
        self.doc["accessors"].append(item)
        return len(self.doc["accessors"]) - 1

    def texture(self, path, team_color=False):
        key = (path, team_color)
        if key in self.texture_cache:
            return self.texture_cache[key]
        with Image.open(path) as source:
            image = source.convert("RGBA")
        if team_color:
            background = Image.new("RGBA", image.size, (110, 135, 165, 255))
            image = Image.alpha_composite(background, image)
        stream = io.BytesIO()
        image.save(stream, format="PNG", optimize=False)
        self.doc["images"].append({"bufferView": self.view(stream.getvalue()), "mimeType": "image/png"})
        self.doc["textures"].append({"sampler": 0, "source": len(self.doc["images"]) - 1})
        result = len(self.doc["textures"]) - 1
        self.texture_cache[key] = result
        return result

    def write(self, path):
        # Binding uses indices, so neutral artist-authored names preserve the rig.
        for collection in ("nodes", "meshes", "materials"):
            for item in self.doc[collection]:
                item["name"] = re.sub(r"A New Dawn|Rise of Mankind|\bAND\b|\bRoM\b", "Unit", item["name"], flags=re.I)
        self.doc["buffers"] = [{"byteLength": len(self.binary)}]
        doc = {k: v for k, v in self.doc.items() if v != []}
        encoded = json.dumps(doc, separators=(",", ":"), allow_nan=False).encode()
        encoded += b" " * (-len(encoded) % 4)
        data = bytes(self.binary) + b"\0" * (-len(self.binary) % 4)
        total = 12 + 8 + len(encoded) + 8 + len(data)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(struct.pack("<III", 0x46546C67, 2, total)
                         + struct.pack("<II", len(encoded), 0x4E4F534A) + encoded
                         + struct.pack("<II", len(data), 0x004E4942) + data)
        return total
