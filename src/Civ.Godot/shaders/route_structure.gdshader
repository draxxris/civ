// The 3D parts of routes (MapRenderer.RouteParts.cs): maglev and jumplane decks and their supports, and the portal masts and
// contact wire over electric railroads. Opaque and lit. COLOR = sRGB albedo. UV = (along, across): along is the path's arc
// length in pattern units (RoutePlan.PatternUnit), across the position on the deck profile in world units.
// UV2.x = surface: 0 plain, 1 concrete with expansion joints, 2 glowing strip, 3 jumplane deck with light bars, 4 metal.
// UV2.y = margin at a junction (large elsewhere): how much nearer the point is to this deck's centre line than to any other
// deck's, less RouteJunction.Yield. A deck is cut where it is negative, so overlapping decks join as one.
// Fog tones the parts per pixel, as it does the terrain under them.
shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

const vec3 GLOW = vec3(0.35, 0.78, 1.0);

varying vec3 world_pos;
varying vec2 pattern;
varying float surface;
varying float rival;

void vertex() {
	world_pos = VERTEX;
	pattern = UV;
	surface = UV2.x;
	rival = UV2.y;
}

float stripe(float d, float half_width, float px) {
	return 1.0 - smoothstep(half_width - px, half_width + px, d);
}

void fragment() {
	if (rival < 0.0) discard;
	bool linear = !OUTPUT_IS_SRGB;
	vec3 albedo = decode_colour(COLOR.rgb, linear);
	int kind = int(round(surface));
	float fogged, hidden;
	sight_blend(world_pos.xz, fogged, hidden);
	float along_px = fwidth(pattern.x);
	float grain = periodic_noise(world_pos.xz, 47.0) - 0.5;
	float rough = 0.8;
	float metal = 0.0;
	float specular = 0.35;
	vec3 glow = vec3(0.0);
	if (kind == 1) {
		// Cast concrete: faint mottling and a dark joint every pattern unit.
		float joint = stripe(abs(fract(pattern.x / 0.4 + 0.5) - 0.5), 0.02, along_px * 2.5);
		albedo *= (0.93 + grain * 0.12) * (1.0 - joint * 0.35 * (1.0 - smoothstep(0.05, 0.2, along_px)));
		rough = 0.9;
		specular = 0.15;
	} else if (kind == 2) {
		float pulse = 0.85 + 0.15 * sin(TIME * 2.0 + pattern.x * 6.0);
		glow = decode_colour(GLOW, linear) * 1.1 * pulse;
		albedo = decode_colour(GLOW, linear) * 0.3;
		rough = 0.3;
	} else if (kind == 3) {
		// Double chevron bars down the middle of the deck, lit from inside.
		float v = abs(pattern.y);
		float bar = stripe(abs(fract((pattern.x + v * 0.9) / 0.1 + 0.5) - 0.5), 0.13, along_px * 10.0);
		bar *= stripe(v, 0.028, 0.004) * (1.0 - smoothstep(0.03, 0.08, along_px));
		// Plain deck where decks join, rather than chevrons of several decks cut against each other.
		bar *= smoothstep(0.01, 0.05, rival);
		albedo = mix(albedo * (0.95 + grain * 0.1), decode_colour(GLOW, linear), bar);
		glow = decode_colour(GLOW, linear) * bar;
		rough = 0.75;
		metal = 0.1;
		specular = 0.2;
	} else if (kind == 4) {
		albedo *= 0.95 + grain * 0.1;
		rough = 0.6;
		metal = 0.3;
		specular = 0.25;
	}
	ALBEDO = mix(albedo, fog_tone(albedo), fogged);
	EMISSION = glow * (1.0 - fogged * 0.8);
	ROUGHNESS = rough;
	METALLIC = metal;
	SPECULAR = specular;
}
