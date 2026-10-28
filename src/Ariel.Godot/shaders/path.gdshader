shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

// The go-to route. Ribbon vertices (white) carry UV = (across -1..1, distance along the route): a worn, glowing
// halo around a dark-edged bevelled core, with bright chevrons that flow toward the destination. Waypoint pips
// (black vertices) carry a radial UV: a jewel-like dot in a dark rim. Both keep the noisy, uneven glow.

uniform vec4 path_colour : source_color = vec4(0.4, 1.0, 0.5, 0.95);
uniform float flow_speed = 0.9;

const float CORE = 0.3;
const float OUTLINE = 0.44;
const float CHEVRON_SPACING = 0.34;
const float CHEVRON_SLANT = 0.11;

varying vec2 map_position;

float path_hash(vec2 p) {
	vec3 h = fract(vec3(p.xyx) * 0.1031);
	h += dot(h, h.yzx + 33.33);
	return fract((h.x + h.y) * h.z);
}

float path_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(path_hash(cell), path_hash(cell + vec2(1.0, 0.0)), f.x),
		mix(path_hash(cell + vec2(0.0, 1.0)), path_hash(cell + vec2(1.0)), f.x), f.y);
}

vec4 over(vec4 top, vec4 bottom) {
	float a = top.a + bottom.a * (1.0 - top.a);
	vec3 rgb = a > 0.0 ? (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / a : vec3(0.0);
	return vec4(rgb, a);
}

float fill(float d, float edge, float aa) {
	return 1.0 - smoothstep(edge - aa, edge + aa, d);
}

void vertex() {
	// Mesh coordinates keep the texture fixed as the camera moves and identical on wrapped copies.
	map_position = VERTEX.xz;
}

vec4 ribbon(vec2 uv, float wear, float grain, float waviness) {
	float across = abs(uv.x + (waviness - 0.5) * 0.08);
	float aa = fwidth(uv.x) * 1.5;
	float texture_opacity = mix(0.6, 1.0, grain) * mix(0.75, 1.0, wear);
	float radius = across * mix(0.85, 1.25, wear);
	vec3 light = mix(path_colour.rgb, vec3(1.0), 0.55);
	vec3 deep = path_colour.rgb * 0.55;

	vec4 colour = vec4(path_colour.rgb, path_colour.a * exp(-2.5 * radius * radius) * 0.7 * texture_opacity
		* (1.0 - smoothstep(0.6, 1.0, across)));
	colour = over(vec4(0.02, 0.05, 0.03, 0.6 * fill(across, OUTLINE, aa)), colour);
	// Bevel: bright down the middle, deeper toward the edges.
	vec3 body = mix(mix(path_colour.rgb, light, 0.25), deep, smoothstep(0.05, CORE, across));
	colour = over(vec4(body, fill(across, CORE, aa) * mix(0.85, 1.0, wear)), colour);
	// Chevrons pointing along the route, sliding toward the destination.
	float phase = (uv.y + across * CHEVRON_SLANT * 3.0) / CHEVRON_SPACING - TIME * flow_speed;
	float m = fract(phase);
	float chevron = smoothstep(0.0, 0.06, m) * (1.0 - smoothstep(0.24, 0.32, m)) * fill(across, CORE * 0.8, aa);
	colour.rgb = mix(colour.rgb, vec3(1.0, 1.0, 0.95), 0.9 * chevron);
	// Fade in where the ribbon leaves the selection ring.
	colour.a *= smoothstep(0.3, 0.5, uv.y);
	return colour;
}

vec4 pip(vec2 uv, float wear) {
	float r = length(uv);
	float aa = fwidth(r) * 1.5;
	vec3 light = mix(path_colour.rgb, vec3(1.0), 0.6);
	vec4 colour = vec4(path_colour.rgb, path_colour.a * 0.45 * exp(-5.0 * r * r) * mix(0.7, 1.0, wear));
	colour = over(vec4(0.02, 0.05, 0.03, 0.85 * fill(r, 0.62, aa)), colour);
	// Lit from the top left, like the gold markers.
	float lit = clamp(0.5 - 0.6 * (uv.x + uv.y), 0.0, 1.0);
	vec3 body = mix(path_colour.rgb * 0.6, light, lit);
	colour = over(vec4(body, fill(r, 0.46, aa)), colour);
	float glint = fill(length(uv - vec2(-0.14, -0.14)), 0.1, aa);
	colour.rgb = mix(colour.rgb, vec3(1.0), 0.7 * glint);
	return colour;
}

void fragment() {
	float waviness = path_noise(map_position * 5.0);
	float wear = path_noise(map_position * 28.0);
	float grain = path_noise(map_position * 75.0);
	vec4 colour = COLOR.r > 0.5 ? ribbon(UV, wear, grain, waviness) : pip(UV, wear);
	ALBEDO = colour.rgb;
	ALPHA = colour.a;
}
