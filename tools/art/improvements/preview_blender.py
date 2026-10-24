"""Preview procedural model GLBs in Blender under the game's light, one close-up PNG per model.

    blender -b --factory-startup -P tools/art/improvements/preview_blender.py -- --out /tmp/previews \
        src/Mods/World/art/models/wheat.glb [more.glb ...] [--pitch 55] [--yaw 0] [--distance 1.3] [--size 520]

The stage copies the map's look: one sun from the back left (MapRenderer: rotation -40, -135 degrees, energy 1, no
shadows), white ambient light at 0.45 (GameView), the game camera's 50 degree vertical field of view, flat shading,
roughness 0.9, and COLOR_0 read as linear, as glTF and ImprovementRenderer read it. --srgb reads it as sRGB instead,
to see what a GLB that stored sRGB values was meant to look like. The model stands at the origin of a grass hex of
corner radius 1 (or in translucent sea with --water), +Z (the model's front) toward the camera. Inside a running
Blender, run this file with run_name set to anything but "__main__" and call stage(), load() and shoot() yourself.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

GRASS = (0x76, 0x92, 0x3A)  # sRGB, the average of the grass terrain on screen
SEA = (0x8C, 0xAB, 0xB8)  # sRGB, the coast water on screen
SEA_FLOOR = (0x3E, 0x5E, 0x6E)
PREVIEW = "preview"


def linear(c):
    c = np.asarray(c, dtype=float)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def godot_to_blender(v):
    """glTF and Godot are +Y up with +Z toward the default camera; Blender is +Z up with -Y toward it."""
    x, y, z = v
    return Vector((x, -z, y))


def clear():
    for collection in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras,
                       bpy.data.curves):
        for block in list(collection):
            collection.remove(block)


def material(name, colour=None):
    """The ImprovementRenderer material: vertex colour albedo (or a flat colour), roughness 0.9, specular 0.3."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.9
    bsdf.inputs["Specular IOR Level"].default_value = 0.3
    if colour is None:
        attribute = nodes.new("ShaderNodeVertexColor")
        links.new(attribute.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*linear(np.array(colour) / 255), 1)
    return mat


def stage(ground=GRASS, size=520, aspect=1.0, samples=16, water=False):
    """Clear the scene and set up the light, world, ground hex (or translucent sea at y = 0) and camera."""
    clear()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = samples
    scene.render.resolution_x, scene.render.resolution_y = int(size * aspect), size
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (1, 1, 1, 1)
    background.inputs["Strength"].default_value = 0.45
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 1.0
    sun.use_shadow = False
    sun_object = bpy.data.objects.new("sun", sun)
    sun_object.rotation_euler = godot_to_blender((0.5417, -0.6428, 0.5417)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun_object)
    if water:
        ground = SEA_FLOOR
    mesh = bpy.data.meshes.new("ground")
    floor = -0.16 if water else 0.0
    corners = [(math.cos(math.radians(30 + 60 * i)) * 1.0, math.sin(math.radians(30 + 60 * i)) * 1.0, floor)
               for i in range(6)]
    mesh.from_pydata(corners, [], [list(range(6))])
    mesh.materials.append(material("ground", ground))
    hex_object = bpy.data.objects.new("ground", mesh)
    scene.collection.objects.link(hex_object)
    if water:
        surface = bpy.data.meshes.new("sea")
        surface.from_pydata([(-6, -6, 0), (6, -6, 0), (6, 6, 0), (-6, 6, 0)], [], [[0, 1, 2, 3]])
        sea = material("sea", SEA)
        sea.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.6
        sea.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.3
        sea.surface_render_method = "BLENDED"
        surface.materials.append(sea)
        scene.collection.objects.link(bpy.data.objects.new("sea", surface))
    # A wider, darker apron so the frame edge is not the void.
    apron = bpy.data.meshes.new("apron")
    apron.from_pydata([(-6, -6, floor - 0.002), (6, -6, floor - 0.002), (6, 6, floor - 0.002), (-6, 6, floor - 0.002)],
                      [], [[0, 1, 2, 3]])
    apron.materials.append(material("apron", tuple(int(c * 0.82) for c in ground)))
    apron_object = bpy.data.objects.new("apron", apron)
    scene.collection.objects.link(apron_object)
    camera = bpy.data.cameras.new("camera")
    camera.sensor_fit = "VERTICAL"
    camera.angle_y = math.radians(50)
    camera.clip_start = 0.01
    camera_object = bpy.data.objects.new("camera", camera)
    scene.collection.objects.link(camera_object)
    scene.camera = camera_object
    return scene


def load(path, at=(0.0, 0.0, 0.0), yaw=0.0, srgb=False):
    """Import a GLB under an empty at a Blender-space point, with the preview material (and srgb: vertex colours
    reinterpreted as sRGB)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    parts = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.new(Path(path).stem, None)
    bpy.context.scene.collection.objects.link(root)
    mat = bpy.data.materials.get(PREVIEW) or material(PREVIEW)
    for part in parts:
        if part.parent is None:
            part.parent = root
        if part.type != "MESH":
            continue
        mesh = part.data
        for attribute in mesh.color_attributes if srgb else []:
            buffer = np.zeros(len(attribute.data) * 4, dtype=np.float32)
            attribute.data.foreach_get("color", buffer)
            buffer = buffer.reshape(-1, 4)
            buffer[:, :3] = linear(buffer[:, :3])
            attribute.data.foreach_set("color", buffer.ravel())
        mesh.materials.clear()
        mesh.materials.append(mat)
    root.location = at
    root.rotation_euler = (0, 0, yaw)
    return root


def shoot(path, target=(0.0, 0.0, 0.06), pitch=55.0, yaw=0.0, distance=1.3):
    """Aim the camera at a Blender-space target as the game rig does (yaw 0 looks north, -Y to +Y here) and render."""
    scene = bpy.context.scene
    camera = scene.camera
    p, y = math.radians(pitch), math.radians(yaw)
    flat = distance * math.cos(p)
    offset = Vector((flat * math.sin(y), -flat * math.cos(y), distance * math.sin(p)))
    camera.location = Vector(target) + offset
    camera.rotation_euler = (-offset).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main(argv):
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("models", nargs="+")
    parser.add_argument("--out", required=True)
    parser.add_argument("--pitch", type=float, default=55.0)
    parser.add_argument("--yaw", type=float, default=0.0)
    parser.add_argument("--distance", type=float, default=1.3)
    parser.add_argument("--size", type=int, default=520)
    parser.add_argument("--water", action="store_true", help="stand the models in translucent sea at y = 0")
    parser.add_argument("--srgb", action="store_true", help="read COLOR_0 as sRGB (old GLBs' intended colours)")
    args = parser.parse_args(argv)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for model in args.models:
        stage(size=args.size, aspect=1.25, water=args.water)
        load(model, srgb=args.srgb)
        shoot(out / f"{Path(model).stem}.png", pitch=args.pitch, yaw=args.yaw, distance=args.distance)


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
