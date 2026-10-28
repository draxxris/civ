// City buildings (CityRenderer.cs). Each instance is a unit part (box, roof, cone, ...) scaled to its size.
// Instance colour: rgb = albedo times fog, a = fog. Instance custom: xyz = part size in world units, w = facade kind:
// 0 plain, 1 timber framing, 2 sash windows (Victorian brick), 3 ribbon windows (modern block), 4 glass curtain wall.
// Windows are counted down from the top of the part, one row per storey, so a part sunk into a slope loses only its
// lowest row.
shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_schlick_ggx;

// CityLayout.BuildingScale: window and beam spacing grow with the buildings.
uniform float building_scale = 1.0;

varying vec3 local_pos;
varying vec3 local_normal;
varying flat vec4 part;

void vertex() {
	part = INSTANCE_CUSTOM;
	local_pos = VERTEX * part.xyz;
	local_normal = NORMAL;
}

// 1 inside [lo, hi] of a repeating cell, softened by the pixel footprint so far views do not shimmer.
float band(float t, float lo, float hi) {
	float f = fract(t);
	float w = max(fwidth(t), 1e-4);
	return smoothstep(lo - w, lo + w, f) * (1.0 - smoothstep(hi - w, hi + w, f));
}

void fragment() {
	vec3 albedo = COLOR.rgb;
	float fog = COLOR.a;
	int kind = int(part.w + 0.5);
	bool wall = abs(local_normal.y) < 0.5;
	float rough = 0.85;
	float metal = 0.0;
	if (wall && kind > 0) {
		float u = abs(local_normal.x) > 0.5 ? local_pos.z : local_pos.x;
		float down = part.y - local_pos.y;
		if (kind == 1) {
			// Dark oak beams: posts, a rail at every storey and one brace pattern.
			float posts = band(u / (0.022 * building_scale), 0.0, 0.16);
			float rails = band(down / (0.04 * building_scale), 0.0, 0.12);
			albedo = mix(albedo, vec3(0.2, 0.13, 0.08) * fog, max(posts, rails));
		} else if (kind == 2) {
			float rows = band(down / (0.036 * building_scale), 0.3, 0.72);
			float cols = band(u / (0.024 * building_scale), 0.3, 0.7);
			float sill = band(down / (0.036 * building_scale), 0.72, 0.8) * band(u / (0.024 * building_scale), 0.25, 0.75);
			albedo = mix(albedo, vec3(0.88, 0.85, 0.78) * fog, sill);
			albedo = mix(albedo, vec3(0.1, 0.12, 0.15) * fog, rows * cols);
			rough = mix(rough, 0.3, rows * cols);
		} else if (kind == 3) {
			float rows = band(down / (0.03 * building_scale), 0.22, 0.72);
			float mullions = 1.0 - band(u / (0.018 * building_scale), 0.0, 0.08);
			float glass = rows * mullions;
			albedo = mix(albedo, vec3(0.2, 0.27, 0.34) * fog, glass);
			rough = mix(rough, 0.15, glass);
			metal = 0.3 * glass;
		} else if (kind == 4) {
			float floors = band(down / (0.03 * building_scale), 0.0, 0.1);
			float mullions = band(u / (0.016 * building_scale), 0.0, 0.08);
			float frame = max(floors, mullions);
			albedo = mix(albedo, albedo * 0.55 + vec3(0.12) * fog, frame);
			rough = mix(0.12, 0.6, frame);
			metal = 0.45 * (1.0 - frame);
		}
	} else if (!wall && kind >= 3 && local_normal.y > 0.5) {
		// Flat roofs of modern buildings: grey membrane, not glass.
		albedo = vec3(0.36, 0.36, 0.38) * fog;
	}
	ALBEDO = albedo;
	ROUGHNESS = rough;
	METALLIC = metal;
}
