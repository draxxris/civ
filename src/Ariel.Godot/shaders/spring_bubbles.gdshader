// Bubbles welling up on a spring's water (SpringMesher): each bubble is a quad lying flat on the water, its four vertices
// all at its centre; UV is the corner (-1..1), UV2 = (size, slowness) and COLOR.r = phase. On its own cycle a bubble
// swells up out of the water, pops, and leaves a ring spreading out over the surface.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

#include "res://shaders/terrain_common.gdshaderinc"

// Seconds per bubble, before each bubble's own slowness.
uniform float cycle = 2.4;
uniform float strength = 0.75;
// Share of the cycle a bubble swells for before it pops.
uniform float swell = 0.55;

varying vec3 centre;
varying vec2 corner;
varying float life;

void vertex() {
	float size = UV2.x;
	life = fract(TIME / (cycle * max(UV2.y, 0.1)) + COLOR.r);
	corner = UV;
	centre = VERTEX;
	VERTEX.xz += UV * size;
}

void fragment() {
	bool linear = !OUTPUT_IS_SRGB;
	float fogged, hidden;
	sight_blend(centre.xz, fogged, hidden);
	float d = length(corner);
	float alpha;
	if (life < swell) {
		// The bubble: a dome whose rim catches the light, with a bright glint off-centre.
		float grow = smoothstep(0.0, 1.0, life / swell);
		float r = mix(0.12, 0.55, grow);
		float rim = smoothstep(r - 0.12, r, d) * (1.0 - smoothstep(r, r + 0.05, d));
		float glint = 1.0 - smoothstep(0.0, 0.12 * r + 0.03, length(corner - vec2(-0.35, -0.35) * r));
		float body = (1.0 - smoothstep(r - 0.02, r, d)) * 0.18;
		alpha = (rim * 0.85 + glint + body) * smoothstep(0.0, 0.15, grow);
	} else {
		// Popped: a thin ring spreading out over the water and fading.
		float spread = (life - swell) / (1.0 - swell);
		float r = mix(0.55, 1.0, spread);
		float ring = smoothstep(r - 0.08, r, d) * (1.0 - smoothstep(r, r + 0.04, d));
		alpha = ring * 0.6 * (1.0 - spread);
	}
	vec3 colour = decode_colour(vec3(0.92, 0.96, 0.98), linear);
	ALBEDO = mix(colour, fog_tone(colour), fogged);
	ALPHA = clamp(alpha * strength * (1.0 - hidden), 0.0, 1.0);
}
