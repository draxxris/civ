// The surface every body of water shares, so a river runs into a lake or the sea as the same water: its colours, the
// waves of a normal map (Water/README.md) drifting along the water's motion, the sky reflected toward grazing angles, and
// how it shines. Sea and lake water (water.gdshader) drift the waves with the wind and add swells offshore; rivers
// (river.gdshader) drift them downstream and show their bed through the shallows.
// Needs terrain_common.gdshaderinc (period, decode_colour). TerrainRenderer sets water_normal on both materials.

// The normal map, at water_tiling repeats per world unit and again twice as fine, each drifted in two cross-faded
// phases. A phase lasts 1 / flow_rate seconds and drifts flow_distance world units at unit speed.
uniform sampler2D water_normal : filter_linear_mipmap, repeat_enable;
uniform float water_tiling = 1.1;
uniform float normal_strength = 0.55;
uniform float flow_rate = 0.22;
uniform float flow_distance = 0.25;

// sRGB colours, decoded like the terrain palette (see decode_colour): shallow coastal water and the open ocean's deep
// water, the sky the surface reflects, and foam.
const vec3 WATER_SHALLOW = vec3(0.07, 0.40, 0.50);
const vec3 WATER_DEEP = vec3(0.07, 0.27, 0.52);
const vec3 WATER_SKY = vec3(0.66, 0.76, 0.84);
const vec3 WATER_FOAM = vec3(0.92, 0.96, 1.0);
// Shallow water lets its floor through: WATER_BED_SHOW of the floor's colour mixes into the water's own colour, and the
// water is WATER_SHALLOW_ALPHA opaque over the floor (the sea draws itself translucent over the terrain's sea floor, so
// it gets the second share by blending; a river, drawing its own floor, takes both shares in water_over_floor).
const float WATER_BED_SHOW = 0.08;
const float WATER_SHALLOW_ALPHA = 0.84;
// Open water shines softly: a tile spans a city's worth of water, so the sun's reflection must read as a broad sheen
// broken up by the waves, not as a nearby lamp.
const float WATER_ROUGHNESS = 0.25;
const float WATER_SPECULAR = 0.12;

// One scale of the normal map, drifted along velocity in two half-cycle-offset phases and cross-faded, so it moves
// without stretching: the surface slope (x, z) and the foam pattern. tiling is rounded to whole repeats per period, so
// the surface is seamless across the wrap.
vec3 water_layer(vec2 p, vec2 velocity, float tiling, float time, vec2 jump) {
	float repeats = max(1.0, round(period * tiling));
	float scale = repeats / period;
	vec3 sum = vec3(0.0);
	for (int k = 0; k < 2; k++) {
		float offset = float(k) * 0.5;
		float progress = fract(time + offset);
		vec2 uv = (p - velocity * flow_distance * (progress - 0.5)) * scale + offset + (time - progress) * jump;
		vec3 s = texture(water_normal, uv).rgb;
		sum += vec3(s.rg * 2.0 - 1.0, s.b) * (1.0 - abs(1.0 - 2.0 * progress));
	}
	return sum;
}

// The waves at p, drifting along velocity (world units a second, scaled by flow_distance): the surface slope (x, z) and
// a foam pattern, bright where foam gathers. seconds is the clock, so a storm can run it faster.
vec3 water_waves(vec2 p, vec2 velocity, float seconds) {
	float time = seconds * flow_rate;
	vec3 coarse = water_layer(p, velocity, water_tiling, time, vec2(0.24, 0.2083));
	vec3 fine = water_layer(p, velocity, water_tiling * 2.0, time, vec2(0.2, 0.25));
	return vec3((coarse.xy * 0.65 + fine.xy * 0.35) * normal_strength, coarse.z * fine.z * 2.0);
}

// The sky in the surface: little looking down into the water, more toward grazing angles (Schlick's fresnel).
// view_normal and view are in view space (NORMAL, VIEW).
vec3 water_sky(vec3 colour, vec3 view_normal, vec3 view, bool linear) {
	float fresnel = 0.02 + 0.98 * pow(1.0 - clamp(dot(view_normal, view), 0.0, 1.0), 5.0);
	return mix(colour, decode_colour(WATER_SKY, linear), fresnel * 0.8);
}

// Shallow water's colour over its floor, the way the sea shows over its own: the water tinted by the floor and laid over
// it, opaque by alpha (the sea is WATER_SHALLOW_ALPHA opaque at the shore). For a surface drawn opaque, so it must show
// its floor itself.
vec3 water_over_floor(vec3 floor_colour, float alpha, bool linear) {
	vec3 water = mix(decode_colour(WATER_SHALLOW, linear), floor_colour, WATER_BED_SHOW);
	return mix(floor_colour, water, alpha);
}
