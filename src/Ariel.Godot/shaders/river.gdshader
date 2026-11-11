// River water and riverbeds: strips along each river curve and discs for the ponds at river ends (RiverMesher), draped
// over the channel the terrain carves. UV.x runs across the strip (|x| = 1 at the waterline, RiverMesher.BankExtent at the
// outer edge of the riverbed band; out from a pond's centre the same way), UV2 is the downstream flow (x, z) times speed,
// COLOR.r fades the river where sea or lake water covers it, COLOR.g marks steep white water and COLOR.b is the rock
// contact (RiverMeshData.Rock): 1 at the side of a cliff wall's rock column standing in the water, 0 a short way out from
// it, which churns the water white around the rock. Waterfalls are drawn separately (waterfall.gdshader).
// Between the water and the grass the channel's banks show bare riverbed: pale sand and pebbles with rocks along the
// waterline, or dark cracked mud where the terrain asks for it (palette turn row, G). Bank normals lean toward the water
// (see RiverMesher), so the light picks out the channel's walls and the river reads as sunk into the ground.
// Only sight, owner borders, hover and the terrain layer's riverbed are read from the tile data.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"
#include "res://shaders/surf.gdshaderinc"

// Lifts the strip toward the camera so it wins against the terrain triangles it is draped over.
uniform float depth_bias = 0.03;
// White water where the river runs against the rock of a cliff (see surf.gdshaderinc). A river is narrow and the rock
// stands in it, so it churns at the rock and draws out only faint foam lines.
uniform float cliff_churn = 1.0;
uniform float cliff_streaks = 0.35;
// Where the riverbed band gives way to the ground, in water half widths, and how far its edge wanders either way.
uniform float bed_reach = 2.15;
uniform float bed_ragged = 0.5;

// sRGB colours, decoded like the terrain palette (see decode_colour): slate water that lightens over the shallows and
// darkens in the shade of its banks.
const vec3 RIVER_DEEP = vec3(0.20, 0.32, 0.40);
const vec3 RIVER_SHALLOW = vec3(0.38, 0.48, 0.48);
const vec3 RIVER_LIGHT = vec3(0.70, 0.78, 0.80);
// Gravel: dry pale sand, wet darker sand at the waterline, and pebbles from grey to warm brown.
const vec3 SAND_DRY = vec3(0.70, 0.64, 0.52);
const vec3 SAND_WET = vec3(0.42, 0.38, 0.31);
const vec3 PEBBLE_GREY = vec3(0.56, 0.55, 0.52);
const vec3 PEBBLE_BROWN = vec3(0.50, 0.42, 0.33);
// Mud: dark wet silt at the water, drying paler and cracking further up the bank.
const vec3 MUD_WET = vec3(0.30, 0.24, 0.17);
const vec3 MUD_DRY = vec3(0.56, 0.46, 0.34);
const vec3 MUD_CRACK = vec3(0.30, 0.24, 0.18);

varying vec3 world_pos;
varying vec2 lane;
varying vec2 flow;
varying vec3 info;

void vertex() {
	world_pos = VERTEX;
	lane = UV;
	flow = UV2;
	info = COLOR.rgb;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(-view.xyz) * depth_bias;
	POSITION = PROJECTION_MATRIX * view;
}

// Two half-cycle-offset samples advected along the flow and cross-faded, so ripples drift downstream without stretching.
float flowing_ripples(vec2 p, vec2 velocity, float time) {
	float phase = fract(time * 0.25);
	float other = fract(phase + 0.5);
	vec2 a = p - velocity * phase * 0.9;
	vec2 b = p - velocity * other * 0.9 + vec2(0.37, 0.71);
	float first = periodic_noise(a, 6.0) * 0.6 + periodic_noise(a, 13.0) * 0.4;
	float second = periodic_noise(b, 6.0) * 0.6 + periodic_noise(b, 13.0) * 0.4;
	return mix(first, second, abs(1.0 - 2.0 * phase));
}

// Cellular noise that repeats every period in x: distance to the nearest and second-nearest jittered cell point (in
// cells) and a random value for the nearest cell; offset is the nearest point's direction from p, in cells.
vec3 periodic_cells(vec2 p, float frequency, uint seed, out vec2 offset) {
	int cells = max(1, int(round(period * frequency)));
	vec2 u = p * (float(cells) / period);
	vec2 f = floor(u);
	float first = 8.0, second = 8.0, value = 0.0;
	offset = vec2(0.0);
	for (int j = -1; j <= 1; j++)
		for (int i = -1; i <= 1; i++) {
			vec2 c = f + vec2(float(i), float(j));
			int x = int(mod(c.x, float(cells)));
			if (x >= cells) x -= cells;
			ivec2 cell = ivec2(x, int(c.y));
			vec2 point = c + vec2(lattice_hash(cell, seed), lattice_hash(cell, seed + 7u)) * 0.8 + 0.1;
			float d = length(u - point);
			if (d < first) {
				second = first;
				first = d;
				value = lattice_hash(cell, seed + 13u);
				offset = point - u;
			} else if (d < second) second = d;
		}
	return vec3(first, second, value);
}

// A rounded stone of the given radius (in cells) around a cell point: coverage, and a dome shade that is lit on the side
// toward the sun (north-west, like the terrain's light) and dark on the far side.
vec2 stone(vec3 cell, vec2 offset, float radius) {
	float cover = 1.0 - smoothstep(radius * 0.8, radius, cell.x);
	// offset points from p to the stone's centre, so it points away from the sun on the lit side.
	float facing = dot(offset / max(radius, 1e-4), vec2(0.6, 0.6));
	return vec2(cover, 1.0 + 0.35 * facing - 0.25 * smoothstep(radius * 0.5, radius, cell.x));
}

// Share of muddy riverbed at p: the riverbed of the three tiles around p, blended like the ground so it changes softly
// where the terrain does. Unexplored tiles do not vote.
float riverbed_mud(vec2 p) {
	ivec2 base;
	bool upper;
	vec3 weights = blend_weights(corner_lambda(p + warp_sample(p) * warp_amplitude, base, upper), 2.0);
	float mud = 0.0, seen = 0.0;
	for (int k = 0; k < 3; k++) {
		ivec2 t = clamped_texel(corner_hex(base, upper, k));
		if (byte_of(texelFetch(tile_b, t, 0).r) == 0) continue;
		int layer = byte_of(texelFetch(tile_a, t, 0).r);
		mud += weights[k] * texelFetch(palette, ivec2(layer, 7), 0).g;
		seen += weights[k];
	}
	return seen > 0.0 ? mud / seen : 0.0;
}

// Pale grainy sand strewn with pebbles, wet and dark at the waterline, with larger rocks right along it. wet is 1 at the
// water. Each stone is shaded as a dome so it stands up from the sand instead of reading as a painted dot.
vec3 gravel_bed(vec2 p, float wet, float rocks, bool linear, out float bump) {
	vec3 sand = mix(decode_colour(SAND_DRY, linear), decode_colour(SAND_WET, linear), wet);
	sand *= 0.86 + 0.14 * periodic_noise(p, 23.0) + 0.1 * (periodic_noise(p, 97.0) - 0.5) + 0.08 * (periodic_noise(p, 181.0) - 0.5);
	vec2 small_offset, large_offset;
	vec3 small = periodic_cells(p, 38.0, 101u, small_offset);
	vec3 large = periodic_cells(p, 13.0, 211u, large_offset);
	// Pebbles of mixed size, thinning out up the bank; the rocks keep to the waterline.
	float keep = step(0.25 + 0.45 * (1.0 - wet), fract(small.z * 7.13));
	vec2 pebble = stone(small, small_offset, 0.2 + 0.14 * fract(small.z * 3.31));
	vec2 rock = stone(large, large_offset, 0.28 + 0.12 * fract(large.z * 5.7));
	float pebbles = pebble.x * keep;
	float stones = rock.x * step(0.45, large.z) * rocks;
	vec3 tone = mix(decode_colour(PEBBLE_GREY, linear), decode_colour(PEBBLE_BROWN, linear), fract(small.z * 11.7));
	tone *= mix(1.1, 0.8, wet) * (0.92 + 0.16 * fract(small.z * 17.3));
	vec3 stone_tone = mix(decode_colour(PEBBLE_GREY, linear), decode_colour(PEBBLE_BROWN, linear), fract(large.z * 3.7));
	stone_tone *= mix(1.0, 0.75, wet) * (0.9 + 0.2 * periodic_noise(p, 61.0));
	// A soft shadow on the sand just beyond each stone's dark side grounds it.
	float shadow = (1.0 - smoothstep(0.0, 0.5, large.x - 0.05)) * step(0.45, large.z) * rocks * (1.0 - rock.x);
	vec3 colour = sand * (1.0 - 0.25 * shadow);
	colour = mix(colour, tone * pebble.y, pebbles * 0.8);
	colour = mix(colour, stone_tone * rock.y, stones);
	bump = max(pebbles * 0.5, stones);
	return colour;
}

// Dark wet silt by the water, drying paler up the bank and splitting into cracked plates.
vec3 mud_bed(vec2 p, float wet, bool linear) {
	vec3 mud = mix(decode_colour(MUD_DRY, linear), decode_colour(MUD_WET, linear), wet);
	mud *= 0.88 + 0.24 * periodic_fbm(p, 9.0);
	vec2 plate_offset;
	vec3 plates = periodic_cells(p, 14.0, 307u, plate_offset);
	float crack = (1.0 - smoothstep(0.03, 0.09, plates.y - plates.x)) * (1.0 - wet);
	mud = mix(mud, decode_colour(MUD_CRACK, linear), crack * 0.8);
	return mud * (0.94 + 0.12 * plates.z);
}

void fragment() {
	vec2 p = world_pos.xz;
	bool linear = !OUTPUT_IS_SRGB;
	float fogged, hidden;
	sight_blend(p, fogged, hidden);

	float across = abs(lane.x);
	float wobble = periodic_noise(p, 5.0) - 0.5;
	float shoreline = across + wobble * 0.16;
	float water = 1.0 - smoothstep(0.86, 1.0, shoreline);

	float ripples = flowing_ripples(p, flow, TIME);
	float centre = 1.0 - smoothstep(0.1, 0.95, across);
	vec3 river = mix(decode_colour(RIVER_SHALLOW, linear), decode_colour(RIVER_DEEP, linear), centre);
	river = mix(river, decode_colour(RIVER_LIGHT, linear), smoothstep(0.58, 0.85, ripples) * 0.25);
	river = mix(river, vec3(0.9, 0.93, 0.94), info.g * smoothstep(0.4, 0.75, ripples) * 0.7);
	// The water's edge lies in the shade of the bank above it.
	river *= 1.0 - 0.3 * smoothstep(0.55, 0.95, shoreline);
	float surf = cliff_surf(p, info.b, TIME, cliff_churn, cliff_streaks) * water;
	river = mix(river, vec3(0.92, 0.96, 1.0), surf);

	// The riverbed band: from under the water out to a ragged edge where the grass takes over again.
	float ragged = periodic_noise(p, 2.3) * 0.5 + periodic_noise(p, 7.5) * 0.3 + periodic_noise(p, 19.0) * 0.2 - 0.5;
	float edge = bed_reach + ragged * 2.0 * bed_ragged;
	// Sand thins into the grass over a soft margin instead of ending at a line.
	float bed = 1.0 - smoothstep(edge - 0.55, edge + 0.1, across);
	float wet = 1.0 - smoothstep(0.9, 1.35, shoreline);
	float rocks = 1.0 - smoothstep(1.0, 1.45, shoreline + wobble * 0.3);
	float mud = riverbed_mud(p);
	float bump = 0.0;
	vec3 gravel = mud < 0.999 ? gravel_bed(p, wet, rocks, linear, bump) : vec3(0.0);
	vec3 silt = mud > 0.001 ? mud_bed(p, wet, linear) : vec3(0.0);
	vec3 ground = mix(gravel, silt, mud);
	bump *= 1.0 - mud;
	// The steep wall just above the water is in its own shade on top of the leaning normal.
	ground *= 1.0 - 0.18 * (1.0 - smoothstep(1.0, 1.4, across));

	vec3 colour = mix(ground, river, water);
	float alpha = max(max(water * 0.9, surf * 0.95), bed * 0.96);
	alpha *= info.r * (1.0 - tile_lines(p));
	colour = mix(colour, fog_tone(colour) * 1.1, fogged);
	if (hidden > 0.0) alpha *= 1.0 - clouds(p, hidden, TIME, linear).a;

	// Ripples tilt the water normal along the flow; banks keep the leaning normal from the mesh.
	vec2 tilt = (ripples - 0.5) * 0.35 * (length(flow) > 0.0 ? normalize(flow) : vec2(0.0));
	vec3 rippled = normalize((VIEW_MATRIX * vec4(normalize(vec3(-tilt.x, 1.0, -tilt.y)), 0.0)).xyz);
	NORMAL = normalize(mix(NORMAL, rippled, water));
	ALBEDO = colour;
	ALPHA = alpha;
	// Water is glossy but not a mirror, so a still pond does not flash white at grazing angles; wet stones and mud shine a
	// little.
	float bank_rough = mix(0.92, 0.6, wet * max(bump, mud * 0.6));
	ROUGHNESS = mix(bank_rough, 0.42, water);
	SPECULAR = mix(0.12, 0.3, water) * (1.0 - fogged * 0.6);
}
