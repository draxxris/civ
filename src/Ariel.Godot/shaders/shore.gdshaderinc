// Shores: the ground along the waterline where land meets the sea or a lake without a cliff, drawn on the terrain under
// the translucent water so it shows through the shallows. Each land terrain names its own shore (palette turn row, B =
// Game.Shore): dune sand, shell sand, salt-crusted sand, shingle, mudflat or shore ice. None of them is a riverbed look.
// The shore meets the ground the way a riverbed does (river.gdshader): a soft blend whose middle wanders along the coast,
// pushed about by broad clumps and frayed by the ground texture's relief, so the land reaches onto the beach in its own
// tufts.
// Needs terrain_common.gdshaderinc.

// How far up from the waterline (world units, hex outer radius 1) the middle of the blend lies; how far it wanders
// either way along the coast; how far broad clumps push it; how far the ground's relief frays it; and the width of the
// blend, 10% to 90%. Measured from the waterline itself, not the hex edge, so the shore is as wide on every tile.
uniform float shore_reach = 0.18;
uniform float shore_ragged = 0.05;
uniform float shore_clumps = 0.06;
uniform float shore_detail = 0.08;
uniform float shore_blend = 0.06;
// How far inland of the land/water edge (the warped hex blend, see coast_near) a shore may lie at all, so a low hollow
// further inland is not taken for the waterline.
uniform float shore_inland = 1.2;
// Water depth (world units) down to which the shore ground runs under the water before the sea floor takes over.
uniform float shore_depth = 0.04;
// Height above the water (world units) over which wet ground dries.
uniform float shore_wet = 0.012;

// Game.Shore.
const int SHORE_DUNES = 1;
const int SHORE_SHELLS = 2;
const int SHORE_SALT = 3;
const int SHORE_SHINGLE = 4;
const int SHORE_MUDFLAT = 5;
const int SHORE_ICE = 6;

// sRGB colours, decoded like the terrain palette.
const vec3 DUNE_DRY = vec3(0.86, 0.80, 0.64);
const vec3 DUNE_WET = vec3(0.68, 0.62, 0.49);
const vec3 DUNE_GRASS = vec3(0.56, 0.60, 0.36);
const vec3 DUNE_WRACK = vec3(0.25, 0.23, 0.16);
const vec3 SHELL_DRY = vec3(0.87, 0.78, 0.64);
const vec3 SHELL_WET = vec3(0.72, 0.63, 0.52);
const vec3 SHELL_WHITE = vec3(0.95, 0.92, 0.86);
const vec3 SHELL_PINK = vec3(0.91, 0.76, 0.70);
const vec3 SALT_DRY = vec3(0.92, 0.88, 0.78);
const vec3 SALT_WET = vec3(0.74, 0.70, 0.61);
const vec3 SALT_CRUST = vec3(0.97, 0.96, 0.93);
const vec3 SHINGLE_SLATE = vec3(0.47, 0.49, 0.52);
const vec3 SHINGLE_PALE = vec3(0.66, 0.65, 0.62);
const vec3 SHINGLE_BLUE = vec3(0.40, 0.45, 0.51);
const vec3 SHINGLE_GAP = vec3(0.30, 0.30, 0.29);
const vec3 SHINGLE_FROST = vec3(0.85, 0.89, 0.93);
const vec3 FLAT_DRY = vec3(0.53, 0.51, 0.44);
const vec3 FLAT_WET = vec3(0.37, 0.38, 0.34);
const vec3 FLAT_CREEK = vec3(0.24, 0.31, 0.32);
const vec3 FLAT_WEED = vec3(0.30, 0.40, 0.20);
const vec3 FLAT_SAMPHIRE = vec3(0.50, 0.36, 0.30);
const vec3 ICE_WHITE = vec3(0.86, 0.91, 0.95);
const vec3 ICE_BLUE = vec3(0.64, 0.79, 0.90);
const vec3 ICE_CRACK = vec3(0.46, 0.62, 0.74);
const vec3 ICE_WET = vec3(0.50, 0.70, 0.76);
const vec3 ICE_SNOW = vec3(0.95, 0.96, 0.98);

// The coast at p, from the three tiles around it: edge is the distance (world units) inland of the land/water edge,
// negative out over the water, from the same warped barycentric blend as the ground (at an edge's midpoint ln(land/water)
// changes by 4/√3 a world unit); kinds_a and kinds_b are the shares of the six shores among the land corners, under
// sharpened weights so neighbouring shores meet in a short blend; relief is the land texture's relief (ground_relief)
// under the same weights. False where no seen corner is open water or no land corner has a shore. Unexplored tiles do
// not vote.
bool coast_near(vec2 p, vec4 dp, bool linear, out float edge, out vec3 kinds_a, out vec3 kinds_b, out float relief) {
	ivec2 base;
	bool upper;
	vec3 lambda = corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper);
	float land = 0.0, water = 0.0, shores = 0.0;
	ivec3 layers = ivec3(-1);
	ivec3 kinds = ivec3(0);
	for (int k = 0; k < 3; k++) {
		ivec2 t = clamped_texel(corner_hex(base, upper, k));
		if (byte_of(texelFetch(tile_b, t, 0).r) == 0) continue;
		int layer = byte_of(texelFetch(tile_a, t, 0).r);
		// Water layers have no ragged edge (palette edge row, A = 0).
		if (texelFetch(palette, ivec2(layer, 4), 0).a < 0.5) water += lambda[k];
		else {
			land += lambda[k];
			layers[k] = layer;
			kinds[k] = byte_of(texelFetch(palette, ivec2(layer, 7), 0).b);
		}
	}
	edge = 2.0;
	kinds_a = vec3(0.0);
	kinds_b = vec3(0.0);
	relief = 0.5;
	if (water <= 0.0 || land <= 0.0) return false;
	edge = (log(land) - log(max(water, 1e-6))) * SQRT3 / 4.0;
	vec3 sharp = pow(max(lambda, vec3(1e-6)), vec3(4.0));
	int cached_layer = -1;
	float cached_relief = 0.5;
	float total = 0.0;
	relief = 0.0;
	for (int k = 0; k < 3; k++) {
		if (layers[k] < 0) continue;
		total += sharp[k];
		if (kinds[k] <= 0) continue;
		// Neighbouring corners of the same ground share one sample.
		if (layers[k] != cached_layer) {
			cached_relief = ground_relief(layers[k], p, dp, linear);
			cached_layer = layers[k];
		}
		relief += sharp[k] * cached_relief;
		shores += sharp[k];
		int kind = kinds[k];
		if (kind == SHORE_DUNES) kinds_a.x += sharp[k];
		else if (kind == SHORE_SHELLS) kinds_a.y += sharp[k];
		else if (kind == SHORE_SALT) kinds_a.z += sharp[k];
		else if (kind == SHORE_SHINGLE) kinds_b.x += sharp[k];
		else if (kind == SHORE_MUDFLAT) kinds_b.y += sharp[k];
		else if (kind == SHORE_ICE) kinds_b.z += sharp[k];
	}
	if (shores <= 0.0) return false;
	relief /= shores;
	// Land without a shore keeps its own share of the waterline.
	kinds_a /= total;
	kinds_b /= total;
	return true;
}

// How much of the shore shows at p: the ragged blend up from the waterline, a thin wet margin always at it, and under
// the water a fade into the sea floor. up is the distance (world units) up the shore from the waterline, edge the
// distance inland of the land/water edge (coast_near), height the ground's height over the water, footprint the world
// size of a pixel.
float shore_share(vec2 p, float up, float edge, float relief, float height, float footprint) {
	float reach = shore_reach + (seeded_noise(p, 1.4, 601u) - 0.5) * 2.0 * shore_ragged;
	float clumps = (seeded_noise(p, 4.0, 602u) * 0.65 + seeded_noise(p, 8.0, 603u) * 0.35 - 0.5) * 2.0 * shore_clumps;
	float share = ragged_band(reach + clumps - (relief - 0.5) * 2.0 * shore_detail - up, shore_blend, footprint);
	// The wet margin hugs the water: on land lying just above it, low height alone would spread it over the whole flat.
	share = max(share, (1.0 - smoothstep(0.0, shore_wet * 0.5, height)) * (1.0 - smoothstep(0.02, 0.05, up)));
	share *= 1.0 - smoothstep(shore_inland - 0.3, shore_inland, edge);
	return share * smoothstep(-shore_depth, -shore_depth * 0.3, height);
}

// Pale dune sand, drawn into faint wind streaks, with a dark line of washed-up weed at the high-water mark and tufts of
// beach grass above it.
vec3 dunes(vec2 p, float up, float wet, bool linear) {
	vec3 sand = mix(decode_colour(DUNE_DRY, linear), decode_colour(DUNE_WET, linear), wet);
	sand *= 0.93 + 0.1 * seeded_noise(p * vec2(1.0, 2.6), 9.0, 611u) + 0.05 * (seeded_noise(p, 60.0, 612u) - 0.5);
	// The weed lies in short broken clumps along the mark, not in a continuous line.
	float mark = up - 0.1 - (seeded_noise(p, 3.0, 613u) - 0.5) * 0.06;
	float clumps = smoothstep(0.6, 0.75, seeded_noise(p, 12.0, 614u) * 0.6 + seeded_noise(p, 50.0, 615u) * 0.4);
	float wrack = (1.0 - smoothstep(0.006, 0.016, abs(mark))) * clumps;
	vec3 colour = mix(sand, decode_colour(DUNE_WRACK, linear), wrack * 0.7);
	float field = seeded_noise(p, 40.0, 616u) * 0.6 + seeded_noise(p, 95.0, 617u) * 0.4;
	float tufts = smoothstep(0.64, 0.72, field) * smoothstep(0.12, 0.2, up);
	vec3 grass = decode_colour(DUNE_GRASS, linear) * (0.75 + 0.45 * seeded_noise(p * vec2(1.0, 4.0), 140.0, 618u));
	return mix(colour, grass, tufts * 0.9);
}

// Pale, pinkish sand strewn with crushed shell, thickest in drifts.
vec3 shells(vec2 p, float up, float wet, bool linear) {
	vec3 sand = mix(decode_colour(SHELL_DRY, linear), decode_colour(SHELL_WET, linear), wet);
	sand *= 0.92 + 0.08 * seeded_noise(p, 14.0, 621u) + 0.06 * (seeded_noise(p, 90.0, 622u) - 0.5);
	vec2 offset;
	vec3 cell = periodic_cells(p, 70.0, 623u, offset);
	float fleck = (1.0 - smoothstep(0.16, 0.26, cell.x)) * step(0.5, cell.z);
	float drift = smoothstep(0.35, 0.7, seeded_noise(p, 5.0, 624u));
	vec3 shell = mix(decode_colour(SHELL_WHITE, linear), decode_colour(SHELL_PINK, linear), step(0.75, fract(cell.z * 7.1)));
	return mix(sand, shell * (0.9 + 0.15 * fract(cell.z * 3.3)), fleck * mix(0.3, 0.9, drift) * (1.0 - 0.4 * wet));
}

// Near-white sand in ripple marks along and under the water, crusted further up with plates of salt whose raised rims
// catch the light.
vec3 salt(vec2 p, float up, float wet, bool linear) {
	vec3 sand = mix(decode_colour(SALT_DRY, linear), decode_colour(SALT_WET, linear), wet);
	// A whole number of ripples across the map period, so the marks meet across the wrap.
	float phase = p.x * TAU * round(period * 11.0) / period + p.y * TAU * 15.0 + seeded_noise(p, 4.0, 631u) * 6.0;
	sand *= 1.0 + 0.09 * sin(phase) * (1.0 - smoothstep(0.08, 0.2, up));
	sand *= 0.95 + 0.07 * seeded_noise(p, 20.0, 634u);
	vec2 offset;
	vec3 cell = periodic_cells(p, 10.0, 632u, offset);
	float crust = smoothstep(0.02, 0.07, up) * smoothstep(0.4, 0.6, seeded_noise(p, 3.0, 633u) * 0.7 + 0.3 * cell.z);
	float rim = 1.0 - smoothstep(0.03, 0.1, cell.y - cell.x);
	vec3 plate = decode_colour(SALT_CRUST, linear) * (0.93 + 0.08 * cell.z);
	plate = mix(plate * 0.86, plate * 1.05, rim);
	return mix(sand, plate, crust * 0.85);
}

// Packed, flat, rounded pebbles of slate and grey with dark gaps between, darker in the wet and frosted where dry.
vec3 shingle(vec2 p, float wet, bool linear, out float cover) {
	vec2 offset;
	vec3 cell = periodic_cells(p, 48.0, 641u, offset);
	vec2 pebble = stone(cell, offset, 0.5);
	vec3 tone = mix(decode_colour(SHINGLE_SLATE, linear), decode_colour(SHINGLE_PALE, linear), fract(cell.z * 5.3));
	tone = mix(tone, decode_colour(SHINGLE_BLUE, linear), step(0.7, fract(cell.z * 13.1)) * 0.7);
	tone *= pebble.y * (0.94 + 0.1 * fract(cell.z * 9.7));
	float gap = 1.0 - smoothstep(0.0, 0.08, cell.y - cell.x);
	vec3 colour = mix(tone, decode_colour(SHINGLE_GAP, linear), max(gap * 0.6, (1.0 - pebble.x) * 0.45));
	colour *= mix(1.0, 0.72, wet);
	cover = pebble.x * (1.0 - gap);
	return mix(colour, decode_colour(SHINGLE_FROST, linear), (1.0 - wet) * 0.25 * cover);
}

// Glossy grey tidal mud, cut by winding creeks that drain into the sea, filmed with green weed and topped by red
// marsh plants. creek is the share of open creek water.
vec3 mudflat(vec2 p, float up, float wet, bool linear, out float creek) {
	vec3 mud = mix(decode_colour(FLAT_DRY, linear), decode_colour(FLAT_WET, linear), wet);
	mud *= 0.92 + 0.12 * seeded_noise(p, 11.0, 651u);
	vec2 q = p + (vec2(seeded_noise(p, 3.0, 652u), seeded_noise(p, 3.0, 653u)) - 0.5) * 0.35;
	float n = seeded_noise(q, 6.0, 654u) * 0.7 + seeded_noise(q, 15.0, 655u) * 0.3;
	creek = (1.0 - smoothstep(0.012, 0.03, abs(n - 0.5))) * (1.0 - smoothstep(0.06, 0.16, up));
	vec3 colour = mix(mud, decode_colour(FLAT_CREEK, linear), creek);
	float film = smoothstep(0.55, 0.7, seeded_noise(p, 8.0, 656u)) * (1.0 - creek);
	colour = mix(colour, decode_colour(FLAT_WEED, linear), film * 0.55);
	float field = seeded_noise(p, 45.0, 657u) * 0.6 + seeded_noise(p, 110.0, 658u) * 0.4;
	float plants = smoothstep(0.6, 0.72, field) * smoothstep(0.1, 0.2, up);
	vec3 samphire = decode_colour(FLAT_SAMPHIRE, linear) * (0.85 + 0.3 * seeded_noise(p, 160.0, 659u));
	return mix(colour, samphire, plants * 0.6);
}

// Slabs of shore ice split by blue cracks, glassy green-blue at the water and drifted over with snow further up. snow is
// the share of snow drift.
vec3 shore_ice(vec2 p, float up, float wet, bool linear, out float snow) {
	vec2 offset;
	vec2 q = p + (vec2(seeded_noise(p, 7.0, 665u), seeded_noise(p, 7.0, 666u)) - 0.5) * 0.04;
	vec3 cell = periodic_cells(q, 6.0, 661u, offset);
	vec3 slab = mix(decode_colour(ICE_WHITE, linear), decode_colour(ICE_BLUE, linear), fract(cell.z * 3.7) * 0.45 + wet * 0.4);
	slab *= 0.95 + 0.08 * seeded_noise(p, 40.0, 662u);
	// Fine cracks, some open and some healed over, so the slabs do not read as tiles.
	float crack = (1.0 - smoothstep(0.006, 0.022, cell.y - cell.x)) * smoothstep(0.3, 0.6, seeded_noise(p, 14.0, 667u));
	vec3 colour = mix(slab, decode_colour(ICE_CRACK, linear), crack * 0.7);
	snow = smoothstep(0.45, 0.65, seeded_noise(p, 5.0, 663u) * 0.6 + 0.4 * seeded_noise(p, 18.0, 664u)) * smoothstep(0.03, 0.12, up);
	colour = mix(colour, decode_colour(ICE_SNOW, linear), snow);
	return mix(colour, decode_colour(ICE_WET, linear), wet * 0.5 * (1.0 - snow));
}

// The shore's colour at p, the shores blended by their shares, and how glossy it is. up is the distance (world units)
// above the waterline up the shore, height the ground's height over the water.
vec3 shore_colour(vec2 p, vec3 kinds_a, vec3 kinds_b, float up, float height, bool linear, out float shine) {
	float wet = 1.0 - smoothstep(0.0, shore_wet, height + (seeded_noise(p, 9.0, 604u) - 0.5) * shore_wet * 0.5);
	vec3 colour = vec3(0.0);
	shine = 0.0;
	if (kinds_a.x > 0.001) {
		colour += kinds_a.x * dunes(p, up, wet, linear);
		shine += kinds_a.x * 0.35 * wet;
	}
	if (kinds_a.y > 0.001) {
		colour += kinds_a.y * shells(p, up, wet, linear);
		shine += kinds_a.y * 0.35 * wet;
	}
	if (kinds_a.z > 0.001) {
		colour += kinds_a.z * salt(p, up, wet, linear);
		shine += kinds_a.z * 0.4 * wet;
	}
	if (kinds_b.x > 0.001) {
		float cover;
		colour += kinds_b.x * shingle(p, wet, linear, cover);
		shine += kinds_b.x * 0.55 * wet * cover;
	}
	if (kinds_b.y > 0.001) {
		float creek;
		colour += kinds_b.y * mudflat(p, up, wet, linear, creek);
		shine += kinds_b.y * mix(0.25 + 0.4 * wet, 0.9, creek);
	}
	if (kinds_b.z > 0.001) {
		float snow;
		colour += kinds_b.z * shore_ice(p, up, wet, linear, snow);
		shine += kinds_b.z * (0.2 + 0.4 * (1.0 - snow));
	}
	float total = kinds_a.x + kinds_a.y + kinds_a.z + kinds_b.x + kinds_b.y + kinds_b.z;
	return colour / max(total, 1e-6);
}
