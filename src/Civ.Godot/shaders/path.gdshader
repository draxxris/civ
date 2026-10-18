shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

uniform vec4 path_colour : source_color = vec4(0.4, 1.0, 0.5, 0.95);

varying vec2 map_position;

float path_hash(vec2 p) {
	vec3 h = fract(vec3(p.xyx) * 0.1031);
	h += dot(h, h.yzx + 33.33);
	return fract((h.x + h.y) * h.z);
}

float path_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(path_hash(cell), path_hash(cell + vec2(1.0, 0.0)), f.x),
		mix(path_hash(cell + vec2(0.0, 1.0)), path_hash(cell + vec2(1.0)), f.x), f.y);
}

void vertex() {
	// Mesh coordinates keep the texture fixed as the camera moves and identical on wrapped copies.
	map_position = VERTEX.xz;
}

void fragment() {
	float waviness = path_noise(map_position * 5.0);
	float wear = path_noise(map_position * 28.0);
	float grain = path_noise(map_position * 75.0);
	vec2 uneven_uv = UV + vec2((waviness - 0.5) * 0.08, 0.0);
	float radius = length(uneven_uv) * mix(0.85, 1.25, wear);
	float core = exp(-30.0 * radius * radius);
	float halo = exp(-5.0 * radius * radius);
	float edge = 1.0 - smoothstep(0.6, 1.0, length(UV));
	float texture_opacity = mix(0.6, 1.0, grain) * mix(0.75, 1.0, wear);

	ALBEDO = mix(path_colour.rgb, vec3(1.0), 0.45 * core);
	ALPHA = path_colour.a * edge * (0.55 * halo + 0.45 * core) * texture_opacity;
}
