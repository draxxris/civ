#!/usr/bin/env blender --background --python
"""Convert Quaternius FBX scatter models to compact, self-contained GLBs."""

from __future__ import annotations

import argparse
import array
import json
import math
import re
import struct
import sys
from pathlib import Path

import bpy


REPO_ROOT = Path(__file__).resolve().parents[2]
SETS_PATH = Path(__file__).with_name("nature_sets.json")
OUTPUT_ROOT = REPO_ROOT / "src/Mods/World/art/scatter"
REPORT_PATH = Path(__file__).with_name("nature_report.md")
MAX_OPAQUE_TEXTURE_SIZE = 256
MAX_ALPHA_TEXTURE_SIZE = 512
TREE_TRIANGLE_BUDGET = 1500
SMALL_TRIANGLE_BUDGET = 300
TREE_FAMILIES = ("PineTree", "PalmTree", "NormalTree", "BirchTree", "MapleTree", "DeadTree")
ALPHA_WORDS = ("leaf", "leav", "flower", "grass", "plant")
SNOW_COLOR = (0.79, 0.88, 1.0)
SNOW_MIX = 0.58


class ConversionError(RuntimeError):
    pass


def parse_arguments() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, help="Pack root containing FBX/ and Textures/")
    return parser.parse_args(argv)


def find_pack_root(source: Path) -> Path:
    source = source.expanduser().resolve()
    if (source / "FBX").is_dir() and (source / "Textures").is_dir():
        return source
    roots = sorted(p.parent for p in source.rglob("FBX") if p.is_dir() and (p.parent / "Textures").is_dir())
    if not roots:
        raise ConversionError(f"Could not find FBX/ and Textures/ under {source}")
    return roots[0]


def load_sets() -> dict[str, list[str]]:
    mapping = json.loads(SETS_PATH.read_text(encoding="utf-8"))
    if set(mapping) != {"forest", "forest_snow", "jungle", "swamp", "oasis", "rocks"}:
        raise ConversionError("nature_sets.json must define the six scatter sets")
    for set_name, models in mapping.items():
        if len(models) < (4 if set_name == "rocks" else 3):
            raise ConversionError(f"Scatter set {set_name} does not meet its minimum model count")
        if len(models) != len(set(models)):
            raise ConversionError(f"Scatter set {set_name} contains duplicate model names")
    return mapping


def source_model_name(set_name: str, output_name: str) -> str:
    if set_name == "forest_snow" and output_name.endswith("_Snow"):
        return output_name[:-5]
    if set_name == "jungle" and output_name.startswith("NormalTree_") and output_name.endswith("_Large"):
        return output_name[:-6]
    return output_name


def model_family(source_name: str) -> str:
    for family in TREE_FAMILIES:
        if source_name.startswith(family):
            return "NormalTree" if family == "DeadTree" else family
    if source_name.startswith("Bush"):
        return "Bush"
    return source_name.split("_", 1)[0]


def is_tree(source_name: str) -> bool:
    return source_name.startswith(TREE_FAMILIES)


def is_rock(source_name: str) -> bool:
    return source_name.startswith("Rock_")


def target_dimension(set_name: str, output_name: str, source_name: str) -> float:
    if is_tree(source_name):
        if source_name.startswith("PalmTree"):
            return 0.48
        if set_name == "jungle" and output_name.endswith("_Large"):
            return 0.50
        if source_name.startswith("PineTree"):
            return 0.44 if set_name == "forest_snow" else 0.42
        return 0.40
    if source_name.startswith("Bush_Large"):
        return 0.17
    if is_rock(source_name):
        return 0.16
    return 0.15


def texture_name(source_name: str, material_name: str) -> str:
    family = model_family(source_name)
    role = material_name.lower()
    if "rock" in role:
        return "Rocks.png"
    if "bark" in role or "trunk" in role:
        return "PalmTree_Trunk.png" if family == "PalmTree" else f"{family}_Bark.png"
    if "leaf" in role or "leav" in role:
        return f"{family}_Leaves.png"
    if "flower" in role:
        return "Flowers.png"
    if "grass" in role or source_name.startswith("Grass_"):
        return "Grass.png"
    if source_name.startswith("Plant_"):
        return "Flowers.png"
    raise ConversionError(f"No texture rule for {source_name} material {material_name}")


def material_uses_alpha(material_name: str, source_name: str) -> bool:
    name = material_name.lower()
    return source_name.startswith("Plant_") or any(word in name for word in ALPHA_WORDS)


def load_texture(texture_root: Path, filename: str, max_size: int,
                 snow_tint: bool = False) -> bpy.types.Image:
    filepath = texture_root / filename
    if not filepath.is_file():
        raise ConversionError(f"Missing source texture {filepath}")
    image = bpy.data.images.load(str(filepath), check_existing=False)
    image.name = f"{filepath.stem}{'_Snow' if snow_tint else ''}"
    image.alpha_mode = "STRAIGHT"
    if max(image.size) > max_size:
        scale = max_size / max(image.size)
        width = max(1, round(image.size[0] * scale))
        height = max(1, round(image.size[1] * scale))
        image.scale(width, height)
    if snow_tint:
        tint_snow(image)
    image.pack()
    return image


def tint_snow(image: bpy.types.Image) -> None:
    pixels = array.array("f", [0.0]) * (image.size[0] * image.size[1] * 4)
    image.pixels.foreach_get(pixels)
    keep = 1.0 - SNOW_MIX
    for offset in range(0, len(pixels), 4):
        pixels[offset] = pixels[offset] * keep + SNOW_COLOR[0] * SNOW_MIX
        pixels[offset + 1] = pixels[offset + 1] * keep + SNOW_COLOR[1] * SNOW_MIX
        pixels[offset + 2] = pixels[offset + 2] * keep + SNOW_COLOR[2] * SNOW_MIX
    image.pixels.foreach_set(pixels)


def set_image_color_space(image: bpy.types.Image) -> None:
    image.colorspace_settings.name = "sRGB"


def wire_material(material: bpy.types.Material, texture_root: Path, source_name: str,
                  set_name: str, snow_variant: bool, texture_sizes: dict[str, tuple[int, int]]) -> None:
    is_leaf_material = any(word in material.name.lower() for word in ("leaf", "leav"))
    alpha_required = material_uses_alpha(material.name, source_name)
    albedo_name = texture_name(source_name, material.name)
    max_size = MAX_ALPHA_TEXTURE_SIZE if alpha_required else MAX_OPAQUE_TEXTURE_SIZE
    albedo = load_texture(texture_root, albedo_name, max_size,
                          snow_variant and is_leaf_material)
    set_image_color_space(albedo)
    texture_sizes[f"{albedo_name}{' (snow tint)' if snow_variant and is_leaf_material else ''}"] = tuple(albedo.size)

    material.use_nodes = True
    material.node_tree.nodes.clear()
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.88
    albedo_node = nodes.new("ShaderNodeTexImage")
    albedo_node.image = albedo
    albedo_node.interpolation = "Linear"
    links.new(albedo_node.outputs["Color"], shader.inputs["Base Color"])
    if alpha_required:
        links.new(albedo_node.outputs["Alpha"], shader.inputs["Alpha"])
        material.surface_render_method = "DITHERED"
        material.alpha_threshold = 0.5
        material.blend_method = "CLIP"
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    if set_name == "forest_snow" and snow_variant and is_leaf_material:
        material.name = f"{material.name}_Snow"


def import_model(fbx_path: Path) -> list[bpy.types.Object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path), use_image_search=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise ConversionError(f"FBX contains no meshes: {fbx_path}")
    for obj in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def attach_materials(obj: bpy.types.Object, source_name: str, texture_root: Path,
                     set_name: str, snow_variant: bool,
                     texture_sizes: dict[str, tuple[int, int]]) -> None:
    if not obj.data.materials:
        material = bpy.data.materials.new(f"{source_name}_Grass")
        obj.data.materials.append(material)
    for material in list(obj.data.materials):
        if material is None:
            material = bpy.data.materials.new(f"{source_name}_Material")
        wire_material(material, texture_root, source_name, set_name, snow_variant, texture_sizes)


def object_material_names(obj: bpy.types.Object) -> set[str]:
    names = set()
    for polygon in obj.data.polygons:
        if polygon.material_index < len(obj.data.materials):
            material = obj.data.materials[polygon.material_index]
            if material:
                names.add(material.name)
    return names


def triangle_count(obj: bpy.types.Object) -> int:
    return sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)


def split_materials(obj: bpy.types.Object) -> list[bpy.types.Object]:
    if len(obj.data.materials) < 2:
        return [obj]
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    return [candidate for candidate in bpy.context.scene.objects if candidate.type == "MESH"]


def evaluated_triangle_count(obj: bpy.types.Object) -> int:
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    count = sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons)
    evaluated.to_mesh_clear()
    return count


def decimate_object(obj: bpy.types.Object, target: int) -> None:
    current = triangle_count(obj)
    if current <= target:
        return
    modifier = obj.modifiers.new("ScatterTriangleBudget", "DECIMATE")
    modifier.ratio = max(0.005, target / current)
    for _ in range(8):
        bpy.context.view_layer.update()
        if evaluated_triangle_count(obj) <= target:
            break
        modifier.ratio = max(0.001, modifier.ratio * 0.88)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def simplify_model(objects: list[bpy.types.Object], source_name: str) -> list[bpy.types.Object]:
    tree = is_tree(source_name)
    rock = is_rock(source_name)
    if not tree and not rock:
        return objects
    objects = split_materials(objects[0])
    leaves = [obj for obj in objects if any(any(word in name.lower() for word in ("leaf", "leav")) for name in object_material_names(obj))]
    trunks = [obj for obj in objects if obj not in leaves]
    if tree:
        leaf_triangles = sum(triangle_count(obj) for obj in leaves)
        trunk_triangles = sum(triangle_count(obj) for obj in trunks)
        trunk_budget = max(4, TREE_TRIANGLE_BUDGET - leaf_triangles)
        for obj in trunks:
            share = max(4, round(trunk_budget * triangle_count(obj) / max(1, trunk_triangles)))
            decimate_object(obj, share)
    if rock:
        for obj in objects:
            decimate_object(obj, SMALL_TRIANGLE_BUDGET)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    return [bpy.context.view_layer.objects.active]


def normalize_model(obj: bpy.types.Object, target: float, tree: bool) -> tuple[float, float, float]:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    vertices = obj.data.vertices
    minima = [min(vertex.co[axis] for vertex in vertices) for axis in range(3)]
    maxima = [max(vertex.co[axis] for vertex in vertices) for axis in range(3)]
    source_size = [maxima[i] - minima[i] for i in range(3)]
    reference_size = source_size[2] if tree else max(source_size)
    if reference_size <= 1e-8:
        raise ConversionError(f"Degenerate model geometry: {obj.name}")
    scale = target / reference_size
    center_x = (minima[0] + maxima[0]) * 0.5
    center_y = (minima[1] + maxima[1]) * 0.5
    base_z = minima[2]
    for vertex in vertices:
        vertex.co.x = (vertex.co.x - center_x) * scale
        vertex.co.y = (vertex.co.y - center_y) * scale
        vertex.co.z = (vertex.co.z - base_z) * scale
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    obj.data.update()
    normalized = [value * scale for value in source_size]
    return normalized[0], normalized[2], normalized[1]


def patch_glb(path: Path) -> dict:
    data = path.read_bytes()
    if len(data) < 20:
        raise ConversionError(f"Invalid GLB output: {path}")
    magic, version, _ = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2:
        raise ConversionError(f"Unsupported GLB header in {path}")
    chunks = []
    offset = 12
    while offset < len(data):
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        start = offset + 8
        chunks.append((chunk_type, data[start : start + chunk_length]))
        offset = start + chunk_length
    json_index = next((index for index, chunk in enumerate(chunks) if chunk[0] == 0x4E4F534A), None)
    if json_index is None:
        raise ConversionError(f"GLB has no JSON chunk: {path}")
    document = json.loads(chunks[json_index][1].decode("utf-8").rstrip(" \0"))
    for image in document.get("images", []):
        uri = image.get("uri", "")
        if uri and not uri.startswith("data:"):
            raise ConversionError(f"GLB references an external image: {path}: {uri}")
    textures = document.get("textures", [])
    used_images = set()
    for material in document.get("materials", []):
        if material.get("normalTexture"):
            raise ConversionError(f"Scatter GLB contains a normal map: {path}: {material.get('name')}")
        albedo = material.get("pbrMetallicRoughness", {}).get("baseColorTexture")
        if albedo:
            used_images.add(textures[albedo["index"]]["source"])
    if used_images != set(range(len(document.get("images", [])))):
        raise ConversionError(f"GLB contains unused or non-albedo images: {path}")
    for material in document.get("materials", []):
        if any(word in material.get("name", "").lower() for word in ALPHA_WORDS):
            if not material.get("pbrMetallicRoughness", {}).get("baseColorTexture"):
                raise ConversionError(f"Cutout material has no albedo texture: {path}: {material.get('name')}")
            material["alphaMode"] = "MASK"
            material["alphaCutoff"] = 0.5
    packed_json = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    packed_json += b" " * ((-len(packed_json)) % 4)
    chunks[json_index] = (chunks[json_index][0], packed_json)
    output = bytearray(b"glTF" + struct.pack("<II", 2, 0))
    for chunk_type, payload in chunks:
        output.extend(struct.pack("<II", len(payload), chunk_type))
        output.extend(payload)
    struct.pack_into("<I", output, 8, len(output))
    path.write_bytes(output)
    return document


def export_model(obj: bpy.types.Object, output_path: Path) -> dict:
    # Each FBX import starts from factory settings; purge its orphaned source textures and materials too.
    bpy.data.orphans_purge(do_recursive=True)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        export_yup=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        use_selection=True,
    )
    if not output_path.is_file():
        raise ConversionError(f"Blender did not write {output_path}")
    return patch_glb(output_path)


def format_size(size: tuple[int, int]) -> str:
    return f"{size[0]}×{size[1]}"


def process_model(pack_root: Path, set_name: str, output_name: str) -> dict:
    source_name = source_model_name(set_name, output_name)
    fbx_path = pack_root / "FBX" / f"{source_name}.fbx"
    if not fbx_path.is_file():
        raise ConversionError(f"Missing model source {fbx_path}")
    texture_sizes: dict[str, tuple[int, int]] = {}
    snow_variant = set_name == "forest_snow" and output_name.endswith("_Snow")
    objects = import_model(fbx_path)
    attach_materials(objects[0], source_name, pack_root / "Textures", set_name,
                     snow_variant, texture_sizes)
    triangles_before = sum(triangle_count(obj) for obj in objects)
    objects = simplify_model(objects, source_name)
    obj = objects[0]
    obj.name = output_name
    obj.data.name = output_name
    dimensions = normalize_model(obj, target_dimension(set_name, output_name, source_name), is_tree(source_name))
    triangles_after = triangle_count(obj)
    output_path = OUTPUT_ROOT / set_name / f"{output_name}.glb"
    gltf = export_model(obj, output_path)
    actual_alpha = [material for material in gltf.get("materials", [])
                    if any(word in material.get("name", "").lower() for word in ALPHA_WORDS)]
    for material in actual_alpha:
        if material.get("alphaMode") != "MASK" or abs(material.get("alphaCutoff", 0.0) - 0.5) > 1e-6:
            raise ConversionError(f"Alpha cutoff was not written for {output_path}: {material.get('name')}")
    return {
        "set": set_name,
        "name": output_name,
        "source": f"FBX/{source_name}.fbx",
        "triangles_before": triangles_before,
        "triangles_after": triangles_after,
        "dimensions": dimensions,
        "textures": texture_sizes,
        "output": output_path.relative_to(REPO_ROOT).as_posix(),
        "size_bytes": output_path.stat().st_size,
        "alpha_materials": len(actual_alpha),
    }


def report_line(result: dict) -> str:
    dims = " × ".join(f"{axis:.3f}" for axis in result["dimensions"])
    textures = ", ".join(
        f"{name} {format_size(size)}" for name, size in sorted(result["textures"].items())
    ) or "none"
    return (
        f"- `{result['name']}` — `{result['source']}`; "
        f"{result['triangles_before']:,} → {result['triangles_after']:,} triangles; "
        f"Y-up bounds {dims}; textures: {textures}; "
        f"output `{result['output']}` ({result['size_bytes']:,} bytes)."
    )


def write_report(results: list[dict], set_mapping: dict[str, list[str]], pack_root: Path) -> None:
    lines = [
        "# Quaternius Ultimate Stylized Nature conversion report",
        "",
        "Source archive: `Ultimate Stylized Nature - May 2022-20261014T154712Z-1-001.zip`.",
        "Source FBX and texture files are read from the extracted pack directory; the archive is not shipped.",
        "Licence: Quaternius pack `License.txt` states CC0 1.0 Universal (public domain dedication).",
        "The previous 138,824,120-byte export repeated high-entropy 512 × 512 bark albedo and normal PNGs in each tree GLB; inspected GLBs had no unused FBX images.",
        "",
        "Only referenced albedo textures are embedded; bark and rock textures are at most 256 × 256, while alpha-cutout PNGs are at most 512 × 512.",
        "Bark normal maps are omitted because sub-pixel props do not benefit from their extra data and decode cost.",
        "",
        "Forest snow variants tint pine leaf RGB values toward blue-white and preserve the source alpha.",
        "GLB JSON is checked for embedded albedo-only images; cutout materials are written as `MASK` with cutoff 0.5.",
        "",
    ]
    misses = []
    total_bytes = 0
    for set_name, model_names in set_mapping.items():
        set_results = [result for result in results if result["set"] == set_name]
        set_triangles = sum(result["triangles_after"] for result in set_results)
        lines.extend([f"## {set_name}", "", f"{len(set_results)} models; {set_triangles:,} total triangles.", ""])
        lines.extend(report_line(result) for result in set_results)
        lines.append("")
        for result in set_results:
            total_bytes += result["size_bytes"]
            budget = TREE_TRIANGLE_BUDGET if is_tree(source_model_name(set_name, result["name"])) else SMALL_TRIANGLE_BUDGET if is_rock(source_model_name(set_name, result["name"])) or source_model_name(set_name, result["name"]).startswith(("Plant_", "Grass_")) else None
            if budget and result["triangles_after"] > budget:
                misses.append((set_name, result["name"], result["triangles_after"], budget))
    lines.extend(["## Triangle-budget misses", ""])
    if not misses:
        lines.append("None.")
    else:
        for set_name, name, count, budget in misses:
            reason = "Leaf cards were not decimated." if is_tree(source_model_name(set_name, name)) else "Plant and grass geometry was kept intact."
            lines.append(f"- `{set_name}/{name}`: {count:,} triangles exceeds {budget:,}; {reason}")
    lines.extend([
        "",
        f"Total GLB output: {len(results)} files, {total_bytes:,} bytes.",
        f"Pack root used for this conversion: `{pack_root}` (ignored work directory only).",
        "",
    ])
    REPORT_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    args = parse_arguments()
    pack_root = find_pack_root(Path(args.source))
    set_mapping = load_sets()
    results = []
    for set_name, models in set_mapping.items():
        for output_name in models:
            result = process_model(pack_root, set_name, output_name)
            results.append(result)
            print(f"Converted {set_name}/{output_name}: {result['triangles_after']} triangles")
    write_report(results, set_mapping, pack_root)
    print(f"Wrote {len(results)} GLBs and {REPORT_PATH}")


if __name__ == "__main__":
    main()
