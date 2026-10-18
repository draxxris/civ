// Dirt roads: ribbons along each road curve plus a round blob at each road tile centre (MapRenderer.Roads.cs), draped
// over the terrain and lit like it. UV.x runs across the ribbon (|x| = 1 at the outer edge of the faint verge, 0 on the
// centre line; the blob uses the radius instead). The worn middle and wheel ruts fade out near tile centres, where links
// meet. The track picks up the blended terrain colour under it: light earth over dark ground, a darker, browner take on
// pale ground, faded over several tiles. Its edge is broken up by wrap-safe noise, and fog and clouds follow sight per
// pixel like the terrain. Owner borders and the hover outline show through.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// Lifts the ribbon toward the camera so it wins against the terrain triangles it is draped over.
uniform float depth_bias = 0.03;

// sRGB colours, decoded like the terrain palette (see decode_colour): packed light earth on the track, a darker worn
// line down the middle and in the ruts, and a trampled brown verge.
const vec3 ROAD_DIRT = vec3(0.80, 0.68, 0.50);
const vec3 ROAD_VERGE = vec3(0.50, 0.44, 0.30);

varying vec3 world_pos;
varying vec2 lane;

void vertex() {
	world_pos = VERTEX;
	lane = UV;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(-view.xyz) * depth_bias;
	POSITION = PROJECTION_MATRIX * view;
}

vec3 road_colour(vec3 c, float fogged, bool linear) {
	vec3 decoded = decode_colour(c, linear);
	return mix(decoded, fog_tone(decoded), fogged);
}

// How pale the ground is near p, 0 (dark, e.g. grassland) to 1 (pale, e.g. plains or sand): each corner tile is judged
// by the average colour of its material (smallest mip), and the corners are blended linearly, not with the terrain's
// sharpened weights. So the road's earth fades gradually from one tile centre to the next instead of switching at the
// terrain border.
float corner_paleness(vec2 p, bool linear) {
	ivec2 base;
	bool upper;
	vec3 weights = blend_weights(corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper), 1.0);
	float pale = 0.0;
	for (int k = 0; k < 3; k++) {
		int layer = byte_of(texelFetch(tile_a, clamped_texel(corner_hex(base, upper, k)), 0).r);
		vec3 average = decode_colour(textureLod(terrain_textures, vec3(0.5, 0.5, float(layer)), 12.0).rgb, linear);
		pale += weights[k] * smoothstep(0.5, 0.58, dot(average, vec3(0.2126, 0.7152, 0.0722)));
	}
	return pale;
}

// Paleness averaged over a disc about three and a half tiles across (two rings of samples), so a road changes its earth
// gradually over several tiles, not at the terrain border.
float ground_paleness(vec2 p, bool linear) {
	float pale = corner_paleness(p, linear);
	for (int k = 0; k < 12; k++) {
		float a = TAU * float(k) / 12.0;
		pale += corner_paleness(p + vec2(cos(a), sin(a)) * 1.8, linear);
		if (k % 2 == 0) pale += corner_paleness(p + vec2(cos(a + 0.26), sin(a + 0.26)) * 0.9, linear);
	}
	return pale / 19.0;
}

void fragment() {
	vec2 p = world_pos.xz;
	bool linear = !OUTPUT_IS_SRGB;
	vec3 land;
	vec4 water_tint;
	float fogged, hidden;
	vec3 weights;
	blend_tiles(p, linear, 0.0, 0.0, land, water_tint, fogged, hidden, weights);

	// Broad noise swells and narrows the track; fine noise frays its edge.
	float swell = periodic_noise(p, 2.3) - 0.5;
	float fray = periodic_noise(p, 7.0) * 0.6 + periodic_noise(p, 19.0) * 0.4 - 0.5;
	float grain = periodic_noise(p, 41.0) - 0.5;
	float across = abs(lane.x);
	float edge = across + fray * 0.2 + grain * 0.08;
	float half_width = 0.46 + swell * 0.18;
	float track = 1.0 - smoothstep(half_width - 0.2, half_width + 0.06, edge);
	float verge = 1.0 - smoothstep(half_width - 0.05, 1.0, edge);

	// The worn middle and two wheel ruts, faded out when they would be thinner than a pixel.
	// Both fade out at tile centres, where links meet in a plain trampled patch.
	float detail = 1.0 - smoothstep(0.25, 0.5, fwidth(lane.x));
	detail *= smoothstep(0.08, 0.3, distance(p, hex_centre(containing_hex(p))));
	float middle = 1.0 - smoothstep(0.0, half_width * 0.55, across + fray * 0.12);
	float rut_at = abs(across - half_width * 0.5 + fray * 0.05);
	float ruts = 1.0 - smoothstep(0.03, 0.09, rut_at);

	// The terrain colour is already toned in fog; tone the road's own colours to match.
	// Light earth over dark ground; over pale ground (plains, sand, snow) a darker, browner take on the ground itself.
	vec3 earth = road_colour(ROAD_DIRT, fogged, linear);
	float pale = ground_paleness(p, linear);
	vec3 dirt = mix(mix(earth, land, 0.15), mix(land * vec3(0.8, 0.73, 0.66), earth * 0.75, 0.3), pale);
	dirt *= 1.0 + grain * 0.1;
	vec3 colour = mix(dirt, dirt * vec3(0.8, 0.78, 0.75), max(middle * 0.5, ruts * 0.6) * detail);
	vec3 trampled = mix(land, road_colour(ROAD_VERGE, fogged, linear), 0.3);
	colour = mix(trampled, colour, track);

	float alpha = max(track * 0.92, verge * 0.25);
	alpha *= 1.0 - tile_lines(p);
	if (hidden > 0.0) alpha *= 1.0 - clouds(p, hidden, TIME, linear).a;
	ALBEDO = colour;
	ALPHA = alpha;
	ROUGHNESS = 0.92;
	SPECULAR = 0.15;
}
