shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

// The go-to destination: a bevelled ring in the route colour with a dark outline, four inward chevrons that tap
// toward the centre, a ripple that keeps spreading outward, and a jewel in the middle (a cross for an attack).
// Everything has a drop shadow. UV spans the draped square, scaled so the ring centre line has radius 1.

uniform vec4 marker_colour : source_color = vec4(0.4, 1.0, 0.5, 1.0);
uniform bool attack = false;
uniform float uv_scale = 1.45;

const float RING_WIDTH = 0.075;
const float OUTLINE = 0.04;
const float CHEVRON_TIP = 0.42;
const float CHEVRON_DEPTH = 0.2;
const float CHEVRON_HALF_SPAN = 0.22;
const float CHEVRON_THICKNESS = 0.055;
const vec2 SHADOW_OFFSET = vec2(0.035, 0.06);

float segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

float ring_sd(vec2 p) {
	return abs(length(p) - 1.0) - RING_WIDTH;
}

// Four chevrons on the diagonals, tips toward the centre. y is the across-stroke coordinate for the bevel.
vec2 chevron_sd(vec2 p) {
	float sector = TAU / 4.0;
	float angle = atan(p.y, p.x) - sector * 0.5;
	angle = mod(angle + sector * 0.5, sector) - sector * 0.5;
	vec2 local = length(p) * vec2(cos(angle), sin(angle));
	float tap = pow(0.5 + 0.5 * sin(TIME * 4.0), 3.0);
	float tip = CHEVRON_TIP - 0.08 * tap;
	vec2 a = vec2(tip, 0.0);
	float d = min(segment(local, a, vec2(tip + CHEVRON_DEPTH, CHEVRON_HALF_SPAN)),
		segment(local, a, vec2(tip + CHEVRON_DEPTH, -CHEVRON_HALF_SPAN))) - CHEVRON_THICKNESS;
	return vec2(d, local.x - tip - abs(local.y) * CHEVRON_DEPTH / CHEVRON_HALF_SPAN);
}

float centre_sd(vec2 p) {
	if (!attack) return length(p) - 0.13;
	float cross = min(segment(p, vec2(-0.16, -0.16), vec2(0.16, 0.16)), segment(p, vec2(-0.16, 0.16), vec2(0.16, -0.16)));
	return cross - 0.05;
}

float shape_sd(vec2 p) {
	return min(min(ring_sd(p), chevron_sd(p).x), centre_sd(p));
}

vec4 over(vec4 top, vec4 bottom) {
	float a = top.a + bottom.a * (1.0 - top.a);
	vec3 rgb = a > 0.0 ? (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / a : vec3(0.0);
	return vec4(rgb, a);
}

vec3 bevel(float across) {
	// across is -1 on the inner edge and +1 on the outer edge.
	vec3 light = mix(marker_colour.rgb, vec3(1.0), 0.6);
	vec3 deep = marker_colour.rgb * 0.5;
	vec3 colour = mix(light, marker_colour.rgb, smoothstep(-1.0, 0.0, across));
	return mix(colour, deep, smoothstep(0.0, 1.0, across));
}

void fragment() {
	vec2 p = UV * uv_scale;
	float aa = fwidth(p.x) + fwidth(p.y);
	float r = length(p);
	float pulse = 0.5 + 0.5 * sin(TIME * 4.0);

	float ring = ring_sd(p);
	vec2 chevron = chevron_sd(p);
	float centre = centre_sd(p);
	float shape = min(min(ring, chevron.x), centre);

	vec4 colour = vec4(0.0);
	float shadow = shape_sd(p - SHADOW_OFFSET);
	colour = over(vec4(0.0, 0.0, 0.0, 0.4 * (1.0 - smoothstep(-0.02, 0.08, shadow))), colour);
	// A wash inside the ring, a halo around it, and a ripple that spreads out from the ring and fades.
	float wash = smoothstep(0.1, 1.0, r) * step(r, 1.0) * 0.2;
	float halo = exp(-max(ring, 0.0) * 12.0) * 0.3;
	float wave = fract(TIME * 0.7);
	float ripple = (1.0 - smoothstep(0.0, 0.035 + aa, abs(r - 1.0 - wave * 0.38))) * (1.0 - wave) * 0.7;
	colour = over(vec4(marker_colour.rgb, max(max(wash, halo), ripple)), colour);
	colour = over(vec4(0.03, 0.04, 0.03, 0.85 * (1.0 - smoothstep(OUTLINE - aa, OUTLINE + aa, shape))), colour);

	vec3 body;
	if (ring <= min(chevron.x, centre)) body = bevel(clamp((r - 1.0) / RING_WIDTH, -1.0, 1.0));
	else if (chevron.x <= centre) body = bevel(clamp(chevron.y / (CHEVRON_THICKNESS * 1.4), -1.0, 1.0)) * (1.0 + 0.2 * pulse);
	else body = mix(mix(marker_colour.rgb, vec3(1.0), 0.35), marker_colour.rgb * 0.55, clamp(0.5 + 2.5 * (p.x + p.y), 0.0, 1.0));
	colour = over(vec4(body, 1.0 - smoothstep(-aa, aa, shape)), colour);
	// A thin specular line along the top of the ring.
	float glint = (1.0 - smoothstep(0.0, 0.014 + aa, abs(r - 1.0 + RING_WIDTH * 0.35)))
		* (1.0 - smoothstep(-0.9, 0.2, p.y / max(r, 1e-4)));
	colour.rgb = mix(colour.rgb, vec3(1.0), 0.5 * glint * step(ring, 0.0));

	ALBEDO = colour.rgb;
	ALPHA = colour.a * marker_colour.a;
}
