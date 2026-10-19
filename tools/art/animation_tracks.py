"""Convert source transform interpolators to glTF TRS channels."""
import math
import re

import numpy as np

from glb_writer import quaternion

# Preference order matters. Never select fade or cross-fade sequences.
CLIPS = {
    "idle": ("idle", "ranged_idle", "rangedidle"),
    "run": ("run", "walk", "move"),
    "fortify": ("fortifya", "fortifya_idle", "rangedfortify", "fortify"),
    "attack": ("strikea", "strikeb", "rangedstrike", "rangedstrikea", "strike", "attack", "bombard"),
    "hurt": ("hurta", "rangedhurta", "hurt"),
    "die": ("diea", "dieb", "rangeddiea", "die", "death"),
}


def choose_clips(paths):
    result = {}
    for clip, suffixes in CLIPS.items():
        for suffix in suffixes:
            matches = [p for p in paths if re.search(r"(?:_|^)" + suffix + r"\.kf$", p.name.lower())]
            if matches:
                # A suffix-only match also finds fortifya_idle. Prefer the
                # actual MD idle sequence before any contextual idle variant.
                result[clip] = min(matches, key=lambda p: (not p.name.lower().endswith("_md_" + suffix + ".kf"), p.name))
                break
    return result


def spline_value(points, u):
    """Evaluate the open uniform cubic basis used by NiBSplineBasisData."""
    n = len(points)
    degree = min(3, n - 1)
    knots = np.r_[np.zeros(degree + 1), np.arange(1, n - degree),
                  np.full(degree + 1, n - degree)]
    x = np.clip(u, 0, 1) * (n - degree)
    k = min(max(int(np.searchsorted(knots, x, side="right")) - 1, degree), n - 1)
    values = [np.array(points[j + k - degree], dtype=float) for j in range(degree + 1)]
    for r in range(1, degree + 1):
        for j in range(degree, r - 1, -1):
            i = j + k - degree
            denominator = knots[i + degree - r + 1] - knots[i]
            alpha = (x - knots[i]) / denominator if denominator else 0
            values[j] = (1 - alpha) * values[j - 1] + alpha * values[j]
    return values[degree]


def sample_times(start, stop):
    return np.linspace(start, stop, max(2, math.ceil((stop - start) * 30) + 1))


def vector(v):
    return np.array(v.as_list(), dtype=float)


def constant(interp, path):
    attr = {"translation": "translation", "rotation": "rotation", "scale": "scale"}[path]
    value = getattr(interp, attr)
    if path == "rotation":
        raw = np.array([value.x, value.y, value.z, value.w])
        return quaternion(value) if np.max(np.abs(raw)) < 1e10 else None
    value = np.full(3, value) if path == "scale" else vector(value)
    return value if np.all(np.isfinite(value)) and np.max(np.abs(value)) < 1e10 else None


def spline_track(interp, path, start, stop):
    getter = {"translation": "get_translations", "rotation": "get_rotations", "scale": "get_scales"}[path]
    points = list(getattr(interp, getter)())
    if not points:
        return None
    times = sample_times(start, stop)
    span = interp.stop_time - interp.start_time
    values = np.array([spline_value(points, (t - interp.start_time) / span) for t in times])
    if path == "rotation":
        values = values[:, [1, 2, 3, 0]]  # compressed controls store w,x,y,z
    if path == "scale":
        values = np.repeat(values.reshape(-1, 1), 3, axis=1)
    return times, values


def key_values(keys, path):
    if path == "rotation":
        return np.array([quaternion(k.value) for k in keys])
    if path == "scale":
        return np.array([[k.value] * 3 for k in keys])
    return np.array([vector(k.value) for k in keys])


def vector_key_track(group, path, start, stop):
    keys = list(group.keys)
    if not keys:
        return None
    times = np.array([k.time for k in keys])
    values = key_values(keys, path)
    if group.interpolation == 1 or len(keys) == 1:
        return times, values
    if group.interpolation != 2:
        # TBC keys are rare. Preserve their authored values and timing.
        return times, values
    output_times = np.unique(np.r_[sample_times(start, stop), times])
    result = []
    for t in output_times:
        i = min(max(np.searchsorted(times, t, side="right") - 1, 0), len(keys) - 2)
        dt = times[i + 1] - times[i]
        u = np.clip((t - times[i]) / dt, 0, 1)

        forward = keys[i].forward
        backward = keys[i + 1].backward
        a = np.full(3, forward) if path == "scale" else vector(forward)
        b = np.full(3, backward) if path == "scale" else vector(backward)
        result.append((2*u**3-3*u**2+1)*values[i] + (u**3-2*u**2+u)*a
                      + (-2*u**3+3*u**2)*values[i+1] + (u**3-u**2)*b)
    return output_times, np.array(result)


def euler_track(data, start, stop):
    times = sample_times(start, stop)
    angles = []
    for group in data.xyz_rotations:
        keys = list(group.keys)
        angles.append(np.interp(times, [k.time for k in keys], [k.value for k in keys])
                      if keys else np.zeros(len(times)))
    values = []
    for x, y, z in zip(*angles):
        # Gamebryo XYZ Euler keys rotate about X, then Y, then Z: R = Rz @ Ry @ Rx. The opposite order turns
        # hulls and limbs driven by these tracks far from their rest orientation (120 degrees on Helgoland).
        cx, cy, cz = np.cos(np.array([x, y, z]) / 2)
        sx, sy, sz = np.sin(np.array([x, y, z]) / 2)
        q = np.array([sx*cy*cz-cx*sy*sz, cx*sy*cz+sx*cy*sz,
                      cx*cy*sz-sx*sy*cz, cx*cy*cz+sx*sy*sz])
        values.append(q / np.linalg.norm(q))
    return times, np.array(values)


def key_track(data, path, start, stop):
    if path == "rotation":
        if data.rotation_type == 4:
            return euler_track(data, start, stop)
        keys = list(data.quaternion_keys)
        if not keys:
            return None
        return np.array([k.time for k in keys]), key_values(keys, path)
    return vector_key_track(getattr(data, {"translation": "translations", "scale": "scales"}[path]),
                            path, start, stop)


def interpolator_track(interp, path, start, stop):
    if hasattr(interp, "get_translations"):
        track = spline_track(interp, path, start, stop)
    elif hasattr(interp, "data") and interp.data:
        track = key_track(interp.data, path, start, stop)
    else:
        track = None
    if track is not None:
        return track
    value = constant(interp, path)
    return (np.array([start, stop]), np.array([value, value])) if value is not None else None


def append_channel(glb, animation, node, path, track, start, stop):
    times, values = track
    # Clamp source keys to clip bounds. Clips commonly contain keys before start.
    order = np.argsort(times, kind="stable")
    times, values = times[order], values[order]
    unique = np.r_[True, np.diff(times) > 1e-6]
    times, values = times[unique], values[unique]
    if path == "rotation":
        values /= np.linalg.norm(values, axis=1)[:, None]
        for i in range(1, len(values)):
            if np.dot(values[i - 1], values[i]) < 0:
                values[i] *= -1
    keep = (times >= start - 1e-6) & (times <= stop + 1e-6)
    times, values = times[keep], values[keep]
    if not len(times):
        return
    times = np.maximum(times - start, 0)
    # Float32 conversion can collapse close source times.
    unique = np.r_[True, np.diff(times.astype("<f4")) > 0]
    times, values = times[unique], values[unique]
    sampler = {"input": glb.accessor(times, "SCALAR", bounds=True),
               "output": glb.accessor(values, "VEC4" if path == "rotation" else "VEC3"),
               "interpolation": "LINEAR"}
    animation["channels"].append({"sampler": len(animation["samplers"]),
                                  "target": {"node": node, "path": path}})
    animation["samplers"].append(sampler)


def quat_mul(a, b):
    """Hamilton product of xyzw quaternions; a may be one quaternion and b an array of them."""
    ax, ay, az, aw = np.moveaxis(np.broadcast_to(a, np.shape(b)), -1, 0)
    bx, by, bz, bw = np.moveaxis(b, -1, 0)
    return np.stack([aw*bx + ax*bw + ay*bz - az*by, aw*by - ax*bz + ay*bw + az*bx,
                     aw*bz + ax*by - ay*bx + az*bw, aw*bw - ax*bx - ay*by - az*bz], -1)


def first_rotation(blocks, name, start, stop, rest):
    """A node's rotation at clip start: its track's first key, else the node's rest rotation."""
    for block in blocks:
        if block.get_node_name() == name and block.interpolator is not None and hasattr(block.interpolator, "rotation"):
            track = interpolator_track(block.interpolator, "rotation", start, stop)
            if track is not None:
                times, values = track
                q = values[np.argmin(times)]
                return q / np.linalg.norm(q)
    return np.array(rest.get("rotation", [0, 0, 0, 1]), dtype=float)


def accum_correction(glb, blocks, accum_root, node_names, start, stop):
    """Rotation that re-poses the accumulation root when a clip starts in another rig's orientation.

    Gamebryo splits the root pose between the accumulation root ("MD") and "MD NonAccum"; only their product is
    meaningful. A model's own clips start with that product equal to the rest pose. A borrowed clip may not: the
    cow rests turned 120 degrees, and the cavalry clips it uses would lay it on its side. The correction is
    rest product * inverse(first-key product), applied before the MD keys; it is identity for native clips.
    """
    root = node_names.get(accum_root, [None])[0]
    if root is None:
        return None
    nodes = [glb.doc["nodes"][root]]
    sub = node_names.get(accum_root + b" NonAccum", [None])[0]
    if sub is not None:
        nodes.append(glb.doc["nodes"][sub])
    rest = first = np.array([0, 0, 0, 1.0])
    for node, name in zip(nodes, [accum_root, accum_root + b" NonAccum"]):
        rest = quat_mul(rest, np.array(node.get("rotation", [0, 0, 0, 1]), dtype=float))
        first = quat_mul(first, first_rotation(blocks, name, start, stop, node))
    x, y, z, w = first
    correction = quat_mul(rest, np.array([-x, -y, -z, w]))
    return None if abs(correction[3]) > 1 - 1e-4 else correction


def append_block(glb, animation, block, node_names, start, stop, accum_root=None, correction=None):
    name = block.get_node_name()
    nodes = node_names.get(name, [])
    interp = block.interpolator
    if not nodes or interp is None or not hasattr(interp, "rotation"):
        return  # Material, alpha, visibility, and particle tracks are not TRS.
    for path in ("translation", "rotation", "scale"):
        track = interpolator_track(interp, path, start, stop)
        if track is None:
            continue
        if path == "rotation" and correction is not None and name == accum_root:
            track = (track[0], quat_mul(correction, track[1]))
        for node in nodes:
            append_channel(glb, animation, node, path, track, start, stop)


def convert_animation(glb, data, name, node_names, warnings, extra=None):
    """Append one clip. extra(animation, length) may add more channels, such as visibility."""
    animation = {"name": name, "channels": [], "samplers": []}
    length = None
    for sequence in data.roots:
        if not hasattr(sequence, "controlled_blocks"):
            continue
        start, stop = sequence.start_time, sequence.stop_time
        if stop <= start or stop - start > 3600:
            warnings.append(f"{name}: invalid source duration")
            continue
        length = stop - start if length is None else length
        accum_root = getattr(sequence, "target_name", None) or None  # pyffi names the accum root "target name"
        blocks = list(sequence.controlled_blocks)
        correction = accum_correction(glb, blocks, accum_root, node_names, start, stop) if accum_root else None
        for block in blocks:
            append_block(glb, animation, block, node_names, start, stop, accum_root, correction)
    if animation["channels"] and extra is not None:
        extra(animation, length)
    if animation["channels"]:
        glb.doc["animations"].append(animation)
        return len(animation["channels"])
    warnings.append(f"{name}: no matching transform tracks")
    return 0
