// Lit terrain chunks. Albedo/height textures blend across the height field's corner triangle, then the containing hex
// adds the work-tile overlay, owner borders, rivers and the hover outline. Unexplored tiles become drifting clouds.
shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// 0 = normal; 1 = sharpened blend weights as RGB; 2 = hidden weight; 3 = blended colour without lines or clouds;
// 4 = white ground (lighting and relief only).
uniform int debug_mode = 0;
uniform float border_width = 0.075;
uniform float river_width = 0.065;

varying vec3 world_pos;
varying vec3 ground_normal;

// Outward unit normals of the six hex edges, in Hex.Directions order (E, NE, NW, W, SW, SE).
const vec2 EDGE_NORMAL[6] = vec2[6](
	vec2(1.0, 0.0), vec2(0.5, -0.8660254), vec2(-0.5, -0.8660254),
	vec2(-1.0, 0.0), vec2(-0.5, 0.8660254), vec2(0.5, 0.8660254));
const ivec2 DIRECTION[6] = ivec2[6](
	ivec2(1, 0), ivec2(1, -1), ivec2(0, -1), ivec2(-1, 0), ivec2(-1, 1), ivec2(0, 1));

// Hex.FromPixel: cube rounding of the unwarped world position (banker's rounding like Math.Round).
ivec2 containing_hex(vec2 p) {
	float q = SQRT3 / 3.0 * p.x - p.y / 3.0;
	float r = 2.0 * p.y / 3.0;
	float s = -q - r;
	float rq = roundEven(q), rr = roundEven(r), rs = roundEven(s);
	float dq = abs(rq - q), dr = abs(rr - r), ds = abs(rs - s);
	if (dq > dr && dq > ds) rq = -rr - rs;
	else if (dr > ds) rr = -rq - rs;
	return ivec2(int(rq), int(rr));
}

vec2 hex_centre(ivec2 hex) {
	return vec2(SQRT3 * (float(hex.x) + float(hex.y) * 0.5), 1.5 * float(hex.y));
}

bool on_map(ivec2 hex) {
	return hex.y >= 0 && hex.y < map_size.y && (wrap_x || (offset_col(hex) >= 0 && offset_col(hex) < map_size.x));
}

ivec2 texel_of(ivec2 hex) {
	return ivec2(wrap_col(offset_col(hex)), hex.y);
}

int owner_of(ivec2 hex) {
	return on_map(hex) ? byte_of(texelFetch(tile_a, texel_of(hex), 0).b) : 0;
}

float band(float distance, float width) {
	return 1.0 - smoothstep(width * 0.7, width, distance);
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
	float slope = smoothstep(0.08, 0.40, 1.0 - normalize(ground_normal).y);
	slope *= smoothstep(-0.01, 0.04, world_pos.y);
	// Keep sample coordinates unwrapped to prevent a mip-derivative jump at the cylinder seam.
	blend_tiles(world_pos.xz, slope, land, water_tint, fogged, hidden, w);
	vec3 colour = land;
	vec4 sky = clouds(world_pos.xz, hidden, TIME);

	ivec2 hex = containing_hex(p);
	bool inside = on_map(hex);
	int sight = 0, mark = 0, owner = 0, rivers = 0;
	bool hover = false;
	if (inside) {
		vec4 a = texelFetch(tile_a, texel_of(hex), 0);
		vec4 b = texelFetch(tile_b, texel_of(hex), 0);
		sight = byte_of(b.r);
		mark = byte_of(b.g);
		hover = b.b > 0.5;
		owner = byte_of(a.b);
		rivers = byte_of(a.a);
	}
	vec2 d = p - hex_centre(hex);
	float fog = sight == 1 ? 0.72 : 1.0;

	if (debug_mode == 0 && sight > 0) {
		if (mark == 1) colour = mix(colour, vec3(1.0), 0.16);
		else if (mark == 2) colour = mix(colour, vec3(1.0, 0.32, 0.01), 0.42);
		else if (mark == 3) colour = mix(colour, vec3(0.0), 0.5);
		float border = 0.0, glow = 0.0, river = 0.0;
		for (int k = 0; k < 6; k++) {
			float edge = SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]);
			if (owner > 0 && owner_of(hex + DIRECTION[k]) != owner) {
				border = max(border, band(edge, border_width));
				glow = max(glow, 1.0 - smoothstep(0.0, 0.32, edge));
			}
			if (((rivers >> k) & 1) != 0) river = max(river, band(edge, river_width));
		}
		if (owner > 0) {
			vec3 own = palette_entry(owner, 2).rgb * fog;
			colour = mix(colour, own, glow * 0.3);
			colour = mix(colour, own, border);
		}
		colour = mix(colour, vec3(0.03, 0.2, 0.62) * fog, river);
	}

	float cover = debug_mode == 0 ? sky.a : 0.0;
	if (debug_mode == 1) colour = w;
	else if (debug_mode == 2) colour = vec3(hidden);
	else if (debug_mode == 4) colour = vec3(0.8);

	vec3 emission = sky.rgb * cover;
	if (hover) {
		float edge = 1.0;
		for (int k = 0; k < 6; k++) edge = min(edge, SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]));
		float outline = smoothstep(0.02, 0.035, edge) * (1.0 - smoothstep(0.075, 0.09, edge));
		emission = mix(emission, vec3(0.9), outline);
		cover = max(cover, outline);
	}
	ALBEDO = colour * (1.0 - cover);
	EMISSION = emission;
	ROUGHNESS = 0.92;
	SPECULAR = 0.15 * (1.0 - cover);
}
