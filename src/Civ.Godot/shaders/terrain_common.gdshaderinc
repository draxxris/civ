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
// row 2 owner colours (index = owner + 1), row 3 water tints per layer (A = strength),
// row 4 ragged-edge strength per layer (A; 0 for water, so seabeds only fade into each other), R = 1 for deep ocean.
uniform sampler2D palette : filter_nearest, repeat_disable;
// RGB = sRGB albedo, A = linear height. No source_color hint: decode RGB only (see decode_colour), leaving height unchanged.
uniform sampler2DArray terrain_textures : filter_linear_mipmap, repeat_enable;
uniform int cliff_layer = 0;
uniform int seabed_layer = 0;
uniform int deep_seabed_layer = 0;
uniform int unexplored_layer = 0;
uniform int snow_layer = 0;
uniform float uv_freq = 0.38;
// Ground transitions are soft blends whose centre line wanders, instead of a fade along the straight hex edge. Each tile's
// claim on a point is its log barycentric weight plus its own wrap-safe noise field, its spread (palette row 0 alpha) and
// its texture height; a softmax of the claims gives the colour weights. Distances are world units (hex outer radius 1),
// measured at a hex edge's midpoint.
// ground_reach: furthest the noise moves a transition from the hex edge.
// ground_creep: extra distance the most spreading ground (grass) pushes into the least spreading one (desert).
// ground_detail: how far texture height frays the transition into tufts.
// ground_blend: width of the transition, from 10% to 90% of the neighbour's colour.
uniform float ground_reach = 0.4;
uniform float ground_creep = 0.12;
uniform float ground_detail = 0.12;
uniform float ground_blend = 0.35;
uniform float ground_noise_frequency = 1.4;

uniform ivec2 map_size = ivec2(1, 1);
uniform bool wrap_x = true;
uniform float period = 1.0;
uniform float sharpness = 4.0;
uniform float warp_amplitude = 0.3;
uniform ivec2 warp_size = ivec2(1, 2);
uniform vec2 warp_step = vec2(1.0, 1.0);
uniform float warp_z_min = 0.0;

#include "res://shaders/hash.gdshaderinc"

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
uniform bool show_borders = true;
uniform bool show_grid = false;

float band(float distance, float width) {
	return 1.0 - smoothstep(width * 0.7, width, distance);
}


// Hover outline coverage at offset d from the hovered hex centre.
float hover_outline(vec2 d) {
	float edge = 1.0;
	for (int k = 0; k < 6; k++) edge = min(edge, SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]));
	return smoothstep(0.02, 0.035, edge) * (1.0 - smoothstep(0.075, 0.09, edge));
}

// Geometry-only grid, clipped by perceived sight. Derivatives keep its thin strokes stable at different zoom levels.
float grid_outline(vec2 d) {
	float edge = 1.0;
	for (int k = 0; k < 6; k++) edge = min(edge, SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]));
	float aa = max(fwidth(edge), 0.002);
	return show_grid ? 1.0 - smoothstep(0.018, 0.018 + aa, edge) : 0.0;
}

// Shared terrain/water shading. The river lets the same underlying lines through via tile_lines.
vec3 map_overlays(vec2 p, vec3 colour, bool linear) {
	ivec2 hex = containing_hex(p);
	if (!on_map(hex)) return colour;
	vec4 b = texelFetch(tile_b, texel_of(hex), 0);
	int sight = byte_of(b.r);
	if (sight == 0) return colour;
	int mark = byte_of(b.g);
	if (mark == 1) colour = mix(colour, vec3(1.0), 0.16);
	else if (mark == 2) colour = mix(colour, vec3(1.0, 0.32, 0.01), 0.42);
	else if (mark == 3) colour = mix(colour, vec3(0.0), 0.5);
	vec2 d = p - hex_centre(hex);
	colour = mix(colour, vec3(0.08), grid_outline(d) * 0.65);
	int owner = owner_of(hex);
	if (!show_borders || owner == 0) return colour;
	float border = 0.0, glow = 0.0;
	for (int k = 0; k < 6; k++) {
		float edge = SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]);
		if (owner_of(hex + DIRECTION[k]) != owner) {
			border = max(border, band(edge, border_width));
			glow = max(glow, 1.0 - smoothstep(0.0, 0.32, edge));
		}
	}
	vec3 own = palette_entry(owner, 2, linear).rgb * (sight == 1 ? 0.72 : 1.0);
	colour = mix(colour, own, glow * 0.3);
	return mix(colour, own, border);
}

// Coverage of the owner border, grid and hover lines, so surfaces over the terrain can let them through.
float tile_lines(vec2 p) {
	ivec2 hex = containing_hex(p);
	if (!on_map(hex)) return 0.0;
	vec4 b = texelFetch(tile_b, texel_of(hex), 0);
	if (byte_of(b.r) == 0) return 0.0;
	vec2 d = p - hex_centre(hex);
float lines = max(b.b > 0.5 ? hover_outline(d) : 0.0, grid_outline(d));
	int owner = owner_of(hex);
	if (!show_borders || owner == 0) return lines;
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

// Share of deep ocean at p under broad blend weights, so open water shades from coast to ocean over a wide band instead of
// following the seabed's steeper height blend. Weights are continuous across corner-triangle borders for any sharpness.
float ocean_share(vec2 p, float s) {
	ivec2 base;
	bool upper;
	vec3 weights = blend_weights(corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper), s);
	float ocean = 0.0;
	for (int k = 0; k < 3; k++) {
		int layer = byte_of(texelFetch(tile_a, clamped_texel(corner_hex(base, upper, k)), 0).r);
		ocean += weights[k] * texelFetch(palette, ivec2(layer, 4), 0).r;
	}
	return ocean;
}

vec3 fog_tone(vec3 c) {
	float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
	return mix(vec3(l), c, 0.55) * 0.72;
}



// Value noise in [0, 1] that repeats every period in x. The seed picks an independent field.
float seeded_noise(vec2 p, float frequency, uint seed) {
	int cells = max(1, int(round(period * frequency)));
	vec2 u = p * (float(cells) / period);
	vec2 f = floor(u);
	vec2 t = u - f;
	t = t * t * (3.0 - 2.0 * t);
	// GLSL leaves % of negative ints undefined, so wrap in float (exact for these small integers).
	int x0 = int(mod(f.x, float(cells)));
	if (x0 >= cells) x0 -= cells;
	int x1 = x0 + 1 == cells ? 0 : x0 + 1;
	int y = int(f.y);
	float a = lattice_hash(ivec2(x0, y), seed), b = lattice_hash(ivec2(x1, y), seed);
	float c = lattice_hash(ivec2(x0, y + 1), seed), d = lattice_hash(ivec2(x1, y + 1), seed);
	return mix(mix(a, b, t.x), mix(c, d, t.x), t.y);
}

// Value noise in [0, 1] that repeats every period in x (a whole number of cells per period).
float periodic_noise(vec2 p, float frequency) {
	return seeded_noise(p, frequency, 0u);
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

// Noise in [-1, 1] for one tile's ground claim. Each tile has its own field, seeded by its canonical texel so wrap copies
// agree, so two neighbours disagree and their shared edge wanders.
float ground_noise(vec2 p, ivec2 texel) {
	uint seed = uint(texel.x) * 73856093u ^ uint(texel.y) * 19349663u;
	float f = ground_noise_frequency;
	float n = seeded_noise(p, f, seed) * 0.53 + seeded_noise(p, f * 2.0, seed ^ 0x9e3779b9u) * 0.29
		+ seeded_noise(p, f * 4.0, seed ^ 0x85ebca6bu) * 0.18;
	return clamp((n - 0.5) * 4.5, -1.0, 1.0);
}

// Hidden tiles supply only cloud coverage, never their true material. Sight and water tint retain geometric weights.
// Ground colour is a softmax of the tiles' claims, ln λ plus a bounded shift. Along a corner-triangle border the opposite
// vertex has λ = 0, so its claim falls to -∞: it contributes nothing on either side and the colour is continuous.
void blend_tiles(vec2 p, bool linear, float rock_weight, float snow_weight, out vec3 land, out vec4 water_tint, out float fogged, out float hidden, out vec3 weights) {
	ivec2 base;
	bool upper;
	vec3 lambda = corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper);
	weights = blend_weights(lambda, sharpness);
	// At an edge midpoint (λ = ½, ½) ln λA - ln λB changes by 4/√3 per world unit, so a claim difference d moves the edge
	// by d·√3/4. Nearer the hex corners ln λ is steeper and the same claims move edges less.
	const float CLAIM_PER_UNIT = 4.0 / SQRT3;
	// Broad enough that forest shade has no visible hex outline.
	const float SHADE_SHARPNESS = 2.0;
	vec3 colours[3];
	vec3 claims = vec3(-1e4);
	// World size of a pixel. Claims can swap corners at triangle borders, so take derivatives of p, not of the claims.
	vec2 fp = fwidth(p);
	float footprint = 2.0 * max(fp.x, fp.y);
	water_tint = vec4(0.0);
	fogged = 0.0;
	hidden = 0.0;
	vec4 rock_sample = material_sample(cliff_layer, p, linear);
	vec3 rock = rock_sample.rgb;
	vec3 snow = snow_weight > 0.0 ? material_sample(snow_layer, p, linear).rgb : vec3(0.0);
	// Snow settles in the rock's low crevices first, so thin cover follows the stone. Below it, a frost lightens the bare
	// rock. Height shifts the coverage rather than thresholding it, so the fade stays broad and survives mipmapping.
	float snow_mask = smoothstep(0.18, 0.82, snow_weight + (0.5 - rock_sample.a) * 0.55);
	float frost = 0.2 * smoothstep(0.0, 0.5, snow_weight);
	vec3 soft = blend_weights(lambda, SHADE_SHARPNESS);
	vec4 shade = vec4(0.0);
	float soft_seen = 0.0;
	for (int k = 0; k < 3; k++) {
		colours[k] = vec3(0.0);
		ivec2 t = clamped_texel(corner_hex(base, upper, k));
		int sight = byte_of(texelFetch(tile_b, t, 0).r);
		if (sight == 0) {
			hidden += weights[k];
			continue;
		}
		soft_seen += soft[k];
		vec4 a = texelFetch(tile_a, t, 0);
		int layer = byte_of(a.r);
		vec4 material = material_sample(layer, p, linear);
		float spread = texelFetch(palette, ivec2(layer, 0), 0).a;
		// Two noises in [-1, 1] differ by up to 2, the spreads by up to 1: each term's largest edge shift is its uniform.
		float ragged = texelFetch(palette, ivec2(layer, 4), 0).a;
		float shift = ragged * (0.5 * ground_reach * ground_noise(p, t) + ground_detail * material.a) + ground_creep * spread;
		claims[k] = log(max(lambda[k], 1e-6)) + shift * CLAIM_PER_UNIT;
		vec3 c = mix(material.rgb, rock, rock_weight);
		vec4 slime = palette_entry(layer, 3, linear);
		colours[k] = mix(c, slime.rgb, slime.a);
		// Feature ground (forest floor) is shade under props, not ground: it fades softly instead of following the edge.
		vec4 tint = palette_entry(byte_of(a.g), 1, linear);
		shade += soft[k] * vec4(tint.rgb * tint.a, tint.a);
		if (sight == 1) fogged += weights[k];
		water_tint += weights[k] * slime;
	}
	land = vec3(0.0);
	if (hidden < 1.0 - 1e-5) {
		float highest = max(claims.x, max(claims.y, claims.z));
		// At least the edge width, widened to a few pixels far away so the ragged edge does not shimmer.
		// Softmax: exp(s·claim) = λ^s · e^(s·shift), the sharpened blend weights with the noise moving the transition.
		// Across an edge the claim difference changes by CLAIM_PER_UNIT per world unit, and 10% to 90% is a difference of
		// 2·ln 9 / s. Far away the width grows to a few pixels so the wandering line does not shimmer.
		float s = 2.0 * log(9.0) / (max(ground_blend, footprint) * CLAIM_PER_UNIT);
		// Hidden corners keep a claim of -1e4, so their weight underflows to zero.
		vec3 adjusted = exp(s * (claims - vec3(highest)));
		adjusted /= adjusted.x + adjusted.y + adjusted.z;
		land = colours[0] * adjusted.x + colours[1] * adjusted.y + colours[2] * adjusted.z;
		// Shade, water tint and fog use smooth geometric weights over the revealed corners, not the ragged ground edge.
		shade /= max(soft_seen, 1e-8);
		land = land * (1.0 - shade.a) + shade.rgb;
		// Snow caps by altitude cover whatever the summit tile is made of; fog then tones them like the ground.
		land = mix(land, mix(land, snow, 0.55), frost);
		land = mix(land, snow, snow_mask);
		float geometric_seen = max(1.0 - hidden, 1e-8);
		water_tint /= geometric_seen;
		fogged /= geometric_seen;
		land = mix(land, fog_tone(land), fogged);
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
