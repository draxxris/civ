// Soft worn-earth patches under improvement buildings (ImprovementRenderer.Decals.cs). The mesh is draped on the
// height field; vertex colour carries the earth tint times fog, and alpha carries the feathered edge. Lit like the
// terrain so the patch darkens it evenly on every slope.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_lambert, specular_disabled;

// Pull each vertex this fraction of its view distance toward the camera. It changes depth only (the screen position
// stays), so the patch wins against the terrain where the coarse terrain lattice bulges above the sampled field.
uniform float depth_pull = 0.0025;

varying vec2 ground_pos;

#include "res://shaders/hash.gdshaderinc"

// p is always a whole cell coordinate.
float hash(vec2 p) {
	return lattice_hash(ivec2(p), 41u);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
	// Canonical map position (wrap copies only translate the instance), so every copy shows the same speckle.
	ground_pos = VERTEX.xz;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz *= 1.0 - depth_pull;
	POSITION = PROJECTION_MATRIX * view;
}

void fragment() {
	// Two octaves of noise break the outline and the fill into trodden, uneven earth.
	float n = 0.65 * value_noise(ground_pos * 9.0) + 0.35 * value_noise(ground_pos * 23.0);
	float edge = COLOR.a;
	// Thin the feathered rim more than the core, so the edge frays instead of tracing a curve.
	float alpha = edge * mix(0.55, 1.15, n) * smoothstep(0.0, 0.35, edge + 0.25 * (n - 0.5));
	ALBEDO = COLOR.rgb * mix(0.85, 1.1, n);
	ALPHA = clamp(alpha, 0.0, 1.0);
	ROUGHNESS = 0.95;
}
