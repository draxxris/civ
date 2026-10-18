// Ground routes: ribbons along each route curve plus round hubs at some tile centres (MapRenderer.Roads.cs, RoutePlan), draped
// over the terrain and lit like it. One shader draws every look (RouteLook order): cart path, road, paved road, highway,
// railroad, electric railroad, and the soft shadow under the maglev and jumplane decks (their 3D parts are in
// route_structure.gdshader).
//
// Per vertex: UV = (across, along): across is the signed distance from the centre line in world units (the hub uses the
// distance from its centre), along is arc length in pattern units from the shared tile edge (RoutePlan.PatternUnit divides
// every half path, so sleepers and dashes meet at edges and centres). UV2 = (blend, inner): blend is the share of the
// neighbour's look, inner the distance to a junction or dead end (large on through curves, negative on a hub).
// COLOR.r and COLOR.g are this tile's look and the neighbour's, each divided by 15. COLOR.a from 0.5 to 1 is how far the
// ribbon is raised off the ground onto a deck (a viaduct, bridge or the top of an elevated route): there it is cut to the
// deck's width, its verge takes a neutral deck colour instead of the land's, and elevated looks are not drawn (the deck is
// its own 3D part), so a ground look fades out over a deck. COLOR.a below 0.5 marks a shade ribbon under a raised ground
// route, of strength COLOR.a / 0.45. COLOR.b is the layer, divided by 2:
// 0 the whole look. A junction's paths overlap, so they are drawn as layer 1, the look without markings, only where the
// path's centre line is the nearest (the union of the paths), then layer 2, just the markings (centre line, rails). A path that ends at a hub draws only in
// its wedge about the tile centre, so paths that overlap there cover it once: CUSTOM0 holds the wedge's clockwise and
// counterclockwise boundary rays, CUSTOM1 = (centre x, centre z, past, kind) with kind 0 for no wedge, 1 for a wedge of at
// most half a turn and 2 for a wider one. Kind 3 marks a junction path: CUSTOM0 = (margin, 1 on the through curve), the
// margin being how much nearer the point is to this path's centre line than to any other's, less RouteJunction.Yield. Past is the distance beyond the path's inner end, where across becomes the
// distance from that end, so the path ends in a round cap.
//
// Where two looks meet, the width eases from one to the other and the surfaces crossfade along a ragged, noisy front.
// The organic looks pick up the blended terrain colour under them: light earth over dark ground, a darker, browner take on
// pale ground, faded over several tiles. Edges are broken up by wrap-safe noise, and fog and clouds follow sight per
// pixel like the terrain. Owner borders and the hover outline show through.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// Lifts the ribbon toward the camera so it wins against the terrain triangles it is draped over.
uniform float depth_bias = 0.03;
// Paths that end at a hub keep to their wedge only this close to the centre (world units), past where they can overlap.
const float WEDGE_RADIUS = 0.6;
const float WEDGE_OVERLAP = 5e-4;

const int TRACK = 0;
const int ROAD = 1;
const int PAVED = 2;
const int HIGHWAY = 3;
const int RAILROAD = 4;
const int ELECTRIC = 5;
// Ribbon half widths in world units, RouteLooks.Style(...).Ribbon. Each look is drawn in units of the ribbon (|a| = 1 at
// the outer edge of its verge).
const float RIBBON[8] = float[8](0.11, 0.14, 0.12, 0.13, 0.12, 0.12, 0.12, 0.12);

// sRGB colours, decoded like the terrain palette (see decode_colour).
const vec3 ROAD_DIRT = vec3(0.80, 0.68, 0.50);
const vec3 ROAD_VERGE = vec3(0.50, 0.44, 0.30);
const vec3 MUD = vec3(0.36, 0.31, 0.24);
const vec3 STONE = vec3(0.62, 0.60, 0.57);
const vec3 MORTAR = vec3(0.50, 0.47, 0.42);
const vec3 ASPHALT = vec3(0.21, 0.21, 0.22);
const vec3 SHOULDER = vec3(0.47, 0.44, 0.39);
const vec3 LINE_WHITE = vec3(0.86, 0.86, 0.82);
const vec3 LINE_YELLOW = vec3(0.96, 0.70, 0.12);
const vec3 BALLAST = vec3(0.50, 0.42, 0.34);
const vec3 BALLAST_GREY = vec3(0.52, 0.50, 0.47);
const vec3 SLEEPER_WOOD = vec3(0.37, 0.25, 0.15);
const vec3 SLEEPER_CONCRETE = vec3(0.66, 0.64, 0.60);
const vec3 STEEL = vec3(0.80, 0.81, 0.83);
const vec3 DECK_GREY = vec3(0.62, 0.61, 0.58);
// Half width of the decks a raised ribbon lies on (MapRenderer.RouteParts.cs), in world units.
const float DECK_HALF = 0.097;

varying vec3 world_pos;
varying vec2 lane;
varying vec2 extra;
varying vec2 looks;
varying float draw_layer;
varying float raise;
varying float shade_strength;
varying vec4 wedge;
varying vec4 hub;

void vertex() {
	world_pos = VERTEX;
	lane = UV;
	extra = UV2;
	looks = COLOR.rg;
	draw_layer = COLOR.b * 2.0;
	raise = COLOR.a >= 0.5 ? (COLOR.a - 0.5) * 2.0 : 0.0;
	shade_strength = COLOR.a < 0.5 ? COLOR.a / 0.45 : -1.0;
	wedge = CUSTOM0;
	hub = CUSTOM1;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(-view.xyz) * depth_bias;
	POSITION = PROJECTION_MATRIX * view;
}

// What every look needs at one pixel.
struct Surface {
	vec2 p;
	vec3 land;
	float fogged;
	bool linear;
	float fray;
	float swell;
	float grain;
	float pale;
	float along;
	float along_px;
	float inner;
	bool hub;
	float from_centre;
	int layer;
	// A spur's centre line stops where another path takes over; the through curve's runs on across the junction.
	bool centre_line;
	// On a deck: elevated looks are not drawn.
	bool raised;
};

vec3 tone(vec3 c, Surface s) {
	vec3 decoded = decode_colour(c, s.linear);
	return mix(decoded, fog_tone(decoded), s.fogged);
}

// How pale the ground is near p, 0 (dark, e.g. grassland) to 1 (pale, e.g. plains or sand): each corner tile is judged
// by the average colour of its material (smallest mip), and the corners are blended linearly, not with the terrain's
// sharpened weights. So the road's earth fades gradually from one tile centre to the next instead of switching at the
// terrain border.
float corner_paleness(vec2 p, bool linear) {
	ivec2 base;
	bool upper;
	vec3 weights = blend_weights(corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper), 1.0);
	float pale = 0.0;
	for (int k = 0; k < 3; k++) {
		int layer = byte_of(texelFetch(tile_a, clamped_texel(corner_hex(base, upper, k)), 0).r);
		vec3 average = decode_colour(textureLod(terrain_textures, vec3(0.5, 0.5, float(layer)), 12.0).rgb, linear);
		pale += weights[k] * smoothstep(0.5, 0.58, dot(average, vec3(0.2126, 0.7152, 0.0722)));
	}
	return pale;
}

// Paleness averaged over a disc about three and a half tiles across (two rings of samples), so a road changes its earth
// gradually over several tiles, not at the terrain border.
float ground_paleness(vec2 p, bool linear) {
	float pale = corner_paleness(p, linear);
	for (int k = 0; k < 12; k++) {
		float a = TAU * float(k) / 12.0;
		pale += corner_paleness(p + vec2(cos(a), sin(a)) * 1.8, linear);
		if (k % 2 == 0) pale += corner_paleness(p + vec2(cos(a + 0.26), sin(a + 0.26)) * 0.9, linear);
	}
	return pale / 19.0;
}

// Light earth over dark ground; over pale ground (plains, sand, snow) a darker, browner take on the ground itself.
vec3 earth(Surface s) {
	vec3 dirt = tone(ROAD_DIRT, s);
	return mix(mix(dirt, s.land, 0.15), mix(s.land * vec3(0.8, 0.73, 0.66), dirt * 0.75, 0.3), s.pale);
}

// Worn middle and wheel ruts fade out when thinner than a pixel.
float organic_detail(float px) {
	return 1.0 - smoothstep(0.25, 0.5, px);
}

vec4 road_look(float a, float px, Surface s) {
	float across = abs(a);
	float edge = across + s.fray * 0.2 + s.grain * 0.08;
	float half_width = 0.46 + s.swell * 0.18;
	float track = 1.0 - smoothstep(half_width - 0.2, half_width + 0.06, edge);
	float verge = 1.0 - smoothstep(half_width - 0.05, 1.0, edge);
	float detail = organic_detail(px);
	float middle = 1.0 - smoothstep(0.0, half_width * 0.55, across + s.fray * 0.12);
	float rut_at = abs(across - half_width * 0.5 + s.fray * 0.05);
	float ruts = 1.0 - smoothstep(0.03, 0.09, rut_at);
	vec3 dirt = earth(s) * (1.0 + s.grain * 0.1);
	vec3 colour = mix(dirt, dirt * vec3(0.8, 0.78, 0.75), max(middle * 0.5, ruts * 0.6) * detail);
	vec3 trampled = mix(s.land, tone(ROAD_VERGE, s), 0.3);
	colour = mix(trampled, colour, track);
	return vec4(colour, max(track * 0.92, verge * 0.25));
}

// Two thin ruts that wander together over the grass, with a trampled strip between them. The wander settles near the tile
// centre: there paths with either sign of across meet, and their ruts must line up.
vec4 track_look(float a, float px, Surface s) {
	float wander = (periodic_noise(s.p, 3.1) - 0.5) * 0.55 + (periodic_noise(s.p, 8.3) - 0.5) * 0.18;
	float x = a + wander * smoothstep(0.12, 0.4, s.from_centre);
	float across = abs(x);
	float detail = organic_detail(px);
	// Soft, frayed ruts whose wetness comes and goes, so the grass shows through in places.
	float clumps = periodic_noise(s.p, 31.0) - 0.5;
	float rut_at = abs(across - 0.3) + s.fray * 0.12 + s.grain * 0.06 + clumps * 0.08;
	float rut = (1.0 - smoothstep(0.0, 0.26, rut_at)) * (1.0 - smoothstep(0.0, 0.26, rut_at)) * 0.4
		+ (1.0 - smoothstep(0.02, 0.14, rut_at)) * 0.6;
	float wet = 0.5 + periodic_noise(s.p, 5.3) * 0.3 + clumps * 0.3;
	float strip = 1.0 - smoothstep(0.15, 0.45, across + s.fray * 0.15);
	float verge = 1.0 - smoothstep(0.3, 0.85, across + s.fray * 0.2);
	vec3 mud = mix(tone(MUD, s), s.land * vec3(0.5, 0.48, 0.42), 0.45) * (0.88 + s.grain * 0.15 + clumps * 0.2);
	vec3 dirt = mix(earth(s), mud, 0.5);
	vec3 worn = mix(s.land, dirt, 0.4);
	vec3 colour = mix(worn, mud, rut);
	float alpha = max(rut * wet, max(strip * 0.24, verge * 0.1));
	return mix(vec4(dirt, strip * 0.5), vec4(colour, alpha), detail);
}

// Wrap-safe Voronoi cells on a grid with a whole number of cells per period: x = nearest distance, y = second nearest
// (cell units), z = a hash of the nearest cell.
vec3 periodic_voronoi(vec2 p, float frequency) {
	float cells = max(1.0, round(period * frequency));
	vec2 u = p * (cells / period);
	vec2 i = floor(u);
	vec2 f = u - i;
	float f1 = 8.0, f2 = 8.0, id = 0.0;
	for (int y = -1; y <= 1; y++)
		for (int x = -1; x <= 1; x++) {
			vec2 c = i + vec2(float(x), float(y));
			vec2 key = vec2(mod(c.x, cells), c.y);
			vec2 jitter = vec2(cloud_hash(key), cloud_hash(key + 17.3)) * 0.8 + 0.1;
			float d = length(vec2(float(x), float(y)) + jitter - f);
			if (d < f1) { f2 = f1; f1 = d; id = cloud_hash(key + 5.1); }
			else if (d < f2) f2 = d;
		}
	return vec3(f1, f2, id);
}

// Irregular grey setts in dark joints, with a soft earth verge.
vec4 paved_look(float a, float px, Surface s) {
	vec3 v = periodic_voronoi(s.p, 24.0);
	float joint = v.y - v.x;
	float stone_px = fwidth(s.p.x) * 24.0;
	float detail = 1.0 - smoothstep(0.3, 0.7, stone_px);
	float shade = mix(0.84, 1.04, smoothstep(0.0, 0.45, joint)) * (0.86 + v.z * 0.26);
	vec3 stone = tone(STONE, s) * vec3(1.0 + (v.z - 0.5) * 0.06, 1.0, 1.0 - (v.z - 0.5) * 0.08) * shade;
	vec3 mortar = tone(MORTAR, s);
	vec3 surface = mix(stone, mortar, (1.0 - smoothstep(0.03, 0.1, joint)) * detail * 0.85);
	surface = mix(tone(STONE, s) * 0.86, surface, detail);
	float edge = abs(a) + s.fray * 0.1 + (v.z - 0.5) * 0.05 * detail;
	float cover = 1.0 - smoothstep(0.6, 0.66, edge);
	float verge = 1.0 - smoothstep(0.62, 1.0, abs(a) + s.fray * 0.2);
	if (s.layer == 2) return vec4(0.0);
	vec3 soil = mix(s.land, tone(ROAD_VERGE, s), 0.45);
	return vec4(mix(soil, surface, cover), max(cover * 0.96, verge * 0.35));
}

float stripe(float d, float half_width, float px) {
	return 1.0 - smoothstep(half_width - px, half_width + px, d);
}

// Dark asphalt between gravel shoulders, white edge lines and a dashed yellow centre line.
vec4 highway_look(float a, float px, Surface s) {
	float across = abs(a);
	float tar = periodic_noise(s.p, 13.0);
	vec3 asphalt = tone(ASPHALT, s) * (0.9 + s.grain * 0.25 + tar * 0.15);
	// Lines keep their average coverage once thinner than a pixel, so a far highway still reads as striped.
	float white = stripe(abs(across - 0.56), 0.04, max(px, 0.01));
	float dash = stripe(abs(fract(s.along / 0.2 + 0.5) - 0.5), 0.25, s.along_px * 5.0);
	float yellow = stripe(across, 0.055, max(px, 0.01)) * mix(dash, 0.5, smoothstep(0.3, 0.8, s.along_px * 5.0));
	yellow *= s.hub || !s.centre_line ? 0.0 : smoothstep(0.08, 0.2, s.inner);
	if (s.layer == 2) return vec4(tone(LINE_YELLOW, s), yellow);
	vec3 colour = mix(asphalt, tone(LINE_WHITE, s), white * 0.9);
	colour = mix(colour, tone(LINE_YELLOW, s), s.layer == 1 ? 0.0 : yellow);
	float surface = 1.0 - smoothstep(0.64, 0.68, across);
	float shoulder = 1.0 - smoothstep(0.7, 0.95, across + s.fray * 0.12);
	vec3 gravel = mix(s.land, tone(SHOULDER, s) * (0.9 + s.grain * 0.3), 0.7);
	return vec4(mix(gravel, colour, surface), max(surface * 0.98, shoulder * 0.55));
}

// Steel rails on sleepers in a ragged ballast bed. Rails get a lit crown and a shadow on the far side.
vec4 rail_look(float a, float px, Surface s, bool electric) {
	float across = abs(a);
	vec3 ballast = tone(electric ? BALLAST_GREY : BALLAST, s) * (0.84 + s.grain * 0.35 + periodic_noise(s.p, 63.0) * 0.18);
	float bed = 1.0 - smoothstep(0.62, 0.74, across + s.fray * 0.14 + s.grain * 0.05);
	float verge = 1.0 - smoothstep(0.6, 1.0, across + s.fray * 0.2);
	// Sleepers every fifth of a pattern unit, faded to their average once they are thinner than a pixel.
	float cell = abs(fract(s.along / 0.04 + 0.5) - 0.5);
	float spread = s.along_px * 25.0;
	float sleeper = mix(stripe(cell, 0.2, spread), 0.4, smoothstep(0.25, 0.6, spread));
	sleeper *= stripe(across, 0.5, px) * (s.hub ? 0.0 : 1.0);
	vec3 wood = tone(electric ? SLEEPER_CONCRETE : SLEEPER_WOOD, s) * (0.88 + s.grain * 0.25);
	vec3 colour = mix(ballast, wood, sleeper);
	float rail_at = across - 0.24;
	float rail = stripe(abs(rail_at), 0.045, max(px, 0.012)) * (s.hub ? 0.0 : 1.0);
	float shadow = stripe(abs(rail_at - 0.07), 0.035, max(px, 0.012)) * (s.hub ? 0.0 : 1.0);
	float crown = stripe(abs(rail_at + 0.01), 0.018, max(px, 0.01));
	vec3 steel = tone(STEEL, s);
	if (s.layer == 2) {
		// The rails over a shadow: the shadow darkens what is under it, like the whole look's.
		vec3 metal = mix(steel * 0.55, steel * 1.12, crown);
		float shade = shadow * 0.55 * (1.0 - rail);
		float cover = rail + shade;
		return vec4((metal * rail + colour * 0.45 * shade) / max(cover, 1e-4), cover);
	}
	if (s.layer == 0) {
		colour = mix(colour, colour * 0.45, shadow * 0.7);
		colour = mix(colour, mix(steel * 0.55, steel * 1.12, crown), rail);
	}
	vec3 soil = mix(s.land, tone(ROAD_VERGE, s), 0.4);
	return vec4(mix(soil, colour, bed), max(bed * 0.97, verge * 0.3));
}

// The soft shade under an elevated deck.
vec4 shadow_look(float a, Surface s) {
	float shade = 1.0 - smoothstep(0.15, 0.8, abs(a) + s.fray * 0.1);
	return vec4(s.land * 0.35, shade * 0.3);
}

vec4 look_colour(int look, float a, float px, Surface s) {
	if ((s.layer > 0 && look <= ROAD) || (look > ELECTRIC && (s.layer == 2 || s.raised))) return vec4(0.0);
	if (look == TRACK) return track_look(a, px, s);
	if (look == ROAD) return road_look(a, px, s);
	if (look == PAVED) return paved_look(a, px, s);
	if (look == HIGHWAY) return highway_look(a, px, s);
	if (look == RAILROAD || look == ELECTRIC) return rail_look(a, px, s, look == ELECTRIC);
	return shadow_look(a, s);
}

void fragment() {
	vec2 rel = world_pos.xz - hub.xy;
	int kind = int(round(hub.w));
	// At a junction a path owns the points nearer its centre line than any other's (RouteJunction).
	bool owner = kind != 3 || wedge.x >= 0.0;
	if (!owner && draw_layer < 1.5) discard;
	if ((kind == 1 || kind == 2) && dot(rel, rel) < WEDGE_RADIUS * WEDGE_RADIUS) {
		// Neighbouring wedges share a boundary ray; each reaches a sliver past it (in world units), so a pixel on the ray
		// cannot fall out of both through the rounding of its interpolated position. The overlap is far below a pixel.
		bool after_start = wedge.x * rel.y - wedge.y * rel.x >= -WEDGE_OVERLAP;
		bool before_end = wedge.z * rel.y - wedge.w * rel.x < WEDGE_OVERLAP;
		if (kind == 1 ? !(after_start && before_end) : !(after_start || before_end)) discard;
	}
	Surface s;
	s.p = world_pos.xz;
	s.linear = !OUTPUT_IS_SRGB;
	vec4 water_tint;
	float hidden;
	vec3 weights;
	blend_tiles(s.p, s.linear, 0.0, 0.0, s.land, water_tint, s.fogged, hidden, weights);
	s.land = mix(s.land, tone(DECK_GREY, s), raise);
	s.raised = raise > 0.5;
	// Broad noise swells and narrows a track; fine noise frays its edge.
	s.swell = periodic_noise(s.p, 2.3) - 0.5;
	s.fray = periodic_noise(s.p, 7.0) * 0.6 + periodic_noise(s.p, 19.0) * 0.4 - 0.5;
	s.grain = periodic_noise(s.p, 41.0) - 0.5;
	s.along = lane.y;
	s.along_px = fwidth(lane.y);
	s.inner = extra.y;
	s.hub = extra.y < 0.0;
	s.from_centre = length(rel);
	s.layer = int(round(draw_layer));
	s.centre_line = owner || wedge.y > 0.5;

	int own = int(round(looks.x * 15.0));
	int other = int(round(looks.y * 15.0));
	// The crossfade follows a ragged front: each pixel switches looks when the blend passes its own threshold, spread by
	// noise (or, with setts, by stone, so the stones thin out one by one). Both looks are exact at blend 0 and 1.
	float blend = extra.x;
	float threshold = 0.5 + ((periodic_noise(s.p, 9.0) - 0.5) + (periodic_noise(s.p, 23.0) - 0.5) * 0.5) * 0.22;
	float sharp = 1.6;
	if (own == PAVED || other == PAVED) {
		threshold = 0.1 + periodic_voronoi(s.p, 24.0).z * 0.8;
		sharp = 14.0;
	}
	float share = blend > 0.0 ? clamp((blend - threshold) * sharp + 0.5, 0.0, 1.0) : 0.0;
	bool organic = own <= ROAD || (share > 0.001 && other <= ROAD);
	s.pale = organic ? ground_paleness(s.p, s.linear) : 0.0;

	// The width eases smoothly, whatever the front does.
	float ribbon = mix(RIBBON[own], RIBBON[other], smoothstep(0.0, 1.0, blend));
	float across = hub.z > 0.0 ? (lane.x < 0.0 ? -1.0 : 1.0) * length(vec2(lane.x, hub.z)) : lane.x;
	float a = across / ribbon;
	float px = fwidth(a);
	vec4 first = look_colour(own, a, px, s);
	vec4 second = share > 0.001 ? look_colour(other, a, px, s) : vec4(0.0);
	float alpha = mix(first.a, second.a, share);
	vec3 colour = (first.rgb * first.a * (1.0 - share) + second.rgb * second.a * share) / max(alpha, 1e-4);

	// A shade ribbon (drawn as an elevated look) fades with the height of the route over it.
	if (shade_strength >= 0.0) alpha *= shade_strength;
	// On a deck, nothing overhangs its sides.
	alpha *= mix(1.0, 1.0 - smoothstep(DECK_HALF - max(fwidth(across), 0.002), DECK_HALF, abs(across)), raise);
	alpha *= 1.0 - tile_lines(s.p);
	if (hidden > 0.0) alpha *= 1.0 - clouds(s.p, hidden, TIME, s.linear).a;
	ALBEDO = colour;
	ALPHA = alpha;
	ROUGHNESS = 0.9;
	SPECULAR = 0.15;
}
