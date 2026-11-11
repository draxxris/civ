// River water and riverbeds: strips along each river curve, closed round the ponds at river ends (RiverMesher), draped
// over the channel the terrain carves. UV.x runs across the strip (|x| = 1 at the waterline, RiverMesher.BankExtent at the
// outer edge of the riverbed band), UV2 is the downstream flow (x, z) times speed, COLOR.r fades the river where sea or
// lake water covers it, COLOR.g marks steep white water, COLOR.b is the rock contact (RiverMeshData.Rock): 1 at the side
// of a cliff wall's rock column standing in the water, 0 a short way out from it, which churns the water white around the
// rock, and COLOR.a is the water's depth over RiverMeshData.DepthRange. Waterfalls are drawn separately
// (waterfall.gdshader).
// The water is drawn over its own bed: the bed's colour shows through the shallows and fades into the deep colour as the
// water deepens, so the shore has no edge of its own. The surface is the one all water shares (water_surface.gdshaderinc),
// its waves drifting downstream. Between the water and the grass the banks show the same bed: sand with stones along the
// waterline, or cracked mud where the terrain asks for it (palette turn row, G). Bank normals lean toward the water (see
// RiverMesher), so the light picks out the channel's walls and the river reads as sunk into the ground.
// Only sight, owner borders, hover and the terrain layer's riverbed are read from the tile data.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"
#include "res://shaders/surf.gdshaderinc"
#include "res://shaders/water_surface.gdshaderinc"

// Lifts the strip toward the camera so it wins against the terrain triangles it is draped over.
uniform float depth_bias = 0.03;
// White water where the river runs against the rock of a cliff (see surf.gdshaderinc). A river is narrow and the rock
// stands in it, so it churns at the rock and draws out only faint foam lines.
uniform float cliff_churn = 1.0;
uniform float cliff_streaks = 0.35;
// The riverbed meets the ground the way two grounds meet (see blend_ground): a soft blend whose centre line wanders, frayed
// by the ground texture's own height so the ground reaches into the bed in its own tufts. Distances are in lanes past the
// waterline (one lane is about a lowland river's half width, RiverMesher.BankUnit).
// bed_reach: mean distance out to the middle of the blend; bed_ragged: how far it wanders either way along the river.
// bed_clumps: how far broad clumps of ground push into the bed or patches of bed out into the ground.
// Each comes in two: a sandy bank is a thin strip, a mud bank a wider apron of silt (mud_ versions).
// bed_detail: how far the ground texture's height frays the blend into tufts.
// bed_blend: width of the blend, from 10% to 90% of the bed; it widens to a few pixels far away so it never shimmers.
uniform float bed_reach = 1.8;
uniform float bed_ragged = 0.3;
uniform float bed_clumps = 0.35;
uniform float mud_reach = 2.0;
uniform float mud_ragged = 0.4;
uniform float mud_clumps = 0.5;
uniform float bed_detail = 1.3;
uniform float bed_blend = 0.7;
// Lanes at the edge of the strip, where the bed must be gone (RiverMesher.BankExtent).
uniform float bank_extent = 3.6;

// Still water is calmer: its wave slopes are scaled by still_calm.
uniform float still_calm = 0.7;
// Depth (world units) over which the bed fades out under the water, and how far the surface bends the view of it.
uniform float clarity = 0.022;
// How opaque the water is over its bed at the very edge, where it is thinnest.
uniform float shallows_alpha = 0.45;
uniform float refraction = 0.025;
// Foam from the normal map's foam pattern where the water runs steep (COLOR.g).
uniform float foam_amount = 1.6;

// Sand: pale and dry up the bank, darker where wet; pebbles of grey and brown on it.
const vec3 SAND_DRY = vec3(0.70, 0.64, 0.52);
const vec3 SAND_WET = vec3(0.45, 0.40, 0.33);
const vec3 PEBBLE_GREY = vec3(0.59, 0.58, 0.54);
const vec3 PEBBLE_BROWN = vec3(0.54, 0.46, 0.37);
// Mud: dark wet silt at the water, drying paler and cracking further up the bank.
const vec3 MUD_WET = vec3(0.30, 0.24, 0.17);
const vec3 MUD_DRY = vec3(0.56, 0.46, 0.34);
const vec3 MUD_CRACK = vec3(0.30, 0.24, 0.18);

varying vec3 world_pos;
varying vec2 lane;
varying vec2 flow;
varying vec4 info;

void vertex() {
	world_pos = VERTEX;
	lane = UV;
	flow = UV2;
	info = COLOR;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(-view.xyz) * depth_bias;
	POSITION = PROJECTION_MATRIX * view;
}

// The ground around p, from the three tiles around it, blended like the ground so it changes softly where the terrain
// does: the share of muddy riverbed (palette turn row, G) and the ground texture's relief (ground_relief), which frays the
// bed's edge equally for every ground. Unexplored tiles do not vote.
void ground_under(vec2 p, vec4 dp, bool linear, out float mud, out float height) {
	ivec2 base;
	bool upper;
	vec3 weights = blend_weights(corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper), 2.0);
	float seen = 0.0;
	mud = 0.0;
	height = 0.0;
	int cached_layer = -1;
	float cached_height = 0.5;
	for (int k = 0; k < 3; k++) {
		ivec2 t = clamped_texel(corner_hex(base, upper, k));
		if (byte_of(texelFetch(tile_b, t, 0).r) == 0) continue;
		int layer = byte_of(texelFetch(tile_a, t, 0).r);
		// Neighbouring corners of the same ground share one sample.
		if (layer != cached_layer) {
			cached_height = ground_relief(layer, p, dp, linear);
			cached_layer = layer;
		}
		mud += weights[k] * texelFetch(palette, ivec2(layer, 7), 0).g;
		height += weights[k] * cached_height;
		seen += weights[k];
	}
	mud = seen > 0.0 ? mud / seen : 0.0;
	height = seen > 0.0 ? height / seen : 0.5;
}

// Pale, even sand strewn with pebbles, darker at the waterline, with a few larger stones right along it. wet is 1 at the
// water. The stones are close to the sand's own tone and only softly shaded, so the bank reads as one even surface
// rather than as a crazing of dark gaps.
vec3 gravel_bed(vec2 p, float wet, float rocks, bool linear, out float bump) {
	vec3 sand = mix(decode_colour(SAND_DRY, linear), decode_colour(SAND_WET, linear), wet);
	sand *= 0.9 + 0.1 * periodic_noise(p, 23.0) + 0.07 * (periodic_noise(p, 97.0) - 0.5) + 0.04 * (periodic_noise(p, 181.0) - 0.5);
	vec2 small_offset, large_offset;
	vec3 small = periodic_cells(p, 38.0, 101u, small_offset);
	vec3 large = periodic_cells(p, 13.0, 211u, large_offset);
	// Pebbles of mixed size, thinning out up the bank; the stones keep to the waterline.
	float keep = step(0.35 + 0.4 * (1.0 - wet), fract(small.z * 7.13));
	vec2 pebble = stone(small, small_offset, 0.19 + 0.13 * fract(small.z * 3.31));
	vec2 rock = stone(large, large_offset, 0.27 + 0.11 * fract(large.z * 5.7));
	float pebbles = pebble.x * keep * max(rocks, 1.0 - wet);
	float stones = rock.x * step(0.52, large.z) * rocks;
	vec3 tone = mix(decode_colour(PEBBLE_GREY, linear), decode_colour(PEBBLE_BROWN, linear), fract(small.z * 11.7));
	tone *= mix(1.08, 0.82, wet) * (0.93 + 0.13 * fract(small.z * 17.3));
	vec3 stone_tone = mix(decode_colour(PEBBLE_GREY, linear), decode_colour(PEBBLE_BROWN, linear), fract(large.z * 3.7));
	stone_tone *= mix(1.0, 0.8, wet) * (0.95 + 0.1 * periodic_noise(p, 61.0));
	// A faint shadow on the sand just beyond each stone grounds it.
	float shadow = (1.0 - smoothstep(0.0, 0.45, large.x - 0.05)) * step(0.52, large.z) * rocks * (1.0 - rock.x);
	vec3 colour = sand * (1.0 - 0.16 * shadow);
	colour = mix(colour, tone * pebble.y, pebbles * 0.7);
	colour = mix(colour, stone_tone * rock.y, stones * 0.92);
	bump = max(pebbles * 0.45, stones);
	return colour;
}

// Dark wet silt by the water, drying paler up the bank and splitting into cracked plates.
vec3 mud_bed(vec2 p, float wet, bool linear) {
	vec3 mud = mix(decode_colour(MUD_DRY, linear), decode_colour(MUD_WET, linear), wet);
	mud *= 0.88 + 0.24 * periodic_fbm(p, 9.0);
	vec2 unused;
	vec3 plates = periodic_cells(p, 14.0, 307u, unused);
	float crack = (1.0 - smoothstep(0.03, 0.09, plates.y - plates.x)) * (1.0 - wet);
	mud = mix(mud, decode_colour(MUD_CRACK, linear), crack * 0.8);
	return mud * (0.94 + 0.12 * plates.z);
}

void fragment() {
	vec2 p = world_pos.xz;
	bool linear = !OUTPUT_IS_SRGB;
	float fogged, hidden;
	sight_blend(p, fogged, hidden);
	vec4 dp = vec4(dFdx(p), dFdy(p));

	float across = abs(lane.x);
	float wobble = periodic_noise(p, 5.0) - 0.5;
	float shoreline = across + wobble * 0.16;
	float water = 1.0 - smoothstep(0.86, 1.0, shoreline);
	float depth = info.a * 0.15;

	// The surface: the normal map drifting downstream as fast as the water runs; still water (a pond, the end of a river
	// widening into one) drifts slowly on its own, so it never stands as a mirror.
	float speed = length(flow);
	vec2 velocity = flow + vec2(0.35, 0.2) * (1.0 - smoothstep(0.0, 0.3, speed));
	vec3 waves = water_waves(p, velocity, TIME);
	float running = smoothstep(0.0, 0.3, speed);
	vec2 slope = waves.xy * mix(still_calm, 1.0, running);
	float foam_pattern = waves.z;
	vec3 surface = normalize(vec3(-slope.x, 1.0, -slope.y));

	// The bed, seen through the water bent by its surface: sand and stones, or mud.
	vec2 seen_at = p + slope * refraction * smoothstep(0.0, 0.03, depth) * water;
	float mud, height;
	ground_under(seen_at, dp, linear, mud, height);
	float wet = 1.0 - smoothstep(0.9, 1.35, shoreline);
	// The stones keep to a band along the waterline: mid-channel the bed is clean sand under the water.
	float rocks = (1.0 - smoothstep(1.0, 1.45, shoreline + wobble * 0.3)) * smoothstep(0.55, 0.9, shoreline);
	float bump = 0.0;
	vec3 sandy = mud < 0.999 ? gravel_bed(seen_at, wet, rocks, linear, bump) : vec3(0.0);
	vec3 silt = mud > 0.001 ? mud_bed(seen_at, wet, linear) : vec3(0.0);
	vec3 ground = mix(sandy, silt, mud);
	bump *= 1.0 - mud;
	// The steep wall just above the water is in its own shade on top of the leaning normal.
	ground *= 1.0 - 0.18 * (1.0 - smoothstep(1.0, 1.4, across)) * (1.0 - water);

	// The water: the bed through the shallows, fading into the deep colour; the sky toward grazing angles; foam where it
	// runs steep and against rock.
	// The river is the same water as the lake or sea it runs into (water_over_floor): in the channel it shows like the
	// sea at its shore over sand; toward the edges it thins over its own bed, which shows through more and more.
	float murk = 1.0 - exp(-depth / clarity);
	vec3 under = mix(ground, material_sample(seabed_layer, seen_at, dp, linear).rgb, murk);
	vec3 river = water_over_floor(under, mix(shallows_alpha, WATER_SHALLOW_ALPHA, murk), linear);
	vec3 view_normal = normalize((VIEW_MATRIX * vec4(surface, 0.0)).xyz);
	river = water_sky(river, view_normal, VIEW, linear);
	float foam = clamp(foam_pattern * foam_amount - 0.5 / foam_amount, 0.0, 1.0) * info.g;
	float surf = cliff_surf(p, info.b, TIME, cliff_churn, cliff_streaks);
	foam = max(foam, surf);
	river = mix(river, decode_colour(WATER_FOAM, linear), foam);

	// The riverbed band: from under the water out to where the ground takes over again, blended like two grounds. The
	// middle of the blend wanders slowly along the river and is pushed about by broad clumps; the ground texture's height
	// frays it, so tall grass reaches into the bed in its own tufts while the low gaps between them show bed.
	float reach = mix(bed_reach, mud_reach, mud) + (periodic_noise(p, 1.4) - 0.5) * 2.0 * mix(bed_ragged, mud_ragged, mud);
	float clumps = (periodic_fbm(p, 4.0) - 0.5) * 2.0 * mix(bed_clumps, mud_clumps, mud);
	float claim = reach + clumps - (height - 0.5) * 2.0 * bed_detail - across;
	float bed = ragged_band(claim, bed_blend, fwidth(lane.x));
	// A thin wet margin always shows at the water, and the bed is gone before the strip ends.
	bed = max(bed, 1.0 - smoothstep(1.0, 1.1, shoreline));
	bed *= 1.0 - smoothstep(bank_extent - 0.6, bank_extent, across);

	vec3 colour = mix(ground, river, water);
	float alpha = max(water, bed * 0.96);
	alpha *= info.r * (1.0 - tile_lines(p));
	colour = mix(colour, fog_tone(colour) * 1.1, fogged);
	if (hidden > 0.0) alpha *= 1.0 - clouds(p, hidden, TIME, linear).a;

	NORMAL = normalize(mix(NORMAL, view_normal, water));
	ALBEDO = colour;
	ALPHA = alpha;
	// Water shines like all water (WATER_ROUGHNESS, WATER_SPECULAR); foam is matte. Wet stones and mud shine a little.
	float bank_rough = mix(0.92, 0.55, wet * max(bump, mud * 0.6));
	ROUGHNESS = mix(bank_rough, mix(WATER_ROUGHNESS, 0.8, foam), water);
	SPECULAR = mix(0.1, WATER_SPECULAR, water) * (1.0 - fogged * 0.6);
}
