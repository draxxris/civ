// Shared by terrain.gdshader, water.gdshader and river.gdshader. Repeats the engine-free blend in Ariel.Client.Core/Terrain exactly:
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
// row 4 ragged-edge strength per layer (A; 0 for water, so seabeds only fade into each other), R = 1 for deep ocean,
// row 5 feature looks (R = ground pattern, G = its strength, B = haze kind, A = haze density), row 6 feature haze colours,
// row 7 texture turn per layer (R = largest hex-tiling turn, 0..1 of 180°).
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

// The texture scale has an integer number of repeats across the period, so a period shift in world x is an integer
// shift in texture space.
vec2 terrain_uv(vec2 p, float frequency) {
	return p * (max(1.0, round(period * frequency)) / period);
}

// Hex-tiling cells per texture repeat. Each cell shows the texture with its own random offset and rotation, so the
// repeat of one image does not show. Must be a whole number so the cells also repeat with the map period.
const float HEX_CELLS = 2.0;
// How far the hex blend weights are sharpened before texture height decides the seams between cells.
const float HEX_SHARPEN = 4.0;

// The texture seen from one hex-tiling cell: turned about the cell centre by up to ±turn·180° and offset, both chosen
// by the cell's wrapped id, so the same cell one map period away looks the same.
vec4 hex_cell_sample(int layer, vec2 uv, vec2 dx, vec2 dy, ivec2 cell, int cells_x, float turn) {
	ivec2 id = ivec2(int(mod(float(cell.x), float(cells_x))), cell.y);
	if (id.x >= cells_x) id.x -= cells_x;
	uint seed = uint(layer) * 9781u + 17u;
	float angle = (lattice_hash(id, seed) * 2.0 - 1.0) * turn * PI;
	vec2 offset = vec2(lattice_hash(id, seed ^ 0x68bc21ebu), lattice_hash(id, seed ^ 0x02e5be93u));
	vec2 centre = vec2(float(cell.x) + 0.5 * float(cell.y), float(cell.y) * SQRT3 * 0.5) / HEX_CELLS;
	mat2 r = mat2(vec2(cos(angle), sin(angle)), vec2(-sin(angle), cos(angle)));
	return textureGrad(terrain_textures, vec3(r * (uv - centre) + centre + offset, float(layer)), r * dx, r * dy);
}

// Hex-tiled texture sample (after Mikkelsen, "Practical Real-Time Hex-Tiling", 2022): three cells of a triangular
// lattice blend with sharpened weights, and texture height decides which cell's ground lies on top at the seams.
// dp = screen derivatives of p (dFdx, dFdy), taken by the caller outside any branch.
vec4 hex_tiled(int layer, vec2 p, vec4 dp) {
	float repeats = max(1.0, round(period * uv_freq));
	vec2 uv = p * (repeats / period);
	vec2 dx = dp.xy * (repeats / period), dy = dp.zw * (repeats / period);
	int cells_x = int(repeats * HEX_CELLS);
	float turn = texelFetch(palette, ivec2(layer, 7), 0).r;
	// Lattice coordinates on the basis (1, 0), (½, √3/2): a shift in x moves only the first, by whole cells per period.
	vec2 st = uv * HEX_CELLS;
	vec2 skew = vec2(st.x - st.y / SQRT3, st.y * 2.0 / SQRT3);
	vec2 cell = floor(skew);
	vec2 f = skew - cell;
	ivec2 c = ivec2(cell);
	ivec2 v0, v1, v2;
	vec3 w;
	if (f.x + f.y < 1.0) {
		v0 = c; v1 = c + ivec2(1, 0); v2 = c + ivec2(0, 1);
		w = vec3(1.0 - f.x - f.y, f.x, f.y);
	} else {
		v0 = c + ivec2(1, 1); v1 = c + ivec2(0, 1); v2 = c + ivec2(1, 0);
		w = vec3(f.x + f.y - 1.0, 1.0 - f.x, 1.0 - f.y);
	}
	vec4 a = hex_cell_sample(layer, uv, dx, dy, v0, cells_x, turn);
	vec4 b = hex_cell_sample(layer, uv, dx, dy, v1, cells_x, turn);
	vec4 e = hex_cell_sample(layer, uv, dx, dy, v2, cells_x, turn);
	w = pow(w, vec3(HEX_SHARPEN)) * (vec3(a.a, b.a, e.a) + 0.15);
	w /= max(w.x + w.y + w.z, 1e-6);
	return a * w.x + b * w.y + e * w.z;
}

// Most a patch partner covers the base ground: a faint hint of it, not a splotch.
uniform float patch_opacity = 0.16;

// A layer's ground, with its patch partner (palette row 4: G = patch layer + 1, B = share) showing through in places.
// The partner's texture height frays the patch edges.
vec4 material_sample(int layer, vec2 p, vec4 dp, bool linear) {
	vec4 sample_colour = hex_tiled(layer, p, dp);
	vec4 edge = texelFetch(palette, ivec2(layer, 4), 0);
	int partner = byte_of(edge.g) - 1;
	float share = edge.b;
	if (partner >= 0 && share > 0.0) {
		uint seed = uint(layer) * 131u + 900u;
		float n = seeded_noise(p, 0.6, seed) * 0.65 + seeded_noise(p, 1.8, seed + 1u) * 0.35;
		// Value noise of this mix is roughly normal around ½; this threshold covers about the share of the ground.
		float threshold = 0.5 + 0.2 * (1.0 - 2.0 * share);
		float reach = n - threshold;
		if (reach > -0.1) {
			vec4 other = hex_tiled(partner, p, dp);
			// Fine noise and texture heights fray the edge into tufts; the partner only tints the base ground.
			float fray = (seeded_noise(p, 5.0, seed + 3u) - 0.5) * 0.35 + (other.a - sample_colour.a) * 0.5;
			float cover = smoothstep(-0.3, 0.3, reach * 3.0 + fray);
			sample_colour = mix(sample_colour, other, cover * patch_opacity);
		}
	}
	return vec4(decode_colour(sample_colour.rgb, linear), sample_colour.a);
}

// Broad, gentle colour drift over the land so wide areas of one ground are not even: brightness, and a shift
// between drier (warmer) and lusher (greener) tones.
uniform float macro_strength = 1.0;
vec3 macro_variation(vec2 p, vec3 colour) {
	float light = seeded_noise(p, 0.3, 801u);
	float lush = seeded_noise(p, 0.2, 803u);
	float l = dot(colour, vec3(0.2126, 0.7152, 0.0722));
	vec3 tone = mix(colour * vec3(1.03, 1.0, 0.94), colour * vec3(0.96, 1.03, 0.97), smoothstep(0.3, 0.7, lush));
	vec3 varied = mix(vec3(l), tone, 1.0 + (lush - 0.5) * 0.12) * mix(0.9, 1.08, smoothstep(0.2, 0.8, light));
	return mix(colour, max(varied, vec3(0.0)), macro_strength);
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

// Feature ground patterns (FeatureGround order) in palette row 5.
const int GROUND_POOLS = 1;
const int GROUND_FERTILE = 2;
const int GROUND_SPRING = 3;
const int GROUND_BLIGHT = 4;
const int GROUND_FLOES = 5;
const int GROUND_SQUALL = 6;
const int GROUND_CRATER = 7;
// Broad enough that forest shade and feature patterns have no visible hex outline.
const float SHADE_SHARPNESS = 2.0;

int ground_pattern(int feature_ground) {
	return byte_of(texelFetch(palette, ivec2(feature_ground, 5), 0).r);
}

// Shares of the feature ground patterns at p under the broad shade weights over seen tiles, each times its strength:
// a = (pools, fertile, spring, blight), b = (floes, squall, crater, unused).
void feature_shares(vec2 p, out vec4 a, out vec4 b) {
	ivec2 base;
	bool upper;
	vec3 soft = blend_weights(corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper), SHADE_SHARPNESS);
	a = vec4(0.0);
	b = vec4(0.0);
	float seen = 0.0;
	for (int k = 0; k < 3; k++) {
		ivec2 t = clamped_texel(corner_hex(base, upper, k));
		if (byte_of(texelFetch(tile_b, t, 0).r) == 0) continue;
		seen += soft[k];
		vec4 look = texelFetch(palette, ivec2(byte_of(texelFetch(tile_a, t, 0).g), 5), 0);
		int pattern = byte_of(look.r);
		float w = soft[k] * look.g;
		if (pattern == GROUND_POOLS) a.x += w;
		else if (pattern == GROUND_FERTILE) a.y += w;
		else if (pattern == GROUND_SPRING) a.z += w;
		else if (pattern == GROUND_BLIGHT) a.w += w;
		else if (pattern == GROUND_FLOES) b.x += w;
		else if (pattern == GROUND_SQUALL) b.y += w;
		else if (pattern == GROUND_CRATER) b.z += w;
	}
	a /= max(seen, 1e-8);
	b /= max(seen, 1e-8);
}

// Distance from p to the nearest river edge of its own hex, or 2 when the hex has none.
float river_edge_distance(vec2 p) {
	ivec2 hex = containing_hex(p);
	if (!on_map(hex)) return 2.0;
	int bits = byte_of(texelFetch(tile_a, texel_of(hex), 0).a);
	vec2 d = p - hex_centre(hex);
	float best = 2.0;
	for (int k = 0; k < 6; k++)
		if ((bits & (1 << k)) != 0) best = min(best, SQRT3 * 0.5 - dot(d, EDGE_NORMAL[k]));
	return best;
}

// Feature ground patterns over the blended ground colour. glow is light the ground gives off (fallout specks), wet the
// share of standing water (glossy) at p.
vec3 feature_ground(vec2 p, vec3 land, vec4 a, vec4 b, bool linear, out vec3 glow, out float wet) {
	glow = vec3(0.0);
	wet = 0.0;
	float lum = dot(land, vec3(0.2126, 0.7152, 0.0722));
	if (a.x > 0.001) {
		// Pools: small, dark, still pools in the hollows of a wandering field, ringed by black mud; tussocks between
		// stay green. A coarse field decides where pools gather, a fine one gives them ragged shores.
		float n = seeded_noise(p, 5.5, 41u) * 0.5 + seeded_noise(p, 12.0, 42u) * 0.32 + seeded_noise(p, 27.0, 53u) * 0.18;
		float gather = smoothstep(0.25, 0.75, seeded_noise(p, 1.6, 54u));
		float level = 1.0 - 0.5 * a.x * mix(0.55, 1.15, gather);
		float pool = smoothstep(level, level + 0.025, n);
		float mud = smoothstep(level - 0.07, level, n) * (1.0 - pool);
		vec3 water = decode_colour(vec3(0.2, 0.25, 0.22), linear) * mix(0.7, 1.05, seeded_noise(p, 16.0, 43u));
		vec3 tussock = land * decode_colour(vec3(0.86, 0.95, 0.72), linear);
		land = mix(land, tussock, 0.5 * a.x);
		land = mix(land, land * 0.5, mud * 0.85);
		land = mix(land, water, pool);
		wet = max(wet, pool * 0.6);
	}
	if (a.y > 0.001) {
		// Fertile: lush green growth and dark wet silt, thickest along the tile's river.
		float riverside = 1.0 - smoothstep(0.05, 0.75, river_edge_distance(p));
		float strength = a.y * mix(0.55, 1.0, riverside);
		float patches = smoothstep(0.3, 0.7, seeded_noise(p, 2.6, 44u) * 0.7 + seeded_noise(p, 9.0, 45u) * 0.3);
		vec3 lush = decode_colour(vec3(0.33, 0.52, 0.18), linear) * (0.75 + 0.5 * lum);
		vec3 silt = decode_colour(vec3(0.34, 0.29, 0.2), linear) * (0.8 + 0.4 * lum);
		vec3 fertile = mix(lush, mix(lush, silt, 0.6), (1.0 - patches) * (0.4 + 0.6 * riverside));
		land = mix(land, fertile, strength * 0.85);
	}
	if (a.z > 0.001) {
		// Spring: a small pool of clear water at the hex centre in a ring of damp, green ground.
		ivec2 hex = containing_hex(p);
		if (on_map(hex) && ground_pattern(byte_of(texelFetch(tile_a, texel_of(hex), 0).g)) == GROUND_SPRING) {
			vec2 d = p - hex_centre(hex);
			float wobble = (seeded_noise(p, 5.0, 46u) - 0.5) * 0.1;
			float r = length(d * vec2(1.0, 1.2)) + wobble;
			float pool = 1.0 - smoothstep(0.3, 0.315, r);
			float damp = (1.0 - smoothstep(0.3, 0.62, r)) * (1.0 - pool);
			vec3 water = mix(decode_colour(vec3(0.12, 0.5, 0.5), linear), decode_colour(vec3(0.04, 0.26, 0.34), linear),
				1.0 - smoothstep(0.0, 0.3, r));
			vec3 sand = land * decode_colour(vec3(0.7, 0.62, 0.5), linear);
			vec3 green = decode_colour(vec3(0.33, 0.45, 0.2), linear) * (0.8 + 0.4 * lum);
			land = mix(land, mix(sand, green, smoothstep(0.34, 0.42, r) * seeded_noise(p, 9.0, 47u)), damp * a.z);
			land = mix(land, water, pool);
			wet = max(wet, pool + damp * 0.3);
		}
	}
	if (a.w > 0.001) {
		// Blight: bleached, sickly yellow ground with dark scorch blotches and a few faintly glowing specks.
		vec3 sick = vec3(lum) * decode_colour(vec3(1.08, 1.02, 0.56), linear);
		float scorch = smoothstep(0.55, 0.8, seeded_noise(p, 3.4, 48u) * 0.65 + seeded_noise(p, 11.0, 49u) * 0.35);
		vec3 blighted = mix(sick, sick * decode_colour(vec3(0.42, 0.36, 0.26), linear), scorch);
		land = mix(land, blighted, a.w * 0.8);
		float speck = smoothstep(0.86, 0.95, seeded_noise(p, 26.0, 50u)) * (1.0 - scorch);
		glow = decode_colour(vec3(0.55, 0.75, 0.12), linear) * speck * a.w * 0.6;
	}
	if (b.y > 0.001) {
		// Squall: the storm's cloud shadow darkens the shore around it.
		land *= 1.0 - 0.45 * b.y;
	}
	if (b.z > 0.001) {
		// Crater: dark ash and cinders over the volcano's flanks, in streaks.
		float streak = seeded_noise(p * vec2(1.0, 0.45), 5.0, 51u) * 0.6 + seeded_noise(p, 13.0, 52u) * 0.4;
		vec3 ash = decode_colour(vec3(0.2, 0.18, 0.17), linear) * mix(0.7, 1.3, streak);
		land = mix(land, ash, b.z * mix(0.55, 0.85, smoothstep(0.35, 0.65, streak)));
	}
	return land;
}

// Hidden tiles supply only cloud coverage, never their true material. Sight and water tint retain geometric weights.
// Ground colour is a softmax of the tiles' claims, ln λ plus a bounded shift. Along a corner-triangle border the opposite
// vertex has λ = 0, so its claim falls to -∞: it contributes nothing on either side and the colour is continuous.
void blend_ground(vec2 p, bool linear, float rock_weight, float snow_weight, vec4 looks_a, vec4 looks_b, out vec3 land,
		out vec4 water_tint, out float fogged, out float hidden, out vec3 weights, out vec3 glow, out float wet) {
	ivec2 base;
	bool upper;
	vec3 lambda = corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper);
	weights = blend_weights(lambda, sharpness);
	// At an edge midpoint (λ = ½, ½) ln λA - ln λB changes by 4/√3 per world unit, so a claim difference d moves the edge
	// by d·√3/4. Nearer the hex corners ln λ is steeper and the same claims move edges less.
	const float CLAIM_PER_UNIT = 4.0 / SQRT3;
	vec3 colours[3];
	vec3 claims = vec3(-1e4);
	// World size of a pixel. Claims can swap corners at triangle borders, so take derivatives of p, not of the claims.
	vec2 fp = fwidth(p);
	vec4 dp = vec4(dFdx(p), dFdy(p));
	float footprint = 2.0 * max(fp.x, fp.y);
	water_tint = vec4(0.0);
	fogged = 0.0;
	hidden = 0.0;
	// Rock shows only on cliffs and under snow; skip its texture elsewhere.
	vec4 rock_sample = rock_weight > 0.0 || snow_weight > 0.0 ? material_sample(cliff_layer, p, dp, linear) : vec4(0.0, 0.0, 0.0, 0.5);
	vec3 rock = rock_sample.rgb;
	vec3 snow = snow_weight > 0.0 ? material_sample(snow_layer, p, dp, linear).rgb : vec3(0.0);
	// Snow settles in the rock's low crevices first, so thin cover follows the stone. Below it, a frost lightens the bare
	// rock. Height shifts the coverage rather than thresholding it, so the fade stays broad and survives mipmapping.
	float snow_mask = smoothstep(0.18, 0.82, snow_weight + (0.5 - rock_sample.a) * 0.55);
	float frost = 0.2 * smoothstep(0.0, 0.5, snow_weight);
	vec3 soft = blend_weights(lambda, SHADE_SHARPNESS);
	vec4 shade = vec4(0.0);
	float soft_seen = 0.0;
	int cached_layer = -1;
	vec4 cached_material = vec4(0.0);
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
		// Neighbouring corners of the same ground share one sample: inside a region of one terrain that is all three.
		vec4 material;
		if (layer == cached_layer) material = cached_material;
		else {
			material = material_sample(layer, p, dp, linear);
			cached_layer = layer;
			cached_material = material;
		}
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
	glow = vec3(0.0);
	wet = 0.0;
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
		land = macro_variation(p, land);
		// Shade, water tint and fog use smooth geometric weights over the revealed corners, not the ragged ground edge.
		shade /= max(soft_seen, 1e-8);
		land = land * (1.0 - shade.a) + shade.rgb;
		land = feature_ground(p, land, looks_a, looks_b, linear, glow, wet);
		// Snow caps by altitude cover whatever the summit tile is made of; fog then tones them like the ground.
		land = mix(land, mix(land, snow, 0.55), frost);
		land = mix(land, snow, snow_mask);
		float geometric_seen = max(1.0 - hidden, 1e-8);
		water_tint /= geometric_seen;
		fogged /= geometric_seen;
		land = mix(land, fog_tone(land), fogged);
		glow *= (1.0 - fogged) * (1.0 - hidden);
	}
}

void blend_tiles(vec2 p, bool linear, float rock_weight, float snow_weight, out vec3 land, out vec4 water_tint, out float fogged, out float hidden, out vec3 weights) {
	vec4 a, b;
	feature_shares(p, a, b);
	vec3 glow;
	float wet;
	blend_ground(p, linear, rock_weight, snow_weight, a, b, land, water_tint, fogged, hidden, weights, glow, wet);
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
