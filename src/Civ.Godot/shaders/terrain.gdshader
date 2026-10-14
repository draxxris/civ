// Lit terrain chunks. Albedo/height textures blend across the height field's corner triangle, then the containing hex
// adds the work-tile overlay, owner borders and the hover outline. Rivers are separate draped surfaces (river.gdshader)
// over the channels the height field carves. Unexplored tiles become drifting clouds.
shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// 0 = normal; 1 = sharpened blend weights as RGB; 2 = hidden weight; 3 = blended colour without lines or clouds;
// 4 = white ground (lighting and relief only).
uniform int debug_mode = 0;
// Snow caps: altitude of the snow line, half width of its transition, and the slope (1 - normal.y) above which
// faces shed their snow. Only mountain summits rise above the line; lowland snow comes from tile terrain instead.
uniform float snow_line = 0.86;
uniform float snow_fade = 0.05;
uniform float snow_shed = 0.38;

varying vec3 world_pos;
varying vec3 ground_normal;

// Wrap-safe noise breaks up the snow line and exposes rock on steep faces.
float snow_cover(vec2 p, float height, float steep) {
	const float drift_height = 0.4;
	const float streak_height = 0.18;
	// Noise lies in [-0.5, 0.5], so lowlands cannot reach the snow line even at its lowest point.
	if (height <= snow_line - snow_fade - 0.5 * (drift_height + streak_height)) return 0.0;
	float drift = periodic_fbm(p, 1.1) - 0.5;
	float streak = periodic_fbm(p, 4.3) - 0.5;
	float snow = smoothstep(snow_line - snow_fade, snow_line + snow_fade, height + drift * drift_height + streak * streak_height);
	return snow * (1.0 - smoothstep(snow_shed - 0.05, snow_shed + 0.05, steep + drift * 0.2 + streak * 0.3));
}

void vertex() {
	// Chunk vertices are canonical world positions; wrap copies only translate the instance.
	world_pos = VERTEX;
	ground_normal = NORMAL;
}

void fragment() {
	vec2 p = vec2(wrap_world_x(world_pos.x), world_pos.z);
	vec3 land;
	vec4 water_tint;
	float fogged, hidden;
	vec3 w;
	float steep = 1.0 - normalize(ground_normal).y;
	float slope = smoothstep(0.08, 0.40, steep);
	slope *= smoothstep(-0.01, 0.04, world_pos.y);
	float snow = snow_cover(world_pos.xz, world_pos.y, steep);
	// Keep sample coordinates unwrapped to prevent a mip-derivative jump at the cylinder seam.
	bool linear = !OUTPUT_IS_SRGB;
	blend_tiles(world_pos.xz, linear, slope, snow, land, water_tint, fogged, hidden, w);
	vec3 colour = land;
	vec4 sky = clouds(world_pos.xz, hidden, TIME, linear);

	ivec2 hex = containing_hex(p);
	bool inside = on_map(hex);
	int sight = 0, mark = 0, owner = 0;
	bool hover = false;
	if (inside) {
		vec4 a = texelFetch(tile_a, texel_of(hex), 0);
		vec4 b = texelFetch(tile_b, texel_of(hex), 0);
		sight = byte_of(b.r);
		mark = byte_of(b.g);
		hover = b.b > 0.5;
		owner = byte_of(a.b);
	}
	vec2 d = p - hex_centre(hex);
	float fog = sight == 1 ? 0.72 : 1.0;

	if (debug_mode == 0 && sight > 0) {
		if (mark == 1) colour = mix(colour, vec3(1.0), 0.16);
		else if (mark == 2) colour = mix(colour, vec3(1.0, 0.32, 0.01), 0.42);
		else if (mark == 3) colour = mix(colour, vec3(0.0), 0.5);
		float border = 0.0, glow = 0.0;
		for (int k = 0; k < 6; k++) {
			float edge = SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]);
			if (owner > 0 && owner_of(hex + DIRECTION[k]) != owner) {
				border = max(border, band(edge, border_width));
				glow = max(glow, 1.0 - smoothstep(0.0, 0.32, edge));
			}
		}
		if (owner > 0) {
			vec3 own = palette_entry(owner, 2, linear).rgb * fog;
			colour = mix(colour, own, glow * 0.3);
			colour = mix(colour, own, border);
		}
	}

	float cover = debug_mode == 0 ? sky.a : 0.0;
	if (debug_mode == 1) colour = w;
	else if (debug_mode == 2) colour = vec3(hidden);
	else if (debug_mode == 4) colour = vec3(0.8);

	vec3 emission = sky.rgb * cover;
	if (hover) {
		float outline = hover_outline(d);
		emission = mix(emission, vec3(0.9), outline);
		cover = max(cover, outline);
	}
	ALBEDO = colour * (1.0 - cover);
	EMISSION = emission;
	ROUGHNESS = 0.92;
	SPECULAR = 0.15 * (1.0 - cover);
}
