# Unit art conversion report

## Run

- Registry: `src/Mods/Military/data/unit.json` (359 units in this run).
- Source: `/home/gordon/workspace/civ/AND-extraction`.
- Converted: **357**. Failed: **2**.
- Animated: **351**. Skinned: **333**. Textured: **355**.
- Output: `src/Mods/Military/art/models/<unit-id-suffix>.glb`.
- Total GLB size: 136,175,636 bytes.
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
Material, visibility, particle, sound, and effect-controller animations do not become glTF transform tracks.
Hidden geometry does not export. Runtime projectiles and source shaders need separate renderer support.
Team-color decal textures composite over a neutral blue-gray background. This conversion does not implement dynamic team colors.
Zero-scale nodes use a scale of 0.000001 because Godot cannot decompose singular bases.
The report lists missing assets and unsupported bindings. Failed units do not receive substitute models.
A source clip without matching transform targets does not count as an exported animation.

## Validation of this batch

- Khronos glTF Validator checked all 357 GLBs: **0 errors**, 594 warnings.
- All warnings are `NODE_SKINNED_MESH_NON_ROOT`. Mesh nodes share the coordinate-normalization root with their skeletons.
- Godot 4.7.2 loaded all 357 files through `GLTFDocument.append_from_file` and `generate_scene`.
- Godot reported **0 failed imports** and no engine errors.
- Godot advanced the spike animations from time zero to each clip midpoint. Skeleton bone poses changed in every check.
- The spike models each contain all six required clip names and embedded textures.
- The JSON chunks contain no external URIs or source-branding names.
- This validation checks file structure, scene generation, and animated bone poses. It does not replace a visual review.

Changed bones in the runtime animation checks:

| Model | idle | attack | die |
|---|---:|---:|---:|
| warrior | 15 | 19 | 19 |
| archer | 15 | 18 | 18 |
| axeman | 14 | 18 | 19 |

Known source failures:

- `military:ballista`: the NIF contains a node with multiple parents. This converter requires a tree hierarchy.
- `military:battlecruiser`: a source UV accessor is empty or contains non-finite values.
- `military:bull` and `military:captured_bull`: the referenced `bizon.dds` texture is absent or ambiguous.
  These two files import without textures. Their art definition also selects cavalry animations with few matching transform targets.
- Some source effects and attachment textures remain unresolved. The per-unit notes identify these assets.

### Complexity report

Radon measured these functions before and after helper extraction:

| Function | Before | After |
|---|---:|---:|
| add_hierarchy | 16 | 9 |
| convert_animation | 13 | 7 |
| profile_files | 15 | 10 |
| convert_unit | 16 | 13 |
| write_report | 18 | 12 |

The full conversion, Khronos validation, and Godot import checks passed after these changes, except for the documented source failures.

## Per-unit registry

### military:007

- Model: `src/Mods/Military/art/models/007.glb` (478,716 bytes).
- Art: `ART_DEF_UNIT_007`. Source scale: 0.46.
- NIF: `art/units/007/007.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 739 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4055, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels).
- SHA-256: `f5a35aba5994e8059baeebba4eea498ca96e598adf30363af81f3c9a059d376f`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:acv

- Model: `src/Mods/Military/art/models/acv.glb` (217,312 bytes).
- Art: `ART_DEF_UNIT_ACV`. Source scale: 0.43.
- NIF: `art/units/acv/acv.nif`.
- KFM: `art/units/mechanizedinfantry/mechanizedinfantry.kfm`.
- Geometry: 553 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.5493, radius 0.4800, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels).
- SHA-256: `5537b7b14b07cc68ffc658a893fce1567ecd3774588c00bb159961cbec32614c`.
- idle: `art/units/mechanizedinfantry/mechanizedinfantry_md_idle.kf`.
- run: `art/units/mechanizedinfantry/mechanizedinfantry_md_run.kf`.
- fortify: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedfortify.kf`.
- attack: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedstrike.kf`.
- hurt: `art/units/mechanizedinfantry/mechanizedinfantry_md_hurta.kf`.

### military:acvsam

- Model: `src/Mods/Military/art/models/acvsam.glb` (287,976 bytes).
- Art: `ART_DEF_UNIT_ACVSAM`. Source scale: 0.48.
- NIF: `art/units/acv sam/acv_sam.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 614 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.5339, radius 0.4800, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels).
- SHA-256: `42abf35828d4826555c0e81cb9bcbedb7dabda2b6ebc38c11fadb7ca803ec2dd`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:adventurer

- Model: `src/Mods/Military/art/models/adventurer.glb` (612,636 bytes).
- Art: `ART_DEF_UNIT_ADVENTURER`. Source scale: 0.42.
- NIF: `art/units/adventurer/persianimmortal.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 1150 triangles, 3 skins, 38 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2843, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `8e4d0dd505966a8414f80a30ccf88ed32b32a443601c85a3328394e168997ff9`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:agent

- Model: `src/Mods/Military/art/models/agent.glb` (751,832 bytes).
- Art: `ART_DEF_UNIT_AGENT`. Source scale: 0.46.
- NIF: `art/units/agent/agent.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 823 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3957, feet Y=0.
- Clips: idle (107 channels), run (107 channels), fortify (107 channels), attack (107 channels).
- SHA-256: `6116037dd7a938aeffe87fa4b74c0aca16f9ec0e474a32ec7e8718cb0d2f7e23`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:ah64_gunship

- Model: `src/Mods/Military/art/models/ah64_gunship.glb` (374,160 bytes).
- Art: `ART_DEF_UNIT_AH64_GUNSHIP`. Source scale: 0.36.
- NIF: `art/units/gunship/ah64/ah64.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 2109 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.2539, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), fortify (28 channels), attack (28 channels), hurt (28 channels).
- SHA-256: `50c769abe6328d04cc21293e7a91168e8d722ab6a7998f0cbce85e0d6f87848d`.
- idle: `art/units/gunship/gunship_md_idle.kf`.
- run: `art/units/gunship/gunship_md_run.kf`.
- fortify: `art/units/gunship/gunship_md_rangedfortify.kf`.
- attack: `art/units/gunship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/gunship/gunship_md_hurta.kf`.

### military:airship

- Model: `src/Mods/Military/art/models/airship.glb` (224,768 bytes).
- Art: `ART_DEF_UNIT_AIRSHIP`. Source scale: 0.3.
- NIF: `art/units/airship/airship.nif`.
- KFM: `art/units/airship/airship.kfm`.
- Geometry: 904 triangles, 3 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.1889, radius 0.4800, feet Y=0.
- Clips: idle (39 channels), run (39 channels), hurt (39 channels), die (39 channels).
- SHA-256: `6d17c20055ca4876b5d05d9b63adf5729dc3ddbdede4910d18199270d23a3223`.
- idle: `art/units/airship/airship_md_idle.kf`.
- run: `art/units/airship/airship_md_run.kf`.
- hurt: `art/units/airship/airship_md_hurt.kf`.
- die: `art/units/airship/airship_md_die.kf`.

### military:albanian_light_guerilla_cavalry

- Model: `src/Mods/Military/art/models/albanian_light_guerilla_cavalry.glb` (602,324 bytes).
- Art: `ART_DEF_UNIT_KALORSE_KNIGHT`. Source scale: 0.42.
- NIF: `art/albania/kalorse/knight/knight.nif`.
- KFM: `art/albania/kalorse/knight/knight.kfm`.
- Geometry: 1143 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3002, feet Y=0.
- Clips: idle (109 channels), run (109 channels), fortify (109 channels), attack (109 channels), hurt (109 channels), die (109 channels).
- SHA-256: `cb9a8842f71a583c3d7a03c7d858030317f6890c225c801b68e49d672698f513`.
- idle: `art/albania/kalorse/knight/knight_md_idle.kf`.
- run: `art/albania/kalorse/knight/knight_md_run.kf`.
- fortify: `art/albania/kalorse/knight/knight_md_fortifya.kf`.
- attack: `art/albania/kalorse/knight/knight_md_strikea.kf`.
- hurt: `art/albania/kalorse/knight/knight_md_hurta.kf`.
- die: `art/albania/kalorse/knight/knight_md_diea.kf`.
- Note: First of 3 formation art groups exported

### military:ambassador

- Model: `src/Mods/Military/art/models/ambassador.glb` (709,140 bytes).
- Art: `ART_DEF_UNIT_AMBASSADOR`. Source scale: 0.46.
- NIF: `art/units/ambassador/great_spy_modern_fx.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 590 triangles, 1 skins, 15 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2993, feet Y=0.
- Clips: idle (104 channels), run (104 channels), fortify (104 channels), attack (104 channels).
- SHA-256: `68ebb59bfbb0b25ef3a4ad70bddd7844ab03ec7f830cbc9aab786fbcd3d6ddd2`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:american_navy_seal

- Model: `src/Mods/Military/art/models/american_navy_seal.glb` (340,836 bytes).
- Art: `ART_DEF_UNIT_AMERICAN_NAVY_SEAL`. Source scale: 0.44.
- NIF: `art/units/unique/american navy seal/marines.nif`.
- KFM: `art/units/unique/american navy seal/marines.kfm`.
- Geometry: 798 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3356, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `edf2fc9a54f0aa6216c6a30e9cfe1f5abbf0e34b9b765452827fbc5d283d8357`.
- idle: `art/units/unique/american navy seal/marines_md_idle.kf`.
- run: `art/units/unique/american navy seal/marines_md_run.kf`.
- fortify: `art/units/unique/american navy seal/marines_md_rangedfortify.kf`.
- attack: `art/units/unique/american navy seal/marines_md_rangedstrike.kf`.
- hurt: `art/units/unique/american navy seal/marines_md_hurta.kf`.
- die: `art/units/unique/american navy seal/marines_md_rangeddiea.kf`.

### military:anglo_saxon_huscarl

- Model: `src/Mods/Military/art/models/anglo_saxon_huscarl.glb` (500,852 bytes).
- Art: `ART_DEF_UNIT_ANGLO_SAXON_HUSCARL`. Source scale: 0.44.
- NIF: `art/anglosaxons/huskarl/huscarl.nif`.
- KFM: `art/anglosaxons/huskarl/huscarl.kfm`.
- Geometry: 689 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3407, feet Y=0.
- Clips: idle (144 channels), run (144 channels), fortify (144 channels), attack (144 channels), hurt (144 channels), die (144 channels).
- SHA-256: `215cf6bdd432045534bed9c6f6e49cac9efff6059b53128102cea13eb08f39ef`.
- idle: `art/anglosaxons/huskarl/huscarl_md_idle.kf`.
- run: `art/anglosaxons/huskarl/huscarl_md_run.kf`.
- fortify: `art/anglosaxons/huskarl/huscarl_md_fortifya.kf`.
- attack: `art/anglosaxons/huskarl/huscarl_md_strikea.kf`.
- hurt: `art/anglosaxons/huskarl/huscarl_md_hurta.kf`.
- die: `art/anglosaxons/huskarl/huscarl_md_diea.kf`.

### military:arabia_camelarcher

- Model: `src/Mods/Military/art/models/arabia_camelarcher.glb` (669,832 bytes).
- Art: `ART_DEF_UNIT_ARABIA_CAMELARCHER`. Source scale: 0.39.
- NIF: `art/units/unique/arabia/camelarcher/arabiacamelarcher.nif`.
- KFM: `art/units/unique/arabia/camelarcher/arabiacamelarcher.kfm`.
- Geometry: 1250 triangles, 4 skins, 42 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2183, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `b367b1b8c29ce20a6e2739016a2a6622d3ee65584dfb9055a801de002b5f1f39`.
- idle: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_idle.kf`.
- run: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_run.kf`.
- fortify: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_fortifya.kf`.
- attack: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_strikea.kf`.
- hurt: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_hurta.kf`.
- die: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:archer

- Model: `src/Mods/Military/art/models/archer.glb` (341,692 bytes).
- Art: `ART_DEF_UNIT_ARCHER`. Source scale: 0.44.
- NIF: `art/units/archer/archer.nif`.
- KFM: `art/units/archer/archer.kfm`.
- Geometry: 756 triangles, 3 skins, 20 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2956, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `456a19a9293e3c52b0130b942219f39fe391b9007727c42e363d753334d3bda0`.
- idle: `art/units/archer/archer_md_idle.kf`.
- run: `art/units/archer/archer_md_run.kf`.
- fortify: `art/units/archer/archer_md_fortifya.kf`.
- attack: `art/units/archer/archer_md_strikea.kf`.
- hurt: `art/units/archer/archer_md_hurta.kf`.
- die: `art/units/archer/archer_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:armor_m60

- Model: `src/Mods/Military/art/models/armor_m60.glb` (217,484 bytes).
- Art: `ART_DEF_UNIT_ARMOR_M60`. Source scale: 0.4.
- NIF: `art/units/main battle tank/generic/m60.nif`.
- KFM: `art/units/main battle tank/generic/m60.kfm`.
- Geometry: 956 triangles, 11 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.2922, radius 0.4800, feet Y=0.
- Clips: idle (12 channels), run (12 channels), fortify (12 channels), attack (12 channels), hurt (12 channels).
- SHA-256: `e3053340cb560a88548fb5a58c6e192b296bc3361fd7536b6f17989c0ae465c6`.
- idle: `art/units/main battle tank/generic/m60_md_idle.kf`.
- run: `art/units/main battle tank/generic/m60_md_run.kf`.
- fortify: `art/units/main battle tank/generic/m60_md_rangedfortify.kf`.
- attack: `art/units/main battle tank/generic/m60_md_rangedstrike.kf`.
- hurt: `art/units/main battle tank/generic/m60_md_hurta.kf`.

### military:armored_car

- Model: `src/Mods/Military/art/models/armored_car.glb` (163,188 bytes).
- Art: `ART_DEF_UNIT_ARMORED_CAR`. Source scale: 0.40.
- NIF: `art/units/armored car/rollsroyce.nif`.
- KFM: `art/units/armored car/rollsroyce.kfm`.
- Geometry: 852 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.3519, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `efcc3b8bf5c9ebd57846e63e5ed416973e6014c8a9eb3fdf30d8120afb374ae5`.
- idle: `art/units/armored car/rollsroyce_md_idle.kf`.
- run: `art/units/armored car/rollsroyce_md_run.kf`.
- fortify: `art/units/armored car/rollsroyce_md_rangedfortify.kf`.
- attack: `art/units/armored car/rollsroyce_md_rangedstrike.kf`.
- hurt: `art/units/armored car/rollsroyce_md_hurta.kf`.

### military:arquebusier

- Model: `src/Mods/Military/art/models/arquebusier.glb` (319,752 bytes).
- Art: `ART_DEF_UNIT_ARQUEBUSIER`. Source scale: 0.44.
- NIF: `art/units/musketman/musketman.nif`.
- KFM: `art/units/musketman/musketman.kfm`.
- Geometry: 715 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2782, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `4f4b457eb85fcae7f98639ea9dfdf786e2394db9b31af99e67f1b6490e577bed`.
- idle: `art/units/musketman/musketman_md_idle.kf`.
- run: `art/units/musketman/musketman_md_run.kf`.
- fortify: `art/units/musketman/musketman_md_rangedfortify.kf`.
- attack: `art/units/musketman/musketman_md_rangedstrike.kf`.
- hurt: `art/units/musketman/musketman_md_hurta.kf`.
- die: `art/units/musketman/musketman_md_rangeddiea.kf`.

### military:artillery

- Model: `src/Mods/Military/art/models/artillery.glb` (180,064 bytes).
- Art: `ART_DEF_UNIT_ARTILLERY`. Source scale: 0.65.
- NIF: `art/units/artillery/artillery.nif`.
- KFM: `art/units/artillery/artillery.kfm`.
- Geometry: 478 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2534, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels).
- SHA-256: `49ceae587cfab4ae92dc3f7bb8461b654d7c50907c400d6ed3ec89831f691bb3`.
- idle: `art/units/artillery/artillery_md_idle.kf`.
- run: `art/units/artillery/artillery_md_run.kf`.
- fortify: `art/units/artillery/artillery_md_rangedfortify.kf`.
- attack: `art/units/artillery/artillery_md_rangedstrike.kf`.
- hurt: `art/units/artillery/artillery_md_hurta.kf`.

### military:artist

- Model: `src/Mods/Military/art/models/artist.glb` (204,768 bytes).
- Art: `ART_DEF_UNIT_ARTIST`. Source scale: 0.46.
- NIF: `art/units/greatpeople/artist/greatartist.nif`.
- KFM: `art/units/greatpeople/artist/greatartist.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2844, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels).
- SHA-256: `d17b193e4de6bd1b310780f7254a8acba1c00cc770893c7d77d7478e1ef6686f`.
- idle: `art/units/greatpeople/artist/greatartist_md_idle.kf`.
- run: `art/units/greatpeople/artist/greatartist_md_run.kf`.
- fortify: `art/units/greatpeople/artist/greatartist_md_fortify.kf`.

### military:asatru_missionary

- Model: `src/Mods/Military/art/models/asatru_missionary.glb` (200,792 bytes).
- Art: `ART_DEF_UNIT_ASATRU_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/gothi/missionary_general.nif`.
- KFM: `art/units/gothi/missionary_general.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2677, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), die (48 channels).
- SHA-256: `e091f42c03bd6eec828257444a508ba0f13d1b1bc7ce0193c3b6f369ab3dc97a`.
- idle: `art/units/gothi/missionary_general_md_idle.kf`.
- run: `art/units/gothi/missionary_general_md_run.kf`.
- fortify: `art/units/gothi/missionary_general_md_fortifya.kf`.
- die: `art/units/gothi/missionary_general_md_idle_die.kf`.

### military:assault_droid

- Model: `src/Mods/Military/art/models/assault_droid.glb` (668,020 bytes).
- Art: `ART_DEF_UNIT_ASSAULT_DROID`. Source scale: 0.8.
- NIF: `art/units/assault droid/mech.nif`.
- KFM: `art/units/assault droid/mech.kfm`.
- Geometry: 1700 triangles, 1 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.5752, radius 0.4800, feet Y=0.
- Clips: idle (186 channels), run (186 channels), fortify (186 channels), attack (186 channels), hurt (186 channels).
- SHA-256: `11cc90c6f8cc0feba675480666798a8b0a9aa28332cd00fb4aa789439c5668de`.
- idle: `art/units/assault droid/mech_md_idle.kf`.
- run: `art/units/assault droid/mech_md_run.kf`.
- fortify: `art/units/assault droid/mech_md_rangedfortify.kf`.
- attack: `art/units/assault droid/mech_md_rangedstrike.kf`.
- hurt: `art/units/assault droid/mech_md_rangedhurta.kf`.

### military:assault_mech

- Model: `src/Mods/Military/art/models/assault_mech.glb` (429,824 bytes).
- Art: `ART_DEF_UNIT_ASSAULT_MECH`. Source scale: 0.35.
- NIF: `art/units/assault_mech/nextwar_assaultmech.nif`.
- KFM: `art/units/assault_mech/nextwar_assaultmech.kfm`.
- Geometry: 833 triangles, 3 skins, 20 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4350, feet Y=0.
- Clips: idle (81 channels), run (81 channels), fortify (81 channels), attack (81 channels), die (81 channels).
- SHA-256: `e8327bc7a317876462d6723e595495c46c000e8cb9eddef2a58d5bd51c9c1168`.
- idle: `art/units/assault_mech/nextwar_assaultmech_md_idle.kf`.
- run: `art/units/assault_mech/nextwar_assaultmech_md_run.kf`.
- fortify: `art/units/assault_mech/nextwar_assaultmech_md_rangedfortify.kf`.
- attack: `art/units/assault_mech/nextwar_assaultmech_md_rangedstrikea.kf`.
- die: `art/units/assault_mech/nextwar_assaultmech_md_rangeddiea.kf`.

### military:assyria_ram

- Model: `src/Mods/Military/art/models/assyria_ram.glb` (335,008 bytes).
- Art: `ART_DEF_UNIT_ASSYRIA_RAM`. Source scale: 0.70.
- NIF: `art/units/battering_ram/catapult.nif`.
- KFM: `art/units/siege ram/ram.kfm`.
- Geometry: 1452 triangles, 0 skins, 0 joint bindings, 3 textures.
- Bounds: height 0.4343, radius 0.4800, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels).
- SHA-256: `08cf67de9a90f0e9870bba0b05ce55ec84360c3c2f2f20660f4e65707e9cd072`.
- idle: `art/units/siege ram/ram_md_idle.kf`.
- run: `art/units/siege ram/ram_md_run.kf`.
- fortify: `art/units/siege ram/ram_md_rangedfortify.kf`.
- attack: `art/units/siege ram/ram_md_rangedstrike.kf`.
- hurt: `art/units/siege ram/ram_md_hurta.kf`.
- Note: Unreadable diffuse texture: annual_rings.nif: cannot identify image file '/home/gordon/workspace/civ/AND-extraction/media/assets/art/units/battering_ram/annual_rings.nif'
- Note: Unreadable diffuse texture: rope.nif: cannot identify image file '/home/gordon/workspace/civ/AND-extraction/media/assets/art/units/battering_ram/rope.nif'

### military:assyrian_redu

- Model: `src/Mods/Military/art/models/assyrian_redu.glb` (311,404 bytes).
- Art: `ART_DEF_UNIT_ASSYRIAN_REDU`. Source scale: 0.44.
- NIF: `art/units/redu/spearman.nif`.
- KFM: `art/units/redu/spearman.kfm`.
- Geometry: 752 triangles, 1 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2904, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels), die (48 channels).
- SHA-256: `8b5987803d01b497f776acc25cef708650b61c7f2a84bebf716e189b3cd805cf`.
- idle: `art/units/redu/spearman_md_idle.kf`.
- run: `art/units/redu/spearman_md_run.kf`.
- fortify: `art/units/redu/spearman_md_fortifya.kf`.
- attack: `art/units/redu/spearman_md_strikea.kf`.
- hurt: `art/units/redu/spearman_md_hurta.kf`.
- die: `art/units/redu/spearman_md_diea.kf`.

### military:at_infantry

- Model: `src/Mods/Military/art/models/at_infantry.glb` (433,196 bytes).
- Art: `ART_DEF_UNIT_AT_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/antitankinfantry/wwantitankinfantry.nif`.
- KFM: `art/units/antitankinfantry/wwantitankinfantry.kfm`.
- Geometry: 800 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3512, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), die (44 channels).
- SHA-256: `17e00eab48dc8d61834cfc9c07365f3e2283e0ce33e9546f059b40aae23a9784`.
- idle: `art/units/antitankinfantry/wwantitankinfantry_md_idle.kf`.
- run: `art/units/antitankinfantry/wwantitankinfantry_md_run.kf`.
- fortify: `art/units/antitankinfantry/wwantitankinfantry_md_rangedfortify.kf`.
- attack: `art/units/antitankinfantry/wwantitankinfantry_md_rangedstrike.kf`.
- die: `art/units/antitankinfantry/wwantitankinfantry_md_rangeddiea.kf`.

### military:atomic

- Model: `src/Mods/Military/art/models/atomic.glb` (255,280 bytes).
- Art: `ART_DEF_UNIT_ATOMIC`. Source scale: 0.5.
- NIF: `art/units/a bomb/bomber.nif`.
- KFM: `art/units/a bomb/bomber.kfm`.
- Geometry: 1006 triangles, 4 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.1799, radius 0.4800, feet Y=0.
- Clips: idle (61 channels), run (61 channels), hurt (61 channels), die (61 channels).
- SHA-256: `2aeafcf7c42d2bb20b86da08c39401b2969a316f372a679801d2236da9f93818`.
- idle: `art/units/a bomb/bomber_md_idle.kf`.
- run: `art/units/a bomb/bomber_md_run.kf`.
- hurt: `art/units/a bomb/bomber_md_hurt.kf`.
- die: `art/units/a bomb/bomber_md_die.kf`.

### military:attack_submarine

- Model: `src/Mods/Military/art/models/attack_submarine.glb` (180,004 bytes).
- Art: `ART_DEF_UNIT_ATTACK_SUBMARINE`. Source scale: 0.35.
- NIF: `art/units/nuclear_sub/nuclear_sub.nif`.
- KFM: `art/units/nuclear_sub/nuclear_sub.kfm`.
- Geometry: 305 triangles, 2 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.2504, radius 0.4800, feet Y=0.
- Clips: idle (29 channels), run (29 channels), fortify (29 channels), attack (29 channels), hurt (29 channels), die (29 channels).
- SHA-256: `95a33e0492298c18ca401593873278f77296bfaf50b46f0891107e33c7c98cf5`.
- idle: `art/units/nuclear_sub/nuclear_sub_md_idle.kf`.
- run: `art/units/nuclear_sub/nuclear_sub_md_run.kf`.
- fortify: `art/units/nuclear_sub/nuclear_sub_md_fortifya.kf`.
- attack: `art/units/nuclear_sub/nuclear_sub_md_strikea.kf`.
- hurt: `art/units/nuclear_sub/nuclear_sub_md_hurta.kf`.
- die: `art/units/nuclear_sub/nuclear_sub_md_diea.kf`.

### military:automatons

- Model: `src/Mods/Military/art/models/automatons.glb` (296,024 bytes).
- Art: `ART_DEF_UNIT_AUTOMATON`. Source scale: 0.44.
- NIF: `art/units/nextwar_automaton/nextwar_automaton.nif`.
- KFM: `art/units/nextwar_automaton/nextwar_automaton.kfm`.
- Geometry: 920 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3946, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), die (57 channels).
- SHA-256: `287ed69a86e9154907739ce3cbd09974021d8db33b975e9cda5b1fc837e0b663`.
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

- Model: `src/Mods/Military/art/models/axeman.glb` (296,676 bytes).
- Art: `ART_DEF_UNIT_AXEMAN`. Source scale: 0.44.
- NIF: `art/units/axeman/axeman.nif`.
- KFM: `art/units/axeman/axeman.kfm`.
- Geometry: 681 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3944, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `4670786bc8b23da1a05d6298d277bbd95d295dc71142375fe7c7330274e6077f`.
- idle: `art/units/axeman/axeman_md_idle.kf`.
- run: `art/units/axeman/axeman_md_run.kf`.
- fortify: `art/units/axeman/axeman_md_fortifya.kf`.
- attack: `art/units/axeman/axeman_md_strikea.kf`.
- hurt: `art/units/axeman/axeman_md_hurta.kf`.
- die: `art/units/axeman/axeman_md_diea.kf`.

### military:axeman_latvia

- Model: `src/Mods/Military/art/models/axeman_latvia.glb` (414,368 bytes).
- Art: `ART_DEF_UNIT_AXEMAN_LATVIA`. Source scale: 0.44.
- NIF: `art/latvia/axemanlatvia/axeman.nif`.
- KFM: `art/latvia/axemanlatvia/axeman.kfm`.
- Geometry: 663 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3236, feet Y=0.
- Clips: idle (92 channels), run (92 channels), fortify (92 channels), attack (92 channels), die (92 channels).
- SHA-256: `0c745232bf2e36f41a2a1dcc6e6c7b2eb09009edef46c3b6dd9571b169f7e5ee`.
- idle: `art/latvia/axemanlatvia/axeman_md_idle.kf`.
- run: `art/latvia/axemanlatvia/axeman_md_run.kf`.
- fortify: `art/latvia/axemanlatvia/axeman_md_fortifya.kf`.
- attack: `art/latvia/axemanlatvia/axeman_md_strikea.kf`.
- die: `art/latvia/axemanlatvia/axeman_md_dieb.kf`.
- Note: Hidden geometry omitted: Lightswordsman_sword
- Note: Hidden geometry omitted: Warrior_Body

### military:aztec_jaguar

- Model: `src/Mods/Military/art/models/aztec_jaguar.glb` (359,076 bytes).
- Art: `ART_DEF_UNIT_AZTEC_JAGUAR`. Source scale: 0.44.
- NIF: `art/units/unique/aztec/jaguar/aztec_jaguar.nif`.
- KFM: `art/units/unique/aztec/jaguar/aztec_jaguar.kfm`.
- Geometry: 702 triangles, 2 skins, 29 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3541, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `fc432decc649e8fef73529d031331d22d9c847704978a893f8c337a98093506d`.
- idle: `art/units/unique/aztec/jaguar/aztec_jaguar_md_idle.kf`.
- run: `art/units/unique/aztec/jaguar/aztec_jaguar_md_run.kf`.
- fortify: `art/units/unique/aztec/jaguar/aztec_jaguar_md_fortifya.kf`.
- attack: `art/units/unique/aztec/jaguar/aztec_jaguar_md_strikea.kf`.
- hurt: `art/units/unique/aztec/jaguar/aztec_jaguar_md_hurta.kf`.
- die: `art/units/unique/aztec/jaguar/aztec_jaguar_md_diea.kf`.

### military:b52

- Model: `src/Mods/Military/art/models/b52.glb` (166,440 bytes).
- Art: `ART_DEF_UNIT_B52`. Source scale: 0.5.
- NIF: `art/units/strategic bomber/b52.nif`.
- KFM: `art/units/stealthbomber/stealthbomber.kfm`.
- Geometry: 960 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.1848, radius 0.4800, feet Y=0.
- Clips: idle (25 channels), run (25 channels), hurt (25 channels), die (25 channels).
- SHA-256: `71d8377605e44ee1a3a2ec12f3e9770a19eaa295af05fd877cdcb2f6e93a2f6f`.
- idle: `art/units/stealthbomber/stealthbomber_md_idle.kf`.
- run: `art/units/stealthbomber/stealthbomber_md_run.kf`.
- hurt: `art/units/stealthbomber/stealthbomber_md_hurt.kf`.
- die: `art/units/stealthbomber/stealthbomber_md_die.kf`.

### military:babylon_bowman

- Model: `src/Mods/Military/art/models/babylon_bowman.glb` (270,948 bytes).
- Art: `ART_DEF_UNIT_BABYLON_BOWMAN`. Source scale: 0.44.
- NIF: `art/units/unique_babylon_bowman/unique_babylon_bowman.nif`.
- KFM: `art/units/unique_babylon_bowman/unique_babylon_bowman.kfm`.
- Geometry: 712 triangles, 2 skins, 21 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3953, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), die (53 channels).
- SHA-256: `ddb577d3e7f1711aaf160f87ded5363b79cf3c233f7d06762ba978203ef046cf`.
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

- Model: `src/Mods/Military/art/models/battleship.glb` (353,908 bytes).
- Art: `ART_DEF_UNIT_BATTLESHIP`. Source scale: 0.8.
- NIF: `art/units/battleship/battleship.nif`.
- KFM: `art/units/battleship/battleship.kfm`.
- Geometry: 641 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.2730, radius 0.4800, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `a304e4d98d14eff7d56deccd02c3bb74976b93d6b1be8cfc64325982a3c6cdda`.
- idle: `art/units/battleship/battleship_md_idle.kf`.
- run: `art/units/battleship/battleship_md_run.kf`.
- fortify: `art/units/battleship/battleship_md_fortifya.kf`.
- attack: `art/units/battleship/battleship_md_strikea.kf`.
- hurt: `art/units/battleship/battleship_md_hurta.kf`.
- die: `art/units/battleship/battleship_md_diea.kf`.

### military:bazooka

- Model: `src/Mods/Military/art/models/bazooka.glb` (383,872 bytes).
- Art: `ART_DEF_UNIT_BAZOOKA`. Source scale: 0.44.
- NIF: `art/units/bazooka/european/saminfantry.nif`.
- KFM: `art/units/bazooka/european/saminfantry.kfm`.
- Geometry: 754 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3298, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `388e109510140c71d1c57ae10b0fd26798f2516c835cceb2e333e08bb632476d`.
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
- Bounds: height 0.5203, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `d99621d6c2ee5e76d5c1fe214a7f05251cd4f28aef263e18f41110e0fdab011d`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `b5d54819e0dbf360ebc2e7e1151db0f8549ebb5e6ad6b7f592709121582255f5`.
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
- SHA-256: `4d5f2d82a46d49a0edc5a78362d2df5fc63b1a1dc59d5cb48025223390aa16e4`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:biological_warfare_missile

- Model: `src/Mods/Military/art/models/biological_warfare_missile.glb` (79,384 bytes).
- Art: `ART_DEF_UNIT_BIOLOGICAL_WARFARE_MISSILE`. Source scale: 0.40.
- NIF: `art/units/bioweapon_missile/bioweapon_missile.nif`.
- KFM: `art/units/bioweapon_missile/bioweapon_missile.kfm`.
- Geometry: 146 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.1889, radius 0.4800, feet Y=0.
- Clips: idle (19 channels), run (19 channels), attack (19 channels).
- SHA-256: `2db02ccc7542e0bbfeff8042b1dfd73e53a5bced0022140d43f1d5bc7e00d906`.
- idle: `art/units/bioweapon_missile/bioweapon_missile_md_idle.kf`.
- run: `art/units/bioweapon_missile/bioweapon_missile_md_run.kf`.
- attack: `art/units/bioweapon_missile/bioweapon_missile_md_strikea.kf`.

### military:bleeder

- Model: `src/Mods/Military/art/models/bleeder.glb` (401,220 bytes).
- Art: `ART_DEF_UNIT_CYBORG1`. Source scale: 0.44.
- NIF: `art/units/genetic soldier/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 1036 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2812, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `683883f124cfe969542f6de3ca59afb2bed4ca6bdd13dbe6afe17d3483ccf4dd`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.
- Note: Missing texture: //cca.dds

### military:boar

- Model: `src/Mods/Military/art/models/boar.glb` (885,244 bytes).
- Art: `ART_DEF_UNIT_BOAR`. Source scale: 0.38.
- NIF: `art/units/boar/companion_cavalry.nif`.
- KFM: `art/units/knight_south_america/companion_cavalry.kfm`.
- Geometry: 948 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.5969, radius 0.4800, feet Y=0.
- Clips: idle (164 channels), run (164 channels), fortify (164 channels), attack (164 channels), hurt (164 channels), die (164 channels).
- SHA-256: `d76f28fbddd1d76c2c1d7935bc165cf525184c4733eda62e846eeed8acf19ecd`.
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
- SHA-256: `cf980dba3a87abe52eb4852fd88648e011e3e09d270055707fde310ad56c35db`.
- idle: `art/units/bombard/bombardb_md_idle.kf`.
- run: `art/units/bombard/bombardb_md_run.kf`.
- fortify: `art/units/bombard/bombardb_md_rangedfortify.kf`.
- attack: `art/units/bombard/bombardb_md_rangedstrike.kf`.
- hurt: `art/units/bombard/bombardb_md_hurta.kf`.

### military:bomber

- Model: `src/Mods/Military/art/models/bomber.glb` (304,340 bytes).
- Art: `ART_DEF_UNIT_BOMBER`. Source scale: 0.5.
- NIF: `art/units/bomber/bomber.nif`.
- KFM: `art/units/bomber/bomber.kfm`.
- Geometry: 1006 triangles, 4 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.1799, radius 0.4800, feet Y=0.
- Clips: idle (61 channels), run (61 channels), hurt (61 channels), die (61 channels).
- SHA-256: `fff006c274fa2b04875361ac9770ee0cc54f3f8e7856dbe93b99679a0be7b5d9`.
- idle: `art/units/bomber/bomber_md_idle.kf`.
- run: `art/units/bomber/bomber_md_run.kf`.
- hurt: `art/units/bomber/bomber_md_hurt.kf`.
- die: `art/units/bomber/bomber_md_die.kf`.

### military:brigantine

- Model: `src/Mods/Military/art/models/brigantine.glb` (288,324 bytes).
- Art: `ART_DEF_UNIT_CARAVEL_SQUARERIG`. Source scale: 0.22.
- NIF: `art/units/brigantine/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 758 triangles, 3 skins, 13 joint bindings, 2 textures.
- Bounds: height 0.5886, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `5b56b38a4be602557ce9b0fb5164726e44838211f33383677ddf5a01cc2c3fa9`.
- idle: `art/units/caravel/caravel_md_idle.kf`.
- run: `art/units/caravel/caravel_md_run.kf`.
- fortify: `art/units/caravel/caravel_md_fortifya.kf`.
- attack: `art/units/caravel/caravel_md_strikea.kf`.
- hurt: `art/units/caravel/caravel_md_hurta.kf`.
- die: `art/units/caravel/caravel_md_diea.kf`.

### military:buddhist_missionary

- Model: `src/Mods/Military/art/models/buddhist_missionary.glb` (221,816 bytes).
- Art: `ART_DEF_UNIT_BUDDHIST_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/buddhist/buddhist_missionary.nif`.
- KFM: `art/units/missionary/buddhist/buddhist_missionary.kfm`.
- Geometry: 630 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2756, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), die (48 channels).
- SHA-256: `278b7a0387c23ef583c1f8f072999829e73f05a35813f436f5eb4ee3d11fdcac`.
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
- Bounds: height 0.6000, radius 0.3966, feet Y=0.
- Clips: idle (103 channels), run (103 channels), fortify (103 channels), attack (103 channels), hurt (103 channels), die (103 channels).
- SHA-256: `86ca409eb03848ffc50d423dbb6455b272da225b1c7d25a5432819fd1a4e1b77`.
- idle: `art/bulgaria/konnik/horseman_eurasian_md_idle.kf`.
- run: `art/bulgaria/konnik/horseman_eurasian_md_run.kf`.
- fortify: `art/bulgaria/konnik/horseman_eurasian_md_fortifya.kf`.
- attack: `art/bulgaria/konnik/horseman_eurasian_md_strikea.kf`.
- hurt: `art/bulgaria/konnik/horseman_eurasian_md_hurta.kf`.
- die: `art/bulgaria/konnik/horseman_eurasian_md_diea.kf`.

### military:bull

- Model: `src/Mods/Military/art/models/bull.glb` (146,908 bytes).
- Art: `ART_DEF_UNIT_BULL`. Source scale: 0.5.
- NIF: `art/units/bull/cow.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1182 triangles, 3 skins, 42 joint bindings, 0 textures.
- Bounds: height 0.5743, radius 0.4800, feet Y=0.
- Clips: idle (24 channels), run (24 channels), fortify (24 channels), attack (24 channels), hurt (24 channels), die (24 channels).
- SHA-256: `221cf406c09422146d3887757ca66e1b7829ea829d1a895f578cc63c522ebd19`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.
- Note: Missing texture: bizon.dds
- Note: No diffuse texture resolved

### military:byzantine_cataphract

- Model: `src/Mods/Military/art/models/byzantine_cataphract.glb` (884,976 bytes).
- Art: `ART_DEF_UNIT_BYZANTINE_CATAPHRACT`. Source scale: 0.42.
- NIF: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract.nif`.
- KFM: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract.kfm`.
- Geometry: 1330 triangles, 3 skins, 47 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3987, feet Y=0.
- Clips: idle (158 channels), run (158 channels), fortify (158 channels), attack (158 channels), hurt (158 channels), die (158 channels).
- SHA-256: `32894bd4a933c88a180596fc3cf73150f514a93caae1939b49d13020cdd99fcb`.
- idle: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_idle.kf`.
- run: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_run.kf`.
- fortify: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_fortifya.kf`.
- attack: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_strikea.kf`.
- hurt: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_hurta.kf`.
- die: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_diea.kf`.

### military:cannon

- Model: `src/Mods/Military/art/models/cannon.glb` (363,320 bytes).
- Art: `ART_DEF_UNIT_CANNON`. Source scale: 0.53.
- NIF: `art/units/cannon/cannon.nif`.
- KFM: `art/units/cannon/cannon.kfm`.
- Geometry: 1168 triangles, 3 skins, 23 joint bindings, 4 textures.
- Bounds: height 0.5620, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels).
- SHA-256: `993b6a64335e5e90a90b8242aa178fec5316c6883d0126d33eab87553b9dce73`.
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
- Bounds: height 0.5203, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `d99621d6c2ee5e76d5c1fe214a7f05251cd4f28aef263e18f41110e0fdab011d`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `b5d54819e0dbf360ebc2e7e1151db0f8549ebb5e6ad6b7f592709121582255f5`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_boar

- Model: `src/Mods/Military/art/models/captured_boar.glb` (885,244 bytes).
- Art: `ART_DEF_UNIT_BOAR`. Source scale: 0.38.
- NIF: `art/units/boar/companion_cavalry.nif`.
- KFM: `art/units/knight_south_america/companion_cavalry.kfm`.
- Geometry: 948 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.5969, radius 0.4800, feet Y=0.
- Clips: idle (164 channels), run (164 channels), fortify (164 channels), attack (164 channels), hurt (164 channels), die (164 channels).
- SHA-256: `d76f28fbddd1d76c2c1d7935bc165cf525184c4733eda62e846eeed8acf19ecd`.
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

- Model: `src/Mods/Military/art/models/captured_bull.glb` (146,908 bytes).
- Art: `ART_DEF_UNIT_BULL`. Source scale: 0.5.
- NIF: `art/units/bull/cow.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1182 triangles, 3 skins, 42 joint bindings, 0 textures.
- Bounds: height 0.5743, radius 0.4800, feet Y=0.
- Clips: idle (24 channels), run (24 channels), fortify (24 channels), attack (24 channels), hurt (24 channels), die (24 channels).
- SHA-256: `221cf406c09422146d3887757ca66e1b7829ea829d1a895f578cc63c522ebd19`.
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
- Bounds: height 0.5756, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `0a4e1c2f42b86de360d4637dc3638d398da1baeedb1a6b385732e08b313d5ee4`.
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
- Bounds: height 0.5203, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b0f48a83fcbb7469d52f4f804a9c1fd9eb18a43ec37994189bfaaf742234a2f9`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `b3fb749d058153b61d01d1560a317e0046e5527f824e54c54dc2768e2d5ac947`.
- idle: `art/units/animals/cheetah/panther_md_idle.kf`.
- run: `art/units/animals/cheetah/panther_md_run.kf`.
- fortify: `art/units/animals/cheetah/panther_md_fortifya.kf`.
- attack: `art/units/animals/cheetah/panther_md_strikea.kf`.
- hurt: `art/units/animals/cheetah/panther_md_hurta.kf`.
- die: `art/units/animals/cheetah/panther_md_diea.kf`.

### military:captured_direwolf

- Model: `src/Mods/Military/art/models/captured_direwolf.glb` (346,820 bytes).
- Art: `ART_DEF_UNIT_DIREWOLF`. Source scale: 0.55.
- NIF: `art/units/animals/direwolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5084, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `1e4ad0a20ef70611820ca63758e52ccce5590c98f4590c70b2f31a256b84f35a`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:captured_elephant

- Model: `src/Mods/Military/art/models/captured_elephant.glb` (583,240 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/elephant/elephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 630 triangles, 1 skins, 24 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4359, feet Y=0.
- Clips: idle (101 channels), run (101 channels), fortify (101 channels), attack (101 channels), hurt (101 channels), die (101 channels).
- SHA-256: `44a168fb23584d995a32898bd61e2af6bd6275f18e9d0c9130baf8eab79b8e1f`.
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
- Bounds: height 0.5756, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `f4889d9d081755a0e164d37f13b2586c90438216bd1e1be6251d4eb876e66ee3`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `a794b9793cde66fa5d05c3f452d06876226a43661daebbeea20f9af213d6cd4f`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_polarbear

- Model: `src/Mods/Military/art/models/captured_polarbear.glb` (275,428 bytes).
- Art: `ART_DEF_UNIT_POLARBEAR`. Source scale: 0.7.
- NIF: `art/units/animals/polarbear/bear.nif`.
- KFM: `art/units/animals/polarbear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5203, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `80919b1ee5fd5e2ffcefa1238e8b08fe825076cd66c09d8eab58368edd4236d3`.
- idle: `art/units/animals/polarbear/bear_md_idle.kf`.
- run: `art/units/animals/polarbear/bear_md_run.kf`.
- fortify: `art/units/animals/polarbear/bear_md_fortifya.kf`.
- attack: `art/units/animals/polarbear/bear_md_strikea.kf`.
- hurt: `art/units/animals/polarbear/bear_md_hurta.kf`.
- die: `art/units/animals/polarbear/bear_md_diea.kf`.

### military:captured_ram

- Model: `src/Mods/Military/art/models/captured_ram.glb` (460,112 bytes).
- Art: `ART_DEF_UNIT_WILD_RAM`. Source scale: 18.0.
- NIF: `art/units/wild_ram/mouflon.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 794 triangles, 2 skins, 33 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2982, feet Y=0.
- Clips: none.
- SHA-256: `42759592a8ef91c55b3859a0a1afaada148b2d559d807f6957a2bfe206685f9a`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `51f01faafa3f0e6f35bbe24a284211e94efe745e7adb75330c63cb7771d8f08d`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_stag

- Model: `src/Mods/Military/art/models/captured_stag.glb` (712,084 bytes).
- Art: `ART_DEF_UNIT_STAG`. Source scale: 0.38.
- NIF: `art/units/stag/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 392 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2812, feet Y=0.
- Clips: idle (154 channels), run (154 channels), fortify (154 channels), attack (154 channels), hurt (154 channels), die (154 channels).
- SHA-256: `393ea8b4b049a18897f128bdece11ffbacd9ee4ce0512a4048e978e333ecea9a`.
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
- SHA-256: `e8b38708b6c16d203283530f0a3ea77ec8533739cf838692b089d82bb55d7101`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:captured_wolf

- Model: `src/Mods/Military/art/models/captured_wolf.glb` (346,724 bytes).
- Art: `ART_DEF_UNIT_WOLF`. Source scale: 0.5.
- NIF: `art/units/wolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5084, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `31e8f032541f2f0e5a2d97e80f3399cf75672356ac21e4a4411f461832f53c86`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:caravel

- Model: `src/Mods/Military/art/models/caravel.glb` (192,180 bytes).
- Art: `ART_DEF_UNIT_CARAVEL`. Source scale: 0.24.
- NIF: `art/units/caravel/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 427 triangles, 3 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3653, feet Y=0.
- Clips: idle (23 channels), run (23 channels), fortify (23 channels), attack (23 channels), hurt (23 channels), die (23 channels).
- SHA-256: `330e1509a730cb250c7c9bdc8fe94bd4593b2664608cfeaebdac1f4c2c0c5ab0`.
- idle: `art/units/caravel/caravel_md_idle.kf`.
- run: `art/units/caravel/caravel_md_run.kf`.
- fortify: `art/units/caravel/caravel_md_fortifya.kf`.
- attack: `art/units/caravel/caravel_md_strikea.kf`.
- hurt: `art/units/caravel/caravel_md_hurta.kf`.
- die: `art/units/caravel/caravel_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:carrier

- Model: `src/Mods/Military/art/models/carrier.glb` (194,328 bytes).
- Art: `ART_DEF_UNIT_CARRIER_ENGLAND`. Source scale: 1.0.
- NIF: `art/units/carrier/english/ark_royal.nif`.
- KFM: `art/units/carrier/carrier.kfm`.
- Geometry: 594 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.2222, radius 0.4800, feet Y=0.
- Clips: idle (10 channels), run (10 channels), fortify (10 channels), attack (10 channels), hurt (10 channels), die (10 channels).
- SHA-256: `89e1cad90a61e6b6fa6b292ab4b45194088ea6bd5b60b0430c1d7512267fdad2`.
- idle: `art/units/carrier/carrier_md_idle.kf`.
- run: `art/units/carrier/carrier_md_run.kf`.
- fortify: `art/units/carrier/carrier_md_fortifya.kf`.
- attack: `art/units/carrier/carrier_md_strikea.kf`.
- hurt: `art/units/carrier/carrier_md_hurta.kf`.
- die: `art/units/carrier/carrier_md_diea.kf`.

### military:carthage_numidian_cavalry

- Model: `src/Mods/Military/art/models/carthage_numidian_cavalry.glb` (585,280 bytes).
- Art: `ART_DEF_UNIT_CARTHAGE_NUMIDIAN_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/numidian_cavalry/numidian_cavalry.nif`.
- KFM: `art/units/numidian_cavalry/numidian_cavalry.kfm`.
- Geometry: 1027 triangles, 2 skins, 35 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3036, feet Y=0.
- Clips: idle (105 channels), run (105 channels), fortify (105 channels), attack (105 channels), hurt (105 channels), die (105 channels).
- SHA-256: `3fde4dfa2dd280bcf690194e67ff5100fb180ac9e4f5e8a7898ecf69dbbe1ffa`.
- idle: `art/units/numidian_cavalry/numidian_cavalry_md_idle.kf`.
- run: `art/units/numidian_cavalry/numidian_cavalry_md_run.kf`.
- fortify: `art/units/numidian_cavalry/numidian_cavalry_md_fortifya.kf`.
- attack: `art/units/numidian_cavalry/numidian_cavalry_md_strikea.kf`.
- hurt: `art/units/numidian_cavalry/numidian_cavalry_md_hurta.kf`.
- die: `art/units/numidian_cavalry/numidian_cavalry_md_diea.kf`.

### military:catapult

- Model: `src/Mods/Military/art/models/catapult.glb` (286,440 bytes).
- Art: `ART_DEF_UNIT_CATAPULT`. Source scale: 0.65.
- NIF: `art/units/catapult/catapult.nif`.
- KFM: `art/units/catapult/catapult.kfm`.
- Geometry: 646 triangles, 1 skins, 9 joint bindings, 2 textures.
- Bounds: height 0.5198, radius 0.4800, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels), attack (26 channels), hurt (26 channels).
- SHA-256: `6f91bb4f90446228cf7e391db74d8e9bfa16597edd47f0cd479e8e2560e8df1d`.
- idle: `art/units/catapult/catapult_md_idle.kf`.
- run: `art/units/catapult/catapult_md_run.kf`.
- fortify: `art/units/catapult/catapult_md_rangedfortify.kf`.
- attack: `art/units/catapult/catapult_md_rangedstrike.kf`.
- hurt: `art/units/catapult/catapult_md_hurta.kf`.

### military:cavalry

- Model: `src/Mods/Military/art/models/cavalry.glb` (554,624 bytes).
- Art: `ART_DEF_UNIT_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/cavalry/cavalry.nif`.
- KFM: `art/units/cavalry/cavalry.kfm`.
- Geometry: 1142 triangles, 4 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3090, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (86 channels), attack (86 channels), hurt (86 channels), die (86 channels).
- SHA-256: `8635bff6c1fbd1d84f4310c76cec3d296743e18440cee65b27d0b9c9832cf539`.
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
- SHA-256: `ce275541448f0894991514061d3027335806f1c10135714d7a5c259b31934311`.
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
- Bounds: height 0.5756, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `0a4e1c2f42b86de360d4637dc3638d398da1baeedb1a6b385732e08b313d5ee4`.
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
- Bounds: height 0.5203, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b0f48a83fcbb7469d52f4f804a9c1fd9eb18a43ec37994189bfaaf742234a2f9`.
- idle: `art/units/animals/cavebear/bear_md_idle.kf`.
- run: `art/units/animals/cavebear/bear_md_run.kf`.
- fortify: `art/units/animals/cavebear/bear_md_fortifya.kf`.
- attack: `art/units/animals/cavebear/bear_md_strikea.kf`.
- hurt: `art/units/animals/cavebear/bear_md_hurta.kf`.
- die: `art/units/animals/cavebear/bear_md_diea.kf`.

### military:celtic_gallic_warrior

- Model: `src/Mods/Military/art/models/celtic_gallic_warrior.glb` (299,180 bytes).
- Art: `ART_DEF_UNIT_CELTIC_BARBARIAN`. Source scale: 0.44.
- NIF: `art/units/celtic_warrior/celtic_warrior.nif`.
- KFM: `art/units/celtic_warrior/celtic_warrior.kfm`.
- Geometry: 595 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3494, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `8bf70a94331d9e657f670998d38c7055c7b6045eda79416ffc75038d3147d4e4`.
- idle: `art/units/celtic_warrior/celtic_warrior_md_idle.kf`.
- run: `art/units/celtic_warrior/celtic_warrior_md_run.kf`.
- fortify: `art/units/celtic_warrior/celtic_warrior_md_fortifya.kf`.
- attack: `art/units/celtic_warrior/celtic_warrior_md_strikea.kf`.
- hurt: `art/units/celtic_warrior/celtic_warrior_md_hurta.kf`.
- die: `art/units/celtic_warrior/celtic_warrior_md_diea.kf`.

### military:chariot

- Model: `src/Mods/Military/art/models/chariot.glb` (937,776 bytes).
- Art: `ART_DEF_UNIT_CHARIOT`. Source scale: 0.35.
- NIF: `art/units/chariot/chariot.nif`.
- KFM: `art/units/chariot/chariot.kfm`.
- Geometry: 1488 triangles, 5 skins, 48 joint bindings, 5 textures.
- Bounds: height 0.3837, radius 0.4800, feet Y=0.
- Clips: idle (197 channels), run (197 channels), fortify (197 channels), attack (197 channels), hurt (197 channels), die (197 channels).
- SHA-256: `9a487a79a52a3d87d6c9f6df4f74abd618a87cb1ea874b0109295eaa90d5492f`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `b3fb749d058153b61d01d1560a317e0046e5527f824e54c54dc2768e2d5ac947`.
- idle: `art/units/animals/cheetah/panther_md_idle.kf`.
- run: `art/units/animals/cheetah/panther_md_run.kf`.
- fortify: `art/units/animals/cheetah/panther_md_fortifya.kf`.
- attack: `art/units/animals/cheetah/panther_md_strikea.kf`.
- hurt: `art/units/animals/cheetah/panther_md_hurta.kf`.
- die: `art/units/animals/cheetah/panther_md_diea.kf`.

### military:china_chokonu

- Model: `src/Mods/Military/art/models/china_chokonu.glb` (456,124 bytes).
- Art: `ART_DEF_UNIT_CHINA_CHOKONU`. Source scale: 0.44.
- NIF: `art/units/unique/china/chokonu/chokunu.nif`.
- KFM: `art/units/unique/china/chokonu/chokunu.kfm`.
- Geometry: 812 triangles, 2 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3552, feet Y=0.
- Clips: idle (88 channels), run (88 channels), fortify (88 channels), attack (88 channels), hurt (88 channels), die (88 channels).
- SHA-256: `a7b88a0a4679382129c85fe8d8fe5c1b7c1bbb6aa5ebbac0e68fd16751a39776`.
- idle: `art/units/unique/china/chokonu/chokunu_md_idle.kf`.
- run: `art/units/unique/china/chokonu/chokunu_md_run.kf`.
- fortify: `art/units/unique/china/chokonu/chokunu_md_rangedfortify.kf`.
- attack: `art/units/unique/china/chokonu/chokunu_md_rangedstrike.kf`.
- hurt: `art/units/unique/china/chokonu/chokunu_md_hurta.kf`.
- die: `art/units/unique/china/chokonu/chokunu_md_rangeddiea.kf`.

### military:chivalry

- Model: `src/Mods/Military/art/models/chivalry.glb` (864,988 bytes).
- Art: `ART_DEF_UNIT_CHIVALRY`. Source scale: 0.42.
- NIF: `art/units/mailed knight/european/mtemplar_heavy.nif`.
- KFM: `art/units/knight/knight.kfm`.
- Geometry: 1090 triangles, 3 skins, 40 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2940, feet Y=0.
- Clips: idle (171 channels), run (171 channels), fortify (171 channels), attack (171 channels), hurt (171 channels), die (171 channels).
- SHA-256: `9e9fc59a7ef599b3146f6cd20aaf8580c0ff048b2c57ecbb13316db34217b499`.
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
- SHA-256: `da0b3de037fbd186ab2aabbbc227d84f72d91ec4a292d0b0e4e2d3f3250d1ffc`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:clones

- Model: `src/Mods/Military/art/models/clones.glb` (226,360 bytes).
- Art: `ART_DEF_UNIT_CLONEWORKER`. Source scale: 0.44.
- NIF: `art/units/nextwar_cloneworker/nextwar_cloneworker.nif`.
- KFM: `art/units/nextwar_cloneworker/nextwar_cloneworker.kfm`.
- Geometry: 802 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3304, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels).
- SHA-256: `3d40524b24286af2cb4bcd169589a0e82a38942537aaac60d7f359a4e2ed9ff4`.
- idle: `art/units/nextwar_cloneworker/nextwar_cloneworker_md_idle.kf`.
- run: `art/units/nextwar_cloneworker/nextwar_cloneworker_md_run.kf`.
- fortify: `art/units/nextwar_cloneworker/nextwar_cloneworker_md_fortify.kf`.
- Note: Missing texture: Modern_Worker_tools_64.dds
- Note: Missing texture: Worker_tools.dds

### military:cog

- Model: `src/Mods/Military/art/models/cog.glb` (532,444 bytes).
- Art: `ART_DEF_UNIT_COG`. Source scale: 0.13.
- NIF: `art/units/cog/cog.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 1313 triangles, 0 skins, 0 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.3313, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `51aa77b9c72825f9bfd100ec1bdfd9dae0544c42bed44b2db29f7de82116a5ad`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:colonist

- Model: `src/Mods/Military/art/models/colonist.glb` (250,304 bytes).
- Art: `ART_DEF_UNIT_COLONIST_MALE`. Source scale: 0.46.
- NIF: `art/units/settler/settler_male.nif`.
- KFM: `art/units/settler/settler_male.kfm`.
- Geometry: 700 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3317, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `c342b2acc7673e5f6668d245b9909b542bf5137b2493d497a4e6e1fe21bae56f`.
- idle: `art/units/settler/settler_male_md_idle.kf`.
- run: `art/units/settler/settler_male_md_run.kf`.
- fortify: `art/units/settler/settler_male_md_fortify.kf`.
- die: `art/units/settler/settler_male_md_idle_die.kf`.
- Note: First of 7 formation art groups exported

### military:confucian_missionary

- Model: `src/Mods/Military/art/models/confucian_missionary.glb` (189,564 bytes).
- Art: `ART_DEF_UNIT_CONFUCIAN_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/confucian/confucian_missionary.nif`.
- KFM: `art/units/missionary/confucian/confucian_missionary.kfm`.
- Geometry: 480 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2507, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), die (50 channels).
- SHA-256: `74eb64925b1322e45964732163f5811e8c317f430e79fefb9cd8c79784c12fa3`.
- idle: `art/units/missionary/confucian/confucian_missionary_md_idle.kf`.
- run: `art/units/missionary/confucian/confucian_missionary_md_run.kf`.
- fortify: `art/units/missionary/confucian/confucian_missionary_md_fortifya.kf`.
- die: `art/units/missionary/confucian/confucian_missionary_md_idle_die.kf`.

### military:construct_ship

- Model: `src/Mods/Military/art/models/construct_ship.glb` (238,972 bytes).
- Art: `ART_DEF_UNIT_CONSTRUCT_SHIP`. Source scale: 0.5.
- NIF: `art/units/finalfrontier_constructionship/constructionship.nif`.
- KFM: `art/units/finalfrontier_constructionship/constructionship.kfm`.
- Geometry: 853 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2697, radius 0.4800, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels).
- SHA-256: `99b8d4a515fe1e4ca4fdff7d0bf2607458139c267194d49dcfca906812478752`.
- idle: `art/units/finalfrontier_constructionship/constructionship_md_idle.kf`.
- run: `art/units/finalfrontier_constructionship/constructionship_md_run.kf`.
- fortify: `art/units/finalfrontier_constructionship/constructionship_md_fortifya.kf`.

### military:crossbowman

- Model: `src/Mods/Military/art/models/crossbowman.glb` (448,872 bytes).
- Art: `ART_DEF_UNIT_CROSSBOWMAN`. Source scale: 0.44.
- NIF: `art/units/crossbowman/crossbowman.nif`.
- KFM: `art/units/crossbowman/crossbowman.kfm`.
- Geometry: 832 triangles, 3 skins, 22 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3016, feet Y=0.
- Clips: idle (88 channels), run (88 channels), fortify (88 channels), attack (88 channels), hurt (88 channels), die (88 channels).
- SHA-256: `8d177c906c722b258dfa677bc08c631d30a39238debdf783c5c3a7f7b94c4d35`.
- idle: `art/units/crossbowman/crossbowman_md_idle.kf`.
- run: `art/units/crossbowman/crossbowman_md_run.kf`.
- fortify: `art/units/crossbowman/crossbowman_md_fortifya.kf`.
- attack: `art/units/crossbowman/crossbowman_md_strikea.kf`.
- hurt: `art/units/crossbowman/crossbowman_md_hurta.kf`.
- die: `art/units/crossbowman/crossbowman_md_diea.kf`.
- Note: Missing texture: Arrow_Bolt.dds

### military:cruiser

- Model: `src/Mods/Military/art/models/cruiser.glb` (384,648 bytes).
- Art: `ART_DEF_UNIT_BROOKLYN`. Source scale: 0.6.
- NIF: `art/units/cruiser/brooklyn.nif`.
- KFM: `art/units/cruiser/brooklyn.kfm`.
- Geometry: 894 triangles, 2 skins, 7 joint bindings, 2 textures.
- Bounds: height 0.1921, radius 0.4800, feet Y=0.
- Clips: idle (30 channels), run (30 channels), fortify (30 channels), attack (30 channels), hurt (30 channels).
- SHA-256: `f6352f380213a26f0510dfff90a5fedb77010cfd0709bd060778e29b14c79c65`.
- idle: `art/units/cruiser/brooklyn_md_idle.kf`.
- run: `art/units/cruiser/brooklyn_md_run.kf`.
- fortify: `art/units/cruiser/brooklyn_md_rangedfortify.kf`.
- attack: `art/units/cruiser/brooklyn_md_rangedstrike.kf`.
- hurt: `art/units/cruiser/brooklyn_md_hurta.kf`.

### military:cuirassier

- Model: `src/Mods/Military/art/models/cuirassier.glb` (646,376 bytes).
- Art: `ART_DEF_UNIT_CUIRASSIER`. Source scale: 0.42.
- NIF: `art/units/cuirassier/cuirassier.nif`.
- KFM: `art/units/cuirassier/cuirassier.kfm`.
- Geometry: 1344 triangles, 2 skins, 46 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3025, feet Y=0.
- Clips: idle (143 channels), run (143 channels), fortify (143 channels), attack (143 channels), die (143 channels).
- SHA-256: `6491e3226a3faeb3c22807a9fa910956fbad5a4ba2b8e5cc3954b73c6186a6fd`.
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

- Model: `src/Mods/Military/art/models/cyborg.glb` (303,416 bytes).
- Art: `ART_DEF_UNIT_CYBORG`. Source scale: 0.44.
- NIF: `art/units/nextwar_cyborg/nextwar_cyborg.nif`.
- KFM: `art/units/nextwar_cyborg/nextwar_cyborg.kfm`.
- Geometry: 828 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3730, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), die (51 channels).
- SHA-256: `765e2d9f4ee42f67c4264d3d78796a1dd6af18786b94d9a28982fc2f0b17f7e6`.
- idle: `art/units/nextwar_cyborg/nextwar_cyborg_md_idle.kf`.
- run: `art/units/nextwar_cyborg/nextwar_cyborg_md_run.kf`.
- fortify: `art/units/nextwar_cyborg/nextwar_cyborg_md_rangedfortify.kf`.
- attack: `art/units/nextwar_cyborg/nextwar_cyborg_md_rangedstrike.kf`.
- die: `art/units/nextwar_cyborg/nextwar_cyborg_md_rangeddiea.kf`.

### military:czech_hussite

- Model: `src/Mods/Military/art/models/czech_hussite.glb` (532,440 bytes).
- Art: `ART_DEF_UNIT_CZECH_HUSSITE`. Source scale: 0.44.
- NIF: `art/czech/hussite/imperialmusket.nif`.
- KFM: `art/units/musketman/musketman.kfm`.
- Geometry: 955 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2786, feet Y=0.
- Clips: idle (78 channels), run (78 channels), fortify (78 channels), attack (78 channels), hurt (78 channels), die (78 channels).
- SHA-256: `4b6d1b07f7171331022e2c9d9b64e6909bb0b8545ba21f12e5786950779f366f`.
- idle: `art/units/musketman/musketman_md_idle.kf`.
- run: `art/units/musketman/musketman_md_run.kf`.
- fortify: `art/units/musketman/musketman_md_rangedfortify.kf`.
- attack: `art/units/musketman/musketman_md_rangedstrike.kf`.
- hurt: `art/units/musketman/musketman_md_hurta.kf`.
- die: `art/units/musketman/musketman_md_rangeddiea.kf`.
- Note: Hidden geometry omitted: Editable Mesh

### military:danish_workboat

- Model: `src/Mods/Military/art/models/danish_workboat.glb` (209,272 bytes).
- Art: `ART_DEF_UNIT_DANISH_WORKBOAT`. Source scale: 0.19.
- NIF: `art/units/workboat_ancient/workboat_ancient.nif`.
- KFM: `art/units/workboat_ancient/workboat_ancient.kfm`.
- Geometry: 417 triangles, 3 skins, 10 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3534, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), die (15 channels).
- SHA-256: `24222b2efb746e8b19d35000fe9e65ea0881deebb307282aec8294a3033298cf`.
- idle: `art/units/workboat_ancient/workboat_ancient_md_idle.kf`.
- run: `art/units/workboat_ancient/workboat_ancient_md_run.kf`.
- fortify: `art/units/workboat_ancient/workboat_ancient_md_fortifya.kf`.
- die: `art/units/workboat_ancient/workboat_ancient_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:destroyer

- Model: `src/Mods/Military/art/models/destroyer.glb` (354,580 bytes).
- Art: `ART_DEF_UNIT_DESTROYER`. Source scale: 0.7.
- NIF: `art/units/destroyer/destroyer.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 517 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.2892, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `285853b6d127e50c7269d7c6b88c2b17e7ebe920753b3ae61001f05676b92977`.
- idle: `art/units/destroyer/destroyer_md_idle.kf`.
- run: `art/units/destroyer/destroyer_md_run.kf`.
- fortify: `art/units/destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/destroyer/destroyer_md_hurta.kf`.
- die: `art/units/destroyer/destroyer_md_diea.kf`.

### military:direwolf

- Model: `src/Mods/Military/art/models/direwolf.glb` (346,820 bytes).
- Art: `ART_DEF_UNIT_DIREWOLF`. Source scale: 0.55.
- NIF: `art/units/animals/direwolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5084, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `1e4ad0a20ef70611820ca63758e52ccce5590c98f4590c70b2f31a256b84f35a`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:dragoon

- Model: `src/Mods/Military/art/models/dragoon.glb` (595,164 bytes).
- Art: `ART_DEF_UNIT_DRAGOON`. Source scale: 0.42.
- NIF: `art/units/dragoon/british/light_dragoon.nif`.
- KFM: `art/units/dragoon/british/light_dragoon.kfm`.
- Geometry: 1200 triangles, 5 skins, 40 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2712, feet Y=0.
- Clips: idle (102 channels), run (104 channels), fortify (104 channels), attack (104 channels), hurt (104 channels), die (104 channels).
- SHA-256: `7b145c1f717709df8d046215759da4fbf9882cc001d77330119e281506e0b144`.
- idle: `art/units/dragoon/british/mounted_swordsman_mongol_md_idle.kf`.
- run: `art/units/dragoon/british/mounted_swordsman_mongol_md_run.kf`.
- fortify: `art/units/dragoon/british/mounted_swordsman_mongol_md_fortifya.kf`.
- attack: `art/units/dragoon/british/mounted_swordsman_mongol_md_strikea.kf`.
- hurt: `art/units/dragoon/british/mounted_swordsman_mongol_md_hurta.kf`.
- die: `art/units/dragoon/british/mounted_swordsman_mongol_md_diea.kf`.

### military:dreadnought

- Model: `src/Mods/Military/art/models/dreadnought.glb` (351,648 bytes).
- Art: `ART_DEF_UNIT_DREADNOUGHT`. Source scale: 0.75.
- NIF: `art/units/dreadnought/dreadnought.nif`.
- KFM: `art/units/dreadnought/dreadnought.kfm`.
- Geometry: 397 triangles, 2 skins, 13 joint bindings, 1 textures.
- Bounds: height 0.2642, radius 0.4800, feet Y=0.
- Clips: idle (93 channels), run (93 channels), fortify (93 channels), attack (93 channels).
- SHA-256: `354a827f49da1d8f3668daff7139ca5813bebaf77e0e9c8db8ed76cfb546f253`.
- idle: `art/units/dreadnought/dreadnought_md_idle.kf`.
- run: `art/units/dreadnought/dreadnought_md_run.kf`.
- fortify: `art/units/dreadnought/dreadnought_md_rangedfortify.kf`.
- attack: `art/units/dreadnought/dreadnought_md_rangedstrikea.kf`.

### military:dreadnought_helgoland

- Model: `src/Mods/Military/art/models/dreadnought_helgoland.glb` (500,808 bytes).
- Art: `ART_DEF_UNIT_DREADNOUGHT_SHIP`. Source scale: 0.6.
- NIF: `art/units/dreadnought/european/helgoland.nif`.
- KFM: `art/units/dreadnought/european/helgoland.kfm`.
- Geometry: 1996 triangles, 3 skins, 7 joint bindings, 3 textures.
- Bounds: height 0.3012, radius 0.4800, feet Y=0.
- Clips: idle (27 channels), run (27 channels), fortify (27 channels), attack (27 channels), hurt (27 channels).
- SHA-256: `47ea441f162f329ae1103c2af73b262ed847d75ad7ca5574b3a52f21b484dcc6`.
- idle: `art/units/dreadnought/european/helgoland_md_idle.kf`.
- run: `art/units/dreadnought/european/helgoland_md_run.kf`.
- fortify: `art/units/dreadnought/european/helgoland_md_rangedfortify.kf`.
- attack: `art/units/dreadnought/european/helgoland_md_rangedstrike.kf`.
- hurt: `art/units/dreadnought/european/helgoland_md_hurta.kf`.

### military:drekar

- Model: `src/Mods/Military/art/models/drekar.glb` (465,124 bytes).
- Art: `ART_DEF_UNIT_DREKAR_SCANDINAVIAN`. Source scale: 0.1.
- NIF: `art/units/drekar/galley_fx.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 514 triangles, 0 skins, 0 joint bindings, 4 textures.
- Bounds: height 0.5538, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `e481aaecde2e04f9729c16e1bee7a35678765c279c6c7441e3853ae69b229749`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:dropship

- Model: `src/Mods/Military/art/models/dropship.glb` (219,304 bytes).
- Art: `ART_DEF_UNIT_DROPSHIP`. Source scale: 0.36.
- NIF: `art/units/dropship/isscv.nif`.
- KFM: `art/units/dropship/gunship.kfm`.
- Geometry: 1168 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2084, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels).
- SHA-256: `1c9afe6fe898b799b1cffc73226a410cc3a1cffaa16bb76b206a455d62161797`.
- idle: `art/units/dropship/gunship_md_idle.kf`.
- run: `art/units/dropship/gunship_md_run.kf`.
- fortify: `art/units/dropship/gunship_md_rangedfortify.kf`.
- attack: `art/units/dropship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/dropship/gunship_md_hurta.kf`.

### military:early_tank

- Model: `src/Mods/Military/art/models/early_tank.glb` (160,548 bytes).
- Art: `ART_DEF_UNIT_MARK_V_TANK`. Source scale: 0.48.
- NIF: `art/units/scenario/mark_v_tank/markvtank.nif`.
- KFM: `art/units/scenario/mark_v_tank/markvtank.kfm`.
- Geometry: 530 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.3122, radius 0.4800, feet Y=0.
- Clips: idle (10 channels), run (10 channels), fortify (10 channels), attack (10 channels), hurt (10 channels).
- SHA-256: `bed4f0d67b9daa55e5136ff6d1d9a04803a23a63691031e4e875e156c1a14917`.
- idle: `art/units/scenario/mark_v_tank/markvtank_md_idle.kf`.
- run: `art/units/scenario/mark_v_tank/markvtank_md_run.kf`.
- fortify: `art/units/scenario/mark_v_tank/markvtank_md_rangedfortify.kf`.
- attack: `art/units/scenario/mark_v_tank/markvtank_md_rangedstrike.kf`.
- hurt: `art/units/scenario/mark_v_tank/markvtank_md_hurta.kf`.

### military:egypt_myth_missionary

- Model: `src/Mods/Military/art/models/egypt_myth_missionary.glb` (402,840 bytes).
- Art: `ART_DEF_UNIT_RA_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/scribe/hindu_missionary.nif`.
- KFM: `art/units/missionary/scribe/hindu_missionary.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3339, feet Y=0.
- Clips: idle (98 channels), run (98 channels), fortify (98 channels), die (98 channels).
- SHA-256: `e78c98b735bcfe60c3173d80bc0ac25ddea7aff8184a3ae1f135a7a330aef965`.
- idle: `art/units/missionary/scribe/hindu_missionary_md_idle.kf`.
- run: `art/units/missionary/scribe/hindu_missionary_md_run.kf`.
- fortify: `art/units/missionary/scribe/hindu_missionary_md_fortify.kf`.
- die: `art/units/missionary/scribe/hindu_missionary_md_idle_die.kf`.

### military:egypt_warchariot

- Model: `src/Mods/Military/art/models/egypt_warchariot.glb` (1,166,424 bytes).
- Art: `ART_DEF_UNIT_EGYPT_WARCHARIOT`. Source scale: 0.37.
- NIF: `art/units/unique/egypt/warchariot/egyptianwarchariot.nif`.
- KFM: `art/units/unique/egypt/warchariot/egyptianwarchariot.kfm`.
- Geometry: 2412 triangles, 6 skins, 75 joint bindings, 5 textures.
- Bounds: height 0.3800, radius 0.4800, feet Y=0.
- Clips: idle (214 channels), run (214 channels), fortify (214 channels), attack (214 channels), hurt (214 channels), die (214 channels).
- SHA-256: `cd29ecaecc0ff24e131f42fbb1d893e770583efe3cf8b6bec266511824d4f1e1`.
- idle: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_idle.kf`.
- run: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_run.kf`.
- fortify: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_fortifya.kf`.
- attack: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_strikea.kf`.
- hurt: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_hurta.kf`.
- die: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_diea.kf`.

### military:elephant

- Model: `src/Mods/Military/art/models/elephant.glb` (583,240 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/elephant/elephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 630 triangles, 1 skins, 24 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4359, feet Y=0.
- Clips: idle (101 channels), run (101 channels), fortify (101 channels), attack (101 channels), hurt (101 channels), die (101 channels).
- SHA-256: `44a168fb23584d995a32898bd61e2af6bd6275f18e9d0c9130baf8eab79b8e1f`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: Bone

### military:elephant_bombard

- Model: `src/Mods/Military/art/models/elephant_bombard.glb` (1,510,796 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_BOMBARD`. Source scale: 0.32.
- NIF: `art/units/elephant bombard/siegeelephant.nif`.
- KFM: `art/units/elephant bombard/siegeelephant.kfm`.
- Geometry: 1973 triangles, 5 skins, 47 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (271 channels), run (271 channels), fortify (271 channels), attack (271 channels), hurt (271 channels), die (271 channels).
- SHA-256: `60db55d4c48b995efc93e59282b9bc5962b1a3653c0d7d33631b0c96899c56e6`.
- idle: `art/units/elephant bombard/warelephant_md_idle.kf`.
- run: `art/units/elephant bombard/warelephant_md_run.kf`.
- fortify: `art/units/elephant bombard/warelephant_md_fortifya.kf`.
- attack: `art/units/elephant bombard/warelephant_md_strikea.kf`.
- hurt: `art/units/elephant bombard/warelephant_md_hurta.kf`.
- die: `art/units/elephant bombard/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: HeadPeice:0
- Note: Hidden geometry omitted: WarElephant-Carriage:1

### military:elephant_cavalry

- Model: `src/Mods/Military/art/models/elephant_cavalry.glb` (1,295,972 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_CAVALRY`. Source scale: 0.32.
- NIF: `art/units/elephant cavalry/siegeelephant.nif`.
- KFM: `art/units/elephant cavalry/siegeelephant.kfm`.
- Geometry: 1718 triangles, 3 skins, 45 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.2644, feet Y=0.
- Clips: idle (231 channels), run (231 channels), fortify (231 channels), attack (231 channels), hurt (231 channels), die (231 channels).
- SHA-256: `5d23166d1a8a410e1a7005fdb25185ffe51ff6e0019f0bf01459f2bea8e9d915`.
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

- Model: `src/Mods/Military/art/models/elephant_rider.glb` (857,312 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_RIDER`. Source scale: 0.32.
- NIF: `art/units/warelephant/warelephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 1454 triangles, 3 skins, 47 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2463, feet Y=0.
- Clips: idle (140 channels), run (140 channels), fortify (140 channels), attack (140 channels), hurt (140 channels), die (140 channels).
- SHA-256: `784dff99ef82d64aae2b2cdf069c7245f3a0ded5f76647f6480e0ed7c3fd617a`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.

### military:emp_infantry

- Model: `src/Mods/Military/art/models/emp_infantry.glb` (528,696 bytes).
- Art: `ART_DEF_UNIT_EMP_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/emp_infantry2/marines.nif`.
- KFM: `art/units/emp_infantry2/animations/marines.kfm`.
- Geometry: 2262 triangles, 1 skins, 16 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3790, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `75217e6e4ca7b3bb10e6bd7a79b6add5232dfdcfc76277e5ac24d870d0f5689e`.
- idle: `art/units/emp_infantry2/animations/marines_md_idle.kf`.
- run: `art/units/emp_infantry2/animations/marines_md_run.kf`.
- fortify: `art/units/emp_infantry2/animations/marines_md_rangedfortify.kf`.
- attack: `art/units/emp_infantry2/animations/marines_md_rangedstrike.kf`.
- hurt: `art/units/emp_infantry2/animations/marines_md_hurta.kf`.
- die: `art/units/emp_infantry2/animations/marines_md_rangeddiea.kf`.

### military:engineer

- Model: `src/Mods/Military/art/models/engineer.glb` (305,880 bytes).
- Art: `ART_DEF_UNIT_ENGINEER`. Source scale: 0.46.
- NIF: `art/units/greatpeople/engineer/engineer.nif`.
- KFM: `art/units/greatpeople/engineer/engineer.kfm`.
- Geometry: 706 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3785, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels).
- SHA-256: `ed152b19bd7350ccd73b627158591763c270955df06613ae88b136f10cd0fd63`.
- idle: `art/units/greatpeople/engineer/engineer_md_idle.kf`.
- run: `art/units/greatpeople/engineer/engineer_md_run.kf`.
- fortify: `art/units/greatpeople/engineer/engineer_md_fortify.kf`.

### military:english_redcoat

- Model: `src/Mods/Military/art/models/english_redcoat.glb` (365,492 bytes).
- Art: `ART_DEF_UNIT_ENGLISH_REDCOAT`. Source scale: 0.44.
- NIF: `art/units/unique/england/redcoat/redcoat.nif`.
- KFM: `art/units/unique/england/redcoat/redcoat.kfm`.
- Geometry: 772 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2149, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `250a8c234a895b0e184be787110461393870ea4760ce27987b88dd6b838b2cd5`.
- idle: `art/units/unique/england/redcoat/redcoat_md_idle.kf`.
- run: `art/units/unique/england/redcoat/redcoat_md_run.kf`.
- fortify: `art/units/unique/england/redcoat/redcoat_md_rangedfortify.kf`.
- attack: `art/units/unique/england/redcoat/redcoat_md_rangedstrike.kf`.
- hurt: `art/units/unique/england/redcoat/redcoat_md_hurta.kf`.
- die: `art/units/unique/england/redcoat/redcoat_md_rangeddiea.kf`.

### military:envoy

- Model: `src/Mods/Military/art/models/envoy.glb` (309,976 bytes).
- Art: `ART_DEF_UNIT_ENVOY`. Source scale: 0.46.
- NIF: `art/units/envoy/european/master_tobacconist.nif`.
- KFM: `art/units/envoy/european/master_tobacconist.kfm`.
- Geometry: 708 triangles, 1 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2843, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `cc8ff1f99f989af6476649f1a9bd1b4ab247ee6ce450a49f739d993194034a45`.
- idle: `art/units/envoy/european/master_tobacconist_md_idle.kf`.
- run: `art/units/envoy/european/master_tobacconist_md_run.kf`.
- fortify: `art/units/envoy/european/master_tobacconist_md_fortifya.kf`.
- attack: `art/units/envoy/european/master_tobacconist_md_strikea.kf`.
- hurt: `art/units/envoy/european/master_tobacconist_md_hurta.kf`.
- die: `art/units/envoy/european/master_tobacconist_md_diea.kf`.

### military:ethiopian_oromo_warrior

- Model: `src/Mods/Military/art/models/ethiopian_oromo_warrior.glb` (330,964 bytes).
- Art: `ART_DEF_UNIT_ETHIOPIAN_OROMO_WARRIOR_ORANGE`. Source scale: 0.44.
- NIF: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_orange.nif`.
- KFM: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior.kfm`.
- Geometry: 834 triangles, 2 skins, 22 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2538, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), die (53 channels).
- SHA-256: `9aad68850897e4a61951f2c1de48e7b7018a111b6f4c4fefd649980b9b52a98d`.
- idle: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_idle.kf`.
- run: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_run.kf`.
- fortify: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangedfortify.kf`.
- attack: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangedstrike.kf`.
- die: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangeddiea.kf`.
- Note: First of 3 formation art groups exported

### military:executive_1

- Model: `src/Mods/Military/art/models/executive_1.glb` (259,240 bytes).
- Art: `ART_DEF_UNIT_CEO_1`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_cereal.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `8816b05d7213ee9b98f2bf452b73f9b004422c65a86d85dcffff3c1eaf71b405`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_2

- Model: `src/Mods/Military/art/models/executive_2.glb` (260,028 bytes).
- Art: `ART_DEF_UNIT_CEO_2`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_sushi.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `ec32b227475cdf8c85f99b01ac7ee4d6186558f2e1d8a69f45ac37facdae25ae`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_3

- Model: `src/Mods/Military/art/models/executive_3.glb` (259,104 bytes).
- Art: `ART_DEF_UNIT_CEO_3`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_ethanol.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `2cc92aed603837797046eba8d18c74bf8015ca8334097b0e90da93c1b6e04978`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_4

- Model: `src/Mods/Military/art/models/executive_4.glb` (259,136 bytes).
- Art: `ART_DEF_UNIT_CEO_4`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_construction.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `89ed5bce3fa2d04456bf39d814f9ebe44597b92892fac5ef8d66452976234443`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_5

- Model: `src/Mods/Military/art/models/executive_5.glb` (258,916 bytes).
- Art: `ART_DEF_UNIT_CEO_5`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_mining.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `78eecc36cd8ee7d31bda1846443aed04e1fac33bc7f045c431b939eba10300a5`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_6

- Model: `src/Mods/Military/art/models/executive_6.glb` (258,944 bytes).
- Art: `ART_DEF_UNIT_CEO_6`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_aluminum.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `f4b4473b21ab05244cf1a92688fc3eb21810928e1ebf3eb0c99f727cfb66de7e`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_7

- Model: `src/Mods/Military/art/models/executive_7.glb` (259,336 bytes).
- Art: `ART_DEF_UNIT_CEO_7`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_jewelry.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `73477713a12dc326390e358923c118f5a7e3ead9268886502ab0a01c28c39aba`.
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
- SHA-256: `7333d167b4605cd8355f426464d56a85c670b2cedd01e23feae0dfe9835ea754`.
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
- SHA-256: `c2cd743cac7fcd851f4eb6b12b3319130b0db796717cb52d4fac6853909e108f`.
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
- SHA-256: `c2cd743cac7fcd851f4eb6b12b3319130b0db796717cb52d4fac6853909e108f`.
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
- SHA-256: `0ab8e48dc9906564f58f9f9612b74726b72ae41d473d1e3d802b58bc6afaa8bd`.
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
- SHA-256: `4d5f2d82a46d49a0edc5a78362d2df5fc63b1a1dc59d5cb48025223390aa16e4`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_burgerworld

- Model: `src/Mods/Military/art/models/executive_burgerworld.glb` (260,212 bytes).
- Art: `ART_DEF_UNIT_CEO_BURGERWORLD`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_bw.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `0c3c19929c48b0a06670841c680f12be50f0d18c50478f8251e69e3c37f58cd3`.
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
- SHA-256: `0ab8e48dc9906564f58f9f9612b74726b72ae41d473d1e3d802b58bc6afaa8bd`.
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
- SHA-256: `4d5f2d82a46d49a0edc5a78362d2df5fc63b1a1dc59d5cb48025223390aa16e4`.
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
- SHA-256: `c2cd743cac7fcd851f4eb6b12b3319130b0db796717cb52d4fac6853909e108f`.
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
- SHA-256: `c2cd743cac7fcd851f4eb6b12b3319130b0db796717cb52d4fac6853909e108f`.
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
- SHA-256: `c97a4038d91a9d412b6658b38ef4308c939d728fd0acc8fa4aa4dec933368c18`.
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
- SHA-256: `c2cd743cac7fcd851f4eb6b12b3319130b0db796717cb52d4fac6853909e108f`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_ewtrade

- Model: `src/Mods/Military/art/models/executive_ewtrade.glb` (259,432 bytes).
- Art: `ART_DEF_UNIT_CEO_EWTRADE`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_ew.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `252f254f415a29bc50b474947f00c7ce18f5fca045579fb435c94a7eea692e8b`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_fashion

- Model: `src/Mods/Military/art/models/executive_fashion.glb` (257,612 bytes).
- Art: `ART_DEF_UNIT_CEO_FASHION`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_fashion.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `65b094de133c0cf5791cb95ae5eba9d4bafc473504308687cdc1b230b5fd775f`.
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
- SHA-256: `0ab8e48dc9906564f58f9f9612b74726b72ae41d473d1e3d802b58bc6afaa8bd`.
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
- SHA-256: `4d5f2d82a46d49a0edc5a78362d2df5fc63b1a1dc59d5cb48025223390aa16e4`.
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
- SHA-256: `4d5f2d82a46d49a0edc5a78362d2df5fc63b1a1dc59d5cb48025223390aa16e4`.
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
- SHA-256: `0ab8e48dc9906564f58f9f9612b74726b72ae41d473d1e3d802b58bc6afaa8bd`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_mallwart

- Model: `src/Mods/Military/art/models/executive_mallwart.glb` (257,348 bytes).
- Art: `ART_DEF_UNIT_CEO_MALLWART`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_mw.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `ee91ed5fb35e5c20a8206846fbee1fe1a2d0ad34dc8272270aff6eee2dd98403`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_mapster

- Model: `src/Mods/Military/art/models/executive_mapster.glb` (257,584 bytes).
- Art: `ART_DEF_UNIT_CEO_MAPSTER`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_marnzmotor.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `954b4e9a014928ec17b13a4da895bbcb07c6df86a045d6b7a19120e7e027d8d5`.
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
- SHA-256: `4d5f2d82a46d49a0edc5a78362d2df5fc63b1a1dc59d5cb48025223390aa16e4`.
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
- SHA-256: `0ab8e48dc9906564f58f9f9612b74726b72ae41d473d1e3d802b58bc6afaa8bd`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_milehigh

- Model: `src/Mods/Military/art/models/executive_milehigh.glb` (259,080 bytes).
- Art: `ART_DEF_UNIT_CEO_MILEHIGH`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_mh.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `2d1d853a82210102456518102aa66c9bec54b610f7a1ae9d731f5180dd3ab1f4`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_mobby

- Model: `src/Mods/Military/art/models/executive_mobby.glb` (260,304 bytes).
- Art: `ART_DEF_UNIT_CEO_MOBBY`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_mobby.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `b3dc683f52652fbe75e80b9270606050befc970f97552a06ad582b7d69088667`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_red

- Model: `src/Mods/Military/art/models/executive_red.glb` (258,564 bytes).
- Art: `ART_DEF_UNIT_CEO_RED`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_rc.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `4569d9545d3234567472b3984a74f0b49a306a7780d75c8fcbffc85babb48cb5`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_safari

- Model: `src/Mods/Military/art/models/executive_safari.glb` (259,536 bytes).
- Art: `ART_DEF_UNIT_CEO_SAFARI`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_safari.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `fa22bf4328a86072e005be560e20baa3fe97fe28bbc1c268943aa590ebe9dfe7`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_ultsoldier

- Model: `src/Mods/Military/art/models/executive_ultsoldier.glb` (259,452 bytes).
- Art: `ART_DEF_UNIT_CEO_ULTSOLDIER`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_soldier.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `33424e4d373c33d5256ecbc6de5afadd5baeccd39e30a24d1776d255831b98a7`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_wonka

- Model: `src/Mods/Military/art/models/executive_wonka.glb` (259,224 bytes).
- Art: `ART_DEF_UNIT_CEO_WONKA`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_wonka.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `031558e3a1dc0a4d7fb16aa1920f7be2144241b28502c3afd8dd1e084657a382`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:experimental

- Model: `src/Mods/Military/art/models/experimental.glb` (59,364 bytes).
- Art: `ART_DEF_UNIT_EXPERIMENTAL`. Source scale: 1.4.
- NIF: `art/units/nukes/peacemaker/icbm.nif`.
- KFM: `art/units/nukes/peacemaker/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `128d91bf22c57710f3f52255846e4bdcfa2f94e9d953832322af3a9c96483db5`.
- idle: `art/units/nukes/peacemaker/icbm_md_idle.kf`.
- run: `art/units/nukes/peacemaker/icbm_md_run.kf`.
- attack: `art/units/nukes/peacemaker/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:explorer

- Model: `src/Mods/Military/art/models/explorer.glb` (303,688 bytes).
- Art: `ART_DEF_UNIT_EXPLORER`. Source scale: 0.44.
- NIF: `art/units/explorer/explorer.nif`.
- KFM: `art/units/explorer/explorer.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2782, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `f6e62c8bf0c33584de380bcbaf14ff0935546e3adc06e6716c7fad1ff6403270`.
- idle: `art/units/explorer/explorer_md_idle.kf`.
- run: `art/units/explorer/explorer_md_run.kf`.
- fortify: `art/units/explorer/explorer_md_fortifya.kf`.
- attack: `art/units/explorer/explorer_md_strikea.kf`.
- hurt: `art/units/explorer/explorer_md_hurta.kf`.
- die: `art/units/explorer/explorer_md_diea.kf`.

### military:f117

- Model: `src/Mods/Military/art/models/f117.glb` (141,524 bytes).
- Art: `ART_DEF_UNIT_F117`. Source scale: 0.42.
- NIF: `art/units/stealth fighter/f117.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 402 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.1187, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), hurt (23 channels).
- SHA-256: `ce7a75317c2d1d2e8d0a9ca7b9eba1d809d66a20de62e7f9bc0729145970e72a`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:f15

- Model: `src/Mods/Military/art/models/f15.glb` (236,888 bytes).
- Art: `ART_DEF_UNIT_STRIKE_FIGHTER_F15`. Source scale: 0.48.
- NIF: `art/units/strike fighter/american/f15.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 1205 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.2219, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), hurt (23 channels).
- SHA-256: `fde5323c3657d4b3acabc072bb54a0b46d99cb61310be6acf0806dd43a1ed5f0`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:feral

- Model: `src/Mods/Military/art/models/feral.glb` (518,016 bytes).
- Art: `ART_DEF_UNIT_CYBORG2`. Source scale: 0.44.
- NIF: `art/units/super soldier/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 1499 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2609, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `255447d932f9c3cb52838bb4cc688db9f4066df6cd500b2d48c134d24256f0c8`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.
- Note: Missing texture: //cca.dds

### military:ff_destroyer_i

- Model: `src/Mods/Military/art/models/ff_destroyer_i.glb` (236,856 bytes).
- Art: `ART_DEF_UNIT_FF_DESTROYER_I`. Source scale: 0.45.
- NIF: `art/units/finalfrontier_destroyer/destroyer.nif`.
- KFM: `art/units/finalfrontier_destroyer/destroyer.kfm`.
- Geometry: 983 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2620, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `daffa159226729005164d29f3c362ed0ab11374a2f75bb0fd58b774924f1e0e0`.
- idle: `art/units/finalfrontier_destroyer/destroyer_md_idle.kf`.
- run: `art/units/finalfrontier_destroyer/destroyer_md_run.kf`.
- fortify: `art/units/finalfrontier_destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/finalfrontier_destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/finalfrontier_destroyer/destroyer_md_hurta.kf`.
- die: `art/units/finalfrontier_destroyer/destroyer_md_diea.kf`.

### military:fighter

- Model: `src/Mods/Military/art/models/fighter.glb` (196,256 bytes).
- Art: `ART_DEF_UNIT_FIGHTER`. Source scale: 0.5.
- NIF: `art/units/fighter/fighter.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 532 triangles, 4 skins, 8 joint bindings, 1 textures.
- Bounds: height 0.3279, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `8fda3062815d906f9163470ae71cc2a8c5486a0d6efb75ee6f909d9ca9e9e403`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:fluyt

- Model: `src/Mods/Military/art/models/fluyt.glb` (341,500 bytes).
- Art: `ART_DEF_UNIT_FLUYT`. Source scale: 0.12.
- NIF: `art/units/galleon/galleon.nif`.
- KFM: `art/units/galleon/galleon.kfm`.
- Geometry: 1023 triangles, 3 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2871, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `3cd455723e55705f35633c0578add21f6690f02543ec443b741b6da2782ea089`.
- idle: `art/units/galleon/galleon_md_idle.kf`.
- run: `art/units/galleon/galleon_md_run.kf`.
- fortify: `art/units/galleon/galleon_md_fortifya.kf`.
- attack: `art/units/galleon/galleon_md_strikea.kf`.
- hurt: `art/units/galleon/galleon_md_hurta.kf`.
- die: `art/units/galleon/galleon_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:fokkerdr1

- Model: `src/Mods/Military/art/models/fokkerdr1.glb` (198,304 bytes).
- Art: `ART_DEF_UNIT_BIPLANE`. Source scale: 0.44.
- NIF: `art/units/earlyfighter/pup.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 1080 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.3789, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `b9f21ae4ec2e9058e1a0299a58f41c4de341d4b3f128dde4dc01686a2272863a`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:freight

- Model: `src/Mods/Military/art/models/freight.glb` (56,728 bytes).
- Art: `ART_DEF_UNIT_FREIGHT`. Source scale: 0.35.
- NIF: `art/units/freight/opel.nif`.
- KFM: `art/units/tank/tank.kfm`.
- Geometry: 314 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.4560, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `1eaf05b349eac190096933edc2963a9bf367a42cea76771e31c91561848a292f`.
- idle: `art/units/tank/tank_md_idle.kf`.
- run: `art/units/tank/tank_md_run.kf`.
- fortify: `art/units/tank/tank_md_rangedfortify.kf`.
- attack: `art/units/tank/tank_md_rangedstrike.kf`.
- hurt: `art/units/tank/tank_md_hurta.kf`.

### military:french_musketeer

- Model: `src/Mods/Military/art/models/french_musketeer.glb` (571,744 bytes).
- Art: `ART_DEF_UNIT_FRENCH_MUSKETEER`. Source scale: 0.44.
- NIF: `art/units/unique/french/musketeer/musketeer.nif`.
- KFM: `art/units/unique/french/musketeer/musketeer.kfm`.
- Geometry: 718 triangles, 2 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3188, feet Y=0.
- Clips: idle (88 channels), run (88 channels), fortify (88 channels), attack (88 channels), hurt (88 channels), die (88 channels).
- SHA-256: `7a4a7a0db10555937669cf14597b4691112872bd77297fdc2d0e6dd4d905c800`.
- idle: `art/units/unique/french/musketeer/musketeer_md_idle.kf`.
- run: `art/units/unique/french/musketeer/musketeer_md_run.kf`.
- fortify: `art/units/unique/french/musketeer/musketeer_md_rangedfortify.kf`.
- attack: `art/units/unique/french/musketeer/musketeer_md_rangedstrike.kf`.
- hurt: `art/units/unique/french/musketeer/musketeer_md_hurta.kf`.
- die: `art/units/unique/french/musketeer/musketeer_md_rangeddiea.kf`.

### military:frigate

- Model: `src/Mods/Military/art/models/frigate.glb` (304,800 bytes).
- Art: `ART_DEF_UNIT_FRIGATE`. Source scale: 0.24.
- NIF: `art/units/frigate/frigate.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1174 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4001, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `e6e56679cb68d6f54d437998942a9af38d7521684bd24f4cbe7df08b8a0c70ad`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:fusion_battleship

- Model: `src/Mods/Military/art/models/fusion_battleship.glb` (292,536 bytes).
- Art: `ART_DEF_UNIT_FF_BATTLESHIP_I`. Source scale: 0.21.
- NIF: `art/units/finalfrontier_battleship/battleship.nif`.
- KFM: `art/units/finalfrontier_battleship/battleship.kfm`.
- Geometry: 1456 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2224, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `f067a9fbc042231e82f6bb99b6c9d42a6dc9ddf1b0c938ea6704bd2168b39dd4`.
- idle: `art/units/finalfrontier_battleship/battleship_md_idle.kf`.
- run: `art/units/finalfrontier_battleship/battleship_md_run.kf`.
- fortify: `art/units/finalfrontier_battleship/battleship_md_fortifya.kf`.
- attack: `art/units/finalfrontier_battleship/battleship_md_strikea.kf`.
- hurt: `art/units/finalfrontier_battleship/battleship_md_hurta.kf`.
- die: `art/units/finalfrontier_battleship/battleship_md_diea.kf`.

### military:fusion_carrier

- Model: `src/Mods/Military/art/models/fusion_carrier.glb` (377,764 bytes).
- Art: `ART_DEF_UNIT_FF_CARRIER_I`. Source scale: 0.21.
- NIF: `art/units/finalfrontier_carrier/carrier.nif`.
- KFM: `art/units/finalfrontier_carrier/carrier.kfm`.
- Geometry: 1874 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.4454, radius 0.4800, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), attack (15 channels), hurt (15 channels), die (15 channels).
- SHA-256: `0f2f443e7f82b60dd26745f513b3b62e5b0a70af4d3294c71db5b658fd4ec543`.
- idle: `art/units/finalfrontier_carrier/carrier_md_idle.kf`.
- run: `art/units/finalfrontier_carrier/carrier_md_run.kf`.
- fortify: `art/units/finalfrontier_carrier/carrier_md_fortifya.kf`.
- attack: `art/units/finalfrontier_carrier/carrier_md_strikea.kf`.
- hurt: `art/units/finalfrontier_carrier/carrier_md_hurta.kf`.
- die: `art/units/finalfrontier_carrier/carrier_md_diea.kf`.

### military:fusion_cruiser

- Model: `src/Mods/Military/art/models/fusion_cruiser.glb` (270,856 bytes).
- Art: `ART_DEF_UNIT_FF_CRUISER_I`. Source scale: 0.25.
- NIF: `art/units/finalfrontier_cruiser/cruiser.nif`.
- KFM: `art/units/finalfrontier_cruiser/cruiser.kfm`.
- Geometry: 1046 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2745, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `c41ede528b98e9d9d127e94c9a45dd94112e2df7c819c9aa8e9aeae96ee16f3a`.
- idle: `art/units/finalfrontier_cruiser/cruiser_md_idle.kf`.
- run: `art/units/finalfrontier_cruiser/cruiser_md_run.kf`.
- fortify: `art/units/finalfrontier_cruiser/cruiser_md_fortifya.kf`.
- attack: `art/units/finalfrontier_cruiser/cruiser_md_strikea.kf`.
- hurt: `art/units/finalfrontier_cruiser/cruiser_md_hurta.kf`.
- die: `art/units/finalfrontier_cruiser/cruiser_md_diea.kf`.

### military:fusion_nova

- Model: `src/Mods/Military/art/models/fusion_nova.glb` (65,464 bytes).
- Art: `ART_DEF_UNIT_FUSION_NOVA`. Source scale: 1.
- NIF: `art/units/nukes/fusionbombs/icbm.nif`.
- KFM: `art/units/nukes/fusionbombs/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `3eaae0f248f7991f35992bf17c6d796b441025cfb1668ef455e4d6a444509971`.
- idle: `art/units/nukes/fusionbombs/icbm_md_idle.kf`.
- run: `art/units/nukes/fusionbombs/icbm_md_run.kf`.
- attack: `art/units/nukes/fusionbombs/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:fusion_nuke

- Model: `src/Mods/Military/art/models/fusion_nuke.glb` (65,464 bytes).
- Art: `ART_DEF_UNIT_FUSION_NUKE`. Source scale: 0.62.
- NIF: `art/units/nukes/fusionbombs/icbm.nif`.
- KFM: `art/units/nukes/fusionbombs/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `3eaae0f248f7991f35992bf17c6d796b441025cfb1668ef455e4d6a444509971`.
- idle: `art/units/nukes/fusionbombs/icbm_md_idle.kf`.
- run: `art/units/nukes/fusionbombs/icbm_md_run.kf`.
- attack: `art/units/nukes/fusionbombs/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:fusion_submarine

- Model: `src/Mods/Military/art/models/fusion_submarine.glb` (231,868 bytes).
- Art: `ART_DEF_UNIT_FUSION_SUBMARINE`. Source scale: 0.60.
- NIF: `art/units/fusion submarine/submarine.nif`.
- KFM: `art/units/fusion submarine/submarine.kfm`.
- Geometry: 568 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.1884, radius 0.4800, feet Y=0.
- Clips: idle (39 channels), run (39 channels), fortify (39 channels), attack (39 channels), hurt (39 channels), die (39 channels).
- SHA-256: `45e2426d1977981465ec2c48e68ae186f6ee4dbfb9e39b408556386dcf8f5662`.
- idle: `art/units/fusion submarine/submarine_md_idle.kf`.
- run: `art/units/fusion submarine/submarine_md_run.kf`.
- fortify: `art/units/fusion submarine/submarine_md_fortifya.kf`.
- attack: `art/units/fusion submarine/submarine_md_strikea.kf`.
- hurt: `art/units/fusion submarine/submarine_md_hurta.kf`.
- die: `art/units/fusion submarine/submarine_md_diea.kf`.

### military:fusion_transport

- Model: `src/Mods/Military/art/models/fusion_transport.glb` (193,664 bytes).
- Art: `ART_DEF_UNIT_FF_INVASION_SHIP_I`. Source scale: 0.5.
- NIF: `art/units/finalfrontier_invasionship/invasionship.nif`.
- KFM: `art/units/finalfrontier_invasionship/invasionship.kfm`.
- Geometry: 641 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2336, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `aeb5f5d86150a7f27d1286e6b669a9a08e12acd47621168dc251cad1d9233087`.
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

- Model: `src/Mods/Military/art/models/galleas.glb` (657,436 bytes).
- Art: `ART_DEF_UNIT_GALLEAS`. Source scale: 0.10.
- NIF: `art/venice/galleas/galley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 1444 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3762, feet Y=0.
- Clips: idle (134 channels), run (134 channels), fortify (134 channels), attack (134 channels), hurt (134 channels), die (134 channels).
- SHA-256: `9f739cb822885e5315e54338fdf6cdbd5a8fe14eab40b8d1ce56ea012835116b`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.
- Note: Missing texture: //frigate.dds

### military:galleass

- Model: `src/Mods/Military/art/models/galleass.glb` (602,212 bytes).
- Art: `ART_DEF_UNIT_GALLEASS`. Source scale: 0.14.
- NIF: `art/units/galleas/galley.nif`.
- KFM: `art/units/galleas/galley.kfm`.
- Geometry: 1444 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3762, feet Y=0.
- Clips: idle (131 channels), run (131 channels), fortify (131 channels), attack (65 channels), hurt (131 channels), die (131 channels).
- SHA-256: `a75ea731f297d9d3420485253844214558dd6b28b08ce21a36c2956d35bf2a3c`.
- idle: `art/units/galleas/galley_md_idle.kf`.
- run: `art/units/galleas/galley_md_run.kf`.
- fortify: `art/units/galleas/galley_md_fortifya.kf`.
- attack: `art/units/galleas/galley_md_strikea.kf`.
- hurt: `art/units/galleas/galley_md_hurta.kf`.
- die: `art/units/galleas/galley_md_diea.kf`.
- Note: Missing texture: \\frigate.dds

### military:galleon

- Model: `src/Mods/Military/art/models/galleon.glb` (341,500 bytes).
- Art: `ART_DEF_UNIT_GALLEON`. Source scale: 0.2.
- NIF: `art/units/galleon/galleon.nif`.
- KFM: `art/units/galleon/galleon.kfm`.
- Geometry: 1023 triangles, 3 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2871, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `3cd455723e55705f35633c0578add21f6690f02543ec443b741b6da2782ea089`.
- idle: `art/units/galleon/galleon_md_idle.kf`.
- run: `art/units/galleon/galleon_md_run.kf`.
- fortify: `art/units/galleon/galleon_md_fortifya.kf`.
- attack: `art/units/galleon/galleon_md_strikea.kf`.
- hurt: `art/units/galleon/galleon_md_hurta.kf`.
- die: `art/units/galleon/galleon_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:galley

- Model: `src/Mods/Military/art/models/galley.glb` (270,752 bytes).
- Art: `ART_DEF_UNIT_GALLEY`. Source scale: 0.18.
- NIF: `art/units/galley/galley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 897 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4308, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `386e511b61089f89d890a28ebe33fa78fe9721b90e931034087afb2a8c2c5296`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:gatling_gun

- Model: `src/Mods/Military/art/models/gatling_gun.glb` (481,020 bytes).
- Art: `ART_DEF_UNIT_GATLING_GUN`. Source scale: 0.44.
- NIF: `art/units/gatlinggun/european/machinegunner.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 1282 triangles, 7 skins, 36 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4592, feet Y=0.
- Clips: idle (111 channels), run (111 channels), fortify (111 channels), attack (111 channels), hurt (111 channels).
- SHA-256: `2825e60521523fc354d3fb57dac2093d081970c6ae6b89ce68c1b67521c85c54`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:german_panzer

- Model: `src/Mods/Military/art/models/german_panzer.glb` (293,444 bytes).
- Art: `ART_DEF_UNIT_GERMAN_PANZER`. Source scale: 0.47.
- NIF: `art/units/unique/german/panzer/panzer.nif`.
- KFM: `art/units/unique/german/panzer/panzer.kfm`.
- Geometry: 526 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.4921, radius 0.4800, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels).
- SHA-256: `234b808da72a7f4b76997fbd29f38dbe351688ab0f1afb64e5442fbf5db93900`.
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
- SHA-256: `0ab8e48dc9906564f58f9f9612b74726b72ae41d473d1e3d802b58bc6afaa8bd`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:gothic_cavalry

- Model: `src/Mods/Military/art/models/gothic_cavalry.glb` (996,308 bytes).
- Art: `ART_DEF_UNIT_GOTHIC_CAVALRY`. Source scale: 0.43.
- NIF: `art/goths/gothiccavalry/mongelkeshik.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1505 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3649, feet Y=0.
- Clips: idle (181 channels), run (181 channels), fortify (181 channels), attack (181 channels), hurt (181 channels), die (181 channels).
- SHA-256: `7265cf1f7dffb2ba9551a1328a33c3ba2f93e9e801da4dead935b43b188720cc`.
- idle: `art/units/unique/mongol/keshik/mongelkeshik_md_idle.kf`.
- run: `art/units/unique/mongol/keshik/mongelkeshik_md_run.kf`.
- fortify: `art/units/unique/mongol/keshik/mongelkeshik_md_fortifya.kf`.
- attack: `art/units/unique/mongol/keshik/mongelkeshik_md_strikea.kf`.
- hurt: `art/units/unique/mongol/keshik/mongelkeshik_md_hurta.kf`.
- die: `art/units/unique/mongol/keshik/mongelkeshik_md_diea.kf`.
- Note: Missing texture: //bb2.dds

### military:great_diplomat

- Model: `src/Mods/Military/art/models/great_diplomat.glb` (287,460 bytes).
- Art: `ART_DEF_UNIT_GREAT_DIPLOMAT`. Source scale: 0.46.
- NIF: `art/units/great diplomat/engineer.nif`.
- KFM: `art/units/great diplomat/engineer.kfm`.
- Geometry: 658 triangles, 1 skins, 16 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3258, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels).
- SHA-256: `5eea2d627cba03edfdbd17fe721e4fe5ef9f3941ccc58c423e33fa67ede44539`.
- idle: `art/units/great diplomat/engineer_md_idle.kf`.
- run: `art/units/great diplomat/engineer_md_run.kf`.
- fortify: `art/units/great diplomat/engineer_md_fortify.kf`.

### military:great_general

- Model: `src/Mods/Military/art/models/great_general.glb` (368,220 bytes).
- Art: `ART_DEF_UNIT_WARLORD_ANCIENT`. Source scale: 0.5.
- NIF: `art/units/warlord_ancient/warlord_ancient.nif`.
- KFM: `art/units/warlord_ancient/warlord_ancient.kfm`.
- Geometry: 806 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.5845, radius 0.4800, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels).
- SHA-256: `4e157db179e373a0ea00d52f4509ba3ef046ae6f04a2d9339e08cf6f4e4fd791`.
- idle: `art/units/warlord_ancient/warlord_ancient_md_idle.kf`.
- run: `art/units/warlord_ancient/warlord_ancient_md_run.kf`.
- fortify: `art/units/warlord_ancient/warlord_ancient_md_fortify.kf`.

### military:great_spy

- Model: `src/Mods/Military/art/models/great_spy.glb` (445,452 bytes).
- Art: `ART_DEF_UNIT_ANCIENT_GREAT_SPY`. Source scale: 0.46.
- NIF: `art/units/great_spy_ancient/great_spy_ancient.nif`.
- KFM: `art/units/great_spy_ancient/great_spy_ancient.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3743, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels).
- SHA-256: `75417278f90087ccb80a8f76d1f7fa03d14b65ea2094d5ad0277b6c195ba0cd7`.
- idle: `art/units/great_spy_ancient/great_spy_ancient_md_idle.kf`.
- run: `art/units/great_spy_ancient/great_spy_ancient_md_run.kf`.
- fortify: `art/units/great_spy_ancient/great_spy_ancient_md_fortify.kf`.
- attack: `art/units/great_spy_ancient/great_spy_ancient_md_rangedstrike.kf`.

### military:greek_phalanx

- Model: `src/Mods/Military/art/models/greek_phalanx.glb` (312,628 bytes).
- Art: `ART_DEF_UNIT_GREEK_PHALANX`. Source scale: 0.44.
- NIF: `art/units/spearman/spearman.nif`.
- KFM: `art/units/spearman/spearman.kfm`.
- Geometry: 708 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2854, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `b98cf6677b37ed180a48f81e8a1ce7932bf8e0912bf45edd495feb6fec7c310e`.
- idle: `art/units/spearman/spearman_md_idle.kf`.
- run: `art/units/spearman/spearman_md_run.kf`.
- fortify: `art/units/spearman/spearman_md_fortifya.kf`.
- attack: `art/units/spearman/spearman_md_strikea.kf`.
- hurt: `art/units/spearman/spearman_md_hurta.kf`.
- die: `art/units/spearman/spearman_md_diea.kf`.

### military:grenadier

- Model: `src/Mods/Military/art/models/grenadier.glb` (420,388 bytes).
- Art: `ART_DEF_UNIT_GRENADIER`. Source scale: 0.44.
- NIF: `art/units/grenadier/grenadier.nif`.
- KFM: `art/units/grenadier/grenadier.kfm`.
- Geometry: 749 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2608, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `ad15b0da9f689c4b4e9e2462e3782374da8beb8e0d3859f66bb3caf0cac88aca`.
- idle: `art/units/grenadier/grenadier_md_idle.kf`.
- run: `art/units/grenadier/grenadier_md_run.kf`.
- fortify: `art/units/grenadier/grenadier_md_fortifya.kf`.
- attack: `art/units/grenadier/grenadier_md_strikea.kf`.
- hurt: `art/units/grenadier/grenadier_md_hurta.kf`.
- die: `art/units/grenadier/grenadier_md_diea.kf`.

### military:guided_missile

- Model: `src/Mods/Military/art/models/guided_missile.glb` (79,372 bytes).
- Art: `ART_DEF_UNIT_GUIDED_MISSILE`. Source scale: 0.2.
- NIF: `art/units/guided_missile/guided_missile.nif`.
- KFM: `art/units/guided_missile/guided_missile.kfm`.
- Geometry: 146 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.1889, radius 0.4800, feet Y=0.
- Clips: idle (19 channels), run (19 channels), attack (19 channels).
- SHA-256: `7f2d6ae2e48ef27292ada96147ce8032b6877522136ea701ea5af9fa24887af8`.
- idle: `art/units/guided_missile/guided_missile_md_idle.kf`.
- run: `art/units/guided_missile/guided_missile_md_run.kf`.
- attack: `art/units/guided_missile/guided_missile_md_strikea.kf`.

### military:gunship

- Model: `src/Mods/Military/art/models/gunship.glb` (288,588 bytes).
- Art: `ART_DEF_UNIT_GUNSHIP`. Source scale: 0.36.
- NIF: `art/units/gunship/gunship.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 1174 triangles, 2 skins, 7 joint bindings, 2 textures.
- Bounds: height 0.2070, radius 0.4800, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels), attack (43 channels), hurt (43 channels).
- SHA-256: `b1f804ece8082a4d22d370d50bc56145b704190f7d5ad684eea3a2fd058c6ab0`.
- idle: `art/units/gunship/gunship_md_idle.kf`.
- run: `art/units/gunship/gunship_md_run.kf`.
- fortify: `art/units/gunship/gunship_md_rangedfortify.kf`.
- attack: `art/units/gunship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/gunship/gunship_md_hurta.kf`.

### military:hacker

- Model: `src/Mods/Military/art/models/hacker.glb` (177,868 bytes).
- Art: `ART_DEF_UNIT_HACKER`. Source scale: 0.46.
- NIF: `art/units/hacker/modernscientist.nif`.
- KFM: `art/units/hacker/modernscientist.kfm`.
- Geometry: 568 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2621, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels).
- SHA-256: `c5997a6d8d4f4a8b368f0e8f6588102fe604534a8ea50978c70551e739be70bb`.
- idle: `art/units/hacker/modernscientist_md_idle.kf`.
- run: `art/units/greatpeople/modernscientist/modernscientist_md_run.kf`.
- fortify: `art/units/hacker/modernscientist_md_fortify.kf`.

### military:halftrack

- Model: `src/Mods/Military/art/models/halftrack.glb` (392,568 bytes).
- Art: `ART_DEF_UNIT_HALFTRACK`. Source scale: 0.6.
- NIF: `art/units/antiair halftrack/ma_ht.nif`.
- KFM: `art/units/antiair halftrack/modernarmor.kfm`.
- Geometry: 1152 triangles, 2 skins, 6 joint bindings, 2 textures.
- Bounds: height 0.4192, radius 0.4800, feet Y=0.
- Clips: idle (43 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels).
- SHA-256: `ba9d19a51d0baebedbf84011e4a405b9d296f6d9bb2cf9018edda6dc48682de7`.
- idle: `art/units/antiair halftrack/modernarmor_md_idle.kf`.
- run: `art/units/antiair halftrack/modernarmor_md_run.kf`.
- fortify: `art/units/antiair halftrack/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/antiair halftrack/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/antiair halftrack/modernarmor_md_hurta.kf`.

### military:heavy_cavalry

- Model: `src/Mods/Military/art/models/heavy_cavalry.glb` (644,192 bytes).
- Art: `ART_DEF_UNIT_HEAVY_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavycavalry/heavycavalry.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1227 triangles, 3 skins, 57 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4091, feet Y=0.
- Clips: idle (122 channels), run (122 channels), fortify (122 channels), attack (122 channels), hurt (122 channels), die (122 channels).
- SHA-256: `cc7e1d81d5675bd34bec9a8ccb39d510e7e1af8fc2aa3748da6e304bc9a564a7`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.

### military:heavy_pikeman

- Model: `src/Mods/Military/art/models/heavy_pikeman.glb` (273,632 bytes).
- Art: `ART_DEF_UNIT_HEAVY_SPEARMAN`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavyspearman/heavyspearman.nif`.
- KFM: `art/units/charlemagne_heavyspearman/heavyspearman.kfm`.
- Geometry: 608 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.5518, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `aa02aa1b92a288468bc2e19deacb831940f5f4e0c2a57a61f72247bd0f239ea7`.
- idle: `art/units/charlemagne_heavyspearman/heavyspearman_md_idle.kf`.
- run: `art/units/charlemagne_heavyspearman/heavyspearman_md_run.kf`.
- fortify: `art/units/charlemagne_heavyspearman/heavyspearman_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavyspearman/heavyspearman_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavyspearman/heavyspearman_md_hurta.kf`.
- die: `art/units/charlemagne_heavyspearman/heavyspearman_md_diea.kf`.

### military:heavy_swordsman

- Model: `src/Mods/Military/art/models/heavy_swordsman.glb` (279,224 bytes).
- Art: `ART_DEF_UNIT_HEAVY_SWORDSMAN`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavyswordsman/heavyswordsman.nif`.
- KFM: `art/units/charlemagne_heavyswordsman/heavyswordsman.kfm`.
- Geometry: 602 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3189, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b232d65bb1efb79d37490c0dd384a36d3c32338cc69c93a42899fe175539eadd`.
- idle: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_idle.kf`.
- run: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_run.kf`.
- fortify: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_hurta.kf`.
- die: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_diea.kf`.

### military:heavy_tank_m26

- Model: `src/Mods/Military/art/models/heavy_tank_m26.glb` (177,720 bytes).
- Art: `ART_DEF_UNIT_HEAVY_TANK`. Source scale: 0.38.
- NIF: `art/units/heavy tank/european/m26.nif`.
- KFM: `art/units/heavy tank/european/m26.kfm`.
- Geometry: 822 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.2670, radius 0.4800, feet Y=0.
- Clips: idle (12 channels), run (12 channels), fortify (12 channels), attack (12 channels), hurt (12 channels).
- SHA-256: `98d0fdd9813e719094c134ca0aeac21abf9d6436f5b703bc697798ead3bd6468`.
- idle: `art/units/heavy tank/european/m26_md_idle.kf`.
- run: `art/units/heavy tank/european/m26_md_run.kf`.
- fortify: `art/units/heavy tank/european/m26_md_rangedfortify.kf`.
- attack: `art/units/heavy tank/european/m26_md_rangedstrike.kf`.
- hurt: `art/units/heavy tank/european/m26_md_hurta.kf`.

### military:hellenic_missionary

- Model: `src/Mods/Military/art/models/hellenic_missionary.glb` (249,576 bytes).
- Art: `ART_DEF_UNIT_OLYMP_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/oracle/missionary_general.nif`.
- KFM: `art/units/missionary/oracle/missionary_general.kfm`.
- Geometry: 643 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2923, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), die (48 channels).
- SHA-256: `150133e046d5f52a56ff6a8259a509da8f7393e03868feac6be04a813f388c11`.
- idle: `art/units/missionary/oracle/missionary_general_md_idle.kf`.
- run: `art/units/missionary/oracle/missionary_general_md_run.kf`.
- fortify: `art/units/missionary/oracle/missionary_general_md_fortifya.kf`.
- die: `art/units/missionary/oracle/missionary_general_md_idle_die.kf`.

### military:highlander

- Model: `src/Mods/Military/art/models/highlander.glb` (291,476 bytes).
- Art: `ART_DEF_UNIT_HIGHLANDER`. Source scale: 0.44.
- NIF: `art/scotland/highlander/swordsman.nif`.
- KFM: `art/scotland/highlander/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3265, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `a689fcaff31a478659ad10b8ae9520193fa44717b93ea47b82b92484331a415a`.
- idle: `art/scotland/highlander/swordsman_md_idle.kf`.
- run: `art/scotland/highlander/swordsman_md_run.kf`.
- fortify: `art/scotland/highlander/swordsman_md_fortifya.kf`.
- attack: `art/scotland/highlander/swordsman_md_strikea.kf`.
- hurt: `art/scotland/highlander/swordsman_md_hurta.kf`.
- die: `art/scotland/highlander/swordsman_md_diea.kf`.

### military:hindu_missionary

- Model: `src/Mods/Military/art/models/hindu_missionary.glb` (402,220 bytes).
- Art: `ART_DEF_UNIT_HINDU_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/hindu/hindu_missionary.nif`.
- KFM: `art/units/missionary/hindu/hindu_missionary.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3339, feet Y=0.
- Clips: idle (98 channels), run (98 channels), fortify (98 channels), die (98 channels).
- SHA-256: `7a0e9587bf6e51c9c6c185ea3757c47ba7b811d5c57e177eac900cade12ecc9a`.
- idle: `art/units/missionary/hindu/hindu_missionary_md_idle.kf`.
- run: `art/units/missionary/hindu/hindu_missionary_md_run.kf`.
- fortify: `art/units/missionary/hindu/hindu_missionary_md_fortify.kf`.
- die: `art/units/missionary/hindu/hindu_missionary_md_idle_die.kf`.

### military:hitech_robot

- Model: `src/Mods/Military/art/models/hitech_robot.glb` (1,192,652 bytes).
- Art: `ART_DEF_UNIT_JAL`. Source scale: 0.62.
- NIF: `art/units/afterworld_gravebringer/jal/gravebringer.nif`.
- KFM: `art/units/afterworld_gravebringer/jal/gravebringer.kfm`.
- Geometry: 3438 triangles, 8 skins, 35 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.1485, feet Y=0.
- Clips: idle (91 channels), run (91 channels), fortify (91 channels), attack (91 channels), hurt (91 channels), die (91 channels).
- SHA-256: `9c564f75b8eb6f6a3856b01b4ea5f49357f966bb47264d38a8b04dae494c6f03`.
- idle: `art/units/afterworld_gravebringer/jal/gravebringer_md_idle.kf`.
- run: `art/units/afterworld_gravebringer/jal/gravebringer_md_run.kf`.
- fortify: `art/units/afterworld_gravebringer/jal/gravebringer_md_fortifya.kf`.
- attack: `art/units/afterworld_gravebringer/jal/gravebringer_md_strikea.kf`.
- hurt: `art/units/afterworld_gravebringer/jal/gravebringer_md_hurta.kf`.
- die: `art/units/afterworld_gravebringer/jal/gravebringer_md_diea.kf`.

### military:hittites_hittite_chariot

- Model: `src/Mods/Military/art/models/hittites_hittite_chariot.glb` (1,152,160 bytes).
- Art: `ART_DEF_UNIT_HITTITES_HITTITE_CHARIOT`. Source scale: 0.36.
- NIF: `art/units/unique/hittite chariot/egyptianwarchariot.nif`.
- KFM: `art/units/unique/egypt/warchariot/egyptianwarchariot.kfm`.
- Geometry: 2412 triangles, 6 skins, 75 joint bindings, 5 textures.
- Bounds: height 0.3800, radius 0.4800, feet Y=0.
- Clips: idle (214 channels), run (214 channels), fortify (214 channels), attack (214 channels), hurt (214 channels), die (214 channels).
- SHA-256: `fa51f27a6b0293431a1c263259a66a4ba3050a015b9b15d0a947da77c8d1c6a6`.
- idle: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_idle.kf`.
- run: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_run.kf`.
- fortify: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_fortifya.kf`.
- attack: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_strikea.kf`.
- hurt: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_hurta.kf`.
- die: `art/units/unique/egypt/warchariot/egyptianwarchariot_md_diea.kf`.

### military:holy_roman_landsknecht

- Model: `src/Mods/Military/art/models/holy_roman_landsknecht.glb` (344,620 bytes).
- Art: `ART_DEF_UNIT_HOLY_ROMAN_LANDSKNECHT`. Source scale: 0.44.
- NIF: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht.nif`.
- KFM: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht.kfm`.
- Geometry: 920 triangles, 2 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.4435, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `183557cad0ab605d577872ed848f4b9597b11869a6d5201395d7446cd247a7af`.
- idle: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_idle.kf`.
- run: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_run.kf`.
- fortify: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_fortifya.kf`.
- attack: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_strikea.kf`.
- hurt: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_hurta.kf`.
- die: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_diea.kf`.

### military:horse_archer

- Model: `src/Mods/Military/art/models/horse_archer.glb` (596,316 bytes).
- Art: `ART_DEF_UNIT_HORSE_ARCHER`. Source scale: 0.42.
- NIF: `art/units/horsearcher/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 1116 triangles, 3 skins, 37 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3088, feet Y=0.
- Clips: idle (107 channels), run (107 channels), fortify (107 channels), attack (107 channels), hurt (107 channels), die (107 channels).
- SHA-256: `aa65949e8e9c7495a868a309fbe715c1e5e11fb821c177185701a4dccf45c39d`.
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
- Bounds: height 0.6000, radius 0.3966, feet Y=0.
- Clips: idle (103 channels), run (103 channels), fortify (103 channels), attack (103 channels), hurt (103 channels), die (103 channels).
- SHA-256: `e29ed3d388ff43b2d1ebb35b5c7a4bc6c25c6650f7b7710c20fc66821320ce92`.
- idle: `art/units/horseman_eurasian/horseman_eurasian_md_idle.kf`.
- run: `art/units/horseman_eurasian/horseman_eurasian_md_run.kf`.
- fortify: `art/units/horseman_eurasian/horseman_eurasian_md_fortifya.kf`.
- attack: `art/units/horseman_eurasian/horseman_eurasian_md_strikea.kf`.
- hurt: `art/units/horseman_eurasian/horseman_eurasian_md_hurta.kf`.
- die: `art/units/horseman_eurasian/horseman_eurasian_md_diea.kf`.

### military:hungarian_huszar_cavalry

- Model: `src/Mods/Military/art/models/hungarian_huszar_cavalry.glb` (801,460 bytes).
- Art: `ART_DEF_UNIT_HUSZAR_CAVALRY`. Source scale: 0.42.
- NIF: `art/hungary/huszar cavalry/russiancossack.nif`.
- KFM: `art/hungary/huszar cavalry/russiancossack.kfm`.
- Geometry: 1076 triangles, 3 skins, 40 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3018, feet Y=0.
- Clips: idle (139 channels), run (139 channels), fortify (139 channels), attack (139 channels), hurt (139 channels), die (139 channels).
- SHA-256: `1102ce0338cbf01fb0bb098b8d0f388b248ddf48c39e796dbd1ec05a9f73ec89`.
- idle: `art/hungary/huszar cavalry/russiancossack_md_idle.kf`.
- run: `art/hungary/huszar cavalry/russiancossack_md_run.kf`.
- fortify: `art/hungary/huszar cavalry/russiancossack_md_fortifya.kf`.
- attack: `art/hungary/huszar cavalry/russiancossack_md_strikea.kf`.
- hurt: `art/hungary/huszar cavalry/russiancossack_md_hurta.kf`.
- die: `art/hungary/huszar cavalry/russiancossack_md_diea.kf`.

### military:huns_tarkan

- Model: `src/Mods/Military/art/models/huns_tarkan.glb` (1,508,420 bytes).
- Art: `ART_DEF_UNIT_HUNS_TARKAN`. Source scale: 0.42.
- NIF: `art/huns/tarkan/horsearcher.nif`.
- KFM: `art/huns/tarkan/horsearcher.kfm`.
- Geometry: 1132 triangles, 3 skins, 37 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3055, feet Y=0.
- Clips: idle (291 channels), run (291 channels), fortify (291 channels), attack (291 channels), hurt (291 channels), die (291 channels).
- SHA-256: `a5c28e97b7284d83512f75f688e856312ee62972eb48b4ce3ac2f540ba66ad59`.
- idle: `art/huns/tarkan/horsearcher_md_idle.kf`.
- run: `art/huns/tarkan/horsearcher_md_run.kf`.
- fortify: `art/huns/tarkan/horsearcher_md_fortifya.kf`.
- attack: `art/huns/tarkan/horsearcher_md_strikea.kf`.
- hurt: `art/huns/tarkan/horsearcher_md_hurta.kf`.
- die: `art/huns/tarkan/horsearcher_md_diea.kf`.
- Note: Missing texture: HorseArcher_weapons_64.dds

### military:hybrid_gunship

- Model: `src/Mods/Military/art/models/hybrid_gunship.glb` (247,668 bytes).
- Art: `ART_DEF_UNIT_HYBRID_GUNSHIP`. Source scale: 0.30.
- NIF: `art/units/hybrid gunship/heavymiss.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 1859 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2113, radius 0.4800, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels), attack (26 channels), hurt (26 channels).
- SHA-256: `ed071a273c01777108d60d066429115b8577599ed51df0a38845c23d8e287505`.
- idle: `art/units/gunship/gunship_md_idle.kf`.
- run: `art/units/gunship/gunship_md_run.kf`.
- fortify: `art/units/gunship/gunship_md_rangedfortify.kf`.
- attack: `art/units/gunship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/gunship/gunship_md_hurta.kf`.

### military:hydrogen

- Model: `src/Mods/Military/art/models/hydrogen.glb` (115,084 bytes).
- Art: `ART_DEF_UNIT_HYDROGEN`. Source scale: 0.85.
- NIF: `art/units/nukes/topol-m/icbm.nif`.
- KFM: `art/units/nukes/topol-m/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `c2604a8b3ef055e107e8ff37b0fd23239d501650bab9ee4d195a13759d96a346`.
- idle: `art/units/nukes/topol-m/icbm_md_idle.kf`.
- run: `art/units/nukes/topol-m/icbm_md_run.kf`.
- attack: `art/units/nukes/topol-m/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:icbm

- Model: `src/Mods/Military/art/models/icbm.glb` (94,168 bytes).
- Art: `ART_DEF_UNIT_ICBM`. Source scale: 0.85.
- NIF: `art/units/icbm/icbm.nif`.
- KFM: `art/units/icbm/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `5738f58182a24e3dd3b0fe91666278e2cf2851e62371c645ed9d3e9d624126e1`.
- idle: `art/units/icbm/icbm_md_idle.kf`.
- run: `art/units/icbm/icbm_md_run.kf`.
- attack: `art/units/icbm/icbm_md_strikea.kf`.

### military:il2

- Model: `src/Mods/Military/art/models/il2.glb` (316,164 bytes).
- Art: `ART_DEF_UNIT_IL2`. Source scale: 0.5.
- NIF: `art/units/il2/shturmovik.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 1430 triangles, 0 skins, 0 joint bindings, 3 textures.
- Bounds: height 0.2973, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `eed1b60fe0acd39e60752dec5e7197f8fdf15446824ae3f450deba052ecb1b6f`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:incan_quechua

- Model: `src/Mods/Military/art/models/incan_quechua.glb` (481,764 bytes).
- Art: `ART_DEF_UNIT_INCAN_QUECHUA`. Source scale: 0.44.
- NIF: `art/units/unique/inca/quechua/incanquechua.nif`.
- KFM: `art/units/unique/inca/quechua/incanquechua.kfm`.
- Geometry: 648 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3355, feet Y=0.
- Clips: idle (92 channels), run (92 channels), fortify (92 channels), attack (92 channels), hurt (92 channels), die (92 channels).
- SHA-256: `c52a714373ab9327b77bd524f6826e219c903770ef37c48892e943cfdebe79e2`.
- idle: `art/units/unique/inca/quechua/incanquechua_md_idle.kf`.
- run: `art/units/unique/inca/quechua/incanquechua_md_run.kf`.
- fortify: `art/units/unique/inca/quechua/incanquechua_md_fortifya.kf`.
- attack: `art/units/unique/inca/quechua/incanquechua_md_strikea.kf`.
- hurt: `art/units/unique/inca/quechua/incanquechua_md_hurta.kf`.
- die: `art/units/unique/inca/quechua/incanquechua_md_diea.kf`.

### military:indian_fast_worker

- Model: `src/Mods/Military/art/models/indian_fast_worker.glb` (237,856 bytes).
- Art: `ART_DEF_UNIT_INDIAN_FAST_WORKER`. Source scale: 0.44.
- NIF: `art/units/unique/india/worker/indianfastworker.nif`.
- KFM: `art/units/unique/india/worker/indianfastworker.kfm`.
- Geometry: 929 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3164, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels).
- SHA-256: `5eb6aafd3fa765d01f58da2b9d073828872fce923c0bf6a0d6289b03c19512dd`.
- idle: `art/units/unique/india/worker/indianfastworker_md_idle.kf`.
- run: `art/units/unique/india/worker/indianfastworker_md_run.kf`.
- fortify: `art/units/unique/india/worker/indianfastworker_md_fortify.kf`.
- Note: Missing texture: Worker_tools.dds

### military:infantry

- Model: `src/Mods/Military/art/models/infantry.glb` (352,328 bytes).
- Art: `ART_DEF_UNIT_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/infantry/infantry.nif`.
- KFM: `art/units/infantry/infantry.kfm`.
- Geometry: 658 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3162, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `6a9bb19a2a5d346dc68b4f40f7a1fe628fdbb605e38b70f0c8aba7557e4ffbeb`.
- idle: `art/units/infantry/infantry_md_idle.kf`.
- run: `art/units/infantry/infantry_md_run.kf`.
- fortify: `art/units/infantry/infantry_md_rangedfortify.kf`.
- attack: `art/units/infantry/infantry_md_rangedstrike.kf`.
- hurt: `art/units/infantry/infantry_md_hurta.kf`.
- die: `art/units/infantry/infantry_md_rangeddiea.kf`.

### military:inquisitor

- Model: `src/Mods/Military/art/models/inquisitor.glb` (416,256 bytes).
- Art: `ART_DEF_UNIT_INQUISITOR`. Source scale: 0.44.
- NIF: `art/units/godsofold_inquisitor/inquisitor_red.nif`.
- KFM: `art/units/godsofold_inquisitor/inquisitor_red.kfm`.
- Geometry: 678 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4407, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels).
- SHA-256: `d926dcf7c585683a119e33f932e4ff69b1107c05cb3d52632b52cebf60d3ac5e`.
- idle: `art/units/godsofold_inquisitor/inquisitor_red_md_idle.kf`.
- run: `art/units/godsofold_inquisitor/inquisitor_red_md_run.kf`.
- fortify: `art/units/godsofold_inquisitor/inquisitor_red_md_fortify.kf`.
- attack: `art/units/godsofold_inquisitor/inquisitor_red_md_rangedstrike.kf`.
- Note: Missing texture: Great_Spy_Ancient_DIFF.dds

### military:irbm

- Model: `src/Mods/Military/art/models/irbm.glb` (107,904 bytes).
- Art: `ART_DEF_UNIT_IRBM`. Source scale: 0.85.
- NIF: `art/units/nukes/irbm/icbm.nif`.
- KFM: `art/units/nukes/irbm/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `62753c6e6f675d8aef724cd4c57839c429cb0d402b8939ac356c34deb78a468c`.
- idle: `art/units/nukes/irbm/icbm_md_idle.kf`.
- run: `art/units/nukes/irbm/icbm_md_run.kf`.
- attack: `art/units/nukes/irbm/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:irish_fianna

- Model: `src/Mods/Military/art/models/irish_fianna.glb` (291,188 bytes).
- Art: `ART_DEF_UNIT_FIANNA`. Source scale: 0.44.
- NIF: `art/ireland/fianna/swordsman.nif`.
- KFM: `art/ireland/fianna/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3265, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `0f9b4de3aad043363b1670408dc2b76ab93702ba83bc19fd2c3e988b8b1657b4`.
- idle: `art/ireland/fianna/swordsman_md_idle.kf`.
- run: `art/ireland/fianna/swordsman_md_run.kf`.
- fortify: `art/ireland/fianna/swordsman_md_fortifya.kf`.
- attack: `art/ireland/fianna/swordsman_md_strikea.kf`.
- hurt: `art/ireland/fianna/swordsman_md_hurta.kf`.
- die: `art/ireland/fianna/swordsman_md_diea.kf`.

### military:iron_frigate

- Model: `src/Mods/Military/art/models/iron_frigate.glb` (340,372 bytes).
- Art: `ART_DEF_UNIT_IRON_FRIGATE`. Source scale: 0.19.
- NIF: `art/units/iron frigate/hms warrior.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1393 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.4769, radius 0.4800, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `3854363d697409072f7b51a16da3830b620b2ba34952fdbb3672d13e04eb60fb`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.
- Note: Missing texture: //hmswarrior256.dds

### military:ironclad

- Model: `src/Mods/Military/art/models/ironclad.glb` (301,420 bytes).
- Art: `ART_DEF_UNIT_IRONCLAD`. Source scale: 0.52.
- NIF: `art/units/ironclad/ironclad.nif`.
- KFM: `art/units/ironclad/ironclad.kfm`.
- Geometry: 445 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.2585, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `2b8af061878befb0f9808f9e357f08e5259e1372c5000990107b8d175ac2cb0a`.
- idle: `art/units/ironclad/ironclad_md_idle.kf`.
- run: `art/units/ironclad/ironclad_md_run.kf`.
- fortify: `art/units/ironclad/ironclad_md_fortifya.kf`.
- attack: `art/units/ironclad/ironclad_md_strikea.kf`.
- hurt: `art/units/ironclad/ironclad_md_hurta.kf`.
- die: `art/units/ironclad/ironclad_md_diea.kf`.

### military:ironclad_cruiser

- Model: `src/Mods/Military/art/models/ironclad_cruiser.glb` (750,232 bytes).
- Art: `ART_DEF_UNIT_IRONCLAD_CRUISER`. Source scale: 0.32.
- NIF: `art/units/ironclad cruiser/ww1_cr_rus.nif`.
- KFM: `art/units/ironclad cruiser/ww1_cr_rus.kfm`.
- Geometry: 2188 triangles, 1 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.3554, radius 0.4800, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (86 channels), attack (86 channels), hurt (86 channels), die (86 channels).
- SHA-256: `d5043c215f2af9a3cd18aece96207defb78702a645713c27d3f196b87b19aa4f`.
- idle: `art/units/ironclad cruiser/ww1_cr_rus_md_idle.kf`.
- run: `art/units/ironclad cruiser/ww1_cr_rus_md_run.kf`.
- fortify: `art/units/ironclad cruiser/ww1_cr_rus_md_fortify.kf`.
- attack: `art/units/ironclad cruiser/ww1_cr_rus_md_strike.kf`.
- hurt: `art/units/ironclad cruiser/ww1_cr_rus_md_hurt.kf`.
- die: `art/units/ironclad cruiser/ww1_cr_rus_md_die.kf`.

### military:iroquois_mohawkarcher

- Model: `src/Mods/Military/art/models/iroquois_mohawkarcher.glb` (365,344 bytes).
- Art: `ART_DEF_UNIT_IROQUOIS_MOHAWKARCHER`. Source scale: 0.44.
- NIF: `art/units/unique/iroquois/mohawk longbowman/archer.nif`.
- KFM: `art/units/archer/archer.kfm`.
- Geometry: 848 triangles, 3 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2956, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `c01b156bedfea7ad975899cd37bc61334551eb25aa5645a6fd998d15e543dd3c`.
- idle: `art/units/archer/archer_md_idle.kf`.
- run: `art/units/archer/archer_md_run.kf`.
- fortify: `art/units/archer/archer_md_fortifya.kf`.
- attack: `art/units/archer/archer_md_strikea.kf`.
- hurt: `art/units/archer/archer_md_hurta.kf`.
- die: `art/units/archer/archer_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:islamic_missionary

- Model: `src/Mods/Military/art/models/islamic_missionary.glb` (187,664 bytes).
- Art: `ART_DEF_UNIT_ISLAMIC_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/islamic/islamic_missionary.nif`.
- KFM: `art/units/missionary/islamic/islamic_missionary.kfm`.
- Geometry: 566 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2300, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), die (46 channels).
- SHA-256: `8ff2941089417b017b3abe8375f7380aadbdcd42c692a0a1a24000c5a76e9e8a`.
- idle: `art/units/missionary/islamic/islamic_missionary_md_idle.kf`.
- run: `art/units/missionary/islamic/islamic_missionary_md_run.kf`.
- fortify: `art/units/missionary/islamic/islamic_missionary_md_fortify.kf`.
- die: `art/units/missionary/islamic/islamic_missionary_md_idle_die.kf`.

### military:japan_samurai

- Model: `src/Mods/Military/art/models/japan_samurai.glb` (302,372 bytes).
- Art: `ART_DEF_UNIT_JAPAN_SAMURAI`. Source scale: 0.44.
- NIF: `art/units/unique/japan/samurai/japanesesamurai.nif`.
- KFM: `art/units/unique/japan/samurai/japanesesamurai.kfm`.
- Geometry: 801 triangles, 2 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3957, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `7521aa45f9094b5d41feece5b15fd6a51f6185d3023d3c0d7779648c6a86f54e`.
- idle: `art/units/unique/japan/samurai/japanesesamurai_md_idle.kf`.
- run: `art/units/unique/japan/samurai/japanesesamurai_md_run.kf`.
- fortify: `art/units/unique/japan/samurai/japanesesamurai_md_fortifya.kf`.
- attack: `art/units/unique/japan/samurai/japanesesamurai_md_strikea.kf`.
- hurt: `art/units/unique/japan/samurai/japanesesamurai_md_hurta.kf`.
- die: `art/units/unique/japan/samurai/japanesesamurai_md_diea.kf`.

### military:javelineer

- Model: `src/Mods/Military/art/models/javelineer.glb` (517,644 bytes).
- Art: `ART_DEF_UNIT_JAVELINEER`. Source scale: 0.44.
- NIF: `art/units/javelineer/european/javelineer.nif`.
- KFM: `art/units/javelineer/animations/javelineer_2.kfm`.
- Geometry: 620 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3723, feet Y=0.
- Clips: idle (100 channels), run (100 channels), fortify (100 channels), attack (100 channels), hurt (100 channels), die (100 channels).
- SHA-256: `f38aa00331c65f9830601186bc08664c41ca13d38f1a6e90c7bd0b2380aa35dd`.
- idle: `art/units/javelineer/animations/maliskirmisher_md_idle.kf`.
- run: `art/units/javelineer/animations/maliskirmisher_md_run.kf`.
- fortify: `art/units/javelineer/animations/maliskirmisher_md_fortifya.kf`.
- attack: `art/units/javelineer/animations/maliskirmisher_md_strikea.kf`.
- hurt: `art/units/javelineer/animations/maliskirmisher_md_hurta.kf`.
- die: `art/units/javelineer/animations/maliskirmisher_md_diea.kf`.

### military:jeep

- Model: `src/Mods/Military/art/models/jeep.glb` (498,960 bytes).
- Art: `ART_DEF_UNIT_JEEP`. Source scale: 0.45.
- NIF: `art/units/jeep/willys.nif`.
- KFM: `art/units/jeep/willys.kfm`.
- Geometry: 2443 triangles, 3 skins, 22 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4513, feet Y=0.
- Clips: idle (72 channels), run (72 channels), fortify (72 channels), attack (72 channels), hurt (72 channels).
- SHA-256: `5d8b4d91ff6e9dd7e24aab6ad9274c399a31968a4cd69570824168cd90355a3e`.
- idle: `art/units/jeep/willys_md_idle.kf`.
- run: `art/units/jeep/willys_md_run.kf`.
- fortify: `art/units/jeep/willys_md_rangedfortify.kf`.
- attack: `art/units/jeep/willys_md_rangedstrike.kf`.
- hurt: `art/units/jeep/willys_md_hurta.kf`.
- Note: Hidden geometry omitted: Biped Object
- Note: Hidden geometry omitted: Biped Object@#0

### military:jet_fighter

- Model: `src/Mods/Military/art/models/jet_fighter.glb` (244,196 bytes).
- Art: `ART_DEF_UNIT_JET_FIGHTER`. Source scale: 0.48.
- NIF: `art/units/jetfighter/jetfighter.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 1760 triangles, 2 skins, 6 joint bindings, 1 textures.
- Bounds: height 0.2951, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), hurt (23 channels).
- SHA-256: `dbc29662e1b3926f47e162bc96ce27dff96930f3e9bbb2a30ffdeafc6ea69f79`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:jewish_missionary

- Model: `src/Mods/Military/art/models/jewish_missionary.glb` (170,800 bytes).
- Art: `ART_DEF_UNIT_JEWISH_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/jewish/jewishmissionary.nif`.
- KFM: `art/units/missionary/jewish/jewishmissionary.kfm`.
- Geometry: 592 triangles, 2 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2512, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), die (45 channels).
- SHA-256: `2f4fed5e4dc5e81ded865acc8e3643b44173384e0e30bcedf11e40a134b0ea9f`.
- idle: `art/units/missionary/jewish/jewishmissionary_md_idle.kf`.
- run: `art/units/missionary/jewish/jewishmissionary_md_run.kf`.
- fortify: `art/units/missionary/jewish/jewishmissionary_md_fortifya.kf`.
- die: `art/units/missionary/jewish/jewishmissionary_md_idle_die.kf`.

### military:khmer_ballista_elephant

- Model: `src/Mods/Military/art/models/khmer_ballista_elephant.glb` (663,228 bytes).
- Art: `ART_DEF_UNIT_KHMER_BALLISTA_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/unique_khmer_elephantballista/khmer_elephantballista.nif`.
- KFM: `art/units/unique_khmer_elephantballista/khmer_elephantballista.kfm`.
- Geometry: 1414 triangles, 3 skins, 46 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2909, feet Y=0.
- Clips: idle (134 channels), run (134 channels), attack (134 channels), die (134 channels).
- SHA-256: `29026bb831c582cc52e4836a9d6500d25f57fab3f21cde261e373575764c3b70`.
- idle: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_idle.kf`.
- run: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_run.kf`.
- attack: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_rangedstrikea.kf`.
- die: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_rangeddiea.kf`.

### military:knight

- Model: `src/Mods/Military/art/models/knight.glb` (615,640 bytes).
- Art: `ART_DEF_UNIT_KNIGHT`. Source scale: 0.42.
- NIF: `art/units/knight/knight.nif`.
- KFM: `art/units/knight/knight.kfm`.
- Geometry: 1143 triangles, 3 skins, 37 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3002, feet Y=0.
- Clips: idle (109 channels), run (109 channels), fortify (109 channels), attack (109 channels), hurt (109 channels), die (109 channels).
- SHA-256: `19638f5f310b4277a425fc606caabedd7aa0f18b9878be35c13ab05735e77e3b`.
- idle: `art/units/knight/knight_md_idle.kf`.
- run: `art/units/knight/knight_md_run.kf`.
- fortify: `art/units/knight/knight_md_fortifya.kf`.
- attack: `art/units/knight/knight_md_strikea.kf`.
- hurt: `art/units/knight/knight_md_hurta.kf`.
- die: `art/units/knight/knight_md_diea.kf`.
- Note: Missing texture: Knight_horse_128.dds

### military:korean_hwacha

- Model: `src/Mods/Military/art/models/korean_hwacha.glb` (406,980 bytes).
- Art: `ART_DEF_UNIT_KOREAN_HWACHA`. Source scale: 0.50.
- NIF: `art/units/hwacha_korean/hwacha.nif`.
- KFM: `art/units/hwacha_korean/hwacha.kfm`.
- Geometry: 1298 triangles, 2 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4053, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels).
- SHA-256: `53de43b286515473a349072e32f130ac85557c5f655554295351bb706bc28852`.
- idle: `art/units/hwacha_korean/hwacha_md_idle.kf`.
- run: `art/units/hwacha_korean/hwacha_md_run.kf`.
- fortify: `art/units/hwacha_korean/hwacha_md_rangedfortify.kf`.
- attack: `art/units/hwacha_korean/hwacha_md_rangedstrike.kf`.
- hurt: `art/units/hwacha_korean/hwacha_md_hurta.kf`.

### military:lancer

- Model: `src/Mods/Military/art/models/lancer.glb` (695,532 bytes).
- Art: `ART_DEF_UNIT_LANCER`. Source scale: 0.42.
- NIF: `art/units/lancer/european/lancer.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1228 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3672, feet Y=0.
- Clips: idle (119 channels), run (119 channels), fortify (119 channels), attack (119 channels), hurt (119 channels), die (119 channels).
- SHA-256: `bfc70e06d17632279911d9dee3d0c317378c6e671ec7da7964bd9b0149e49ee4`.
- idle: `art/units/unique/mongol/keshik/mongelkeshik_md_idle.kf`.
- run: `art/units/unique/mongol/keshik/mongelkeshik_md_run.kf`.
- fortify: `art/units/unique/mongol/keshik/mongelkeshik_md_fortifya.kf`.
- attack: `art/units/unique/mongol/keshik/mongelkeshik_md_strikea.kf`.
- hurt: `art/units/unique/mongol/keshik/mongelkeshik_md_hurta.kf`.
- die: `art/units/unique/mongol/keshik/mongelkeshik_md_diea.kf`.

### military:light_aa

- Model: `src/Mods/Military/art/models/light_aa.glb` (136,056 bytes).
- Art: `ART_DEF_UNIT_LIGHT_AA`. Source scale: 0.6.
- NIF: `art/units/light antiair gun/lightaa.nif`.
- KFM: `art/units/light antiair gun/lightaa.kfm`.
- Geometry: 1045 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.3455, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `d23b3f8980cb4567836718693edc146d46c7c1d186985957ff7cb1ffbccdf32e`.
- idle: `art/units/light antiair gun/lightaa_md_idle.kf`.
- run: `art/units/light antiair gun/lightaa_md_run.kf`.
- fortify: `art/units/light antiair gun/lightaa_md_rangedfortify.kf`.
- attack: `art/units/light antiair gun/lightaa_md_rangedstrike.kf`.
- hurt: `art/units/light antiair gun/lightaa_md_hurta.kf`.

### military:light_artillery

- Model: `src/Mods/Military/art/models/light_artillery.glb` (215,504 bytes).
- Art: `ART_DEF_UNIT_LIGHT_ARTILLERY`. Source scale: 0.7.
- NIF: `art/units/light artillery/artillery.nif`.
- KFM: `art/units/artillery/artillery.kfm`.
- Geometry: 442 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2290, radius 0.4800, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels).
- SHA-256: `db74346098700426277212b619fbb3caf66a1647e83841e1be979c467f0094d7`.
- idle: `art/units/artillery/artillery_md_idle.kf`.
- run: `art/units/artillery/artillery_md_run.kf`.
- fortify: `art/units/artillery/artillery_md_rangedfortify.kf`.
- attack: `art/units/artillery/artillery_md_rangedstrike.kf`.
- hurt: `art/units/artillery/artillery_md_hurta.kf`.

### military:light_cavalry

- Model: `src/Mods/Military/art/models/light_cavalry.glb` (635,496 bytes).
- Art: `ART_DEF_UNIT_LIGHT_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/charlemagne_lightcavalry/lightcavalry.nif`.
- KFM: `art/units/charlemagne_lightcavalry/lightcavalry.kfm`.
- Geometry: 1306 triangles, 2 skins, 43 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4265, feet Y=0.
- Clips: idle (108 channels), run (108 channels), fortify (108 channels), attack (108 channels), hurt (108 channels), die (108 channels).
- SHA-256: `277d2eae0315504f970a2ea25af6898d4c4d4d3b72dcd715d617e02615c33494`.
- idle: `art/units/charlemagne_lightcavalry/lightcavalry_md_idle.kf`.
- run: `art/units/charlemagne_lightcavalry/lightcavalry_md_run.kf`.
- fortify: `art/units/charlemagne_lightcavalry/lightcavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_lightcavalry/lightcavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_lightcavalry/lightcavalry_md_hurta.kf`.
- die: `art/units/charlemagne_lightcavalry/lightcavalry_md_diea.kf`.

### military:light_swordsman

- Model: `src/Mods/Military/art/models/light_swordsman.glb` (292,464 bytes).
- Art: `ART_DEF_UNIT_SWORDSMAN`. Source scale: 0.44.
- NIF: `art/units/swordsman/swordsman.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3265, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `16d01b81cc045817e8d2c39d368ea385346ba3445b5c841c60f05352f4ea96d3`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:light_tank

- Model: `src/Mods/Military/art/models/light_tank.glb` (239,348 bytes).
- Art: `ART_DEF_UNIT_VICKERS_E`. Source scale: 0.5.
- NIF: `art/units/light tank/european/vickers_e.nif`.
- KFM: `art/units/light tank/european/vickers_e.kfm`.
- Geometry: 712 triangles, 2 skins, 4 joint bindings, 2 textures.
- Bounds: height 0.4028, radius 0.4800, feet Y=0.
- Clips: idle (10 channels), run (10 channels), fortify (10 channels), attack (10 channels), hurt (10 channels).
- SHA-256: `7cc0471bd3eabc500eb57139a455e06f22e754f9400aab5ad5f6c41fb0e82fa8`.
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
- Bounds: height 0.5756, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `f4889d9d081755a0e164d37f13b2586c90438216bd1e1be6251d4eb876e66ee3`.
- idle: `art/units/lion/lion_md_idle.kf`.
- run: `art/units/lion/lion_md_run.kf`.
- fortify: `art/units/lion/lion_md_fortifya.kf`.
- attack: `art/units/lion/lion_md_strikea.kf`.
- hurt: `art/units/lion/lion_md_hurta.kf`.
- die: `art/units/lion/lion_md_diea.kf`.

### military:lithuanian_vytis

- Model: `src/Mods/Military/art/models/lithuanian_vytis.glb` (872,208 bytes).
- Art: `ART_DEF_UNIT_VYTIS`. Source scale: 0.42.
- NIF: `art/lithuania/vytis/heavycavalry.nif`.
- KFM: `art/lithuania/vytis/heavycavalry.kfm`.
- Geometry: 1138 triangles, 2 skins, 39 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4156, feet Y=0.
- Clips: idle (182 channels), run (182 channels), fortify (182 channels), attack (182 channels), hurt (182 channels), die (182 channels).
- SHA-256: `21ff0cc0ec38db475d9c9df1ec83acb7e383fba03b7df326673c22e4400cdef4`.
- idle: `art/lithuania/vytis/heavycavalry_md_idle.kf`.
- run: `art/lithuania/vytis/heavycavalry_md_run.kf`.
- fortify: `art/lithuania/vytis/heavycavalry_md_fortifya.kf`.
- attack: `art/lithuania/vytis/heavycavalry_md_strikea.kf`.
- hurt: `art/lithuania/vytis/heavycavalry_md_hurta.kf`.
- die: `art/lithuania/vytis/heavycavalry_md_diea.kf`.

### military:littoral_combat_ship

- Model: `src/Mods/Military/art/models/littoral_combat_ship.glb` (368,876 bytes).
- Art: `ART_DEF_UNIT_LITTORAL_COMBAT_SHIP`. Source scale: 0.7.
- NIF: `art/units/littoral combat ship/dd_sens.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 955 triangles, 2 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.3774, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `ce65fa6491b01f6d14619ab5c3a26b1528b7f3be79afadd66f2e5baf61b8782c`.
- idle: `art/units/destroyer/destroyer_md_idle.kf`.
- run: `art/units/destroyer/destroyer_md_run.kf`.
- fortify: `art/units/destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/destroyer/destroyer_md_hurta.kf`.
- die: `art/units/destroyer/destroyer_md_diea.kf`.

### military:lombard_gasindii

- Model: `src/Mods/Military/art/models/lombard_gasindii.glb` (485,336 bytes).
- Art: `ART_DEF_UNIT_GASINDII`. Source scale: 0.44.
- NIF: `art/lombards/gasindii/greekphalanx.nif`.
- KFM: `art/lombards/gasindii/greekphalanx.kfm`.
- Geometry: 598 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4593, feet Y=0.
- Clips: idle (95 channels), run (95 channels), fortify (95 channels), attack (95 channels), hurt (95 channels), die (95 channels).
- SHA-256: `dcc2b71d21faf80d7e5eadc4b2b969fbe2dce343a5fd023959bb5e6d34b11f81`.
- idle: `art/lombards/gasindii/greekphalanx_md_idle.kf`.
- run: `art/lombards/gasindii/greekphalanx_md_run.kf`.
- fortify: `art/lombards/gasindii/greekphalanx_md_fortifya.kf`.
- attack: `art/lombards/gasindii/greekphalanx_md_strikea.kf`.
- hurt: `art/lombards/gasindii/greekphalanx_md_hurta.kf`.
- die: `art/lombards/gasindii/greekphalanx_md_diea.kf`.

### military:longbowman

- Model: `src/Mods/Military/art/models/longbowman.glb` (387,608 bytes).
- Art: `ART_DEF_UNIT_LONGBOWMAN`. Source scale: 0.44.
- NIF: `art/units/longbowman/longbowman.nif`.
- KFM: `art/units/longbowman/longbowman.kfm`.
- Geometry: 718 triangles, 2 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3156, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels), attack (59 channels), hurt (59 channels), die (59 channels).
- SHA-256: `501216ff7237208251d4f200018e4c4dca48467f1e51ef55223d4aca904b8007`.
- idle: `art/units/longbowman/longbowman_md_idle.kf`.
- run: `art/units/longbowman/longbowman_md_run.kf`.
- fortify: `art/units/longbowman/longbowman_md_fortifya.kf`.
- attack: `art/units/longbowman/longbowman_md_strikea.kf`.
- hurt: `art/units/longbowman/longbowman_md_hurta.kf`.
- die: `art/units/longbowman/longbowman_md_diea.kf`.

### military:maceman

- Model: `src/Mods/Military/art/models/maceman.glb` (359,308 bytes).
- Art: `ART_DEF_UNIT_MACEMAN`. Source scale: 0.44.
- NIF: `art/units/maceman/maceman.nif`.
- KFM: `art/units/maceman/maceman.kfm`.
- Geometry: 672 triangles, 2 skins, 20 joint bindings, 2 textures.
- Bounds: height 0.5576, radius 0.4800, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels), die (58 channels).
- SHA-256: `75cd4a94c184af8f050b72b207427f7ec7a71bdfafecff96f55bef0ecba69c11`.
- idle: `art/units/maceman/maceman_md_idle.kf`.
- run: `art/units/maceman/maceman_md_run.kf`.
- fortify: `art/units/maceman/maceman_md_fortifya.kf`.
- attack: `art/units/maceman/maceman_md_strikea.kf`.
- hurt: `art/units/maceman/maceman_md_hurta.kf`.
- die: `art/units/maceman/maceman_md_diea.kf`.

### military:machine_gun

- Model: `src/Mods/Military/art/models/machine_gun.glb` (276,264 bytes).
- Art: `ART_DEF_UNIT_MACHINE_GUN`. Source scale: 0.44.
- NIF: `art/units/machinegun/machinegunner.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 774 triangles, 3 skins, 23 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2972, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels), hurt (61 channels).
- SHA-256: `a386a6be3b885a2c814cc245c988f4fb2791a77a6c037edf7ab5435eba4ba3e8`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:mali_skirmisher

- Model: `src/Mods/Military/art/models/mali_skirmisher.glb` (330,204 bytes).
- Art: `ART_DEF_UNIT_MALI_SKIRMISHER`. Source scale: 0.44.
- NIF: `art/units/unique/mali/skirmisher/maliskirmisher.nif`.
- KFM: `art/units/unique/mali/skirmisher/maliskirmisher.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3728, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `2fe88a9a14909bdfe8f3a182e9f9e3cbb6cadb4783ca63d8a2bf6bdb14c107d4`.
- idle: `art/units/unique/mali/skirmisher/maliskirmisher_md_idle.kf`.
- run: `art/units/unique/mali/skirmisher/maliskirmisher_md_run.kf`.
- fortify: `art/units/unique/mali/skirmisher/maliskirmisher_md_fortifya.kf`.
- attack: `art/units/unique/mali/skirmisher/maliskirmisher_md_strikea.kf`.
- hurt: `art/units/unique/mali/skirmisher/maliskirmisher_md_hurta.kf`.
- die: `art/units/unique/mali/skirmisher/maliskirmisher_md_diea.kf`.

### military:man_at_arms

- Model: `src/Mods/Military/art/models/man_at_arms.glb` (652,868 bytes).
- Art: `ART_DEF_UNIT_MAN_AT_ARMS`. Source scale: 0.42.
- NIF: `art/units/man at arms/european/mmaceman_light.nif`.
- KFM: `art/units/horse archer/greek/companion_cavalry.kfm`.
- Geometry: 1145 triangles, 3 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2600, feet Y=0.
- Clips: idle (116 channels), run (116 channels), fortify (116 channels), attack (116 channels), hurt (116 channels), die (116 channels).
- SHA-256: `3bec5e748beb8061886a6c637724dcab509cdad1e9d33ab32d96ed9ff9f8e607`.
- idle: `art/units/horse archer/greek/companion_cavalry_md_idle.kf`.
- run: `art/units/horse archer/greek/companion_cavalry_md_run.kf`.
- fortify: `art/units/horse archer/greek/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/horse archer/greek/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/horse archer/greek/companion_cavalry_md_hurta.kf`.
- die: `art/units/horse archer/greek/companion_cavalry_md_diea.kf`.

### military:manowar

- Model: `src/Mods/Military/art/models/manowar.glb` (409,580 bytes).
- Art: `ART_DEF_UNIT_MANOWAR`. Source scale: 0.20.
- NIF: `art/units/man o war/frigate.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1135 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2956, feet Y=0.
- Clips: idle (82 channels), run (82 channels), fortify (82 channels), attack (82 channels), hurt (82 channels), die (82 channels).
- SHA-256: `c99f77d979636e20e3861187ff25d7f5416da89325d52cf5f4f8c4e4a11130de`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.

### military:marine

- Model: `src/Mods/Military/art/models/marine.glb` (315,800 bytes).
- Art: `ART_DEF_UNIT_MARINE`. Source scale: 0.44.
- NIF: `art/units/marine/marines.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 728 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3476, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `12da63a0506fd5c82f174cff7aed67f796d48a56375ea7266955d110c438efba`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:maya_holkan

- Model: `src/Mods/Military/art/models/maya_holkan.glb` (368,252 bytes).
- Art: `ART_DEF_UNIT_MAYA_HOLKAN`. Source scale: 0.44.
- NIF: `art/units/unique_maya_holkan/unique_mayan_holkan.nif`.
- KFM: `art/units/unique_maya_holkan/unique_mayan_holkan.kfm`.
- Geometry: 588 triangles, 2 skins, 25 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4025, feet Y=0.
- Clips: idle (62 channels), run (62 channels), fortify (62 channels), attack (62 channels), hurt (62 channels), die (62 channels).
- SHA-256: `1296bc077d686ea6a5014ad88a1973fad99b1425bc0ced4cb02fc29b6967bf2b`.
- idle: `art/units/unique_maya_holkan/unique_mayan_holkan_md_idle.kf`.
- run: `art/units/unique_maya_holkan/unique_mayan_holkan_md_run.kf`.
- fortify: `art/units/unique_maya_holkan/unique_mayan_holkan_md_fortifya.kf`.
- attack: `art/units/unique_maya_holkan/unique_mayan_holkan_md_strikea.kf`.
- hurt: `art/units/unique_maya_holkan/unique_mayan_holkan_md_hurta.kf`.
- die: `art/units/unique_maya_holkan/unique_mayan_holkan_md_diea.kf`.

### military:mechanized_infantry

- Model: `src/Mods/Military/art/models/mechanized_infantry.glb` (258,764 bytes).
- Art: `ART_DEF_UNIT_MECHANIZED_INFANTRY`. Source scale: 0.43.
- NIF: `art/units/mechanizedinfantry/mechanizedinfantry.nif`.
- KFM: `art/units/mechanizedinfantry/mechanizedinfantry.kfm`.
- Geometry: 515 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4240, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels).
- SHA-256: `cce906b8764d9048e7f7e446cdc4106e250cc8a3a64c7f34fbfb7a5c186746bf`.
- idle: `art/units/mechanizedinfantry/mechanizedinfantry_md_idle.kf`.
- run: `art/units/mechanizedinfantry/mechanizedinfantry_md_run.kf`.
- fortify: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedfortify.kf`.
- attack: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedstrike.kf`.
- hurt: `art/units/mechanizedinfantry/mechanizedinfantry_md_hurta.kf`.

### military:med_caravan

- Model: `src/Mods/Military/art/models/med_caravan.glb` (648,332 bytes).
- Art: `ART_DEF_UNIT_MED_CARAVAN`. Source scale: 0.34.
- NIF: `art/units/charlemagne_supplytrain/supplytrain.nif`.
- KFM: `art/units/charlemagne_supplytrain/supplytrain.kfm`.
- Geometry: 2396 triangles, 4 skins, 81 joint bindings, 4 textures.
- Bounds: height 0.3331, radius 0.4800, feet Y=0.
- Clips: idle (191 channels), run (191 channels).
- SHA-256: `9a234e4d999d361d369358f538a6f36d25bdde7c073a52f394d3db3f0e5f1d69`.
- idle: `art/units/charlemagne_supplytrain/supplytrain_md_idle.kf`.
- run: `art/units/charlemagne_supplytrain/supplytrain_md_run.kf`.

### military:merchant

- Model: `src/Mods/Military/art/models/merchant.glb` (423,408 bytes).
- Art: `ART_DEF_UNIT_MERCHANT`. Source scale: 0.46.
- NIF: `art/units/greatpeople/merchant/greatmerchantancient.nif`.
- KFM: `art/units/greatpeople/merchant/greatmerchantancient.kfm`.
- Geometry: 1410 triangles, 3 skins, 42 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3688, feet Y=0.
- Clips: idle (100 channels), run (100 channels), fortify (100 channels).
- SHA-256: `8aaf1be1b9ae7620e5092a619d7a5b3bb9bcc676d14734906da4882b80f80d3f`.
- idle: `art/units/greatpeople/merchant/greatmerchantancient_md_idle.kf`.
- run: `art/units/greatpeople/merchant/greatmerchantancient_md_run.kf`.
- fortify: `art/units/greatpeople/merchant/greatmerchantancient_md_fortify.kf`.
- Note: Non-triangle effect geometry omitted: GoldThrow_front
- Note: Non-triangle effect geometry omitted: GoldDig_rightside
- Note: Non-triangle effect geometry omitted: GoldDig_leftside

### military:minigun

- Model: `src/Mods/Military/art/models/minigun.glb` (318,916 bytes).
- Art: `ART_DEF_UNIT_MINIGUN`. Source scale: 0.44.
- NIF: `art/units/minigun/european/fremg.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 1012 triangles, 3 skins, 23 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2972, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels), hurt (61 channels).
- SHA-256: `35ff220c7e0a86ad575a713f694bea2a9f72ad9955f26f67bbafca7ad1f8e738`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:missile_cruiser

- Model: `src/Mods/Military/art/models/missile_cruiser.glb` (262,348 bytes).
- Art: `ART_DEF_UNIT_MISSILE_CRUISER`. Source scale: 0.6.
- NIF: `art/units/missile_cruiser/missile_cruiser.nif`.
- KFM: `art/units/missile_cruiser/missile_cruiser.kfm`.
- Geometry: 526 triangles, 1 skins, 5 joint bindings, 1 textures.
- Bounds: height 0.3535, radius 0.4800, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels), hurt (42 channels), die (42 channels).
- SHA-256: `681646b2a24e8bad0e2bc1a158dadebc312213cb9a670ce4626a2acef220747d`.
- idle: `art/units/missile_cruiser/missile_cruiser_md_idle.kf`.
- run: `art/units/missile_cruiser/missile_cruiser_md_run.kf`.
- fortify: `art/units/missile_cruiser/missile_cruiser_md_fortifya.kf`.
- attack: `art/units/missile_cruiser/missile_cruiser_md_strikea.kf`.
- hurt: `art/units/missile_cruiser/missile_cruiser_md_hurta.kf`.
- die: `art/units/missile_cruiser/missile_cruiser_md_diea.kf`.

### military:mobile_artillery

- Model: `src/Mods/Military/art/models/mobile_artillery.glb` (217,440 bytes).
- Art: `ART_DEF_UNIT_MOBILE_ARTILLERY`. Source scale: 0.57.
- NIF: `art/units/mobileartillery/mobileartillery.nif`.
- KFM: `art/units/mobileartillery/mobileartillery.kfm`.
- Geometry: 708 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.3153, radius 0.4800, feet Y=0.
- Clips: idle (19 channels), run (19 channels), fortify (19 channels), attack (19 channels), hurt (19 channels).
- SHA-256: `f780201f8c8fc1db49dec755b76044fa5c12a5aff3982e1a59d50958a8eb4009`.
- idle: `art/units/mobileartillery/mobileartillery_md_idle.kf`.
- run: `art/units/mobileartillery/mobileartillery_md_run.kf`.
- fortify: `art/units/mobileartillery/mobileartillery_md_rangedfortify.kf`.
- attack: `art/units/mobileartillery/mobileartillery_md_rangedstrike.kf`.
- hurt: `art/units/mobileartillery/mobileartillery_md_hurta.kf`.

### military:mobile_sam

- Model: `src/Mods/Military/art/models/mobile_sam.glb` (348,256 bytes).
- Art: `ART_DEF_UNIT_MOBILE_SAM`. Source scale: 0.57.
- NIF: `art/units/mobilesam/mobilesam.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 802 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.3824, radius 0.4800, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels).
- SHA-256: `dd3684baa0003fce6068f0ac096eeec82d7155a590665682ae0ca584d8b27b5b`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:modern_armor

- Model: `src/Mods/Military/art/models/modern_armor.glb` (238,844 bytes).
- Art: `ART_DEF_UNIT_MODERN_ARMOR`. Source scale: 0.57.
- NIF: `art/units/modernarmor/modernarmor.nif`.
- KFM: `art/units/modernarmor/modernarmor.kfm`.
- Geometry: 546 triangles, 1 skins, 20 joint bindings, 1 textures.
- Bounds: height 0.2531, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels).
- SHA-256: `50b2f1c7ff2a603f589fdbcb6140207b82f78e6a43945be0bb5ff0ec0046085d`.
- idle: `art/units/modernarmor/modernarmor_md_idle.kf`.
- run: `art/units/modernarmor/modernarmor_md_run.kf`.
- fortify: `art/units/modernarmor/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/modernarmor/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/modernarmor/modernarmor_md_hurta.kf`.

### military:modern_grenadier

- Model: `src/Mods/Military/art/models/modern_grenadier.glb` (1,043,812 bytes).
- Art: `ART_DEF_UNIT_MODERN_GRENADIER`. Source scale: 0.44.
- NIF: `art/units/modern grenadier/grenadier.nif`.
- KFM: `art/units/grenadier/grenadier.kfm`.
- Geometry: 848 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3449, feet Y=0.
- Clips: idle (180 channels), run (180 channels), fortify (180 channels), attack (180 channels), hurt (180 channels), die (180 channels).
- SHA-256: `09e809ecf1db28faa69da8d9edb0bf9422148645bd5a25cc2cfbf69dcc01415e`.
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

- Model: `src/Mods/Military/art/models/modern_infantry.glb` (353,036 bytes).
- Art: `ART_DEF_UNIT_MODERNINFANTRY`. Source scale: 0.44.
- NIF: `art/units/modern infantry/european/moderninfantry.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 783 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3682, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `ebb90fcc3856afa217f41b1c36cdcaee91d5bfe716eb5833ccfcb3523ea6e655`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:modern_marine

- Model: `src/Mods/Military/art/models/modern_marine.glb` (380,292 bytes).
- Art: `ART_DEF_UNIT_MODERN_MARINE`. Source scale: 0.44.
- NIF: `art/units/modern marine/moderninfantry.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 823 triangles, 1 skins, 15 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3698, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `6c0f158deb920a79ae1ee2df025577f5843da27ca80638b3833eb9873543bc00`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:modern_paratrooper

- Model: `src/Mods/Military/art/models/modern_paratrooper.glb` (846,936 bytes).
- Art: `ART_DEF_UNIT_MODERN_PARATROOPER`. Source scale: 0.44.
- NIF: `art/units/modern paratrooper/paratrooper.nif`.
- KFM: `art/units/modern paratrooper/paratrooper.kfm`.
- Geometry: 1487 triangles, 2 skins, 25 joint bindings, 8 textures.
- Bounds: height 0.6000, radius 0.1996, feet Y=0.
- Clips: idle (132 channels), run (132 channels), fortify (132 channels), attack (132 channels), die (132 channels).
- SHA-256: `b2c30a9f2c1d4a3ee8a6a6b97518aba647a58e52052ca38931b6c867f022372b`.
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

- Model: `src/Mods/Military/art/models/modern_workboat.glb` (261,580 bytes).
- Art: `ART_DEF_UNIT_WORKBOAT_MODERN`. Source scale: 0.28.
- NIF: `art/units/workboat_modern/workboat_modern.nif`.
- KFM: `art/units/workboat_modern/workboat_modern.kfm`.
- Geometry: 489 triangles, 3 skins, 7 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3644, feet Y=0.
- Clips: idle (13 channels), run (13 channels), fortify (13 channels), die (13 channels).
- SHA-256: `43d9c36e3ddd0c516b0a127f3a4aad1c629265e98e009fe903763102528208b3`.
- idle: `art/units/workboat_modern/workboat_modern_md_idle.kf`.
- run: `art/units/workboat_modern/workboat_modern_md_run.kf`.
- fortify: `art/units/workboat_modern/workboat_modern_md_fortifya.kf`.
- die: `art/units/workboat_modern/workboat_modern_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:mongol_keshik

- Model: `src/Mods/Military/art/models/mongol_keshik.glb` (687,088 bytes).
- Art: `ART_DEF_UNIT_MONGOL_KESHIK`. Source scale: 0.42.
- NIF: `art/units/unique/mongol/keshik/mongelkeshik.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1148 triangles, 3 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3651, feet Y=0.
- Clips: idle (119 channels), run (119 channels), fortify (119 channels), attack (119 channels), hurt (119 channels), die (119 channels).
- SHA-256: `974517c46411c7b3be40cc0da807013f9a4840db7c5811fd879953802eb227b5`.
- idle: `art/units/unique/mongol/keshik/mongelkeshik_md_idle.kf`.
- run: `art/units/unique/mongol/keshik/mongelkeshik_md_run.kf`.
- fortify: `art/units/unique/mongol/keshik/mongelkeshik_md_fortifya.kf`.
- attack: `art/units/unique/mongol/keshik/mongelkeshik_md_strikea.kf`.
- hurt: `art/units/unique/mongol/keshik/mongelkeshik_md_hurta.kf`.
- die: `art/units/unique/mongol/keshik/mongelkeshik_md_diea.kf`.

### military:moonbean_executive

- Model: `src/Mods/Military/art/models/moonbean_executive.glb` (261,368 bytes).
- Art: `ART_DEF_UNIT_CEO_MOONBEAN_EXECUTIVE`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_mb.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3892, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `c63f93086b3eb355b482323e8578b14d6a526950d910a9d15bdb1dd4d86f6b03`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:motorcycle

- Model: `src/Mods/Military/art/models/motorcycle.glb` (962,772 bytes).
- Art: `ART_DEF_UNIT_MOTORCYCLE`. Source scale: 0.45.
- NIF: `art/units/motorcycle/wehrmachtbiker.nif`.
- KFM: `art/units/motorcycle/wehrmachtbiker.kfm`.
- Geometry: 1674 triangles, 3 skins, 32 joint bindings, 3 textures.
- Bounds: height 0.3201, radius 0.4800, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `a1e4b98c5188d8da77aefdb3420bf13fc23cfa3a53d9dfbb27fa57af64064e05`.
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

- Model: `src/Mods/Military/art/models/mounted_infantry.glb` (666,456 bytes).
- Art: `ART_DEF_UNIT_MOUNTED_INFANTRY`. Source scale: 0.42.
- NIF: `art/units/mounted infantry/european/mswordsman_light.nif`.
- KFM: `art/units/horse archer/greek/companion_cavalry.kfm`.
- Geometry: 1114 triangles, 3 skins, 38 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2868, feet Y=0.
- Clips: idle (116 channels), run (116 channels), fortify (116 channels), attack (116 channels), hurt (116 channels), die (116 channels).
- SHA-256: `3b74207b80a267bb3333dcc4fca5989c92d85ed29ca95e9ddf414c59512f2636`.
- idle: `art/units/horse archer/greek/companion_cavalry_md_idle.kf`.
- run: `art/units/horse archer/greek/companion_cavalry_md_run.kf`.
- fortify: `art/units/horse archer/greek/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/horse archer/greek/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/horse archer/greek/companion_cavalry_md_hurta.kf`.
- die: `art/units/horse archer/greek/companion_cavalry_md_diea.kf`.

### military:mounted_rifleman

- Model: `src/Mods/Military/art/models/mounted_rifleman.glb` (855,868 bytes).
- Art: `ART_DEF_UNIT_MOUNTED_RIFLEMAN`. Source scale: 0.42.
- NIF: `art/units/mounted rifleman/european/cavalry_fx.nif`.
- KFM: `art/units/cavalry/cavalry.kfm`.
- Geometry: 1156 triangles, 4 skins, 38 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3014, feet Y=0.
- Clips: idle (154 channels), run (154 channels), fortify (154 channels), attack (154 channels), hurt (154 channels), die (154 channels).
- SHA-256: `d7296cd99376eecc0a433751390b32af7a07c8d8b498aa39c30a33bd38306f46`.
- idle: `art/units/cavalry/cavalry_md_idle.kf`.
- run: `art/units/cavalry/cavalry_md_run.kf`.
- fortify: `art/units/cavalry/cavalry_md_fortifya.kf`.
- attack: `art/units/cavalry/cavalry_md_strikea.kf`.
- hurt: `art/units/cavalry/cavalry_md_hurta.kf`.
- die: `art/units/cavalry/cavalry_md_diea.kf`.
- Note: Hidden geometry omitted: CavalryRider01

### military:mrbm

- Model: `src/Mods/Military/art/models/mrbm.glb` (128,128 bytes).
- Art: `ART_DEF_UNIT_MRBM`. Source scale: 0.85.
- NIF: `art/units/nukes/mrbm/icbm.nif`.
- KFM: `art/units/nukes/mrbm/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `d79215b37ca6c974a22c9d8c6341f03dbc30fedcd479a68153a3af912d05288c`.
- idle: `art/units/nukes/mrbm/icbm_md_idle.kf`.
- run: `art/units/nukes/mrbm/icbm_md_run.kf`.
- attack: `art/units/nukes/mrbm/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:musketman

- Model: `src/Mods/Military/art/models/musketman.glb` (376,148 bytes).
- Art: `ART_DEF_UNIT_MUSKETMAN`. Source scale: 0.44.
- NIF: `art/units/musketman/european/redcoat.nif`.
- KFM: `art/units/unique/england/redcoat/redcoat.kfm`.
- Geometry: 800 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2461, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `3b8d44ad8bcccc34a30b3c8ffd75b721e321eb9bb1cc2802ed4ac905ed0bdef3`.
- idle: `art/units/unique/england/redcoat/redcoat_md_idle.kf`.
- run: `art/units/unique/england/redcoat/redcoat_md_run.kf`.
- fortify: `art/units/unique/england/redcoat/redcoat_md_rangedfortify.kf`.
- attack: `art/units/unique/england/redcoat/redcoat_md_rangedstrike.kf`.
- hurt: `art/units/unique/england/redcoat/redcoat_md_hurta.kf`.
- die: `art/units/unique/england/redcoat/redcoat_md_rangeddiea.kf`.

### military:nagualism_missionary

- Model: `src/Mods/Military/art/models/nagualism_missionary.glb` (410,152 bytes).
- Art: `ART_DEF_UNIT_TOLTEC_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/tlenamacac/greatprophet.nif`.
- KFM: `art/units/missionary/tlenamacac/greatprophet.kfm`.
- Geometry: 1540 triangles, 2 skins, 34 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3106, feet Y=0.
- Clips: idle (91 channels), run (91 channels), fortify (91 channels).
- SHA-256: `c973e6b2286381917f309b5afc49e49bab854a7d4045a3d834e79516998eb8d8`.
- idle: `art/units/missionary/tlenamacac/greatprophet_md_idle.kf`.
- run: `art/units/missionary/tlenamacac/greatprophet_md_run.kf`.
- fortify: `art/units/missionary/tlenamacac/greatprophet_md_fortifya.kf`.
- Note: Missing texture: \\shamstaff.dds
- Note: Missing texture: \\skull.dds

### military:nanite_cloud

- Model: `src/Mods/Military/art/models/nanite_cloud.glb` (712,200 bytes).
- Art: `ART_DEF_UNIT_ENTITY`. Source scale: 0.6.
- NIF: `art/units/afterworld_boss/afterworld_boss.nif`.
- KFM: `art/units/afterworld_boss/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.2449, radius 0.4800, feet Y=0.
- Clips: idle (64 channels), attack (64 channels), die (64 channels).
- SHA-256: `81f97db420a3cdfb27b1ef91bce57701f446aa199a6bd22f3aefe7f99cbb4852`.
- idle: `art/units/afterworld_boss/afterworld_boss_md_idle.kf`.
- attack: `art/units/afterworld_boss/afterworld_boss_md_strikea.kf`.
- die: `art/units/afterworld_boss/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:nanite_spy

- Model: `src/Mods/Military/art/models/nanite_spy.glb` (694,440 bytes).
- Art: `ART_DEF_UNIT_NANITE_SPY`. Source scale: 0.1.
- NIF: `art/units/nanite spy/afterworld_boss.nif`.
- KFM: `art/units/nanite spy/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.2449, radius 0.4800, feet Y=0.
- Clips: idle (64 channels), attack (64 channels), die (64 channels).
- SHA-256: `9a8ea56be8e772f98dce4acaa28db72f43ee10689600f796ddaba191b80e24c1`.
- idle: `art/units/nanite spy/afterworld_boss_md_idle.kf`.
- attack: `art/units/nanite spy/afterworld_boss_md_strikea.kf`.
- die: `art/units/nanite spy/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:nanite_swarm

- Model: `src/Mods/Military/art/models/nanite_swarm.glb` (695,232 bytes).
- Art: `ART_DEF_UNIT_NANITE_SWARM`. Source scale: 0.5.
- NIF: `art/units/nanite swarm/afterworld_boss.nif`.
- KFM: `art/units/nanite swarm/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.2449, radius 0.4800, feet Y=0.
- Clips: idle (64 channels), attack (64 channels), die (64 channels).
- SHA-256: `f3e50e819494d9c1a312decb0ece62399acd7b7ff3ddf620c6af6d21da306ee8`.
- idle: `art/units/nanite swarm/afterworld_boss_md_idle.kf`.
- attack: `art/units/nanite swarm/afterworld_boss_md_strikea.kf`.
- die: `art/units/nanite swarm/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:native_america_dog_soldier

- Model: `src/Mods/Military/art/models/native_america_dog_soldier.glb` (561,360 bytes).
- Art: `ART_DEF_UNIT_NATIVE_AMERICA_DOG_SOLDIER`. Source scale: 0.44.
- NIF: `art/units/unique_nativeamerica_dogsoldier/dogsoldier.nif`.
- KFM: `art/units/unique_nativeamerica_dogsoldier/dogsoldier.kfm`.
- Geometry: 1014 triangles, 4 skins, 23 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2688, feet Y=0.
- Clips: idle (111 channels), run (111 channels), fortify (111 channels), attack (111 channels), hurt (111 channels), die (111 channels).
- SHA-256: `24596fce825b6e386ce6aa5fe4495e795742ce315c955a06df75278c6cbdf057`.
- idle: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_idle.kf`.
- run: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_run.kf`.
- fortify: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_fortifya.kf`.
- attack: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_strikea.kf`.
- hurt: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_hurta.kf`.
- die: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_diea.kf`.

### military:netherlands_oostindievaarder

- Model: `src/Mods/Military/art/models/netherlands_oostindievaarder.glb` (263,160 bytes).
- Art: `ART_DEF_UNIT_NETHERLANDS_OOSTINDIEVAARDER`. Source scale: 0.2.
- NIF: `art/units/dutch_east_india_man/dutch_east_india_man.nif`.
- KFM: `art/units/dutch_east_india_man/dutch_east_india_man.kfm`.
- Geometry: 844 triangles, 3 skins, 27 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4458, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `86f8a903357fcf9df72b225e8bc5bd0b2d52757fa5fdaa1728192ccf3f579ab9`.
- idle: `art/units/dutch_east_india_man/dutch_east_india_man_md_idle.kf`.
- run: `art/units/dutch_east_india_man/dutch_east_india_man_md_run.kf`.
- fortify: `art/units/dutch_east_india_man/dutch_east_india_man_md_fortifya.kf`.
- attack: `art/units/dutch_east_india_man/dutch_east_india_man_md_strikea.kf`.
- hurt: `art/units/dutch_east_india_man/dutch_east_india_man_md_hurta.kf`.
- die: `art/units/dutch_east_india_man/dutch_east_india_man_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:nlos_cannon

- Model: `src/Mods/Military/art/models/nlos_cannon.glb` (370,604 bytes).
- Art: `ART_DEF_UNIT_NLOS_CANNON`. Source scale: 0.57.
- NIF: `art/units/nlos cannon/disari.nif`.
- KFM: `art/units/nlos cannon/mobileartillery.kfm`.
- Geometry: 1306 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.3093, radius 0.4800, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels), attack (26 channels), hurt (26 channels).
- SHA-256: `4f7b27475df81108dc39c149e3802099d6ab09175bf813acb04ce5851976c607`.
- idle: `art/units/nlos cannon/mobileartillery_md_idle.kf`.
- run: `art/units/nlos cannon/mobileartillery_md_run.kf`.
- fortify: `art/units/nlos cannon/mobileartillery_md_rangedfortify.kf`.
- attack: `art/units/nlos cannon/mobileartillery_md_rangedstrike.kf`.
- hurt: `art/units/nlos cannon/mobileartillery_md_hurta.kf`.

### military:nuclear_carrier

- Model: `src/Mods/Military/art/models/nuclear_carrier.glb` (287,316 bytes).
- Art: `ART_DEF_UNIT_NUCLEAR_CARRIER`. Source scale: 1.1.
- NIF: `art/units/carrier/carrier.nif`.
- KFM: `art/units/carrier/carrier.kfm`.
- Geometry: 603 triangles, 2 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.2029, radius 0.4800, feet Y=0.
- Clips: idle (36 channels), run (36 channels), fortify (36 channels), attack (36 channels), hurt (36 channels), die (36 channels).
- SHA-256: `56ed7af465e9558efc7616df379327cddb3000b9a419da514192b3f12bc0c424`.
- idle: `art/units/carrier/carrier_md_idle.kf`.
- run: `art/units/carrier/carrier_md_run.kf`.
- fortify: `art/units/carrier/carrier_md_fortifya.kf`.
- attack: `art/units/carrier/carrier_md_strikea.kf`.
- hurt: `art/units/carrier/carrier_md_hurta.kf`.
- die: `art/units/carrier/carrier_md_diea.kf`.

### military:nuclear_submarine

- Model: `src/Mods/Military/art/models/nuclear_submarine.glb` (174,812 bytes).
- Art: `ART_DEF_UNIT_NUCLEAR_SUBMARINE`. Source scale: 0.75.
- NIF: `art/units/missile submarine/american/ohio.nif`.
- KFM: `art/units/submarine/submarine.kfm`.
- Geometry: 620 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.1843, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), fortify (28 channels), attack (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `557bdd18893d2d303c452858db6d04d4bfb14614bb518044269f7d0cdf5ec26c`.
- idle: `art/units/submarine/submarine_md_idle.kf`.
- run: `art/units/submarine/submarine_md_run.kf`.
- fortify: `art/units/submarine/submarine_md_fortifya.kf`.
- attack: `art/units/submarine/submarine_md_strikea.kf`.
- hurt: `art/units/submarine/submarine_md_hurta.kf`.
- die: `art/units/submarine/submarine_md_diea.kf`.

### military:orbital_bomber

- Model: `src/Mods/Military/art/models/orbital_bomber.glb` (128,264 bytes).
- Art: `ART_DEF_UNIT_ORBITAL_BOMBER`. Source scale: 0.5.
- NIF: `art/units/thunderbolt/jf_smart.nif`.
- KFM: `none`.
- Geometry: 676 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.1202, radius 0.4800, feet Y=0.
- Clips: none.
- SHA-256: `508cd72400b2e30ca0f888b9f0590fc60b105b41fb88e09d643ed6e0b633ed9f`.

### military:orbital_fighter

- Model: `src/Mods/Military/art/models/orbital_fighter.glb` (259,536 bytes).
- Art: `ART_DEF_UNIT_ORBITAL_FIGHTER`. Source scale: 0.45.
- NIF: `art/units/hammerheadrefar/jetfighter.nif`.
- KFM: `art/units/hammerheadrefar/jetfighter.kfm`.
- Geometry: 1001 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.1120, radius 0.4800, feet Y=0.
- Clips: idle (31 channels), run (31 channels), hurt (31 channels).
- SHA-256: `f497227092e991a84f00f0146fe951bcef4cf1c35a7e7651e3c02e5675c7b9b1`.
- idle: `art/units/hammerheadrefar/jetfighter_md_idle.kf`.
- run: `art/units/hammerheadrefar/jetfighter_md_run.kf`.
- hurt: `art/units/hammerheadrefar/jetfighter_md_hurt.kf`.

### military:ottoman_janissary

- Model: `src/Mods/Military/art/models/ottoman_janissary.glb` (306,584 bytes).
- Art: `ART_DEF_UNIT_OTTOMAN_JANISSARY`. Source scale: 0.44.
- NIF: `art/units/janissary/janissary.nif`.
- KFM: `art/units/janissary/janissary.kfm`.
- Geometry: 696 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2583, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `a780e264c9b8d53290c76ac35be7bfb5376251e78081f3e2cc7a3d7d56815a20`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `a794b9793cde66fa5d05c3f452d06876226a43661daebbeea20f9af213d6cd4f`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:paratrooper

- Model: `src/Mods/Military/art/models/paratrooper.glb` (481,912 bytes).
- Art: `ART_DEF_UNIT_PARATROOPER`. Source scale: 0.44.
- NIF: `art/units/paratrooper/paratrooper.nif`.
- KFM: `art/units/paratrooper/paratrooper.kfm`.
- Geometry: 882 triangles, 2 skins, 26 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.1996, feet Y=0.
- Clips: idle (90 channels), run (90 channels), fortify (90 channels), attack (90 channels), die (90 channels).
- SHA-256: `c8c710071a825f467095417310b77388a7426e94749a1d02e8f33c6055ab70d7`.
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
- Bounds: height 0.6000, radius 0.3881, feet Y=0.
- Clips: idle (107 channels), run (107 channels), fortify (107 channels), attack (107 channels), hurt (107 channels), die (107 channels).
- SHA-256: `1796382cdb421052cddc1d321d67b03a266c4a55755d2c50a3574bda049241a5`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:picts_cruithni

- Model: `src/Mods/Military/art/models/picts_cruithni.glb` (268,780 bytes).
- Art: `ART_DEF_UNIT_PICTS_CRUITHNI`. Source scale: 0.44.
- NIF: `art/picts/cruithni/swordsman_fx.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3265, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `1bf81b090c1db9bff4cd15003c0b5fc4d9fec0f82bd733f60c9e170de15af4de`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:pikeman

- Model: `src/Mods/Military/art/models/pikeman.glb` (318,248 bytes).
- Art: `ART_DEF_UNIT_PIKEMAN`. Source scale: 0.44.
- NIF: `art/units/pikeman/pikeman.nif`.
- KFM: `art/units/pikeman/pikeman.kfm`.
- Geometry: 864 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.4461, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `4757f574076ebcd2dbcf5bf79f77e01e4c2f6994c196b177a0b33229f2c73c2e`.
- idle: `art/units/pikeman/pikeman_md_idle.kf`.
- run: `art/units/pikeman/pikeman_md_run.kf`.
- fortify: `art/units/pikeman/pikeman_md_fortifya.kf`.
- attack: `art/units/pikeman/pikeman_md_strikea.kf`.
- hurt: `art/units/pikeman/pikeman_md_hurta.kf`.
- die: `art/units/pikeman/pikeman_md_diea.kf`.

### military:pioneer

- Model: `src/Mods/Military/art/models/pioneer.glb` (250,304 bytes).
- Art: `ART_DEF_UNIT_PIONEER_MALE`. Source scale: 0.46.
- NIF: `art/units/settler/settler_male.nif`.
- KFM: `art/units/settler/settler_male.kfm`.
- Geometry: 700 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3317, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `c342b2acc7673e5f6668d245b9909b542bf5137b2493d497a4e6e1fe21bae56f`.
- idle: `art/units/settler/settler_male_md_idle.kf`.
- run: `art/units/settler/settler_male_md_run.kf`.
- fortify: `art/units/settler/settler_male_md_fortify.kf`.
- die: `art/units/settler/settler_male_md_idle_die.kf`.
- Note: First of 7 formation art groups exported

### military:plasma_armor

- Model: `src/Mods/Military/art/models/plasma_armor.glb` (340,352 bytes).
- Art: `ART_DEF_UNIT_HELLHOUND_ARMOR`. Source scale: 0.3.
- NIF: `art/units/plasma armor/hellhound.nif`.
- KFM: `art/units/plasma armor/hellhound.kfm`.
- Geometry: 584 triangles, 4 skins, 4 joint bindings, 3 textures.
- Bounds: height 0.3229, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels).
- SHA-256: `a0700c9c31fbd048e04b616f2e73faf47adfd01b48a155f4874fcde8eb5c69b0`.
- idle: `art/units/plasma armor/hellhound_md_idle.kf`.
- run: `art/units/plasma armor/hellhound_md_run.kf`.
- fortify: `art/units/plasma armor/hellhound_md_rangedfortify.kf`.
- attack: `art/units/plasma armor/hellhound_md_rangedstrike.kf`.
- hurt: `art/units/plasma armor/hellhound_md_hurta.kf`.

### military:poison_nova

- Model: `src/Mods/Military/art/models/poison_nova.glb` (125,320 bytes).
- Art: `ART_DEF_UNIT_POISON_NOVA`. Source scale: 0.85.
- NIF: `art/units/nukes/poisonnova/icbm.nif`.
- KFM: `art/units/nukes/poisonnova/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `98247f7356c5d3ffbf2a9e5ef7e9e74b188d1e78d19a65b0298b1a1a6d3572f9`.
- idle: `art/units/nukes/poisonnova/icbm_md_idle.kf`.
- run: `art/units/nukes/poisonnova/icbm_md_run.kf`.
- attack: `art/units/nukes/poisonnova/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:poison_nuke

- Model: `src/Mods/Military/art/models/poison_nuke.glb` (126,292 bytes).
- Art: `ART_DEF_UNIT_POISON_NUKE`. Source scale: 0.85.
- NIF: `art/units/nukes/poisonnuke/icbm.nif`.
- KFM: `art/units/nukes/poisonnuke/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `014c69926567bb0a622a1a0edfac1dac609ca3aba96726c00ab10556df7c4824`.
- idle: `art/units/nukes/poisonnuke/icbm_md_idle.kf`.
- run: `art/units/nukes/poisonnuke/icbm_md_run.kf`.
- attack: `art/units/nukes/poisonnuke/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:polarbear

- Model: `src/Mods/Military/art/models/polarbear.glb` (275,428 bytes).
- Art: `ART_DEF_UNIT_POLARBEAR`. Source scale: 0.7.
- NIF: `art/units/animals/polarbear/bear.nif`.
- KFM: `art/units/animals/polarbear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5203, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `80919b1ee5fd5e2ffcefa1238e8b08fe825076cd66c09d8eab58368edd4236d3`.
- idle: `art/units/animals/polarbear/bear_md_idle.kf`.
- run: `art/units/animals/polarbear/bear_md_run.kf`.
- fortify: `art/units/animals/polarbear/bear_md_fortifya.kf`.
- attack: `art/units/animals/polarbear/bear_md_strikea.kf`.
- hurt: `art/units/animals/polarbear/bear_md_hurta.kf`.
- die: `art/units/animals/polarbear/bear_md_diea.kf`.

### military:polish_heavy_hussar

- Model: `src/Mods/Military/art/models/polish_heavy_hussar.glb` (1,482,560 bytes).
- Art: `ART_DEF_UNIT_HEAVY_HUSSAR`. Source scale: 0.42.
- NIF: `art/poland/heavy hussar/heavycavalry.nif`.
- KFM: `art/poland/heavy hussar/heavycavalry.kfm`.
- Geometry: 1252 triangles, 2 skins, 35 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.3710, feet Y=0.
- Clips: idle (314 channels), run (314 channels), fortify (314 channels), attack (314 channels), hurt (314 channels), die (314 channels).
- SHA-256: `f31f997433ebcd2f0660ec67930bc10c993204a5e6843fccf93fe53551369ec6`.
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

- Model: `src/Mods/Military/art/models/portugal_carrack.glb` (224,292 bytes).
- Art: `ART_DEF_UNIT_PORTUGAL_CARRACK`. Source scale: 0.24.
- NIF: `art/units/portugal_carrack/portugal_carrack.nif`.
- KFM: `art/units/portugal_carrack/portugal_carrack.kfm`.
- Geometry: 482 triangles, 3 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4509, feet Y=0.
- Clips: idle (38 channels), run (38 channels), fortify (38 channels), attack (38 channels), hurt (38 channels), die (38 channels).
- SHA-256: `caaf0b228f8f88e01f010aade78d8902429e00d2d13222ef4109e03601c61c74`.
- idle: `art/units/portugal_carrack/portugal_carrack_md_idle.kf`.
- run: `art/units/portugal_carrack/portugal_carrack_md_run.kf`.
- fortify: `art/units/portugal_carrack/portugal_carrack_md_fortifya.kf`.
- attack: `art/units/portugal_carrack/portugal_carrack_md_strikea.kf`.
- hurt: `art/units/portugal_carrack/portugal_carrack_md_hurta.kf`.
- die: `art/units/portugal_carrack/portugal_carrack_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:power_armored_infantry

- Model: `src/Mods/Military/art/models/power_armored_infantry.glb` (301,648 bytes).
- Art: `ART_DEF_UNIT_POWER_ARMOR_INFANTRY`. Source scale: 0.75.
- NIF: `art/units/power armor infantry/warhammer.nif`.
- KFM: `art/units/power armor infantry/warhammer.kfm`.
- Geometry: 1568 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2592, feet Y=0.
- Clips: idle (57 channels), run (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `064cdb65c6b93af9960889d4aa21c15d445ea6d893e364b097e33401352be76c`.
- idle: `art/units/power armor infantry/warhammer_md_idle.kf`.
- run: `art/units/power armor infantry/warhammer_md_run.kf`.
- attack: `art/units/power armor infantry/warhammer_md_strikeb.kf`.
- hurt: `art/units/power armor infantry/warhammer_md_hurta.kf`.
- die: `art/units/power armor infantry/warhammer_md_dieb.kf`.

### military:pre_dreadnought

- Model: `src/Mods/Military/art/models/pre_dreadnought.glb` (353,904 bytes).
- Art: `ART_DEF_UNIT_PRE_DREADNOUGHT`. Source scale: 0.6.
- NIF: `art/units/ironclad battleship/hms_canopus.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 1598 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.4166, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `29dd6661f45b943334d3ea52334da3e8bbb43be584d2307336c7925709c54e07`.
- idle: `art/units/destroyer/destroyer_md_idle.kf`.
- run: `art/units/destroyer/destroyer_md_run.kf`.
- fortify: `art/units/destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/destroyer/destroyer_md_hurta.kf`.
- die: `art/units/destroyer/destroyer_md_diea.kf`.

### military:precision_attack_missile

- Model: `src/Mods/Military/art/models/precision_attack_missile.glb` (101,776 bytes).
- Art: `ART_DEF_UNIT_PRECISION_ATTACK_MISSILE`. Source scale: 0.3.
- NIF: `art/units/precision attack missile/tomahawk.nif`.
- KFM: `art/units/guided_missile/guided_missile.kfm`.
- Geometry: 286 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.2314, radius 0.4800, feet Y=0.
- Clips: idle (19 channels), run (19 channels), attack (19 channels).
- SHA-256: `aca527cede9459e8d5476187c97d9e14da10b1d6ce4ac79b58597ba81d1fbe57`.
- idle: `art/units/guided_missile/guided_missile_md_idle.kf`.
- run: `art/units/guided_missile/guided_missile_md_run.kf`.
- attack: `art/units/guided_missile/guided_missile_md_strikea.kf`.

### military:privateer

- Model: `src/Mods/Military/art/models/privateer.glb` (459,548 bytes).
- Art: `ART_DEF_UNIT_PRIVATEER`. Source scale: 0.2.
- NIF: `art/units/privateer/privateer.nif`.
- KFM: `art/units/privateer/privateer.kfm`.
- Geometry: 923 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3946, feet Y=0.
- Clips: idle (108 channels), run (108 channels), fortify (108 channels), attack (108 channels), hurt (108 channels), die (108 channels).
- SHA-256: `9f200262569f618d94cefaa9a20f50701a23b61dbea35643502afbf1c567ae05`.
- idle: `art/units/privateer/privateer_md_idle.kf`.
- run: `art/units/privateer/privateer_md_run.kf`.
- fortify: `art/units/privateer/privateer_md_fortifya.kf`.
- attack: `art/units/privateer/privateer_md_strikea.kf`.
- hurt: `art/units/privateer/privateer_md_hurta.kf`.
- die: `art/units/privateer/privateer_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:prophet

- Model: `src/Mods/Military/art/models/prophet.glb` (171,588 bytes).
- Art: `ART_DEF_UNIT_PROPHET`. Source scale: 0.46.
- NIF: `art/units/greatpeople/prophet/greatprophet.nif`.
- KFM: `art/units/greatpeople/prophet/greatprophet.kfm`.
- Geometry: 552 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2845, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels).
- SHA-256: `c5e1522f47096939b2599e70cdac79b05435dcfea21788fa8d5827345610543d`.
- idle: `art/units/greatpeople/prophet/greatprophet_md_idle.kf`.
- run: `art/units/greatpeople/prophet/greatprophet_md_run.kf`.
- fortify: `art/units/greatpeople/prophet/greatprophet_md_fortifya.kf`.

### military:ram

- Model: `src/Mods/Military/art/models/ram.glb` (191,092 bytes).
- Art: `ART_DEF_UNIT_RAM`. Source scale: 0.70.
- NIF: `art/units/logram/logram.nif`.
- KFM: `none`.
- Geometry: 2352 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.4471, radius 0.4800, feet Y=0.
- Clips: none.
- SHA-256: `2082dc782158714eb975889bddc28c0040b6cab9116cbc8bf7e1a92eb60329f7`.

### military:rifleman

- Model: `src/Mods/Military/art/models/rifleman.glb` (280,216 bytes).
- Art: `ART_DEF_UNIT_RIFLEMAN`. Source scale: 0.44.
- NIF: `art/units/rifleman/rifleman.nif`.
- KFM: `art/units/rifleman/rifleman.kfm`.
- Geometry: 618 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2539, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `ac401afe3d1ddae97c31bfc643a11772f77611e4b254d62d101c2ed90508f310`.
- idle: `art/units/rifleman/rifleman_md_idle.kf`.
- run: `art/units/rifleman/rifleman_md_run.kf`.
- fortify: `art/units/rifleman/rifleman_md_rangedfortify.kf`.
- attack: `art/units/rifleman/rifleman_md_rangedstrike.kf`.
- hurt: `art/units/rifleman/rifleman_md_hurta.kf`.
- die: `art/units/rifleman/rifleman_md_rangeddiea.kf`.

### military:rocket_artillery

- Model: `src/Mods/Military/art/models/rocket_artillery.glb` (248,268 bytes).
- Art: `ART_DEF_UNIT_ROCKET_ARTILLERY`. Source scale: 0.7.
- NIF: `art/units/rocket artillery/m270.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 400 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.3341, radius 0.4800, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels).
- SHA-256: `c93c46fc8e7c302d7a6518eac3979934202fdb402cf48d4355d5354ce23e2bbf`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:rome_praetorian

- Model: `src/Mods/Military/art/models/rome_praetorian.glb` (280,464 bytes).
- Art: `ART_DEF_UNIT_ROME_PRAETORIAN`. Source scale: 0.44.
- NIF: `art/units/unique/roman/praetorian/praetorian.nif`.
- KFM: `art/units/unique/roman/praetorian/praetorian.kfm`.
- Geometry: 810 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3283, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `801667c95e318cc76f07fc36cd2ab5785ad1dae6c4427f694284a85654637bde`.
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
- SHA-256: `6a2bcfd102a664195d6fbcbae24b8c9468e6b19a2cf7627fd9da490eb41256c4`.
- idle: `art/units/rover/rover_md_idle.kf`.
- run: `art/units/rover/rover_md_run.kf`.
- fortify: `art/units/rover/rover_md_rangedfortify.kf`.
- attack: `art/units/rover/rover_md_rangedstrike.kf`.
- hurt: `art/units/rover/rover_md_rangedhurta.kf`.

### military:russia_cossack

- Model: `src/Mods/Military/art/models/russia_cossack.glb` (802,468 bytes).
- Art: `ART_DEF_UNIT_RUSSIA_COSSACK`. Source scale: 0.42.
- NIF: `art/units/unique/russia/cossack/russiancossack.nif`.
- KFM: `art/units/unique/russia/cossack/russiancossack.kfm`.
- Geometry: 1076 triangles, 3 skins, 40 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3018, feet Y=0.
- Clips: idle (139 channels), run (139 channels), fortify (139 channels), attack (139 channels), hurt (139 channels), die (139 channels).
- SHA-256: `010b4ecb86b34a06cd5bdb9530b767603e68b7333c29729a58e82b7be0529ba9`.
- idle: `art/units/unique/russia/cossack/russiancossack_md_idle.kf`.
- run: `art/units/unique/russia/cossack/russiancossack_md_run.kf`.
- fortify: `art/units/unique/russia/cossack/russiancossack_md_fortifya.kf`.
- attack: `art/units/unique/russia/cossack/russiancossack_md_strikea.kf`.
- hurt: `art/units/unique/russia/cossack/russiancossack_md_hurta.kf`.
- die: `art/units/unique/russia/cossack/russiancossack_md_diea.kf`.

### military:sam_infantry

- Model: `src/Mods/Military/art/models/sam_infantry.glb` (378,456 bytes).
- Art: `ART_DEF_UNIT_SAM_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/saminfantry/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 754 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3298, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `95cf4f4463f4776c06bad8bfab159a50cffc81c768cc4119beae96a64de37583`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.

### military:scientist

- Model: `src/Mods/Military/art/models/scientist.glb` (199,204 bytes).
- Art: `ART_DEF_UNIT_SCIENTIST`. Source scale: 0.46.
- NIF: `art/units/greatpeople/scientist/greatscientistancient.nif`.
- KFM: `art/units/greatpeople/scientist/greatscientistancient.kfm`.
- Geometry: 788 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3386, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels).
- SHA-256: `7f6d32993fa86308f8edcd5ecb18278847c387134d1475d3bf9e1249d64265b5`.
- idle: `art/units/greatpeople/scientist/greatscientistancient_md_idle.kf`.
- run: `art/units/greatpeople/scientist/greatscientistancient_md_run.kf`.
- fortify: `art/units/greatpeople/scientist/greatscientistancient_md_fortify.kf`.

### military:scout

- Model: `src/Mods/Military/art/models/scout.glb` (307,928 bytes).
- Art: `ART_DEF_UNIT_SCOUT`. Source scale: 0.44.
- NIF: `art/units/scout/scout.nif`.
- KFM: `art/units/scout/scout.kfm`.
- Geometry: 740 triangles, 2 skins, 23 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3252, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels), die (48 channels).
- SHA-256: `b90f320018cdc67fde20b23cadc878ba08221b5cf71f77b990cadae3b83ef671`.
- idle: `art/units/scout/scout_md_idle.kf`.
- run: `art/units/scout/scout_md_run.kf`.
- fortify: `art/units/scout/scout_md_fortifya.kf`.
- attack: `art/units/scout/scout_md_strikea.kf`.
- hurt: `art/units/scout/scout_md_hurta.kf`.
- die: `art/units/scout/scout_md_diea.kf`.

### military:scout_mech

- Model: `src/Mods/Military/art/models/scout_mech.glb` (490,632 bytes).
- Art: `ART_DEF_UNIT_SCOUT_MECH2`. Source scale: 1.5.
- NIF: `art/units/scout mech/mech.nif`.
- KFM: `art/units/scout mech/mech.kfm`.
- Geometry: 1519 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.5911, radius 0.4800, feet Y=0.
- Clips: idle (120 channels), run (120 channels), fortify (120 channels), attack (120 channels), hurt (120 channels).
- SHA-256: `71796470b827381d3c1bfdd0c07a1818ae0a66dec5bf0a8e92e47e6140f05faf`.
- idle: `art/units/scout mech/mech_md_idle.kf`.
- run: `art/units/scout mech/mech_md_run.kf`.
- fortify: `art/units/scout mech/mech_md_rangedfortify.kf`.
- attack: `art/units/scout mech/mech_md_rangedstrike.kf`.
- hurt: `art/units/scout mech/mech_md_rangedhurta.kf`.

### military:scramjet

- Model: `src/Mods/Military/art/models/scramjet.glb` (119,520 bytes).
- Art: `ART_DEF_UNIT_SCRAMJET`. Source scale: 0.48.
- NIF: `art/units/orbital fighters/spacefighter_y.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 142 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.2280, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), hurt (23 channels).
- SHA-256: `b170b28024f6ee277826d43a0b434ea714aec4585a816015b7bfdd35c218c8c8`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:seaplane

- Model: `src/Mods/Military/art/models/seaplane.glb` (205,264 bytes).
- Art: `ART_DEF_UNIT_SEAPLANE`. Source scale: 0.4.
- NIF: `art/units/seaplane/generic/e16a1.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 1129 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.3385, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `46fe581ad613ace1a5630795d5b44cc9a3ec5f307199c7f281ab90186fa0644b`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:settler

- Model: `src/Mods/Military/art/models/settler.glb` (250,304 bytes).
- Art: `ART_DEF_UNIT_SETTLER_MALE`. Source scale: 0.46.
- NIF: `art/units/settler/settler_male.nif`.
- KFM: `art/units/settler/settler_male.kfm`.
- Geometry: 700 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3317, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `c342b2acc7673e5f6668d245b9909b542bf5137b2493d497a4e6e1fe21bae56f`.
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
- Bounds: height 0.6000, radius 0.3424, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `e33516cf3e947e4c50e7646956002255f623f9f192cb6d9c8ffe7457a99ded7f`.
- idle: `art/units/warrior/warrior_md_idle.kf`.
- run: `art/units/warrior/warrior_md_run.kf`.
- fortify: `art/units/warrior/warrior_md_fortifya.kf`.
- attack: `art/units/warrior/warrior_md_strikea.kf`.
- hurt: `art/units/warrior/warrior_md_hurta.kf`.
- die: `art/units/warrior/warrior_md_diea.kf`.

### military:shinto_missionary

- Model: `src/Mods/Military/art/models/shinto_missionary.glb` (188,532 bytes).
- Art: `ART_DEF_UNIT_SHINTO_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/kannushi/confucian_missionary.nif`.
- KFM: `art/units/kannushi/confucian_missionary.kfm`.
- Geometry: 480 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2507, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), die (50 channels).
- SHA-256: `1bd33de3799e8ef595e0fb1cdd43f1dacf23c62c23634dfd5b9c3a24cfce3f3b`.
- idle: `art/units/kannushi/confucian_missionary_md_idle.kf`.
- run: `art/units/kannushi/confucian_missionary_md_run.kf`.
- fortify: `art/units/kannushi/confucian_missionary_md_fortifya.kf`.
- die: `art/units/kannushi/confucian_missionary_md_idle_die.kf`.

### military:ship_of_the_line

- Model: `src/Mods/Military/art/models/ship_of_the_line.glb` (178,080 bytes).
- Art: `ART_DEF_UNIT_SHIP_OF_THE_LINE`. Source scale: 0.22.
- NIF: `art/units/ship_0f_the_line/ship_0f_the_line.nif`.
- KFM: `art/units/ship_0f_the_line/ship_of_the_line.kfm`.
- Geometry: 1257 triangles, 2 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4446, feet Y=0.
- Clips: idle (1 channels), run (1 channels), fortify (1 channels), attack (1 channels), hurt (1 channels), die (1 channels).
- SHA-256: `05fb6dc99af326829397e0acf6b0963c2c14dd8353283404fd4fd511c4f9574a`.
- idle: `art/units/ship_0f_the_line/ship_of_the_line_md_idle.kf`.
- run: `art/units/ship_0f_the_line/ship_of_the_line_md_run.kf`.
- fortify: `art/units/ship_0f_the_line/ship_of_the_line_md_fortifya.kf`.
- attack: `art/units/ship_0f_the_line/ship_of_the_line_md_strikea.kf`.
- hurt: `art/units/ship_0f_the_line/ship_of_the_line_md_hurta.kf`.
- die: `art/units/ship_0f_the_line/ship_of_the_line_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:siam_elephant

- Model: `src/Mods/Military/art/models/siam_elephant.glb` (1,908,064 bytes).
- Art: `ART_DEF_UNIT_SIAM_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/unique/siamese/battle elephant/warelephant_european_fx.nif`.
- KFM: `art/units/unique/siamese/battle elephant/warelephant_carthage.kfm`.
- Geometry: 2813 triangles, 4 skins, 56 joint bindings, 9 textures.
- Bounds: height 0.6000, radius 0.2423, feet Y=0.
- Clips: idle (312 channels), run (312 channels), fortify (312 channels), attack (312 channels), hurt (312 channels), die (312 channels).
- SHA-256: `fcf56442875f237308e14dac8f7bae7f7af021a3d3573bd61838af56b5d38a20`.
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
- Bounds: height 0.4722, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `51f01faafa3f0e6f35bbe24a284211e94efe745e7adb75330c63cb7771d8f08d`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:siege_droid

- Model: `src/Mods/Military/art/models/siege_droid.glb` (912,572 bytes).
- Art: `ART_DEF_UNIT_SIEGE_DROID`. Source scale: 0.8.
- NIF: `art/units/siege droid/mech.nif`.
- KFM: `art/units/siege droid/mech.kfm`.
- Geometry: 2478 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.5480, radius 0.4800, feet Y=0.
- Clips: idle (267 channels), run (267 channels), fortify (267 channels), attack (267 channels), hurt (267 channels).
- SHA-256: `f77707f2b1849ce43928d36699f3c417675c197c24aea2619af1b423720302f7`.
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
- SHA-256: `91fc2a0c962bab1a22cc5ffeac5adea0d83e972aaa167a363e5777b006a0dacb`.
- idle: `art/units/siege ram/ram_md_idle.kf`.
- run: `art/units/siege ram/ram_md_run.kf`.
- fortify: `art/units/siege ram/ram_md_rangedfortify.kf`.
- attack: `art/units/siege ram/ram_md_rangedstrike.kf`.
- hurt: `art/units/siege ram/ram_md_hurta.kf`.

### military:slinger

- Model: `src/Mods/Military/art/models/slinger.glb` (708,424 bytes).
- Art: `ART_DEF_UNIT_SLINGER`. Source scale: 0.44.
- NIF: `art/units/peltast/peltast.nif`.
- KFM: `art/units/peltast/peltast.kfm`.
- Geometry: 792 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2981, feet Y=0.
- Clips: idle (103 channels), run (103 channels), fortify (103 channels), attack (103 channels), hurt (103 channels), die (103 channels).
- SHA-256: `216d1288606babd9aa7d5f8e44bf0017c4e8388665911f64ad8132eb50a7e6d6`.
- idle: `art/units/peltast/peltast_md_idle.kf`.
- run: `art/units/peltast/peltast_md_run.kf`.
- fortify: `art/units/peltast/peltast_md_ranged_fortify.kf`.
- attack: `art/units/peltast/peltast_md_rangedstrike.kf`.
- hurt: `art/units/peltast/peltast_md_hurta.kf`.
- die: `art/units/peltast/peltast_md_rangeddiea.kf`.

### military:sloop

- Model: `src/Mods/Military/art/models/sloop.glb` (279,164 bytes).
- Art: `ART_DEF_UNIT_CARAVEL_SIMPLERIG`. Source scale: 0.18.
- NIF: `art/units/sloop/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 797 triangles, 3 skins, 12 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4022, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels), attack (43 channels), hurt (43 channels), die (43 channels).
- SHA-256: `8deb2674c0f3f923f451391c90966fcd3a6cbb2b69e91dd964ebb9ff3d613f39`.
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
- SHA-256: `444c0127e73cacac03701ad9736894a3f8b5b22e1be8c9a88b5d32924d02f5d5`.
- idle: `art/units/unique/spanish/conquistador/conquistador_md_idle.kf`.
- run: `art/units/unique/spanish/conquistador/conquistador_md_run.kf`.
- fortify: `art/units/unique/spanish/conquistador/conquistador_md_fortifya.kf`.
- attack: `art/units/unique/spanish/conquistador/conquistador_md_strikea.kf`.
- hurt: `art/units/unique/spanish/conquistador/conquistador_md_hurta.kf`.
- die: `art/units/unique/spanish/conquistador/conquistador_md_diea.kf`.
- Note: Missing texture: Knight_horse_128.dds

### military:spearman

- Model: `src/Mods/Military/art/models/spearman.glb` (282,484 bytes).
- Art: `ART_DEF_UNIT_SPEARMAN`. Source scale: 0.44.
- NIF: `art/units/unique/greek/phalanx/greekphalanx.nif`.
- KFM: `art/units/unique/greek/phalanx/greekphalanx.kfm`.
- Geometry: 728 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4288, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `fb3302564c158e5a57ff859c1f064ee885b88cd107b4be963c62a3e0ae94bb9e`.
- idle: `art/units/unique/greek/phalanx/greekphalanx_md_idle.kf`.
- run: `art/units/unique/greek/phalanx/greekphalanx_md_run.kf`.
- fortify: `art/units/unique/greek/phalanx/greekphalanx_md_fortifya.kf`.
- attack: `art/units/unique/greek/phalanx/greekphalanx_md_strikea.kf`.
- hurt: `art/units/unique/greek/phalanx/greekphalanx_md_hurta.kf`.
- die: `art/units/unique/greek/phalanx/greekphalanx_md_diea.kf`.

### military:special_agent

- Model: `src/Mods/Military/art/models/special_agent.glb` (753,520 bytes).
- Art: `ART_DEF_UNIT_SPECIAL_AGENT`. Source scale: 0.46.
- NIF: `art/units/specialagent/specialagent.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 811 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4055, feet Y=0.
- Clips: idle (107 channels), run (107 channels), fortify (107 channels), attack (107 channels).
- SHA-256: `11af21f93a5e7105e376a1dab11f02ed7d512dcaaa74d99a1ddde881e254ae17`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:special_forces

- Model: `src/Mods/Military/art/models/special_forces.glb` (379,124 bytes).
- Art: `ART_DEF_UNIT_SPECIAL_FORCES`. Source scale: 0.44.
- NIF: `art/units/unique/america/navyseal/navyseal.nif`.
- KFM: `art/units/unique/america/navyseal/navyseal.kfm`.
- Geometry: 802 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3830, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `1e27e1a58466584f09633884b76fa1f9cb027a2187cdd4095ca53e9bb74f48c7`.
- idle: `art/units/unique/america/navyseal/navyseal_md_idle.kf`.
- run: `art/units/unique/america/navyseal/navyseal_md_run.kf`.
- fortify: `art/units/unique/america/navyseal/navyseal_md_rangedfortify.kf`.
- attack: `art/units/unique/america/navyseal/navyseal_md_rangedstrike.kf`.
- hurt: `art/units/unique/america/navyseal/navyseal_md_hurta.kf`.
- die: `art/units/unique/america/navyseal/navyseal_md_rangeddiea.kf`.

### military:special_infantry

- Model: `src/Mods/Military/art/models/special_infantry.glb` (395,980 bytes).
- Art: `ART_DEF_UNIT_SPACE_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/space infantry/marines.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 1190 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3417, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `7a4428e04ecf3a5c3d4f61380a25eccb750698cc09a6c5f7bd52da9d645e1ca0`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:spy

- Model: `src/Mods/Military/art/models/spy.glb` (408,156 bytes).
- Art: `ART_DEF_UNIT_ANCIENT_SPY`. Source scale: 0.46.
- NIF: `art/units/ancientspy/ancientspy.nif`.
- KFM: `art/units/ancientspy/ancientspy.kfm`.
- Geometry: 678 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4407, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels).
- SHA-256: `090979e3665ff89ac3387fd15c44d12ca7d4da96d2f70bab860e6264c3b83dad`.
- idle: `art/units/ancientspy/ancientspy_md_idle.kf`.
- run: `art/units/ancientspy/ancientspy_md_run.kf`.
- fortify: `art/units/ancientspy/ancientspy_md_fortify.kf`.
- attack: `art/units/ancientspy/ancientspy_md_rangedstrike.kf`.
- Note: Missing texture: Great_Spy_Ancient_DIFF.dds

### military:sr71

- Model: `src/Mods/Military/art/models/sr71.glb` (81,276 bytes).
- Art: `ART_DEF_UNIT_SR71`. Source scale: 0.48.
- NIF: `art/units/sr71/sr71.nif`.
- KFM: `art/units/jetfighter/jetfighter.kfm`.
- Geometry: 726 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.1231, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), hurt (23 channels).
- SHA-256: `8c778e4100ce7745d3a9bf7b632dc5757b0bff4510e6777d129e93e26ff756ed`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:stag

- Model: `src/Mods/Military/art/models/stag.glb` (712,084 bytes).
- Art: `ART_DEF_UNIT_STAG`. Source scale: 0.38.
- NIF: `art/units/stag/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 392 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2812, feet Y=0.
- Clips: idle (154 channels), run (154 channels), fortify (154 channels), attack (154 channels), hurt (154 channels), die (154 channels).
- SHA-256: `393ea8b4b049a18897f128bdece11ffbacd9ee4ce0512a4048e978e333ecea9a`.
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
- SHA-256: `e8b38708b6c16d203283530f0a3ea77ec8533739cf838692b089d82bb55d7101`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:stealth_armor

- Model: `src/Mods/Military/art/models/stealth_armor.glb` (359,068 bytes).
- Art: `ART_DEF_UNIT_STEALTH_ARMOR`. Source scale: 0.40.
- NIF: `art/units/stealth armor/myrmidon.nif`.
- KFM: `art/units/stealth armor/modernarmor.kfm`.
- Geometry: 476 triangles, 1 skins, 2 joint bindings, 2 textures.
- Bounds: height 0.2768, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels).
- SHA-256: `299156ea527eb7d76359b3aa554ed8cb0ca0e4fd1c04d0baa32cf0839e248551`.
- idle: `art/units/stealth armor/modernarmor_md_idle.kf`.
- run: `art/units/stealth armor/modernarmor_md_run.kf`.
- fortify: `art/units/stealth armor/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/stealth armor/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/stealth armor/modernarmor_md_hurta.kf`.

### military:stealth_bomber

- Model: `src/Mods/Military/art/models/stealth_bomber.glb` (162,640 bytes).
- Art: `ART_DEF_UNIT_STEALTH_BOMBER`. Source scale: 0.5.
- NIF: `art/units/stealthbomber/stealthbomber.nif`.
- KFM: `art/units/stealthbomber/stealthbomber.kfm`.
- Geometry: 536 triangles, 2 skins, 6 joint bindings, 1 textures.
- Bounds: height 0.0627, radius 0.4800, feet Y=0.
- Clips: idle (25 channels), run (25 channels), hurt (25 channels), die (25 channels).
- SHA-256: `8069601a10f4dce6cc6acb1f0fd0974e573e34c73781136c8eb39438f2a65b75`.
- idle: `art/units/stealthbomber/stealthbomber_md_idle.kf`.
- run: `art/units/stealthbomber/stealthbomber_md_run.kf`.
- hurt: `art/units/stealthbomber/stealthbomber_md_hurt.kf`.
- die: `art/units/stealthbomber/stealthbomber_md_die.kf`.

### military:stealth_destroyer

- Model: `src/Mods/Military/art/models/stealth_destroyer.glb` (207,460 bytes).
- Art: `ART_DEF_UNIT_STEALTH_DESTROYER`. Source scale: 0.6.
- NIF: `art/units/stealth_destroyer/stealth_destroyer.nif`.
- KFM: `art/units/stealth_destroyer/stealth_destroyer.kfm`.
- Geometry: 168 triangles, 1 skins, 8 joint bindings, 1 textures.
- Bounds: height 0.2749, radius 0.4800, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels), hurt (42 channels), die (42 channels).
- SHA-256: `60ad5c74d37673cf27a88d856383e816ed82288f6ecd0cecaba0f5a8c417929c`.
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
- SHA-256: `0480d4b8dd669420b4fd5c33678d57314836c91055121f7e1dd4a2b96dd563c5`.
- idle: `art/units/astute/astutesub_md_idle.kf`.
- run: `art/units/astute/astutesub_md_run.kf`.
- fortify: `art/units/astute/astutesub_md_rangedfortify.kf`.
- attack: `art/units/astute/astutesub_md_rangedstrike.kf`.
- hurt: `art/units/astute/astutesub_md_rangedhurta.kf`.
- die: `art/units/astute/astutesub_md_diea.kf`.

### military:steamer

- Model: `src/Mods/Military/art/models/steamer.glb` (379,560 bytes).
- Art: `ART_DEF_UNIT_STEAMER`. Source scale: 0.3.
- NIF: `art/units/paddlesteamer/paddlewheeler.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 2152 triangles, 1 skins, 10 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4761, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `8c4b614b9df1988985e8d223edf327b0acb8715317abf5de62affc851daa8dd9`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.

### military:stiletto_boat

- Model: `src/Mods/Military/art/models/stiletto_boat.glb` (207,460 bytes).
- Art: `ART_DEF_UNIT_STILETTO_BOAT`. Source scale: 0.40.
- NIF: `art/units/stealth_destroyer/stealth_destroyer.nif`.
- KFM: `art/units/stealth_destroyer/stealth_destroyer.kfm`.
- Geometry: 168 triangles, 1 skins, 8 joint bindings, 1 textures.
- Bounds: height 0.2749, radius 0.4800, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels), hurt (42 channels), die (42 channels).
- SHA-256: `60ad5c74d37673cf27a88d856383e816ed82288f6ecd0cecaba0f5a8c417929c`.
- idle: `art/units/stealth_destroyer/stealth_destroyer_md_idle.kf`.
- run: `art/units/stealth_destroyer/stealth_destroyer_md_run.kf`.
- fortify: `art/units/stealth_destroyer/stealth_destroyer_md_fortifya.kf`.
- attack: `art/units/stealth_destroyer/stealth_destroyer_md_strikea.kf`.
- hurt: `art/units/stealth_destroyer/stealth_destroyer_md_hurta.kf`.
- die: `art/units/stealth_destroyer/stealth_destroyer_md_diea.kf`.

### military:submarine

- Model: `src/Mods/Military/art/models/submarine.glb` (261,020 bytes).
- Art: `ART_DEF_UNIT_SUBMARINE`. Source scale: 0.55.
- NIF: `art/units/submarine/submarine.nif`.
- KFM: `art/units/submarine/submarine.kfm`.
- Geometry: 424 triangles, 2 skins, 13 joint bindings, 2 textures.
- Bounds: height 0.3012, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), fortify (28 channels), attack (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `2f38df4c90c2e6f26547203a9c0e0c51a525ddd2de3ba5824acae0b49baf8702`.
- idle: `art/units/submarine/submarine_md_idle.kf`.
- run: `art/units/submarine/submarine_md_run.kf`.
- fortify: `art/units/submarine/submarine_md_fortifya.kf`.
- attack: `art/units/submarine/submarine_md_strikea.kf`.
- hurt: `art/units/submarine/submarine_md_hurta.kf`.
- die: `art/units/submarine/submarine_md_diea.kf`.
- Note: submarine:1: strongest four influences retained for 2 vertices

### military:sumerian_vulture

- Model: `src/Mods/Military/art/models/sumerian_vulture.glb` (347,192 bytes).
- Art: `ART_DEF_UNIT_SUMERIAN_VULTURE`. Source scale: 0.44.
- NIF: `art/units/unique_sumeria_vulture/unique_sumerian_vulture.nif`.
- KFM: `art/units/unique_sumeria_vulture/unique_sumerian_vulture.kfm`.
- Geometry: 994 triangles, 1 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3644, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `a971ccc6aa35a2a1d4b71d398e974b73fb46c3f84c97e7a6e11b940e838fb262`.
- idle: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_idle.kf`.
- run: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_run.kf`.
- fortify: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_fortifya.kf`.
- attack: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_strikea.kf`.
- hurt: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_hurta.kf`.
- die: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_diea.kf`.

### military:swiss_hellebardier

- Model: `src/Mods/Military/art/models/swiss_hellebardier.glb` (310,944 bytes).
- Art: `ART_DEF_UNIT_SWISS_HELLEBARDIER`. Source scale: 0.44.
- NIF: `art/switzerland/swisspikeman/pikeman.nif`.
- KFM: `art/switzerland/swisspikeman/pikeman.kfm`.
- Geometry: 864 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.4461, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `890d929f3216344a2fec22ccd74e2c9684cab76d096f757b71c4d001f4cadf53`.
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
- Bounds: height 0.6000, radius 0.3101, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `4d21442f7064c46533b301bfa00db0acfce6323b9ccd29140cf1066a58f2a28e`.
- idle: `art/units/charlemagne_lightswordsman/lightswordsman_md_idle.kf`.
- run: `art/units/charlemagne_lightswordsman/lightswordsman_md_run.kf`.
- fortify: `art/units/charlemagne_lightswordsman/lightswordsman_md_fortifya.kf`.
- attack: `art/units/charlemagne_lightswordsman/lightswordsman_md_strikea.kf`.
- hurt: `art/units/charlemagne_lightswordsman/lightswordsman_md_hurta.kf`.
- die: `art/units/charlemagne_lightswordsman/lightswordsman_md_diea.kf`.

### military:t95

- Model: `src/Mods/Military/art/models/t95.glb` (353,504 bytes).
- Art: `ART_DEF_UNIT_T95`. Source scale: 0.4.
- NIF: `art/units/hybrid tank/t-95.nif`.
- KFM: `art/units/hybrid tank/tank.kfm`.
- Geometry: 2434 triangles, 1 skins, 5 joint bindings, 2 textures.
- Bounds: height 0.2082, radius 0.4800, feet Y=0.
- Clips: idle (14 channels), run (14 channels), fortify (14 channels), attack (14 channels), hurt (14 channels).
- SHA-256: `b02a6d0478c2890f48c62b2a0626b0a9c15072c6643f87b27e3a3d179f1c830e`.
- idle: `art/units/hybrid tank/tank_md_idle.kf`.
- run: `art/units/hybrid tank/tank_md_run.kf`.
- fortify: `art/units/hybrid tank/tank_md_rangedfortify.kf`.
- attack: `art/units/hybrid tank/tank_md_rangedstrike.kf`.
- hurt: `art/units/hybrid tank/tank_md_hurta.kf`.

### military:tactical_nuke

- Model: `src/Mods/Military/art/models/tactical_nuke.glb` (89,280 bytes).
- Art: `ART_DEF_UNIT_TACTICAL_NUKE`. Source scale: 0.5.
- NIF: `art/units/tactical_nuke/tactical_nuke.nif`.
- KFM: `art/units/tactical_nuke/tactical_nuke.kfm`.
- Geometry: 154 triangles, 1 skins, 3 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.1203, feet Y=0.
- Clips: idle (18 channels), run (18 channels), attack (18 channels).
- SHA-256: `71c1c606178b933422b194538d0f27ce6529de798688db3affc052d5552a55fe`.
- idle: `art/units/tactical_nuke/tactical_nuke_md_idle.kf`.
- run: `art/units/tactical_nuke/tactical_nuke_md_run.kf`.
- attack: `art/units/tactical_nuke/tactical_nuke_md_strikea.kf`.

### military:tank

- Model: `src/Mods/Military/art/models/tank.glb` (263,604 bytes).
- Art: `ART_DEF_UNIT_TANK`. Source scale: 0.48.
- NIF: `art/units/tank/tank.nif`.
- KFM: `art/units/tank/tank.kfm`.
- Geometry: 369 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5171, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels).
- SHA-256: `575ff7fcd98ce7f2c1d1e994032ba90de5fd4d8eb425a5de5442c02afa62845f`.
- idle: `art/units/tank/tank_md_idle.kf`.
- run: `art/units/tank/tank_md_run.kf`.
- fortify: `art/units/tank/tank_md_rangedfortify.kf`.
- attack: `art/units/tank/tank_md_rangedstrike.kf`.
- hurt: `art/units/tank/tank_md_hurta.kf`.

### military:taoist_missionary

- Model: `src/Mods/Military/art/models/taoist_missionary.glb` (212,108 bytes).
- Art: `ART_DEF_UNIT_TAOIST_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/taoist/taoistmissionary.nif`.
- KFM: `art/units/missionary/taoist/taoistmissionary.kfm`.
- Geometry: 600 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2543, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `7c8285f2f35d00fefbfad31a7252d0b7fce2d6068378ee59514941b132e6e0c3`.
- idle: `art/units/missionary/taoist/taoistmissionary_md_idle.kf`.
- run: `art/units/missionary/taoist/taoistmissionary_md_run.kf`.
- fortify: `art/units/missionary/taoist/taoistmissionary_md_fortify.kf`.
- die: `art/units/missionary/taoist/taoistmissionary_md_idle_die.kf`.

### military:trade_caravan

- Model: `src/Mods/Military/art/models/trade_caravan.glb` (825,008 bytes).
- Art: `ART_DEF_UNIT_TRADE_CARAVAN`. Source scale: 0.34.
- NIF: `art/units/caravan/caravan_art.nif`.
- KFM: `art/units/caravan/caravan_base.kfm`.
- Geometry: 2320 triangles, 4 skins, 74 joint bindings, 5 textures.
- Bounds: height 0.3182, radius 0.4800, feet Y=0.
- Clips: idle (239 channels), run (239 channels).
- SHA-256: `dafee83177eaedb0155bebaf0a60ccbd2eb8b72e0f5769042c4cf9b833a8d4e2`.
- idle: `art/units/caravan/caravan_base_md_idle.kf`.
- run: `art/units/caravan/caravan_base_md_run.kf`.

### military:transport

- Model: `src/Mods/Military/art/models/transport.glb` (454,128 bytes).
- Art: `ART_DEF_UNIT_TRANSPORT`. Source scale: 0.92.
- NIF: `art/units/transport/transport.nif`.
- KFM: `art/units/transport/transport.kfm`.
- Geometry: 490 triangles, 2 skins, 12 joint bindings, 1 textures.
- Bounds: height 0.2713, radius 0.4800, feet Y=0.
- Clips: idle (88 channels), run (88 channels), fortify (88 channels), attack (88 channels), hurt (88 channels), die (88 channels).
- SHA-256: `731fdefed375d5ccb643d78c1d9cc97821f798773cd26e8fa8e8fd6aecdcf74a`.
- idle: `art/units/transport/transport_md_idle.kf`.
- run: `art/units/transport/transport_md_run.kf`.
- fortify: `art/units/transport/transport_md_fortifya.kf`.
- attack: `art/units/transport/transport_md_strikea.kf`.
- hurt: `art/units/transport/transport_md_hurta.kf`.
- die: `art/units/transport/transport_md_diea.kf`.

### military:treasure

- Model: `src/Mods/Military/art/models/treasure.glb` (204,240 bytes).
- Art: `ART_DEF_UNIT_TREASURE_UNIT`. Source scale: 0.44.
- NIF: `art/units/treasure/treasure.nif`.
- KFM: `art/units/treasure/treasure.kfm`.
- Geometry: 946 triangles, 2 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.5603, radius 0.4800, feet Y=0.
- Clips: idle (55 channels), run (55 channels).
- SHA-256: `dfe578c8061fe3adbe2c411229dc4f1b373e450e4f5a3d1ab7e0875d7db77520`.
- idle: `art/units/treasure/treasure_md_idle.kf`.
- run: `art/units/treasure/treasure_md_run.kf`.

### military:trebuchet

- Model: `src/Mods/Military/art/models/trebuchet.glb` (349,940 bytes).
- Art: `ART_DEF_UNIT_TREBUCHET`. Source scale: 0.40.
- NIF: `art/units/trebuchet/trebuchet.nif`.
- KFM: `art/units/trebuchet/trebuchet.kfm`.
- Geometry: 680 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4218, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `9080dd4d456e16588f447f1ace2e8b4672c32e503c08bf0a3ee82c9a6b5dde18`.
- idle: `art/units/trebuchet/trebuchet_md_idle.kf`.
- run: `art/units/trebuchet/trebuchet_md_run.kf`.
- fortify: `art/units/trebuchet/trebuchet_md_fortifya.kf`.
- attack: `art/units/trebuchet/trebuchet_md_rangedstrike.kf`.
- hurt: `art/units/trebuchet/trebuchet_md_hurta.kf`.
- die: `art/units/trebuchet/trebuchet_md_diea.kf`.

### military:trireme

- Model: `src/Mods/Military/art/models/trireme.glb` (406,776 bytes).
- Art: `ART_DEF_UNIT_TRIREME`. Source scale: 0.14.
- NIF: `art/units/trireme/trireme.nif`.
- KFM: `art/units/trireme/trireme.kfm`.
- Geometry: 1144 triangles, 2 skins, 20 joint bindings, 2 textures.
- Bounds: height 0.4465, radius 0.4800, feet Y=0.
- Clips: idle (62 channels), run (62 channels), fortify (62 channels), attack (62 channels), hurt (62 channels), die (62 channels).
- SHA-256: `dbc7381fd8fe23cd87aba700d4611d2f6aed4e34ffc24ab56eac5c369fb4d3ac`.
- idle: `art/units/trireme/trireme_md_idle.kf`.
- run: `art/units/trireme/trireme_md_run.kf`.
- fortify: `art/units/trireme/trireme_md_fortifya.kf`.
- attack: `art/units/trireme/trireme_md_strikea.kf`.
- hurt: `art/units/trireme/trireme_md_hurta.kf`.
- die: `art/units/trireme/trireme_md_diea.kf`.

### military:turn

- Model: `src/Mods/Military/art/models/turn.glb` (126,368 bytes).
- Art: `ART_DEF_UNIT_TURN`. Source scale: 0.85.
- NIF: `art/units/nukes/yam/icbm.nif`.
- KFM: `art/units/nukes/yam/icbm.kfm`.
- Geometry: 386 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1730, feet Y=0.
- Clips: idle (13 channels), run (13 channels), attack (13 channels).
- SHA-256: `6d4ce6938f05529ef0d1901b6be72dc3693acffa25cd490379073af48fe8cb98`.
- idle: `art/units/nukes/yam/icbm_md_idle.kf`.
- run: `art/units/nukes/yam/icbm_md_run.kf`.
- attack: `art/units/nukes/yam/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:uav

- Model: `src/Mods/Military/art/models/uav.glb` (209,916 bytes).
- Art: `ART_DEF_UNIT_UNMANNED_AIR_VEHICLE`. Source scale: 0.2.
- NIF: `art/units/drone bomber/predator.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 600 triangles, 0 skins, 0 joint bindings, 2 textures.
- Bounds: height 0.2082, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `42f5dd9382199680d64ed7ded77093164a826b3995d6ca466bd922799fc4b156`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.
- Note: Hidden geometry omitted: FighterDeathModel:1

### military:unit_unit_crusader

- Model: `src/Mods/Military/art/models/unit_unit_crusader.glb` (509,092 bytes).
- Art: `ART_DEF_UNIT_KNIGHTS_TEMPLAR`. Source scale: 0.44.
- NIF: `art/units/scenario/knigts_templar/knightstemplar.nif`.
- KFM: `art/units/scenario/knigts_templar/knightstemplar.kfm`.
- Geometry: 556 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2889, feet Y=0.
- Clips: idle (102 channels), run (102 channels), fortify (102 channels), attack (102 channels), hurt (102 channels), die (102 channels).
- SHA-256: `bc7d5802681a337b8a5a6e102f8b01a58ca6c63032a41f582464ce462f5ba9b6`.
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

- Model: `src/Mods/Military/art/models/vandal_raider.glb` (460,528 bytes).
- Art: `ART_DEF_UNIT_VANDAL_RAIDER`. Source scale: 0.44.
- NIF: `art/units/raider/swordsman.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 710 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3410, feet Y=0.
- Clips: idle (100 channels), run (100 channels), fortify (100 channels), attack (100 channels), hurt (100 channels), die (100 channels).
- SHA-256: `45e869fb7be21b15f207f870cf3e94c8dc3a44788dd8088fb49a2f2311d7e102`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:viking_beserker

- Model: `src/Mods/Military/art/models/viking_beserker.glb` (449,184 bytes).
- Art: `ART_DEF_UNIT_VIKING_BESERKER`. Source scale: 0.44.
- NIF: `art/units/viking_berserker/berserker.nif`.
- KFM: `art/units/viking_berserker/berserker.kfm`.
- Geometry: 860 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3475, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `861a544cd21048b809e551e395df09fb944e4ff64d941ef01b316bacaab02863`.
- idle: `art/units/viking_berserker/berserker_md_idle.kf`.
- run: `art/units/viking_berserker/berserker_md_run.kf`.
- fortify: `art/units/viking_berserker/berserker_md_fortifya.kf`.
- attack: `art/units/viking_berserker/berserker_md_strikea.kf`.
- hurt: `art/units/viking_berserker/berserker_md_hurta.kf`.
- die: `art/units/viking_berserker/berserker_md_diea.kf`.

### military:walker

- Model: `src/Mods/Military/art/models/walker.glb` (379,824 bytes).
- Art: `ART_DEF_UNIT_WALKER`. Source scale: 0.8.
- NIF: `art/units/walker droid/mech.nif`.
- KFM: `art/units/walker droid/mech.kfm`.
- Geometry: 1128 triangles, 2 skins, 10 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3368, feet Y=0.
- Clips: idle (84 channels), run (84 channels), fortify (84 channels), attack (84 channels), hurt (84 channels).
- SHA-256: `508ca72be40e753a9ccc9d8f3e2c0e42661625d8f38070bf82fc9659963579ae`.
- idle: `art/units/walker droid/mech_md_idle.kf`.
- run: `art/units/walker droid/mech_md_run.kf`.
- fortify: `art/units/walker droid/mech_md_rangedfortify.kf`.
- attack: `art/units/walker droid/mech_md_rangedstrike.kf`.
- hurt: `art/units/walker droid/mech_md_rangedhurta.kf`.

### military:war_elephant

- Model: `src/Mods/Military/art/models/war_elephant.glb` (857,312 bytes).
- Art: `ART_DEF_UNIT_WAR_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/warelephant/warelephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 1454 triangles, 3 skins, 47 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2463, feet Y=0.
- Clips: idle (140 channels), run (140 channels), fortify (140 channels), attack (140 channels), hurt (140 channels), die (140 channels).
- SHA-256: `784dff99ef82d64aae2b2cdf069c7245f3a0ded5f76647f6480e0ed7c3fd617a`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.

### military:wargalley

- Model: `src/Mods/Military/art/models/wargalley.glb` (253,800 bytes).
- Art: `ART_DEF_UNIT_WARGALLEY`. Source scale: 0.16.
- NIF: `art/units/war galley/wargalley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 1102 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3745, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `90105f2acab502b4fff2c1de0e259ee91c33c0e8bba9f7ca2a4ed41431a4d3d1`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:warrior

- Model: `src/Mods/Military/art/models/warrior.glb` (511,632 bytes).
- Art: `ART_DEF_UNIT_WARRIOR`. Source scale: 0.44.
- NIF: `art/units/warrior/warrior.nif`.
- KFM: `art/units/warrior/warrior.kfm`.
- Geometry: 598 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3395, feet Y=0.
- Clips: idle (93 channels), run (93 channels), fortify (93 channels), attack (93 channels), hurt (93 channels), die (93 channels).
- SHA-256: `d817bab5b7a8aadb1f3bd47cf38de97f288d108ff5089ae16b11787b8dc0b1d8`.
- idle: `art/units/warrior/warrior_md_idle.kf`.
- run: `art/units/warrior/warrior_md_run.kf`.
- fortify: `art/units/warrior/warrior_md_fortifya.kf`.
- attack: `art/units/warrior/warrior_md_strikea.kf`.
- hurt: `art/units/warrior/warrior_md_hurta.kf`.
- die: `art/units/warrior/warrior_md_diea.kf`.

### military:welsh_longbowman

- Model: `src/Mods/Military/art/models/welsh_longbowman.glb` (699,304 bytes).
- Art: `ART_DEF_UNIT_WELSH_LONGBOWMAN`. Source scale: 0.44.
- NIF: `art/wales/welsh longbowman/longbowman.nif`.
- KFM: `art/wales/welsh longbowman/longbowman.kfm`.
- Geometry: 652 triangles, 2 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3059, feet Y=0.
- Clips: idle (133 channels), run (133 channels), fortify (133 channels), attack (133 channels), hurt (133 channels), die (133 channels).
- SHA-256: `1ac3a17476d3fab7df1c4235f061abe23f0d69759edaf73be49cc0d88b68be99`.
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
- SHA-256: `c2cd743cac7fcd851f4eb6b12b3319130b0db796717cb52d4fac6853909e108f`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:wild_ram

- Model: `src/Mods/Military/art/models/wild_ram.glb` (460,112 bytes).
- Art: `ART_DEF_UNIT_WILD_RAM`. Source scale: 18.0.
- NIF: `art/units/wild_ram/mouflon.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 794 triangles, 2 skins, 33 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2982, feet Y=0.
- Clips: none.
- SHA-256: `42759592a8ef91c55b3859a0a1afaada148b2d559d807f6957a2bfe206685f9a`.
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

- Model: `src/Mods/Military/art/models/wolf.glb` (346,724 bytes).
- Art: `ART_DEF_UNIT_WOLF`. Source scale: 0.5.
- NIF: `art/units/wolf/wolf.nif`.
- KFM: `art/units/wolf/wolf.kfm`.
- Geometry: 604 triangles, 2 skins, 24 joint bindings, 2 textures.
- Bounds: height 0.5084, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `31e8f032541f2f0e5a2d97e80f3399cf75672356ac21e4a4411f461832f53c86`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:workboat

- Model: `src/Mods/Military/art/models/workboat.glb` (209,272 bytes).
- Art: `ART_DEF_UNIT_WORKBOAT`. Source scale: 0.19.
- NIF: `art/units/workboat_ancient/workboat_ancient.nif`.
- KFM: `art/units/workboat_ancient/workboat_ancient.kfm`.
- Geometry: 417 triangles, 3 skins, 10 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3534, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), die (15 channels).
- SHA-256: `24222b2efb746e8b19d35000fe9e65ea0881deebb307282aec8294a3033298cf`.
- idle: `art/units/workboat_ancient/workboat_ancient_md_idle.kf`.
- run: `art/units/workboat_ancient/workboat_ancient_md_run.kf`.
- fortify: `art/units/workboat_ancient/workboat_ancient_md_fortifya.kf`.
- die: `art/units/workboat_ancient/workboat_ancient_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:worker

- Model: `src/Mods/Military/art/models/worker.glb` (246,896 bytes).
- Art: `ART_DEF_UNIT_WORKER`. Source scale: 0.44.
- NIF: `art/units/worker/worker.nif`.
- KFM: `art/units/worker/worker.kfm`.
- Geometry: 884 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3281, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels).
- SHA-256: `d35459d7e2038515959019fcb8b0c789a9292bed300fb0ddcbecd4b4866e6731`.
- idle: `art/units/worker/worker_md_idle.kf`.
- run: `art/units/worker/worker_md_run.kf`.
- fortify: `art/units/worker/worker_md_fortify.kf`.

### military:worker_android

- Model: `src/Mods/Military/art/models/worker_android.glb` (242,800 bytes).
- Art: `ART_DEF_UNIT_WORKER_ANDROID`. Source scale: 0.44.
- NIF: `art/units/android worker/modernworker.nif`.
- KFM: `art/units/android worker/modernworker.kfm`.
- Geometry: 854 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3330, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels).
- SHA-256: `7aee98e7411fefa0fc3c6c958caeb5df2550ab0c9138710c3f3dad61bc304158`.
- idle: `art/units/android worker/modernworker_md_idle.kf`.
- run: `art/units/android worker/modernworker_md_run.kf`.
- fortify: `art/units/android worker/modernworker_md_fortify.kf`.
- Note: Missing texture: Worker_tools.dds

### military:ww1_destroyer

- Model: `src/Mods/Military/art/models/ww1_destroyer.glb` (308,492 bytes).
- Art: `ART_DEF_UNIT_WW1_DESTROYER`. Source scale: 0.6.
- NIF: `art/units/destroyer/american/4stacker.nif`.
- KFM: `art/units/destroyer/american/4stacker.kfm`.
- Geometry: 650 triangles, 2 skins, 5 joint bindings, 2 textures.
- Bounds: height 0.4628, radius 0.4800, feet Y=0.
- Clips: idle (21 channels), run (21 channels), fortify (21 channels), attack (21 channels), hurt (21 channels).
- SHA-256: `37e5cfc71b6909f158c5eb6c3a91eca37594f66eb76463d8f9813945dbe8c2b3`.
- idle: `art/units/destroyer/american/4stacker_md_idle.kf`.
- run: `art/units/destroyer/american/4stacker_md_run.kf`.
- fortify: `art/units/destroyer/american/4stacker_md_rangedfortify.kf`.
- attack: `art/units/destroyer/american/4stacker_md_rangedstrike.kf`.
- hurt: `art/units/destroyer/american/4stacker_md_hurta.kf`.

### military:ww2_battleship

- Model: `src/Mods/Military/art/models/ww2_battleship.glb` (453,376 bytes).
- Art: `ART_DEF_UNIT_WW2_BATTLESHIP`. Source scale: 0.7.
- NIF: `art/units/battleship/european/battleship.nif`.
- KFM: `art/units/battleship/european/battleship.kfm`.
- Geometry: 2177 triangles, 0 skins, 0 joint bindings, 1 textures.
- Bounds: height 0.2600, radius 0.4800, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `7e2368e66e1e11c1666cd695a873eb1728dec5d24e9b53a9ad7c26384e122186`.
- idle: `art/units/battleship/european/battleship_md_idle.kf`.
- run: `art/units/battleship/european/battleship_md_run.kf`.
- fortify: `art/units/battleship/european/battleship_md_fortifya.kf`.
- attack: `art/units/battleship/european/battleship_md_strikea.kf`.
- hurt: `art/units/battleship/european/battleship_md_hurta.kf`.
- die: `art/units/battleship/european/battleship_md_diea.kf`.

### military:zoroastrian_missionary

- Model: `src/Mods/Military/art/models/zoroastrian_missionary.glb` (186,972 bytes).
- Art: `ART_DEF_UNIT_ZORO_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/magi/confucian_missionary.nif`.
- KFM: `art/units/missionary/magi/confucian_missionary.kfm`.
- Geometry: 480 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2507, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), die (50 channels).
- SHA-256: `1b0ef4225e0858c9bfdb1bbc8380918a71146af7cfdb11e7f5be2d68de43123e`.
- idle: `art/units/missionary/magi/confucian_missionary_md_idle.kf`.
- run: `art/units/missionary/magi/confucian_missionary_md_run.kf`.
- fortify: `art/units/missionary/magi/confucian_missionary_md_fortifya.kf`.
- die: `art/units/missionary/magi/confucian_missionary_md_idle_die.kf`.

### military:zulu_impi

- Model: `src/Mods/Military/art/models/zulu_impi.glb` (364,888 bytes).
- Art: `ART_DEF_UNIT_ZULU_SPEARMAN`. Source scale: 0.44.
- NIF: `art/units/zulu_impi/zulu_impi.nif`.
- KFM: `art/units/zulu_impi/zulu_impi.kfm`.
- Geometry: 1105 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3229, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels), hurt (55 channels), die (55 channels).
- SHA-256: `0e01bcd8a6137bb963f06ab4728fec0da3d23770bb0c4ba03ee54c2c09f9149d`.
- idle: `art/units/zulu_impi/zulu_impi_md_idle.kf`.
- run: `art/units/zulu_impi/zulu_impi_md_run.kf`.
- fortify: `art/units/zulu_impi/zulu_impi_md_fortifya.kf`.
- attack: `art/units/zulu_impi/zulu_impi_md_strikea.kf`.
- hurt: `art/units/zulu_impi/zulu_impi_md_hurta.kf`.
- die: `art/units/zulu_impi/zulu_impi_md_diea.kf`.
