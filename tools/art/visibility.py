"""Convert source visibility controllers to glTF scale channels.

Civ4 units carry spare props (a second staff, a bow on the back, knives) that each animation shows or hides with an
NiVisController. glTF has no core visibility channel, so a hidden subtree is scaled to almost zero instead. The
converter inserts a dedicated "_Vis" node under every controlled node, so these channels never compete with the
node's own transform tracks. A skinned mesh ignores its node transform, so a skinned mesh under a controlled node is
bound to proxy joints (children of the real joints) that carry the same channel and collapse the mesh when hidden.

Every clip animates every visibility node, because an animation player keeps the last value of a property that the
next clip does not touch. Clips without a track for a controlled node use the node's own default visibility.
"""
import numpy as np
from pyffi.formats.nif import NifFormat as N

from glb_writer import text

HIDDEN_SCALE = 1e-6  # Godot cannot decompose an exactly singular basis.


def clip_tracks(data):
    """{node name: (times, visible flags)} for every NiVisController in a KF, times relative to the clip start."""
    tracks = {}
    for sequence in data.roots:
        if not hasattr(sequence, "controlled_blocks"):
            continue
        start = sequence.start_time
        for block in sequence.controlled_blocks:
            interp = block.interpolator
            if text(block.get_controller_type()) != "NiVisController":
                continue
            if not isinstance(interp, (N.NiBoolInterpolator, N.NiBoolTimelineInterpolator)):
                continue
            keys = list(interp.data.data.keys) if interp.data else []
            if keys:
                times = np.maximum(np.array([k.time for k in keys], dtype=float) - start, 0)
                values = np.array([bool(k.value) for k in keys])
            else:
                times, values = np.zeros(1), np.array([bool(interp.bool_value)])
            tracks[text(block.get_node_name())] = (times, values)
    return tracks


def visible_at(track, t):
    times, values = track
    i = np.searchsorted(times, t, side="right") - 1
    return bool(values[max(i, 0)])


def combined(names, tracks, defaults, length):
    """Step keys for a node that is visible only while every named source node is visible."""
    times = np.unique(np.r_[0.0, [t for n in names if n in tracks for t in tracks[n][0]]])
    times = times[times <= max(length, 0) + 1e-6]
    values = [all(visible_at(tracks[n], t) if n in tracks else defaults[n] for n in names) for t in times]
    # Drop keys that repeat the previous value; the first key always stays.
    keep = [0] + [i for i in range(1, len(values)) if values[i] != values[i - 1]]
    return times[keep], [values[i] for i in keep]


def append_channels(glb, animation, vis_nodes, tracks, defaults, length):
    """Add one STEP scale channel per visibility node; returns the number of channels added."""
    for node, names in vis_nodes.items():
        times, values = combined(names, tracks, defaults, length)
        scales = [[1.0] * 3 if v else [HIDDEN_SCALE] * 3 for v in values]
        sampler = {"input": glb.accessor(times, "SCALAR", bounds=True),
                   "output": glb.accessor(scales, "VEC3"), "interpolation": "STEP"}
        animation["channels"].append({"sampler": len(animation["samplers"]),
                                      "target": {"node": node, "path": "scale"}})
        animation["samplers"].append(sampler)
    return len(vis_nodes)
