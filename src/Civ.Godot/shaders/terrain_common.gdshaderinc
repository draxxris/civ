// Shared by terrain.gdshader, water.gdshader and river.gdshader. Repeats the engine-free blend in Civ.Client.Core/Terrain exactly:
// WarpGrid.Sample, HexBlend.CornerTriangle, HexBlend.Weights and TerrainSnapshot.ClampedTile. Uses only texelFetch on
// RGBA8 data textures and a mipmapped albedo/height array on the Compatibility renderer (no depth or screen reads).

// Per tile, offset coordinates (col, row), one texel per tile.
// tile_a: R = terrain layer, G = feature ground, B = owner + 1, A = river edge bits (Hex.Directions order).
uniform sampler2D tile_a : filter_nearest, repeat_disable;
// tile_b: R = sight (0 hidden, 1 fogged, 2 visible), G = overlay mark (1 workable, 2 worked, 3 locked), B = hover.
uniform sampler2D tile_b : filter_nearest, repeat_disable;
// Domain warp grid: R,G = dx and B,A = dz, each a 16-bit value mapping [0, 65535] to [-1, 1].
uniform sampler2D warp_grid : filter_nearest, repeat_disable;
// Colour table (sRGB bytes): row 0 layer colours, row 1 feature ground tints (A = strength),
// row 2 owner colours (index = owner + 1), row 3 water tints per layer (A = strength).
uniform sampler2D palette : filter_nearest, repeat_disable;
// RGB = sRGB albedo, A = linear height. No source_color hint: decode RGB only (see decode_colour), leaving height unchanged.
uniform sampler2DArray terrain_textures : filter_linear_mipmap, repeat_enable;
uniform int cliff_layer = 0;
uniform int seabed_layer = 0;
uniform int deep_seabed_layer = 0;
uniform int unexplored_layer = 0;
uniform int snow_layer = 0;
uniform float uv_freq = 0.38;
uniform float height_depth = 0.38;
// Colour transitions are broader than relief, cloud coverage and scatter density transitions.
uniform float ground_sharpness = 1.6;

uniform ivec2 map_size = ivec2(1, 1);
uniform bool wrap_x = true;
uniform float period = 1.0;
uniform float sharpness = 4.0;
uniform float warp_amplitude = 0.3;
uniform ivec2 warp_size = ivec2(1, 2);
uniform vec2 warp_step = vec2(1.0, 1.0);
uniform float warp_z_min = 0.0;

const float SQRT3 = 1.7320508075688772;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

// Colour data is stored as sRGB. Pass `!OUTPUT_IS_SRGB` as `linear`: Forward+ and Mobile light in linear space, but the
// Compatibility renderer lights and outputs in sRGB space, like its source_color textures. Decoding there would show
// every texture as its linearised value (mid-grey rock nearly black, sand neon orange) and out of step with the props.
vec3 decode_colour(vec3 c, bool linear) {
	return linear ? srgb_to_linear(c) : c;
}

int byte_of(float v) {
	return int(round(v * 255.0));
}

vec4 palette_entry(int index, int row, bool linear) {
	vec4 c = texelFetch(palette, ivec2(index, row), 0);
	return vec4(decode_colour(c.rgb, linear), c.a);
}

// Canonical x in [0, period) on wrapping maps, so all three wrap copies sample identically.
float wrap_world_x(float x) {
	return wrap_x ? x - period * floor(x / period) : x;
}

vec2 warp_texel(int col, int row) {
	vec4 b = round(texelFetch(warp_grid, ivec2(col, row), 0) * 255.0);
	return vec2(b.r * 256.0 + b.g, b.b * 256.0 + b.a) / 65535.0 * 2.0 - 1.0;
}

// WarpGrid.Sample: bilinear, wrapping in x over the period, clamping in z.
vec2 warp_sample(vec2 p) {
	float u = (p.x - period * floor(p.x / period)) / warp_step.x;
	float fu = floor(u);
	int c0 = min(int(fu), warp_size.x - 1);
	int c1 = c0 + 1 == warp_size.x ? 0 : c0 + 1;
	float tx = u - fu;
	float v = clamp((p.y - warp_z_min) / warp_step.y, 0.0, float(warp_size.y - 1));
	int r0 = min(int(v), warp_size.y - 2);
	float tz = v - float(r0);
	vec2 top = mix(warp_texel(c0, r0), warp_texel(c1, r0), tx);
	vec2 bottom = mix(warp_texel(c0, r0 + 1), warp_texel(c1, r0 + 1), tx);
	return mix(top, bottom, tz);
}

// HexBlend.CornerTriangle: barycentric λ of the triangle of tile centres containing q; base and upper identify the hexes.
vec3 corner_lambda(vec2 q, out ivec2 base, out bool upper) {
	float aq = SQRT3 / 3.0 * q.x - q.y / 3.0;
	float ar = 2.0 * q.y / 3.0;
	float fi = floor(aq), fj = floor(ar);
	base = ivec2(int(fi), int(fj));
	float fq = aq - fi, fr = ar - fj;
	upper = fq + fr >= 1.0;
	return upper ? vec3(fq + fr - 1.0, 1.0 - fq, 1.0 - fr) : vec3(1.0 - fq - fr, fq, fr);
}

ivec2 corner_hex(ivec2 base, bool upper, int k) {
	if (upper) return k == 0 ? base + ivec2(1, 1) : k == 1 ? base + ivec2(0, 1) : base + ivec2(1, 0);
	return k == 0 ? base : k == 1 ? base + ivec2(1, 0) : base + ivec2(0, 1);
}

// HexBlend.Weights: λ^s, normalised.
vec3 blend_weights(vec3 lambda, float s) {
	vec3 l = pow(max(clamp(lambda, 0.0, 1.0), vec3(1e-6)), vec3(s));
	float sum = l.x + l.y + l.z;
	return sum > 0.0 ? l / sum : vec3(1.0 / 3.0);
}

// Odd-row offset column of an axial hex: q + floor(r / 2) (HexGrid.ToOffset).
int offset_col(ivec2 hex) {
	return hex.x + int(floor(float(hex.y) * 0.5));
}

int wrap_col(int col) {
	return wrap_x ? col - map_size.x * int(floor(float(col) / float(map_size.x))) : clamp(col, 0, map_size.x - 1);
}

// TerrainSnapshot.ClampedTile: rows beyond the north and south edge clamp to the nearest row.
ivec2 clamped_texel(ivec2 hex) {
	return ivec2(wrap_col(offset_col(hex)), clamp(hex.y, 0, map_size.y - 1));
}

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

// Owner border line width, shared by the terrain and the river surface drawn over it.
uniform float border_width = 0.075;

float band(float distance, float width) {
	return 1.0 - smoothstep(width * 0.7, width, distance);
}


// Hover outline coverage at offset d from the hovered hex centre.
float hover_outline(vec2 d) {
	float edge = 1.0;
	for (int k = 0; k < 6; k++) edge = min(edge, SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]));
	return smoothstep(0.02, 0.035, edge) * (1.0 - smoothstep(0.075, 0.09, edge));
}

// Coverage of the owner border and hover lines the terrain draws at p, so surfaces over the terrain can let them through.
float tile_lines(vec2 p) {
	ivec2 hex = containing_hex(p);
	if (!on_map(hex)) return 0.0;
	vec4 b = texelFetch(tile_b, texel_of(hex), 0);
	if (byte_of(b.r) == 0) return 0.0;
	vec2 d = p - hex_centre(hex);
	float lines = b.b > 0.5 ? hover_outline(d) : 0.0;
	int owner = owner_of(hex);
	if (owner == 0) return lines;
	for (int k = 0; k < 6; k++)
		if (owner_of(hex + DIRECTION[k]) != owner) lines = max(lines, band(SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]), border_width));
	return lines;
}

// Sight-only version of blend_tiles: the fogged and hidden shares at p, without any material sampling.
void sight_blend(vec2 p, out float fogged, out float hidden) {
	ivec2 base;
	bool upper;
	vec3 weights = blend_weights(corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper), sharpness);
	fogged = 0.0;
	hidden = 0.0;
	for (int k = 0; k < 3; k++) {
		int sight = byte_of(texelFetch(tile_b, clamped_texel(corner_hex(base, upper, k)), 0).r);
		if (sight == 0) hidden += weights[k];
		else if (sight == 1) fogged += weights[k];
	}
	fogged /= max(1.0 - hidden, 1e-8);
}

vec3 fog_tone(vec3 c) {
	float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
	return mix(vec3(l), c, 0.55) * 0.72;
}



float cloud_hash(vec2 i) {
	return fract(sin(dot(i, vec2(127.1, 311.7))) * 43758.5453);
}

// Value noise in [0, 1] that repeats every period in x (a whole number of cells per period).
float periodic_noise(vec2 p, float frequency) {
	float cells = max(1.0, round(period * frequency));
	vec2 u = p * (cells / period);
	vec2 i = floor(u);
	vec2 t = u - i;
	t = t * t * (3.0 - 2.0 * t);
	float i0 = mod(i.x, cells);
	float i1 = mod(i.x + 1.0, cells);
	if (i0 >= cells) i0 -= cells;
	if (i1 >= cells) i1 -= cells;
	float a = cloud_hash(vec2(i0, i.y)), b = cloud_hash(vec2(i1, i.y));
	float c = cloud_hash(vec2(i0, i.y + 1.0)), d = cloud_hash(vec2(i1, i.y + 1.0));
	return mix(mix(a, b, t.x), mix(c, d, t.x), t.y);
}

float periodic_fbm(vec2 p, float frequency) {
	return periodic_noise(p, frequency) * 0.57 + periodic_noise(p, frequency * 2.0) * 0.29 + periodic_noise(p, frequency * 4.0) * 0.14;
}

// Both scales have an integer number of repeats across the period. A 90-degree rotation of the second sample
// swaps the axes, so a period shift in world x is an integer shift in texture v too. Mip derivatives stay unwrapped.
vec2 terrain_uv(vec2 p, float frequency) {
	return p * (max(1.0, round(period * frequency)) / period);
}

vec4 material_sample(int layer, vec2 p, bool linear) {
	vec4 first = texture(terrain_textures, vec3(terrain_uv(p, uv_freq), float(layer)));
	vec4 second = texture(terrain_textures, vec3(terrain_uv(p, uv_freq * 1.47).yx + vec2(0.37, 0.61), float(layer)));
	float variation = smoothstep(0.2, 0.8, periodic_noise(p, 0.16));
	vec4 sample_colour = mix(first, second, variation * 0.55);
	return vec4(decode_colour(sample_colour.rgb, linear), sample_colour.a);
}

// Hidden tiles supply only cloud coverage, never their true material. Sight and water tint retain geometric weights.
// Along a zero-weight edge, the largest remaining weight is at least 0.5. Keep depth + height scale < 0.5,
// so the opposite corner contributes exactly zero on either side, even if height_depth changes.
void blend_tiles(vec2 p, bool linear, float rock_weight, float snow_weight, out vec3 land, out vec4 water_tint, out float fogged, out float hidden, out vec3 weights) {
	ivec2 base;
	bool upper;
	vec3 lambda = corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper);
	weights = blend_weights(lambda, sharpness);
	vec3 ground_weights = blend_weights(lambda, ground_sharpness);
	float depth = clamp(height_depth, 0.01, 0.48);
	float height_scale = min(0.10, 0.49 - depth);
	vec3 colours[3];
	vec3 heights = vec3(0.0);
	vec3 revealed = vec3(0.0);
	water_tint = vec4(0.0);
	fogged = 0.0;
	hidden = 0.0;
	vec3 rock = material_sample(cliff_layer, p, linear).rgb;
	vec3 snow = snow_weight > 0.0 ? material_sample(snow_layer, p, linear).rgb : vec3(0.0);
	for (int k = 0; k < 3; k++) {
		colours[k] = vec3(0.0);
		ivec2 t = clamped_texel(corner_hex(base, upper, k));
		int sight = byte_of(texelFetch(tile_b, t, 0).r);
		if (sight == 0) {
			hidden += weights[k];
			continue;
		}
		revealed[k] = ground_weights[k];
		vec4 a = texelFetch(tile_a, t, 0);
		int layer = byte_of(a.r);
		vec4 material = material_sample(layer, p, linear);
		heights[k] = material.a * height_scale;
		vec3 c = mix(material.rgb, rock, rock_weight);
		vec4 tint = palette_entry(byte_of(a.g), 1, linear);
		c = mix(c, tint.rgb, tint.a);
		vec4 slime = palette_entry(layer, 3, linear);
		c = mix(c, slime.rgb, slime.a);
		// Snow caps by altitude cover whatever the summit tile is made of; fog then tones them like the ground.
		c = mix(c, snow, snow_weight);
		if (sight == 1) {
			c = fog_tone(c);
			fogged += weights[k];
		}
		colours[k] = c;
		water_tint += weights[k] * slime;
	}
	float seen = revealed.x + revealed.y + revealed.z;
	land = vec3(0.0);
	if (seen > 1e-5) {
		vec3 scores = heights + revealed / seen;
		float highest = max(scores.x, max(scores.y, scores.z));
		vec3 adjusted = max(scores - vec3(highest) + vec3(depth), vec3(0.0));
		// Exclude hidden corners before normalisation, even at the feathered reveal boundary.
		adjusted *= step(vec3(1e-8), revealed);
		adjusted /= max(adjusted.x + adjusted.y + adjusted.z, 1e-8);
		land = colours[0] * adjusted.x + colours[1] * adjusted.y + colours[2] * adjusted.z;
		// Tint and fog accumulated with the original geometric weights, not the broader colour weights.
		float geometric_seen = max(1.0 - hidden, 1e-8);
		water_tint /= geometric_seen;
		fogged /= geometric_seen;
	}
}

// Two wrap-safe scrolling samples of the cloud layer, plus periodic coverage noise. Fully hidden means full cover.
vec4 clouds(vec2 p, float hidden, float time, bool linear) {
	vec2 slow_p = p + vec2(time * 0.05, time * 0.012);
	vec2 fast_p = p + vec2(-time * 0.11, time * 0.03);
	vec3 slow = decode_colour(texture(terrain_textures, vec3(terrain_uv(slow_p, 0.12), float(unexplored_layer))).rgb, linear);
	vec3 fast = decode_colour(texture(terrain_textures, vec3(terrain_uv(fast_p, 0.19).yx + vec2(0.23, 0.49), float(unexplored_layer))).rgb, linear);
	float n = periodic_fbm(slow_p, 0.22) * 0.65 + periodic_fbm(fast_p, 0.55) * 0.35;
	vec3 colour = mix(slow, fast, 0.35) * mix(0.8, 1.12, n);
	float cover = smoothstep(0.3, 0.7, hidden + (n - 0.5) * 0.4);
	return vec4(colour, cover);
}
