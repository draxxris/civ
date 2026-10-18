// Translucent water surface at y = 0. Depth comes from the terrain height baked per vertex (UV2.x = -HeightAt), so no
// depth-texture reads are needed. Fog, clouds and per-layer tints (slimy water) use the same tile blend as the terrain.
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

uniform vec3 shallow_colour : source_color = vec3(0.24, 0.72, 0.74);
uniform vec3 deep_colour : source_color = vec3(0.04, 0.17, 0.42);
uniform float deep_depth = 0.42;
uniform float foam_depth = 0.022;

varying vec3 world_pos;
varying float water_depth;

void vertex() {
	world_pos = VERTEX;
	water_depth = UV2.x;
}

float ripple(vec2 p, float time) {
	return periodic_noise(p + vec2(time * 0.06, time * 0.025), 1.6) * 0.6
		+ periodic_noise(p + vec2(-time * 0.04, time * 0.07), 3.1) * 0.4;
}

void fragment() {
	vec2 p = world_pos.xz;
	vec3 land;
	vec4 water_tint;
	float fogged, hidden;
	vec3 w;
	bool linear = !OUTPUT_IS_SRGB;
	blend_tiles(p, linear, 0.0, 0.0, land, water_tint, fogged, hidden, w);

	float deep = smoothstep(0.0, deep_depth, water_depth);
	vec3 bed = mix(material_sample(seabed_layer, p, linear).rgb, material_sample(deep_seabed_layer, p, linear).rgb, deep);
	vec3 colour = mix(shallow_colour, deep_colour, deep);
	colour = mix(colour, bed, mix(0.16, 0.06, deep));
	colour = mix(colour, water_tint.rgb, water_tint.a);

	float e = 0.04;
	float h0 = ripple(p, TIME);
	float hx = ripple(p + vec2(e, 0.0), TIME);
	float hz = ripple(p + vec2(0.0, e), TIME);
	vec3 n = normalize(vec3(-(hx - h0) / e * 0.06, 1.0, -(hz - h0) / e * 0.06));
	NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);

	float wobble = periodic_noise(p + vec2(TIME * 0.05, 0.0), 2.4);
	float foam = (1.0 - smoothstep(foam_depth * 0.3, foam_depth * (0.6 + wobble), water_depth)) * (0.45 + 0.4 * h0);
	colour = mix(colour, vec3(0.92, 0.96, 1.0), foam);
	float alpha = mix(0.55, 0.9, deep);
	alpha = mix(alpha, 0.95, foam);

	float fog = mix(1.0, 0.55, fogged);
	colour = mix(colour, fog_tone(colour) * 1.1, fogged);

	colour = map_overlays(vec2(wrap_world_x(p.x), p.y), colour, linear);
	// Keep the hover outline above sea level as well as above the land underneath it.
	ivec2 hex = containing_hex(p);
	if (on_map(hex) && texelFetch(tile_b, texel_of(hex), 0).b > 0.5)
		colour = mix(colour, vec3(0.9), hover_outline(p - hex_centre(hex)));
	vec4 sky = clouds(p, hidden, TIME, linear);
	ALBEDO = colour * (1.0 - sky.a);
	EMISSION = sky.rgb * sky.a;
	ALPHA = mix(alpha, 1.0, sky.a);
	ROUGHNESS = mix(0.18, 1.0, max(sky.a, fogged * 0.5));
	SPECULAR = 0.5 * (1.0 - sky.a) * fog;
}
