// A volcano's smoke plume, raymarched in a box over the crater: a column that rises from the crater floor, leans
// downwind and spreads into a cloud, billowing upward through tileable 3D noise. Its base is lit by the lava below.
// The crater floor is the box's centre in x and z and its bottom in y. COLOR.b is the fog brightness, COLOR.a the density.
shader_type spatial;
render_mode blend_mix, unshaded, cull_back, depth_draw_never, shadows_disabled;

#include "res://shaders/volume_common.gdshaderinc"

uniform vec3 ash_colour : source_color = vec3(0.3, 0.28, 0.27);
uniform vec3 smoke_colour : source_color = vec3(0.7, 0.68, 0.66);
uniform vec3 glow_colour : source_color = vec3(1.0, 0.4, 0.1);
uniform float plume_height = 1.9;
uniform float extinction = 26.0;
uniform float step_length = 0.04;
uniform int max_steps = 40;

varying float fog;
varying float strength;

void vertex() {
	local_pos = VERTEX;
	box_lo = box_corner(UV, COLOR.r);
	box_hi = box_corner(UV2, COLOR.g);
	fog = COLOR.b;
	strength = COLOR.a;
}

// Extinction at p and the share of the column's height there (0 at the crater, 1 at the top).
float plume_density(vec3 p, vec3 crater, float time, out float h) {
	vec3 q = p - crater;
	h = q.y / plume_height;
	if (h <= 0.0 || h >= 1.0) return 0.0;
	vec2 drift = normalize(wind.xz) * 0.6 * pow(h, 1.4);
	float r = length(q.xz - drift);
	float radius = 0.07 + 0.42 * pow(h, 0.75);
	float core = exp(-2.4 * (r / radius) * (r / radius));
	vec2 n = cloud_noise_at(p * vec3(1.0, 0.8, 1.0), 2.2, vec3(0.0, -time * 0.12, 0.0) + wind * time);
	float billow = clamp(n.x * 1.4 - 0.3 + (1.0 - h) * 0.25 - (1.0 - n.y) * 0.15, 0.0, 1.0);
	float ends = smoothstep(0.0, 0.03, h) * (1.0 - smoothstep(0.6, 1.0, h));
	return core * billow * ends * extinction * strength;
}

void fragment() {
	vec3 crater = vec3(0.5 * (box_lo.x + box_hi.x), box_lo.y + 0.02, 0.5 * (box_lo.z + box_hi.z));
	vec3 ro = CAMERA_POSITION_WORLD - MODEL_MATRIX[3].xyz;
	vec3 rd = normalize(local_pos - ro);
	float span = min(box_exit(local_pos, rd, box_lo, box_hi), 6.0);
	int steps = clamp(int(ceil(span / step_length)), 4, max_steps);
	float dt = span / float(steps);
	float t = dt * march_jitter(FRAGCOORD.xy);
	float transmittance = 1.0;
	vec3 light = vec3(0.0);
	vec3 glow = vec3(0.0);
	for (int i = 0; i < max_steps; i++) {
		if (i >= steps) break;
		if (t >= span || transmittance < 0.02) break;
		vec3 p = local_pos + rd * t;
		float h;
		float d = plume_density(p, crater, TIME, h);
		if (d > 0.001) {
			float hs;
			float shadow = plume_density(p + sun_direction * 0.06, crater, TIME, hs) * 0.06
				+ plume_density(p + sun_direction * 0.16, crater, TIME, hs) * 0.12;
			float sun = exp(-shadow);
			vec3 lit = mix(ash_colour, smoke_colour, smoothstep(0.05, 0.7, h)) * (0.45 + 0.75 * sun);
			float a = 1.0 - exp(-d * dt);
			light += transmittance * a * lit;
			glow += transmittance * a * glow_colour * exp(-h * 14.0) * 0.8;
			transmittance *= 1.0 - a;
		}
		t += dt;
	}
	float alpha = 1.0 - transmittance;
	if (alpha < 0.003) discard;
	ALBEDO = light / alpha * fog;
	EMISSION = glow / alpha * fog;
	ALPHA = alpha;
}
