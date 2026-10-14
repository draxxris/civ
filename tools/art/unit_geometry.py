"""Convert NIF hierarchy, geometry, materials, and skin bindings."""
import numpy as np
from pyffi.formats.nif import NifFormat as N

from glb_writer import Glb, decompose, matrix, text, transform_points


def vertex_weights(shape):
    weights = shape.get_vertex_weights()
    if any(weights):
        return weights
    partition = shape.get_skin_partition()
    if partition is None:
        raise ValueError("Skin has neither vertex weights nor partitions")
    for part in partition.skin_partition_blocks:
        for i, vertex in enumerate(part.vertex_map):
            influences = []
            for j, weight in enumerate(part.vertex_weights[i]):
                if weight > 0:
                    influences.append((part.bones[part.bone_indices[i][j]], weight))
            weights[vertex] = influences
    return weights


def skin_arrays(shape, warnings):
    weights = vertex_weights(shape)
    joints = np.zeros((len(weights), 4), dtype="<u2")
    values = np.zeros((len(weights), 4), dtype=float)
    truncated = 0
    for i, influences in enumerate(weights):
        if len(influences) > 4:
            truncated += 1
        chosen = sorted(influences, key=lambda x: -x[1])[:4]
        if not chosen:
            raise ValueError(f"Unweighted vertex {i} in {text(shape.name)}")
        for slot, (joint, weight) in enumerate(chosen):
            joints[i, slot], values[i, slot] = joint, weight
    values /= values.sum(axis=1)[:, None]
    if truncated:
        warnings.append(f"{text(shape.name)}: strongest four influences retained for {truncated} vertices")
    return joints, values


def diffuse_source(prop):
    base = prop.base_texture.source if prop.has_base_texture else None
    team_color = base is not None and text(base.file_name).lower() == "teamcolor.bmp"
    if base and not team_color:
        return base, False
    # Civ4's shader NIFs put their diffuse map in the detail slot. The decal
    # slot on these meshes instead contains the damage overlay.
    slots = ("decal_0_texture", "detail_texture") if team_color else ("detail_texture", "decal_0_texture")
    for slot in slots:
        descriptor = getattr(prop, slot)
        if getattr(prop, "has_" + slot) and descriptor.source:
            return descriptor.source, team_color
    return base, team_color


def material_texture(glb, prop, assets, folders, warnings):
    source, team_color = diffuse_source(prop)
    if source is None:
        return None, team_color
    filename = source.file_name
    path = assets.resolve(filename, folders)
    if not path:
        warnings.append("Missing texture: " + text(filename))
        return None, team_color
    try:
        return glb.texture(path, team_color), team_color
    except OSError as error:
        warnings.append("Unreadable diffuse texture: " + text(filename) + ": " + str(error))
        return None, team_color


def mesh_material(glb, shape, assets, folders, warnings):
    material = {"name": "Diffuse", "pbrMetallicRoughness": {"metallicFactor": 0, "roughnessFactor": 1},
                "doubleSided": True}
    team_color = False
    for prop in shape.properties:
        if isinstance(prop, N.NiTexturingProperty):
            texture, team_color = material_texture(glb, prop, assets, folders, warnings)
            if texture is not None:
                material["pbrMetallicRoughness"]["baseColorTexture"] = {"index": texture}
        if isinstance(prop, N.NiAlphaProperty):
            if prop.flags & 512:
                material.update(alphaMode="MASK", alphaCutoff=prop.threshold / 255)
            elif prop.flags & 1:
                material["alphaMode"] = "BLEND"
    if team_color:
        material.pop("alphaMode", None)
        material.pop("alphaCutoff", None)
    glb.doc["materials"].append(material)
    return len(glb.doc["materials"]) - 1


def hierarchy_node(obj, ids, parents):
    node = {"name": text(obj.name) or f"Node{ids[obj]}", **decompose(matrix(obj.get_transform()))}
    children = list(dict.fromkeys(c for c in getattr(obj, "children", []) if c in ids))
    if children:
        node["children"] = [ids[c] for c in children]
    for child in children:
        if child in parents:
            raise ValueError("NIF hierarchy has multiple parents")
        parents[child] = obj
    return node


def add_hierarchy(glb, data):
    objects = [b for b in data.blocks if isinstance(b, N.NiAVObject)]
    ids = {b: i + 1 for i, b in enumerate(objects)}
    names = {}
    parents = {}
    for obj in objects:
        glb.doc["nodes"].append(hierarchy_node(obj, ids, parents))
        names.setdefault(obj.name, []).append(ids[obj])
    roots = [o for o in objects if o not in parents]
    glb.doc["nodes"][0]["children"] = [ids[o] for o in roots]
    worlds = {}

    def world(obj):
        if obj not in worlds:
            local = matrix(obj.get_transform())
            worlds[obj] = world(parents[obj]) @ local if obj in parents else local
        return worlds[obj]

    for obj in objects:
        world(obj)
    return ids, names, worlds, parents


def source_skin_matrices(shape, worlds):
    instance = shape.skin_instance
    skin = instance.data
    # Some source meshes intentionally offset their whole deformed skin relative
    # to the skeleton (the warrior body is one). A corrected skeleton copy keeps
    # that left-side transform without changing inverse-bind matrices or weapons.
    correction = worlds[shape] @ matrix(skin.get_transform()) @ np.linalg.inv(worlds[instance.skeleton_root])
    return [matrix(b.get_transform()) for b in skin.bone_list], correction


def corrected_joints(glb, bones, ids, parents, correction, names):
    if np.allclose(correction, np.eye(4), atol=1e-5):
        return {b: ids[b] for b in bones}
    needed = set()
    for bone in bones:
        while bone is not None:
            needed.add(bone)
            bone = parents.get(bone)
    wrapper = len(glb.doc["nodes"])
    glb.doc["nodes"].append({"name": "SkinOffset", **decompose(correction), "children": []})
    glb.doc["nodes"][0]["children"].append(wrapper)
    copies = {}
    for obj in ids:
        if obj not in needed:
            continue
        copies[obj] = len(glb.doc["nodes"])
        original = glb.doc["nodes"][ids[obj]]
        node = {k: v for k, v in original.items() if k not in ("mesh", "skin", "children")}
        node["name"] += "_SkinOffset"
        glb.doc["nodes"].append(node)
        names[obj.name].append(copies[obj])
    for obj, index in copies.items():
        parent = copies.get(parents.get(obj), wrapper)
        glb.doc["nodes"][parent].setdefault("children", []).append(index)
    return copies


def add_skin(glb, shape, ids, worlds, parents, names, warnings):
    instance = shape.skin_instance
    if not instance.bones or any(b not in ids for b in instance.bones):
        raise ValueError("Skin references missing bones")
    inverse_bind, correction = source_skin_matrices(shape, worlds)
    corrected = corrected_joints(glb, instance.bones, ids, parents, correction, names)
    joints, weights = skin_arrays(shape, warnings)
    if joints.max() >= len(instance.bones):
        raise ValueError("Skin joint index outside bone table")
    skin = {"joints": [corrected[b] for b in instance.bones],
            "inverseBindMatrices": glb.accessor([m.T.reshape(16) for m in inverse_bind], "MAT4")}
    glb.doc["skins"].append(skin)
    return len(glb.doc["skins"]) - 1, joints, weights, inverse_bind, correction


def normals_for(vertices, triangles):
    normals = np.zeros_like(vertices)
    face = np.cross(vertices[triangles[:, 1]] - vertices[triangles[:, 0]],
                    vertices[triangles[:, 2]] - vertices[triangles[:, 0]])
    for slot in range(3):
        np.add.at(normals, triangles[:, slot], face)
    return normals


def mesh_attributes(glb, shape):
    geometry = shape.data
    vertices = np.array([v.as_list() for v in geometry.vertices])
    triangles = np.array(list(geometry.get_triangles()), dtype="<u4")
    if not len(triangles) or triangles.max() >= len(vertices):
        raise ValueError("Invalid triangle indices")
    normals = np.array([n.as_list() for n in geometry.normals]) if geometry.has_normals else normals_for(vertices, triangles)
    lengths = np.linalg.norm(normals, axis=1)
    normals[lengths < 1e-10] = [0, 0, 1]
    normals /= np.linalg.norm(normals, axis=1)[:, None]
    attributes = {"POSITION": glb.accessor(vertices, "VEC3", target=34962, bounds=True),
                  "NORMAL": glb.accessor(normals, "VEC3", target=34962)}
    if geometry.uv_sets:
        # NIF and glTF both store texture coordinates from the upper-left corner.
        uv = [[v.u, v.v] for v in geometry.uv_sets[0]]
        attributes["TEXCOORD_0"] = glb.accessor(uv, "VEC2", target=34962)
    primitive = {"attributes": attributes,
                 "indices": glb.accessor(triangles.reshape(-1), "SCALAR", "<u4", target=34963)}
    return primitive, vertices, len(triangles)


def skinned_points(shape, vertices, joints, weights, inverse_bind, worlds, correction):
    result = np.zeros_like(vertices)
    for i, bone in enumerate(shape.skin_instance.bones):
        # A vertex can mention the same bone more than once in malformed assets.
        influence = np.sum(np.where(joints == i, weights, 0), axis=1)
        result += transform_points(correction @ worlds[bone] @ inverse_bind[i], vertices) * influence[:, None]
    return result


def add_shape(glb, shape, ids, worlds, parents, names, assets, folders, warnings):
    primitive, vertices, triangles = mesh_attributes(glb, shape)
    primitive["material"] = mesh_material(glb, shape, assets, folders, warnings)
    node = glb.doc["nodes"][ids[shape]]
    points = transform_points(worlds[shape], vertices)
    if shape.skin_instance:
        skin, joints, weights, inverse_bind, correction = add_skin(glb, shape, ids, worlds, parents, names, warnings)
        primitive["attributes"].update(JOINTS_0=glb.accessor(joints, "VEC4", "<u2", target=34962),
                                        WEIGHTS_0=glb.accessor(weights, "VEC4", target=34962))
        node["skin"] = skin
        # glTF skin matrices ignore the mesh node's own transform. Keep the source
        # node (and its attachments) intact and put the skinned mesh on a sibling.
        mesh_node = {"name": node["name"] + "_Skin", "skin": skin}
        glb.doc["nodes"].append(mesh_node)
        glb.doc["nodes"][0]["children"].append(len(glb.doc["nodes"]) - 1)
        del node["skin"]
        node = mesh_node
        points = skinned_points(shape, vertices, joints, weights, inverse_bind, worlds, correction)
    node["mesh"] = len(glb.doc["meshes"])
    glb.doc["meshes"].append({"name": text(shape.name), "primitives": [primitive]})
    return points, triangles


def geometry(data, assets, folders, warnings):
    glb = Glb()
    ids, names, worlds, parents = add_hierarchy(glb, data)
    all_points, triangle_count = [], 0
    for shape in ids:
        if not isinstance(shape, N.NiGeometry) or not shape.data or not shape.data.num_vertices:
            continue
        if not hasattr(shape.data, "get_triangles"):
            warnings.append("Non-triangle effect geometry omitted: " + text(shape.name))
            continue
        # Hidden meshes generally are projectile/effect attachments.
        if shape.flags & 1:
            warnings.append("Hidden geometry omitted: " + text(shape.name))
            continue
        points, triangles = add_shape(glb, shape, ids, worlds, parents, names, assets, folders, warnings)
        all_points.append(points)
        triangle_count += triangles
    if not all_points:
        raise ValueError("NIF contains no visible triangle meshes")
    bounds = normalize(glb, np.concatenate(all_points))
    return glb, names, triangle_count, bounds


def normalize(glb, points):
    # Right-handed -90 degrees around X: source (x,y,z) -> Godot (x,z,-y).
    change = np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], dtype=float)
    points = points @ change.T
    low, high = points.min(axis=0), points.max(axis=0)
    center = (low + high) / 2
    center[1] = low[1]
    points -= center
    radius = np.linalg.norm(points[:, [0, 2]], axis=1).max()
    height = high[1] - low[1]
    scale = min(0.60 / max(height, 1e-6), 0.48 / max(radius, 1e-6))
    node = glb.doc["nodes"][0]
    node.update(rotation=[-2**-0.5, 0, 0, 2**-0.5], scale=[scale]*3,
                translation=(-center * scale).tolist())
    return {"height": float(height * scale), "radius": float(radius * scale),
            "feetY": 0.0, "scale": float(scale)}
