// Shared by the raymarched haze volumes (cloud_volume.gdshader, plume_volume.gdshader). Each volume is drawn on the front
// faces of a box mesh: a fragment is where the view ray enters the box, and the ray is marched from there to where it
// leaves. The depth test against the opaque scene stops volumes behind hills and mountains; the haze map's floor stops
// rays inside a volume at the ground, so no depth texture is read (the map must run on the Compatibility renderer).
// Box corners come per vertex: UV = (x0, z0), UV2 = (x1, z1), COLOR.rg = encoded y0 and y1 (see HazeVolume.EncodeHeight).

#include "res://shaders/hash.gdshaderinc"

// Tileable Perlin-Worley noise (CloudNoise): R = billows, G = fine cells for wispy edges.
uniform sampler3D cloud_noise : filter_linear, repeat_enable;
// Noise tiles per world unit; a whole number of tiles fits the map period, so the wrap has no seam.
uniform float noise_frequency = 1.0;
// Toward the scene's directional light, as in terrain.gdshader.
uniform vec3 sun_direction = vec3(-0.542, 0.643, -0.542);
uniform vec3 wind = vec3(0.05, 0.0, 0.02);

const float HEIGHT_MIN = -0.5;
const float HEIGHT_SPAN = 3.0;

varying vec3 local_pos;
varying vec3 box_lo;
varying vec3 box_hi;

// Box corner from its encoded per-vertex form; vertex() assigns the varyings (functions may not).
vec3 box_corner(vec2 xz, float encoded_y) {
	return vec3(xz.x, HEIGHT_MIN + encoded_y * HEIGHT_SPAN, xz.y);
}

// Distance along rd from ro (inside or on the box) to where the ray leaves the box.
float box_exit(vec3 ro, vec3 rd, vec3 lo, vec3 hi) {
	vec3 safe = mix(rd, vec3(1e-5), vec3(lessThan(abs(rd), vec3(1e-5))));
	vec3 inv = 1.0 / safe;
	vec3 t = max((lo - ro) * inv, (hi - ro) * inv);
	return max(0.0, min(min(t.x, t.y), t.z));
}

// Cloud noise at p: R = billows, G = fine cells. scale multiplies the base frequency.
vec2 cloud_noise_at(vec3 p, float scale, vec3 drift) {
	return texture(cloud_noise, p * noise_frequency * scale + drift).rg;
}

// Per-pixel start offset in [0, 1), so the march steps do not show as bands.
float march_jitter(vec2 fragcoord) {
	return lattice_hash(ivec2(fragcoord), 7u);
}
