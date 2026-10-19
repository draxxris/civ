# Unit art conversion report

## Run

- Registry: `src/Mods/Military/data/unit.json` (359 units in this run).
- Source: `/home/gordon/workspace/civ/AND-extraction`.
- Converted: **357**. Failed: **2**.
- Animated: **351**. Skinned: **333**. Textured: **355**.
- Output: `src/Mods/Military/art/models/<unit-id-suffix>.glb`.
- Total GLB size: 122,712,332 bytes.
- The report itself is the art registry. Each unit section identifies its source and output status.

## Reproduction

```sh
python3 tools/art/convert_units.py
# Three-unit spike:
python3 tools/art/convert_units.py --units warrior archer axeman
```

The script uses Python 3.11, PyFFI 2.2.3, NumPy 2.4.6, and Pillow 12.3.0.
If dependencies are absent, uv creates the pinned environment. The first run needs network access.
The converter reads active core files with whole-file layer fallback and the default nested MLF module profile.
The importer ID map resolves source symbols when content IDs differ from source symbols.

## Conversion contract

- NIF node hierarchies remain intact. Skins use source bone offsets, weights, and joint transforms.
- Triangle strips become indexed triangles. Missing normals come from triangle geometry.
- DDS diffuse textures become embedded RGBA PNG images. No GLB references an external file.
- The root rotates source Z-up coordinates into Y-up coordinates. Source -Y faces output +Z.
- The bind-pose bounds set feet at Y=0 and center the model horizontally.
- Height is at most 0.60 world units. Horizontal radius is at most 0.48 world units.
- Wide ships and vehicles can be shorter than 0.45 units to fit the hex.
- Source fScale values appear in this report. Per-model bounds replace these source scene scale values.
- KFM filenames select KF sequences. Output names are idle, run, fortify, attack, hurt, and die.
- Missing source clips remain absent. No procedural or copied animation replaces a missing source clip.
- Transform keys preserve source timing. Quaternion keys use normalized, shortest-path rotations.
- Compressed and uncompressed cubic B-spline controls evaluate at 30 samples per second.
- Quadratic vector keys evaluate with Hermite interpolation. TBC and quaternion curves use linear segments.

## Limits

The converter exports the first early-era art group, not the full multi-figure formation or civilization variants.
Material, particle, sound, and effect-controller animations do not become glTF transform tracks.
Visibility controllers become STEP scale channels on inserted _Vis nodes and skinned-mesh proxy joints. Hidden subtrees scale to 1e-6.
Hidden geometry does not export. Runtime projectiles and source shaders need separate renderer support.
Team-color decal textures composite over a neutral blue-gray background. This conversion does not implement dynamic team colors.
Zero-scale nodes use a scale of 0.000001 because Godot cannot decompose singular bases.
The report lists missing assets and unsupported bindings. Failed units do not receive substitute models.
A source clip without matching transform targets does not count as an exported animation.

## Per-unit registry

### military:007

- Model: `src/Mods/Military/art/models/007.glb` (484,940 bytes).
- Art: `ART_DEF_UNIT_007`. Source scale: 0.46.
- NIF: `art/units/007/007.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 739 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2761, feet Y=0.
- Clips: idle (65 channels), run (65 channels), fortify (65 channels), attack (65 channels).
- SHA-256: `9715a752ca962446f8669762c584501bd60ef6b03875f9186c22f605d976ca6f`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:acv

- Model: `src/Mods/Military/art/models/acv.glb` (219,236 bytes).
- Art: `ART_DEF_UNIT_ACV`. Source scale: 0.43.
- NIF: `art/units/acv/acv.nif`.
- KFM: `art/units/mechanizedinfantry/mechanizedinfantry.kfm`.
- Geometry: 553 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.5493, radius 0.4800, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels).
- SHA-256: `1e202a9c19310b1e277d849557788966e42c2264865ec3c5916c41741ecba8c2`.
- idle: `art/units/mechanizedinfantry/mechanizedinfantry_md_idle.kf`.
- run: `art/units/mechanizedinfantry/mechanizedinfantry_md_run.kf`.
- fortify: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedfortify.kf`.
- attack: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedstrike.kf`.
- hurt: `art/units/mechanizedinfantry/mechanizedinfantry_md_hurta.kf`.

### military:acvsam

- Model: `src/Mods/Military/art/models/acvsam.glb` (289,896 bytes).
- Art: `ART_DEF_UNIT_ACVSAM`. Source scale: 0.48.
- NIF: `art/units/acv sam/acv_sam.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 614 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.5339, radius 0.4800, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels).
- SHA-256: `9128770eca9ba940420da5dae3220e7180321179171acd22ffcd9048ab1aee54`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:adventurer

- Model: `src/Mods/Military/art/models/adventurer.glb` (597,992 bytes).
- Art: `ART_DEF_UNIT_ADVENTURER`. Source scale: 0.42.
- NIF: `art/units/adventurer/persianimmortal.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 1150 triangles, 3 skins, 38 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2842, feet Y=0.
- Clips: idle (104 channels), run (104 channels), fortify (104 channels), attack (104 channels), hurt (104 channels), die (104 channels).
- SHA-256: `4dbe673019cad6d4e617f622c446bd0faa4fc0f4ac480e910a4cccd3d9107554`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:agent

- Model: `src/Mods/Military/art/models/agent.glb` (488,816 bytes).
- Art: `ART_DEF_UNIT_AGENT`. Source scale: 0.46.
- NIF: `art/units/agent/agent.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 823 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2694, feet Y=0.
- Clips: idle (65 channels), run (65 channels), fortify (65 channels), attack (65 channels).
- SHA-256: `67b66201386b55892d4ef38238be46a21f33ecf885d5b78b7e61c669c019d3fe`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:ah64_gunship

- Model: `src/Mods/Military/art/models/ah64_gunship.glb` (387,420 bytes).
- Art: `ART_DEF_UNIT_AH64_GUNSHIP`. Source scale: 0.36.
- NIF: `art/units/gunship/ah64/ah64.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 2109 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.2539, radius 0.4800, feet Y=0.
- Clips: idle (35 channels), run (35 channels), fortify (35 channels), attack (35 channels), hurt (35 channels).
- SHA-256: `552bfd330e37cdfcafda6eddb9e6d5a3e3a427ec786df4a7973abb292eab4478`.
- idle: `art/units/gunship/gunship_md_idle.kf`.
- run: `art/units/gunship/gunship_md_run.kf`.
- fortify: `art/units/gunship/gunship_md_rangedfortify.kf`.
- attack: `art/units/gunship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/gunship/gunship_md_hurta.kf`.

### military:airship

- Model: `src/Mods/Military/art/models/airship.glb` (246,348 bytes).
- Art: `ART_DEF_UNIT_AIRSHIP`. Source scale: 0.3.
- NIF: `art/units/airship/airship.nif`.
- KFM: `art/units/airship/airship.kfm`.
- Geometry: 904 triangles, 3 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.1889, radius 0.4800, feet Y=0.
- Clips: idle (53 channels), run (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `c4a03448f4cbf15a6462c3164987b4b76a671b29e1c16b4364b76f640a74ce5c`.
- idle: `art/units/airship/airship_md_idle.kf`.
- run: `art/units/airship/airship_md_run.kf`.
- hurt: `art/units/airship/airship_md_hurt.kf`.
- die: `art/units/airship/airship_md_die.kf`.

### military:albanian_light_guerilla_cavalry

- Model: `src/Mods/Military/art/models/albanian_light_guerilla_cavalry.glb` (604,600 bytes).
- Art: `ART_DEF_UNIT_KALORSE_KNIGHT`. Source scale: 0.42.
- NIF: `art/albania/kalorse/knight/knight.nif`.
- KFM: `art/albania/kalorse/knight/knight.kfm`.
- Geometry: 1143 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3002, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `a4657f0b66f2870c8a5c66d48d787941229d57fdbe23c8254c49bec4c9ab9089`.
- idle: `art/albania/kalorse/knight/knight_md_idle.kf`.
- run: `art/albania/kalorse/knight/knight_md_run.kf`.
- fortify: `art/albania/kalorse/knight/knight_md_fortifya.kf`.
- attack: `art/albania/kalorse/knight/knight_md_strikea.kf`.
- hurt: `art/albania/kalorse/knight/knight_md_hurta.kf`.
- die: `art/albania/kalorse/knight/knight_md_diea.kf`.
- Note: First of 3 formation art groups exported

### military:ambassador

- Model: `src/Mods/Military/art/models/ambassador.glb` (444,580 bytes).
- Art: `ART_DEF_UNIT_AMBASSADOR`. Source scale: 0.46.
- NIF: `art/units/ambassador/great_spy_modern_fx.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 590 triangles, 1 skins, 15 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2669, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels).
- SHA-256: `13dd1ed5169eaa5255b11e1998b3bd723a4a816c8ea2800b553344b07fed5b50`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:american_navy_seal

- Model: `src/Mods/Military/art/models/american_navy_seal.glb` (345,428 bytes).
- Art: `ART_DEF_UNIT_AMERICAN_NAVY_SEAL`. Source scale: 0.44.
- NIF: `art/units/unique/american navy seal/marines.nif`.
- KFM: `art/units/unique/american navy seal/marines.kfm`.
- Geometry: 798 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3356, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `78e001496ab15e9139c64b600c99f7602edce997f72675281fc8fc45be227741`.
- idle: `art/units/unique/american navy seal/marines_md_idle.kf`.
- run: `art/units/unique/american navy seal/marines_md_run.kf`.
- fortify: `art/units/unique/american navy seal/marines_md_rangedfortify.kf`.
- attack: `art/units/unique/american navy seal/marines_md_rangedstrike.kf`.
- hurt: `art/units/unique/american navy seal/marines_md_hurta.kf`.
- die: `art/units/unique/american navy seal/marines_md_rangeddiea.kf`.

### military:anglo_saxon_huscarl

- Model: `src/Mods/Military/art/models/anglo_saxon_huscarl.glb` (298,100 bytes).
- Art: `ART_DEF_UNIT_ANGLO_SAXON_HUSCARL`. Source scale: 0.44.
- NIF: `art/anglosaxons/huskarl/huscarl.nif`.
- KFM: `art/anglosaxons/huskarl/huscarl.kfm`.
- Geometry: 689 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3407, feet Y=0.
- Clips: idle (72 channels), run (72 channels), fortify (72 channels), attack (72 channels), hurt (72 channels), die (72 channels).
- SHA-256: `18b63953b51f56d825fd503b8761a9491481c80e6cdb7b93a043366829a5bc3a`.
- idle: `art/anglosaxons/huskarl/huscarl_md_idle.kf`.
- run: `art/anglosaxons/huskarl/huscarl_md_run.kf`.
- fortify: `art/anglosaxons/huskarl/huscarl_md_fortifya.kf`.
- attack: `art/anglosaxons/huskarl/huscarl_md_strikea.kf`.
- hurt: `art/anglosaxons/huskarl/huscarl_md_hurta.kf`.
- die: `art/anglosaxons/huskarl/huscarl_md_diea.kf`.

### military:arabia_camelarcher

- Model: `src/Mods/Military/art/models/arabia_camelarcher.glb` (672,236 bytes).
- Art: `ART_DEF_UNIT_ARABIA_CAMELARCHER`. Source scale: 0.39.
- NIF: `art/units/unique/arabia/camelarcher/arabiacamelarcher.nif`.
- KFM: `art/units/unique/arabia/camelarcher/arabiacamelarcher.kfm`.
- Geometry: 1250 triangles, 4 skins, 42 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2157, feet Y=0.
- Clips: idle (118 channels), run (118 channels), fortify (118 channels), attack (118 channels), hurt (118 channels), die (118 channels).
- SHA-256: `4ce91a4fb37cca21d00a14d0630245db5bd76a2d5369556e13c6b87f4dad9916`.
- idle: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_idle.kf`.
- run: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_run.kf`.
- fortify: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_fortifya.kf`.
- attack: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_strikea.kf`.
- hurt: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_hurta.kf`.
- die: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:archer

- Model: `src/Mods/Military/art/models/archer.glb` (367,084 bytes).
- Art: `ART_DEF_UNIT_ARCHER`. Source scale: 0.44.
- NIF: `art/units/archer/archer.nif`.
- KFM: `art/units/archer/archer.kfm`.
- Geometry: 756 triangles, 3 skins, 20 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2733, feet Y=0.
- Clips: idle (68 channels), run (68 channels), fortify (68 channels), attack (68 channels), hurt (68 channels), die (68 channels).
- SHA-256: `1ef0e5e50d3d3176381bc467ea2dc822b0901ea9beb2359f591781bd4464c99a`.
- idle: `art/units/archer/archer_md_idle.kf`.
- run: `art/units/archer/archer_md_run.kf`.
- fortify: `art/units/archer/archer_md_fortifya.kf`.
- attack: `art/units/archer/archer_md_strikea.kf`.
- hurt: `art/units/archer/archer_md_hurta.kf`.
- die: `art/units/archer/archer_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:armor_m60

- Model: `src/Mods/Military/art/models/armor_m60.glb` (219,428 bytes).
- Art: `ART_DEF_UNIT_ARMOR_M60`. Source scale: 0.4.
- NIF: `art/units/main battle tank/generic/m60.nif`.
- KFM: `art/units/main battle tank/generic/m60.kfm`.
- Geometry: 956 triangles, 11 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.2922, radius 0.4800, feet Y=0.
- Clips: idle (13 channels), run (13 channels), fortify (13 channels), attack (13 channels), hurt (13 channels).
- SHA-256: `b69240dca3c39676448ce4ecd82dd60dc7d2e4a66a8643a32a7f976e95481b08`.
- idle: `art/units/main battle tank/generic/m60_md_idle.kf`.
- run: `art/units/main battle tank/generic/m60_md_run.kf`.
- fortify: `art/units/main battle tank/generic/m60_md_rangedfortify.kf`.
- attack: `art/units/main battle tank/generic/m60_md_rangedstrike.kf`.
- hurt: `art/units/main battle tank/generic/m60_md_hurta.kf`.

### military:armored_car

- Model: `src/Mods/Military/art/models/armored_car.glb` (165,076 bytes).
- Art: `ART_DEF_UNIT_ARMORED_CAR`. Source scale: 0.40.
- NIF: `art/units/armored car/rollsroyce.nif`.
- KFM: `art/units/armored car/rollsroyce.kfm`.
- Geometry: 852 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.3519, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels).
- SHA-256: `94379cf2e8a021c7eb21da12e266266747c8b43ee2d4b5ab33da12a1caf50fb5`.
- idle: `art/units/armored car/rollsroyce_md_idle.kf`.
- run: `art/units/armored car/rollsroyce_md_run.kf`.
- fortify: `art/units/armored car/rollsroyce_md_rangedfortify.kf`.
- attack: `art/units/armored car/rollsroyce_md_rangedstrike.kf`.
- hurt: `art/units/armored car/rollsroyce_md_hurta.kf`.

### military:arquebusier

- Model: `src/Mods/Military/art/models/arquebusier.glb` (319,748 bytes).
- Art: `ART_DEF_UNIT_ARQUEBUSIER`. Source scale: 0.44.
- NIF: `art/units/musketman/musketman.nif`.
- KFM: `art/units/musketman/musketman.kfm`.
- Geometry: 715 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2782, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `2a448556ffa48807d2c3afad560bc9fd83cebaa339007c856b3a88f009d823e9`.
- idle: `art/units/musketman/musketman_md_idle.kf`.
- run: `art/units/musketman/musketman_md_run.kf`.
- fortify: `art/units/musketman/musketman_md_rangedfortify.kf`.
- attack: `art/units/musketman/musketman_md_rangedstrike.kf`.
- hurt: `art/units/musketman/musketman_md_hurta.kf`.
- die: `art/units/musketman/musketman_md_rangeddiea.kf`.

### military:artillery

- Model: `src/Mods/Military/art/models/artillery.glb` (180,060 bytes).
- Art: `ART_DEF_UNIT_ARTILLERY`. Source scale: 0.65.
- NIF: `art/units/artillery/artillery.nif`.
- KFM: `art/units/artillery/artillery.kfm`.
- Geometry: 478 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2534, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels).
- SHA-256: `8a3deeddd223148f80e9be882b8f36c20ef25b5d245188940088f03cfd0cbc0b`.
- idle: `art/units/artillery/artillery_md_idle.kf`.
- run: `art/units/artillery/artillery_md_run.kf`.
- fortify: `art/units/artillery/artillery_md_rangedfortify.kf`.
- attack: `art/units/artillery/artillery_md_rangedstrike.kf`.
- hurt: `art/units/artillery/artillery_md_hurta.kf`.

### military:artist

- Model: `src/Mods/Military/art/models/artist.glb` (207,052 bytes).
- Art: `ART_DEF_UNIT_ARTIST`. Source scale: 0.46.
- NIF: `art/units/greatpeople/artist/greatartist.nif`.
- KFM: `art/units/greatpeople/artist/greatartist.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2764, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels).
- SHA-256: `3f600df420eca0cc2ab4a8e055af737f4f7e2d962147678f3bb56a67e502cce7`.
- idle: `art/units/greatpeople/artist/greatartist_md_idle.kf`.
- run: `art/units/greatpeople/artist/greatartist_md_run.kf`.
- fortify: `art/units/greatpeople/artist/greatartist_md_fortify.kf`.

### military:asatru_missionary

- Model: `src/Mods/Military/art/models/asatru_missionary.glb` (200,788 bytes).
- Art: `ART_DEF_UNIT_ASATRU_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/gothi/missionary_general.nif`.
- KFM: `art/units/gothi/missionary_general.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2677, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), die (48 channels).
- SHA-256: `9072f8dfdac489dec96dd19206da1fe76acf91c1f422cd3c0b7832ff2dc26577`.
- idle: `art/units/gothi/missionary_general_md_idle.kf`.
- run: `art/units/gothi/missionary_general_md_run.kf`.
- fortify: `art/units/gothi/missionary_general_md_fortifya.kf`.
- die: `art/units/gothi/missionary_general_md_idle_die.kf`.

### military:assault_droid

- Model: `src/Mods/Military/art/models/assault_droid.glb` (458,276 bytes).
- Art: `ART_DEF_UNIT_ASSAULT_DROID`. Source scale: 0.8.
- NIF: `art/units/assault droid/mech.nif`.
- KFM: `art/units/assault droid/mech.kfm`.
- Geometry: 1700 triangles, 1 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.5752, radius 0.4800, feet Y=0.
- Clips: idle (93 channels), run (93 channels), fortify (93 channels), attack (93 channels), hurt (93 channels).
- SHA-256: `dcae31dc0e5fc24f10143f542608ca6aed9b02bcabb55a5661b93abe5c9f69ad`.
- idle: `art/units/assault droid/mech_md_idle.kf`.
- run: `art/units/assault droid/mech_md_run.kf`.
- fortify: `art/units/assault droid/mech_md_rangedfortify.kf`.
- attack: `art/units/assault droid/mech_md_rangedstrike.kf`.
- hurt: `art/units/assault droid/mech_md_rangedhurta.kf`.

### military:assault_mech

- Model: `src/Mods/Military/art/models/assault_mech.glb` (396,896 bytes).
- Art: `ART_DEF_UNIT_ASSAULT_MECH`. Source scale: 0.35.
- NIF: `art/units/assault_mech/nextwar_assaultmech.nif`.
- KFM: `art/units/assault_mech/nextwar_assaultmech.kfm`.
- Geometry: 833 triangles, 3 skins, 20 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2597, feet Y=0.
- Clips: idle (76 channels), run (76 channels), fortify (76 channels), attack (76 channels), die (76 channels).
- SHA-256: `21e8fef3059b50cf58c3bbbeecb610a9a54c25f89705c1136694ecf89be1de49`.
- idle: `art/units/assault_mech/nextwar_assaultmech_md_idle.kf`.
- run: `art/units/assault_mech/nextwar_assaultmech_md_run.kf`.
- fortify: `art/units/assault_mech/nextwar_assaultmech_md_rangedfortify.kf`.
- attack: `art/units/assault_mech/nextwar_assaultmech_md_rangedstrikea.kf`.
- die: `art/units/assault_mech/nextwar_assaultmech_md_rangeddiea.kf`.

### military:assyria_ram

- Model: `src/Mods/Military/art/models/assyria_ram.glb` (334,996 bytes).
- Art: `ART_DEF_UNIT_ASSYRIA_RAM`. Source scale: 0.70.
- NIF: `art/units/battering_ram/catapult.nif`.
- KFM: `art/units/siege ram/ram.kfm`.
- Geometry: 1452 triangles, 0 skins, 0 joint bindings, 3 textures.
- Bounds: height 0.4343, radius 0.4800, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels).
- SHA-256: `6dea33bd58da287c7c2d5b0cb7ddae7fd548549974cff469c3502c9f3d2f2c16`.
- idle: `art/units/siege ram/ram_md_idle.kf`.
- run: `art/units/siege ram/ram_md_run.kf`.
- fortify: `art/units/siege ram/ram_md_rangedfortify.kf`.
- attack: `art/units/siege ram/ram_md_rangedstrike.kf`.
- hurt: `art/units/siege ram/ram_md_hurta.kf`.
- Note: Unreadable diffuse texture: annual_rings.nif: cannot identify image file '/home/gordon/workspace/civ/AND-extraction/media/assets/art/units/battering_ram/annual_rings.nif'
- Note: Unreadable diffuse texture: rope.nif: cannot identify image file '/home/gordon/workspace/civ/AND-extraction/media/assets/art/units/battering_ram/rope.nif'

### military:assyrian_redu

- Model: `src/Mods/Military/art/models/assyrian_redu.glb` (313,792 bytes).
- Art: `ART_DEF_UNIT_ASSYRIAN_REDU`. Source scale: 0.44.
- NIF: `art/units/redu/spearman.nif`.
- KFM: `art/units/redu/spearman.kfm`.
- Geometry: 752 triangles, 1 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2904, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `1dfe35b29c7e17f11ae8096bd5ebac793cfa953c0ce741d4970222beaa179c87`.
- idle: `art/units/redu/spearman_md_idle.kf`.
- run: `art/units/redu/spearman_md_run.kf`.
- fortify: `art/units/redu/spearman_md_fortifya.kf`.
- attack: `art/units/redu/spearman_md_strikea.kf`.
- hurt: `art/units/redu/spearman_md_hurta.kf`.
- die: `art/units/redu/spearman_md_diea.kf`.

### military:at_infantry

- Model: `src/Mods/Military/art/models/at_infantry.glb` (437,016 bytes).
- Art: `ART_DEF_UNIT_AT_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/antitankinfantry/wwantitankinfantry.nif`.
- KFM: `art/units/antitankinfantry/wwantitankinfantry.kfm`.
- Geometry: 800 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3418, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), die (46 channels).
- SHA-256: `af3034360925a5cd3ef8329ffbfa9514b220ba8757c22abd77cca1ace77ec9f0`.
- idle: `art/units/antitankinfantry/wwantitankinfantry_md_idle.kf`.
- run: `art/units/antitankinfantry/wwantitankinfantry_md_run.kf`.
- fortify: `art/units/antitankinfantry/wwantitankinfantry_md_rangedfortify.kf`.
- attack: `art/units/antitankinfantry/wwantitankinfantry_md_rangedstrike.kf`.
- die: `art/units/antitankinfantry/wwantitankinfantry_md_rangeddiea.kf`.

### military:atomic

- Model: `src/Mods/Military/art/models/atomic.glb` (226,108 bytes).
- Art: `ART_DEF_UNIT_ATOMIC`. Source scale: 0.5.
- NIF: `art/units/a bomb/bomber.nif`.
- KFM: `art/units/a bomb/bomber.kfm`.
- Geometry: 1006 triangles, 4 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.1799, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `236952cd9bc7af4a56dba9032b1c1e7e791ccd606ebc230d43a3959a93fb810a`.
- idle: `art/units/a bomb/bomber_md_idle.kf`.
- run: `art/units/a bomb/bomber_md_run.kf`.
- hurt: `art/units/a bomb/bomber_md_hurt.kf`.
- die: `art/units/a bomb/bomber_md_die.kf`.

### military:attack_submarine

- Model: `src/Mods/Military/art/models/attack_submarine.glb` (186,856 bytes).
- Art: `ART_DEF_UNIT_ATTACK_SUBMARINE`. Source scale: 0.35.
- NIF: `art/units/nuclear_sub/nuclear_sub.nif`.
- KFM: `art/units/nuclear_sub/nuclear_sub.kfm`.
- Geometry: 305 triangles, 2 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.2504, radius 0.4800, feet Y=0.
- Clips: idle (32 channels), run (32 channels), fortify (32 channels), attack (32 channels), hurt (32 channels), die (32 channels).
- SHA-256: `3923291dd001deef77e65ab59ff96d8da5fc5cba469eba789794dab2ff4d6e61`.
- idle: `art/units/nuclear_sub/nuclear_sub_md_idle.kf`.
- run: `art/units/nuclear_sub/nuclear_sub_md_run.kf`.
- fortify: `art/units/nuclear_sub/nuclear_sub_md_fortifya.kf`.
- attack: `art/units/nuclear_sub/nuclear_sub_md_strikea.kf`.
- hurt: `art/units/nuclear_sub/nuclear_sub_md_hurta.kf`.
- die: `art/units/nuclear_sub/nuclear_sub_md_diea.kf`.

### military:automatons

- Model: `src/Mods/Military/art/models/automatons.glb` (299,900 bytes).
- Art: `ART_DEF_UNIT_AUTOMATON`. Source scale: 0.44.
- NIF: `art/units/nextwar_automaton/nextwar_automaton.nif`.
- KFM: `art/units/nextwar_automaton/nextwar_automaton.kfm`.
- Geometry: 920 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2848, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels), attack (59 channels), die (59 channels).
- SHA-256: `f8447ce1357935fb2aea3cac1e596d904120745e84f510fd06b3073989d606e5`.
- idle: `art/units/nextwar_automaton/nextwar_automaton_md_idle.kf`.
- run: `art/units/nextwar_automaton/nextwar_automaton_md_run.kf`.
- fortify: `art/units/nextwar_automaton/nextwar_automaton_md_rangedfortify.kf`.
- attack: `art/units/nextwar_automaton/nextwar_automaton_md_rangedstrike.kf`.
- die: `art/units/nextwar_automaton/nextwar_automaton_md_rangeddiea.kf`.
- Note: Hidden geometry omitted: Biped Object
- Note: Hidden geometry omitted: Biped Object@#0
- Note: Hidden geometry omitted: Biped Object@#1
- Note: Hidden geometry omitted: Biped Object@#2
- Note: Hidden geometry omitted: Biped Object@#4
- Note: Hidden geometry omitted: Biped Object@#5
- Note: Hidden geometry omitted: Biped Object@#6
- Note: Hidden geometry omitted: Biped Object@#7

### military:axeman

- Model: `src/Mods/Military/art/models/axeman.glb` (301,188 bytes).
- Art: `ART_DEF_UNIT_AXEMAN`. Source scale: 0.44.
- NIF: `art/units/axeman/axeman.nif`.
- KFM: `art/units/axeman/axeman.kfm`.
- Geometry: 681 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3483, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `6d704099180a6ff8f814dbb2cf5806713db2679b2abe70e04712aedf10125d6a`.
- idle: `art/units/axeman/axeman_md_idle.kf`.
- run: `art/units/axeman/axeman_md_run.kf`.
- fortify: `art/units/axeman/axeman_md_fortifya.kf`.
- attack: `art/units/axeman/axeman_md_strikea.kf`.
- hurt: `art/units/axeman/axeman_md_hurta.kf`.
- die: `art/units/axeman/axeman_md_diea.kf`.

### military:axeman_latvia

- Model: `src/Mods/Military/art/models/axeman_latvia.glb` (257,656 bytes).
- Art: `ART_DEF_UNIT_AXEMAN_LATVIA`. Source scale: 0.44.
- NIF: `art/latvia/axemanlatvia/axeman.nif`.
- KFM: `art/latvia/axemanlatvia/axeman.kfm`.
- Geometry: 663 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3237, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), die (46 channels).
- SHA-256: `a0d3486a0901fe320dafd54929518b34fb53d8a4dfe90d9411a53710ab4151f9`.
- idle: `art/latvia/axemanlatvia/axeman_md_idle.kf`.
- run: `art/latvia/axemanlatvia/axeman_md_run.kf`.
- fortify: `art/latvia/axemanlatvia/axeman_md_fortifya.kf`.
- attack: `art/latvia/axemanlatvia/axeman_md_strikea.kf`.
- die: `art/latvia/axemanlatvia/axeman_md_dieb.kf`.
- Note: Hidden geometry omitted: Lightswordsman_sword
- Note: Hidden geometry omitted: Warrior_Body

### military:aztec_jaguar

- Model: `src/Mods/Military/art/models/aztec_jaguar.glb` (363,636 bytes).
- Art: `ART_DEF_UNIT_AZTEC_JAGUAR`. Source scale: 0.44.
- NIF: `art/units/unique/aztec/jaguar/aztec_jaguar.nif`.
- KFM: `art/units/unique/aztec/jaguar/aztec_jaguar.kfm`.
- Geometry: 702 triangles, 2 skins, 29 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3538, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), attack (56 channels), hurt (56 channels), die (56 channels).
- SHA-256: `a0e21e96813f59a9c71068cfde06a72c280e7a3f237cd19e6acfad1a2cd57c87`.
- idle: `art/units/unique/aztec/jaguar/aztec_jaguar_md_idle.kf`.
- run: `art/units/unique/aztec/jaguar/aztec_jaguar_md_run.kf`.
- fortify: `art/units/unique/aztec/jaguar/aztec_jaguar_md_fortifya.kf`.
- attack: `art/units/unique/aztec/jaguar/aztec_jaguar_md_strikea.kf`.
- hurt: `art/units/unique/aztec/jaguar/aztec_jaguar_md_hurta.kf`.
- die: `art/units/unique/aztec/jaguar/aztec_jaguar_md_diea.kf`.

### military:b52

- Model: `src/Mods/Military/art/models/b52.glb` (171,128 bytes).
- Art: `ART_DEF_UNIT_B52`. Source scale: 0.5.
- NIF: `art/units/strategic bomber/b52.nif`.
- KFM: `art/units/stealthbomber/stealthbomber.kfm`.
- Geometry: 960 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.1848, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `3d70c7e4b0ccad81c39fd11d49ef0c4c495b2001996466692cc2a7cea5094543`.
- idle: `art/units/stealthbomber/stealthbomber_md_idle.kf`.
- run: `art/units/stealthbomber/stealthbomber_md_run.kf`.
- hurt: `art/units/stealthbomber/stealthbomber_md_hurt.kf`.
- die: `art/units/stealthbomber/stealthbomber_md_die.kf`.

### military:babylon_bowman

- Model: `src/Mods/Military/art/models/babylon_bowman.glb` (272,912 bytes).
- Art: `ART_DEF_UNIT_BABYLON_BOWMAN`. Source scale: 0.44.
- NIF: `art/units/unique_babylon_bowman/unique_babylon_bowman.nif`.
- KFM: `art/units/unique_babylon_bowman/unique_babylon_bowman.kfm`.
- Geometry: 712 triangles, 2 skins, 21 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2905, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), die (54 channels).
- SHA-256: `246d98fd5eda39ef66506b3394032396b39a86ff447dd27f718cf313646c268a`.
- idle: `art/units/unique_babylon_bowman/unique_babylon_bowman_md_idle.kf`.
- run: `art/units/unique_babylon_bowman/unique_babylon_bowman_md_run.kf`.
- fortify: `art/units/unique_babylon_bowman/unique_babylon_bowman_md_rangedfortify.kf`.
- attack: `art/units/unique_babylon_bowman/unique_babylon_bowman_md_rangedstrike.kf`.
- die: `art/units/unique_babylon_bowman/unique_babylon_bowman_md_rangeddiea.kf`.

### military:ballista

- Failed: ValueError: NIF hierarchy has multiple parents

### military:battlecruiser

- Failed: ValueError: Empty or non-finite accessor

### military:battleship

- Model: `src/Mods/Military/art/models/battleship.glb` (360,772 bytes).
- Art: `ART_DEF_UNIT_BATTLESHIP`. Source scale: 0.8.
- NIF: `art/units/battleship/battleship.nif`.
- KFM: `art/units/battleship/battleship.kfm`.
- Geometry: 641 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.2730, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `bc74538dd46ea42371a690c16d6e64f29fcd3927829e2a7204b6959d249f108b`.
- idle: `art/units/battleship/battleship_md_idle.kf`.
- run: `art/units/battleship/battleship_md_run.kf`.
- fortify: `art/units/battleship/battleship_md_fortifya.kf`.
- attack: `art/units/battleship/battleship_md_strikea.kf`.
- hurt: `art/units/battleship/battleship_md_hurta.kf`.
- die: `art/units/battleship/battleship_md_diea.kf`.

### military:bazooka

- Model: `src/Mods/Military/art/models/bazooka.glb` (386,168 bytes).
- Art: `ART_DEF_UNIT_BAZOOKA`. Source scale: 0.44.
- NIF: `art/units/bazooka/european/saminfantry.nif`.
- KFM: `art/units/bazooka/european/saminfantry.kfm`.
- Geometry: 754 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2850, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `8ba0eb33df90930563a8023c0f896cae9658b5c824ddd3337e6e505ac600ca23`.
- idle: `art/units/bazooka/european/saminfantry_md_idle.kf`.
- run: `art/units/bazooka/european/saminfantry_md_run.kf`.
- fortify: `art/units/bazooka/european/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/bazooka/european/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/bazooka/european/saminfantry_md_rangedhurta.kf`.
- die: `art/units/bazooka/european/saminfantry_md_rangeddiea.kf`.

### military:bear

- Model: `src/Mods/Military/art/models/bear.glb` (277,992 bytes).
- Art: `ART_DEF_UNIT_BEAR`. Source scale: 0.6.
- NIF: `art/units/bear/bear.nif`.
- KFM: `art/units/bear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5204, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `73875821c432e6a986f907dd1ed599adf7b12affa4bc27958167f9109c3d312a`.
- idle: `art/units/bear/bear_md_idle.kf`.
- run: `art/units/bear/bear_md_run.kf`.
- fortify: `art/units/bear/bear_md_fortifya.kf`.
- attack: `art/units/bear/bear_md_strikea.kf`.
- hurt: `art/units/bear/bear_md_hurta.kf`.
- die: `art/units/bear/bear_md_diea.kf`.

### military:bengaltiger

- Model: `src/Mods/Military/art/models/bengaltiger.glb` (350,548 bytes).
- Art: `ART_DEF_UNIT_BENGALTIGER`. Source scale: 0.6.
- NIF: `art/units/animals/bengaltiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `eca156539c1aa528ce21bf3d73f4e5ad74aa166c215b8305feae123c9da82dc8`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:bgmaster

- Model: `src/Mods/Military/art/models/bgmaster.glb` (228,332 bytes).
- Art: `ART_DEF_UNIT_BGMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `31a87664e94ce45c9616d264dae41d1d1cf24e8fc6aed84f4c3fff4dc8e092a1`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:biological_warfare_missile

- Model: `src/Mods/Military/art/models/biological_warfare_missile.glb` (81,748 bytes).
- Art: `ART_DEF_UNIT_BIOLOGICAL_WARFARE_MISSILE`. Source scale: 0.40.
- NIF: `art/units/bioweapon_missile/bioweapon_missile.nif`.
- KFM: `art/units/bioweapon_missile/bioweapon_missile.kfm`.
- Geometry: 146 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.1889, radius 0.4800, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `9970d96337c6f98b175e1bcc994d7fca0d6dc95bf9074f0a87d5f5d8512a236f`.
- idle: `art/units/bioweapon_missile/bioweapon_missile_md_idle.kf`.
- run: `art/units/bioweapon_missile/bioweapon_missile_md_run.kf`.
- attack: `art/units/bioweapon_missile/bioweapon_missile_md_strikea.kf`.

### military:bleeder

- Model: `src/Mods/Military/art/models/bleeder.glb` (403,552 bytes).
- Art: `ART_DEF_UNIT_CYBORG1`. Source scale: 0.44.
- NIF: `art/units/genetic soldier/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 1036 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2812, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `66d2b5e4232cf1bdb5ca2318f2f2de4ae2c7927eba5b9ab1fb11ec2d3ae75e01`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.
- Note: Missing texture: //cca.dds

### military:boar

- Model: `src/Mods/Military/art/models/boar.glb` (641,476 bytes).
- Art: `ART_DEF_UNIT_BOAR`. Source scale: 0.38.
- NIF: `art/units/boar/companion_cavalry.nif`.
- KFM: `art/units/knight_south_america/companion_cavalry.kfm`.
- Geometry: 948 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4759, feet Y=0.
- Clips: idle (108 channels), run (108 channels), fortify (108 channels), attack (108 channels), hurt (108 channels), die (108 channels).
- SHA-256: `78f4455cdcae1218cefd2d32979a40279407c8b53eecf2e25a8f93451635b430`.
- idle: `art/units/knight_south_america/companion_cavalry_md_idle.kf`.
- run: `art/units/knight_south_america/companion_cavalry_md_run.kf`.
- fortify: `art/units/knight_south_america/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/knight_south_america/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/knight_south_america/companion_cavalry_md_hurta.kf`.
- die: `art/units/knight_south_america/companion_cavalry_md_diea.kf`.
- Note: Editable Mesh: strongest four influences retained for 2 vertices
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: SuperSpray flame ity
- Note: Non-triangle effect geometry omitted: SS swirls01
- Note: Non-triangle effect geometry omitted: SS swirls02
- Note: Non-triangle effect geometry omitted: SS swirls03
- Note: Non-triangle effect geometry omitted: SS swirls04
- Note: Non-triangle effect geometry omitted: PCloud glow

### military:bombard

- Model: `src/Mods/Military/art/models/bombard.glb` (226,540 bytes).
- Art: `ART_DEF_UNIT_BOMBARD`. Source scale: 0.53.
- NIF: `art/units/bombard/bombardb.nif`.
- KFM: `art/units/bombard/bombardb.kfm`.
- Geometry: 992 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.3846, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `a73d96895a771feb420a641c87105b2cdd9f64bd0ad0a3a4c179f98412e505e7`.
- idle: `art/units/bombard/bombardb_md_idle.kf`.
- run: `art/units/bombard/bombardb_md_run.kf`.
- fortify: `art/units/bombard/bombardb_md_rangedfortify.kf`.
- attack: `art/units/bombard/bombardb_md_rangedstrike.kf`.
- hurt: `art/units/bombard/bombardb_md_hurta.kf`.

### military:bomber

- Model: `src/Mods/Military/art/models/bomber.glb` (275,164 bytes).
- Art: `ART_DEF_UNIT_BOMBER`. Source scale: 0.5.
- NIF: `art/units/bomber/bomber.nif`.
- KFM: `art/units/bomber/bomber.kfm`.
- Geometry: 1006 triangles, 4 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.1799, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `67e6cea1a39ac4dbd09856e3e8f89d7119990fdd1400f2ad970a4216f53b526e`.
- idle: `art/units/bomber/bomber_md_idle.kf`.
- run: `art/units/bomber/bomber_md_run.kf`.
- hurt: `art/units/bomber/bomber_md_hurt.kf`.
- die: `art/units/bomber/bomber_md_die.kf`.

### military:brigantine

- Model: `src/Mods/Military/art/models/brigantine.glb` (216,552 bytes).
- Art: `ART_DEF_UNIT_CARAVEL_SQUARERIG`. Source scale: 0.22.
- NIF: `art/units/brigantine/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 758 triangles, 3 skins, 13 joint bindings, 2 textures.
- Bounds: height 0.5865, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels), die (20 channels).
- SHA-256: `a8a965abf599b17873cf6fad1e4436649b944e1c90c3677dcc8fc04af52ab9e1`.
- idle: `art/units/caravel/caravel_md_idle.kf`.
- run: `art/units/caravel/caravel_md_run.kf`.
- fortify: `art/units/caravel/caravel_md_fortifya.kf`.
- attack: `art/units/caravel/caravel_md_strikea.kf`.
- hurt: `art/units/caravel/caravel_md_hurta.kf`.
- die: `art/units/caravel/caravel_md_diea.kf`.

### military:buddhist_missionary

- Model: `src/Mods/Military/art/models/buddhist_missionary.glb` (224,872 bytes).
- Art: `ART_DEF_UNIT_BUDDHIST_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/buddhist/buddhist_missionary.nif`.
- KFM: `art/units/missionary/buddhist/buddhist_missionary.kfm`.
- Geometry: 630 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2545, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), die (50 channels).
- SHA-256: `22cf0a541df251ca855e2b7665c572e31bda78053e2b5db8d0c5331bb6d8cac7`.
- idle: `art/units/missionary/buddhist/buddhist_missionary_md_idle.kf`.
- run: `art/units/missionary/buddhist/buddhist_missionary_md_run.kf`.
- fortify: `art/units/missionary/buddhist/buddhist_missionary_md_fortify.kf`.
- die: `art/units/missionary/buddhist/buddhist_missionary_md_idle_die.kf`.

### military:bulgarian_konnik

- Model: `src/Mods/Military/art/models/bulgarian_konnik.glb` (597,276 bytes).
- Art: `ART_DEF_UNIT_KONNIK`. Source scale: 0.42.
- NIF: `art/bulgaria/konnik/horseman_eurasian.nif`.
- KFM: `art/bulgaria/konnik/horseman_eurasian.kfm`.
- Geometry: 1183 triangles, 2 skins, 32 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3965, feet Y=0.
- Clips: idle (103 channels), run (103 channels), fortify (103 channels), attack (103 channels), hurt (103 channels), die (103 channels).
- SHA-256: `30aebdec65e1472d888b4126406d7ee978c2b0ca3b1eb0fc6019a3004170f657`.
- idle: `art/bulgaria/konnik/horseman_eurasian_md_idle.kf`.
- run: `art/bulgaria/konnik/horseman_eurasian_md_run.kf`.
- fortify: `art/bulgaria/konnik/horseman_eurasian_md_fortifya.kf`.
- attack: `art/bulgaria/konnik/horseman_eurasian_md_strikea.kf`.
- hurt: `art/bulgaria/konnik/horseman_eurasian_md_hurta.kf`.
- die: `art/bulgaria/konnik/horseman_eurasian_md_diea.kf`.

### military:bull

- Model: `src/Mods/Military/art/models/bull.glb` (88,220 bytes).
- Art: `ART_DEF_UNIT_BULL`. Source scale: 0.5.
- NIF: `art/units/bull/cow.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1182 triangles, 3 skins, 42 joint bindings, 0 textures.
- Bounds: height 0.5807, radius 0.4800, feet Y=0.
- Clips: idle (6 channels), run (6 channels), fortify (6 channels), attack (6 channels), hurt (6 channels), die (6 channels).
- SHA-256: `ea6dc199c3adf59e3758d9dd57b0c8b6b4df257e20957ef18786cb1dded47280`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.
- Note: Missing texture: bizon.dds
- Note: No diffuse texture resolved

### military:byzantine_cataphract

- Model: `src/Mods/Military/art/models/byzantine_cataphract.glb` (770,668 bytes).
- Art: `ART_DEF_UNIT_BYZANTINE_CATAPHRACT`. Source scale: 0.42.
- NIF: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract.nif`.
- KFM: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract.kfm`.
- Geometry: 1330 triangles, 3 skins, 47 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.4216, feet Y=0.
- Clips: idle (130 channels), run (130 channels), fortify (130 channels), attack (130 channels), hurt (130 channels), die (130 channels).
- SHA-256: `d6c1cf7872145b059a3510c814092ae7a4db36e20b65fa01a09637c3562ec2f4`.
- idle: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_idle.kf`.
- run: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_run.kf`.
- fortify: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_fortifya.kf`.
- attack: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_strikea.kf`.
- hurt: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_hurta.kf`.
- die: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_diea.kf`.

### military:cannon

- Model: `src/Mods/Military/art/models/cannon.glb` (363,272 bytes).
- Art: `ART_DEF_UNIT_CANNON`. Source scale: 0.53.
- NIF: `art/units/cannon/cannon.nif`.
- KFM: `art/units/cannon/cannon.kfm`.
- Geometry: 1168 triangles, 3 skins, 23 joint bindings, 4 textures.
- Bounds: height 0.5618, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels).
- SHA-256: `81675fb5f8423c8f7be7d4daad41e20cb7c1b7ecacfcf33a96103d7911cc315b`.
- idle: `art/units/cannon/cannon_md_idle.kf`.
- run: `art/units/cannon/cannon_md_run.kf`.
- fortify: `art/units/cannon/cannon_md_rangedfortify.kf`.
- attack: `art/units/cannon/cannon_md_rangedstrike.kf`.
- hurt: `art/units/cannon/cannon_md_hurta.kf`.

### military:captured_bear

- Model: `src/Mods/Military/art/models/captured_bear.glb` (277,992 bytes).
- Art: `ART_DEF_UNIT_BEAR`. Source scale: 0.6.
- NIF: `art/units/bear/bear.nif`.
- KFM: `art/units/bear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5204, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `73875821c432e6a986f907dd1ed599adf7b12affa4bc27958167f9109c3d312a`.
- idle: `art/units/bear/bear_md_idle.kf`.
- run: `art/units/bear/bear_md_run.kf`.
- fortify: `art/units/bear/bear_md_fortifya.kf`.
- attack: `art/units/bear/bear_md_strikea.kf`.
- hurt: `art/units/bear/bear_md_hurta.kf`.
- die: `art/units/bear/bear_md_diea.kf`.

### military:captured_bengaltiger

- Model: `src/Mods/Military/art/models/captured_bengaltiger.glb` (350,548 bytes).
- Art: `ART_DEF_UNIT_BENGALTIGER`. Source scale: 0.6.
- NIF: `art/units/animals/bengaltiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `eca156539c1aa528ce21bf3d73f4e5ad74aa166c215b8305feae123c9da82dc8`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_boar

- Model: `src/Mods/Military/art/models/captured_boar.glb` (641,476 bytes).
- Art: `ART_DEF_UNIT_BOAR`. Source scale: 0.38.
- NIF: `art/units/boar/companion_cavalry.nif`.
- KFM: `art/units/knight_south_america/companion_cavalry.kfm`.
- Geometry: 948 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4759, feet Y=0.
- Clips: idle (108 channels), run (108 channels), fortify (108 channels), attack (108 channels), hurt (108 channels), die (108 channels).
- SHA-256: `78f4455cdcae1218cefd2d32979a40279407c8b53eecf2e25a8f93451635b430`.
- idle: `art/units/knight_south_america/companion_cavalry_md_idle.kf`.
- run: `art/units/knight_south_america/companion_cavalry_md_run.kf`.
- fortify: `art/units/knight_south_america/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/knight_south_america/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/knight_south_america/companion_cavalry_md_hurta.kf`.
- die: `art/units/knight_south_america/companion_cavalry_md_diea.kf`.
- Note: Editable Mesh: strongest four influences retained for 2 vertices
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: SuperSpray flame ity
- Note: Non-triangle effect geometry omitted: SS swirls01
- Note: Non-triangle effect geometry omitted: SS swirls02
- Note: Non-triangle effect geometry omitted: SS swirls03
- Note: Non-triangle effect geometry omitted: SS swirls04
- Note: Non-triangle effect geometry omitted: PCloud glow

### military:captured_bull

- Model: `src/Mods/Military/art/models/captured_bull.glb` (88,220 bytes).
- Art: `ART_DEF_UNIT_BULL`. Source scale: 0.5.
- NIF: `art/units/bull/cow.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1182 triangles, 3 skins, 42 joint bindings, 0 textures.
- Bounds: height 0.5807, radius 0.4800, feet Y=0.
- Clips: idle (6 channels), run (6 channels), fortify (6 channels), attack (6 channels), hurt (6 channels), die (6 channels).
- SHA-256: `ea6dc199c3adf59e3758d9dd57b0c8b6b4df257e20957ef18786cb1dded47280`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.
- Note: Missing texture: bizon.dds
- Note: No diffuse texture resolved

### military:captured_cave_lion

- Model: `src/Mods/Military/art/models/captured_cave_lion.glb` (320,020 bytes).
- Art: `ART_DEF_UNIT_CAVELION`. Source scale: 0.75.
- NIF: `art/units/animals/cavelion/lion.nif`.
- KFM: `art/units/animals/cavelion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5755, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `798c299d9f2220ee774c8bc137e2507aa131d3664013786fffb0f9df13da365c`.
- idle: `art/units/animals/cavelion/lion_md_idle.kf`.
- run: `art/units/animals/cavelion/lion_md_run.kf`.
- fortify: `art/units/animals/cavelion/lion_md_fortifya.kf`.
- attack: `art/units/animals/cavelion/lion_md_strikea.kf`.
- hurt: `art/units/animals/cavelion/lion_md_hurta.kf`.
- die: `art/units/animals/cavelion/lion_md_diea.kf`.

### military:captured_cavebear

- Model: `src/Mods/Military/art/models/captured_cavebear.glb` (276,416 bytes).
- Art: `ART_DEF_UNIT_CAVEBEAR`. Source scale: 0.8.
- NIF: `art/units/animals/cavebear/bear.nif`.
- KFM: `art/units/animals/cavebear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5204, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `8c98c974431ed461f9823a7c0b847c195cd67799f498b70ff89b35f14ada2a8a`.
- idle: `art/units/animals/cavebear/bear_md_idle.kf`.
- run: `art/units/animals/cavebear/bear_md_run.kf`.
- fortify: `art/units/animals/cavebear/bear_md_fortifya.kf`.
- attack: `art/units/animals/cavebear/bear_md_strikea.kf`.
- hurt: `art/units/animals/cavebear/bear_md_hurta.kf`.
- die: `art/units/animals/cavebear/bear_md_diea.kf`.

### military:captured_cheetah

- Model: `src/Mods/Military/art/models/captured_cheetah.glb` (272,172 bytes).
- Art: `ART_DEF_UNIT_CHEETAH`. Source scale: 0.45.
- NIF: `art/units/animals/cheetah/panther.nif`.
- KFM: `art/units/animals/cheetah/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `41208697c8554714f4caf7f36c12090fffb6f1d59222db8c2f068476428a90b3`.
- idle: `art/units/animals/cheetah/panther_md_idle.kf`.
- run: `art/units/animals/cheetah/panther_md_run.kf`.
- fortify: `art/units/animals/cheetah/panther_md_fortifya.kf`.
- attack: `art/units/animals/cheetah/panther_md_strikea.kf`.
- hurt: `art/units/animals/cheetah/panther_md_hurta.kf`.
- die: `art/units/animals/cheetah/panther_md_diea.kf`.

### military:captured_direwolf

- Model: `src/Mods/Military/art/models/captured_direwolf.glb` (346,824 bytes).
- Art: `ART_DEF_UNIT_DIREWOLF`. Source scale: 0.55.
- NIF: `art/units/animals/direwolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5082, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `75e3e1b365254bd29af120f215344fe00734eb7b13984dffc712ba1999134627`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:captured_elephant

- Model: `src/Mods/Military/art/models/captured_elephant.glb` (583,232 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/elephant/elephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 630 triangles, 1 skins, 24 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4359, feet Y=0.
- Clips: idle (101 channels), run (101 channels), fortify (101 channels), attack (101 channels), hurt (101 channels), die (101 channels).
- SHA-256: `28dc62034cca9024aca0d5e64539cf62f09aa46af7991ca426eebcef5972d50b`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: Bone

### military:captured_lion

- Model: `src/Mods/Military/art/models/captured_lion.glb` (321,216 bytes).
- Art: `ART_DEF_UNIT_LION`. Source scale: 0.61.
- NIF: `art/units/lion/lion.nif`.
- KFM: `art/units/lion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5755, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `00e60cbdf9f7cacafabb5c8e63c37b74b76dac73c762076394b7309eecb0f805`.
- idle: `art/units/lion/lion_md_idle.kf`.
- run: `art/units/lion/lion_md_run.kf`.
- fortify: `art/units/lion/lion_md_fortifya.kf`.
- attack: `art/units/lion/lion_md_strikea.kf`.
- hurt: `art/units/lion/lion_md_hurta.kf`.
- die: `art/units/lion/lion_md_diea.kf`.

### military:captured_panther

- Model: `src/Mods/Military/art/models/captured_panther.glb` (269,824 bytes).
- Art: `ART_DEF_UNIT_PANTHER`. Source scale: 0.5.
- NIF: `art/units/panther/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `3693b9d0fc02ac5d23158ddd6db851231ea27e4b3442749073f61d23599ccff8`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_polarbear

- Model: `src/Mods/Military/art/models/captured_polarbear.glb` (275,432 bytes).
- Art: `ART_DEF_UNIT_POLARBEAR`. Source scale: 0.7.
- NIF: `art/units/animals/polarbear/bear.nif`.
- KFM: `art/units/animals/polarbear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5204, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `05be98ad73f9126252c543da6acc2862fee63987d43e61816750167bd32b54cd`.
- idle: `art/units/animals/polarbear/bear_md_idle.kf`.
- run: `art/units/animals/polarbear/bear_md_run.kf`.
- fortify: `art/units/animals/polarbear/bear_md_fortifya.kf`.
- attack: `art/units/animals/polarbear/bear_md_strikea.kf`.
- hurt: `art/units/animals/polarbear/bear_md_hurta.kf`.
- die: `art/units/animals/polarbear/bear_md_diea.kf`.

### military:captured_ram

- Model: `src/Mods/Military/art/models/captured_ram.glb` (446,792 bytes).
- Art: `ART_DEF_UNIT_WILD_RAM`. Source scale: 18.0.
- NIF: `art/units/wild_ram/mouflon.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 794 triangles, 2 skins, 33 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3082, feet Y=0.
- Clips: none.
- SHA-256: `fd8888fbe5e7f5d9dead6ebee25b320d9148c8653241c6333cb93720cc877c2c`.
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD2:0
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD2:1
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD1:0
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD1:1
- Note: idle: no matching transform tracks
- Note: run: no matching transform tracks
- Note: fortify: no matching transform tracks
- Note: attack: no matching transform tracks
- Note: hurt: no matching transform tracks
- Note: die: no matching transform tracks

### military:captured_siberiantiger

- Model: `src/Mods/Military/art/models/captured_siberiantiger.glb` (349,528 bytes).
- Art: `ART_DEF_UNIT_SIBERIANTIGER`. Source scale: 0.67.
- NIF: `art/units/animals/siberiantiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `2e8d6400f08685625714244763b72c724f4a0297bb890dddcea2c3fc0d611e58`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_stag

- Model: `src/Mods/Military/art/models/captured_stag.glb` (489,108 bytes).
- Art: `ART_DEF_UNIT_STAG`. Source scale: 0.38.
- NIF: `art/units/stag/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 392 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2783, feet Y=0.
- Clips: idle (100 channels), run (100 channels), fortify (100 channels), attack (100 channels), hurt (100 channels), die (100 channels).
- SHA-256: `ee8a3a5627b32cdfc74a1cd505140ea9ff7d6a186f1b2575b99d1bb40088061d`.
- idle: `art/units/horsearcher/horsearcher_md_idle.kf`.
- run: `art/units/horsearcher/horsearcher_md_run.kf`.
- fortify: `art/units/horsearcher/horsearcher_md_fortifya.kf`.
- attack: `art/units/horsearcher/horsearcher_md_strikea.kf`.
- hurt: `art/units/horsearcher/horsearcher_md_hurta.kf`.
- die: `art/units/horsearcher/horsearcher_md_diea.kf`.
- Note: Hidden geometry omitted: HorseArcher_Horse
- Note: Hidden geometry omitted: Deer_Doe
- Note: Hidden geometry omitted: Deer_Buck02
- Note: Hidden geometry omitted: shadow_plane

### military:captured_stallion

- Model: `src/Mods/Military/art/models/captured_stallion.glb` (337,660 bytes).
- Art: `ART_DEF_UNIT_STALLION`. Source scale: 0.3.
- NIF: `art/units/horse/horsedark.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 432 triangles, 1 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2922, feet Y=0.
- Clips: idle (64 channels), run (64 channels), fortify (64 channels), attack (64 channels), hurt (64 channels), die (64 channels).
- SHA-256: `612e0153193bc61be4fa3ff0ee0f395c0a8cd28fa700a258eee0aa0760946c0b`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:captured_wolf

- Model: `src/Mods/Military/art/models/captured_wolf.glb` (346,728 bytes).
- Art: `ART_DEF_UNIT_WOLF`. Source scale: 0.5.
- NIF: `art/units/wolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5082, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `da51c49288d36e2b9270449fdc32be93286fb8f629417ef6c2132bcc94b8032f`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:caravel

- Model: `src/Mods/Military/art/models/caravel.glb` (198,988 bytes).
- Art: `ART_DEF_UNIT_CARAVEL`. Source scale: 0.24.
- NIF: `art/units/caravel/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 427 triangles, 3 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3653, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels), attack (26 channels), hurt (26 channels), die (26 channels).
- SHA-256: `96feeb09fb12f7717d693d54fef1d9c43cbc90ead381bebc8f4dd85ad27155cf`.
- idle: `art/units/caravel/caravel_md_idle.kf`.
- run: `art/units/caravel/caravel_md_run.kf`.
- fortify: `art/units/caravel/caravel_md_fortifya.kf`.
- attack: `art/units/caravel/caravel_md_strikea.kf`.
- hurt: `art/units/caravel/caravel_md_hurta.kf`.
- die: `art/units/caravel/caravel_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:carrier

- Model: `src/Mods/Military/art/models/carrier.glb` (198,904 bytes).
- Art: `ART_DEF_UNIT_CARRIER_ENGLAND`. Source scale: 1.0.
- NIF: `art/units/carrier/english/ark_royal.nif`.
- KFM: `art/units/carrier/carrier.kfm`.
- Geometry: 594 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.2222, radius 0.4800, feet Y=0.
- Clips: idle (12 channels), run (12 channels), fortify (12 channels), attack (12 channels), hurt (12 channels), die (12 channels).
- SHA-256: `0c7556d76a938c7a143a91ce4d5e25d2b642608effb25faec3b7099eafe162ea`.
- idle: `art/units/carrier/carrier_md_idle.kf`.
- run: `art/units/carrier/carrier_md_run.kf`.
- fortify: `art/units/carrier/carrier_md_fortifya.kf`.
- attack: `art/units/carrier/carrier_md_strikea.kf`.
- hurt: `art/units/carrier/carrier_md_hurta.kf`.
- die: `art/units/carrier/carrier_md_diea.kf`.

### military:carthage_numidian_cavalry

- Model: `src/Mods/Military/art/models/carthage_numidian_cavalry.glb` (585,296 bytes).
- Art: `ART_DEF_UNIT_CARTHAGE_NUMIDIAN_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/numidian_cavalry/numidian_cavalry.nif`.
- KFM: `art/units/numidian_cavalry/numidian_cavalry.kfm`.
- Geometry: 1027 triangles, 2 skins, 35 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3037, feet Y=0.
- Clips: idle (105 channels), run (105 channels), fortify (105 channels), attack (105 channels), hurt (105 channels), die (105 channels).
- SHA-256: `827db3e81666e80045d06c014658b3f07f26606ab6041369d041f70d7317f31d`.
- idle: `art/units/numidian_cavalry/numidian_cavalry_md_idle.kf`.
- run: `art/units/numidian_cavalry/numidian_cavalry_md_run.kf`.
- fortify: `art/units/numidian_cavalry/numidian_cavalry_md_fortifya.kf`.
- attack: `art/units/numidian_cavalry/numidian_cavalry_md_strikea.kf`.
- hurt: `art/units/numidian_cavalry/numidian_cavalry_md_hurta.kf`.
- die: `art/units/numidian_cavalry/numidian_cavalry_md_diea.kf`.

### military:catapult

- Model: `src/Mods/Military/art/models/catapult.glb` (288,432 bytes).
- Art: `ART_DEF_UNIT_CATAPULT`. Source scale: 0.65.
- NIF: `art/units/catapult/catapult.nif`.
- KFM: `art/units/catapult/catapult.kfm`.
- Geometry: 646 triangles, 1 skins, 9 joint bindings, 2 textures.
- Bounds: height 0.5199, radius 0.4800, feet Y=0.
- Clips: idle (27 channels), run (27 channels), fortify (27 channels), attack (27 channels), hurt (27 channels).
- SHA-256: `357191a472bcef3378b6c0a370b174dee8586a351c8ed9d5e6ec804c4132912e`.
- idle: `art/units/catapult/catapult_md_idle.kf`.
- run: `art/units/catapult/catapult_md_run.kf`.
- fortify: `art/units/catapult/catapult_md_rangedfortify.kf`.
- attack: `art/units/catapult/catapult_md_rangedstrike.kf`.
- hurt: `art/units/catapult/catapult_md_hurta.kf`.

### military:cavalry

- Model: `src/Mods/Military/art/models/cavalry.glb` (554,640 bytes).
- Art: `ART_DEF_UNIT_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/cavalry/cavalry.nif`.
- KFM: `art/units/cavalry/cavalry.kfm`.
- Geometry: 1142 triangles, 4 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3090, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (86 channels), attack (86 channels), hurt (86 channels), die (86 channels).
- SHA-256: `0afdfbe4823e26e697b28e2ecaa986963d1864d355b946e6f9d996cad099af06`.
- idle: `art/units/cavalry/cavalry_md_idle.kf`.
- run: `art/units/cavalry/cavalry_md_run.kf`.
- fortify: `art/units/cavalry/cavalry_md_fortifya.kf`.
- attack: `art/units/cavalry/cavalry_md_strikea.kf`.
- hurt: `art/units/cavalry/cavalry_md_hurta.kf`.
- die: `art/units/cavalry/cavalry_md_diea.kf`.

### military:cavalry_light

- Model: `src/Mods/Military/art/models/cavalry_light.glb` (566,012 bytes).
- Art: `ART_DEF_UNIT_CAVALRY_LIGHT`. Source scale: 0.42.
- NIF: `art/units/cavalry light/european/cavalry pistol.nif`.
- KFM: `art/units/cavalry light/european/cavalry.kfm`.
- Geometry: 1206 triangles, 3 skins, 36 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2789, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (123 channels), attack (123 channels), hurt (86 channels), die (86 channels).
- SHA-256: `b2a1c49ea5ffe3aad28a06aacb906cae36df03b9bb4f7bdf10c7757ffe690727`.
- idle: `art/units/cavalry light/european/cavalry_md_idle.kf`.
- run: `art/units/cavalry light/european/cavalry_md_run.kf`.
- fortify: `art/units/cavalry light/european/cavalry_md_fortifya.kf`.
- attack: `art/units/cavalry light/european/cavalry_md_strikea.kf`.
- hurt: `art/units/cavalry light/european/cavalry_md_hurta.kf`.
- die: `art/units/cavalry light/european/cavalry_md_diea.kf`.

### military:cave_lion

- Model: `src/Mods/Military/art/models/cave_lion.glb` (320,020 bytes).
- Art: `ART_DEF_UNIT_CAVELION`. Source scale: 0.75.
- NIF: `art/units/animals/cavelion/lion.nif`.
- KFM: `art/units/animals/cavelion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5755, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `798c299d9f2220ee774c8bc137e2507aa131d3664013786fffb0f9df13da365c`.
- idle: `art/units/animals/cavelion/lion_md_idle.kf`.
- run: `art/units/animals/cavelion/lion_md_run.kf`.
- fortify: `art/units/animals/cavelion/lion_md_fortifya.kf`.
- attack: `art/units/animals/cavelion/lion_md_strikea.kf`.
- hurt: `art/units/animals/cavelion/lion_md_hurta.kf`.
- die: `art/units/animals/cavelion/lion_md_diea.kf`.

### military:cavebear

- Model: `src/Mods/Military/art/models/cavebear.glb` (276,416 bytes).
- Art: `ART_DEF_UNIT_CAVEBEAR`. Source scale: 0.8.
- NIF: `art/units/animals/cavebear/bear.nif`.
- KFM: `art/units/animals/cavebear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5204, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `8c98c974431ed461f9823a7c0b847c195cd67799f498b70ff89b35f14ada2a8a`.
- idle: `art/units/animals/cavebear/bear_md_idle.kf`.
- run: `art/units/animals/cavebear/bear_md_run.kf`.
- fortify: `art/units/animals/cavebear/bear_md_fortifya.kf`.
- attack: `art/units/animals/cavebear/bear_md_strikea.kf`.
- hurt: `art/units/animals/cavebear/bear_md_hurta.kf`.
- die: `art/units/animals/cavebear/bear_md_diea.kf`.

### military:celtic_gallic_warrior

- Model: `src/Mods/Military/art/models/celtic_gallic_warrior.glb` (299,164 bytes).
- Art: `ART_DEF_UNIT_CELTIC_BARBARIAN`. Source scale: 0.44.
- NIF: `art/units/celtic_warrior/celtic_warrior.nif`.
- KFM: `art/units/celtic_warrior/celtic_warrior.kfm`.
- Geometry: 595 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3495, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `53e6d03e1ced911720d8d0ecef07f5b551bd371d67f1bda6746ab7ea249ac604`.
- idle: `art/units/celtic_warrior/celtic_warrior_md_idle.kf`.
- run: `art/units/celtic_warrior/celtic_warrior_md_run.kf`.
- fortify: `art/units/celtic_warrior/celtic_warrior_md_fortifya.kf`.
- attack: `art/units/celtic_warrior/celtic_warrior_md_strikea.kf`.
- hurt: `art/units/celtic_warrior/celtic_warrior_md_hurta.kf`.
- die: `art/units/celtic_warrior/celtic_warrior_md_diea.kf`.

### military:chariot

- Model: `src/Mods/Military/art/models/chariot.glb` (588,740 bytes).
- Art: `ART_DEF_UNIT_CHARIOT`. Source scale: 0.35.
- NIF: `art/units/chariot/chariot.nif`.
- KFM: `art/units/chariot/chariot.kfm`.
- Geometry: 1488 triangles, 5 skins, 48 joint bindings, 5 textures.
- Bounds: height 0.3835, radius 0.4800, feet Y=0.
- Clips: idle (105 channels), run (105 channels), fortify (105 channels), attack (105 channels), hurt (105 channels), die (105 channels).
- SHA-256: `77a7492c7c381c86ae77fa24c010e97f9994b1e8349c3ed587a3efff33b8bea1`.
- idle: `art/units/chariot/chariot_md_idle.kf`.
- run: `art/units/chariot/chariot_md_run.kf`.
- fortify: `art/units/chariot/chariot_md_fortifya.kf`.
- attack: `art/units/chariot/chariot_md_strikea.kf`.
- hurt: `art/units/chariot/chariot_md_hurta.kf`.
- die: `art/units/chariot/chariot_md_diea.kf`.

### military:cheetah

- Model: `src/Mods/Military/art/models/cheetah.glb` (272,172 bytes).
- Art: `ART_DEF_UNIT_CHEETAH`. Source scale: 0.45.
- NIF: `art/units/animals/cheetah/panther.nif`.
- KFM: `art/units/animals/cheetah/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `41208697c8554714f4caf7f36c12090fffb6f1d59222db8c2f068476428a90b3`.
- idle: `art/units/animals/cheetah/panther_md_idle.kf`.
- run: `art/units/animals/cheetah/panther_md_run.kf`.
- fortify: `art/units/animals/cheetah/panther_md_fortifya.kf`.
- attack: `art/units/animals/cheetah/panther_md_strikea.kf`.
- hurt: `art/units/animals/cheetah/panther_md_hurta.kf`.
- die: `art/units/animals/cheetah/panther_md_diea.kf`.

### military:china_chokonu

- Model: `src/Mods/Military/art/models/china_chokonu.glb` (352,796 bytes).
- Art: `ART_DEF_UNIT_CHINA_CHOKONU`. Source scale: 0.44.
- NIF: `art/units/unique/china/chokonu/chokunu.nif`.
- KFM: `art/units/unique/china/chokonu/chokunu.kfm`.
- Geometry: 812 triangles, 2 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3544, feet Y=0.
- Clips: idle (63 channels), run (63 channels), fortify (63 channels), attack (63 channels), hurt (63 channels), die (63 channels).
- SHA-256: `a861fcabcdee16df74ff5ff472141bfb552376fd1dc06549a6463398fe85f7fa`.
- idle: `art/units/unique/china/chokonu/chokunu_md_idle.kf`.
- run: `art/units/unique/china/chokonu/chokunu_md_run.kf`.
- fortify: `art/units/unique/china/chokonu/chokunu_md_rangedfortify.kf`.
- attack: `art/units/unique/china/chokonu/chokunu_md_rangedstrike.kf`.
- hurt: `art/units/unique/china/chokonu/chokunu_md_hurta.kf`.
- die: `art/units/unique/china/chokonu/chokunu_md_rangeddiea.kf`.

### military:chivalry

- Model: `src/Mods/Military/art/models/chivalry.glb` (622,540 bytes).
- Art: `ART_DEF_UNIT_CHIVALRY`. Source scale: 0.42.
- NIF: `art/units/mailed knight/european/mtemplar_heavy.nif`.
- KFM: `art/units/knight/knight.kfm`.
- Geometry: 1090 triangles, 3 skins, 40 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2940, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `d19cf3450a36c7ed7a021bb811e641b6973046520ccb713a264f62c446023e20`.
- idle: `art/units/knight/knight_md_idle.kf`.
- run: `art/units/knight/knight_md_run.kf`.
- fortify: `art/units/knight/knight_md_fortifya.kf`.
- attack: `art/units/knight/knight_md_strikea.kf`.
- hurt: `art/units/knight/knight_md_hurta.kf`.
- die: `art/units/knight/knight_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Hidden geometry omitted: Editable Mesh@#0

### military:christian_missionary

- Model: `src/Mods/Military/art/models/christian_missionary.glb` (244,160 bytes).
- Art: `ART_DEF_UNIT_CHRISTIAN_MISSIONARY`. Source scale: 0.44.
- NIF: `art/units/missionary/christian/christianmissionary.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 680 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2309, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), die (53 channels).
- SHA-256: `cf87f936ed3080d21d0268f71a23d5252deb4ffbc649106c01154a3c7d519034`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:clones

- Model: `src/Mods/Military/art/models/clones.glb` (238,208 bytes).
- Art: `ART_DEF_UNIT_CLONEWORKER`. Source scale: 0.44.
- NIF: `art/units/nextwar_cloneworker/nextwar_cloneworker.nif`.
- KFM: `art/units/nextwar_cloneworker/nextwar_cloneworker.kfm`.
- Geometry: 802 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2852, feet Y=0.
- Clips: idle (63 channels), run (63 channels), fortify (63 channels).
- SHA-256: `fddf8316a24457d74f993a70472602cfe13d3d7aae79536b38e2634df7645a94`.
- idle: `art/units/nextwar_cloneworker/nextwar_cloneworker_md_idle.kf`.
- run: `art/units/nextwar_cloneworker/nextwar_cloneworker_md_run.kf`.
- fortify: `art/units/nextwar_cloneworker/nextwar_cloneworker_md_fortify.kf`.
- Note: Missing texture: Modern_Worker_tools_64.dds
- Note: Missing texture: Worker_tools.dds

### military:cog

- Model: `src/Mods/Military/art/models/cog.glb` (539,276 bytes).
- Art: `ART_DEF_UNIT_COG`. Source scale: 0.13.
- NIF: `art/units/cog/cog.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 1313 triangles, 0 skins, 0 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.3128, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `6e25278cf400b43fa5ece632fea9d8be8d843120499c128911c716f121ec0981`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:colonist

- Model: `src/Mods/Military/art/models/colonist.glb` (256,496 bytes).
- Art: `ART_DEF_UNIT_COLONIST_MALE`. Source scale: 0.46.
- NIF: `art/units/settler/settler_male.nif`.
- KFM: `art/units/settler/settler_male.kfm`.
- Geometry: 700 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3516, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), die (56 channels).
- SHA-256: `4fea8e192340b60522433bef0989fd97edf77df4b55e2942005ff4de6f8968fb`.
- idle: `art/units/settler/settler_male_md_idle.kf`.
- run: `art/units/settler/settler_male_md_run.kf`.
- fortify: `art/units/settler/settler_male_md_fortify.kf`.
- die: `art/units/settler/settler_male_md_idle_die.kf`.
- Note: First of 7 formation art groups exported

### military:confucian_missionary

- Model: `src/Mods/Military/art/models/confucian_missionary.glb` (189,560 bytes).
- Art: `ART_DEF_UNIT_CONFUCIAN_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/confucian/confucian_missionary.nif`.
- KFM: `art/units/missionary/confucian/confucian_missionary.kfm`.
- Geometry: 480 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2507, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), die (50 channels).
- SHA-256: `729cd80bb2e844f34831170d25fd6f8de128e02e5a54aa07e2fdc370cc0bfa2d`.
- idle: `art/units/missionary/confucian/confucian_missionary_md_idle.kf`.
- run: `art/units/missionary/confucian/confucian_missionary_md_run.kf`.
- fortify: `art/units/missionary/confucian/confucian_missionary_md_fortifya.kf`.
- die: `art/units/missionary/confucian/confucian_missionary_md_idle_die.kf`.

### military:construct_ship

- Model: `src/Mods/Military/art/models/construct_ship.glb` (248,484 bytes).
- Art: `ART_DEF_UNIT_CONSTRUCT_SHIP`. Source scale: 0.5.
- NIF: `art/units/finalfrontier_constructionship/constructionship.nif`.
- KFM: `art/units/finalfrontier_constructionship/constructionship.kfm`.
- Geometry: 853 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2697, radius 0.4800, feet Y=0.
- Clips: idle (34 channels), run (34 channels), fortify (34 channels).
- SHA-256: `62505d27b31edad0921f02577d57902d271577d77ba48e904924d775809b6327`.
- idle: `art/units/finalfrontier_constructionship/constructionship_md_idle.kf`.
- run: `art/units/finalfrontier_constructionship/constructionship_md_run.kf`.
- fortify: `art/units/finalfrontier_constructionship/constructionship_md_fortifya.kf`.

### military:crossbowman

- Model: `src/Mods/Military/art/models/crossbowman.glb` (375,804 bytes).
- Art: `ART_DEF_UNIT_CROSSBOWMAN`. Source scale: 0.44.
- NIF: `art/units/crossbowman/crossbowman.nif`.
- KFM: `art/units/crossbowman/crossbowman.kfm`.
- Geometry: 832 triangles, 3 skins, 22 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3612, feet Y=0.
- Clips: idle (74 channels), run (74 channels), fortify (74 channels), attack (74 channels), hurt (74 channels), die (74 channels).
- SHA-256: `4aeb4ea50a5e3f7b6b5b3b0a41e8468da6aafdc0850c74aad14a6c36266a6725`.
- idle: `art/units/crossbowman/crossbowman_md_idle.kf`.
- run: `art/units/crossbowman/crossbowman_md_run.kf`.
- fortify: `art/units/crossbowman/crossbowman_md_fortifya.kf`.
- attack: `art/units/crossbowman/crossbowman_md_strikea.kf`.
- hurt: `art/units/crossbowman/crossbowman_md_hurta.kf`.
- die: `art/units/crossbowman/crossbowman_md_diea.kf`.
- Note: Missing texture: Arrow_Bolt.dds

### military:cruiser

- Model: `src/Mods/Military/art/models/cruiser.glb` (384,652 bytes).
- Art: `ART_DEF_UNIT_BROOKLYN`. Source scale: 0.6.
- NIF: `art/units/cruiser/brooklyn.nif`.
- KFM: `art/units/cruiser/brooklyn.kfm`.
- Geometry: 894 triangles, 2 skins, 7 joint bindings, 2 textures.
- Bounds: height 0.1921, radius 0.4800, feet Y=0.
- Clips: idle (30 channels), run (30 channels), fortify (30 channels), attack (30 channels), hurt (30 channels).
- SHA-256: `2927e84d584675478ee4f533c68f5c5d6a826d6c2f1c7edc6414c7c82a5328cf`.
- idle: `art/units/cruiser/brooklyn_md_idle.kf`.
- run: `art/units/cruiser/brooklyn_md_run.kf`.
- fortify: `art/units/cruiser/brooklyn_md_rangedfortify.kf`.
- attack: `art/units/cruiser/brooklyn_md_rangedstrike.kf`.
- hurt: `art/units/cruiser/brooklyn_md_hurta.kf`.

### military:cuirassier

- Model: `src/Mods/Military/art/models/cuirassier.glb` (650,304 bytes).
- Art: `ART_DEF_UNIT_CUIRASSIER`. Source scale: 0.42.
- NIF: `art/units/cuirassier/cuirassier.nif`.
- KFM: `art/units/cuirassier/cuirassier.kfm`.
- Geometry: 1344 triangles, 2 skins, 46 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3026, feet Y=0.
- Clips: idle (145 channels), run (145 channels), fortify (145 channels), attack (145 channels), die (145 channels).
- SHA-256: `be53169f48d9894b4fbf5ce44dbc95b69cbc0501e6386ceee463b04a9234d416`.
- idle: `art/units/cuirassier/cuirassier_md_idle.kf`.
- run: `art/units/cuirassier/cuirassier_md_run.kf`.
- fortify: `art/units/cuirassier/cuirassier_md_rangedfortify.kf`.
- attack: `art/units/cuirassier/cuirassier_md_rangedstrike.kf`.
- die: `art/units/cuirassier/cuirassier_md_rangeddiea.kf`.
- Note: Hidden geometry omitted: Biped Object
- Note: Hidden geometry omitted: Biped Object@#0
- Note: Hidden geometry omitted: Bone
- Note: Hidden geometry omitted: Biped Object@#1
- Note: Hidden geometry omitted: Biped Object@#2
- Note: Hidden geometry omitted: Biped Object@#3
- Note: Hidden geometry omitted: Biped Object@#4
- Note: Hidden geometry omitted: Biped Object@#5
- Note: Hidden geometry omitted: Biped Object@#6
- Note: Hidden geometry omitted: Biped Object@#7
- Note: Hidden geometry omitted: Biped Object@#8
- Note: Hidden geometry omitted: Biped Object@#9
- Note: Hidden geometry omitted: Biped Object@#10

### military:cyborg

- Model: `src/Mods/Military/art/models/cyborg.glb` (303,412 bytes).
- Art: `ART_DEF_UNIT_CYBORG`. Source scale: 0.44.
- NIF: `art/units/nextwar_cyborg/nextwar_cyborg.nif`.
- KFM: `art/units/nextwar_cyborg/nextwar_cyborg.kfm`.
- Geometry: 828 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3729, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), die (51 channels).
- SHA-256: `278daba8dce369a59a0c25361dcfa5c9046fb8e464c1c98c6cc0da74691a8b69`.
- idle: `art/units/nextwar_cyborg/nextwar_cyborg_md_idle.kf`.
- run: `art/units/nextwar_cyborg/nextwar_cyborg_md_run.kf`.
- fortify: `art/units/nextwar_cyborg/nextwar_cyborg_md_rangedfortify.kf`.
- attack: `art/units/nextwar_cyborg/nextwar_cyborg_md_rangedstrike.kf`.
- die: `art/units/nextwar_cyborg/nextwar_cyborg_md_rangeddiea.kf`.

### military:czech_hussite

- Model: `src/Mods/Military/art/models/czech_hussite.glb` (322,420 bytes).
- Art: `ART_DEF_UNIT_CZECH_HUSSITE`. Source scale: 0.44.
- NIF: `art/czech/hussite/imperialmusket.nif`.
- KFM: `art/units/musketman/musketman.kfm`.
- Geometry: 955 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2785, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `04e53ddf0c013430287da55a7b2b50ae9b16f38c8c36b1e168d878a9b08e9574`.
- idle: `art/units/musketman/musketman_md_idle.kf`.
- run: `art/units/musketman/musketman_md_run.kf`.
- fortify: `art/units/musketman/musketman_md_rangedfortify.kf`.
- attack: `art/units/musketman/musketman_md_rangedstrike.kf`.
- hurt: `art/units/musketman/musketman_md_hurta.kf`.
- die: `art/units/musketman/musketman_md_rangeddiea.kf`.
- Note: Hidden geometry omitted: Editable Mesh

### military:danish_workboat

- Model: `src/Mods/Military/art/models/danish_workboat.glb` (212,332 bytes).
- Art: `ART_DEF_UNIT_DANISH_WORKBOAT`. Source scale: 0.19.
- NIF: `art/units/workboat_ancient/workboat_ancient.nif`.
- KFM: `art/units/workboat_ancient/workboat_ancient.kfm`.
- Geometry: 417 triangles, 3 skins, 10 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3534, feet Y=0.
- Clips: idle (17 channels), run (17 channels), fortify (17 channels), die (17 channels).
- SHA-256: `17df00ffcc1b1e902f40a6cbe1c3f36411d11da362b9bc42865090a0173eb06c`.
- idle: `art/units/workboat_ancient/workboat_ancient_md_idle.kf`.
- run: `art/units/workboat_ancient/workboat_ancient_md_run.kf`.
- fortify: `art/units/workboat_ancient/workboat_ancient_md_fortifya.kf`.
- die: `art/units/workboat_ancient/workboat_ancient_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:destroyer

- Model: `src/Mods/Military/art/models/destroyer.glb` (361,448 bytes).
- Art: `ART_DEF_UNIT_DESTROYER`. Source scale: 0.7.
- NIF: `art/units/destroyer/destroyer.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 517 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.2892, radius 0.4800, feet Y=0.
- Clips: idle (63 channels), run (63 channels), fortify (63 channels), attack (63 channels), hurt (63 channels), die (63 channels).
- SHA-256: `a71092d5bc259483c04007e02db59191c97f7353cca023de4736922566c275f3`.
- idle: `art/units/destroyer/destroyer_md_idle.kf`.
- run: `art/units/destroyer/destroyer_md_run.kf`.
- fortify: `art/units/destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/destroyer/destroyer_md_hurta.kf`.
- die: `art/units/destroyer/destroyer_md_diea.kf`.

### military:direwolf

- Model: `src/Mods/Military/art/models/direwolf.glb` (346,824 bytes).
- Art: `ART_DEF_UNIT_DIREWOLF`. Source scale: 0.55.
- NIF: `art/units/animals/direwolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5082, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `75e3e1b365254bd29af120f215344fe00734eb7b13984dffc712ba1999134627`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:dragoon

- Model: `src/Mods/Military/art/models/dragoon.glb` (597,460 bytes).
- Art: `ART_DEF_UNIT_DRAGOON`. Source scale: 0.42.
- NIF: `art/units/dragoon/british/light_dragoon.nif`.
- KFM: `art/units/dragoon/british/light_dragoon.kfm`.
- Geometry: 1200 triangles, 5 skins, 40 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2711, feet Y=0.
- Clips: idle (103 channels), run (105 channels), fortify (105 channels), attack (105 channels), hurt (105 channels), die (105 channels).
- SHA-256: `7a2311cfc898e11d08c06711bd966e1748fb4fcae088773b1711603e5b44510a`.
- idle: `art/units/dragoon/british/mounted_swordsman_mongol_md_idle.kf`.
- run: `art/units/dragoon/british/mounted_swordsman_mongol_md_run.kf`.
- fortify: `art/units/dragoon/british/mounted_swordsman_mongol_md_fortifya.kf`.
- attack: `art/units/dragoon/british/mounted_swordsman_mongol_md_strikea.kf`.
- hurt: `art/units/dragoon/british/mounted_swordsman_mongol_md_hurta.kf`.
- die: `art/units/dragoon/british/mounted_swordsman_mongol_md_diea.kf`.

### military:dreadnought

- Model: `src/Mods/Military/art/models/dreadnought.glb` (227,356 bytes).
- Art: `ART_DEF_UNIT_DREADNOUGHT`. Source scale: 0.75.
- NIF: `art/units/dreadnought/dreadnought.nif`.
- KFM: `art/units/dreadnought/dreadnought.kfm`.
- Geometry: 397 triangles, 2 skins, 13 joint bindings, 1 textures.
- Bounds: height 0.2628, radius 0.4800, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels), attack (43 channels).
- SHA-256: `c80c4fb563f59a9c81c29c615e631deec3f73afd433c2e649c5a2b2adadbcb13`.
- idle: `art/units/dreadnought/dreadnought_md_idle.kf`.
- run: `art/units/dreadnought/dreadnought_md_run.kf`.
- fortify: `art/units/dreadnought/dreadnought_md_rangedfortify.kf`.
- attack: `art/units/dreadnought/dreadnought_md_rangedstrikea.kf`.

### military:dreadnought_helgoland

- Model: `src/Mods/Military/art/models/dreadnought_helgoland.glb` (500,800 bytes).
- Art: `ART_DEF_UNIT_DREADNOUGHT_SHIP`. Source scale: 0.6.
- NIF: `art/units/dreadnought/european/helgoland.nif`.
- KFM: `art/units/dreadnought/european/helgoland.kfm`.
- Geometry: 1996 triangles, 3 skins, 7 joint bindings, 3 textures.
- Bounds: height 0.3012, radius 0.4800, feet Y=0.
- Clips: idle (27 channels), run (27 channels), fortify (27 channels), attack (27 channels), hurt (27 channels).
- SHA-256: `35271a29ef8e98740681db31664eda3a5250e9c8145963ce709cb7ba9ca67308`.
- idle: `art/units/dreadnought/european/helgoland_md_idle.kf`.
- run: `art/units/dreadnought/european/helgoland_md_run.kf`.
- fortify: `art/units/dreadnought/european/helgoland_md_rangedfortify.kf`.
- attack: `art/units/dreadnought/european/helgoland_md_rangedstrike.kf`.
- hurt: `art/units/dreadnought/european/helgoland_md_hurta.kf`.

### military:drekar

- Model: `src/Mods/Military/art/models/drekar.glb` (471,992 bytes).
- Art: `ART_DEF_UNIT_DREKAR_SCANDINAVIAN`. Source scale: 0.1.
- NIF: `art/units/drekar/galley_fx.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 514 triangles, 0 skins, 0 joint bindings, 4 textures.
- Bounds: height 0.5538, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `4df60b9f72f94e99752e1eb7a762bbe9e65449f1c1b50489081d2a859b111712`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:dropship

- Model: `src/Mods/Military/art/models/dropship.glb` (224,980 bytes).
- Art: `ART_DEF_UNIT_DROPSHIP`. Source scale: 0.36.
- NIF: `art/units/dropship/isscv.nif`.
- KFM: `art/units/dropship/gunship.kfm`.
- Geometry: 1168 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2083, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), fortify (23 channels), attack (23 channels), hurt (23 channels).
- SHA-256: `93c3aa671d9daacbc790df4236f210067e5aaa131bd4d86b9b97e9319e5bd81e`.
- idle: `art/units/dropship/gunship_md_idle.kf`.
- run: `art/units/dropship/gunship_md_run.kf`.
- fortify: `art/units/dropship/gunship_md_rangedfortify.kf`.
- attack: `art/units/dropship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/dropship/gunship_md_hurta.kf`.

### military:early_tank

- Model: `src/Mods/Military/art/models/early_tank.glb` (162,468 bytes).
- Art: `ART_DEF_UNIT_MARK_V_TANK`. Source scale: 0.48.
- NIF: `art/units/scenario/mark_v_tank/markvtank.nif`.
- KFM: `art/units/scenario/mark_v_tank/markvtank.kfm`.
- Geometry: 530 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.3122, radius 0.4800, feet Y=0.
- Clips: idle (11 channels), run (11 channels), fortify (11 channels), attack (11 channels), hurt (11 channels).
- SHA-256: `00a20092d0da5ceacb712189954fd2eeef2bc5760199e158b85b63282240c557`.
- idle: `art/units/scenario/mark_v_tank/markvtank_md_idle.kf`.
- run: `art/units/scenario/mark_v_tank/markvtank_md_run.kf`.
- fortify: `art/units/scenario/mark_v_tank/markvtank_md_rangedfortify.kf`.
- attack: `art/units/scenario/mark_v_tank/markvtank_md_rangedstrike.kf`.
- hurt: `art/units/scenario/mark_v_tank/markvtank_md_hurta.kf`.

### military:egypt_myth_missionary

- Model: `src/Mods/Military/art/models/egypt_myth_missionary.glb` (263,572 bytes).
- Art: `ART_DEF_UNIT_RA_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/scribe/hindu_missionary.nif`.
- KFM: `art/units/missionary/scribe/hindu_missionary.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3339, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), die (54 channels).
- SHA-256: `2b42aeb297dd9a4cbb3a21320de22b4b651e491bbb5d2cd859cca62ba1eb59a6`.
- idle: `art/units/missionary/scribe/hindu_missionary_md_idle.kf`.
- run: `art/units/missionary/scribe/hindu_missionary_md_run.kf`.
- fortify: `art/units/missionary/scribe/hindu_missionary_md_fortify.kf`.
- die: `art/units/missionary/scribe/hindu_missionary_md_idle_die.kf`.

### military:egypt_warchariot

- Model: `src/Mods/Military/art/models/egypt_warchariot.glb` (1,166,408 bytes).
- Art: `ART_DEF_UNIT_EGYPT_WARCHARIOT`. Source scale: 0.37.
- NIF: `art/units/unique/egypt/warchariot/egyptianwarchariot.nif`.
- KFM: `art/units/unique/egypt/warchariot/egyptianwarchariot.kfm`.
- Geometry: 2412 triangles, 6 skins, 75 joint bindings, 5 textures.
- Bounds: height 0.3800, radius 0.4800, feet Y=0.
- Clips: idle (214 channels), run (214 channels), fortify (214 channels), attack (214 channels), hurt (214 channels), die (214 channels).
- SHA-256: `1a242bbf433a38cf72f3136af535beb724cc3979701c80cff7cf113f9670334b`.
- idle: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_idle.kf`.
- run: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_run.kf`.
- fortify: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_fortifya.kf`.
- attack: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_strikea.kf`.
- hurt: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_hurta.kf`.
- die: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_diea.kf`.

### military:elephant

- Model: `src/Mods/Military/art/models/elephant.glb` (583,232 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/elephant/elephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 630 triangles, 1 skins, 24 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4359, feet Y=0.
- Clips: idle (101 channels), run (101 channels), fortify (101 channels), attack (101 channels), hurt (101 channels), die (101 channels).
- SHA-256: `28dc62034cca9024aca0d5e64539cf62f09aa46af7991ca426eebcef5972d50b`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: Bone

### military:elephant_bombard

- Model: `src/Mods/Military/art/models/elephant_bombard.glb` (901,356 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_BOMBARD`. Source scale: 0.32.
- NIF: `art/units/elephant bombard/siegeelephant.nif`.
- KFM: `art/units/elephant bombard/siegeelephant.kfm`.
- Geometry: 1973 triangles, 5 skins, 47 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.2643, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `e0390582cf67a58ec6dae20c1240d52a2341356a4d9756f13966f9f7235f49f0`.
- idle: `art/units/elephant bombard/warelephant_md_idle.kf`.
- run: `art/units/elephant bombard/warelephant_md_run.kf`.
- fortify: `art/units/elephant bombard/warelephant_md_fortifya.kf`.
- attack: `art/units/elephant bombard/warelephant_md_strikea.kf`.
- hurt: `art/units/elephant bombard/warelephant_md_hurta.kf`.
- die: `art/units/elephant bombard/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: HeadPeice:0
- Note: Hidden geometry omitted: WarElephant-Carriage:1

### military:elephant_cavalry

- Model: `src/Mods/Military/art/models/elephant_cavalry.glb` (831,056 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_CAVALRY`. Source scale: 0.32.
- NIF: `art/units/elephant cavalry/siegeelephant.nif`.
- KFM: `art/units/elephant cavalry/siegeelephant.kfm`.
- Geometry: 1718 triangles, 3 skins, 45 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.2643, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `6373a44f6579f849dacff5e7faabc3c03694003364de0f37808ad9b68ad7dfd8`.
- idle: `art/units/elephant cavalry/warelephant_md_idle.kf`.
- run: `art/units/elephant cavalry/warelephant_md_run.kf`.
- fortify: `art/units/elephant cavalry/warelephant_md_fortifya.kf`.
- attack: `art/units/elephant cavalry/warelephant_md_strikea.kf`.
- hurt: `art/units/elephant cavalry/warelephant_md_hurta.kf`.
- die: `art/units/elephant cavalry/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: HeadPeice:0
- Note: Hidden geometry omitted: Editable Mesh
- Note: Hidden geometry omitted: WarElephant-Carriage:1

### military:elephant_rider

- Model: `src/Mods/Military/art/models/elephant_rider.glb` (760,276 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_RIDER`. Source scale: 0.32.
- NIF: `art/units/warelephant/warelephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 1454 triangles, 3 skins, 47 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2463, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `1b9942091024308cb0173272a59cbf3b6c8ca9bb75a44fceee7d634a87f36200`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.

### military:emp_infantry

- Model: `src/Mods/Military/art/models/emp_infantry.glb` (533,232 bytes).
- Art: `ART_DEF_UNIT_EMP_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/emp_infantry2/marines.nif`.
- KFM: `art/units/emp_infantry2/animations/marines.kfm`.
- Geometry: 2262 triangles, 1 skins, 16 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3787, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `973894521bc9bf3963d0e44fb46d11b9d2b0fc555c76bd1f64f90ee5e190a60e`.
- idle: `art/units/emp_infantry2/animations/marines_md_idle.kf`.
- run: `art/units/emp_infantry2/animations/marines_md_run.kf`.
- fortify: `art/units/emp_infantry2/animations/marines_md_rangedfortify.kf`.
- attack: `art/units/emp_infantry2/animations/marines_md_rangedstrike.kf`.
- hurt: `art/units/emp_infantry2/animations/marines_md_hurta.kf`.
- die: `art/units/emp_infantry2/animations/marines_md_rangeddiea.kf`.

### military:engineer

- Model: `src/Mods/Military/art/models/engineer.glb` (308,176 bytes).
- Art: `ART_DEF_UNIT_ENGINEER`. Source scale: 0.46.
- NIF: `art/units/greatpeople/engineer/engineer.nif`.
- KFM: `art/units/greatpeople/engineer/engineer.kfm`.
- Geometry: 706 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3637, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels).
- SHA-256: `f7165161faa067c090d32bd73054f5daf36802fe4868137775e9ba6fdf95d0a6`.
- idle: `art/units/greatpeople/engineer/engineer_md_idle.kf`.
- run: `art/units/greatpeople/engineer/engineer_md_run.kf`.
- fortify: `art/units/greatpeople/engineer/engineer_md_fortify.kf`.

### military:english_redcoat

- Model: `src/Mods/Military/art/models/english_redcoat.glb` (372,348 bytes).
- Art: `ART_DEF_UNIT_ENGLISH_REDCOAT`. Source scale: 0.44.
- NIF: `art/units/unique/england/redcoat/redcoat.nif`.
- KFM: `art/units/unique/england/redcoat/redcoat.kfm`.
- Geometry: 772 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2058, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `3f247d97a9717bddc566dd3c4eeef35b58a0bbed8c6f133fb9febb5d732dd7da`.
- idle: `art/units/unique/england/redcoat/redcoat_md_idle.kf`.
- run: `art/units/unique/england/redcoat/redcoat_md_run.kf`.
- fortify: `art/units/unique/england/redcoat/redcoat_md_rangedfortify.kf`.
- attack: `art/units/unique/england/redcoat/redcoat_md_rangedstrike.kf`.
- hurt: `art/units/unique/england/redcoat/redcoat_md_hurta.kf`.
- die: `art/units/unique/england/redcoat/redcoat_md_rangeddiea.kf`.

### military:envoy

- Model: `src/Mods/Military/art/models/envoy.glb` (309,992 bytes).
- Art: `ART_DEF_UNIT_ENVOY`. Source scale: 0.46.
- NIF: `art/units/envoy/european/master_tobacconist.nif`.
- KFM: `art/units/envoy/european/master_tobacconist.kfm`.
- Geometry: 708 triangles, 1 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2843, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `76087385fd78ad6d3a42db32a74a40efb075b2eae62e9b8b237c630829e39186`.
- idle: `art/units/envoy/european/master_tobacconist_md_idle.kf`.
- run: `art/units/envoy/european/master_tobacconist_md_run.kf`.
- fortify: `art/units/envoy/european/master_tobacconist_md_fortifya.kf`.
- attack: `art/units/envoy/european/master_tobacconist_md_strikea.kf`.
- hurt: `art/units/envoy/european/master_tobacconist_md_hurta.kf`.
- die: `art/units/envoy/european/master_tobacconist_md_diea.kf`.

### military:ethiopian_oromo_warrior

- Model: `src/Mods/Military/art/models/ethiopian_oromo_warrior.glb` (334,768 bytes).
- Art: `ART_DEF_UNIT_ETHIOPIAN_OROMO_WARRIOR_ORANGE`. Source scale: 0.44.
- NIF: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_orange.nif`.
- KFM: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior.kfm`.
- Geometry: 834 triangles, 2 skins, 22 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2533, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels), die (55 channels).
- SHA-256: `1786d9f71a876941dddeb8f335a012e474685c8e2a3f1a1111c74616239a2f5c`.
- idle: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_idle.kf`.
- run: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_run.kf`.
- fortify: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangedfortify.kf`.
- attack: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangedstrike.kf`.
- die: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangeddiea.kf`.
- Note: First of 3 formation art groups exported

### military:executive_1

- Model: `src/Mods/Military/art/models/executive_1.glb` (259,172 bytes).
- Art: `ART_DEF_UNIT_CEO_1`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_cereal.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `5d50c99f76ae86dc38c72141ea9da8360eb066694c2fa9b2ee6b922bad80c759`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_2

- Model: `src/Mods/Military/art/models/executive_2.glb` (259,960 bytes).
- Art: `ART_DEF_UNIT_CEO_2`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_sushi.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `40324d4800337ee3379e37dd38b2bc39c44ba67723093f953a70aff2f444c03d`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_3

- Model: `src/Mods/Military/art/models/executive_3.glb` (259,036 bytes).
- Art: `ART_DEF_UNIT_CEO_3`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_ethanol.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `2a3d193c3cc8965b922da1a26fc5855ad4ad56a753d90e046771dd22261b8373`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_4

- Model: `src/Mods/Military/art/models/executive_4.glb` (259,132 bytes).
- Art: `ART_DEF_UNIT_CEO_4`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_construction.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `cc7212fdd01e93cd99b8ae0b91f3afc87fac287178f1b893a3b8668981f27814`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_5

- Model: `src/Mods/Military/art/models/executive_5.glb` (258,848 bytes).
- Art: `ART_DEF_UNIT_CEO_5`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_mining.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `5b1c2f8da938815d8264a31892bcde1d2ec008754096dea63defbb96e723a617`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_6

- Model: `src/Mods/Military/art/models/executive_6.glb` (258,940 bytes).
- Art: `ART_DEF_UNIT_CEO_6`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_aluminum.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `fbd388f17da76b67f01acdebd5ee73d248826e4f4538afe4db6fbdb20dc84d98`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_7

- Model: `src/Mods/Military/art/models/executive_7.glb` (259,268 bytes).
- Art: `ART_DEF_UNIT_CEO_7`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_jewelry.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `d018db974449eaaebfd779a13657261f71a743fa865d52ab109b741d9027848f`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_adventure

- Model: `src/Mods/Military/art/models/executive_adventure.glb` (259,428 bytes).
- Art: `ART_DEF_UNIT_CEO_ADVENTURE`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_adventure.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `c208aa634002c3cba9dfbf0d0941e5dfb0ef042a9c705b2236ecec26ea9145c5`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_apotheguild

- Model: `src/Mods/Military/art/models/executive_apotheguild.glb` (230,316 bytes).
- Art: `ART_DEF_UNIT_APOTHMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `09602758d481bc1a7781c1e5675dba61ed83db9517514a27edca66015e6189ab`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_artistguild

- Model: `src/Mods/Military/art/models/executive_artistguild.glb` (230,316 bytes).
- Art: `ART_DEF_UNIT_ARTISTMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `09602758d481bc1a7781c1e5675dba61ed83db9517514a27edca66015e6189ab`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_bakerguild

- Model: `src/Mods/Military/art/models/executive_bakerguild.glb` (229,352 bytes).
- Art: `ART_DEF_UNIT_BAKERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `daf5b77e090326e3da394160be7dbbf83e8b29b0e59907b1a94a4e40eb5f4729`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_blacksmguild

- Model: `src/Mods/Military/art/models/executive_blacksmguild.glb` (228,332 bytes).
- Art: `ART_DEF_UNIT_BLACKSMITHMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `31a87664e94ce45c9616d264dae41d1d1cf24e8fc6aed84f4c3fff4dc8e092a1`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_burgerworld

- Model: `src/Mods/Military/art/models/executive_burgerworld.glb` (260,208 bytes).
- Art: `ART_DEF_UNIT_CEO_BURGERWORLD`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_bw.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `68c553f23b83eb518a271333d057c9cd696765135b07e2b1912c80337174a67d`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_butcherguild

- Model: `src/Mods/Military/art/models/executive_butcherguild.glb` (229,352 bytes).
- Art: `ART_DEF_UNIT_BUTCHERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `daf5b77e090326e3da394160be7dbbf83e8b29b0e59907b1a94a4e40eb5f4729`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_carpenterguild

- Model: `src/Mods/Military/art/models/executive_carpenterguild.glb` (228,332 bytes).
- Art: `ART_DEF_UNIT_CARPENTERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `31a87664e94ce45c9616d264dae41d1d1cf24e8fc6aed84f4c3fff4dc8e092a1`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_clerksguild

- Model: `src/Mods/Military/art/models/executive_clerksguild.glb` (230,316 bytes).
- Art: `ART_DEF_UNIT_CLERKSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `09602758d481bc1a7781c1e5675dba61ed83db9517514a27edca66015e6189ab`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_drapersguild

- Model: `src/Mods/Military/art/models/executive_drapersguild.glb` (230,316 bytes).
- Art: `ART_DEF_UNIT_DRAPERSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `09602758d481bc1a7781c1e5675dba61ed83db9517514a27edca66015e6189ab`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_empcloth

- Model: `src/Mods/Military/art/models/executive_empcloth.glb` (258,120 bytes).
- Art: `ART_DEF_UNIT_CEO_EMPCLOTH`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_ec.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `f2fc6d6db6b9d566e9b35efbf3a0053a8d23d8edc7fd6b0e07697ae475886c1a`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_equestguild

- Model: `src/Mods/Military/art/models/executive_equestguild.glb` (230,316 bytes).
- Art: `ART_DEF_UNIT_EQUESTMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `09602758d481bc1a7781c1e5675dba61ed83db9517514a27edca66015e6189ab`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_ewtrade

- Model: `src/Mods/Military/art/models/executive_ewtrade.glb` (259,428 bytes).
- Art: `ART_DEF_UNIT_CEO_EWTRADE`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_ew.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `1f4dae84be3fb130fb04c899bd94f0aa624334312fd2a628042c9fc76a6c4db1`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_fashion

- Model: `src/Mods/Military/art/models/executive_fashion.glb` (257,608 bytes).
- Art: `ART_DEF_UNIT_CEO_FASHION`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_fashion.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `02c561a31c0bb3022802559ed478ea5e7942c64925292da586b9b3895e79cd1c`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_fishmonguild

- Model: `src/Mods/Military/art/models/executive_fishmonguild.glb` (229,352 bytes).
- Art: `ART_DEF_UNIT_FISHMONGERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `daf5b77e090326e3da394160be7dbbf83e8b29b0e59907b1a94a4e40eb5f4729`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_glassguild

- Model: `src/Mods/Military/art/models/executive_glassguild.glb` (228,332 bytes).
- Art: `ART_DEF_UNIT_GLASSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `31a87664e94ce45c9616d264dae41d1d1cf24e8fc6aed84f4c3fff4dc8e092a1`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_goldsmguild

- Model: `src/Mods/Military/art/models/executive_goldsmguild.glb` (228,332 bytes).
- Art: `ART_DEF_UNIT_GOLDSMITHMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `31a87664e94ce45c9616d264dae41d1d1cf24e8fc6aed84f4c3fff4dc8e092a1`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_grocerguild

- Model: `src/Mods/Military/art/models/executive_grocerguild.glb` (229,352 bytes).
- Art: `ART_DEF_UNIT_GROCERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `daf5b77e090326e3da394160be7dbbf83e8b29b0e59907b1a94a4e40eb5f4729`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_mallwart

- Model: `src/Mods/Military/art/models/executive_mallwart.glb` (257,344 bytes).
- Art: `ART_DEF_UNIT_CEO_MALLWART`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_mw.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `f22990acff85333b88a90299817aee6886b3ecf7c372dac4feef09c1ff97c6e1`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_mapster

- Model: `src/Mods/Military/art/models/executive_mapster.glb` (257,580 bytes).
- Art: `ART_DEF_UNIT_CEO_MAPSTER`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_marnzmotor.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `5daffb93cc25cf35ce8e1b033b3bc024cb487b1d1232bece380a40cc75a918f6`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_masonguild

- Model: `src/Mods/Military/art/models/executive_masonguild.glb` (228,332 bytes).
- Art: `ART_DEF_UNIT_MASONSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `31a87664e94ce45c9616d264dae41d1d1cf24e8fc6aed84f4c3fff4dc8e092a1`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_mercerguild

- Model: `src/Mods/Military/art/models/executive_mercerguild.glb` (229,352 bytes).
- Art: `ART_DEF_UNIT_MERCERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `daf5b77e090326e3da394160be7dbbf83e8b29b0e59907b1a94a4e40eb5f4729`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_milehigh

- Model: `src/Mods/Military/art/models/executive_milehigh.glb` (259,076 bytes).
- Art: `ART_DEF_UNIT_CEO_MILEHIGH`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_mh.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `20912f2c6923a4fdd92b66a4caa977c35de87bf07b091b976f10a29d2a72ed2d`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_mobby

- Model: `src/Mods/Military/art/models/executive_mobby.glb` (260,236 bytes).
- Art: `ART_DEF_UNIT_CEO_MOBBY`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_mobby.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `3aa297ef70cca9e1641543ab3a903f74a7697c57c30e0bb295cae79c12cfd26a`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_red

- Model: `src/Mods/Military/art/models/executive_red.glb` (258,560 bytes).
- Art: `ART_DEF_UNIT_CEO_RED`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_rc.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `a7f78c4c55480ba7231ff3e25cf8a22c541a5e2375bae8098e18abc9aa8cf7bb`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_safari

- Model: `src/Mods/Military/art/models/executive_safari.glb` (259,532 bytes).
- Art: `ART_DEF_UNIT_CEO_SAFARI`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_safari.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `93a4f9e8301d5c851e015cafc5b407d0bb79b76018b06d723e2dfd07138f2caf`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_ultsoldier

- Model: `src/Mods/Military/art/models/executive_ultsoldier.glb` (259,448 bytes).
- Art: `ART_DEF_UNIT_CEO_ULTSOLDIER`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_soldier.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `05b1499cb3b2847ce8b411d3369143eb5fe1be66ff6532076c8d0845cf7cf74b`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_wonka

- Model: `src/Mods/Military/art/models/executive_wonka.glb` (259,156 bytes).
- Art: `ART_DEF_UNIT_CEO_WONKA`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_wonka.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `4eca534c45710065dd75aff6ec695d1ece7dd232a9a24ca1e7398c0deb5e2dec`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:experimental

- Model: `src/Mods/Military/art/models/experimental.glb` (68,736 bytes).
- Art: `ART_DEF_UNIT_EXPERIMENTAL`. Source scale: 1.4.
- NIF: `art/units/nukes/peacemaker/icbm.nif`.
- KFM: `art/units/nukes/peacemaker/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `2225a2301acb779b5ea5f68437ac5222fed29a4ca30d05b7f85c91c36f836d17`.
- idle: `art/units/nukes/peacemaker/icbm_md_idle.kf`.
- run: `art/units/nukes/peacemaker/icbm_md_run.kf`.
- attack: `art/units/nukes/peacemaker/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:explorer

- Model: `src/Mods/Military/art/models/explorer.glb` (305,984 bytes).
- Art: `ART_DEF_UNIT_EXPLORER`. Source scale: 0.44.
- NIF: `art/units/explorer/explorer.nif`.
- KFM: `art/units/explorer/explorer.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2503, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `c3e86aed610d5ba46e2908a376bbe31635d92fd2643e8fb98cab3bfcfb846956`.
- idle: `art/units/explorer/explorer_md_idle.kf`.
- run: `art/units/explorer/explorer_md_run.kf`.
- fortify: `art/units/explorer/explorer_md_fortifya.kf`.
- attack: `art/units/explorer/explorer_md_strikea.kf`.
- hurt: `art/units/explorer/explorer_md_hurta.kf`.
- die: `art/units/explorer/explorer_md_diea.kf`.

### military:f117

- Model: `src/Mods/Military/art/models/f117.glb` (154,456 bytes).
- Art: `ART_DEF_UNIT_F117`. Source scale: 0.42.
- NIF: `art/units/stealth fighter/f117.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 402 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.1187, radius 0.4800, feet Y=0.
- Clips: idle (34 channels), run (34 channels), hurt (34 channels).
- SHA-256: `e9a84a36e2329cb7e27f2343331880535bf742792272b989e68e2c140c3877be`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:f15

- Model: `src/Mods/Military/art/models/f15.glb` (249,696 bytes).
- Art: `ART_DEF_UNIT_STRIKE_FIGHTER_F15`. Source scale: 0.48.
- NIF: `art/units/strike fighter/american/f15.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 1205 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.2219, radius 0.4800, feet Y=0.
- Clips: idle (34 channels), run (34 channels), hurt (34 channels).
- SHA-256: `c8ec8138b9123cabbabcac7fa024fad5fba79590a902d00917e62dd20560ec92`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:feral

- Model: `src/Mods/Military/art/models/feral.glb` (520,340 bytes).
- Art: `ART_DEF_UNIT_CYBORG2`. Source scale: 0.44.
- NIF: `art/units/super soldier/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 1499 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2610, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `c61df8396eb91436ab2648bcd08b1aee41115879fe65b32e6f75a55500088260`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.
- Note: Missing texture: //cca.dds

### military:ff_destroyer_i

- Model: `src/Mods/Military/art/models/ff_destroyer_i.glb` (241,412 bytes).
- Art: `ART_DEF_UNIT_FF_DESTROYER_I`. Source scale: 0.45.
- NIF: `art/units/finalfrontier_destroyer/destroyer.nif`.
- KFM: `art/units/finalfrontier_destroyer/destroyer.kfm`.
- Geometry: 983 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2620, radius 0.4800, feet Y=0.
- Clips: idle (11 channels), run (11 channels), fortify (11 channels), attack (11 channels), hurt (11 channels), die (11 channels).
- SHA-256: `02075afa26176afb728f8614ef0c47d13e822c337be86a1119e65202c9a9ccdd`.
- idle: `art/units/finalfrontier_destroyer/destroyer_md_idle.kf`.
- run: `art/units/finalfrontier_destroyer/destroyer_md_run.kf`.
- fortify: `art/units/finalfrontier_destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/finalfrontier_destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/finalfrontier_destroyer/destroyer_md_hurta.kf`.
- die: `art/units/finalfrontier_destroyer/destroyer_md_diea.kf`.

### military:fighter

- Model: `src/Mods/Military/art/models/fighter.glb` (222,868 bytes).
- Art: `ART_DEF_UNIT_FIGHTER`. Source scale: 0.5.
- NIF: `art/units/fighter/fighter.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 532 triangles, 4 skins, 8 joint bindings, 1 textures.
- Bounds: height 0.3238, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), hurt (45 channels), die (45 channels).
- SHA-256: `735ec59b6f64f8ed61c5fd0809e9c165ae47102f52e394d56adf5a54c1844dca`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:fluyt

- Model: `src/Mods/Military/art/models/fluyt.glb` (300,800 bytes).
- Art: `ART_DEF_UNIT_FLUYT`. Source scale: 0.12.
- NIF: `art/units/galleon/galleon.nif`.
- KFM: `art/units/galleon/galleon.kfm`.
- Geometry: 1023 triangles, 3 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2871, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels), hurt (42 channels), die (42 channels).
- SHA-256: `837c12d52c290c5bd62d69089e849906dcdb627a6cf393da5627605d170400c8`.
- idle: `art/units/galleon/galleon_md_idle.kf`.
- run: `art/units/galleon/galleon_md_run.kf`.
- fortify: `art/units/galleon/galleon_md_fortifya.kf`.
- attack: `art/units/galleon/galleon_md_strikea.kf`.
- hurt: `art/units/galleon/galleon_md_hurta.kf`.
- die: `art/units/galleon/galleon_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:fokkerdr1

- Model: `src/Mods/Military/art/models/fokkerdr1.glb` (212,292 bytes).
- Art: `ART_DEF_UNIT_BIPLANE`. Source scale: 0.44.
- NIF: `art/units/earlyfighter/pup.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 1080 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.3736, radius 0.4800, feet Y=0.
- Clips: idle (37 channels), run (37 channels), hurt (37 channels), die (37 channels).
- SHA-256: `8ffb1f4cea3bba446b60f78783c105e64f9b3ab414b8dbf129bcbfa7d3ca9ff4`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:freight

- Model: `src/Mods/Military/art/models/freight.glb` (58,616 bytes).
- Art: `ART_DEF_UNIT_FREIGHT`. Source scale: 0.35.
- NIF: `art/units/freight/opel.nif`.
- KFM: `art/units/tank/tank.kfm`.
- Geometry: 314 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.4560, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels).
- SHA-256: `e46aafff97fcb00fa88679260b06feead9746a5352c6c664f56269d62a8706c1`.
- idle: `art/units/tank/tank_md_idle.kf`.
- run: `art/units/tank/tank_md_run.kf`.
- fortify: `art/units/tank/tank_md_rangedfortify.kf`.
- attack: `art/units/tank/tank_md_rangedstrike.kf`.
- hurt: `art/units/tank/tank_md_hurta.kf`.

### military:french_musketeer

- Model: `src/Mods/Military/art/models/french_musketeer.glb` (420,764 bytes).
- Art: `ART_DEF_UNIT_FRENCH_MUSKETEER`. Source scale: 0.44.
- NIF: `art/units/unique/french/musketeer/musketeer.nif`.
- KFM: `art/units/unique/french/musketeer/musketeer.kfm`.
- Geometry: 718 triangles, 2 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2631, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels), hurt (61 channels), die (61 channels).
- SHA-256: `4f4c75f119bf16fabbeee190f78e95f190a2d92f0219a6db9361491f77048501`.
- idle: `art/units/unique/french/musketeer/musketeer_md_idle.kf`.
- run: `art/units/unique/french/musketeer/musketeer_md_run.kf`.
- fortify: `art/units/unique/french/musketeer/musketeer_md_rangedfortify.kf`.
- attack: `art/units/unique/french/musketeer/musketeer_md_rangedstrike.kf`.
- hurt: `art/units/unique/french/musketeer/musketeer_md_hurta.kf`.
- die: `art/units/unique/french/musketeer/musketeer_md_rangeddiea.kf`.

### military:frigate

- Model: `src/Mods/Military/art/models/frigate.glb` (311,616 bytes).
- Art: `ART_DEF_UNIT_FRIGATE`. Source scale: 0.24.
- NIF: `art/units/frigate/frigate.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1174 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4001, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `cb2529a8c7cb68da9f5aeeeb49dedeb32af724021a5c9c42da3eb57aaf0d1a69`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:fusion_battleship

- Model: `src/Mods/Military/art/models/fusion_battleship.glb` (320,036 bytes).
- Art: `ART_DEF_UNIT_FF_BATTLESHIP_I`. Source scale: 0.21.
- NIF: `art/units/finalfrontier_battleship/battleship.nif`.
- KFM: `art/units/finalfrontier_battleship/battleship.kfm`.
- Geometry: 1456 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2224, radius 0.4800, feet Y=0.
- Clips: idle (21 channels), run (21 channels), fortify (21 channels), attack (21 channels), hurt (21 channels), die (21 channels).
- SHA-256: `6ea5d14ff3f81b76e4320dc95ec0f0123e3f5c479dbdc8e40820352b0a5ea68d`.
- idle: `art/units/finalfrontier_battleship/battleship_md_idle.kf`.
- run: `art/units/finalfrontier_battleship/battleship_md_run.kf`.
- fortify: `art/units/finalfrontier_battleship/battleship_md_fortifya.kf`.
- attack: `art/units/finalfrontier_battleship/battleship_md_strikea.kf`.
- hurt: `art/units/finalfrontier_battleship/battleship_md_hurta.kf`.
- die: `art/units/finalfrontier_battleship/battleship_md_diea.kf`.

### military:fusion_carrier

- Model: `src/Mods/Military/art/models/fusion_carrier.glb` (414,448 bytes).
- Art: `ART_DEF_UNIT_FF_CARRIER_I`. Source scale: 0.21.
- NIF: `art/units/finalfrontier_carrier/carrier.nif`.
- KFM: `art/units/finalfrontier_carrier/carrier.kfm`.
- Geometry: 1874 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.4454, radius 0.4800, feet Y=0.
- Clips: idle (31 channels), run (31 channels), fortify (31 channels), attack (31 channels), hurt (31 channels), die (31 channels).
- SHA-256: `55a3b2dd6398a9c5c31ba516713e0adf8b4f7ea9798ca3b497f5c8f34449be0b`.
- idle: `art/units/finalfrontier_carrier/carrier_md_idle.kf`.
- run: `art/units/finalfrontier_carrier/carrier_md_run.kf`.
- fortify: `art/units/finalfrontier_carrier/carrier_md_fortifya.kf`.
- attack: `art/units/finalfrontier_carrier/carrier_md_strikea.kf`.
- hurt: `art/units/finalfrontier_carrier/carrier_md_hurta.kf`.
- die: `art/units/finalfrontier_carrier/carrier_md_diea.kf`.

### military:fusion_cruiser

- Model: `src/Mods/Military/art/models/fusion_cruiser.glb` (289,176 bytes).
- Art: `ART_DEF_UNIT_FF_CRUISER_I`. Source scale: 0.25.
- NIF: `art/units/finalfrontier_cruiser/cruiser.nif`.
- KFM: `art/units/finalfrontier_cruiser/cruiser.kfm`.
- Geometry: 1046 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2745, radius 0.4800, feet Y=0.
- Clips: idle (17 channels), run (17 channels), fortify (17 channels), attack (17 channels), hurt (17 channels), die (17 channels).
- SHA-256: `0e3404f8c905ecfdf375d63a7c631cf164ed984eae687361cfbcb5636b95b4bb`.
- idle: `art/units/finalfrontier_cruiser/cruiser_md_idle.kf`.
- run: `art/units/finalfrontier_cruiser/cruiser_md_run.kf`.
- fortify: `art/units/finalfrontier_cruiser/cruiser_md_fortifya.kf`.
- attack: `art/units/finalfrontier_cruiser/cruiser_md_strikea.kf`.
- hurt: `art/units/finalfrontier_cruiser/cruiser_md_hurta.kf`.
- die: `art/units/finalfrontier_cruiser/cruiser_md_diea.kf`.

### military:fusion_nova

- Model: `src/Mods/Military/art/models/fusion_nova.glb` (74,836 bytes).
- Art: `ART_DEF_UNIT_FUSION_NOVA`. Source scale: 1.
- NIF: `art/units/nukes/fusionbombs/icbm.nif`.
- KFM: `art/units/nukes/fusionbombs/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `31d8abd6658bf843d9359db868fae259d3f51c5731a1ee11d128607e6d857e73`.
- idle: `art/units/nukes/fusionbombs/icbm_md_idle.kf`.
- run: `art/units/nukes/fusionbombs/icbm_md_run.kf`.
- attack: `art/units/nukes/fusionbombs/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:fusion_nuke

- Model: `src/Mods/Military/art/models/fusion_nuke.glb` (74,836 bytes).
- Art: `ART_DEF_UNIT_FUSION_NUKE`. Source scale: 0.62.
- NIF: `art/units/nukes/fusionbombs/icbm.nif`.
- KFM: `art/units/nukes/fusionbombs/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `31d8abd6658bf843d9359db868fae259d3f51c5731a1ee11d128607e6d857e73`.
- idle: `art/units/nukes/fusionbombs/icbm_md_idle.kf`.
- run: `art/units/nukes/fusionbombs/icbm_md_run.kf`.
- attack: `art/units/nukes/fusionbombs/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:fusion_submarine

- Model: `src/Mods/Military/art/models/fusion_submarine.glb` (217,324 bytes).
- Art: `ART_DEF_UNIT_FUSION_SUBMARINE`. Source scale: 0.60.
- NIF: `art/units/fusion submarine/submarine.nif`.
- KFM: `art/units/fusion submarine/submarine.kfm`.
- Geometry: 568 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.1884, radius 0.4800, feet Y=0.
- Clips: idle (34 channels), run (34 channels), fortify (34 channels), attack (34 channels), hurt (34 channels), die (34 channels).
- SHA-256: `4cbcbee06768c103785a6381fa7f73c413976f1a5c9ffa8c63dacf44c691fe5d`.
- idle: `art/units/fusion submarine/submarine_md_idle.kf`.
- run: `art/units/fusion submarine/submarine_md_run.kf`.
- fortify: `art/units/fusion submarine/submarine_md_fortifya.kf`.
- attack: `art/units/fusion submarine/submarine_md_strikea.kf`.
- hurt: `art/units/fusion submarine/submarine_md_hurta.kf`.
- die: `art/units/fusion submarine/submarine_md_diea.kf`.

### military:fusion_transport

- Model: `src/Mods/Military/art/models/fusion_transport.glb` (202,824 bytes).
- Art: `ART_DEF_UNIT_FF_INVASION_SHIP_I`. Source scale: 0.5.
- NIF: `art/units/finalfrontier_invasionship/invasionship.nif`.
- KFM: `art/units/finalfrontier_invasionship/invasionship.kfm`.
- Geometry: 641 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2336, radius 0.4800, feet Y=0.
- Clips: idle (13 channels), run (13 channels), fortify (13 channels), attack (13 channels), hurt (13 channels), die (13 channels).
- SHA-256: `53fd4489b85421b1a9a41a1fd8c3f588e2ae530e6fd640d12c0504eed7fa6734`.
- idle: `art/units/finalfrontier_invasionship/invasionship_md_idle.kf`.
- run: `art/units/finalfrontier_invasionship/invasionship_md_run.kf`.
- fortify: `art/units/finalfrontier_invasionship/invasionship_md_fortifya.kf`.
- attack: `art/units/finalfrontier_invasionship/invasionship_md_strikea.kf`.
- hurt: `art/units/finalfrontier_invasionship/invasionship_md_hurta.kf`.
- die: `art/units/finalfrontier_invasionship/invasionship_md_diea.kf`.

### military:future_armor

- Model: `src/Mods/Military/art/models/future_armor.glb` (100,468 bytes).
- Art: `ART_DEF_UNIT_FUTURE_ARMOR`. Source scale: 1.3.
- NIF: `art/units/hover tank/ftank.nif`.
- KFM: `art/units/modernarmor/modernarmor.kfm`.
- Geometry: 614 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.2573, radius 0.4800, feet Y=0.
- Clips: none.
- SHA-256: `144d5e37e48b653cf57c86c47465308c79608e3ad3ca62c90e7e585b7afd3db3`.
- Note: idle: no matching transform tracks
- Note: run: no matching transform tracks
- Note: fortify: no matching transform tracks
- Note: attack: no matching transform tracks
- Note: hurt: no matching transform tracks

### military:galleas

- Model: `src/Mods/Military/art/models/galleas.glb` (325,620 bytes).
- Art: `ART_DEF_UNIT_GALLEAS`. Source scale: 0.10.
- NIF: `art/venice/galleas/galley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 1444 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3701, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `5e412c86a456e0756efb0a7a312a9edf00d6ff0ffcd3c11bb6d724f0f8f70a53`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.
- Note: Missing texture: //frigate.dds

### military:galleass

- Model: `src/Mods/Military/art/models/galleass.glb` (320,608 bytes).
- Art: `ART_DEF_UNIT_GALLEASS`. Source scale: 0.14.
- NIF: `art/units/galleas/galley.nif`.
- KFM: `art/units/galleas/galley.kfm`.
- Geometry: 1444 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3701, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (44 channels), hurt (50 channels), die (50 channels).
- SHA-256: `4917811587a90ca5ebf96900a572df047343c25b3e0e8dedbbdab319d0e33402`.
- idle: `art/units/galleas/galley_md_idle.kf`.
- run: `art/units/galleas/galley_md_run.kf`.
- fortify: `art/units/galleas/galley_md_fortifya.kf`.
- attack: `art/units/galleas/galley_md_strikea.kf`.
- hurt: `art/units/galleas/galley_md_hurta.kf`.
- die: `art/units/galleas/galley_md_diea.kf`.
- Note: Missing texture: \\frigate.dds

### military:galleon

- Model: `src/Mods/Military/art/models/galleon.glb` (300,800 bytes).
- Art: `ART_DEF_UNIT_GALLEON`. Source scale: 0.2.
- NIF: `art/units/galleon/galleon.nif`.
- KFM: `art/units/galleon/galleon.kfm`.
- Geometry: 1023 triangles, 3 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2871, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels), hurt (42 channels), die (42 channels).
- SHA-256: `837c12d52c290c5bd62d69089e849906dcdb627a6cf393da5627605d170400c8`.
- idle: `art/units/galleon/galleon_md_idle.kf`.
- run: `art/units/galleon/galleon_md_run.kf`.
- fortify: `art/units/galleon/galleon_md_fortifya.kf`.
- attack: `art/units/galleon/galleon_md_strikea.kf`.
- hurt: `art/units/galleon/galleon_md_hurta.kf`.
- die: `art/units/galleon/galleon_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:galley

- Model: `src/Mods/Military/art/models/galley.glb` (277,596 bytes).
- Art: `ART_DEF_UNIT_GALLEY`. Source scale: 0.18.
- NIF: `art/units/galley/galley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 897 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4308, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `dea2ae3ada46149c95a9638bf548d5b19b1f07cbe9ad755d3a7b20afc7bd05a7`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:gatling_gun

- Model: `src/Mods/Military/art/models/gatling_gun.glb` (398,592 bytes).
- Art: `ART_DEF_UNIT_GATLING_GUN`. Source scale: 0.44.
- NIF: `art/units/gatlinggun/european/machinegunner.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 1282 triangles, 7 skins, 36 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3968, feet Y=0.
- Clips: idle (78 channels), run (78 channels), fortify (78 channels), attack (78 channels), hurt (78 channels).
- SHA-256: `9797a84c267a8a871e37a24fc82ff3e57b2932369d62752014f83cf70d86cf2b`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:german_panzer

- Model: `src/Mods/Military/art/models/german_panzer.glb` (295,368 bytes).
- Art: `ART_DEF_UNIT_GERMAN_PANZER`. Source scale: 0.47.
- NIF: `art/units/unique/german/panzer/panzer.nif`.
- KFM: `art/units/unique/german/panzer/panzer.kfm`.
- Geometry: 526 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.4921, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels).
- SHA-256: `5a253d5073299d976c4ac29ac5428cb244f70053f1cb12c8c2ff3d7f816d98da`.
- idle: `art/units/unique/german/panzer/panzer_md_idle.kf`.
- run: `art/units/unique/german/panzer/panzer_md_run.kf`.
- fortify: `art/units/unique/german/panzer/panzer_md_rangedfortify.kf`.
- attack: `art/units/unique/german/panzer/panzer_md_rangedstrike.kf`.
- hurt: `art/units/unique/german/panzer/panzer_md_hurta.kf`.

### military:ggmaster

- Model: `src/Mods/Military/art/models/ggmaster.glb` (229,352 bytes).
- Art: `ART_DEF_UNIT_GGMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `daf5b77e090326e3da394160be7dbbf83e8b29b0e59907b1a94a4e40eb5f4729`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:gothic_cavalry

- Model: `src/Mods/Military/art/models/gothic_cavalry.glb` (733,280 bytes).
- Art: `ART_DEF_UNIT_GOTHIC_CAVALRY`. Source scale: 0.43.
- NIF: `art/goths/gothiccavalry/mongelkeshik.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1505 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3881, feet Y=0.
- Clips: idle (123 channels), run (123 channels), fortify (123 channels), attack (123 channels), hurt (123 channels), die (123 channels).
- SHA-256: `4ccb910844382f7118df942e7814c49b2252df6359b4356a885f8ce05f13eff5`.
- idle: `art/units/unique/mongol/keshik/mongelkeshik_md_idle.kf`.
- run: `art/units/unique/mongol/keshik/mongelkeshik_md_run.kf`.
- fortify: `art/units/unique/mongol/keshik/mongelkeshik_md_fortifya.kf`.
- attack: `art/units/unique/mongol/keshik/mongelkeshik_md_strikea.kf`.
- hurt: `art/units/unique/mongol/keshik/mongelkeshik_md_hurta.kf`.
- die: `art/units/unique/mongol/keshik/mongelkeshik_md_diea.kf`.
- Note: Missing texture: //bb2.dds

### military:great_diplomat

- Model: `src/Mods/Military/art/models/great_diplomat.glb` (282,456 bytes).
- Art: `ART_DEF_UNIT_GREAT_DIPLOMAT`. Source scale: 0.46.
- NIF: `art/units/great diplomat/engineer.nif`.
- KFM: `art/units/great diplomat/engineer.kfm`.
- Geometry: 658 triangles, 1 skins, 16 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3120, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels).
- SHA-256: `e13d09456f34a13f2d3b58a5607eebd5d4a6f041c848e83ec50d3613f72111b8`.
- idle: `art/units/great diplomat/engineer_md_idle.kf`.
- run: `art/units/great diplomat/engineer_md_run.kf`.
- fortify: `art/units/great diplomat/engineer_md_fortify.kf`.

### military:great_general

- Model: `src/Mods/Military/art/models/great_general.glb` (369,372 bytes).
- Art: `ART_DEF_UNIT_WARLORD_ANCIENT`. Source scale: 0.5.
- NIF: `art/units/warlord_ancient/warlord_ancient.nif`.
- KFM: `art/units/warlord_ancient/warlord_ancient.kfm`.
- Geometry: 806 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.5845, radius 0.4800, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels).
- SHA-256: `d9634cb6dd2304271e60f2a63361f00fb6ad2cb0612a2ddfb5dbcf6f80da90ad`.
- idle: `art/units/warlord_ancient/warlord_ancient_md_idle.kf`.
- run: `art/units/warlord_ancient/warlord_ancient_md_run.kf`.
- fortify: `art/units/warlord_ancient/warlord_ancient_md_fortify.kf`.

### military:great_spy

- Model: `src/Mods/Military/art/models/great_spy.glb` (454,636 bytes).
- Art: `ART_DEF_UNIT_ANCIENT_GREAT_SPY`. Source scale: 0.46.
- NIF: `art/units/great_spy_ancient/great_spy_ancient.nif`.
- KFM: `art/units/great_spy_ancient/great_spy_ancient.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3475, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels).
- SHA-256: `3b47ede0e0c2acf027e9e00feeeb0084d52b7603262352d0a636d4711fc9bbd6`.
- idle: `art/units/great_spy_ancient/great_spy_ancient_md_idle.kf`.
- run: `art/units/great_spy_ancient/great_spy_ancient_md_run.kf`.
- fortify: `art/units/great_spy_ancient/great_spy_ancient_md_fortify.kf`.
- attack: `art/units/great_spy_ancient/great_spy_ancient_md_rangedstrike.kf`.

### military:greek_phalanx

- Model: `src/Mods/Military/art/models/greek_phalanx.glb` (317,388 bytes).
- Art: `ART_DEF_UNIT_GREEK_PHALANX`. Source scale: 0.44.
- NIF: `art/units/spearman/spearman.nif`.
- KFM: `art/units/spearman/spearman.kfm`.
- Geometry: 708 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2898, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), attack (56 channels), hurt (56 channels), die (56 channels).
- SHA-256: `3bee9e454dc1b4e41466a0a443a72218577099cfa6e5f176c53ffeab0f905d9e`.
- idle: `art/units/spearman/spearman_md_idle.kf`.
- run: `art/units/spearman/spearman_md_run.kf`.
- fortify: `art/units/spearman/spearman_md_fortifya.kf`.
- attack: `art/units/spearman/spearman_md_strikea.kf`.
- hurt: `art/units/spearman/spearman_md_hurta.kf`.
- die: `art/units/spearman/spearman_md_diea.kf`.

### military:grenadier

- Model: `src/Mods/Military/art/models/grenadier.glb` (429,448 bytes).
- Art: `ART_DEF_UNIT_GRENADIER`. Source scale: 0.44.
- NIF: `art/units/grenadier/grenadier.nif`.
- KFM: `art/units/grenadier/grenadier.kfm`.
- Geometry: 749 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2531, feet Y=0.
- Clips: idle (64 channels), run (64 channels), fortify (64 channels), attack (64 channels), hurt (64 channels), die (64 channels).
- SHA-256: `87eb4fbd94bd3f71e3b76dc332db035e30a363353e0a7b4f1091c556934ff46a`.
- idle: `art/units/grenadier/grenadier_md_idle.kf`.
- run: `art/units/grenadier/grenadier_md_run.kf`.
- fortify: `art/units/grenadier/grenadier_md_fortifya.kf`.
- attack: `art/units/grenadier/grenadier_md_strikea.kf`.
- hurt: `art/units/grenadier/grenadier_md_hurta.kf`.
- die: `art/units/grenadier/grenadier_md_diea.kf`.

### military:guided_missile

- Model: `src/Mods/Military/art/models/guided_missile.glb` (80,584 bytes).
- Art: `ART_DEF_UNIT_GUIDED_MISSILE`. Source scale: 0.2.
- NIF: `art/units/guided_missile/guided_missile.nif`.
- KFM: `art/units/guided_missile/guided_missile.kfm`.
- Geometry: 146 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.1889, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), attack (20 channels).
- SHA-256: `21478ddbb251a80bda76da5d410b5a79561461939aa17cbdbd32318a3d7fb4a2`.
- idle: `art/units/guided_missile/guided_missile_md_idle.kf`.
- run: `art/units/guided_missile/guided_missile_md_run.kf`.
- attack: `art/units/guided_missile/guided_missile_md_strikea.kf`.

### military:gunship

- Model: `src/Mods/Military/art/models/gunship.glb` (282,876 bytes).
- Art: `ART_DEF_UNIT_GUNSHIP`. Source scale: 0.36.
- NIF: `art/units/gunship/gunship.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 1174 triangles, 2 skins, 7 joint bindings, 2 textures.
- Bounds: height 0.2070, radius 0.4800, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels), hurt (42 channels).
- SHA-256: `9f9c79b65678d8878ea1250d20a7dd90001e9c015ec47acb98ff404e02d051ec`.
- idle: `art/units/gunship/gunship_md_idle.kf`.
- run: `art/units/gunship/gunship_md_run.kf`.
- fortify: `art/units/gunship/gunship_md_rangedfortify.kf`.
- attack: `art/units/gunship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/gunship/gunship_md_hurta.kf`.

### military:hacker

- Model: `src/Mods/Military/art/models/hacker.glb` (180,200 bytes).
- Art: `ART_DEF_UNIT_HACKER`. Source scale: 0.46.
- NIF: `art/units/hacker/modernscientist.nif`.
- KFM: `art/units/hacker/modernscientist.kfm`.
- Geometry: 568 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2621, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels).
- SHA-256: `99ead7cd1b94168de2ad6d32f66858fd4e6c00a0dc79e068204559e7e08d23a4`.
- idle: `art/units/hacker/modernscientist_md_idle.kf`.
- run: `art/units/greatpeople/modernscientist/modernscientist_md_run.kf`.
- fortify: `art/units/hacker/modernscientist_md_fortify.kf`.

### military:halftrack

- Model: `src/Mods/Military/art/models/halftrack.glb` (394,492 bytes).
- Art: `ART_DEF_UNIT_HALFTRACK`. Source scale: 0.6.
- NIF: `art/units/antiair halftrack/ma_ht.nif`.
- KFM: `art/units/antiair halftrack/modernarmor.kfm`.
- Geometry: 1152 triangles, 2 skins, 6 joint bindings, 2 textures.
- Bounds: height 0.4191, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels).
- SHA-256: `5ab1c3566aea360d54ffc081513131eac4e28ebb51deb3c23c62adfd36f7a461`.
- idle: `art/units/antiair halftrack/modernarmor_md_idle.kf`.
- run: `art/units/antiair halftrack/modernarmor_md_run.kf`.
- fortify: `art/units/antiair halftrack/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/antiair halftrack/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/antiair halftrack/modernarmor_md_hurta.kf`.

### military:heavy_cavalry

- Model: `src/Mods/Military/art/models/heavy_cavalry.glb` (648,856 bytes).
- Art: `ART_DEF_UNIT_HEAVY_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavycavalry/heavycavalry.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1227 triangles, 3 skins, 57 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4075, feet Y=0.
- Clips: idle (124 channels), run (124 channels), fortify (124 channels), attack (124 channels), hurt (124 channels), die (124 channels).
- SHA-256: `a4e8d33e9219ea1bdbe2184cc1bedaf995e5c39cb454eafe4fca1d21f02f86c3`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.

### military:heavy_pikeman

- Model: `src/Mods/Military/art/models/heavy_pikeman.glb` (273,564 bytes).
- Art: `ART_DEF_UNIT_HEAVY_SPEARMAN`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavyspearman/heavyspearman.nif`.
- KFM: `art/units/charlemagne_heavyspearman/heavyspearman.kfm`.
- Geometry: 608 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.5518, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `c68a488043fef020db03d3f0f1c49ce8d1551375a2d6ba5e34ed4b36188d7d04`.
- idle: `art/units/charlemagne_heavyspearman/heavyspearman_md_idle.kf`.
- run: `art/units/charlemagne_heavyspearman/heavyspearman_md_run.kf`.
- fortify: `art/units/charlemagne_heavyspearman/heavyspearman_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavyspearman/heavyspearman_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavyspearman/heavyspearman_md_hurta.kf`.
- die: `art/units/charlemagne_heavyspearman/heavyspearman_md_diea.kf`.

### military:heavy_swordsman

- Model: `src/Mods/Military/art/models/heavy_swordsman.glb` (279,228 bytes).
- Art: `ART_DEF_UNIT_HEAVY_SWORDSMAN`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavyswordsman/heavyswordsman.nif`.
- KFM: `art/units/charlemagne_heavyswordsman/heavyswordsman.kfm`.
- Geometry: 602 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3190, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `eaf3c1c49cd885af9e7a1473cf9dd9d582c93daa011df0fdd63e01005f36a845`.
- idle: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_idle.kf`.
- run: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_run.kf`.
- fortify: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_hurta.kf`.
- die: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_diea.kf`.

### military:heavy_tank_m26

- Model: `src/Mods/Military/art/models/heavy_tank_m26.glb` (179,636 bytes).
- Art: `ART_DEF_UNIT_HEAVY_TANK`. Source scale: 0.38.
- NIF: `art/units/heavy tank/european/m26.nif`.
- KFM: `art/units/heavy tank/european/m26.kfm`.
- Geometry: 822 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.2670, radius 0.4800, feet Y=0.
- Clips: idle (13 channels), run (13 channels), fortify (13 channels), attack (13 channels), hurt (13 channels).
- SHA-256: `afa48ccb08d0e95133cd4cc05a5b242a2fda6cdcabd142cb2f1bde8dad3fe230`.
- idle: `art/units/heavy tank/european/m26_md_idle.kf`.
- run: `art/units/heavy tank/european/m26_md_run.kf`.
- fortify: `art/units/heavy tank/european/m26_md_rangedfortify.kf`.
- attack: `art/units/heavy tank/european/m26_md_rangedstrike.kf`.
- hurt: `art/units/heavy tank/european/m26_md_hurta.kf`.

### military:hellenic_missionary

- Model: `src/Mods/Military/art/models/hellenic_missionary.glb` (249,572 bytes).
- Art: `ART_DEF_UNIT_OLYMP_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/oracle/missionary_general.nif`.
- KFM: `art/units/missionary/oracle/missionary_general.kfm`.
- Geometry: 643 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2924, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), die (48 channels).
- SHA-256: `5449308758c630c655814c2d7f24dd439aa6b33560798f0f4574a89f5d0f9755`.
- idle: `art/units/missionary/oracle/missionary_general_md_idle.kf`.
- run: `art/units/missionary/oracle/missionary_general_md_run.kf`.
- fortify: `art/units/missionary/oracle/missionary_general_md_fortifya.kf`.
- die: `art/units/missionary/oracle/missionary_general_md_idle_die.kf`.

### military:highlander

- Model: `src/Mods/Military/art/models/highlander.glb` (295,964 bytes).
- Art: `ART_DEF_UNIT_HIGHLANDER`. Source scale: 0.44.
- NIF: `art/scotland/highlander/swordsman.nif`.
- KFM: `art/scotland/highlander/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3230, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), attack (56 channels), hurt (56 channels), die (56 channels).
- SHA-256: `11de6678dd076973eadbc0d289a5d4d986bff0d9d5beccd31902055740352cd5`.
- idle: `art/scotland/highlander/swordsman_md_idle.kf`.
- run: `art/scotland/highlander/swordsman_md_run.kf`.
- fortify: `art/scotland/highlander/swordsman_md_fortifya.kf`.
- attack: `art/scotland/highlander/swordsman_md_strikea.kf`.
- hurt: `art/scotland/highlander/swordsman_md_hurta.kf`.
- die: `art/scotland/highlander/swordsman_md_diea.kf`.

### military:hindu_missionary

- Model: `src/Mods/Military/art/models/hindu_missionary.glb` (262,952 bytes).
- Art: `ART_DEF_UNIT_HINDU_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/hindu/hindu_missionary.nif`.
- KFM: `art/units/missionary/hindu/hindu_missionary.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3339, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), die (54 channels).
- SHA-256: `934456373456dbad5d72ea0d24c3190760d7da913e035107a984c0cc480668f2`.
- idle: `art/units/missionary/hindu/hindu_missionary_md_idle.kf`.
- run: `art/units/missionary/hindu/hindu_missionary_md_run.kf`.
- fortify: `art/units/missionary/hindu/hindu_missionary_md_fortify.kf`.
- die: `art/units/missionary/hindu/hindu_missionary_md_idle_die.kf`.

### military:hitech_robot

- Model: `src/Mods/Military/art/models/hitech_robot.glb` (1,101,056 bytes).
- Art: `ART_DEF_UNIT_JAL`. Source scale: 0.62.
- NIF: `art/units/afterworld_gravebringer/jal/gravebringer.nif`.
- KFM: `art/units/afterworld_gravebringer/jal/gravebringer.kfm`.
- Geometry: 3438 triangles, 8 skins, 35 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.1621, feet Y=0.
- Clips: idle (67 channels), run (67 channels), fortify (67 channels), attack (67 channels), hurt (67 channels), die (67 channels).
- SHA-256: `74af3540c1c1fd21b8bea4c7982d4b93b079a0985ba58e7de35a05a5d65e2b9c`.
- idle: `art/units/afterworld_gravebringer/jal/gravebringer_md_idle.kf`.
- run: `art/units/afterworld_gravebringer/jal/gravebringer_md_run.kf`.
- fortify: `art/units/afterworld_gravebringer/jal/gravebringer_md_fortifya.kf`.
- attack: `art/units/afterworld_gravebringer/jal/gravebringer_md_strikea.kf`.
- hurt: `art/units/afterworld_gravebringer/jal/gravebringer_md_hurta.kf`.
- die: `art/units/afterworld_gravebringer/jal/gravebringer_md_diea.kf`.

### military:hittites_hittite_chariot

- Model: `src/Mods/Military/art/models/hittites_hittite_chariot.glb` (1,152,144 bytes).
- Art: `ART_DEF_UNIT_HITTITES_HITTITE_CHARIOT`. Source scale: 0.36.
- NIF: `art/units/unique/hittite chariot/egyptianwarchariot.nif`.
- KFM: `art/units/unique/egypt/warchariot/egyptianwarchariot.kfm`.
- Geometry: 2412 triangles, 6 skins, 75 joint bindings, 5 textures.
- Bounds: height 0.3800, radius 0.4800, feet Y=0.
- Clips: idle (214 channels), run (214 channels), fortify (214 channels), attack (214 channels), hurt (214 channels), die (214 channels).
- SHA-256: `e11158c2f8b039dd4f896b08546344f0cad9e161492bfd4619ef44fc578e14fc`.
- idle: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_idle.kf`.
- run: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_run.kf`.
- fortify: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_fortifya.kf`.
- attack: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_strikea.kf`.
- hurt: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_hurta.kf`.
- die: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_diea.kf`.

### military:holy_roman_landsknecht

- Model: `src/Mods/Military/art/models/holy_roman_landsknecht.glb` (349,204 bytes).
- Art: `ART_DEF_UNIT_HOLY_ROMAN_LANDSKNECHT`. Source scale: 0.44.
- NIF: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht.nif`.
- KFM: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht.kfm`.
- Geometry: 920 triangles, 2 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.5340, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `2ae6a3d52f6543a7d36b86938cfd3b36d763302b06ecded867a4c24b8db5f67a`.
- idle: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_idle.kf`.
- run: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_run.kf`.
- fortify: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_fortifya.kf`.
- attack: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_strikea.kf`.
- hurt: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_hurta.kf`.
- die: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_diea.kf`.

### military:horse_archer

- Model: `src/Mods/Military/art/models/horse_archer.glb` (598,704 bytes).
- Art: `ART_DEF_UNIT_HORSE_ARCHER`. Source scale: 0.42.
- NIF: `art/units/horsearcher/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 1116 triangles, 3 skins, 37 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3023, feet Y=0.
- Clips: idle (108 channels), run (108 channels), fortify (108 channels), attack (108 channels), hurt (108 channels), die (108 channels).
- SHA-256: `678ab8cfcd6740378fd5b57bf3f0bbfdfad32fb766291aa3350f01a05c469e32`.
- idle: `art/units/horsearcher/horsearcher_md_idle.kf`.
- run: `art/units/horsearcher/horsearcher_md_run.kf`.
- fortify: `art/units/horsearcher/horsearcher_md_fortifya.kf`.
- attack: `art/units/horsearcher/horsearcher_md_strikea.kf`.
- hurt: `art/units/horsearcher/horsearcher_md_hurta.kf`.
- die: `art/units/horsearcher/horsearcher_md_diea.kf`.
- Note: Missing texture: HorseArcher_weapons_64.dds

### military:horseman

- Model: `src/Mods/Military/art/models/horseman.glb` (606,488 bytes).
- Art: `ART_DEF_UNIT_HORSEMAN_EURASIAN`. Source scale: 0.42.
- NIF: `art/units/horseman_eurasian/horseman_eurasian.nif`.
- KFM: `art/units/horseman_eurasian/horseman_eurasian.kfm`.
- Geometry: 1183 triangles, 2 skins, 32 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3965, feet Y=0.
- Clips: idle (103 channels), run (103 channels), fortify (103 channels), attack (103 channels), hurt (103 channels), die (103 channels).
- SHA-256: `7bf0a17abc6f4d79195492cc21cea2d8488eb4fe2fd4f8ccb93aebaffe578a96`.
- idle: `art/units/horseman_eurasian/horseman_eurasian_md_idle.kf`.
- run: `art/units/horseman_eurasian/horseman_eurasian_md_run.kf`.
- fortify: `art/units/horseman_eurasian/horseman_eurasian_md_fortifya.kf`.
- attack: `art/units/horseman_eurasian/horseman_eurasian_md_strikea.kf`.
- hurt: `art/units/horseman_eurasian/horseman_eurasian_md_hurta.kf`.
- die: `art/units/horseman_eurasian/horseman_eurasian_md_diea.kf`.

### military:hungarian_huszar_cavalry

- Model: `src/Mods/Military/art/models/hungarian_huszar_cavalry.glb` (585,708 bytes).
- Art: `ART_DEF_UNIT_HUSZAR_CAVALRY`. Source scale: 0.42.
- NIF: `art/hungary/huszar cavalry/russiancossack.nif`.
- KFM: `art/hungary/huszar cavalry/russiancossack.kfm`.
- Geometry: 1076 triangles, 3 skins, 40 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3028, feet Y=0.
- Clips: idle (95 channels), run (95 channels), fortify (95 channels), attack (95 channels), hurt (95 channels), die (95 channels).
- SHA-256: `d7086e0aa0f11b21b2e67f68aed38209a91991af04b8719b5f1b755926bf42fc`.
- idle: `art/hungary/huszar cavalry/russiancossack_md_idle.kf`.
- run: `art/hungary/huszar cavalry/russiancossack_md_run.kf`.
- fortify: `art/hungary/huszar cavalry/russiancossack_md_fortifya.kf`.
- attack: `art/hungary/huszar cavalry/russiancossack_md_strikea.kf`.
- hurt: `art/hungary/huszar cavalry/russiancossack_md_hurta.kf`.
- die: `art/hungary/huszar cavalry/russiancossack_md_diea.kf`.

### military:huns_tarkan

- Model: `src/Mods/Military/art/models/huns_tarkan.glb` (729,832 bytes).
- Art: `ART_DEF_UNIT_HUNS_TARKAN`. Source scale: 0.42.
- NIF: `art/huns/tarkan/horsearcher.nif`.
- KFM: `art/huns/tarkan/horsearcher.kfm`.
- Geometry: 1132 triangles, 3 skins, 37 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2989, feet Y=0.
- Clips: idle (108 channels), run (108 channels), fortify (108 channels), attack (108 channels), hurt (108 channels), die (108 channels).
- SHA-256: `ceef2a4158d240113ff70076738ebefb2ba217daa22705e99d65a642312232ed`.
- idle: `art/huns/tarkan/horsearcher_md_idle.kf`.
- run: `art/huns/tarkan/horsearcher_md_run.kf`.
- fortify: `art/huns/tarkan/horsearcher_md_fortifya.kf`.
- attack: `art/huns/tarkan/horsearcher_md_strikea.kf`.
- hurt: `art/huns/tarkan/horsearcher_md_hurta.kf`.
- die: `art/huns/tarkan/horsearcher_md_diea.kf`.
- Note: Missing texture: HorseArcher_weapons_64.dds

### military:hybrid_gunship

- Model: `src/Mods/Military/art/models/hybrid_gunship.glb` (257,180 bytes).
- Art: `ART_DEF_UNIT_HYBRID_GUNSHIP`. Source scale: 0.30.
- NIF: `art/units/hybrid gunship/heavymiss.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 1859 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2110, radius 0.4800, feet Y=0.
- Clips: idle (31 channels), run (31 channels), fortify (31 channels), attack (31 channels), hurt (31 channels).
- SHA-256: `bfe27872d72e13465008cc4e023d6faf7ba99d6224b795d8770fc11378a2331e`.
- idle: `art/units/gunship/gunship_md_idle.kf`.
- run: `art/units/gunship/gunship_md_run.kf`.
- fortify: `art/units/gunship/gunship_md_rangedfortify.kf`.
- attack: `art/units/gunship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/gunship/gunship_md_hurta.kf`.

### military:hydrogen

- Model: `src/Mods/Military/art/models/hydrogen.glb` (124,456 bytes).
- Art: `ART_DEF_UNIT_HYDROGEN`. Source scale: 0.85.
- NIF: `art/units/nukes/topol-m/icbm.nif`.
- KFM: `art/units/nukes/topol-m/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `6d48da07dddda0331ee3fcf37d2e943fee06811c4db44540baa47c6170ea6a5e`.
- idle: `art/units/nukes/topol-m/icbm_md_idle.kf`.
- run: `art/units/nukes/topol-m/icbm_md_run.kf`.
- attack: `art/units/nukes/topol-m/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:icbm

- Model: `src/Mods/Military/art/models/icbm.glb` (103,544 bytes).
- Art: `ART_DEF_UNIT_ICBM`. Source scale: 0.85.
- NIF: `art/units/icbm/icbm.nif`.
- KFM: `art/units/icbm/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `be35a3df3cbf2eb480c05dbd2b4da789b55f855f5abc714ce9e0d02f829bfa14`.
- idle: `art/units/icbm/icbm_md_idle.kf`.
- run: `art/units/icbm/icbm_md_run.kf`.
- attack: `art/units/icbm/icbm_md_strikea.kf`.

### military:il2

- Model: `src/Mods/Military/art/models/il2.glb` (330,188 bytes).
- Art: `ART_DEF_UNIT_IL2`. Source scale: 0.5.
- NIF: `art/units/il2/shturmovik.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 1430 triangles, 0 skins, 0 joint bindings, 3 textures.
- Bounds: height 0.2931, radius 0.4800, feet Y=0.
- Clips: idle (37 channels), run (37 channels), hurt (37 channels), die (37 channels).
- SHA-256: `98213404859e84ea560075496c8e395d4c5913765c3bfdeb8398612cef9d5b24`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:incan_quechua

- Model: `src/Mods/Military/art/models/incan_quechua.glb` (280,516 bytes).
- Art: `ART_DEF_UNIT_INCAN_QUECHUA`. Source scale: 0.44.
- NIF: `art/units/unique/inca/quechua/incanquechua.nif`.
- KFM: `art/units/unique/inca/quechua/incanquechua.kfm`.
- Geometry: 648 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3356, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `b71f7780ff9a2059133691b3cc365c51a99b9f118ad3aa9006e6358ac5abe6e6`.
- idle: `art/units/unique/inca/quechua/incanquechua_md_idle.kf`.
- run: `art/units/unique/inca/quechua/incanquechua_md_run.kf`.
- fortify: `art/units/unique/inca/quechua/incanquechua_md_fortifya.kf`.
- attack: `art/units/unique/inca/quechua/incanquechua_md_strikea.kf`.
- hurt: `art/units/unique/inca/quechua/incanquechua_md_hurta.kf`.
- die: `art/units/unique/inca/quechua/incanquechua_md_diea.kf`.

### military:indian_fast_worker

- Model: `src/Mods/Military/art/models/indian_fast_worker.glb` (248,452 bytes).
- Art: `ART_DEF_UNIT_INDIAN_FAST_WORKER`. Source scale: 0.44.
- NIF: `art/units/unique/india/worker/indianfastworker.nif`.
- KFM: `art/units/unique/india/worker/indianfastworker.kfm`.
- Geometry: 929 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2734, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels).
- SHA-256: `ba674ad9e42c5dafe24db31824f45fa76f2da20a9c0798b8bf9876dd8816fa37`.
- idle: `art/units/unique/india/worker/indianfastworker_md_idle.kf`.
- run: `art/units/unique/india/worker/indianfastworker_md_run.kf`.
- fortify: `art/units/unique/india/worker/indianfastworker_md_fortify.kf`.
- Note: Missing texture: Worker_tools.dds

### military:infantry

- Model: `src/Mods/Military/art/models/infantry.glb` (356,904 bytes).
- Art: `ART_DEF_UNIT_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/infantry/infantry.nif`.
- KFM: `art/units/infantry/infantry.kfm`.
- Geometry: 658 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3282, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `75c48e85cfb9606fe6f9fc84e487571a61123d25129a3ef6e2976c9e2a69a22f`.
- idle: `art/units/infantry/infantry_md_idle.kf`.
- run: `art/units/infantry/infantry_md_run.kf`.
- fortify: `art/units/infantry/infantry_md_rangedfortify.kf`.
- attack: `art/units/infantry/infantry_md_rangedstrike.kf`.
- hurt: `art/units/infantry/infantry_md_hurta.kf`.
- die: `art/units/infantry/infantry_md_rangeddiea.kf`.

### military:inquisitor

- Model: `src/Mods/Military/art/models/inquisitor.glb` (427,056 bytes).
- Art: `ART_DEF_UNIT_INQUISITOR`. Source scale: 0.44.
- NIF: `art/units/godsofold_inquisitor/inquisitor_red.nif`.
- KFM: `art/units/godsofold_inquisitor/inquisitor_red.kfm`.
- Geometry: 678 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4148, feet Y=0.
- Clips: idle (62 channels), run (62 channels), fortify (62 channels), attack (62 channels).
- SHA-256: `c0beb1af7f3cfadb97d1fd7e3fafdcc6233e5f49dafc3ac894e91cbc0398a2cf`.
- idle: `art/units/godsofold_inquisitor/inquisitor_red_md_idle.kf`.
- run: `art/units/godsofold_inquisitor/inquisitor_red_md_run.kf`.
- fortify: `art/units/godsofold_inquisitor/inquisitor_red_md_fortify.kf`.
- attack: `art/units/godsofold_inquisitor/inquisitor_red_md_rangedstrike.kf`.
- Note: Missing texture: Great_Spy_Ancient_DIFF.dds

### military:irbm

- Model: `src/Mods/Military/art/models/irbm.glb` (117,276 bytes).
- Art: `ART_DEF_UNIT_IRBM`. Source scale: 0.85.
- NIF: `art/units/nukes/irbm/icbm.nif`.
- KFM: `art/units/nukes/irbm/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `c9171a433396b2c5534ba10376a88d28fede80793c3802f94f6ebc3540cf69d5`.
- idle: `art/units/nukes/irbm/icbm_md_idle.kf`.
- run: `art/units/nukes/irbm/icbm_md_run.kf`.
- attack: `art/units/nukes/irbm/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:irish_fianna

- Model: `src/Mods/Military/art/models/irish_fianna.glb` (295,676 bytes).
- Art: `ART_DEF_UNIT_FIANNA`. Source scale: 0.44.
- NIF: `art/ireland/fianna/swordsman.nif`.
- KFM: `art/ireland/fianna/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3230, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), attack (56 channels), hurt (56 channels), die (56 channels).
- SHA-256: `828a54d7774b169ae479b3353f5001c10d6bbf85be185b7c2666aadcc8f1b4f4`.
- idle: `art/ireland/fianna/swordsman_md_idle.kf`.
- run: `art/ireland/fianna/swordsman_md_run.kf`.
- fortify: `art/ireland/fianna/swordsman_md_fortifya.kf`.
- attack: `art/ireland/fianna/swordsman_md_strikea.kf`.
- hurt: `art/ireland/fianna/swordsman_md_hurta.kf`.
- die: `art/ireland/fianna/swordsman_md_diea.kf`.

### military:iron_frigate

- Model: `src/Mods/Military/art/models/iron_frigate.glb` (307,728 bytes).
- Art: `ART_DEF_UNIT_IRON_FRIGATE`. Source scale: 0.19.
- NIF: `art/units/iron frigate/hms warrior.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1393 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.4769, radius 0.4800, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `a7f1fc021f6cf9c37c88a2540a98ee72fe7aae89be623219c00397a500f44257`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.
- Note: Missing texture: //hmswarrior256.dds

### military:ironclad

- Model: `src/Mods/Military/art/models/ironclad.glb` (310,580 bytes).
- Art: `ART_DEF_UNIT_IRONCLAD`. Source scale: 0.52.
- NIF: `art/units/ironclad/ironclad.nif`.
- KFM: `art/units/ironclad/ironclad.kfm`.
- Geometry: 445 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.2585, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `5c87c285520c0695b30f1b91ed4c4ed64b967045342f70afa58927643422c1e1`.
- idle: `art/units/ironclad/ironclad_md_idle.kf`.
- run: `art/units/ironclad/ironclad_md_run.kf`.
- fortify: `art/units/ironclad/ironclad_md_fortifya.kf`.
- attack: `art/units/ironclad/ironclad_md_strikea.kf`.
- hurt: `art/units/ironclad/ironclad_md_hurta.kf`.
- die: `art/units/ironclad/ironclad_md_diea.kf`.

### military:ironclad_cruiser

- Model: `src/Mods/Military/art/models/ironclad_cruiser.glb` (750,228 bytes).
- Art: `ART_DEF_UNIT_IRONCLAD_CRUISER`. Source scale: 0.32.
- NIF: `art/units/ironclad cruiser/ww1_cr_rus.nif`.
- KFM: `art/units/ironclad cruiser/ww1_cr_rus.kfm`.
- Geometry: 2188 triangles, 1 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.3554, radius 0.4800, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (86 channels), attack (86 channels), hurt (86 channels), die (86 channels).
- SHA-256: `750ae0f53a7c82a1859962a970593c003de3f61ae1880aa48eeba2c48b777c2d`.
- idle: `art/units/ironclad cruiser/ww1_cr_rus_md_idle.kf`.
- run: `art/units/ironclad cruiser/ww1_cr_rus_md_run.kf`.
- fortify: `art/units/ironclad cruiser/ww1_cr_rus_md_fortify.kf`.
- attack: `art/units/ironclad cruiser/ww1_cr_rus_md_strike.kf`.
- hurt: `art/units/ironclad cruiser/ww1_cr_rus_md_hurt.kf`.
- die: `art/units/ironclad cruiser/ww1_cr_rus_md_die.kf`.

### military:iroquois_mohawkarcher

- Model: `src/Mods/Military/art/models/iroquois_mohawkarcher.glb` (390,836 bytes).
- Art: `ART_DEF_UNIT_IROQUOIS_MOHAWKARCHER`. Source scale: 0.44.
- NIF: `art/units/unique/iroquois/mohawk longbowman/archer.nif`.
- KFM: `art/units/archer/archer.kfm`.
- Geometry: 848 triangles, 3 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2691, feet Y=0.
- Clips: idle (68 channels), run (68 channels), fortify (68 channels), attack (68 channels), hurt (68 channels), die (68 channels).
- SHA-256: `4f628e8c17cb6f1cb73fc1aeff88b736dc291ecb76bdf040a3e0e833adbef34b`.
- idle: `art/units/archer/archer_md_idle.kf`.
- run: `art/units/archer/archer_md_run.kf`.
- fortify: `art/units/archer/archer_md_fortifya.kf`.
- attack: `art/units/archer/archer_md_strikea.kf`.
- hurt: `art/units/archer/archer_md_hurta.kf`.
- die: `art/units/archer/archer_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:islamic_missionary

- Model: `src/Mods/Military/art/models/islamic_missionary.glb` (187,672 bytes).
- Art: `ART_DEF_UNIT_ISLAMIC_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/islamic/islamic_missionary.nif`.
- KFM: `art/units/missionary/islamic/islamic_missionary.kfm`.
- Geometry: 566 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2300, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), die (46 channels).
- SHA-256: `9121390b4cd7a2d2bc48f9b9611a24daf3c1f86cbb02fd229cacb97a6f3be5ad`.
- idle: `art/units/missionary/islamic/islamic_missionary_md_idle.kf`.
- run: `art/units/missionary/islamic/islamic_missionary_md_run.kf`.
- fortify: `art/units/missionary/islamic/islamic_missionary_md_fortify.kf`.
- die: `art/units/missionary/islamic/islamic_missionary_md_idle_die.kf`.

### military:japan_samurai

- Model: `src/Mods/Military/art/models/japan_samurai.glb` (307,000 bytes).
- Art: `ART_DEF_UNIT_JAPAN_SAMURAI`. Source scale: 0.44.
- NIF: `art/units/unique/japan/samurai/japanesesamurai.nif`.
- KFM: `art/units/unique/japan/samurai/japanesesamurai.kfm`.
- Geometry: 801 triangles, 2 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2983, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `55545291fde9ae0a650abf178804da072a310c5bdf3ede799879dc4cd553d3d7`.
- idle: `art/units/unique/japan/samurai/japanesesamurai_md_idle.kf`.
- run: `art/units/unique/japan/samurai/japanesesamurai_md_run.kf`.
- fortify: `art/units/unique/japan/samurai/japanesesamurai_md_fortifya.kf`.
- attack: `art/units/unique/japan/samurai/japanesesamurai_md_strikea.kf`.
- hurt: `art/units/unique/japan/samurai/japanesesamurai_md_hurta.kf`.
- die: `art/units/unique/japan/samurai/japanesesamurai_md_diea.kf`.

### military:javelineer

- Model: `src/Mods/Military/art/models/javelineer.glb` (328,328 bytes).
- Art: `ART_DEF_UNIT_JAVELINEER`. Source scale: 0.44.
- NIF: `art/units/javelineer/european/javelineer.nif`.
- KFM: `art/units/javelineer/animations/javelineer_2.kfm`.
- Geometry: 620 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3080, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels), attack (59 channels), hurt (59 channels), die (59 channels).
- SHA-256: `493f75de2ada8e1952dde8b1e80b3dc170f087bab47449467c65c8e597998235`.
- idle: `art/units/javelineer/animations/maliskirmisher_md_idle.kf`.
- run: `art/units/javelineer/animations/maliskirmisher_md_run.kf`.
- fortify: `art/units/javelineer/animations/maliskirmisher_md_fortifya.kf`.
- attack: `art/units/javelineer/animations/maliskirmisher_md_strikea.kf`.
- hurt: `art/units/javelineer/animations/maliskirmisher_md_hurta.kf`.
- die: `art/units/javelineer/animations/maliskirmisher_md_diea.kf`.

### military:jeep

- Model: `src/Mods/Military/art/models/jeep.glb` (458,800 bytes).
- Art: `ART_DEF_UNIT_JEEP`. Source scale: 0.45.
- NIF: `art/units/jeep/willys.nif`.
- KFM: `art/units/jeep/willys.kfm`.
- Geometry: 2443 triangles, 3 skins, 22 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4513, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels).
- SHA-256: `78d9f981fe71d9abeb66422597e8c268c8035a237895e09f040d97a3de97cd21`.
- idle: `art/units/jeep/willys_md_idle.kf`.
- run: `art/units/jeep/willys_md_run.kf`.
- fortify: `art/units/jeep/willys_md_rangedfortify.kf`.
- attack: `art/units/jeep/willys_md_rangedstrike.kf`.
- hurt: `art/units/jeep/willys_md_hurta.kf`.
- Note: Hidden geometry omitted: Biped Object
- Note: Hidden geometry omitted: Biped Object@#0

### military:jet_fighter

- Model: `src/Mods/Military/art/models/jet_fighter.glb` (264,072 bytes).
- Art: `ART_DEF_UNIT_JET_FIGHTER`. Source scale: 0.48.
- NIF: `art/units/jetfighter/jetfighter.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 1760 triangles, 2 skins, 6 joint bindings, 1 textures.
- Bounds: height 0.2951, radius 0.4800, feet Y=0.
- Clips: idle (40 channels), run (40 channels), hurt (40 channels).
- SHA-256: `a7cad79fea127b1fca121fb69cac9db85c1ac77f1406d4485311673497375a89`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:jewish_missionary

- Model: `src/Mods/Military/art/models/jewish_missionary.glb` (170,796 bytes).
- Art: `ART_DEF_UNIT_JEWISH_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/jewish/jewishmissionary.nif`.
- KFM: `art/units/missionary/jewish/jewishmissionary.kfm`.
- Geometry: 592 triangles, 2 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2510, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), die (45 channels).
- SHA-256: `868dac165ca9c5e859087555488bc4abc94af22580bbcbebd9bc4a88f82e287f`.
- idle: `art/units/missionary/jewish/jewishmissionary_md_idle.kf`.
- run: `art/units/missionary/jewish/jewishmissionary_md_run.kf`.
- fortify: `art/units/missionary/jewish/jewishmissionary_md_fortifya.kf`.
- die: `art/units/missionary/jewish/jewishmissionary_md_idle_die.kf`.

### military:khmer_ballista_elephant

- Model: `src/Mods/Military/art/models/khmer_ballista_elephant.glb` (619,720 bytes).
- Art: `ART_DEF_UNIT_KHMER_BALLISTA_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/unique_khmer_elephantballista/khmer_elephantballista.nif`.
- KFM: `art/units/unique_khmer_elephantballista/khmer_elephantballista.kfm`.
- Geometry: 1414 triangles, 3 skins, 46 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2908, feet Y=0.
- Clips: idle (117 channels), run (117 channels), attack (117 channels), die (117 channels).
- SHA-256: `e5c4d7bcec193ddff3f27905780406b98daa51b0ab7b3b281aebf7e67f5a9450`.
- idle: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_idle.kf`.
- run: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_run.kf`.
- attack: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_rangedstrikea.kf`.
- die: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_rangeddiea.kf`.

### military:knight

- Model: `src/Mods/Military/art/models/knight.glb` (617,896 bytes).
- Art: `ART_DEF_UNIT_KNIGHT`. Source scale: 0.42.
- NIF: `art/units/knight/knight.nif`.
- KFM: `art/units/knight/knight.kfm`.
- Geometry: 1143 triangles, 3 skins, 37 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3002, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `3022557f86febd602fdb76ebf1d613889c10601dc0a4dedb602e8f9412fa3e13`.
- idle: `art/units/knight/knight_md_idle.kf`.
- run: `art/units/knight/knight_md_run.kf`.
- fortify: `art/units/knight/knight_md_fortifya.kf`.
- attack: `art/units/knight/knight_md_strikea.kf`.
- hurt: `art/units/knight/knight_md_hurta.kf`.
- die: `art/units/knight/knight_md_diea.kf`.
- Note: Missing texture: Knight_horse_128.dds

### military:korean_hwacha

- Model: `src/Mods/Military/art/models/korean_hwacha.glb` (410,884 bytes).
- Art: `ART_DEF_UNIT_KOREAN_HWACHA`. Source scale: 0.50.
- NIF: `art/units/hwacha_korean/hwacha.nif`.
- KFM: `art/units/hwacha_korean/hwacha.kfm`.
- Geometry: 1298 triangles, 2 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3858, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels).
- SHA-256: `06a57559084303481b5814e0b30a3f8ced87713d705a30203a438ecc954b6631`.
- idle: `art/units/hwacha_korean/hwacha_md_idle.kf`.
- run: `art/units/hwacha_korean/hwacha_md_run.kf`.
- fortify: `art/units/hwacha_korean/hwacha_md_rangedfortify.kf`.
- attack: `art/units/hwacha_korean/hwacha_md_rangedstrike.kf`.
- hurt: `art/units/hwacha_korean/hwacha_md_hurta.kf`.

### military:lancer

- Model: `src/Mods/Military/art/models/lancer.glb` (702,500 bytes).
- Art: `ART_DEF_UNIT_LANCER`. Source scale: 0.42.
- NIF: `art/units/lancer/european/lancer.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1228 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3915, feet Y=0.
- Clips: idle (122 channels), run (122 channels), fortify (122 channels), attack (122 channels), hurt (122 channels), die (122 channels).
- SHA-256: `7d2e56c5764fb4b6dd960d6b676d933050a85505b80415e1d298c37ae26ebffa`.
- idle: `art/units/unique/mongol/keshik/mongelkeshik_md_idle.kf`.
- run: `art/units/unique/mongol/keshik/mongelkeshik_md_run.kf`.
- fortify: `art/units/unique/mongol/keshik/mongelkeshik_md_fortifya.kf`.
- attack: `art/units/unique/mongol/keshik/mongelkeshik_md_strikea.kf`.
- hurt: `art/units/unique/mongol/keshik/mongelkeshik_md_hurta.kf`.
- die: `art/units/unique/mongol/keshik/mongelkeshik_md_diea.kf`.

### military:light_aa

- Model: `src/Mods/Military/art/models/light_aa.glb` (136,052 bytes).
- Art: `ART_DEF_UNIT_LIGHT_AA`. Source scale: 0.6.
- NIF: `art/units/light antiair gun/lightaa.nif`.
- KFM: `art/units/light antiair gun/lightaa.kfm`.
- Geometry: 1045 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.3455, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `f16d0154f9eaefc7e7628cbfbe843de327a3647fa2a6f11586b29e2fe2c9674d`.
- idle: `art/units/light antiair gun/lightaa_md_idle.kf`.
- run: `art/units/light antiair gun/lightaa_md_run.kf`.
- fortify: `art/units/light antiair gun/lightaa_md_rangedfortify.kf`.
- attack: `art/units/light antiair gun/lightaa_md_rangedstrike.kf`.
- hurt: `art/units/light antiair gun/lightaa_md_hurta.kf`.

### military:light_artillery

- Model: `src/Mods/Military/art/models/light_artillery.glb` (168,428 bytes).
- Art: `ART_DEF_UNIT_LIGHT_ARTILLERY`. Source scale: 0.7.
- NIF: `art/units/light artillery/artillery.nif`.
- KFM: `art/units/artillery/artillery.kfm`.
- Geometry: 442 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2290, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels).
- SHA-256: `a84550dca716c0c44f2ef2626a705e6c42ad29a94ca6819c005caf4eb52da934`.
- idle: `art/units/artillery/artillery_md_idle.kf`.
- run: `art/units/artillery/artillery_md_run.kf`.
- fortify: `art/units/artillery/artillery_md_rangedfortify.kf`.
- attack: `art/units/artillery/artillery_md_rangedstrike.kf`.
- hurt: `art/units/artillery/artillery_md_hurta.kf`.

### military:light_cavalry

- Model: `src/Mods/Military/art/models/light_cavalry.glb` (640,116 bytes).
- Art: `ART_DEF_UNIT_LIGHT_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/charlemagne_lightcavalry/lightcavalry.nif`.
- KFM: `art/units/charlemagne_lightcavalry/lightcavalry.kfm`.
- Geometry: 1306 triangles, 2 skins, 43 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4104, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `ef810ef6d51494764d71bcac1b44517a0763f36c44ee245c5a1170ac7d7fefe6`.
- idle: `art/units/charlemagne_lightcavalry/lightcavalry_md_idle.kf`.
- run: `art/units/charlemagne_lightcavalry/lightcavalry_md_run.kf`.
- fortify: `art/units/charlemagne_lightcavalry/lightcavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_lightcavalry/lightcavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_lightcavalry/lightcavalry_md_hurta.kf`.
- die: `art/units/charlemagne_lightcavalry/lightcavalry_md_diea.kf`.

### military:light_swordsman

- Model: `src/Mods/Military/art/models/light_swordsman.glb` (296,948 bytes).
- Art: `ART_DEF_UNIT_SWORDSMAN`. Source scale: 0.44.
- NIF: `art/units/swordsman/swordsman.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3230, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), attack (56 channels), hurt (56 channels), die (56 channels).
- SHA-256: `75b94b174afe4a21debb6bb406f44a84e0c84c2e2dfd4dfdb4ea2fec16b5a37c`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:light_tank

- Model: `src/Mods/Military/art/models/light_tank.glb` (239,372 bytes).
- Art: `ART_DEF_UNIT_VICKERS_E`. Source scale: 0.5.
- NIF: `art/units/light tank/european/vickers_e.nif`.
- KFM: `art/units/light tank/european/vickers_e.kfm`.
- Geometry: 712 triangles, 2 skins, 4 joint bindings, 2 textures.
- Bounds: height 0.4028, radius 0.4800, feet Y=0.
- Clips: idle (10 channels), run (10 channels), fortify (10 channels), attack (10 channels), hurt (10 channels).
- SHA-256: `87eab0ddfdcf56baa28061377463e91e95a522736d7971697b6a6b161bb440bd`.
- idle: `art/units/light tank/european/vickers_e_md_idle.kf`.
- run: `art/units/light tank/european/vickers_e_md_run.kf`.
- fortify: `art/units/light tank/european/vickers_e_md_rangedfortify.kf`.
- attack: `art/units/light tank/european/vickers_e_md_rangedstrike.kf`.
- hurt: `art/units/light tank/european/vickers_e_md_hurta.kf`.

### military:lion

- Model: `src/Mods/Military/art/models/lion.glb` (321,216 bytes).
- Art: `ART_DEF_UNIT_LION`. Source scale: 0.61.
- NIF: `art/units/lion/lion.nif`.
- KFM: `art/units/lion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5755, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `00e60cbdf9f7cacafabb5c8e63c37b74b76dac73c762076394b7309eecb0f805`.
- idle: `art/units/lion/lion_md_idle.kf`.
- run: `art/units/lion/lion_md_run.kf`.
- fortify: `art/units/lion/lion_md_fortifya.kf`.
- attack: `art/units/lion/lion_md_strikea.kf`.
- hurt: `art/units/lion/lion_md_hurta.kf`.
- die: `art/units/lion/lion_md_diea.kf`.

### military:lithuanian_vytis

- Model: `src/Mods/Military/art/models/lithuanian_vytis.glb` (638,160 bytes).
- Art: `ART_DEF_UNIT_VYTIS`. Source scale: 0.42.
- NIF: `art/lithuania/vytis/heavycavalry.nif`.
- KFM: `art/lithuania/vytis/heavycavalry.kfm`.
- Geometry: 1138 triangles, 2 skins, 39 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4131, feet Y=0.
- Clips: idle (124 channels), run (124 channels), fortify (124 channels), attack (124 channels), hurt (124 channels), die (124 channels).
- SHA-256: `757946e6464bf48d54044b30b3205468c4ec39d3a6a4cbc3a1ee5ec1794eabf7`.
- idle: `art/lithuania/vytis/heavycavalry_md_idle.kf`.
- run: `art/lithuania/vytis/heavycavalry_md_run.kf`.
- fortify: `art/lithuania/vytis/heavycavalry_md_fortifya.kf`.
- attack: `art/lithuania/vytis/heavycavalry_md_strikea.kf`.
- hurt: `art/lithuania/vytis/heavycavalry_md_hurta.kf`.
- die: `art/lithuania/vytis/heavycavalry_md_diea.kf`.

### military:littoral_combat_ship

- Model: `src/Mods/Military/art/models/littoral_combat_ship.glb` (375,740 bytes).
- Art: `ART_DEF_UNIT_LITTORAL_COMBAT_SHIP`. Source scale: 0.7.
- NIF: `art/units/littoral combat ship/dd_sens.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 955 triangles, 2 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.3774, radius 0.4800, feet Y=0.
- Clips: idle (63 channels), run (63 channels), fortify (63 channels), attack (63 channels), hurt (63 channels), die (63 channels).
- SHA-256: `ffcbeebdeac843d5a2a1fe29309c01922b73904c8536f38bc163ededd6347b8a`.
- idle: `art/units/destroyer/destroyer_md_idle.kf`.
- run: `art/units/destroyer/destroyer_md_run.kf`.
- fortify: `art/units/destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/destroyer/destroyer_md_hurta.kf`.
- die: `art/units/destroyer/destroyer_md_diea.kf`.

### military:lombard_gasindii

- Model: `src/Mods/Military/art/models/lombard_gasindii.glb` (301,944 bytes).
- Art: `ART_DEF_UNIT_GASINDII`. Source scale: 0.44.
- NIF: `art/lombards/gasindii/greekphalanx.nif`.
- KFM: `art/lombards/gasindii/greekphalanx.kfm`.
- Geometry: 598 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4594, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `54a7583b3136289bb00228156f046df6d21bb7961d1d71725583b8db9ddec544`.
- idle: `art/lombards/gasindii/greekphalanx_md_idle.kf`.
- run: `art/lombards/gasindii/greekphalanx_md_run.kf`.
- fortify: `art/lombards/gasindii/greekphalanx_md_fortifya.kf`.
- attack: `art/lombards/gasindii/greekphalanx_md_strikea.kf`.
- hurt: `art/lombards/gasindii/greekphalanx_md_hurta.kf`.
- die: `art/lombards/gasindii/greekphalanx_md_diea.kf`.

### military:longbowman

- Model: `src/Mods/Military/art/models/longbowman.glb` (403,572 bytes).
- Art: `ART_DEF_UNIT_LONGBOWMAN`. Source scale: 0.44.
- NIF: `art/units/longbowman/longbowman.nif`.
- KFM: `art/units/longbowman/longbowman.kfm`.
- Geometry: 718 triangles, 2 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2478, feet Y=0.
- Clips: idle (66 channels), run (66 channels), fortify (66 channels), attack (66 channels), hurt (66 channels), die (66 channels).
- SHA-256: `f41d88e2f5472d048f6f7f56e4d6f26cfa3b24c4d41eedd71902e6118fbba871`.
- idle: `art/units/longbowman/longbowman_md_idle.kf`.
- run: `art/units/longbowman/longbowman_md_run.kf`.
- fortify: `art/units/longbowman/longbowman_md_fortifya.kf`.
- attack: `art/units/longbowman/longbowman_md_strikea.kf`.
- hurt: `art/units/longbowman/longbowman_md_hurta.kf`.
- die: `art/units/longbowman/longbowman_md_diea.kf`.

### military:maceman

- Model: `src/Mods/Military/art/models/maceman.glb` (359,312 bytes).
- Art: `ART_DEF_UNIT_MACEMAN`. Source scale: 0.44.
- NIF: `art/units/maceman/maceman.nif`.
- KFM: `art/units/maceman/maceman.kfm`.
- Geometry: 672 triangles, 2 skins, 20 joint bindings, 2 textures.
- Bounds: height 0.5574, radius 0.4800, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels), die (58 channels).
- SHA-256: `7a525ac1abcdd45a6b836ea58cc04d7d4b321464ca4f3c6c49270b96a53db883`.
- idle: `art/units/maceman/maceman_md_idle.kf`.
- run: `art/units/maceman/maceman_md_run.kf`.
- fortify: `art/units/maceman/maceman_md_fortifya.kf`.
- attack: `art/units/maceman/maceman_md_strikea.kf`.
- hurt: `art/units/maceman/maceman_md_hurta.kf`.
- die: `art/units/maceman/maceman_md_diea.kf`.

### military:machine_gun

- Model: `src/Mods/Military/art/models/machine_gun.glb` (294,004 bytes).
- Art: `ART_DEF_UNIT_MACHINE_GUN`. Source scale: 0.44.
- NIF: `art/units/machinegun/machinegunner.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 774 triangles, 3 skins, 23 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2373, feet Y=0.
- Clips: idle (70 channels), run (70 channels), fortify (70 channels), attack (70 channels), hurt (70 channels).
- SHA-256: `e6b8aa67c90643ac1a3592896c3a8beb772b0383e4dfe32af10317d7992614e1`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:mali_skirmisher

- Model: `src/Mods/Military/art/models/mali_skirmisher.glb` (341,460 bytes).
- Art: `ART_DEF_UNIT_MALI_SKIRMISHER`. Source scale: 0.44.
- NIF: `art/units/unique/mali/skirmisher/maliskirmisher.nif`.
- KFM: `art/units/unique/mali/skirmisher/maliskirmisher.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3097, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels), attack (59 channels), hurt (59 channels), die (59 channels).
- SHA-256: `1160a2fde2e983698054e0f5450908fbaeb179ca6a53e33a829b2df0cfe51a72`.
- idle: `art/units/unique/mali/skirmisher/maliskirmisher_md_idle.kf`.
- run: `art/units/unique/mali/skirmisher/maliskirmisher_md_run.kf`.
- fortify: `art/units/unique/mali/skirmisher/maliskirmisher_md_fortifya.kf`.
- attack: `art/units/unique/mali/skirmisher/maliskirmisher_md_strikea.kf`.
- hurt: `art/units/unique/mali/skirmisher/maliskirmisher_md_hurta.kf`.
- die: `art/units/unique/mali/skirmisher/maliskirmisher_md_diea.kf`.

### military:man_at_arms

- Model: `src/Mods/Military/art/models/man_at_arms.glb` (638,228 bytes).
- Art: `ART_DEF_UNIT_MAN_AT_ARMS`. Source scale: 0.42.
- NIF: `art/units/man at arms/european/mmaceman_light.nif`.
- KFM: `art/units/horse archer/greek/companion_cavalry.kfm`.
- Geometry: 1145 triangles, 3 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2599, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `e7882dcd2acf0c9e12ac4aa84e5b8489ee012edb4512ca28e0b2ed8fd94fae19`.
- idle: `art/units/horse archer/greek/companion_cavalry_md_idle.kf`.
- run: `art/units/horse archer/greek/companion_cavalry_md_run.kf`.
- fortify: `art/units/horse archer/greek/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/horse archer/greek/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/horse archer/greek/companion_cavalry_md_hurta.kf`.
- die: `art/units/horse archer/greek/companion_cavalry_md_diea.kf`.

### military:manowar

- Model: `src/Mods/Military/art/models/manowar.glb` (293,148 bytes).
- Art: `ART_DEF_UNIT_MANOWAR`. Source scale: 0.20.
- NIF: `art/units/man o war/frigate.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1135 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2956, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels), attack (43 channels), hurt (43 channels), die (43 channels).
- SHA-256: `87120cddc9c2a2a3e1eaffee37968e7528d85b2b678375222070b199f9536ddb`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.

### military:marine

- Model: `src/Mods/Military/art/models/marine.glb` (320,332 bytes).
- Art: `ART_DEF_UNIT_MARINE`. Source scale: 0.44.
- NIF: `art/units/marine/marines.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 728 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3476, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `4e7d1a00936c2c9e948a9011d651df7287abcaabba93bce2962ab6f6f0afa77e`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:maya_holkan

- Model: `src/Mods/Military/art/models/maya_holkan.glb` (368,224 bytes).
- Art: `ART_DEF_UNIT_MAYA_HOLKAN`. Source scale: 0.44.
- NIF: `art/units/unique_maya_holkan/unique_mayan_holkan.nif`.
- KFM: `art/units/unique_maya_holkan/unique_mayan_holkan.kfm`.
- Geometry: 588 triangles, 2 skins, 25 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4024, feet Y=0.
- Clips: idle (62 channels), run (62 channels), fortify (62 channels), attack (62 channels), hurt (62 channels), die (62 channels).
- SHA-256: `ee205aa485f009694ec1feede02455a8eabf9abd324560cc8402d6a3f8563feb`.
- idle: `art/units/unique_maya_holkan/unique_mayan_holkan_md_idle.kf`.
- run: `art/units/unique_maya_holkan/unique_mayan_holkan_md_run.kf`.
- fortify: `art/units/unique_maya_holkan/unique_mayan_holkan_md_fortifya.kf`.
- attack: `art/units/unique_maya_holkan/unique_mayan_holkan_md_strikea.kf`.
- hurt: `art/units/unique_maya_holkan/unique_mayan_holkan_md_hurta.kf`.
- die: `art/units/unique_maya_holkan/unique_mayan_holkan_md_diea.kf`.

### military:mechanized_infantry

- Model: `src/Mods/Military/art/models/mechanized_infantry.glb` (260,688 bytes).
- Art: `ART_DEF_UNIT_MECHANIZED_INFANTRY`. Source scale: 0.43.
- NIF: `art/units/mechanizedinfantry/mechanizedinfantry.nif`.
- KFM: `art/units/mechanizedinfantry/mechanizedinfantry.kfm`.
- Geometry: 515 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4240, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels).
- SHA-256: `506bc8ad660a0ff04079e07c93de27c3f610e9a0e5ee8ab0b6dcec29460559e0`.
- idle: `art/units/mechanizedinfantry/mechanizedinfantry_md_idle.kf`.
- run: `art/units/mechanizedinfantry/mechanizedinfantry_md_run.kf`.
- fortify: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedfortify.kf`.
- attack: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedstrike.kf`.
- hurt: `art/units/mechanizedinfantry/mechanizedinfantry_md_hurta.kf`.

### military:med_caravan

- Model: `src/Mods/Military/art/models/med_caravan.glb` (648,340 bytes).
- Art: `ART_DEF_UNIT_MED_CARAVAN`. Source scale: 0.34.
- NIF: `art/units/charlemagne_supplytrain/supplytrain.nif`.
- KFM: `art/units/charlemagne_supplytrain/supplytrain.kfm`.
- Geometry: 2396 triangles, 4 skins, 81 joint bindings, 4 textures.
- Bounds: height 0.3331, radius 0.4800, feet Y=0.
- Clips: idle (191 channels), run (191 channels).
- SHA-256: `1d9a6636bc0a230a11e6864fc31b13491a17c39067ed0adc951773886660dbc5`.
- idle: `art/units/charlemagne_supplytrain/supplytrain_md_idle.kf`.
- run: `art/units/charlemagne_supplytrain/supplytrain_md_run.kf`.

### military:merchant

- Model: `src/Mods/Military/art/models/merchant.glb` (424,568 bytes).
- Art: `ART_DEF_UNIT_MERCHANT`. Source scale: 0.46.
- NIF: `art/units/greatpeople/merchant/greatmerchantancient.nif`.
- KFM: `art/units/greatpeople/merchant/greatmerchantancient.kfm`.
- Geometry: 1410 triangles, 3 skins, 42 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3687, feet Y=0.
- Clips: idle (101 channels), run (101 channels), fortify (101 channels).
- SHA-256: `6b043e84a741ff95fc40a1d184403ec265c54ecb1a3db7b65eac06ab58706769`.
- idle: `art/units/greatpeople/merchant/greatmerchantancient_md_idle.kf`.
- run: `art/units/greatpeople/merchant/greatmerchantancient_md_run.kf`.
- fortify: `art/units/greatpeople/merchant/greatmerchantancient_md_fortify.kf`.
- Note: Non-triangle effect geometry omitted: GoldThrow_front
- Note: Non-triangle effect geometry omitted: GoldDig_rightside
- Note: Non-triangle effect geometry omitted: GoldDig_leftside

### military:minigun

- Model: `src/Mods/Military/art/models/minigun.glb` (336,652 bytes).
- Art: `ART_DEF_UNIT_MINIGUN`. Source scale: 0.44.
- NIF: `art/units/minigun/european/fremg.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 1012 triangles, 3 skins, 23 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2373, feet Y=0.
- Clips: idle (70 channels), run (70 channels), fortify (70 channels), attack (70 channels), hurt (70 channels).
- SHA-256: `0188cecd786cfa6c96f99a310cebef74e3c962a07a99edc51c796ec84ae7f296`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:missile_cruiser

- Model: `src/Mods/Military/art/models/missile_cruiser.glb` (269,200 bytes).
- Art: `ART_DEF_UNIT_MISSILE_CRUISER`. Source scale: 0.6.
- NIF: `art/units/missile_cruiser/missile_cruiser.nif`.
- KFM: `art/units/missile_cruiser/missile_cruiser.kfm`.
- Geometry: 526 triangles, 1 skins, 5 joint bindings, 1 textures.
- Bounds: height 0.3535, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels), die (45 channels).
- SHA-256: `a2fb962cde7669124e113f7c85da6e14c2b5db9d68ab381a05a65cae3f2d0d7b`.
- idle: `art/units/missile_cruiser/missile_cruiser_md_idle.kf`.
- run: `art/units/missile_cruiser/missile_cruiser_md_run.kf`.
- fortify: `art/units/missile_cruiser/missile_cruiser_md_fortifya.kf`.
- attack: `art/units/missile_cruiser/missile_cruiser_md_strikea.kf`.
- hurt: `art/units/missile_cruiser/missile_cruiser_md_hurta.kf`.
- die: `art/units/missile_cruiser/missile_cruiser_md_diea.kf`.

### military:mobile_artillery

- Model: `src/Mods/Military/art/models/mobile_artillery.glb` (219,364 bytes).
- Art: `ART_DEF_UNIT_MOBILE_ARTILLERY`. Source scale: 0.57.
- NIF: `art/units/mobileartillery/mobileartillery.nif`.
- KFM: `art/units/mobileartillery/mobileartillery.kfm`.
- Geometry: 708 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.3153, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels).
- SHA-256: `7e005a50213532eef3576bb7da4fe6da9335b56a9631b64d3f9129467d6303c3`.
- idle: `art/units/mobileartillery/mobileartillery_md_idle.kf`.
- run: `art/units/mobileartillery/mobileartillery_md_run.kf`.
- fortify: `art/units/mobileartillery/mobileartillery_md_rangedfortify.kf`.
- attack: `art/units/mobileartillery/mobileartillery_md_rangedstrike.kf`.
- hurt: `art/units/mobileartillery/mobileartillery_md_hurta.kf`.

### military:mobile_sam

- Model: `src/Mods/Military/art/models/mobile_sam.glb` (350,172 bytes).
- Art: `ART_DEF_UNIT_MOBILE_SAM`. Source scale: 0.57.
- NIF: `art/units/mobilesam/mobilesam.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 802 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.3824, radius 0.4800, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels).
- SHA-256: `346320383f763bace3b0600163ad468a5ba038e1baad21fa7cacc782c643bd94`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:modern_armor

- Model: `src/Mods/Military/art/models/modern_armor.glb` (240,768 bytes).
- Art: `ART_DEF_UNIT_MODERN_ARMOR`. Source scale: 0.57.
- NIF: `art/units/modernarmor/modernarmor.nif`.
- KFM: `art/units/modernarmor/modernarmor.kfm`.
- Geometry: 546 triangles, 1 skins, 20 joint bindings, 1 textures.
- Bounds: height 0.2531, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels).
- SHA-256: `3b5c07ddc5b41e104b93be30b8b5687c0b8100e835a55706f28e8baf32ee4d59`.
- idle: `art/units/modernarmor/modernarmor_md_idle.kf`.
- run: `art/units/modernarmor/modernarmor_md_run.kf`.
- fortify: `art/units/modernarmor/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/modernarmor/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/modernarmor/modernarmor_md_hurta.kf`.

### military:modern_grenadier

- Model: `src/Mods/Military/art/models/modern_grenadier.glb` (433,032 bytes).
- Art: `ART_DEF_UNIT_MODERN_GRENADIER`. Source scale: 0.44.
- NIF: `art/units/modern grenadier/grenadier.nif`.
- KFM: `art/units/grenadier/grenadier.kfm`.
- Geometry: 848 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3449, feet Y=0.
- Clips: idle (64 channels), run (64 channels), fortify (64 channels), attack (64 channels), hurt (64 channels), die (64 channels).
- SHA-256: `aab9e48696542f810125a649d5198c36fae505c2e623a9975c31d47ba5bc35b8`.
- idle: `art/units/grenadier/grenadier_md_idle.kf`.
- run: `art/units/grenadier/grenadier_md_run.kf`.
- fortify: `art/units/grenadier/grenadier_md_fortifya.kf`.
- attack: `art/units/grenadier/grenadier_md_strikea.kf`.
- hurt: `art/units/grenadier/grenadier_md_hurta.kf`.
- die: `art/units/grenadier/grenadier_md_diea.kf`.
- Note: Hidden geometry omitted: Grenadier
- Note: Hidden geometry omitted: Editable Mesh
- Note: Hidden geometry omitted: Editable Mesh@#1
- Note: Hidden geometry omitted: Editable Poly@#2

### military:modern_infantry

- Model: `src/Mods/Military/art/models/modern_infantry.glb` (357,576 bytes).
- Art: `ART_DEF_UNIT_MODERNINFANTRY`. Source scale: 0.44.
- NIF: `art/units/modern infantry/european/moderninfantry.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 783 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3682, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `6885dfd1d926f13eb27e5f6bd6e9085a95590b8fbd7c8d9b09fe151e955a1b6d`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:modern_marine

- Model: `src/Mods/Military/art/models/modern_marine.glb` (384,820 bytes).
- Art: `ART_DEF_UNIT_MODERN_MARINE`. Source scale: 0.44.
- NIF: `art/units/modern marine/moderninfantry.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 823 triangles, 1 skins, 15 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3698, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `933f7681193102e236d1add2282c25fb987209e43171192c097f974c62958fc3`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:modern_paratrooper

- Model: `src/Mods/Military/art/models/modern_paratrooper.glb` (681,040 bytes).
- Art: `ART_DEF_UNIT_MODERN_PARATROOPER`. Source scale: 0.44.
- NIF: `art/units/modern paratrooper/paratrooper.nif`.
- KFM: `art/units/modern paratrooper/paratrooper.kfm`.
- Geometry: 1487 triangles, 2 skins, 25 joint bindings, 8 textures.
- Bounds: height 0.6000, radius 0.4001, feet Y=0.
- Clips: idle (101 channels), run (101 channels), fortify (101 channels), attack (101 channels), die (101 channels).
- SHA-256: `3feecc2485a68773913c0da312ec44b05df7559eb53f03d9a84ba1a8ff7a060b`.
- idle: `art/units/modern paratrooper/paratrooper_md_idle.kf`.
- run: `art/units/modern paratrooper/paratrooper_md_run.kf`.
- fortify: `art/units/modern paratrooper/paratrooper_md_rangedfortify.kf`.
- attack: `art/units/modern paratrooper/paratrooper_md_rangedstrike.kf`.
- die: `art/units/modern paratrooper/paratrooper_md_rangeddiea.kf`.
- Note: Hidden geometry omitted: Biped Object
- Note: Hidden geometry omitted: Biped Object@#0
- Note: Hidden geometry omitted: Biped Object@#1
- Note: Hidden geometry omitted: Biped Object@#2
- Note: Hidden geometry omitted: Biped Object@#3
- Note: Hidden geometry omitted: Box
- Note: Hidden geometry omitted: Biped Object@#4
- Note: Hidden geometry omitted: Biped Object@#5
- Note: Hidden geometry omitted: Biped Object@#6

### military:modern_workboat

- Model: `src/Mods/Military/art/models/modern_workboat.glb` (264,616 bytes).
- Art: `ART_DEF_UNIT_WORKBOAT_MODERN`. Source scale: 0.28.
- NIF: `art/units/workboat_modern/workboat_modern.nif`.
- KFM: `art/units/workboat_modern/workboat_modern.kfm`.
- Geometry: 489 triangles, 3 skins, 7 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3644, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), die (15 channels).
- SHA-256: `4f5d972f0451e8299ebc2906abe639056a8aa3c8bda472c42b0727cc022fe720`.
- idle: `art/units/workboat_modern/workboat_modern_md_idle.kf`.
- run: `art/units/workboat_modern/workboat_modern_md_run.kf`.
- fortify: `art/units/workboat_modern/workboat_modern_md_fortifya.kf`.
- die: `art/units/workboat_modern/workboat_modern_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:mongol_keshik

- Model: `src/Mods/Military/art/models/mongol_keshik.glb` (694,056 bytes).
- Art: `ART_DEF_UNIT_MONGOL_KESHIK`. Source scale: 0.42.
- NIF: `art/units/unique/mongol/keshik/mongelkeshik.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1148 triangles, 3 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3850, feet Y=0.
- Clips: idle (122 channels), run (122 channels), fortify (122 channels), attack (122 channels), hurt (122 channels), die (122 channels).
- SHA-256: `def6e3034ad1fa9d8c5fced5965202dac6c029b414fc2a5b47d0716c1ae75946`.
- idle: `art/units/unique/mongol/keshik/mongelkeshik_md_idle.kf`.
- run: `art/units/unique/mongol/keshik/mongelkeshik_md_run.kf`.
- fortify: `art/units/unique/mongol/keshik/mongelkeshik_md_fortifya.kf`.
- attack: `art/units/unique/mongol/keshik/mongelkeshik_md_strikea.kf`.
- hurt: `art/units/unique/mongol/keshik/mongelkeshik_md_hurta.kf`.
- die: `art/units/unique/mongol/keshik/mongelkeshik_md_diea.kf`.

### military:moonbean_executive

- Model: `src/Mods/Military/art/models/moonbean_executive.glb` (261,364 bytes).
- Art: `ART_DEF_UNIT_CEO_MOONBEAN_EXECUTIVE`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_mb.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `d1a40294aa2296deb15375fe3bba539592b7e00ed45ffdcc91ac5ab4f8494dec`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:motorcycle

- Model: `src/Mods/Military/art/models/motorcycle.glb` (962,760 bytes).
- Art: `ART_DEF_UNIT_MOTORCYCLE`. Source scale: 0.45.
- NIF: `art/units/motorcycle/wehrmachtbiker.nif`.
- KFM: `art/units/motorcycle/wehrmachtbiker.kfm`.
- Geometry: 1674 triangles, 3 skins, 32 joint bindings, 3 textures.
- Bounds: height 0.3201, radius 0.4800, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `ecbc41a9d593ecdd7685c447f99d4062838b8d788791bc5fb06e57331f532380`.
- idle: `art/units/motorcycle/wehrmachtbiker_md_idle.kf`.
- run: `art/units/motorcycle/wehrmachtbiker_md_run.kf`.
- fortify: `art/units/motorcycle/wehrmachtbiker_md_rangedfortify.kf`.
- attack: `art/units/motorcycle/wehrmachtbiker_md_rangedstrikea.kf`.
- hurt: `art/units/motorcycle/wehrmachtbiker_md_rangedhurta.kf`.
- die: `art/units/motorcycle/wehrmachtbiker_md_rangeddiea.kf`.

### military:motorized_infantry

- Model: `src/Mods/Military/art/models/motorized_infantry.glb` (118,468 bytes).
- Art: `ART_DEF_UNIT_MOTORIZED_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/armor_units/fiat_618_grom/zis-5b.nif`.
- KFM: `art/units/armor_units/fiat_618_grom/zis-5b.kfm`.
- Geometry: 358 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.3658, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `c7f4bb8bdef50abe0d216e3eaf95d9df02f1c3dd33296d0ae196fe1fdcc30874`.
- idle: `art/units/armor_units/fiat_618_grom/zis-5b_md_idle.kf`.
- run: `art/units/armor_units/fiat_618_grom/zis-5b_md_run.kf`.
- fortify: `art/units/armor_units/fiat_618_grom/zis-5b_md_rangedfortify.kf`.
- attack: `art/units/armor_units/fiat_618_grom/zis-5b_md_rangedstrike.kf`.
- hurt: `art/units/armor_units/fiat_618_grom/zis-5b_md_hurta.kf`.
- Note: First of 2 formation art groups exported

### military:mounted_infantry

- Model: `src/Mods/Military/art/models/mounted_infantry.glb` (651,788 bytes).
- Art: `ART_DEF_UNIT_MOUNTED_INFANTRY`. Source scale: 0.42.
- NIF: `art/units/mounted infantry/european/mswordsman_light.nif`.
- KFM: `art/units/horse archer/greek/companion_cavalry.kfm`.
- Geometry: 1114 triangles, 3 skins, 38 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2868, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `d901ad10c0740ff1f57fc8b2fd2b3ee9fa2fb91e5b8c1bd76d954cf5e85b7b21`.
- idle: `art/units/horse archer/greek/companion_cavalry_md_idle.kf`.
- run: `art/units/horse archer/greek/companion_cavalry_md_run.kf`.
- fortify: `art/units/horse archer/greek/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/horse archer/greek/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/horse archer/greek/companion_cavalry_md_hurta.kf`.
- die: `art/units/horse archer/greek/companion_cavalry_md_diea.kf`.

### military:mounted_rifleman

- Model: `src/Mods/Military/art/models/mounted_rifleman.glb` (559,540 bytes).
- Art: `ART_DEF_UNIT_MOUNTED_RIFLEMAN`. Source scale: 0.42.
- NIF: `art/units/mounted rifleman/european/cavalry_fx.nif`.
- KFM: `art/units/cavalry/cavalry.kfm`.
- Geometry: 1156 triangles, 4 skins, 38 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2927, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (86 channels), attack (86 channels), hurt (86 channels), die (86 channels).
- SHA-256: `5df5fb104ab5373da3b7f5fb373a22345fa4470ad08a2ebb877c921944c5ef10`.
- idle: `art/units/cavalry/cavalry_md_idle.kf`.
- run: `art/units/cavalry/cavalry_md_run.kf`.
- fortify: `art/units/cavalry/cavalry_md_fortifya.kf`.
- attack: `art/units/cavalry/cavalry_md_strikea.kf`.
- hurt: `art/units/cavalry/cavalry_md_hurta.kf`.
- die: `art/units/cavalry/cavalry_md_diea.kf`.
- Note: Hidden geometry omitted: CavalryRider01

### military:mrbm

- Model: `src/Mods/Military/art/models/mrbm.glb` (137,548 bytes).
- Art: `ART_DEF_UNIT_MRBM`. Source scale: 0.85.
- NIF: `art/units/nukes/mrbm/icbm.nif`.
- KFM: `art/units/nukes/mrbm/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `6524246b1e6e1facecad101e0a1ac5e04c509e81888b875714fb767d1c057668`.
- idle: `art/units/nukes/mrbm/icbm_md_idle.kf`.
- run: `art/units/nukes/mrbm/icbm_md_run.kf`.
- attack: `art/units/nukes/mrbm/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:musketman

- Model: `src/Mods/Military/art/models/musketman.glb` (383,020 bytes).
- Art: `ART_DEF_UNIT_MUSKETMAN`. Source scale: 0.44.
- NIF: `art/units/musketman/european/redcoat.nif`.
- KFM: `art/units/unique/england/redcoat/redcoat.kfm`.
- Geometry: 800 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2387, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `381e6cb7b92a5a5574d5bfcbac016dfd64715aeeaade2a4e0ca82d7eb970e6ca`.
- idle: `art/units/unique/england/redcoat/redcoat_md_idle.kf`.
- run: `art/units/unique/england/redcoat/redcoat_md_run.kf`.
- fortify: `art/units/unique/england/redcoat/redcoat_md_rangedfortify.kf`.
- attack: `art/units/unique/england/redcoat/redcoat_md_rangedstrike.kf`.
- hurt: `art/units/unique/england/redcoat/redcoat_md_hurta.kf`.
- die: `art/units/unique/england/redcoat/redcoat_md_rangeddiea.kf`.

### military:nagualism_missionary

- Model: `src/Mods/Military/art/models/nagualism_missionary.glb` (301,196 bytes).
- Art: `ART_DEF_UNIT_TOLTEC_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/tlenamacac/greatprophet.nif`.
- KFM: `art/units/missionary/tlenamacac/greatprophet.kfm`.
- Geometry: 1540 triangles, 2 skins, 34 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3117, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels).
- SHA-256: `51173a33e36415bd9ebe7bdda792ebaebcbf389d37526ed7ec7c66d8cd498f1d`.
- idle: `art/units/missionary/tlenamacac/greatprophet_md_idle.kf`.
- run: `art/units/missionary/tlenamacac/greatprophet_md_run.kf`.
- fortify: `art/units/missionary/tlenamacac/greatprophet_md_fortifya.kf`.
- Note: Missing texture: \\shamstaff.dds
- Note: Missing texture: \\skull.dds

### military:nanite_cloud

- Model: `src/Mods/Military/art/models/nanite_cloud.glb` (600,072 bytes).
- Art: `ART_DEF_UNIT_ENTITY`. Source scale: 0.6.
- NIF: `art/units/afterworld_boss/afterworld_boss.nif`.
- KFM: `art/units/afterworld_boss/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.1958, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), attack (47 channels), die (47 channels).
- SHA-256: `97f5ff5f4f6a13683c7e89bf75b3f83d1716aaad63d74ecaf1495bb6d7054352`.
- idle: `art/units/afterworld_boss/afterworld_boss_md_idle.kf`.
- attack: `art/units/afterworld_boss/afterworld_boss_md_strikea.kf`.
- die: `art/units/afterworld_boss/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:nanite_spy

- Model: `src/Mods/Military/art/models/nanite_spy.glb` (582,316 bytes).
- Art: `ART_DEF_UNIT_NANITE_SPY`. Source scale: 0.1.
- NIF: `art/units/nanite spy/afterworld_boss.nif`.
- KFM: `art/units/nanite spy/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.1958, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), attack (47 channels), die (47 channels).
- SHA-256: `57abd168a6c3690d625fff6f7d9b511e9311bfc146c88714dd7cdd44c4d62601`.
- idle: `art/units/nanite spy/afterworld_boss_md_idle.kf`.
- attack: `art/units/nanite spy/afterworld_boss_md_strikea.kf`.
- die: `art/units/nanite spy/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:nanite_swarm

- Model: `src/Mods/Military/art/models/nanite_swarm.glb` (583,108 bytes).
- Art: `ART_DEF_UNIT_NANITE_SWARM`. Source scale: 0.5.
- NIF: `art/units/nanite swarm/afterworld_boss.nif`.
- KFM: `art/units/nanite swarm/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.1958, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), attack (47 channels), die (47 channels).
- SHA-256: `d84208734668edfbb81af9004928cd5e807f0f1c1d3f7d06b838831e025172a0`.
- idle: `art/units/nanite swarm/afterworld_boss_md_idle.kf`.
- attack: `art/units/nanite swarm/afterworld_boss_md_strikea.kf`.
- die: `art/units/nanite swarm/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:native_america_dog_soldier

- Model: `src/Mods/Military/art/models/native_america_dog_soldier.glb` (352,300 bytes).
- Art: `ART_DEF_UNIT_NATIVE_AMERICA_DOG_SOLDIER`. Source scale: 0.44.
- NIF: `art/units/unique_nativeamerica_dogsoldier/dogsoldier.nif`.
- KFM: `art/units/unique_nativeamerica_dogsoldier/dogsoldier.kfm`.
- Geometry: 1014 triangles, 4 skins, 23 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2684, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels), hurt (55 channels), die (55 channels).
- SHA-256: `cea0544716e9ad9f9141f891f79528a652dafd6dbd2d49dfb07434b15fbea51b`.
- idle: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_idle.kf`.
- run: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_run.kf`.
- fortify: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_fortifya.kf`.
- attack: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_strikea.kf`.
- hurt: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_hurta.kf`.
- die: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_diea.kf`.

### military:netherlands_oostindievaarder

- Model: `src/Mods/Military/art/models/netherlands_oostindievaarder.glb` (270,024 bytes).
- Art: `ART_DEF_UNIT_NETHERLANDS_OOSTINDIEVAARDER`. Source scale: 0.2.
- NIF: `art/units/dutch_east_india_man/dutch_east_india_man.nif`.
- KFM: `art/units/dutch_east_india_man/dutch_east_india_man.kfm`.
- Geometry: 844 triangles, 3 skins, 27 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4458, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels), attack (43 channels), hurt (43 channels), die (43 channels).
- SHA-256: `82405ccf37113aec6066aef1cbd7e2ba9578903aa096532009b49f01abe8ee86`.
- idle: `art/units/dutch_east_india_man/dutch_east_india_man_md_idle.kf`.
- run: `art/units/dutch_east_india_man/dutch_east_india_man_md_run.kf`.
- fortify: `art/units/dutch_east_india_man/dutch_east_india_man_md_fortifya.kf`.
- attack: `art/units/dutch_east_india_man/dutch_east_india_man_md_strikea.kf`.
- hurt: `art/units/dutch_east_india_man/dutch_east_india_man_md_hurta.kf`.
- die: `art/units/dutch_east_india_man/dutch_east_india_man_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:nlos_cannon

- Model: `src/Mods/Military/art/models/nlos_cannon.glb` (347,224 bytes).
- Art: `ART_DEF_UNIT_NLOS_CANNON`. Source scale: 0.57.
- NIF: `art/units/nlos cannon/disari.nif`.
- KFM: `art/units/nlos cannon/mobileartillery.kfm`.
- Geometry: 1306 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.3093, radius 0.4800, feet Y=0.
- Clips: idle (16 channels), run (16 channels), fortify (16 channels), attack (16 channels), hurt (16 channels).
- SHA-256: `16a09a5f79431a913546f396c1878b3c2a9d21d2d9cabcb76535bfc5075f97d7`.
- idle: `art/units/nlos cannon/mobileartillery_md_idle.kf`.
- run: `art/units/nlos cannon/mobileartillery_md_run.kf`.
- fortify: `art/units/nlos cannon/mobileartillery_md_rangedfortify.kf`.
- attack: `art/units/nlos cannon/mobileartillery_md_rangedstrike.kf`.
- hurt: `art/units/nlos cannon/mobileartillery_md_hurta.kf`.

### military:nuclear_carrier

- Model: `src/Mods/Military/art/models/nuclear_carrier.glb` (296,472 bytes).
- Art: `ART_DEF_UNIT_NUCLEAR_CARRIER`. Source scale: 1.1.
- NIF: `art/units/carrier/carrier.nif`.
- KFM: `art/units/carrier/carrier.kfm`.
- Geometry: 603 triangles, 2 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.2029, radius 0.4800, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `55bb00a08c69189ffe7585dff861d37fb3ee62a34339951888d582cef8573fba`.
- idle: `art/units/carrier/carrier_md_idle.kf`.
- run: `art/units/carrier/carrier_md_run.kf`.
- fortify: `art/units/carrier/carrier_md_fortifya.kf`.
- attack: `art/units/carrier/carrier_md_strikea.kf`.
- hurt: `art/units/carrier/carrier_md_hurta.kf`.
- die: `art/units/carrier/carrier_md_diea.kf`.

### military:nuclear_submarine

- Model: `src/Mods/Military/art/models/nuclear_submarine.glb` (181,672 bytes).
- Art: `ART_DEF_UNIT_NUCLEAR_SUBMARINE`. Source scale: 0.75.
- NIF: `art/units/missile submarine/american/ohio.nif`.
- KFM: `art/units/submarine/submarine.kfm`.
- Geometry: 620 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.1843, radius 0.4800, feet Y=0.
- Clips: idle (31 channels), run (31 channels), fortify (31 channels), attack (31 channels), hurt (31 channels), die (31 channels).
- SHA-256: `b66e427444f7cd9c0ee758094d95e834bf2aa4d28ad97d6e28be768200885fc7`.
- idle: `art/units/submarine/submarine_md_idle.kf`.
- run: `art/units/submarine/submarine_md_run.kf`.
- fortify: `art/units/submarine/submarine_md_fortifya.kf`.
- attack: `art/units/submarine/submarine_md_strikea.kf`.
- hurt: `art/units/submarine/submarine_md_hurta.kf`.
- die: `art/units/submarine/submarine_md_diea.kf`.

### military:orbital_bomber

- Model: `src/Mods/Military/art/models/orbital_bomber.glb` (127,504 bytes).
- Art: `ART_DEF_UNIT_ORBITAL_BOMBER`. Source scale: 0.5.
- NIF: `art/units/thunderbolt/jf_smart.nif`.
- KFM: `none`.
- Geometry: 676 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.1202, radius 0.4800, feet Y=0.
- Clips: none.
- SHA-256: `6399599708362b73fc7f7d3e5c7863baf67b6f56744156490ffec8eac49ef443`.

### military:orbital_fighter

- Model: `src/Mods/Military/art/models/orbital_fighter.glb` (260,976 bytes).
- Art: `ART_DEF_UNIT_ORBITAL_FIGHTER`. Source scale: 0.45.
- NIF: `art/units/hammerheadrefar/jetfighter.nif`.
- KFM: `art/units/hammerheadrefar/jetfighter.kfm`.
- Geometry: 1001 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.1069, radius 0.4800, feet Y=0.
- Clips: idle (34 channels), run (34 channels), hurt (34 channels).
- SHA-256: `90b5424a5494907cb64ca241688e95f62ef3148a6fbfb61c9ef6777370cbd22d`.
- idle: `art/units/hammerheadrefar/jetfighter_md_idle.kf`.
- run: `art/units/hammerheadrefar/jetfighter_md_run.kf`.
- hurt: `art/units/hammerheadrefar/jetfighter_md_hurt.kf`.

### military:ottoman_janissary

- Model: `src/Mods/Military/art/models/ottoman_janissary.glb` (308,836 bytes).
- Art: `ART_DEF_UNIT_OTTOMAN_JANISSARY`. Source scale: 0.44.
- NIF: `art/units/janissary/janissary.nif`.
- KFM: `art/units/janissary/janissary.kfm`.
- Geometry: 696 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2583, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `3bd064f690fe77f04588506025fca38b8edf0207377980035dc179231de34353`.
- idle: `art/units/janissary/janissary_md_idle.kf`.
- run: `art/units/janissary/janissary_md_run.kf`.
- fortify: `art/units/janissary/janissary_md_rangedfortify.kf`.
- attack: `art/units/janissary/janissary_md_rangedstrike.kf`.
- hurt: `art/units/janissary/janissary_md_hurta.kf`.
- die: `art/units/janissary/janissary_md_rangeddiea.kf`.

### military:panther

- Model: `src/Mods/Military/art/models/panther.glb` (269,824 bytes).
- Art: `ART_DEF_UNIT_PANTHER`. Source scale: 0.5.
- NIF: `art/units/panther/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `3693b9d0fc02ac5d23158ddd6db851231ea27e4b3442749073f61d23599ccff8`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:paratrooper

- Model: `src/Mods/Military/art/models/paratrooper.glb` (503,088 bytes).
- Art: `ART_DEF_UNIT_PARATROOPER`. Source scale: 0.44.
- NIF: `art/units/paratrooper/paratrooper.nif`.
- KFM: `art/units/paratrooper/paratrooper.kfm`.
- Geometry: 882 triangles, 2 skins, 26 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3308, feet Y=0.
- Clips: idle (101 channels), run (101 channels), fortify (101 channels), attack (101 channels), die (101 channels).
- SHA-256: `a0dc343c84984ee2b5be051699a86592ccfe21690726218de5deef626c024798`.
- idle: `art/units/paratrooper/paratrooper_md_idle.kf`.
- run: `art/units/paratrooper/paratrooper_md_run.kf`.
- fortify: `art/units/paratrooper/paratrooper_md_rangedfortify.kf`.
- attack: `art/units/paratrooper/paratrooper_md_rangedstrike.kf`.
- die: `art/units/paratrooper/paratrooper_md_rangeddiea.kf`.
- Note: Hidden geometry omitted: Biped Object
- Note: Hidden geometry omitted: Biped Object@#0
- Note: Hidden geometry omitted: Biped Object@#1
- Note: Hidden geometry omitted: Biped Object@#2
- Note: Hidden geometry omitted: Biped Object@#3
- Note: Hidden geometry omitted: Box
- Note: Hidden geometry omitted: Biped Object@#4
- Note: Hidden geometry omitted: Biped Object@#5
- Note: Hidden geometry omitted: Biped Object@#6

### military:persia_immortal

- Model: `src/Mods/Military/art/models/persia_immortal.glb` (601,368 bytes).
- Art: `ART_DEF_UNIT_PERSIA_IMMORTAL`. Source scale: 0.42.
- NIF: `art/units/unique/persia/immortal/persianimmortal.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 971 triangles, 3 skins, 36 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3880, feet Y=0.
- Clips: idle (107 channels), run (107 channels), fortify (107 channels), attack (107 channels), hurt (107 channels), die (107 channels).
- SHA-256: `7c295ccb1ede31104c831bae0826a6e2239346bbc13946c3b9dea86163250648`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:picts_cruithni

- Model: `src/Mods/Military/art/models/picts_cruithni.glb` (273,308 bytes).
- Art: `ART_DEF_UNIT_PICTS_CRUITHNI`. Source scale: 0.44.
- NIF: `art/picts/cruithni/swordsman_fx.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3230, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), attack (56 channels), hurt (56 channels), die (56 channels).
- SHA-256: `683d49e468d684e15f46e94391985e22982504448b650582b30281eb8c29b216`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:pikeman

- Model: `src/Mods/Military/art/models/pikeman.glb` (322,848 bytes).
- Art: `ART_DEF_UNIT_PIKEMAN`. Source scale: 0.44.
- NIF: `art/units/pikeman/pikeman.nif`.
- KFM: `art/units/pikeman/pikeman.kfm`.
- Geometry: 864 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5337, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `f0aaf1cc4bb8488c55b7634e52f31ff46b0c3e57371699d8b9e39a9d61b52e25`.
- idle: `art/units/pikeman/pikeman_md_idle.kf`.
- run: `art/units/pikeman/pikeman_md_run.kf`.
- fortify: `art/units/pikeman/pikeman_md_fortifya.kf`.
- attack: `art/units/pikeman/pikeman_md_strikea.kf`.
- hurt: `art/units/pikeman/pikeman_md_hurta.kf`.
- die: `art/units/pikeman/pikeman_md_diea.kf`.

### military:pioneer

- Model: `src/Mods/Military/art/models/pioneer.glb` (256,496 bytes).
- Art: `ART_DEF_UNIT_PIONEER_MALE`. Source scale: 0.46.
- NIF: `art/units/settler/settler_male.nif`.
- KFM: `art/units/settler/settler_male.kfm`.
- Geometry: 700 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3516, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), die (56 channels).
- SHA-256: `4fea8e192340b60522433bef0989fd97edf77df4b55e2942005ff4de6f8968fb`.
- idle: `art/units/settler/settler_male_md_idle.kf`.
- run: `art/units/settler/settler_male_md_run.kf`.
- fortify: `art/units/settler/settler_male_md_fortify.kf`.
- die: `art/units/settler/settler_male_md_idle_die.kf`.
- Note: First of 7 formation art groups exported

### military:plasma_armor

- Model: `src/Mods/Military/art/models/plasma_armor.glb` (279,552 bytes).
- Art: `ART_DEF_UNIT_HELLHOUND_ARMOR`. Source scale: 0.3.
- NIF: `art/units/plasma armor/hellhound.nif`.
- KFM: `art/units/plasma armor/hellhound.kfm`.
- Geometry: 584 triangles, 4 skins, 4 joint bindings, 3 textures.
- Bounds: height 0.4254, radius 0.4800, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels), attack (26 channels), hurt (26 channels).
- SHA-256: `d6c8a6a2b2d93f152631811ed078ab0571438874f8f6e451cef9f1040954cc71`.
- idle: `art/units/plasma armor/hellhound_md_idle.kf`.
- run: `art/units/plasma armor/hellhound_md_run.kf`.
- fortify: `art/units/plasma armor/hellhound_md_rangedfortify.kf`.
- attack: `art/units/plasma armor/hellhound_md_rangedstrike.kf`.
- hurt: `art/units/plasma armor/hellhound_md_hurta.kf`.

### military:poison_nova

- Model: `src/Mods/Military/art/models/poison_nova.glb` (134,744 bytes).
- Art: `ART_DEF_UNIT_POISON_NOVA`. Source scale: 0.85.
- NIF: `art/units/nukes/poisonnova/icbm.nif`.
- KFM: `art/units/nukes/poisonnova/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `dd8ed43a6b618a0e9a96d6002bcf6031c7500badc3151a9d6d45acabc092c789`.
- idle: `art/units/nukes/poisonnova/icbm_md_idle.kf`.
- run: `art/units/nukes/poisonnova/icbm_md_run.kf`.
- attack: `art/units/nukes/poisonnova/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:poison_nuke

- Model: `src/Mods/Military/art/models/poison_nuke.glb` (135,716 bytes).
- Art: `ART_DEF_UNIT_POISON_NUKE`. Source scale: 0.85.
- NIF: `art/units/nukes/poisonnuke/icbm.nif`.
- KFM: `art/units/nukes/poisonnuke/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `67f62c2ec43e092ea1623c8f92933dafc6d53eab16888abd3257228fb081ce9c`.
- idle: `art/units/nukes/poisonnuke/icbm_md_idle.kf`.
- run: `art/units/nukes/poisonnuke/icbm_md_run.kf`.
- attack: `art/units/nukes/poisonnuke/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:polarbear

- Model: `src/Mods/Military/art/models/polarbear.glb` (275,432 bytes).
- Art: `ART_DEF_UNIT_POLARBEAR`. Source scale: 0.7.
- NIF: `art/units/animals/polarbear/bear.nif`.
- KFM: `art/units/animals/polarbear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5204, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `05be98ad73f9126252c543da6acc2862fee63987d43e61816750167bd32b54cd`.
- idle: `art/units/animals/polarbear/bear_md_idle.kf`.
- run: `art/units/animals/polarbear/bear_md_run.kf`.
- fortify: `art/units/animals/polarbear/bear_md_fortifya.kf`.
- attack: `art/units/animals/polarbear/bear_md_strikea.kf`.
- hurt: `art/units/animals/polarbear/bear_md_hurta.kf`.
- die: `art/units/animals/polarbear/bear_md_diea.kf`.

### military:polish_heavy_hussar

- Model: `src/Mods/Military/art/models/polish_heavy_hussar.glb` (719,384 bytes).
- Art: `ART_DEF_UNIT_HEAVY_HUSSAR`. Source scale: 0.42.
- NIF: `art/poland/heavy hussar/heavycavalry.nif`.
- KFM: `art/poland/heavy hussar/heavycavalry.kfm`.
- Geometry: 1252 triangles, 2 skins, 35 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.3691, feet Y=0.
- Clips: idle (124 channels), run (124 channels), fortify (124 channels), attack (124 channels), hurt (124 channels), die (124 channels).
- SHA-256: `b487d992aa7fae5e4df404e2f5f9be611d58c1a96ef90be75225fb8317e2729e`.
- idle: `art/poland/heavy hussar/heavycavalry_md_idle.kf`.
- run: `art/poland/heavy hussar/heavycavalry_md_run.kf`.
- fortify: `art/poland/heavy hussar/heavycavalry_md_fortifya.kf`.
- attack: `art/poland/heavy hussar/heavycavalry_md_strikea.kf`.
- hurt: `art/poland/heavy hussar/heavycavalry_md_hurta.kf`.
- die: `art/poland/heavy hussar/heavycavalry_md_diea.kf`.
- Note: Hidden geometry omitted: PolyMesh
- Note: Hidden geometry omitted: Editable Poly
- Note: Hidden geometry omitted: Editable Poly@#1
- Note: Hidden geometry omitted: HeavyCavalry
- Note: Hidden geometry omitted: Heavy_CavalryHorse
- Note: Hidden geometry omitted: Heavy_CavalryHorse_Armor
- Note: Hidden geometry omitted: Editable Poly@#0

### military:portugal_carrack

- Model: `src/Mods/Military/art/models/portugal_carrack.glb` (228,872 bytes).
- Art: `ART_DEF_UNIT_PORTUGAL_CARRACK`. Source scale: 0.24.
- NIF: `art/units/portugal_carrack/portugal_carrack.nif`.
- KFM: `art/units/portugal_carrack/portugal_carrack.kfm`.
- Geometry: 482 triangles, 3 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4509, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `2f0fa835d7e38b6406b953c774497e843ff2ab4f0525b8fb1e6e4d9ac98d31b2`.
- idle: `art/units/portugal_carrack/portugal_carrack_md_idle.kf`.
- run: `art/units/portugal_carrack/portugal_carrack_md_run.kf`.
- fortify: `art/units/portugal_carrack/portugal_carrack_md_fortifya.kf`.
- attack: `art/units/portugal_carrack/portugal_carrack_md_strikea.kf`.
- hurt: `art/units/portugal_carrack/portugal_carrack_md_hurta.kf`.
- die: `art/units/portugal_carrack/portugal_carrack_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:power_armored_infantry

- Model: `src/Mods/Military/art/models/power_armored_infantry.glb` (301,644 bytes).
- Art: `ART_DEF_UNIT_POWER_ARMOR_INFANTRY`. Source scale: 0.75.
- NIF: `art/units/power armor infantry/warhammer.nif`.
- KFM: `art/units/power armor infantry/warhammer.kfm`.
- Geometry: 1568 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2592, feet Y=0.
- Clips: idle (57 channels), run (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `7c4bc9f6456284b7d552dea2e5c40460b3003ea6bd9175aace08ee5ed6fd8708`.
- idle: `art/units/power armor infantry/warhammer_md_idle.kf`.
- run: `art/units/power armor infantry/warhammer_md_run.kf`.
- attack: `art/units/power armor infantry/warhammer_md_strikeb.kf`.
- hurt: `art/units/power armor infantry/warhammer_md_hurta.kf`.
- die: `art/units/power armor infantry/warhammer_md_dieb.kf`.

### military:pre_dreadnought

- Model: `src/Mods/Military/art/models/pre_dreadnought.glb` (360,772 bytes).
- Art: `ART_DEF_UNIT_PRE_DREADNOUGHT`. Source scale: 0.6.
- NIF: `art/units/ironclad battleship/hms_canopus.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 1598 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.4166, radius 0.4800, feet Y=0.
- Clips: idle (63 channels), run (63 channels), fortify (63 channels), attack (63 channels), hurt (63 channels), die (63 channels).
- SHA-256: `843945fdfa8d1a14e145617e845781bdb678f454c9360048039836d716002a39`.
- idle: `art/units/destroyer/destroyer_md_idle.kf`.
- run: `art/units/destroyer/destroyer_md_run.kf`.
- fortify: `art/units/destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/destroyer/destroyer_md_hurta.kf`.
- die: `art/units/destroyer/destroyer_md_diea.kf`.

### military:precision_attack_missile

- Model: `src/Mods/Military/art/models/precision_attack_missile.glb` (102,992 bytes).
- Art: `ART_DEF_UNIT_PRECISION_ATTACK_MISSILE`. Source scale: 0.3.
- NIF: `art/units/precision attack missile/tomahawk.nif`.
- KFM: `art/units/guided_missile/guided_missile.kfm`.
- Geometry: 286 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.2314, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), attack (20 channels).
- SHA-256: `682d60fd534525269546736a26012dd060c586aa35f13ea597b45e1b2c6cdab7`.
- idle: `art/units/guided_missile/guided_missile_md_idle.kf`.
- run: `art/units/guided_missile/guided_missile_md_run.kf`.
- attack: `art/units/guided_missile/guided_missile_md_strikea.kf`.

### military:privateer

- Model: `src/Mods/Military/art/models/privateer.glb` (270,656 bytes).
- Art: `ART_DEF_UNIT_PRIVATEER`. Source scale: 0.2.
- NIF: `art/units/privateer/privateer.nif`.
- KFM: `art/units/privateer/privateer.kfm`.
- Geometry: 923 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3946, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `f2c316ce7513c826ec2915bf647b11e36b31e860bf8040bc47874e0e002c16e3`.
- idle: `art/units/privateer/privateer_md_idle.kf`.
- run: `art/units/privateer/privateer_md_run.kf`.
- fortify: `art/units/privateer/privateer_md_fortifya.kf`.
- attack: `art/units/privateer/privateer_md_strikea.kf`.
- hurt: `art/units/privateer/privateer_md_hurta.kf`.
- die: `art/units/privateer/privateer_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:prophet

- Model: `src/Mods/Military/art/models/prophet.glb` (173,920 bytes).
- Art: `ART_DEF_UNIT_PROPHET`. Source scale: 0.46.
- NIF: `art/units/greatpeople/prophet/greatprophet.nif`.
- KFM: `art/units/greatpeople/prophet/greatprophet.kfm`.
- Geometry: 552 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2844, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels).
- SHA-256: `40b912701ebfb8fd1ca8cfa23429b1ac686f815c0bb458b2a79962915c090628`.
- idle: `art/units/greatpeople/prophet/greatprophet_md_idle.kf`.
- run: `art/units/greatpeople/prophet/greatprophet_md_run.kf`.
- fortify: `art/units/greatpeople/prophet/greatprophet_md_fortifya.kf`.

### military:ram

- Model: `src/Mods/Military/art/models/ram.glb` (191,096 bytes).
- Art: `ART_DEF_UNIT_RAM`. Source scale: 0.70.
- NIF: `art/units/logram/logram.nif`.
- KFM: `none`.
- Geometry: 2352 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.4471, radius 0.4800, feet Y=0.
- Clips: none.
- SHA-256: `df17064bb79c945e68bc734f0f2bf5c469129a7e36580bac26e725077d117e0f`.

### military:rifleman

- Model: `src/Mods/Military/art/models/rifleman.glb` (282,508 bytes).
- Art: `ART_DEF_UNIT_RIFLEMAN`. Source scale: 0.44.
- NIF: `art/units/rifleman/rifleman.nif`.
- KFM: `art/units/rifleman/rifleman.kfm`.
- Geometry: 618 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2540, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `9647c11c39d0412acaf6ac65c338e020b62f09e1455faf31c87543740c0651e3`.
- idle: `art/units/rifleman/rifleman_md_idle.kf`.
- run: `art/units/rifleman/rifleman_md_run.kf`.
- fortify: `art/units/rifleman/rifleman_md_rangedfortify.kf`.
- attack: `art/units/rifleman/rifleman_md_rangedstrike.kf`.
- hurt: `art/units/rifleman/rifleman_md_hurta.kf`.
- die: `art/units/rifleman/rifleman_md_rangeddiea.kf`.

### military:rocket_artillery

- Model: `src/Mods/Military/art/models/rocket_artillery.glb` (250,188 bytes).
- Art: `ART_DEF_UNIT_ROCKET_ARTILLERY`. Source scale: 0.7.
- NIF: `art/units/rocket artillery/m270.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 400 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.3341, radius 0.4800, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels).
- SHA-256: `44d76a7544814a19739cde35b8511c7b6d3cf4bfe663074dcdcddd030c801b1b`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:rome_praetorian

- Model: `src/Mods/Military/art/models/rome_praetorian.glb` (280,468 bytes).
- Art: `ART_DEF_UNIT_ROME_PRAETORIAN`. Source scale: 0.44.
- NIF: `art/units/unique/roman/praetorian/praetorian.nif`.
- KFM: `art/units/unique/roman/praetorian/praetorian.kfm`.
- Geometry: 810 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3284, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `e68e3f7b87d04c2fd6902b95cf856645c1b6d814ce1a0b087f58c65a3ce9ec9f`.
- idle: `art/units/unique/roman/praetorian/praetorian_md_idle.kf`.
- run: `art/units/unique/roman/praetorian/praetorian_md_run.kf`.
- fortify: `art/units/unique/roman/praetorian/praetorian_md_fortifya.kf`.
- attack: `art/units/unique/roman/praetorian/praetorian_md_strikea.kf`.
- hurt: `art/units/unique/roman/praetorian/praetorian_md_hurta.kf`.
- die: `art/units/unique/roman/praetorian/praetorian_md_diea.kf`.

### military:rover

- Model: `src/Mods/Military/art/models/rover.glb` (202,396 bytes).
- Art: `ART_DEF_UNIT_ROVER`. Source scale: 1.1.
- NIF: `art/units/rover/rover.nif`.
- KFM: `art/units/rover/rover.kfm`.
- Geometry: 620 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.3668, radius 0.4800, feet Y=0.
- Clips: idle (21 channels), run (33 channels), fortify (33 channels), attack (33 channels), hurt (33 channels).
- SHA-256: `902792b89abee389371c882b2d3fd4d53f90666f26059d6ccbf68e67b0d98e71`.
- idle: `art/units/rover/rover_md_idle.kf`.
- run: `art/units/rover/rover_md_run.kf`.
- fortify: `art/units/rover/rover_md_rangedfortify.kf`.
- attack: `art/units/rover/rover_md_rangedstrike.kf`.
- hurt: `art/units/rover/rover_md_rangedhurta.kf`.

### military:russia_cossack

- Model: `src/Mods/Military/art/models/russia_cossack.glb` (586,716 bytes).
- Art: `ART_DEF_UNIT_RUSSIA_COSSACK`. Source scale: 0.42.
- NIF: `art/units/unique/russia/cossack/russiancossack.nif`.
- KFM: `art/units/unique/russia/cossack/russiancossack.kfm`.
- Geometry: 1076 triangles, 3 skins, 40 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3028, feet Y=0.
- Clips: idle (95 channels), run (95 channels), fortify (95 channels), attack (95 channels), hurt (95 channels), die (95 channels).
- SHA-256: `eb65e2446a490d57386b82c76e31f0aa0e3dc021ab1cdf3feaf36c6aa389bc14`.
- idle: `art/units/unique/russia/cossack/russiancossack_md_idle.kf`.
- run: `art/units/unique/russia/cossack/russiancossack_md_run.kf`.
- fortify: `art/units/unique/russia/cossack/russiancossack_md_fortifya.kf`.
- attack: `art/units/unique/russia/cossack/russiancossack_md_strikea.kf`.
- hurt: `art/units/unique/russia/cossack/russiancossack_md_hurta.kf`.
- die: `art/units/unique/russia/cossack/russiancossack_md_diea.kf`.

### military:sam_infantry

- Model: `src/Mods/Military/art/models/sam_infantry.glb` (380,784 bytes).
- Art: `ART_DEF_UNIT_SAM_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/saminfantry/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 754 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2850, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `b9056f8ed58d512cc503ca7f2cce12090384e5b021f08d1d831bde479d9deb9c`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.

### military:scientist

- Model: `src/Mods/Military/art/models/scientist.glb` (203,860 bytes).
- Art: `ART_DEF_UNIT_SCIENTIST`. Source scale: 0.46.
- NIF: `art/units/greatpeople/scientist/greatscientistancient.nif`.
- KFM: `art/units/greatpeople/scientist/greatscientistancient.kfm`.
- Geometry: 788 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3013, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels).
- SHA-256: `cd628337505ff9d4ace0ac0d2a1bf757755eb4f62ae582e9df8dde4881eb0043`.
- idle: `art/units/greatpeople/scientist/greatscientistancient_md_idle.kf`.
- run: `art/units/greatpeople/scientist/greatscientistancient_md_run.kf`.
- fortify: `art/units/greatpeople/scientist/greatscientistancient_md_fortify.kf`.

### military:scout

- Model: `src/Mods/Military/art/models/scout.glb` (310,212 bytes).
- Art: `ART_DEF_UNIT_SCOUT`. Source scale: 0.44.
- NIF: `art/units/scout/scout.nif`.
- KFM: `art/units/scout/scout.kfm`.
- Geometry: 740 triangles, 2 skins, 23 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2709, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `e18621e65d8c6beadc86f52c736bb07de5085b72450b0ab4bb3557ccfd96c9fc`.
- idle: `art/units/scout/scout_md_idle.kf`.
- run: `art/units/scout/scout_md_run.kf`.
- fortify: `art/units/scout/scout_md_fortifya.kf`.
- attack: `art/units/scout/scout_md_strikea.kf`.
- hurt: `art/units/scout/scout_md_hurta.kf`.
- die: `art/units/scout/scout_md_diea.kf`.

### military:scout_mech

- Model: `src/Mods/Military/art/models/scout_mech.glb` (349,428 bytes).
- Art: `ART_DEF_UNIT_SCOUT_MECH2`. Source scale: 1.5.
- NIF: `art/units/scout mech/mech.nif`.
- KFM: `art/units/scout mech/mech.kfm`.
- Geometry: 1519 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.5911, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels).
- SHA-256: `e783112fc5cc4f6358bf961f97b2aa3beecd633d136b4acda95e5c9f7095c65d`.
- idle: `art/units/scout mech/mech_md_idle.kf`.
- run: `art/units/scout mech/mech_md_run.kf`.
- fortify: `art/units/scout mech/mech_md_rangedfortify.kf`.
- attack: `art/units/scout mech/mech_md_rangedstrike.kf`.
- hurt: `art/units/scout mech/mech_md_rangedhurta.kf`.

### military:scramjet

- Model: `src/Mods/Military/art/models/scramjet.glb` (132,296 bytes).
- Art: `ART_DEF_UNIT_SCRAMJET`. Source scale: 0.48.
- NIF: `art/units/orbital fighters/spacefighter_y.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 142 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.2280, radius 0.4800, feet Y=0.
- Clips: idle (34 channels), run (34 channels), hurt (34 channels).
- SHA-256: `09c5386c65ff7cfe079d2e31c7ce2ccd9123aaaf93ed8896619f61a210d9d1ad`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:seaplane

- Model: `src/Mods/Military/art/models/seaplane.glb` (206,800 bytes).
- Art: `ART_DEF_UNIT_SEAPLANE`. Source scale: 0.4.
- NIF: `art/units/seaplane/generic/e16a1.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 1129 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.3385, radius 0.4800, feet Y=0.
- Clips: idle (10 channels), run (10 channels), hurt (10 channels), die (10 channels).
- SHA-256: `212f6401aab9f3f4654a526b5bd074ba393a60dac35e8bef901fdf9c81b7c134`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:settler

- Model: `src/Mods/Military/art/models/settler.glb` (256,496 bytes).
- Art: `ART_DEF_UNIT_SETTLER_MALE`. Source scale: 0.46.
- NIF: `art/units/settler/settler_male.nif`.
- KFM: `art/units/settler/settler_male.kfm`.
- Geometry: 700 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3516, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), die (56 channels).
- SHA-256: `4fea8e192340b60522433bef0989fd97edf77df4b55e2942005ff4de6f8968fb`.
- idle: `art/units/settler/settler_male_md_idle.kf`.
- run: `art/units/settler/settler_male_md_run.kf`.
- fortify: `art/units/settler/settler_male_md_fortify.kf`.
- die: `art/units/settler/settler_male_md_idle_die.kf`.
- Note: First of 7 formation art groups exported

### military:shaolin

- Model: `src/Mods/Military/art/models/shaolin.glb` (286,824 bytes).
- Art: `ART_DEF_UNIT_SHAOLIN`. Source scale: 0.44.
- NIF: `art/units/shaolin/asian_warrior.nif`.
- KFM: `art/units/warrior/warrior.kfm`.
- Geometry: 542 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3425, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `2a277730ef54976dc75ef51d18e7b2fb272139e35ede257bbcf961c5162366b1`.
- idle: `art/units/warrior/warrior_md_idle.kf`.
- run: `art/units/warrior/warrior_md_run.kf`.
- fortify: `art/units/warrior/warrior_md_fortifya.kf`.
- attack: `art/units/warrior/warrior_md_strikea.kf`.
- hurt: `art/units/warrior/warrior_md_hurta.kf`.
- die: `art/units/warrior/warrior_md_diea.kf`.

### military:shinto_missionary

- Model: `src/Mods/Military/art/models/shinto_missionary.glb` (188,528 bytes).
- Art: `ART_DEF_UNIT_SHINTO_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/kannushi/confucian_missionary.nif`.
- KFM: `art/units/kannushi/confucian_missionary.kfm`.
- Geometry: 480 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2507, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), die (50 channels).
- SHA-256: `93ebc80b14e2ce9e1cca4b5da67d9f7935f134edacd5730bbbbaf9ea6301c955`.
- idle: `art/units/kannushi/confucian_missionary_md_idle.kf`.
- run: `art/units/kannushi/confucian_missionary_md_run.kf`.
- fortify: `art/units/kannushi/confucian_missionary_md_fortifya.kf`.
- die: `art/units/kannushi/confucian_missionary_md_idle_die.kf`.

### military:ship_of_the_line

- Model: `src/Mods/Military/art/models/ship_of_the_line.glb` (178,008 bytes).
- Art: `ART_DEF_UNIT_SHIP_OF_THE_LINE`. Source scale: 0.22.
- NIF: `art/units/ship_0f_the_line/ship_0f_the_line.nif`.
- KFM: `art/units/ship_0f_the_line/ship_of_the_line.kfm`.
- Geometry: 1257 triangles, 2 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4446, feet Y=0.
- Clips: idle (1 channels), run (1 channels), fortify (1 channels), attack (1 channels), hurt (1 channels), die (1 channels).
- SHA-256: `df71060abe8360b635955bd8295a91c4c1142857fd674c296b70b0b7cd151bab`.
- idle: `art/units/ship_0f_the_line/ship_of_the_line_md_idle.kf`.
- run: `art/units/ship_0f_the_line/ship_of_the_line_md_run.kf`.
- fortify: `art/units/ship_0f_the_line/ship_of_the_line_md_fortifya.kf`.
- attack: `art/units/ship_0f_the_line/ship_of_the_line_md_strikea.kf`.
- hurt: `art/units/ship_0f_the_line/ship_of_the_line_md_hurta.kf`.
- die: `art/units/ship_0f_the_line/ship_of_the_line_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:siam_elephant

- Model: `src/Mods/Military/art/models/siam_elephant.glb` (1,056,604 bytes).
- Art: `ART_DEF_UNIT_SIAM_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/unique/siamese/battle elephant/warelephant_european_fx.nif`.
- KFM: `art/units/unique/siamese/battle elephant/warelephant_carthage.kfm`.
- Geometry: 2813 triangles, 4 skins, 56 joint bindings, 9 textures.
- Bounds: height 0.6000, radius 0.2422, feet Y=0.
- Clips: idle (104 channels), run (104 channels), fortify (104 channels), attack (104 channels), hurt (104 channels), die (104 channels).
- SHA-256: `89263b36cf1bdc5b47da623bbc7039bde3033f7df5558f8aaafa1a548c395b3e`.
- idle: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_idle.kf`.
- run: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_run.kf`.
- fortify: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_fortifya.kf`.
- attack: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_strikea.kf`.
- hurt: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_hurta.kf`.
- die: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_diea.kf`.

### military:siberiantiger

- Model: `src/Mods/Military/art/models/siberiantiger.glb` (349,528 bytes).
- Art: `ART_DEF_UNIT_SIBERIANTIGER`. Source scale: 0.67.
- NIF: `art/units/animals/siberiantiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `2e8d6400f08685625714244763b72c724f4a0297bb890dddcea2c3fc0d611e58`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:siege_droid

- Model: `src/Mods/Military/art/models/siege_droid.glb` (626,572 bytes).
- Art: `ART_DEF_UNIT_SIEGE_DROID`. Source scale: 0.8.
- NIF: `art/units/siege droid/mech.nif`.
- KFM: `art/units/siege droid/mech.kfm`.
- Geometry: 2478 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.5480, radius 0.4800, feet Y=0.
- Clips: idle (138 channels), run (138 channels), fortify (138 channels), attack (138 channels), hurt (138 channels).
- SHA-256: `5b762ec6f0fc358aa04a885f32844c372ef8066cd88de24e6c4175efcbbd020c`.
- idle: `art/units/siege droid/mech_md_idle.kf`.
- run: `art/units/siege droid/mech_md_run.kf`.
- fortify: `art/units/siege droid/mech_md_rangedfortify.kf`.
- attack: `art/units/siege droid/mech_md_rangedstrike.kf`.
- hurt: `art/units/siege droid/mech_md_rangedhurta.kf`.

### military:siege_ram

- Model: `src/Mods/Military/art/models/siege_ram.glb` (326,344 bytes).
- Art: `ART_DEF_UNIT_SIEGE_RAM`. Source scale: 0.6.
- NIF: `art/units/siege ram/ram.nif`.
- KFM: `art/units/siege ram/ram.kfm`.
- Geometry: 334 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.3463, radius 0.4800, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels).
- SHA-256: `3add3f5a66d8c3261226442a702d7c77ac613050ae6fb0116418fcb80e227097`.
- idle: `art/units/siege ram/ram_md_idle.kf`.
- run: `art/units/siege ram/ram_md_run.kf`.
- fortify: `art/units/siege ram/ram_md_rangedfortify.kf`.
- attack: `art/units/siege ram/ram_md_rangedstrike.kf`.
- hurt: `art/units/siege ram/ram_md_hurta.kf`.

### military:slinger

- Model: `src/Mods/Military/art/models/slinger.glb` (442,648 bytes).
- Art: `ART_DEF_UNIT_SLINGER`. Source scale: 0.44.
- NIF: `art/units/peltast/peltast.nif`.
- KFM: `art/units/peltast/peltast.kfm`.
- Geometry: 792 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2981, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `5362955a3c21aeddcd498353a09115a5f6ef7f750d3218cba8cda31383756d57`.
- idle: `art/units/peltast/peltast_md_idle.kf`.
- run: `art/units/peltast/peltast_md_run.kf`.
- fortify: `art/units/peltast/peltast_md_ranged_fortify.kf`.
- attack: `art/units/peltast/peltast_md_rangedstrike.kf`.
- hurt: `art/units/peltast/peltast_md_hurta.kf`.
- die: `art/units/peltast/peltast_md_rangeddiea.kf`.

### military:sloop

- Model: `src/Mods/Military/art/models/sloop.glb` (217,728 bytes).
- Art: `ART_DEF_UNIT_CARAVEL_SIMPLERIG`. Source scale: 0.18.
- NIF: `art/units/sloop/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 797 triangles, 3 skins, 12 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4187, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels), die (20 channels).
- SHA-256: `d10105b9bf02577e6f20cec3098cc11a13f24031f63208fd5832b7ccb4f1de3a`.
- idle: `art/units/caravel/caravel_md_idle.kf`.
- run: `art/units/caravel/caravel_md_run.kf`.
- fortify: `art/units/caravel/caravel_md_fortifya.kf`.
- attack: `art/units/caravel/caravel_md_strikea.kf`.
- hurt: `art/units/caravel/caravel_md_hurta.kf`.
- die: `art/units/caravel/caravel_md_diea.kf`.

### military:spanish_conquistador

- Model: `src/Mods/Military/art/models/spanish_conquistador.glb` (603,008 bytes).
- Art: `ART_DEF_UNIT_SPANISH_CONQUISTADOR`. Source scale: 0.42.
- NIF: `art/units/unique/spanish/conquistador/conquistador.nif`.
- KFM: `art/units/unique/spanish/conquistador/conquistador.kfm`.
- Geometry: 1019 triangles, 3 skins, 50 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2648, feet Y=0.
- Clips: idle (106 channels), run (106 channels), fortify (106 channels), attack (106 channels), hurt (106 channels), die (106 channels).
- SHA-256: `c2fdd35fa4fc5b650abd5962cd94b671ac57917fa839dd2c99f1e5c482874c46`.
- idle: `art/units/unique/spanish/conquistador/conquistador_md_idle.kf`.
- run: `art/units/unique/spanish/conquistador/conquistador_md_run.kf`.
- fortify: `art/units/unique/spanish/conquistador/conquistador_md_fortifya.kf`.
- attack: `art/units/unique/spanish/conquistador/conquistador_md_strikea.kf`.
- hurt: `art/units/unique/spanish/conquistador/conquistador_md_hurta.kf`.
- die: `art/units/unique/spanish/conquistador/conquistador_md_diea.kf`.
- Note: Missing texture: Knight_horse_128.dds

### military:spearman

- Model: `src/Mods/Military/art/models/spearman.glb` (282,480 bytes).
- Art: `ART_DEF_UNIT_SPEARMAN`. Source scale: 0.44.
- NIF: `art/units/unique/greek/phalanx/greekphalanx.nif`.
- KFM: `art/units/unique/greek/phalanx/greekphalanx.kfm`.
- Geometry: 728 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4289, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `dc8b0ba751d8311a921d4668606962ce9ba4e4c9808a67f039a85240546ef920`.
- idle: `art/units/unique/greek/phalanx/greekphalanx_md_idle.kf`.
- run: `art/units/unique/greek/phalanx/greekphalanx_md_run.kf`.
- fortify: `art/units/unique/greek/phalanx/greekphalanx_md_fortifya.kf`.
- attack: `art/units/unique/greek/phalanx/greekphalanx_md_strikea.kf`.
- hurt: `art/units/unique/greek/phalanx/greekphalanx_md_hurta.kf`.
- die: `art/units/unique/greek/phalanx/greekphalanx_md_diea.kf`.

### military:special_agent

- Model: `src/Mods/Military/art/models/special_agent.glb` (490,928 bytes).
- Art: `ART_DEF_UNIT_SPECIAL_AGENT`. Source scale: 0.46.
- NIF: `art/units/specialagent/specialagent.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 811 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2761, feet Y=0.
- Clips: idle (65 channels), run (65 channels), fortify (65 channels), attack (65 channels).
- SHA-256: `40f99b19450dbc3f5e6c599bbac42dd5948062927b60a596d064b2db7499c4b6`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:special_forces

- Model: `src/Mods/Military/art/models/special_forces.glb` (383,664 bytes).
- Art: `ART_DEF_UNIT_SPECIAL_FORCES`. Source scale: 0.44.
- NIF: `art/units/unique/america/navyseal/navyseal.nif`.
- KFM: `art/units/unique/america/navyseal/navyseal.kfm`.
- Geometry: 802 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3761, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `422f88562c2852a9bf077cfe8a837efe8138aaa06fedee060dd626ed3d9aa79d`.
- idle: `art/units/unique/america/navyseal/navyseal_md_idle.kf`.
- run: `art/units/unique/america/navyseal/navyseal_md_run.kf`.
- fortify: `art/units/unique/america/navyseal/navyseal_md_rangedfortify.kf`.
- attack: `art/units/unique/america/navyseal/navyseal_md_rangedstrike.kf`.
- hurt: `art/units/unique/america/navyseal/navyseal_md_hurta.kf`.
- die: `art/units/unique/america/navyseal/navyseal_md_rangeddiea.kf`.

### military:special_infantry

- Model: `src/Mods/Military/art/models/special_infantry.glb` (400,520 bytes).
- Art: `ART_DEF_UNIT_SPACE_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/space infantry/marines.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 1190 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3416, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `67a7b8a4f072b0fbb7dd26d9812df38ba4d4ee897004b26af88af4abb10a2c67`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:spy

- Model: `src/Mods/Military/art/models/spy.glb` (418,868 bytes).
- Art: `ART_DEF_UNIT_ANCIENT_SPY`. Source scale: 0.46.
- NIF: `art/units/ancientspy/ancientspy.nif`.
- KFM: `art/units/ancientspy/ancientspy.kfm`.
- Geometry: 678 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4148, feet Y=0.
- Clips: idle (62 channels), run (62 channels), fortify (62 channels), attack (62 channels).
- SHA-256: `3a6c7c358b5b72f5d9dbba3d8cd0ece31d379beff039a36088bac96466523e85`.
- idle: `art/units/ancientspy/ancientspy_md_idle.kf`.
- run: `art/units/ancientspy/ancientspy_md_run.kf`.
- fortify: `art/units/ancientspy/ancientspy_md_fortify.kf`.
- attack: `art/units/ancientspy/ancientspy_md_rangedstrike.kf`.
- Note: Missing texture: Great_Spy_Ancient_DIFF.dds

### military:sr71

- Model: `src/Mods/Military/art/models/sr71.glb` (92,864 bytes).
- Art: `ART_DEF_UNIT_SR71`. Source scale: 0.48.
- NIF: `art/units/sr71/sr71.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 726 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.1231, radius 0.4800, feet Y=0.
- Clips: idle (33 channels), run (33 channels), hurt (33 channels).
- SHA-256: `1ca2a4ae299694053bca49bebf0ef1b0028620aeb2ed60576c2e4d9fed1ddbea`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:stag

- Model: `src/Mods/Military/art/models/stag.glb` (489,108 bytes).
- Art: `ART_DEF_UNIT_STAG`. Source scale: 0.38.
- NIF: `art/units/stag/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 392 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2783, feet Y=0.
- Clips: idle (100 channels), run (100 channels), fortify (100 channels), attack (100 channels), hurt (100 channels), die (100 channels).
- SHA-256: `ee8a3a5627b32cdfc74a1cd505140ea9ff7d6a186f1b2575b99d1bb40088061d`.
- idle: `art/units/horsearcher/horsearcher_md_idle.kf`.
- run: `art/units/horsearcher/horsearcher_md_run.kf`.
- fortify: `art/units/horsearcher/horsearcher_md_fortifya.kf`.
- attack: `art/units/horsearcher/horsearcher_md_strikea.kf`.
- hurt: `art/units/horsearcher/horsearcher_md_hurta.kf`.
- die: `art/units/horsearcher/horsearcher_md_diea.kf`.
- Note: Hidden geometry omitted: HorseArcher_Horse
- Note: Hidden geometry omitted: Deer_Doe
- Note: Hidden geometry omitted: Deer_Buck02
- Note: Hidden geometry omitted: shadow_plane

### military:stallion

- Model: `src/Mods/Military/art/models/stallion.glb` (337,660 bytes).
- Art: `ART_DEF_UNIT_STALLION`. Source scale: 0.3.
- NIF: `art/units/horse/horsedark.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 432 triangles, 1 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2922, feet Y=0.
- Clips: idle (64 channels), run (64 channels), fortify (64 channels), attack (64 channels), hurt (64 channels), die (64 channels).
- SHA-256: `612e0153193bc61be4fa3ff0ee0f395c0a8cd28fa700a258eee0aa0760946c0b`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:stealth_armor

- Model: `src/Mods/Military/art/models/stealth_armor.glb` (360,984 bytes).
- Art: `ART_DEF_UNIT_STEALTH_ARMOR`. Source scale: 0.40.
- NIF: `art/units/stealth armor/myrmidon.nif`.
- KFM: `art/units/stealth armor/modernarmor.kfm`.
- Geometry: 476 triangles, 1 skins, 2 joint bindings, 2 textures.
- Bounds: height 0.2768, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels).
- SHA-256: `5d21014fd1bb58783a12f6f7a9ebb430af1e18167903b26597ad94e7e77adb93`.
- idle: `art/units/stealth armor/modernarmor_md_idle.kf`.
- run: `art/units/stealth armor/modernarmor_md_run.kf`.
- fortify: `art/units/stealth armor/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/stealth armor/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/stealth armor/modernarmor_md_hurta.kf`.

### military:stealth_bomber

- Model: `src/Mods/Military/art/models/stealth_bomber.glb` (176,864 bytes).
- Art: `ART_DEF_UNIT_STEALTH_BOMBER`. Source scale: 0.5.
- NIF: `art/units/stealthbomber/stealthbomber.nif`.
- KFM: `art/units/stealthbomber/stealthbomber.kfm`.
- Geometry: 536 triangles, 2 skins, 6 joint bindings, 1 textures.
- Bounds: height 0.0627, radius 0.4800, feet Y=0.
- Clips: idle (34 channels), run (34 channels), hurt (34 channels), die (34 channels).
- SHA-256: `7b42266257073a3414871d029defcd1b84327d458a6acd4885078c0e184c82b0`.
- idle: `art/units/stealthbomber/stealthbomber_md_idle.kf`.
- run: `art/units/stealthbomber/stealthbomber_md_run.kf`.
- hurt: `art/units/stealthbomber/stealthbomber_md_hurt.kf`.
- die: `art/units/stealthbomber/stealthbomber_md_die.kf`.

### military:stealth_destroyer

- Model: `src/Mods/Military/art/models/stealth_destroyer.glb` (214,300 bytes).
- Art: `ART_DEF_UNIT_STEALTH_DESTROYER`. Source scale: 0.6.
- NIF: `art/units/stealth_destroyer/stealth_destroyer.nif`.
- KFM: `art/units/stealth_destroyer/stealth_destroyer.kfm`.
- Geometry: 168 triangles, 1 skins, 8 joint bindings, 1 textures.
- Bounds: height 0.2749, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels), die (45 channels).
- SHA-256: `f22dfd331a5299d16bafbf0fb05109900cfc44784c39c89bee991f141e8aa00f`.
- idle: `art/units/stealth_destroyer/stealth_destroyer_md_idle.kf`.
- run: `art/units/stealth_destroyer/stealth_destroyer_md_run.kf`.
- fortify: `art/units/stealth_destroyer/stealth_destroyer_md_fortifya.kf`.
- attack: `art/units/stealth_destroyer/stealth_destroyer_md_strikea.kf`.
- hurt: `art/units/stealth_destroyer/stealth_destroyer_md_hurta.kf`.
- die: `art/units/stealth_destroyer/stealth_destroyer_md_diea.kf`.

### military:stealth_submarine

- Model: `src/Mods/Military/art/models/stealth_submarine.glb` (146,576 bytes).
- Art: `ART_DEF_UNIT_STEALTH_SUBMARINE`. Source scale: 0.40.
- NIF: `art/units/astute/astutesub.nif`.
- KFM: `art/units/astute/astutesub.kfm`.
- Geometry: 750 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.2505, radius 0.4800, feet Y=0.
- Clips: idle (16 channels), run (16 channels), fortify (16 channels), attack (16 channels), hurt (16 channels), die (16 channels).
- SHA-256: `831c7fb35c5deb28cf79a73411f144803412c5755411de5d5dfe836948de7793`.
- idle: `art/units/astute/astutesub_md_idle.kf`.
- run: `art/units/astute/astutesub_md_run.kf`.
- fortify: `art/units/astute/astutesub_md_rangedfortify.kf`.
- attack: `art/units/astute/astutesub_md_rangedstrike.kf`.
- hurt: `art/units/astute/astutesub_md_rangedhurta.kf`.
- die: `art/units/astute/astutesub_md_diea.kf`.

### military:steamer

- Model: `src/Mods/Military/art/models/steamer.glb` (386,428 bytes).
- Art: `ART_DEF_UNIT_STEAMER`. Source scale: 0.3.
- NIF: `art/units/paddlesteamer/paddlewheeler.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 2152 triangles, 1 skins, 10 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4761, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `2a90579facf8ec5bb5697fbadf4c752eb5f1d9cd82c0d729721bf1afcefe19f7`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.

### military:stiletto_boat

- Model: `src/Mods/Military/art/models/stiletto_boat.glb` (214,300 bytes).
- Art: `ART_DEF_UNIT_STILETTO_BOAT`. Source scale: 0.40.
- NIF: `art/units/stealth_destroyer/stealth_destroyer.nif`.
- KFM: `art/units/stealth_destroyer/stealth_destroyer.kfm`.
- Geometry: 168 triangles, 1 skins, 8 joint bindings, 1 textures.
- Bounds: height 0.2749, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels), die (45 channels).
- SHA-256: `f22dfd331a5299d16bafbf0fb05109900cfc44784c39c89bee991f141e8aa00f`.
- idle: `art/units/stealth_destroyer/stealth_destroyer_md_idle.kf`.
- run: `art/units/stealth_destroyer/stealth_destroyer_md_run.kf`.
- fortify: `art/units/stealth_destroyer/stealth_destroyer_md_fortifya.kf`.
- attack: `art/units/stealth_destroyer/stealth_destroyer_md_strikea.kf`.
- hurt: `art/units/stealth_destroyer/stealth_destroyer_md_hurta.kf`.
- die: `art/units/stealth_destroyer/stealth_destroyer_md_diea.kf`.

### military:submarine

- Model: `src/Mods/Military/art/models/submarine.glb` (267,892 bytes).
- Art: `ART_DEF_UNIT_SUBMARINE`. Source scale: 0.55.
- NIF: `art/units/submarine/submarine.nif`.
- KFM: `art/units/submarine/submarine.kfm`.
- Geometry: 424 triangles, 2 skins, 13 joint bindings, 2 textures.
- Bounds: height 0.3012, radius 0.4800, feet Y=0.
- Clips: idle (31 channels), run (31 channels), fortify (31 channels), attack (31 channels), hurt (31 channels), die (31 channels).
- SHA-256: `766eb5f4bcbb65cccf244cbf07aef6df582c6dcf0e9ccbb67ff877b706cd7c1f`.
- idle: `art/units/submarine/submarine_md_idle.kf`.
- run: `art/units/submarine/submarine_md_run.kf`.
- fortify: `art/units/submarine/submarine_md_fortifya.kf`.
- attack: `art/units/submarine/submarine_md_strikea.kf`.
- hurt: `art/units/submarine/submarine_md_hurta.kf`.
- die: `art/units/submarine/submarine_md_diea.kf`.
- Note: submarine:1: strongest four influences retained for 2 vertices

### military:sumerian_vulture

- Model: `src/Mods/Military/art/models/sumerian_vulture.glb` (347,188 bytes).
- Art: `ART_DEF_UNIT_SUMERIAN_VULTURE`. Source scale: 0.44.
- NIF: `art/units/unique_sumeria_vulture/unique_sumerian_vulture.nif`.
- KFM: `art/units/unique_sumeria_vulture/unique_sumerian_vulture.kfm`.
- Geometry: 994 triangles, 1 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3646, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `815d7b0eef47fe168118cbef644ecca3e32870099913210d17889b05e1b1ae61`.
- idle: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_idle.kf`.
- run: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_run.kf`.
- fortify: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_fortifya.kf`.
- attack: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_strikea.kf`.
- hurt: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_hurta.kf`.
- die: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_diea.kf`.

### military:swiss_hellebardier

- Model: `src/Mods/Military/art/models/swiss_hellebardier.glb` (315,544 bytes).
- Art: `ART_DEF_UNIT_SWISS_HELLEBARDIER`. Source scale: 0.44.
- NIF: `art/switzerland/swisspikeman/pikeman.nif`.
- KFM: `art/switzerland/swisspikeman/pikeman.kfm`.
- Geometry: 864 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5337, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `4a4f664e3026d9360c60e1135084461a59ea412a157b4487066abf2c7ef498e5`.
- idle: `art/switzerland/swisspikeman/pikeman_md_idle.kf`.
- run: `art/switzerland/swisspikeman/pikeman_md_run.kf`.
- fortify: `art/switzerland/swisspikeman/pikeman_md_fortifya.kf`.
- attack: `art/switzerland/swisspikeman/pikeman_md_strikea.kf`.
- hurt: `art/switzerland/swisspikeman/pikeman_md_hurta.kf`.
- die: `art/switzerland/swisspikeman/pikeman_md_diea.kf`.

### military:swordsman

- Model: `src/Mods/Military/art/models/swordsman.glb` (257,216 bytes).
- Art: `ART_DEF_UNIT_LIGHT_SWORDSMAN`. Source scale: 0.44.
- NIF: `art/units/charlemagne_lightswordsman/lightswordsman.nif`.
- KFM: `art/units/charlemagne_lightswordsman/lightswordsman.kfm`.
- Geometry: 655 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3102, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `b32730d51aa54d79537693d16e301c82235845b735a5cecf6bdaf3f663249a9e`.
- idle: `art/units/charlemagne_lightswordsman/lightswordsman_md_idle.kf`.
- run: `art/units/charlemagne_lightswordsman/lightswordsman_md_run.kf`.
- fortify: `art/units/charlemagne_lightswordsman/lightswordsman_md_fortifya.kf`.
- attack: `art/units/charlemagne_lightswordsman/lightswordsman_md_strikea.kf`.
- hurt: `art/units/charlemagne_lightswordsman/lightswordsman_md_hurta.kf`.
- die: `art/units/charlemagne_lightswordsman/lightswordsman_md_diea.kf`.

### military:t95

- Model: `src/Mods/Military/art/models/t95.glb` (355,480 bytes).
- Art: `ART_DEF_UNIT_T95`. Source scale: 0.4.
- NIF: `art/units/hybrid tank/t-95.nif`.
- KFM: `art/units/hybrid tank/tank.kfm`.
- Geometry: 2434 triangles, 1 skins, 5 joint bindings, 2 textures.
- Bounds: height 0.2082, radius 0.4800, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), attack (15 channels), hurt (15 channels).
- SHA-256: `e827def95307b33217b131202e26e6b7c8c15ddeb545e2887c060e424084d732`.
- idle: `art/units/hybrid tank/tank_md_idle.kf`.
- run: `art/units/hybrid tank/tank_md_run.kf`.
- fortify: `art/units/hybrid tank/tank_md_rangedfortify.kf`.
- attack: `art/units/hybrid tank/tank_md_rangedstrike.kf`.
- hurt: `art/units/hybrid tank/tank_md_hurta.kf`.

### military:tactical_nuke

- Model: `src/Mods/Military/art/models/tactical_nuke.glb` (91,624 bytes).
- Art: `ART_DEF_UNIT_TACTICAL_NUKE`. Source scale: 0.5.
- NIF: `art/units/tactical_nuke/tactical_nuke.nif`.
- KFM: `art/units/tactical_nuke/tactical_nuke.kfm`.
- Geometry: 154 triangles, 1 skins, 3 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.1203, feet Y=0.
- Clips: idle (20 channels), run (20 channels), attack (20 channels).
- SHA-256: `5aed27142f6a940451e0d3cfb6008b0334f8260368b9addc7c297e597e2ca89b`.
- idle: `art/units/tactical_nuke/tactical_nuke_md_idle.kf`.
- run: `art/units/tactical_nuke/tactical_nuke_md_run.kf`.
- attack: `art/units/tactical_nuke/tactical_nuke_md_strikea.kf`.

### military:tank

- Model: `src/Mods/Military/art/models/tank.glb` (265,528 bytes).
- Art: `ART_DEF_UNIT_TANK`. Source scale: 0.48.
- NIF: `art/units/tank/tank.nif`.
- KFM: `art/units/tank/tank.kfm`.
- Geometry: 369 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5171, radius 0.4800, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels).
- SHA-256: `9377c80a373d67c743d11c91845f9a8713f812d745b20c899180447f59d11b81`.
- idle: `art/units/tank/tank_md_idle.kf`.
- run: `art/units/tank/tank_md_run.kf`.
- fortify: `art/units/tank/tank_md_rangedfortify.kf`.
- attack: `art/units/tank/tank_md_rangedstrike.kf`.
- hurt: `art/units/tank/tank_md_hurta.kf`.

### military:taoist_missionary

- Model: `src/Mods/Military/art/models/taoist_missionary.glb` (215,220 bytes).
- Art: `ART_DEF_UNIT_TAOIST_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/taoist/taoistmissionary.nif`.
- KFM: `art/units/missionary/taoist/taoistmissionary.kfm`.
- Geometry: 600 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2519, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), die (53 channels).
- SHA-256: `100cb8d5d7f4d8a0fdf2653f91bd61384fb3110af02c38f05c52b0fc268b499e`.
- idle: `art/units/missionary/taoist/taoistmissionary_md_idle.kf`.
- run: `art/units/missionary/taoist/taoistmissionary_md_run.kf`.
- fortify: `art/units/missionary/taoist/taoistmissionary_md_fortify.kf`.
- die: `art/units/missionary/taoist/taoistmissionary_md_idle_die.kf`.

### military:trade_caravan

- Model: `src/Mods/Military/art/models/trade_caravan.glb` (825,016 bytes).
- Art: `ART_DEF_UNIT_TRADE_CARAVAN`. Source scale: 0.34.
- NIF: `art/units/caravan/caravan_art.nif`.
- KFM: `art/units/caravan/caravan_base.kfm`.
- Geometry: 2320 triangles, 4 skins, 74 joint bindings, 5 textures.
- Bounds: height 0.3183, radius 0.4800, feet Y=0.
- Clips: idle (239 channels), run (239 channels).
- SHA-256: `04351172018b9dd8739fb1ee908d1b5a70c89da444cb6da1516e6a41c6fef541`.
- idle: `art/units/caravan/caravan_base_md_idle.kf`.
- run: `art/units/caravan/caravan_base_md_run.kf`.

### military:transport

- Model: `src/Mods/Military/art/models/transport.glb` (311,784 bytes).
- Art: `ART_DEF_UNIT_TRANSPORT`. Source scale: 0.92.
- NIF: `art/units/transport/transport.nif`.
- KFM: `art/units/transport/transport.kfm`.
- Geometry: 490 triangles, 2 skins, 12 joint bindings, 1 textures.
- Bounds: height 0.2713, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `3be4bdfbfca3bfd1ef99aa03b1f50347d87273594f9c05ab68e3a0119428c481`.
- idle: `art/units/transport/transport_md_idle.kf`.
- run: `art/units/transport/transport_md_run.kf`.
- fortify: `art/units/transport/transport_md_fortifya.kf`.
- attack: `art/units/transport/transport_md_strikea.kf`.
- hurt: `art/units/transport/transport_md_hurta.kf`.
- die: `art/units/transport/transport_md_diea.kf`.

### military:treasure

- Model: `src/Mods/Military/art/models/treasure.glb` (204,232 bytes).
- Art: `ART_DEF_UNIT_TREASURE_UNIT`. Source scale: 0.44.
- NIF: `art/units/treasure/treasure.nif`.
- KFM: `art/units/treasure/treasure.kfm`.
- Geometry: 946 triangles, 2 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.5602, radius 0.4800, feet Y=0.
- Clips: idle (55 channels), run (55 channels).
- SHA-256: `2836ce4e21c07cf43285aeecd8be8cd47766c9f1491eb1c4b0e276240220c49f`.
- idle: `art/units/treasure/treasure_md_idle.kf`.
- run: `art/units/treasure/treasure_md_run.kf`.

### military:trebuchet

- Model: `src/Mods/Military/art/models/trebuchet.glb` (352,196 bytes).
- Art: `ART_DEF_UNIT_TREBUCHET`. Source scale: 0.40.
- NIF: `art/units/trebuchet/trebuchet.nif`.
- KFM: `art/units/trebuchet/trebuchet.kfm`.
- Geometry: 680 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4217, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `a60a1c6611926ad238ea83fdf811f5aab494c099170215de6bf3d06574c8d16f`.
- idle: `art/units/trebuchet/trebuchet_md_idle.kf`.
- run: `art/units/trebuchet/trebuchet_md_run.kf`.
- fortify: `art/units/trebuchet/trebuchet_md_fortifya.kf`.
- attack: `art/units/trebuchet/trebuchet_md_rangedstrike.kf`.
- hurt: `art/units/trebuchet/trebuchet_md_hurta.kf`.
- die: `art/units/trebuchet/trebuchet_md_diea.kf`.

### military:trireme

- Model: `src/Mods/Military/art/models/trireme.glb` (413,624 bytes).
- Art: `ART_DEF_UNIT_TRIREME`. Source scale: 0.14.
- NIF: `art/units/trireme/trireme.nif`.
- KFM: `art/units/trireme/trireme.kfm`.
- Geometry: 1144 triangles, 2 skins, 20 joint bindings, 2 textures.
- Bounds: height 0.4466, radius 0.4800, feet Y=0.
- Clips: idle (65 channels), run (65 channels), fortify (65 channels), attack (65 channels), hurt (65 channels), die (65 channels).
- SHA-256: `0f2a0eaff4475534e84530f57c1780b455fe033d3a0e0d56b70c38b80606527e`.
- idle: `art/units/trireme/trireme_md_idle.kf`.
- run: `art/units/trireme/trireme_md_run.kf`.
- fortify: `art/units/trireme/trireme_md_fortifya.kf`.
- attack: `art/units/trireme/trireme_md_strikea.kf`.
- hurt: `art/units/trireme/trireme_md_hurta.kf`.
- die: `art/units/trireme/trireme_md_diea.kf`.

### military:turn

- Model: `src/Mods/Military/art/models/turn.glb` (135,792 bytes).
- Art: `ART_DEF_UNIT_TURN`. Source scale: 0.85.
- NIF: `art/units/nukes/yam/icbm.nif`.
- KFM: `art/units/nukes/yam/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (21 channels), run (21 channels), attack (21 channels).
- SHA-256: `9392cd98f3240c9d0f631dcc7c75cb4761dc4c6119fdee96b8b6d925518b9944`.
- idle: `art/units/nukes/yam/icbm_md_idle.kf`.
- run: `art/units/nukes/yam/icbm_md_run.kf`.
- attack: `art/units/nukes/yam/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:uav

- Model: `src/Mods/Military/art/models/uav.glb` (223,852 bytes).
- Art: `ART_DEF_UNIT_UNMANNED_AIR_VEHICLE`. Source scale: 0.2.
- NIF: `art/units/drone bomber/predator.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 600 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.2053, radius 0.4800, feet Y=0.
- Clips: idle (37 channels), run (37 channels), hurt (37 channels), die (37 channels).
- SHA-256: `3800589ace6f000057c210d5f8445410df94c638c0e2d6df7f8ec696f536d40f`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.
- Note: Hidden geometry omitted: FighterDeathModel:1

### military:unit_unit_crusader

- Model: `src/Mods/Military/art/models/unit_unit_crusader.glb` (296,864 bytes).
- Art: `ART_DEF_UNIT_KNIGHTS_TEMPLAR`. Source scale: 0.44.
- NIF: `art/units/scenario/knigts_templar/knightstemplar.nif`.
- KFM: `art/units/scenario/knigts_templar/knightstemplar.kfm`.
- Geometry: 556 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2891, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `679bfbb2cfaba29f7eb1f17d385aa27e5785e8cbe8a5977e99df01dbf427c37b`.
- idle: `art/units/scenario/knigts_templar/knightstemplar_md_idle.kf`.
- run: `art/units/scenario/knigts_templar/knightstemplar_md_run.kf`.
- fortify: `art/units/scenario/knigts_templar/knightstemplar_md_fortifya.kf`.
- attack: `art/units/scenario/knigts_templar/knightstemplar_md_strikea.kf`.
- hurt: `art/units/scenario/knigts_templar/knightstemplar_md_hurta.kf`.
- die: `art/units/scenario/knigts_templar/knightstemplar_md_diea.kf`.
- Note: First of 4 formation art groups exported

### military:v1

- Model: `src/Mods/Military/art/models/v1.glb` (208,492 bytes).
- Art: `ART_DEF_UNIT_V1`. Source scale: 0.7.
- NIF: `art/units/missile/v1.nif`.
- KFM: `art/units/guided_missile/guided_missile.kfm`.
- Geometry: 428 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.1704, radius 0.4800, feet Y=0.
- Clips: none.
- SHA-256: `e4f61e890c93064d2e9bb7d26694c770aff309202ea6161e012dba9114a5a21b`.
- Note: idle: no matching transform tracks
- Note: run: no matching transform tracks
- Note: attack: no matching transform tracks

### military:vandal_raider

- Model: `src/Mods/Military/art/models/vandal_raider.glb` (292,448 bytes).
- Art: `ART_DEF_UNIT_VANDAL_RAIDER`. Source scale: 0.44.
- NIF: `art/units/raider/swordsman.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 710 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3392, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels), attack (56 channels), hurt (56 channels), die (56 channels).
- SHA-256: `e8af6c58770b8703244eddbd356aa669242a3114a2b1300b948b76f590f1098d`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:viking_beserker

- Model: `src/Mods/Military/art/models/viking_beserker.glb` (449,188 bytes).
- Art: `ART_DEF_UNIT_VIKING_BESERKER`. Source scale: 0.44.
- NIF: `art/units/viking_berserker/berserker.nif`.
- KFM: `art/units/viking_berserker/berserker.kfm`.
- Geometry: 860 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3473, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `6eab1f0c1862ff111912106c39c81ddd3329273467691e4e34ba03ccfe40fba1`.
- idle: `art/units/viking_berserker/berserker_md_idle.kf`.
- run: `art/units/viking_berserker/berserker_md_run.kf`.
- fortify: `art/units/viking_berserker/berserker_md_fortifya.kf`.
- attack: `art/units/viking_berserker/berserker_md_strikea.kf`.
- hurt: `art/units/viking_berserker/berserker_md_hurta.kf`.
- die: `art/units/viking_berserker/berserker_md_diea.kf`.

### military:walker

- Model: `src/Mods/Military/art/models/walker.glb` (258,232 bytes).
- Art: `ART_DEF_UNIT_WALKER`. Source scale: 0.8.
- NIF: `art/units/walker droid/mech.nif`.
- KFM: `art/units/walker droid/mech.kfm`.
- Geometry: 1128 triangles, 2 skins, 10 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3368, feet Y=0.
- Clips: idle (30 channels), run (30 channels), fortify (30 channels), attack (30 channels), hurt (30 channels).
- SHA-256: `9d7e9c7db8d83570d234aad497ab97a5fcc46c2a69466d6d44eecba5f8829c54`.
- idle: `art/units/walker droid/mech_md_idle.kf`.
- run: `art/units/walker droid/mech_md_run.kf`.
- fortify: `art/units/walker droid/mech_md_rangedfortify.kf`.
- attack: `art/units/walker droid/mech_md_rangedstrike.kf`.
- hurt: `art/units/walker droid/mech_md_rangedhurta.kf`.

### military:war_elephant

- Model: `src/Mods/Military/art/models/war_elephant.glb` (760,276 bytes).
- Art: `ART_DEF_UNIT_WAR_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/warelephant/warelephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 1454 triangles, 3 skins, 47 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2463, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `1b9942091024308cb0173272a59cbf3b6c8ca9bb75a44fceee7d634a87f36200`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.

### military:wargalley

- Model: `src/Mods/Military/art/models/wargalley.glb` (260,664 bytes).
- Art: `ART_DEF_UNIT_WARGALLEY`. Source scale: 0.16.
- NIF: `art/units/war galley/wargalley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 1102 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3745, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b8c75d6f68c945cfb0b28902b03c87694565db1f3196ddb65e3cbfd83eaf5600`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:warrior

- Model: `src/Mods/Military/art/models/warrior.glb` (304,108 bytes).
- Art: `ART_DEF_UNIT_WARRIOR`. Source scale: 0.44.
- NIF: `art/units/warrior/warrior.nif`.
- KFM: `art/units/warrior/warrior.kfm`.
- Geometry: 598 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3410, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `8fcdeba040e83ff01e5f0f35d4fd3ad9f3cf9d92d7fd0656f2844c99bffac20f`.
- idle: `art/units/warrior/warrior_md_idle.kf`.
- run: `art/units/warrior/warrior_md_run.kf`.
- fortify: `art/units/warrior/warrior_md_fortifya.kf`.
- attack: `art/units/warrior/warrior_md_strikea.kf`.
- hurt: `art/units/warrior/warrior_md_hurta.kf`.
- die: `art/units/warrior/warrior_md_diea.kf`.

### military:welsh_longbowman

- Model: `src/Mods/Military/art/models/welsh_longbowman.glb` (394,872 bytes).
- Art: `ART_DEF_UNIT_WELSH_LONGBOWMAN`. Source scale: 0.44.
- NIF: `art/wales/welsh longbowman/longbowman.nif`.
- KFM: `art/wales/welsh longbowman/longbowman.kfm`.
- Geometry: 652 triangles, 2 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2400, feet Y=0.
- Clips: idle (66 channels), run (66 channels), fortify (66 channels), attack (66 channels), hurt (66 channels), die (66 channels).
- SHA-256: `3bdd3aba2c3fbaefa647cca1496dc640cb64c2352fec35dc36757e7731485f54`.
- idle: `art/wales/welsh longbowman/longbowman_md_idle.kf`.
- run: `art/wales/welsh longbowman/longbowman_md_run.kf`.
- fortify: `art/wales/welsh longbowman/longbowman_md_fortifya.kf`.
- attack: `art/wales/welsh longbowman/longbowman_md_strikea.kf`.
- hurt: `art/wales/welsh longbowman/longbowman_md_hurta.kf`.
- die: `art/wales/welsh longbowman/longbowman_md_diea.kf`.

### military:wgmaster

- Model: `src/Mods/Military/art/models/wgmaster.glb` (230,316 bytes).
- Art: `ART_DEF_UNIT_WGMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `09602758d481bc1a7781c1e5675dba61ed83db9517514a27edca66015e6189ab`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:wild_ram

- Model: `src/Mods/Military/art/models/wild_ram.glb` (446,792 bytes).
- Art: `ART_DEF_UNIT_WILD_RAM`. Source scale: 18.0.
- NIF: `art/units/wild_ram/mouflon.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 794 triangles, 2 skins, 33 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3082, feet Y=0.
- Clips: none.
- SHA-256: `fd8888fbe5e7f5d9dead6ebee25b320d9148c8653241c6333cb93720cc877c2c`.
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD2:0
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD2:1
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD1:0
- Note: Hidden geometry omitted: Ibex_Adult_M_LOD1:1
- Note: idle: no matching transform tracks
- Note: run: no matching transform tracks
- Note: fortify: no matching transform tracks
- Note: attack: no matching transform tracks
- Note: hurt: no matching transform tracks
- Note: die: no matching transform tracks

### military:wolf

- Model: `src/Mods/Military/art/models/wolf.glb` (346,728 bytes).
- Art: `ART_DEF_UNIT_WOLF`. Source scale: 0.5.
- NIF: `art/units/wolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5082, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `da51c49288d36e2b9270449fdc32be93286fb8f629417ef6c2132bcc94b8032f`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:workboat

- Model: `src/Mods/Military/art/models/workboat.glb` (212,332 bytes).
- Art: `ART_DEF_UNIT_WORKBOAT`. Source scale: 0.19.
- NIF: `art/units/workboat_ancient/workboat_ancient.nif`.
- KFM: `art/units/workboat_ancient/workboat_ancient.kfm`.
- Geometry: 417 triangles, 3 skins, 10 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3534, feet Y=0.
- Clips: idle (17 channels), run (17 channels), fortify (17 channels), die (17 channels).
- SHA-256: `17df00ffcc1b1e902f40a6cbe1c3f36411d11da362b9bc42865090a0173eb06c`.
- idle: `art/units/workboat_ancient/workboat_ancient_md_idle.kf`.
- run: `art/units/workboat_ancient/workboat_ancient_md_run.kf`.
- fortify: `art/units/workboat_ancient/workboat_ancient_md_fortifya.kf`.
- die: `art/units/workboat_ancient/workboat_ancient_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:worker

- Model: `src/Mods/Military/art/models/worker.glb` (256,308 bytes).
- Art: `ART_DEF_UNIT_WORKER`. Source scale: 0.44.
- NIF: `art/units/worker/worker.nif`.
- KFM: `art/units/worker/worker.kfm`.
- Geometry: 884 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2917, feet Y=0.
- Clips: idle (64 channels), run (64 channels), fortify (64 channels).
- SHA-256: `904a2c8113c240ebf0b6d61e0d33bef660473562bcba989fc46a07cd81013e96`.
- idle: `art/units/worker/worker_md_idle.kf`.
- run: `art/units/worker/worker_md_run.kf`.
- fortify: `art/units/worker/worker_md_fortify.kf`.

### military:worker_android

- Model: `src/Mods/Military/art/models/worker_android.glb` (254,636 bytes).
- Art: `ART_DEF_UNIT_WORKER_ANDROID`. Source scale: 0.44.
- NIF: `art/units/android worker/modernworker.nif`.
- KFM: `art/units/android worker/modernworker.kfm`.
- Geometry: 854 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2977, feet Y=0.
- Clips: idle (63 channels), run (63 channels), fortify (63 channels).
- SHA-256: `f65085b9d1cb74475bde7a0217063e814c192c7d03b4ba175a5dbdf4b12e55a8`.
- idle: `art/units/android worker/modernworker_md_idle.kf`.
- run: `art/units/android worker/modernworker_md_run.kf`.
- fortify: `art/units/android worker/modernworker_md_fortify.kf`.
- Note: Missing texture: Worker_tools.dds

### military:ww1_destroyer

- Model: `src/Mods/Military/art/models/ww1_destroyer.glb` (308,488 bytes).
- Art: `ART_DEF_UNIT_WW1_DESTROYER`. Source scale: 0.6.
- NIF: `art/units/destroyer/american/4stacker.nif`.
- KFM: `art/units/destroyer/american/4stacker.kfm`.
- Geometry: 650 triangles, 2 skins, 5 joint bindings, 2 textures.
- Bounds: height 0.4628, radius 0.4800, feet Y=0.
- Clips: idle (21 channels), run (21 channels), fortify (21 channels), attack (21 channels), hurt (21 channels).
- SHA-256: `e36bb5bcfded1297e809704b7711f2606cd9a40b8061452bf82b126e330e38a8`.
- idle: `art/units/destroyer/american/4stacker_md_idle.kf`.
- run: `art/units/destroyer/american/4stacker_md_run.kf`.
- fortify: `art/units/destroyer/american/4stacker_md_rangedfortify.kf`.
- attack: `art/units/destroyer/american/4stacker_md_rangedstrike.kf`.
- hurt: `art/units/destroyer/american/4stacker_md_hurta.kf`.

### military:ww2_battleship

- Model: `src/Mods/Military/art/models/ww2_battleship.glb` (460,240 bytes).
- Art: `ART_DEF_UNIT_WW2_BATTLESHIP`. Source scale: 0.7.
- NIF: `art/units/battleship/european/battleship.nif`.
- KFM: `art/units/battleship/european/battleship.kfm`.
- Geometry: 2177 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.2600, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `b62d22771eadad19ea1ab951eedad219052aa6fbc9098f725cd0f495d3e652bd`.
- idle: `art/units/battleship/european/battleship_md_idle.kf`.
- run: `art/units/battleship/european/battleship_md_run.kf`.
- fortify: `art/units/battleship/european/battleship_md_fortifya.kf`.
- attack: `art/units/battleship/european/battleship_md_strikea.kf`.
- hurt: `art/units/battleship/european/battleship_md_hurta.kf`.
- die: `art/units/battleship/european/battleship_md_diea.kf`.

### military:zoroastrian_missionary

- Model: `src/Mods/Military/art/models/zoroastrian_missionary.glb` (186,968 bytes).
- Art: `ART_DEF_UNIT_ZORO_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/magi/confucian_missionary.nif`.
- KFM: `art/units/missionary/magi/confucian_missionary.kfm`.
- Geometry: 480 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2507, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), die (50 channels).
- SHA-256: `39acd95c6da49d95c5ea9418558ec796d529d8f7445502bd82bfa831f5e8655f`.
- idle: `art/units/missionary/magi/confucian_missionary_md_idle.kf`.
- run: `art/units/missionary/magi/confucian_missionary_md_run.kf`.
- fortify: `art/units/missionary/magi/confucian_missionary_md_fortifya.kf`.
- die: `art/units/missionary/magi/confucian_missionary_md_idle_die.kf`.

### military:zulu_impi

- Model: `src/Mods/Military/art/models/zulu_impi.glb` (371,684 bytes).
- Art: `ART_DEF_UNIT_ZULU_SPEARMAN`. Source scale: 0.44.
- NIF: `art/units/zulu_impi/zulu_impi.nif`.
- KFM: `art/units/zulu_impi/zulu_impi.kfm`.
- Geometry: 1105 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3008, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels), die (58 channels).
- SHA-256: `f5a3e00b73be3ca0a2e4869f68885e1142b09f7425b7cadb3c4046a32604a790`.
- idle: `art/units/zulu_impi/zulu_impi_md_idle.kf`.
- run: `art/units/zulu_impi/zulu_impi_md_run.kf`.
- fortify: `art/units/zulu_impi/zulu_impi_md_fortifya.kf`.
- attack: `art/units/zulu_impi/zulu_impi_md_strikea.kf`.
- hurt: `art/units/zulu_impi/zulu_impi_md_hurta.kf`.
- die: `art/units/zulu_impi/zulu_impi_md_diea.kf`.
