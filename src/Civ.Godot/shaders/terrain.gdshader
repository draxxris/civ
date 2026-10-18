// Lit terrain chunks. Albedo/height textures blend across the height field's corner triangle, then the containing hex
// adds the work-tile overlay, owner borders and the hover outline. Rivers are separate draped surfaces (river.gdshader)
// over the channels the height field carves. Unexplored tiles become drifting clouds.
shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_schlick_ggx;

#include "res://shaders/terrain_common.gdshaderinc"

// 0 = normal; 1 = sharpened blend weights as RGB; 2 = hidden weight; 3 = blended colour without lines or clouds;
// 4 = white ground (lighting and relief only); 5 = height bands with contour lines every 0.05; 6 = slope (1 - normal.y); 7 = the mesh lattice
// (triangle edges of the default 9-step lattice) over a white ground; 8 = cavity (blue valleys, red ridges, full colour at ±4).
uniform int debug_mode = 0;
uniform float lattice_steps = 9.0;
// Snow caps: altitude of the snow line and half width of its transition. Only mountain summits rise above the line;
// lowland snow comes from tile terrain instead. Snow sticks to faces flatter than snow_shed (normal.y, after rock
// relief) and slides off steeper ones, so the cap breaks up into ledges and gullies as it fades out.
uniform float snow_line = 0.95;
uniform float snow_fade = 0.22;
uniform float snow_shed = 0.62;

// Mountain relief shading from the lattice cavity (UV2.x): gullies darken and ridges catch light, like ambient occlusion.
// It applies only above the hills, so river channels and lowland folds keep their colour.
uniform float cavity_shade = 0.3;
uniform float ridge_light = 0.12;
// Snow reaches further down gullies (positive cavity) than over open faces, as height added to the snow test.
uniform float gully_snow = 0.18;
// Mountain rock relief: the rock texture height bumps the normal (world units of relief per unit texture height).
uniform float rock_bump = 0.03;
// Mountains get a harder light than the lowlands: faces turned from the sun darken toward a cool shadow, faces toward it
// brighten a little. sun_direction points toward the scene's directional light.
uniform vec3 sun_direction = vec3(-0.542, 0.643, -0.542);
uniform float mountain_shadow = 0.36;
uniform float mountain_light = 0.12;

varying vec3 world_pos;
varying vec3 ground_normal;
varying float cavity;

// Snow cover in [0, 1]: altitude, wrap-safe drift noise and the gully bonus set how much snow can lie here; the face's
// steepness then decides how much of it stays. Inside the transition both terms blend smoothly, so the cap thins out over
// the rock instead of ending in a line.
float snow_cover(vec2 p, float height, float flatness, float hollow) {
	const float drift_height = 0.3;
	// Noise lies in [-0.5, 0.5], so lowlands cannot reach the snow line even at its lowest point.
	height += gully_snow * hollow;
	if (height <= snow_line - snow_fade - 0.5 * drift_height) return 0.0;
	float drift = periodic_fbm(p, 1.1) - 0.5;
	float patch = periodic_fbm(p, 5.0) - 0.5;
	float altitude = (height + drift * drift_height - snow_line) / snow_fade;
	float hold = smoothstep(snow_shed - 0.3, snow_shed + 0.12, flatness + patch * 0.25);
	// High up, snow clings even to steep faces; near the line only ledges and hollows keep it.
	float cover = clamp(0.5 + 0.5 * altitude, 0.0, 1.0);
	return smoothstep(0.0, 1.0, cover * mix(hold, 1.0, cover * cover * 0.6) + patch * 0.15 * cover);
}

// Height of the rock texture's first layer at p, the relief that bumps mountain normals.
float rock_relief(vec2 p) {
	return texture(terrain_textures, vec3(terrain_uv(p, uv_freq), float(cliff_layer))).a;
}

// Bump the normal of the height field by rock relief of the given depth (world units): recover the field's slope from
// the normal, add the relief's slope from finite differences over world xz, and renormalise.
vec3 bump_normal(vec3 n, vec2 p, float depth) {
	const float e = 0.012;
	float h = rock_relief(p);
	vec2 relief = vec2(rock_relief(p + vec2(e, 0.0)) - h, rock_relief(p + vec2(0.0, e)) - h) * (depth / e);
	vec2 field = -n.xz / max(n.y, 0.05);
	return normalize(vec3(-(field.x + relief.x), 1.0, -(field.y + relief.y)));
}

void vertex() {
	// Chunk vertices are canonical world positions; wrap copies only translate the instance.
	world_pos = VERTEX;
	ground_normal = NORMAL;
	cavity = UV2.x;
}

void fragment() {
	vec2 p = vec2(wrap_world_x(world_pos.x), world_pos.z);
	vec3 land;
	vec4 water_tint;
	float fogged, hidden;
	vec3 w;
	bool linear = !OUTPUT_IS_SRGB;
	vec3 normal = normalize(ground_normal);
	float steep = 1.0 - normal.y;
	float slope = smoothstep(0.08, 0.40, steep);
	slope *= smoothstep(-0.01, 0.04, world_pos.y);
	float mountain = smoothstep(0.42, 0.72, world_pos.y);
	if (mountain > 0.0 && debug_mode != 7) {
		normal = bump_normal(normal, world_pos.xz, rock_bump * mountain);
		NORMAL = normalize((VIEW_MATRIX * vec4(normal, 0.0)).xyz);
	}
	float hollow = clamp(cavity / 4.0, -1.0, 1.0) * mountain;
	float snow = snow_cover(world_pos.xz, world_pos.y, normal.y, max(hollow, 0.0));
	// Keep sample coordinates unwrapped to prevent a mip-derivative jump at the cylinder seam.
	blend_tiles(world_pos.xz, linear, slope, snow, land, water_tint, fogged, hidden, w);
	vec3 colour = land * (1.0 - cavity_shade * smoothstep(0.0, 1.0, hollow) + ridge_light * smoothstep(0.0, 1.0, -hollow));
	float sun = smoothstep(-0.25, 0.75, dot(normal, sun_direction));
	vec3 shade = mix(vec3(1.0 - mountain_shadow) * vec3(0.94, 0.97, 1.04), vec3(1.0 + mountain_light), sun);
	// Bare rock is warm brown low down and cooler grey high up. Snow stays bright: its shadows are soft and blue.
	vec3 rock_tint = mix(vec3(1.08, 0.97, 0.84), vec3(1.0, 0.98, 0.95), smoothstep(0.5, 1.1, world_pos.y));
	vec3 snow_shade = mix(vec3(0.84, 0.89, 0.97), vec3(1.15), sun);
	colour *= mix(vec3(1.0), mix(shade * rock_tint, snow_shade, snow), mountain);
	vec4 sky = clouds(world_pos.xz, hidden, TIME, linear);

	ivec2 hex = containing_hex(p);
	bool inside = on_map(hex);
	int sight = 0;
	bool hover = false;
	if (inside) {
		vec4 b = texelFetch(tile_b, texel_of(hex), 0);
		sight = byte_of(b.r);
		hover = b.b > 0.5;
	}
	vec2 d = p - hex_centre(hex);
	if (debug_mode == 0 && sight > 0) colour = map_overlays(p, colour, linear);

	float cover = debug_mode == 0 ? sky.a : 0.0;
	if (debug_mode == 1) colour = w;
	else if (debug_mode == 2) colour = vec3(hidden);
	else if (debug_mode == 4) colour = vec3(0.8);
	else if (debug_mode == 5) {
		float band = fract(world_pos.y / 0.05);
		float contour = 1.0 - smoothstep(0.0, 0.07, min(band, 1.0 - band));
		colour = mix(mix(vec3(0.05, 0.25, 0.85), vec3(0.95, 0.2, 0.1), clamp(world_pos.y / 0.9, 0.0, 1.0)), vec3(0.0), contour * 0.85);
	}
	else if (debug_mode == 7) {
		// Triangular lattice: spacing sqrt(3)/k along x, 1.5/k along z; lines are f = integer for three skewed coordinates.
		float s = 1.7320508 / lattice_steps;
		vec3 f = vec3(p.x / s - p.y / (s * 1.7320508), p.x / s + p.y / (s * 1.7320508), p.y * lattice_steps / 1.5);
		vec3 d = abs(fract(f + 0.5) - 0.5) / max(fwidth(f), vec3(1e-5));
		colour = mix(vec3(0.0), vec3(0.85), clamp(min(d.x, min(d.y, d.z)), 0.0, 1.0));
	}
	else if (debug_mode == 8) colour = mix(vec3(0.85), cavity > 0.0 ? vec3(0.1, 0.3, 0.95) : vec3(0.95, 0.15, 0.1), clamp(abs(cavity) / 4.0, 0.0, 1.0));
	else if (debug_mode == 6) colour = mix(vec3(0.05, 0.6, 0.15), vec3(0.95, 0.1, 0.05), smoothstep(0.0, 0.4, steep));

	vec3 emission = sky.rgb * cover;
	if (hover) {
		float outline = hover_outline(d);
		emission = mix(emission, vec3(0.9), outline);
		cover = max(cover, outline);
	}
	ALBEDO = colour * (1.0 - cover);
	EMISSION = emission;
	ROUGHNESS = 0.92;
	SPECULAR = 0.15 * (1.0 - cover);
}
