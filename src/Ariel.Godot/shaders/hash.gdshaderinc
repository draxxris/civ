// Integer lattice hash in [0, 1), shared by every shader that needs per-cell random values. Do not use fract(sin(x)):
// at large cell coordinates its result depends on how the compiler rounds the argument, so the same cell can hash
// differently in two expressions (seams in noise, banding in glare). The seed picks an independent stream.
float lattice_hash(ivec2 cell, uint seed) {
	uvec2 q = uvec2(cell) * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y ^ seed) * 1597334673u;
	n ^= n >> 16u;
	n *= 2246822519u;
	return float(n >> 8u) * (1.0 / 16777216.0);
}
