// Translucent water surface at y = 0. Depth comes from the terrain height baked per vertex (UV2.x = -HeightAt), so no
// depth-texture reads are needed. Fog, clouds and per-layer tints (slimy water) use the same tile blend as the terrain.
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// Coast and ocean stay close in colour; the ocean reads as deeper, not as a different sea.
uniform vec3 shallow_colour : source_color = vec3(0.07, 0.40, 0.50);
uniform vec3 deep_colour : source_color = vec3(0.07, 0.27, 0.52);
uniform float deep_depth = 0.42;
// Coast to ocean shading: a broad blend of the tiles' ocean flags (lower sharpness = wider band), with part of the depth
// shading kept so coast water still darkens away from the shore.
uniform float ocean_sharpness = 1.3;
uniform float coast_shelf = 0.3;
uniform float foam_depth = 0.022;

// Surf at the foot of cliffs: churned white water against the rock, and lines of foam that the swell draws out to sea.
uniform float cliff_churn = 0.8;
uniform float cliff_streaks = 0.3;

varying vec3 world_pos;
varying float water_depth;
// TerrainField.CliffAt: 1 at a cliff edge, falling linearly to 0 about 0.42 world units out.
varying float cliff;

void vertex() {
	world_pos = VERTEX;
	water_depth = UV2.x;
	cliff = UV2.y;
}

// Surf coverage in [0, 1] beside a cliff. The cliff share stands in for the distance to the rock face.
float cliff_surf(vec2 p, float share, float time) {
	if (share <= 0.01) return 0.0;
	float lace = seeded_noise(p + vec2(time * 0.07, -time * 0.05), 9.0, 16u) * 0.6
		+ seeded_noise(p + vec2(-time * 0.05, time * 0.08), 19.0, 17u) * 0.4;
	float fine = seeded_noise(p + vec2(time * 0.11, time * 0.04), 34.0, 18u);
	// White water against the rock: solid at the face, torn into lace and holes as it spreads out.
	float churn = smoothstep(0.8, 0.97, share + (lace - 0.5) * 0.3);
	churn *= mix(0.45, 1.0, smoothstep(0.3, 0.7, lace * 0.7 + fine * 0.3)) * mix(0.75, 1.0, smoothstep(0.92, 1.0, share));
	// Foam lines move outward from the face and break up into lace.
	float swash = 0.5 + 0.5 * sin((1.0 - share) * 24.0 - time * 1.4 + lace * 4.0);
	float streaks = smoothstep(0.6, 1.0, swash) * smoothstep(0.05, 0.6, share) * smoothstep(0.42, 0.7, lace) * (0.6 + 0.4 * fine);
	return clamp(churn * cliff_churn + streaks * cliff_streaks * (1.0 - churn), 0.0, 1.0);
}

// Wave height (world units for the normal) blending fine coastal ripples into broad ocean swells by the deep share.
// Seeded integer-hash noise: the sin hash of periodic_noise breaks at large z lattice values, and its seams showed as
// horizontal bands in the specular glare.
uniform float ripple_slope = 0.06;
uniform float swell_slope = 0.16;
float ripple(vec2 p, float time, float deep) {
	float fine = seeded_noise(p + vec2(time * 0.06, time * 0.025), 1.6, 11u) * 0.6
		+ seeded_noise(p + vec2(-time * 0.04, time * 0.07), 3.1, 12u) * 0.4;
	float h = fine * ripple_slope * mix(1.0, 0.35, deep);
	if (deep > 0.0) {
		float swell = seeded_noise(p + vec2(time * 0.035, time * 0.012), 0.55, 13u) * 0.6
			+ seeded_noise(p + vec2(-time * 0.02, time * 0.04), 1.1, 14u) * 0.4;
		h += swell * swell_slope * deep;
	}
	return h;
}

void fragment() {
	vec2 p = world_pos.xz;
	vec3 land;
	vec4 water_tint;
	float fogged, hidden;
	vec3 w;
	bool linear = !OUTPUT_IS_SRGB;
	blend_tiles(p, linear, 0.0, 0.0, land, water_tint, fogged, hidden, w);

	float shelf = smoothstep(0.0, deep_depth, water_depth);
	float deep = max(ocean_share(p, ocean_sharpness) * smoothstep(0.0, 0.1, water_depth), shelf * coast_shelf);
	vec3 bed = mix(material_sample(seabed_layer, p, linear).rgb, material_sample(deep_seabed_layer, p, linear).rgb, deep);
	vec3 colour = mix(shallow_colour, deep_colour, deep);
	colour = mix(colour, bed, mix(0.08, 0.05, deep));
	colour = mix(colour, water_tint.rgb, water_tint.a);

	float e = 0.04;
	float h0 = ripple(p, TIME, deep);
	float hx = ripple(p + vec2(e, 0.0), TIME, deep);
	float hz = ripple(p + vec2(0.0, e), TIME, deep);
	vec3 n = normalize(vec3(-(hx - h0) / e, 1.0, -(hz - h0) / e));
	NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);

	float wobble = seeded_noise(p + vec2(TIME * 0.05, 0.0), 2.4, 15u);
	// Foam lies on the shore, where the waves are the fine ripples; h0 / ripple_slope is their noise in [0, 1].
	float foam = (1.0 - smoothstep(foam_depth * 0.3, foam_depth * (0.6 + wobble), water_depth))
		* (0.45 + 0.4 * clamp(h0 / ripple_slope, 0.0, 1.0));
	foam = max(foam, cliff_surf(p, cliff, TIME));
	colour = mix(colour, vec3(0.92, 0.96, 1.0), foam);
	// Opacity follows the real depth, so shallow coast water does not turn pale over its bright seabed.
	float alpha = mix(0.84, 0.94, max(shelf, deep));
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
	// A tile spans a city's worth of sea, so the sun's reflection must not read as a nearby lamp: a weak, broad sheen
	// instead of a bright glint.
	ROUGHNESS = mix(0.35, 1.0, max(sky.a, fogged * 0.5));
	SPECULAR = 0.12 * (1.0 - sky.a) * fog;
}
