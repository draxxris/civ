// Spray and mist off a waterfall's plunge pool (WaterfallMesher): each puff is a quad whose four vertices all sit at its
// centre; UV is the corner (-1..1), UV2 = (size, rise) and COLOR = (phase, density). The quad is turned to face the camera and,
// on its own cycle, swells, drifts up by its rise and fades, so the spray keeps billowing without visible repeats. Dense
// puffs are the splash where the sheet lands: small, bright and quick. Thin ones are the mist drifting off the pool.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

#include "res://shaders/terrain_common.gdshaderinc"

// Seconds per billow of the mist; a splash bursts twice as fast.
uniform float cycle = 3.2;
uniform float strength = 0.55;

varying vec3 centre;
varying vec2 corner;
varying float life;
varying float phase;
varying float density;

void vertex() {
	float size = UV2.x;
	float rise = UV2.y;
	phase = COLOR.r;
	density = COLOR.g;
	float t = fract(TIME / (cycle * mix(1.0, 0.45, density)) + phase);
	life = t;
	corner = UV;
	// A splash leaps up and slows; mist rises steadily.
	float lift = mix(t, 1.0 - (1.0 - t) * (1.0 - t), density);
	vec3 at = VERTEX + vec3(0.0, rise * lift, 0.0);
	centre = at;
	vec4 view = MODELVIEW_MATRIX * vec4(at, 1.0);
	view.xy += UV * size * (0.6 + 0.8 * t);
	POSITION = PROJECTION_MATRIX * view;
}

float value_noise(vec2 p) {
	vec2 f = floor(p);
	vec2 t = p - f;
	t = t * t * (3.0 - 2.0 * t);
	ivec2 c = ivec2(f);
	float a = lattice_hash(c, 11u), b = lattice_hash(c + ivec2(1, 0), 11u);
	float d = lattice_hash(c + ivec2(0, 1), 11u), e = lattice_hash(c + ivec2(1, 1), 11u);
	return mix(mix(a, b, t.x), mix(d, e, t.x), t.y);
}

void fragment() {
	bool linear = !OUTPUT_IS_SRGB;
	float fogged, hidden;
	sight_blend(centre.xz, fogged, hidden);
	float d = length(corner);
	// A soft round puff with a billowing edge; a splash is grainier, a scatter of droplets.
	float wisps = value_noise(corner * 2.6 + vec2(phase * 17.0, -life * 1.5)) * 0.6
		+ value_noise(corner * mix(5.3, 9.0, density) + vec2(phase * 5.0 + 3.0, -life * 2.4)) * 0.4;
	float puff = (1.0 - smoothstep(0.15, 1.0, d + (wisps - 0.5) * 0.6)) * smoothstep(0.2, 0.75, wisps + 0.25);
	// Each puff fades in, then thins out as it lifts.
	float alive = smoothstep(0.0, 0.15, life) * (1.0 - smoothstep(0.4, 1.0, life));
	vec3 colour = decode_colour(vec3(0.94, 0.96, 0.98), linear);
	ALBEDO = mix(colour, fog_tone(colour), fogged);
	ALPHA = clamp(puff * alive * strength * mix(0.55, 1.6, density) * (1.0 - hidden), 0.0, 1.0);
}
