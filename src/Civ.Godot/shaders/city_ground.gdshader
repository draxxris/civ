// The ground under a city (CityRenderer.Ground.cs): 0 dark-brown dirt, 1 cobblestone, 2 concrete. The mesh is draped
// on the height field; vertex colour is the fog shade and alpha the feathered rim. The patterns are procedural in map
// space, so every wrap copy shows the same ground, and fade to their mean colour where they get smaller than a pixel.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_lambert, specular_disabled;

uniform int ground_kind = 0;
// Pull each vertex this fraction of its view distance toward the camera, as ground_decal does, so the patch wins
// against the terrain where the coarse terrain lattice bulges above the sampled field.
uniform float depth_pull = 0.0025;

varying vec2 ground_pos;

vec2 hash2(vec2 p) {
	p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
	return fract(sin(p) * 43758.5453);
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// Distance to the nearest cell border (F2 - F1) and the id of the nearest cell.
vec3 voronoi(vec2 p) {
	vec2 cell = floor(p);
	float d1 = 8.0, d2 = 8.0;
	vec2 id = vec2(0.0);
	for (int j = -1; j <= 1; j++)
		for (int i = -1; i <= 1; i++) {
			vec2 c = cell + vec2(float(i), float(j));
			vec2 q = c + 0.15 + 0.7 * hash2(c);
			float d = length(p - q);
			if (d < d1) { d2 = d1; d1 = d; id = c; }
			else if (d < d2) { d2 = d; }
		}
	return vec3(d2 - d1, id);
}

// Loose straw lying flat: in each cell a few thin strands at random angles. Returns the straw colour and how much of
// the ground it covers, which is about density. Far away the strands blend into their mean cover.
vec4 straw(vec2 p, float density) {
	vec2 q = p * 70.0;
	float w = max(fwidth(q.x), 1e-4);
	float hit = 0.0;
	float tone = 0.0;
	vec2 cell = floor(q);
	for (int j = -1; j <= 1; j++)
		for (int i = -1; i <= 1; i++)
			for (int k = 0; k < 3; k++) {
				vec2 c = cell + vec2(float(i), float(j));
				vec2 h = hash2(c + float(k) * 13.7);
				if (hash(c + float(k) * 5.1) > density) continue;
				vec2 centre = c + h;
				float a = hash(c * 1.7 + float(k)) * 6.2832;
				vec2 dir = vec2(cos(a), sin(a));
				vec2 d = q - centre;
				float along = dot(d, dir), across = abs(dot(d, vec2(-dir.y, dir.x)));
				float len = 0.6 + 0.5 * h.x;
				float m = (1.0 - smoothstep(0.07, 0.07 + w, across)) * (1.0 - smoothstep(len - w, len, abs(along)));
				if (m > hit) { hit = m; tone = h.y; }
			}
	// Mean cover of the strands for distant views, where single strands are smaller than a pixel.
	float near = clamp(1.5 - w * 3.0, 0.0, 1.0);
	hit = mix(density * 0.8, hit, near);
	tone = mix(0.5, tone, near);
	vec3 colour = mix(vec3(0.62, 0.5, 0.26), vec3(0.86, 0.74, 0.44), tone);
	return vec4(colour, hit);
}

void vertex() {
	ground_pos = VERTEX.xz;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz *= 1.0 - depth_pull;
	POSITION = PROJECTION_MATRIX * view;
}

void fragment() {
	vec2 p = ground_pos;
	float n = 0.6 * value_noise(p * 7.0) + 0.4 * value_noise(p * 31.0);
	vec3 albedo;
	float strength = 0.92;
	if (ground_kind == 0) {
		// Mud under strewn straw, as at a fairground: dark wet earth, mostly covered by loose straw that thins
		// out in places so the mud shows through.
		vec3 mud = mix(vec3(0.24, 0.16, 0.1), vec3(0.36, 0.26, 0.16), smoothstep(0.2, 0.8, 0.6 * value_noise(p * 2.5) + 0.4 * n));
		float cover = smoothstep(0.3, 0.6, value_noise(p * 4.0 + 17.0) * 0.75 + n * 0.25);
		vec4 st = straw(p, 0.55 + 0.4 * cover);
		albedo = mix(mud, st.rgb, st.a);
	} else if (ground_kind == 3) {
		// Thick straw at the foot of a building.
		vec4 st = straw(p, 0.95);
		albedo = mix(vec3(0.42, 0.33, 0.18), st.rgb, st.a);
		strength = 0.95;
	} else if (ground_kind == 1) {
		// Rounded setts in dark mortar.
		vec2 q = p * 26.0;
		vec3 v = voronoi(q);
		float w = max(fwidth(q.x), 1e-4);
		float mortar = 1.0 - smoothstep(0.05, 0.05 + 2.0 * w, v.x);
		float shade = mix(0.85, 1.12, hash(v.yz));
		vec3 stone = vec3(0.64, 0.6, 0.55) * shade * mix(0.92, 1.05, n);
		float detail = clamp(1.6 - w * 2.0, 0.0, 1.0);
		vec3 pattern = mix(stone, vec3(0.36, 0.33, 0.29), mortar);
		albedo = mix(vec3(0.58, 0.54, 0.5) * mix(0.92, 1.05, n), pattern, detail);
		// Rounded tops catch a little light at their centres.
		albedo *= 1.0 + 0.12 * smoothstep(0.1, 0.5, v.x) * detail;
		strength = 0.95;
	} else {
		// Poured concrete slabs with thin seams and weathered stains.
		vec2 q = p * 14.0;
		vec2 f = abs(fract(q) - 0.5);
		float w = max(fwidth(q.x), 1e-4);
		float seam = smoothstep(0.47, 0.47 + w * 1.5, max(f.x, f.y));
		float slab = mix(0.92, 1.06, hash(floor(q)));
		albedo = vec3(0.58, 0.58, 0.56) * slab * mix(0.88, 1.06, n);
		albedo *= mix(0.85, 1.0, value_noise(p * 4.0));
		albedo = mix(albedo, vec3(0.32, 0.32, 0.31), seam * clamp(1.0 - w * 4.0, 0.0, 1.0));
		strength = 0.97;
	}
	float edge = COLOR.a;
	float alpha = edge * strength * smoothstep(0.0, 0.3, edge + 0.25 * (n - 0.5));
	ALBEDO = albedo * COLOR.rgb;
	ALPHA = clamp(alpha, 0.0, 1.0);
	ROUGHNESS = ground_kind == 2 ? 0.8 : 0.95;
}
