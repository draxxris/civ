# Water

`water_normal.png` is the river surface's normal map: red and green hold the surface
normal's x and z, blue a foam pattern (bright where foam gathers), alpha is unused. It is
`water1_normal_bump.png` from Waterways by Kasper Arnklit Frandsen
(https://github.com/Arnklit/Waterways), under the MIT License in `LICENSE.txt`.

`shaders/water_surface.gdshaderinc` samples it twice, at two scales, each drifted in two
cross-faded phases: downstream along the flow on rivers (`river.gdshader`), with the wind on
the sea and lakes (`water.gdshader`). `TerrainRenderer` loads it as a plain file, so the
project needs no import step for it.
