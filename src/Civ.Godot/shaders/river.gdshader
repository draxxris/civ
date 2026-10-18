// River water and wet banks: strips along each river curve (RiverMesher), draped over the carved terrain channel.
// UV.x runs across the strip (|x| = 1 at the waterline, 2 at the outer edge of the wet bank), UV2 is the downstream flow
// (x, z) times speed, COLOR.r fades the river where sea or lake water covers it and COLOR.g marks steep white water.
// Only sight, owner borders and hover are read from the tile data; no terrain materials are sampled.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// Lifts the strip toward the camera so it wins against the terrain triangles it is draped over.
uniform float depth_bias = 0.03;

// sRGB colours, decoded like the terrain palette (see decode_colour): slate water that lightens over the shallows, a pale
// silt line at the waterline and a faint damp band on the banks.
const vec3 RIVER_DEEP = vec3(0.23, 0.36, 0.45);
const vec3 RIVER_SHALLOW = vec3(0.45, 0.55, 0.56);
const vec3 RIVER_LIGHT = vec3(0.72, 0.80, 0.83);
const vec3 BANK_WET = vec3(0.30, 0.28, 0.21);
const vec3 BANK_SILT = vec3(0.68, 0.65, 0.54);

varying vec3 world_pos;
varying vec2 lane;
varying vec2 flow;
varying vec2 info;

void vertex() {
	world_pos = VERTEX;
	lane = UV;
	flow = UV2;
	info = COLOR.rg;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(-view.xyz) * depth_bias;
	POSITION = PROJECTION_MATRIX * view;
}

// Two half-cycle-offset samples advected along the flow and cross-faded, so ripples drift downstream without stretching.
float flowing_ripples(vec2 p, vec2 velocity, float time) {
	float phase = fract(time * 0.25);
	float other = fract(phase + 0.5);
	vec2 a = p - velocity * phase * 0.9;
	vec2 b = p - velocity * other * 0.9 + vec2(0.37, 0.71);
	float first = periodic_noise(a, 6.0) * 0.6 + periodic_noise(a, 13.0) * 0.4;
	float second = periodic_noise(b, 6.0) * 0.6 + periodic_noise(b, 13.0) * 0.4;
	return mix(first, second, abs(1.0 - 2.0 * phase));
}

void fragment() {
	vec2 p = world_pos.xz;
	bool linear = !OUTPUT_IS_SRGB;
	float fogged, hidden;
	sight_blend(p, fogged, hidden);

	float across = abs(lane.x);
	float wobble = periodic_noise(p, 5.0) - 0.5;
	float shoreline = across + wobble * 0.22;
	float water = 1.0 - smoothstep(0.82, 1.0, shoreline);
	float silt = smoothstep(0.78, 0.95, shoreline) * (1.0 - smoothstep(1.0, 1.25, shoreline));
	float damp = 1.0 - smoothstep(1.1, 2.0, across + wobble * 0.5);

	float ripples = flowing_ripples(p, flow, TIME);
	float centre = 1.0 - smoothstep(0.1, 0.95, across);
	vec3 river = mix(decode_colour(RIVER_SHALLOW, linear), decode_colour(RIVER_DEEP, linear), centre);
	river = mix(river, decode_colour(RIVER_LIGHT, linear), smoothstep(0.58, 0.85, ripples) * 0.3);
	river = mix(river, vec3(0.9, 0.93, 0.94), info.g * smoothstep(0.4, 0.75, ripples) * 0.7);
	vec3 bank = mix(decode_colour(BANK_WET, linear), decode_colour(BANK_SILT, linear), silt);
	vec3 colour = mix(bank, river, water * (1.0 - silt * 0.7));

	float alpha = max(water * 0.88, max(silt * 0.55, damp * 0.16));
	alpha *= info.r * (1.0 - tile_lines(p));
	colour = mix(colour, fog_tone(colour) * 1.1, fogged);
	if (hidden > 0.0) alpha *= 1.0 - clouds(p, hidden, TIME, linear).a;

	// Ripples tilt the water normal along the flow; banks keep the ground normal from the mesh.
	vec2 tilt = (ripples - 0.5) * 0.35 * (length(flow) > 0.0 ? normalize(flow) : vec2(0.0));
	vec3 rippled = normalize((VIEW_MATRIX * vec4(normalize(vec3(-tilt.x, 1.0, -tilt.y)), 0.0)).xyz);
	NORMAL = normalize(mix(NORMAL, rippled, water));
	ALBEDO = colour;
	ALPHA = alpha;
	ROUGHNESS = mix(0.9, 0.3, water);
	SPECULAR = mix(0.1, 0.4, water) * (1.0 - fogged * 0.6);
}
