#!/usr/bin/env python3
"""Render 5 views (front, back, left, right, isometric), composite 2x3 sheet, and metadata JSON for unit GLBs."""
import argparse
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODELS_DIR = ROOT / "src/Ariel.Rules/Military/art/models"
OUT_ROOT = ROOT / "docs/models"


def glb_info(glb_path: Path) -> dict:
    data = glb_path.read_bytes()
    length = struct.unpack_from("<I", data, 12)[0]
    doc = json.loads(data[20:20 + length])
    accessors = doc.get("accessors", [])
    triangles = 0
    for mesh in doc.get("meshes", []):
        for prim in mesh.get("primitives", []):
            if "indices" in prim:
                triangles += accessors[prim["indices"]]["count"] // 3
            elif "POSITION" in prim.get("attributes", {}):
                triangles += accessors[prim["attributes"]["POSITION"]]["count"] // 3
    animations = [a.get("name", f"anim_{i}") for i, a in enumerate(doc.get("animations", []))]
    return {
        "triangles": triangles,
        "animation_count": len(animations),
        "animations": animations,
    }


def resolve_unit_textures(unit_names: list[str], source_dir: Path) -> dict[str, list[str]]:
    """Run under Python 3.11 with PyFFI via uv to resolve exact NIF texture paths for the requested units."""
    code = """
import json, sys, time
time.clock = time.perf_counter
from pathlib import Path
sys.path.insert(0, str(Path(sys.argv[1]) / "tools/art"))
from convert_units import read_nif, resolve_art, source_symbols, provenance
from source_assets import Assets, definitions, field
from unit_geometry import diffuse_source
from pyffi.formats.nif import NifFormat as N

root = Path(sys.argv[1])
source = Path(sys.argv[2])
target_names = set(json.loads(sys.argv[3]))

units, art = definitions(source / "data")
assets = Assets(source / "media/assets")
symbols = source_symbols(root)
registry = json.loads((root / "src/Ariel.Rules/Military/data/unit.json").read_text())

out = {}
for u in registry:
    name = u["id"].split(":", 1)[1]
    if target_names and name not in target_names:
        continue
    try:
        tag, definition, _ = resolve_art(u, symbols, units, art)
        nif = assets.resolve(field(definition, "NIF")) or assets.resolve(field(definition, "SHADERNIF"))
        kfm = assets.resolve(field(definition, "KFM"))
        folders = [nif.parent] + ([kfm.parent] if kfm else [])
        data = read_nif(nif)
        textures = []
        for b in data.blocks:
            if isinstance(b, N.NiGeometry) and b.data and b.data.num_vertices and hasattr(b.data, "get_triangles") and not (b.flags & 1):
                for prop in b.properties:
                    if isinstance(prop, N.NiTexturingProperty):
                        src, _ = diffuse_source(prop)
                        if src:
                            p = assets.resolve(src.file_name, folders)
                            if p:
                                rel = provenance(p, assets)
                                if rel not in textures:
                                    textures.append(rel)
        out[name] = textures
    except Exception as e:
        out[name] = []
print(json.dumps(out))
"""
    uv = shutil.which("uv")
    cmd = [
        uv, "run", "--python", "3.11",
        "--with", "pyffi==2.2.3",
        "--with", "numpy==2.4.6",
        "--with", "Pillow==12.3.0",
        "python", "-c", code,
        str(ROOT), str(source_dir), json.dumps(unit_names),
    ]
    env = {k: v for k, v in os.environ.items() if k != "PYTHONPATH"}
    res = subprocess.run(cmd, capture_output=True, text=True, check=True, env=env)
    return json.loads(res.stdout.strip().splitlines()[-1])


BLENDER_SCRIPT = """
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
tasks = json.loads(Path(argv[0]).read_text())

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 512
scene.render.resolution_y = 512

world = bpy.data.worlds.new("World")
scene.world = world
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.18, 0.19, 0.22, 1.0)
bg.inputs[1].default_value = 1.0

cdata = bpy.data.cameras.new("Cam")
cdata.type = "ORTHO"
cobj = bpy.data.objects.new("Cam", cdata)
scene.collection.objects.link(cobj)
scene.camera = cobj

light_objs = []
for lname, energy, rot in [
    ("Key", 3.5, (math.radians(-30), math.radians(30), 0)),
    ("Fill", 1.8, (math.radians(20), math.radians(-40), 0)),
    ("Rim", 1.5, (math.radians(150), 0, 0)),
]:
    ldata = bpy.data.lights.new(lname, type="SUN")
    ldata.energy = energy
    lobj = bpy.data.objects.new(lname, ldata)
    lobj.parent = cobj
    lobj.rotation_euler = rot
    scene.collection.objects.link(lobj)
    light_objs.append(lobj)

keep_objs = {cobj, *light_objs}

views = {
    "front": (Vector((0, -1, 0)), (math.radians(90), 0, 0)),
    "back": (Vector((0, 1, 0)), (math.radians(90), 0, math.radians(180))),
    "left": (Vector((-1, 0, 0)), (math.radians(90), 0, math.radians(-90))),
    "right": (Vector((1, 0, 0)), (math.radians(90), 0, math.radians(90))),
    "isometric": (Vector((1, -1, 0.8165)).normalized(), (math.radians(60), 0, math.radians(45))),
}

for idx, task in enumerate(tasks, 1):
    glb_path = Path(task["glb"])
    out_dir = Path(task["out_dir"])
    out_dir.mkdir(parents=True, exist_ok=True)

    for obj in list(bpy.data.objects):
        if obj not in keep_objs:
            bpy.data.objects.remove(obj, do_unlink=True)
    for col in (bpy.data.meshes, bpy.data.materials, bpy.data.textures, bpy.data.images, bpy.data.armatures, bpy.data.actions):
        for block in list(col):
            if block.users == 0:
                col.remove(block)

    bpy.ops.import_scene.gltf(filepath=str(glb_path))

    for obj in list(bpy.data.objects):
        if obj.name.startswith("Icosphere") and obj.type == "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)

    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()

    min_co = Vector((1e9, 1e9, 1e9))
    max_co = Vector((-1e9, -1e9, -1e9))
    found = False
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH" and obj not in keep_objs:
            eval_obj = obj.evaluated_get(depsgraph)
            m = eval_obj.matrix_world
            if m.to_scale().length < 1e-4:
                continue
            mesh = eval_obj.to_mesh()
            for v in mesh.vertices:
                w = m @ v.co
                found = True
                for i in range(3):
                    min_co[i] = min(min_co[i], w[i])
                    max_co[i] = max(max_co[i], w[i])
            eval_obj.to_mesh_clear()

    if not found:
        min_co = Vector((-0.3, -0.3, 0.0))
        max_co = Vector((0.3, 0.3, 0.6))

    center = (min_co + max_co) * 0.5
    span = max(max_co.x - min_co.x, max_co.y - min_co.y, max_co.z - min_co.z, 0.1)
    cdata.ortho_scale = span * 1.25

    for vname, (offset, rot) in views.items():
        cobj.location = center + offset * (span * 4.0)
        cobj.rotation_euler = rot
        scene.render.filepath = str(out_dir / f"{vname}.png")
        bpy.ops.render.render(write_still=True)
    print(f"[{idx}/{len(tasks)}] Rendered {task['name']}", flush=True)
"""


def load_font(size: int):
    from PIL import ImageFont
    for path in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf",
    ):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def build_composite(out_dir: Path, meta: dict):
    from PIL import Image, ImageDraw
    tile = 512
    cols, rows = 2, 3
    sheet = Image.new("RGB", (cols * tile, rows * tile), (30, 32, 36))
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(22)
    body_font = load_font(16)

    panels = [
        ("front", "Front", 0, 0),
        ("back", "Back", 1, 0),
        ("left", "Left", 0, 1),
        ("right", "Right", 1, 1),
        ("isometric", "Isometric", 0, 2),
    ]
    for key, label, col, row in panels:
        x, y = col * tile, row * tile
        img = Image.open(out_dir / f"{key}.png").convert("RGB")
        if img.size != (tile, tile):
            img = img.resize((tile, tile), Image.LANCZOS)
        sheet.paste(img, (x, y))
        draw.rectangle((x, y, x + tile - 1, y + tile - 1), outline=(22, 24, 28), width=2)
        draw.text((x + 14, y + 12), label, font=title_font, fill=(255, 255, 255), stroke_width=3, stroke_fill=(0, 0, 0))

    # 6th cell (col 1, row 2): metadata summary card
    x0, y0 = 1 * tile, 2 * tile
    draw.rectangle((x0, y0, x0 + tile - 1, y0 + tile - 1), fill=(36, 39, 44), outline=(22, 24, 28), width=2)
    draw.text((x0 + 20, y0 + 20), meta["name"], font=title_font, fill=(255, 255, 255))

    anims = ", ".join(meta["animations"]) if meta["animations"] else "none"
    lines = [
        f"Triangles: {meta['triangles']}",
        f"Animations ({meta['animation_count']}): {anims}",
        f"Textures ({len(meta['textures'])}):",
    ]
    for t in meta["textures"]:
        lines.append(f"  {t}")

    y_text = y0 + 64
    for line in lines:
        if len(line) > 56:
            line = line[:26] + "..." + line[-27:]
        draw.text((x0 + 20, y_text), line, font=body_font, fill=(220, 224, 230))
        y_text += 26

    sheet.save(out_dir / "composite.png", optimize=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--units", nargs="*", help="Unit names (without .glb) to render")
    parser.add_argument("--skip-existing", action="store_true", help="Skip units whose composite.png already exists")
    parser.add_argument("--source", type=Path, default=Path("/home/gordon/workspace/civ/AND-extraction"))
    args = parser.parse_args()

    if args.units:
        names = [u.removeprefix("military:").removesuffix(".glb") for u in args.units]
    else:
        names = sorted(p.stem for p in MODELS_DIR.glob("*.glb"))

    if args.skip_existing:
        names = [n for n in names if not (OUT_ROOT / n / "composite.png").exists()]
        if not names:
            print("All models already rendered.")
            return

    textures_by_unit = resolve_unit_textures(names, args.source)

    tmp_dir = Path("/tmp/ariel/model_shots")
    tmp_dir.mkdir(parents=True, exist_ok=True)
    tasks_file = tmp_dir / "tasks.json"
    script_file = tmp_dir / "render_blender.py"

    tasks = [
        {"name": name, "glb": str(MODELS_DIR / f"{name}.glb"), "out_dir": str(OUT_ROOT / name)}
        for name in names
    ]
    tasks_file.write_text(json.dumps(tasks))
    script_file.write_text(BLENDER_SCRIPT)

    env = os.environ.copy()
    pydeps = Path("/tmp/ariel/pydeps")
    if not (pydeps / "numpy").exists():
        uv = shutil.which("uv")
        subprocess.run([uv, "pip", "install", "--target", str(pydeps), "numpy", "pillow"], check=True)
    env["PYTHONPATH"] = f"{pydeps}:{env['PYTHONPATH']}" if env.get("PYTHONPATH") else str(pydeps)

    # Use PYTHONPATH=/tmp/ariel/pydeps in sys.path for PIL if needed
    if str(pydeps) not in sys.path:
        sys.path.insert(0, str(pydeps))

    # Batch in chunks so Blender memory stays bounded and we can check thermal gate between chunks
    chunk_size = 40
    for i in range(0, len(tasks), chunk_size):
        chunk = tasks[i:i + chunk_size]
        chunk_file = tmp_dir / f"tasks_{i}.json"
        chunk_file.write_text(json.dumps(chunk))
        subprocess.run(["tools/cool-wait.sh"], check=True, cwd=ROOT)
        print(f"Rendering batch {i + 1}..{i + len(chunk)} of {len(tasks)}...", flush=True)
        subprocess.run(
            ["blender", "-b", "--factory-startup", "--python-use-system-env", "-P", str(script_file), "--", str(chunk_file)],
            check=True,
            env=env,
        )
        for item in chunk:
            name = item["name"]
            out_dir = OUT_ROOT / name
            info = glb_info(MODELS_DIR / f"{name}.glb")
            textures = textures_by_unit.get(name, [])
            meta = {
                "name": name,
                "model": f"src/Ariel.Rules/Military/art/models/{name}.glb",
                "texture_path": textures[0] if len(textures) == 1 else textures,
                "textures": textures,
                "triangles": info["triangles"],
                "animation_count": info["animation_count"],
                "animations": info["animations"],
            }
            (out_dir / "info.json").write_text(json.dumps(meta, indent=2) + "\n")
            build_composite(out_dir, meta)
            print(f"Completed docs/models/{name}/", flush=True)


if __name__ == "__main__":
    main()
