// Waterfall water (WaterfallMesher): the glassy tongue off the ledge rock, the white falling sheet and the veil of strands
// in front of it, and the churned plunge pool. UV = (across, down): across runs -1..1 over the lip (round the pool), down
// is the distance fallen from the brink (across the pool, the share of its radius).
// UV2 = (part, seed) and COLOR = (foam, fade):
// part 0 lip, 1 sheet, 2 pool, 3 veil; seed shifts the noise; foam is how churned the water is (0 behind the ledge,
// LIP_FOAM at its nose, 1 where it lands); fade drops to 0 where the tongue fades in over the river behind the ledge
// and at a pool's rim.
// The falling water is drawn as aerated white water rather than as river water: bright streaks racing down, blue-grey
// only in the thin gaps near the top, ragged at the sides, and lit by its own glow so it stays white in the cliff's shade.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// Lifts the water toward the camera so it wins against the rock and ground right behind it.
uniform float depth_bias = 0.02;
// How fast the streaks race down the sheet, in sheet heights per second.
uniform float fall_speed = 1.6;

// The tongue starts as the river's own water (river.gdshader's deep and shallow tones) and clears to glass over the nose.
const vec3 RIVER_DEEP = vec3(0.23, 0.36, 0.45);
const vec3 RIVER_SHALLOW = vec3(0.45, 0.55, 0.56);
const vec3 RIVER_LIGHT = vec3(0.72, 0.80, 0.83);
const vec3 GLASS = vec3(0.42, 0.56, 0.60);
const vec3 GAP = vec3(0.62, 0.72, 0.77);
const vec3 FOAM = vec3(0.95, 0.97, 0.98);
const vec3 POOL = vec3(0.47, 0.60, 0.62);
// WaterfallMesher.LipFoam: how churned the water is as it leaves the nose.
const float LIP_FOAM = 0.3;
// Share of a pool's radius that is solid foam (WaterfallMesher.PoolSolid), and where its core starts to break up.
const float POOL_SOLID = 0.6;
const float POOL_CORE = 0.35;

varying vec3 world_pos;
varying vec2 lane;
varying vec4 info;
varying vec2 kind;

void vertex() {
	world_pos = VERTEX;
	lane = UV;
	info = COLOR;
	kind = UV2;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(-view.xyz) * depth_bias;
	POSITION = PROJECTION_MATRIX * view;
}

// Value noise in [0, 1] on the sheet's own coordinates (not world space, so it needs no wrap).
float value_noise(vec2 p) {
	vec2 f = floor(p);
	vec2 t = p - f;
	t = t * t * (3.0 - 2.0 * t);
	ivec2 c = ivec2(f);
	float a = lattice_hash(c, 7u), b = lattice_hash(c + ivec2(1, 0), 7u);
	float d = lattice_hash(c + ivec2(0, 1), 7u), e = lattice_hash(c + ivec2(1, 1), 7u);
	return mix(mix(a, b, t.x), mix(d, e, t.x), t.y);
}

// Streaks down a sheet: noise stretched along the fall and scrolled down it, two octaves at different speeds so the
// pattern never repeats as a block.
float streaks(float across, float down, float seed, float time) {
	float coarse = value_noise(vec2(across * 7.0 + seed * 31.0, down * 9.0 - time * fall_speed * 2.2));
	float fine = value_noise(vec2(across * 19.0 + seed * 17.0 + 4.0, down * 26.0 - time * fall_speed * 3.6));
	return coarse * 0.6 + fine * 0.4;
}

void fragment() {
	bool linear = !OUTPUT_IS_SRGB;
	vec2 p = world_pos.xz;
	float fogged, hidden;
	sight_blend(p, fogged, hidden);
	float part = kind.x, seed = kind.y, foam = info.r, fade = info.g;
	float across = lane.x, down = lane.y;
	vec3 colour;
	float alpha;
	float glow;
	float roughness = 0.9, specular = 0.1;
	if (part > 1.5 && part < 2.5) {
		// The plunge pool: a solid boil of foam where the sheet lands, breaking into drifting scum toward the rim. The
		// foam is laid out in world space, so it has no seam round the pool.
		float r = down;
		vec2 q = p * 55.0 + vec2(seed * 37.0, seed * 19.0);
		float boil = value_noise(q + vec2(TIME * 0.7, -TIME * 0.4)) * 0.55
			+ value_noise(q * 2.3 + vec2(-TIME * 1.1, TIME * 0.8)) * 0.45;
		float cover = 1.0 - r;
		float core = 1.0 - smoothstep(POOL_CORE, POOL_SOLID, r + (boil - 0.5) * 0.2);
		float white = max(smoothstep(1.0 - cover, 1.15 - cover * 0.9, boil), core);
		colour = mix(decode_colour(POOL, linear), decode_colour(FOAM, linear), white);
		alpha = max(mix(0.3, 0.97, white), core) * (1.0 - smoothstep(POOL_SOLID, 1.0, r)) * fade;
		glow = 0.4 * white;
	} else {
		// The water over the ledge and down the fall, one surface from the river's own water behind the rock to the
		// white sheet: foam rises continuously down it (to LIP_FOAM at the nose, then on to 1 where it lands), and the
		// colour, the streaks, the ragged sides and the shine all follow foam, so there is no seam where it turns down.
		// The veil in front is the same water torn into separate strands.
		bool veil = part > 2.5;
		float fallen = clamp((foam - LIP_FOAM) / (1.0 - LIP_FOAM), 0.0, 1.0);
		float s = streaks(across, down, seed, TIME);
		float white = smoothstep(0.95 - 0.75 * foam, 1.1 - 0.6 * foam, s);
		float centre = 1.0 - smoothstep(0.1, 0.95, abs(across));
		vec3 river = mix(decode_colour(RIVER_SHALLOW, linear), decode_colour(RIVER_DEEP, linear), centre * 0.7);
		river = mix(river, decode_colour(RIVER_LIGHT, linear), 0.2);
		vec3 base = mix(river, decode_colour(GLASS, linear), smoothstep(0.0, LIP_FOAM, foam));
		base = mix(base, decode_colour(GAP, linear), smoothstep(0.0, 0.3, fallen));
		colour = mix(base, decode_colour(FOAM, linear), white);
		// Ragged sides: once over the nose the sheet frays into threads, more so as it falls.
		float fray = value_noise(vec2(across * 6.0 + seed * 3.0, down * 12.0 - TIME * fall_speed * 2.0));
		float body = smoothstep(0.0, 0.25, 1.0 - abs(across) - (fray - 0.3) * 0.5 * fallen);
		if (veil) {
			// Strands only, coming away from the sheet below the nose and thinning out toward the bottom, where the
			// veil has turned to spray.
			body *= smoothstep(0.5, 0.75, s) * smoothstep(0.0, 0.15, fallen) * (1.0 - smoothstep(0.7, 1.0, fallen / 0.75));
			alpha = body * 0.7;
		} else {
			// Solid over the ledge rock, so the dark rock does not show through and darken the water.
			alpha = body * 0.97 * fade;
		}
		glow = 0.5 * foam + 0.25 * white;
		float rough = smoothstep(0.0, 0.6, foam);
		roughness = mix(0.25, 0.9, rough);
		specular = mix(0.45, 0.1, rough);
	}
	colour = mix(colour, fog_tone(colour) * 1.1, fogged);
	alpha *= 1.0 - tile_lines(p) * 0.5;
	if (hidden > 0.0) alpha *= 1.0 - clouds(p, hidden, TIME, linear).a;

	ALBEDO = colour;
	ALPHA = clamp(alpha, 0.0, 1.0);
	// White water scatters light in every direction, so it glows a little even where the cliff shades it.
	EMISSION = colour * glow * (1.0 - fogged * 0.7);
	ROUGHNESS = roughness;
	SPECULAR = specular;
}
