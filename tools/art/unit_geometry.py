"""Convert NIF hierarchy, geometry, materials, and skin bindings."""
import numpy as np
from PIL import Image
from pyffi.formats.nif import NifFormat as N

from glb_writer import Glb, compose, decompose, matrix, text, transform_points


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


def soft_alpha(path):
    """True when a noticeable share of the texture is partly transparent, such as a rotor blur."""
    with Image.open(path) as image:
        if "A" not in image.getbands():
            return False
        alpha = np.asarray(image.getchannel("A"))
    return float(((alpha > 8) & (alpha < 247)).mean()) > 0.05


def material_texture(glb, prop, assets, folders, warnings):
    """(texture index, team colour flag, texture has soft alpha)."""
    source, team_color = diffuse_source(prop)
    if source is None:
        return None, team_color, False
    filename = source.file_name
    path = assets.resolve(filename, folders)
    if not path:
        warnings.append("Missing texture: " + text(filename))
        return None, team_color, False
    try:
        return glb.texture(path, team_color), team_color, soft_alpha(path)
    except OSError as error:
        warnings.append("Unreadable diffuse texture: " + text(filename) + ": " + str(error))
        return None, team_color, False


def mesh_material(glb, shape, assets, folders, warnings):
    material = {"name": "Diffuse", "pbrMetallicRoughness": {"metallicFactor": 0, "roughnessFactor": 1},
                "doubleSided": True}
    team_color = soft = False
    alpha = None
    for prop in shape.properties:
        if isinstance(prop, N.NiTexturingProperty):
            texture, team_color, soft = material_texture(glb, prop, assets, folders, warnings)
            if texture is not None:
                material["pbrMetallicRoughness"]["baseColorTexture"] = {"index": texture}
        if isinstance(prop, N.NiAlphaProperty):
            alpha = prop
    if alpha is not None:
        blend, test = alpha.flags & 1, alpha.flags & 512
        if blend and soft:
            # Blended textures with partial alpha (rotor blurs, smoke) must blend; a zero-threshold test would
            # otherwise draw them as solid discs.
            material["alphaMode"] = "BLEND"
        elif test or blend:
            # Opaque textures keep cheap cutouts. A zero threshold alongside blending means "skip empty texels".
            cutoff = alpha.threshold / 255 if alpha.threshold or not blend else 0.5
            material.update(alphaMode="MASK", alphaCutoff=cutoff)
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
            # Use the transform the glTF node will actually carry. Decomposition clamps zero scales (such as a
            # collapsed head bone replaced by a rigid head) to 1e-6; binding against the unclamped singular matrix
            # makes rest pose and inverse bind disagree, and partly weighted vertices shoot off as spikes.
            local = compose(decompose(matrix(obj.get_transform())))
            worlds[obj] = world(parents[obj]) @ local if obj in parents else local
        return worlds[obj]

    for obj in objects:
        world(obj)
    # Some rigs nest an identity node under each bone with the bone's own name. Gamebryo binds an animation track
    # to one node, the first found from the root; driving every namesake applies the transform twice and folds the
    # body up. Keep only the shallowest node per name.
    def depth(index):
        obj = next(o for o, i in ids.items() if i == index)
        d = 0
        while obj in parents:
            obj, d = parents[obj], d + 1
        return d
    names = {name: [min(nodes, key=lambda i: (depth(i), i))] for name, nodes in names.items()}
    return ids, names, worlds, parents


def bind_matrices(shape, worlds):
    """Per-bone matrices taking shape-local vertices to bind-pose world space.

    Each bone offset already maps shape-local vertices into that bone's space. The skin data transform is the
    shape-to-skeleton-root offset, and applying it again would shift the mesh off its bones. That is harmless while
    it is identity, but on models where it is not (the settler and archer move about 70 units), every joint pivots
    far from the mesh and the limbs swing wildly. Skeleton roots here are the scene root, so bone worlds suffice.
    """
    instance = shape.skin_instance
    return [worlds[bone] @ matrix(b.get_transform())
            for bone, b in zip(instance.bones, instance.data.bone_list)]


def add_skin(glb, shape, ids, worlds, warnings, vis):
    instance = shape.skin_instance
    if not instance.bones or any(b not in ids for b in instance.bones):
        raise ValueError("Skin references missing bones")
    joints, weights = skin_arrays(shape, warnings)
    if joints.max() >= len(instance.bones):
        raise ValueError("Skin joint index outside bone table")
    inverse_bind = [np.linalg.inv(worlds[b]) for b in instance.bones]
    controllers = vis.controllers(shape)
    joint_nodes = [ids[b] for b in instance.bones]
    if controllers:
        # The mesh ignores its own node, so hide it through proxy joints that sit exactly on the real ones.
        joint_nodes = [vis.proxy(glb, joint, text(shape.name), controllers) for joint in joint_nodes]
    skin = {"joints": joint_nodes,
            "inverseBindMatrices": glb.accessor([m.T.reshape(16) for m in inverse_bind], "MAT4")}
    glb.doc["skins"].append(skin)
    return len(glb.doc["skins"]) - 1, joints, weights


def normals_for(vertices, triangles):
    normals = np.zeros_like(vertices)
    face = np.cross(vertices[triangles[:, 1]] - vertices[triangles[:, 0]],
                    vertices[triangles[:, 2]] - vertices[triangles[:, 0]])
    for slot in range(3):
        np.add.at(normals, triangles[:, slot], face)
    return normals


def mesh_attributes(glb, shape, deform=None):
    geometry = shape.data
    vertices = np.array([v.as_list() for v in geometry.vertices])
    triangles = np.array(list(geometry.get_triangles()), dtype="<u4")
    if not len(triangles) or triangles.max() >= len(vertices):
        raise ValueError("Invalid triangle indices")
    normals = np.array([n.as_list() for n in geometry.normals]) if geometry.has_normals else normals_for(vertices, triangles)
    lengths = np.linalg.norm(normals, axis=1)
    normals[lengths < 1e-10] = [0, 0, 1]
    normals /= np.linalg.norm(normals, axis=1)[:, None]
    if deform:
        vertices, normals = deform(vertices, normals)
    attributes = {"POSITION": glb.accessor(vertices, "VEC3", target=34962, bounds=True),
                  "NORMAL": glb.accessor(normals, "VEC3", target=34962)}
    if geometry.uv_sets:
        # NIF and glTF both store texture coordinates from the upper-left corner.
        uv = [[v.u, v.v] for v in geometry.uv_sets[0]]
        attributes["TEXCOORD_0"] = glb.accessor(uv, "VEC2", target=34962)
    primitive = {"attributes": attributes,
                 "indices": glb.accessor(triangles.reshape(-1), "SCALAR", "<u4", target=34963)}
    return primitive, vertices, len(triangles)


def skin_deformer(shape, joints, weights, worlds):
    matrices = bind_matrices(shape, worlds)

    def deform(vertices, normals):
        points, result = np.zeros_like(vertices), np.zeros_like(normals)
        for i, m in enumerate(matrices):
            # A vertex can mention the same bone more than once in malformed assets.
            influence = np.sum(np.where(joints == i, weights, 0), axis=1)[:, None]
            points += transform_points(m, vertices) * influence
            result += (normals @ m[:3, :3].T) * influence
        lengths = np.linalg.norm(result, axis=1)
        result[lengths < 1e-10] = [0, 0, 1]
        return points, result / np.linalg.norm(result, axis=1)[:, None]
    return deform


def add_shape(glb, shape, ids, worlds, assets, folders, warnings, vis):
    deform = skin = None
    if shape.skin_instance:
        skin, joints, weights = add_skin(glb, shape, ids, worlds, warnings, vis)
        deform = skin_deformer(shape, joints, weights, worlds)
    primitive, vertices, triangles = mesh_attributes(glb, shape, deform)
    primitive["material"] = mesh_material(glb, shape, assets, folders, warnings)
    node = glb.doc["nodes"][vis.host(ids[shape])]
    points = transform_points(worlds[shape], vertices)
    if skin is not None:
        primitive["attributes"].update(JOINTS_0=glb.accessor(joints, "VEC4", "<u2", target=34962),
                                        WEIGHTS_0=glb.accessor(weights, "VEC4", target=34962))
        # glTF skin matrices ignore the mesh node's own transform, and vertices
        # are already in bind-pose world space. Use a root-level sibling node.
        mesh_node = {"name": node["name"] + "_Skin", "skin": skin}
        glb.doc["nodes"].append(mesh_node)
        glb.doc["nodes"][0]["children"].append(len(glb.doc["nodes"]) - 1)
        node = mesh_node
        points = vertices
    node["mesh"] = len(glb.doc["meshes"])
    glb.doc["meshes"].append({"name": text(shape.name), "primitives": [primitive]})
    return points, triangles


class Visibility:
    """Visibility nodes for subtrees that animations show and hide; see visibility.py."""

    def __init__(self, glb, ids, parents, controlled, shown):
        self.parents, self.controlled, self.shown = parents, controlled, shown
        self.nodes = {}  # glTF node index -> source node names that must all be visible
        self.hosts = {}  # source node index -> its visibility node, which now holds its children and mesh
        self.defaults = {}
        for obj, index in ids.items():
            name = text(obj.name)
            if name not in controlled:
                continue
            self.defaults[name] = not obj.flags & 1
            source = glb.doc["nodes"][index]
            vis = {"name": name + "_Vis", **self.rest([name])}
            if "children" in source:
                vis["children"] = source.pop("children")
            glb.doc["nodes"].append(vis)
            source["children"] = [len(glb.doc["nodes"]) - 1]
            self.hosts[index] = len(glb.doc["nodes"]) - 1
            self.nodes[self.hosts[index]] = [name]

    def rest(self, names):
        """Rest scale matches the first idle frame, so bounds and rest poses leave hidden props out."""
        return {} if all(self.shown(n) for n in names) else {"scale": [1e-6] * 3}

    def host(self, index):
        return self.hosts.get(index, index)

    def controllers(self, obj):
        """Controlled source nodes on the path from the scene root to obj, inclusive."""
        names = []
        while obj is not None:
            if text(obj.name) in self.controlled:
                names.append(text(obj.name))
            obj = self.parents.get(obj)
        return names

    def hidden(self, obj):
        return not all(self.shown(n) for n in self.controllers(obj))

    def proxy(self, glb, joint, owner, names):
        node = {"name": f"{glb.doc['nodes'][joint]['name']}_{owner}_Vis", **self.rest(names)}
        glb.doc["nodes"].append(node)
        index = len(glb.doc["nodes"]) - 1
        parent = glb.doc["nodes"][joint]
        parent.setdefault("children", []).append(index)
        self.nodes[index] = names
        return index


def geometry(data, assets, folders, warnings, controlled=frozenset(), shown=lambda name: True):
    """Convert the NIF scene. Nodes named in controlled get visibility nodes; shown(name) is the idle start state."""
    glb = Glb()
    ids, names, worlds, parents = add_hierarchy(glb, data)
    vis = Visibility(glb, ids, parents, controlled, shown)
    all_points, visible_points, triangle_count = [], [], 0
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
        points, triangles = add_shape(glb, shape, ids, worlds, assets, folders, warnings, vis)
        all_points.append(points)
        if not vis.hidden(shape):
            visible_points.append(points)
        triangle_count += triangles
    if not all_points:
        raise ValueError("NIF contains no visible triangle meshes")
    bounds = normalize(glb, np.concatenate(visible_points or all_points))
    return glb, names, triangle_count, bounds, vis


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
