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
// Points toward the scene's directional light, as in terrain.gdshader; ice floes cast their shadows away from it.
uniform vec3 sun_direction = vec3(-0.542, 0.643, -0.542);

varying vec3 world_pos;
varying float water_depth;
// TerrainField.LedgeAt: 1 at a cliff edge, falling linearly to 0 about 0.42 world units out.
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

// Sea ice: Voronoi floes over a period-wrapped cell lattice, at two sizes. Returns (1 if the cell holds a floe, else 0;
// distance to the cell's edge in world units; the cell's own random value). cover is the share of cells that hold a floe.
vec3 floe_cells(vec2 p, float frequency, float cover, uint seed) {
	int cells = max(1, int(round(period * frequency)));
	vec2 u = p * (float(cells) / period);
	vec2 f = floor(u);
	float best = 9.0, second = 9.0, pick = 0.0;
	for (int j = -1; j <= 1; j++)
		for (int i = -1; i <= 1; i++) {
			vec2 c = f + vec2(float(i), float(j));
			ivec2 cell = ivec2(int(mod(c.x, float(cells))), int(c.y));
			vec2 point = c + vec2(lattice_hash(cell, seed), lattice_hash(cell, seed + 1u)) * 0.8 + 0.1;
			float d = length(u - point);
			if (d < best) {
				second = best;
				best = d;
				pick = lattice_hash(cell, seed + 2u);
			} else if (d < second) second = d;
		}
	// Edge distance in world units, so both sizes share one gap.
	float edge = (second - best) * 0.5 * period / float(cells);
	return vec3(step(pick, cover), edge, pick);
}

// The floe cell at p: a large one where a large cell holds a floe, otherwise a small one. Returns (holds a floe, edge
// distance less the open-water gap around the floe, so > 0 on the floe, random value).
vec3 floes(vec2 p, float cover, float gap) {
	// A warp rounds the cells into irregular floes.
	vec2 q = p + (vec2(seeded_noise(p, 4.0, 76u), seeded_noise(p, 4.0, 77u)) - 0.5) * 0.12;
	vec3 large = floe_cells(q, 2.6, cover * 0.85, 70u);
	if (large.x > 0.0) return vec3(1.0, large.y - gap, large.z);
	vec3 small = floe_cells(q, 7.0, cover, 80u);
	return vec3(small.x, small.y - gap * 0.7, small.z);
}

// Floe height map, in world units above the water. Each floe is a slab of its own thickness whose sides fall steeply to
// the water over floe_bevel, with snow drifts on top. 0 in the open water.
uniform float floe_thickness = 0.03;
uniform float floe_bevel = 0.008;
float floe_height(vec3 f, vec2 p) {
	if (f.x <= 0.0 || f.y <= 0.0) return 0.0;
	float slab = floe_thickness * mix(0.5, 1.0, f.z);
	float drifts = seeded_noise(p, 9.0, 81u) * 0.55 + seeded_noise(p, 24.0, 82u) * 0.3 + seeded_noise(p, 60.0, 83u) * 0.15;
	float side = smoothstep(0.0, floe_bevel, f.y);
	// The drifts thicken inward from the floe's edge, so its rim stays crisp.
	return slab * side * (0.7 + 0.6 * drifts * smoothstep(0.0, 0.05, f.y));
}

float ice_at(vec2 p, float cover, float gap) {
	return floe_height(floes(p, cover, gap), p);
}

// Whitecaps: wind-drawn streaks of foam on storm water, in [0, 1].
float whitecaps(vec2 p, float time) {
	vec2 q = vec2(p.x * 0.8 + p.y * 0.6, -p.x * 0.6 + p.y * 0.8);
	float crest = seeded_noise(q * vec2(1.0, 3.2) + vec2(time * 0.35, 0.0), 4.0, 73u) * 0.65
		+ seeded_noise(p + vec2(time * 0.2, time * 0.1), 11.0, 74u) * 0.35;
	return smoothstep(0.74, 0.86, crest) * (0.5 + 0.5 * seeded_noise(p, 30.0, 78u));
}

void fragment() {
	vec2 p = world_pos.xz;
	vec3 land;
	vec4 water_tint;
	float fogged, hidden;
	vec3 w;
	bool linear = !OUTPUT_IS_SRGB;
	blend_tiles(p, linear, 0.0, 0.0, land, water_tint, fogged, hidden, w);
	vec4 looks_a, looks_b;
	feature_shares(p, looks_a, looks_b);
	float ice = looks_b.x;
	float squall = looks_b.y;

	float shelf = smoothstep(0.0, deep_depth, water_depth);
	float deep = max(ocean_share(p, ocean_sharpness) * smoothstep(0.0, 0.1, water_depth), shelf * coast_shelf);
	vec4 dp = vec4(dFdx(p), dFdy(p));
	vec3 bed = mix(material_sample(seabed_layer, p, dp, linear).rgb, material_sample(deep_seabed_layer, p, dp, linear).rgb, deep);
	vec3 colour = mix(shallow_colour, deep_colour, deep);
	colour = mix(colour, bed, mix(0.08, 0.05, deep));
	colour = mix(colour, water_tint.rgb, water_tint.a);

	// A storm darkens the sea under its clouds and churns it into steep, fast swells.
	colour = mix(colour, colour * 0.35 + vec3(0.025, 0.035, 0.05), smoothstep(0.0, 0.7, squall));
	float e = 0.04;
	float storm_time = TIME * (1.0 + 1.5 * squall);
	float rough = 1.0 + 2.6 * squall;
	float h0 = ripple(p, storm_time, deep) * rough;
	float hx = ripple(p + vec2(e, 0.0), storm_time, deep) * rough;
	float hz = ripple(p + vec2(0.0, e), storm_time, deep) * rough;
	vec3 n = normalize(vec3(-(hx - h0) / e, 1.0, -(hz - h0) / e));
	NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);

	float wobble = seeded_noise(p + vec2(TIME * 0.05, 0.0), 2.4, 15u);
	// Foam lies on the shore, where the waves are the fine ripples; h0 / ripple_slope is their noise in [0, 1].
	float foam = (1.0 - smoothstep(foam_depth * 0.3, foam_depth * (0.6 + wobble), water_depth))
		* (0.45 + 0.4 * clamp(h0 / ripple_slope, 0.0, 1.0));
	foam = max(foam, cliff_surf(p, cliff, TIME));
	foam = max(foam, whitecaps(p, TIME) * squall * 0.6);
	colour = mix(colour, vec3(0.92, 0.96, 1.0), foam);
	// Ice: slush whitens the water, then floes pack tight along the shore and break up further out.
	float floe = 0.0;
	if (ice > 0.001) {
		float shore = 1.0 - smoothstep(0.02, 0.3, water_depth);
		float cover = ice * mix(0.45, 0.95, shore), gap = mix(0.035, 0.008, shore);
		float grow = smoothstep(0.15, 0.5, ice);
		vec2 drift = vec2(TIME * 0.004, 0.0);
		vec2 q = p + drift;
		vec3 slush = mix(colour, vec3(0.62, 0.74, 0.8), 0.45);
		colour = mix(colour, slush, smoothstep(0.0, 0.6, ice));
		// Parallax over the floe height map: step down the view ray from the tallest floe top to the water and stop where
		// it first meets ice, so floes stand above the sea with their sides showing.
		vec3 ray = normalize(world_pos - CAMERA_POSITION_WORLD);
		vec2 run = -ray.xz / max(-ray.y, 0.2);
		float top_y = floe_thickness * 1.2;
		const int STEPS = 8;
		float y_prev = top_y, h_prev = ice_at(q + run * top_y, cover, gap) * grow, hit_y = 0.0;
		bool hit = h_prev >= top_y;
		if (hit) hit_y = top_y;
		for (int i = 1; i <= STEPS && !hit; i++) {
			float y = top_y * (1.0 - float(i) / float(STEPS));
			float h = ice_at(q + run * y, cover, gap) * grow;
			if (h >= y && h > 0.0) {
				// Bisect between the last step above the ice and the first one inside it, so the sides are smooth.
				float above = y_prev, inside = y;
				for (int k = 0; k < 4; k++) {
					float mid = (above + inside) * 0.5;
					if (ice_at(q + run * mid, cover, gap) * grow >= mid) inside = mid;
					else above = mid;
				}
				hit_y = inside;
				hit = true;
			}
			y_prev = y;
			h_prev = h;
		}
		vec2 s = q + run * hit_y;
		// Floe shadows on the water and on lower ice, toward the scene light.
		vec2 sun_run = sun_direction.xz / sun_direction.y;
		float shade = 0.0;
		for (int i = 1; i <= 3; i++) {
			float y = hit_y + top_y * float(i) / 3.0;
			shade = max(shade, step(y, ice_at(s + sun_run * (y - hit_y), cover, gap) * grow));
		}
		if (hit) {
			vec3 f = floes(s, cover, gap);
			float h = floe_height(f, s) * grow;
			float e = 0.0025;
			float hx = ice_at(s + vec2(e, 0.0), cover, gap) * grow;
			float hz = ice_at(s + vec2(0.0, e), cover, gap) * grow;
			vec3 ni = normalize(vec3(-(hx - h) / e, 1.0, -(hz - h) / e));
			// Snow-white tops; the sides show wet blue ice, deepening toward the water line.
			vec3 top = mix(vec3(0.86, 0.91, 0.95), vec3(0.95, 0.97, 1.0), f.z) * mix(0.93, 1.03, seeded_noise(s, 18.0, 75u));
			vec3 wall = mix(vec3(0.32, 0.56, 0.66), vec3(0.62, 0.78, 0.86), smoothstep(0.0, 0.8, h / floe_thickness));
			float steep = smoothstep(0.35, 0.75, 1.0 - ni.y);
			colour = mix(top, wall, steep);
			n = ni;
			floe = 1.0;
		} else {
			// Open water: the submerged part of nearby floes glows pale turquoise under the surface.
			vec3 f = floes(q, cover, gap);
			float near = f.x * grow * (1.0 - smoothstep(0.0, max(gap, 0.012), -f.y));
			colour = mix(colour, vec3(0.3, 0.62, 0.68), near * 0.55);
		}
		colour *= mix(1.0, 0.62, shade);
		NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
	}
	// Opacity follows the real depth, so shallow coast water does not turn pale over its bright seabed.
	float alpha = mix(0.84, 0.94, max(shelf, deep));
	alpha = mix(alpha, 0.95, foam);
	alpha = mix(alpha, 1.0, max(floe, squall * 0.5));

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
	// Ice is matte; under storm clouds the sea has no sun to reflect.
	ROUGHNESS = mix(mix(0.35, 0.8, floe), 1.0, max(sky.a, fogged * 0.5));
	SPECULAR = 0.12 * (1.0 - sky.a) * fog * (1.0 - 0.7 * squall) * (1.0 - 0.6 * floe);
}
