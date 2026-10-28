shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

// The marker under the selected unit: a bevelled gold ring in a dark outline, a soft glow, and chevrons that
// point at the unit, turn slowly and breathe in and out. Everything has a drop shadow for depth.
// UV spans the draped square, scaled so the ring centre line has radius 1.

uniform vec4 marker_colour : source_color = vec4(1.0, 0.82, 0.3, 1.0);
uniform float uv_scale = 1.45;
uniform int chevrons = 4;

const float RING_WIDTH = 0.055;
const float OUTLINE = 0.035;
const float CHEVRON_TIP = 1.17;
const float CHEVRON_DEPTH = 0.19;
const float CHEVRON_HALF_SPAN = 0.25;
const float CHEVRON_THICKNESS = 0.05;
const float DASH_RADIUS = 0.84;
const float DASH_WIDTH = 0.022;
const float DASHES = 16.0;
const vec2 SHADOW_OFFSET = vec2(0.035, 0.06);

float segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

float ring_sd(vec2 p) {
	return abs(length(p) - 1.0) - RING_WIDTH;
}

// Distance to the nearest chevron. Returns the across-stroke coordinate in y for bevel shading.
vec2 chevron_sd(vec2 p) {
	float sector = TAU / float(chevrons);
	float angle = atan(p.y, p.x) + TIME * 0.35;
	angle = mod(angle + sector * 0.5, sector) - sector * 0.5;
	vec2 local = length(p) * vec2(cos(angle), sin(angle));
	float tip = CHEVRON_TIP + 0.025 * sin(TIME * 3.0);
	vec2 a = vec2(tip, 0.0);
	vec2 left = vec2(tip + CHEVRON_DEPTH, CHEVRON_HALF_SPAN);
	vec2 right = vec2(tip + CHEVRON_DEPTH, -CHEVRON_HALF_SPAN);
	float d = min(segment(local, a, left), segment(local, a, right)) - CHEVRON_THICKNESS;
	// Positive on the outer side of the stroke, so the bevel matches the ring: light inside, dark outside.
	return vec2(d, local.x - tip - abs(local.y) * CHEVRON_DEPTH / CHEVRON_HALF_SPAN);
}

// A thin dashed ring inside the main one, turning the other way.
float dash_sd(vec2 p) {
	float angle = atan(p.y, p.x) - TIME * 0.2;
	float cell = fract(angle / TAU * DASHES) - 0.5;
	float along = abs(cell) * TAU / DASHES * DASH_RADIUS - 0.09;
	return max(abs(length(p) - DASH_RADIUS) - DASH_WIDTH, along);
}

float shape_sd(vec2 p) {
	return min(ring_sd(p), chevron_sd(p).x);
}

vec4 over(vec4 top, vec4 bottom) {
	float a = top.a + bottom.a * (1.0 - top.a);
	vec3 rgb = a > 0.0 ? (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / a : vec3(0.0);
	return vec4(rgb, a);
}

vec3 bevel(float across) {
	// across is -1 on the inner edge and +1 on the outer edge.
	vec3 light = mix(marker_colour.rgb, vec3(1.0, 0.98, 0.86), 0.65);
	vec3 deep = marker_colour.rgb * vec3(0.62, 0.48, 0.3);
	vec3 colour = mix(light, marker_colour.rgb, smoothstep(-1.0, 0.0, across));
	return mix(colour, deep, smoothstep(0.0, 1.0, across));
}

void fragment() {
	vec2 p = UV * uv_scale;
	float aa = fwidth(p.x) + fwidth(p.y);
	float r = length(p);
	float pulse = 0.5 + 0.5 * sin(TIME * 3.0);

	float ring = ring_sd(p);
	vec2 chevron = chevron_sd(p);
	float shape = min(ring, chevron.x);

	vec4 colour = vec4(0.0);
	// Soft dark shadow, offset toward the bottom of the screen.
	float shadow = shape_sd(p - SHADOW_OFFSET);
	colour = over(vec4(0.0, 0.0, 0.0, 0.4 * (1.0 - smoothstep(-0.02, 0.08, shadow))), colour);
	// Warm glow: a faint wash inside the ring and a halo around it that breathes with the chevrons.
	float wash = smoothstep(0.15, 1.0, r) * step(r, 1.0) * 0.16;
	float halo = exp(-max(ring, 0.0) * 14.0) * (0.22 + 0.12 * pulse);
	colour = over(vec4(marker_colour.rgb, max(wash, halo)), colour);
	float dash = dash_sd(p);
	colour = over(vec4(0.06, 0.04, 0.02, 0.5 * (1.0 - smoothstep(0.02 - aa, 0.02 + aa, dash))), colour);
	colour = over(vec4(marker_colour.rgb, 0.75 * (1.0 - smoothstep(-aa, aa, dash))), colour);
	// Dark outline, then the bevelled gold body.
	colour = over(vec4(0.06, 0.04, 0.02, 0.85 * (1.0 - smoothstep(OUTLINE - aa, OUTLINE + aa, shape))), colour);
	float ring_across = clamp((r - 1.0) / RING_WIDTH, -1.0, 1.0);
	float chevron_across = clamp(chevron.y / (CHEVRON_THICKNESS * 1.4), -1.0, 1.0);
	vec3 body = ring < chevron.x ? bevel(ring_across) : bevel(chevron_across) * (1.0 + 0.15 * pulse);
	colour = over(vec4(body, 1.0 - smoothstep(-aa, aa, shape)), colour);
	// A thin specular line along the top of the ring.
	float glint = (1.0 - smoothstep(0.0, 0.012 + aa, abs(r - 1.0 + RING_WIDTH * 0.35))) * (1.0 - smoothstep(-0.9, 0.2, p.y / max(r, 1e-4)));
	colour.rgb = mix(colour.rgb, vec3(1.0), 0.55 * glint * step(ring, 0.0));

	ALBEDO = colour.rgb;
	ALPHA = colour.a * marker_colour.a;
}
