// Raymarched fog and storm clouds over features (HazeVolume). Density, colour, kind, the ground floor and the layer's
// bottom and top come from the baked haze maps; tileable 3D noise shapes them and drifts with the wind. Light is marched
// a few steps toward the sun, so clouds shade themselves: bright tops, dark bases. Ground fogs are densest at the
// ground and thin out upward; storm clouds have flat bases and billowing tops.
shader_type spatial;
render_mode blend_mix, unshaded, cull_back, depth_draw_never, shadows_disabled;

#include "res://shaders/volume_common.gdshaderinc"

// Haze maps (HazeVolume): shape = (density, floor, bottom, top), look = (colour, kind / 4). x repeats over the period.
uniform sampler2D haze_shape : filter_linear, repeat_enable;
uniform sampler2D haze_look : filter_linear, repeat_enable;
uniform float period = 1.0;
uniform float z_min = 0.0;
uniform float z_span = 1.0;
// March step length in world units, and the most steps one ray may take.
uniform float step_length = 0.03;
uniform int max_steps = 40;
// Extinction per world unit at full density.
uniform float fog_extinction = 11.0;
uniform float storm_extinction = 16.0;

const int KIND_MIST = 1;
const int KIND_SMOG = 2;
const int KIND_STORM = 3;

void vertex() {
	local_pos = VERTEX;
	box_lo = box_corner(UV, COLOR.r);
	box_hi = box_corner(UV2, COLOR.g);
}

vec2 map_uv(vec2 xz) {
	return vec2(xz.x / period, clamp((xz.y - z_min) / z_span, 0.0, 1.0));
}

// Extinction at p. floor_height is the ground (or sea) under p; colour and kind come from the look map.
float density_at(vec3 p, float time, out float floor_height, out vec3 colour, out int kind) {
	vec2 uv = map_uv(p.xz);
	vec4 shape = texture(haze_shape, uv);
	floor_height = HEIGHT_MIN + shape.g * HEIGHT_SPAN;
	colour = vec3(0.0);
	kind = 0;
	if (shape.r < 0.004) return 0.0;
	float bottom = HEIGHT_MIN + shape.b * HEIGHT_SPAN, top = HEIGHT_MIN + shape.a * HEIGHT_SPAN;
	float h = (p.y - bottom) / max(top - bottom, 0.01);
	if (h <= 0.0 || h >= 1.0) return 0.0;
	vec4 look = texture(haze_look, uv);
	colour = look.rgb;
	kind = int(round(look.a * 4.0));
	float coverage = shape.r;
	if (kind == KIND_STORM) {
		// Coarse cells decide where towers stand and leave gaps of open sky between them; the billows shape the towers.
		// The cells come first, so a ray through a gap skips the finer lookup.
		float cells = cloud_noise_at(p * vec3(1.0, 0.0, 1.0), 0.28, wind * time).x;
		float clumps = smoothstep(0.62 - coverage * 0.3, 0.78 - coverage * 0.3, cells);
		// Each tower tops out at its own height: the denser its cell, the taller it climbs.
		float profile = smoothstep(0.0, 0.1, h) * (1.0 - smoothstep(0.15, 0.35 + 0.65 * smoothstep(0.45, 0.9, cells), h));
		if (clumps * profile < 0.3) return 0.0;
		vec2 n = cloud_noise_at(p * vec3(1.0, 0.7, 1.0), 0.8, wind * time * 2.5);
		float base = n.x * profile * clumps;
		float d = smoothstep(0.3, 0.55, base);
		d = max(0.0, d - (1.0 - n.y) * 0.2 * (1.0 - d));
		return d * storm_extinction;
	}
	// Ground fog: thickest at the ground, rolling billows that leave clear gaps (mist) or a thin veil between (smog).
	vec2 n = cloud_noise_at(p * vec3(1.0, 2.5, 1.0), 1.1, wind * time);
	float profile = pow(1.0 - h, 1.7) * smoothstep(0.0, 0.06, p.y - floor_height);
	float billow = n.x * 0.75 + n.y * 0.25;
	float shape_mask = kind == KIND_MIST ? smoothstep(0.45, 0.75, billow) : mix(0.3, 1.0, smoothstep(0.25, 0.7, billow));
	return coverage * profile * shape_mask * fog_extinction;
}

// Transmittance toward the sun from p, one step through the fog.
float sun_light(vec3 p, float time) {
	float f;
	vec3 c;
	int k;
	return exp(-density_at(p + sun_direction * 0.05, time, f, c, k) * 0.085);
}

// Storm cloud light from one step toward the sun (a full march costs too much over a whole storm), plus height:
// sunward flanks and tops are lit, bases and the inside of thick billows are in shadow.
float storm_light(vec3 p, float h, float time) {
	float f;
	vec3 c;
	int k;
	float sun = exp(-density_at(p + sun_direction * 0.12, time, f, c, k) * 0.16);
	return 0.15 + 0.85 * sun * (0.55 + 0.45 * smoothstep(0.05, 0.85, h));
}

void fragment() {
	vec3 ro = CAMERA_POSITION_WORLD - MODEL_MATRIX[3].xyz;
	vec3 rd = normalize(local_pos - ro);
	float span = min(box_exit(local_pos, rd, box_lo, box_hi), 8.0);
	if (span <= 1e-4) discard;
	// Storm boxes stand high above the sea; their towers take longer steps than low fog.
	float stride = box_lo.y > 0.15 ? step_length * 2.5 : step_length;
	int steps = clamp(int(ceil(span / stride)), 4, max_steps);
	float dt = span / float(steps);
	float t = dt * march_jitter(FRAGCOORD.xy);
	float transmittance = 1.0;
	vec3 light = vec3(0.0);
	for (int i = 0; i < max_steps; i++) {
		if (i >= steps) break;
		if (t >= span || transmittance < 0.02) break;
		vec3 p = local_pos + rd * t;
		float floor_height;
		vec3 colour;
		int kind;
		float d = density_at(p, TIME, floor_height, colour, kind);
		if (p.y < floor_height) break;
		if (d > 0.001) {
			// Storm clouds go slate grey in their own shadow; fog stays pale and lets light through.
			vec3 lit = kind == KIND_STORM
				? colour * (0.28 + 0.85 * storm_light(p, (p.y - box_lo.y) / max(box_hi.y - box_lo.y, 0.01), TIME))
				: colour * (0.72 + 0.45 * sun_light(p, TIME));
			float a = 1.0 - exp(-d * dt);
			light += transmittance * a * lit;
			transmittance *= 1.0 - a;
		}
		t += dt;
	}
	float alpha = 1.0 - transmittance;
	if (alpha < 0.003) discard;
	ALBEDO = light / alpha;
	ALPHA = alpha;
}
