// Surf coverage in [0, 1] beside a cliff, for the sea (water.gdshader) and for rivers (river.gdshader). The cliff share
// (TerrainField.CliffAt: 1 at the cliff edge, where the rock columns stand, falling to 0 about 0.42 world units out)
// stands in for the distance to the rock; churn_weight scales the white water against it and streak_weight the foam
// lines drawn out from it. Needs seeded_noise from terrain_common.gdshaderinc.
float cliff_surf(vec2 p, float share, float time, float churn_weight, float streak_weight) {
	if (share <= 0.01) return 0.0;
	float lace = seeded_noise(p + vec2(time * 0.07, -time * 0.05), 9.0, 16u) * 0.6
		+ seeded_noise(p + vec2(-time * 0.05, time * 0.08), 19.0, 17u) * 0.4;
	float fine = seeded_noise(p + vec2(time * 0.11, time * 0.04), 34.0, 18u);
	// White water against the rock: solid at the face, torn into lace and holes as it spreads out.
	float churn = smoothstep(0.8, 0.97, share + (lace - 0.5) * 0.3);
	churn *= mix(0.45, 1.0, smoothstep(0.3, 0.7, lace * 0.7 + fine * 0.3)) * mix(0.75, 1.0, smoothstep(0.92, 1.0, share));
	// Foam lines move outward from the face and break up into lace.
	float swash = 0.5 + 0.5 * sin((1.0 - share) * 24.0 - time * 1.4 + lace * 4.0);
	float streaks = smoothstep(0.6, 1.0, swash) * smoothstep(0.05, 0.6, share) * smoothstep(0.42, 0.7, lace) * (0.6 + 0.4 * fine);
	return clamp(churn * churn_weight + streaks * streak_weight * (1.0 - churn), 0.0, 1.0);
}
