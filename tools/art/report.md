# Unit art conversion report

## Run

- Registry: `src/Mods/Military/data/unit.json` (359 units in this run).
- Source: `/home/gordon/workspace/civ/AND-extraction`.
- Converted: **357**. Failed: **2**.
- Animated: **351**. Skinned: **333**. Textured: **355**.
- Output: `src/Mods/Military/art/models/<unit-id-suffix>.glb`.
- Total GLB size: 123,748,028 bytes.
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

## Per-unit registry

### military:007

- Model: `src/Mods/Military/art/models/007.glb` (478,716 bytes).
- Art: `ART_DEF_UNIT_007`. Source scale: 0.46.
- NIF: `art/units/007/007.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 739 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4055, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels).
- SHA-256: `d200fa1daad33e95b6a7ee5cd3305a489a194b00f983138cec15abe7d10cd816`.
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

- Model: `src/Mods/Military/art/models/adventurer.glb` (612,632 bytes).
- Art: `ART_DEF_UNIT_ADVENTURER`. Source scale: 0.42.
- NIF: `art/units/adventurer/persianimmortal.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 1150 triangles, 3 skins, 38 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2401, feet Y=0.
- Clips: idle (110 channels), run (110 channels), fortify (110 channels), attack (110 channels), hurt (110 channels), die (110 channels).
- SHA-256: `c39f9df38800ca779abebec71e0a73c7fe067d32a151dac5f261432a3d9332b4`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:agent

- Model: `src/Mods/Military/art/models/agent.glb` (482,592 bytes).
- Art: `ART_DEF_UNIT_AGENT`. Source scale: 0.46.
- NIF: `art/units/agent/agent.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 823 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3957, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels).
- SHA-256: `dce9c35296ee01a83af7ecd9abf80d9c0822f432c37520a254f0e565ee639664`.
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

- Model: `src/Mods/Military/art/models/airship.glb` (224,764 bytes).
- Art: `ART_DEF_UNIT_AIRSHIP`. Source scale: 0.3.
- NIF: `art/units/airship/airship.nif`.
- KFM: `art/units/airship/airship.kfm`.
- Geometry: 904 triangles, 3 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.1889, radius 0.4800, feet Y=0.
- Clips: idle (39 channels), run (39 channels), hurt (39 channels), die (39 channels).
- SHA-256: `8307e89bf8eda664c2750f368d14f4a2352c4b2e685a0917905e5ed1fc6e7b25`.
- idle: `art/units/airship/airship_md_idle.kf`.
- run: `art/units/airship/airship_md_run.kf`.
- hurt: `art/units/airship/airship_md_hurt.kf`.
- die: `art/units/airship/airship_md_die.kf`.

### military:albanian_light_guerilla_cavalry

- Model: `src/Mods/Military/art/models/albanian_light_guerilla_cavalry.glb` (602,312 bytes).
- Art: `ART_DEF_UNIT_KALORSE_KNIGHT`. Source scale: 0.42.
- NIF: `art/albania/kalorse/knight/knight.nif`.
- KFM: `art/albania/kalorse/knight/knight.kfm`.
- Geometry: 1143 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.4800, feet Y=0.
- Clips: idle (109 channels), run (109 channels), fortify (109 channels), attack (109 channels), hurt (109 channels), die (109 channels).
- SHA-256: `7564ba4159ee6fca82a9f42abfa4278766e3b010979420bd93c6c231783e134b`.
- idle: `art/albania/kalorse/knight/knight_md_idle.kf`.
- run: `art/albania/kalorse/knight/knight_md_run.kf`.
- fortify: `art/albania/kalorse/knight/knight_md_fortifya.kf`.
- attack: `art/albania/kalorse/knight/knight_md_strikea.kf`.
- hurt: `art/albania/kalorse/knight/knight_md_hurta.kf`.
- die: `art/albania/kalorse/knight/knight_md_diea.kf`.
- Note: First of 3 formation art groups exported

### military:ambassador

- Model: `src/Mods/Military/art/models/ambassador.glb` (439,908 bytes).
- Art: `ART_DEF_UNIT_AMBASSADOR`. Source scale: 0.46.
- NIF: `art/units/ambassador/great_spy_modern_fx.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 590 triangles, 1 skins, 15 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2995, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels).
- SHA-256: `29b64d0b02416d29ac87c76060fbb43b719297064671a38e19b7d5146779f072`.
- idle: `art/units/great_spy_modern/great_spy_modern_md_idle.kf`.
- run: `art/units/great_spy_modern/great_spy_modern_md_run.kf`.
- fortify: `art/units/great_spy_modern/great_spy_modern_md_fortify.kf`.
- attack: `art/units/great_spy_modern/great_spy_modern_md_rangedstrike.kf`.

### military:american_navy_seal

- Model: `src/Mods/Military/art/models/american_navy_seal.glb` (340,832 bytes).
- Art: `ART_DEF_UNIT_AMERICAN_NAVY_SEAL`. Source scale: 0.44.
- NIF: `art/units/unique/american navy seal/marines.nif`.
- KFM: `art/units/unique/american navy seal/marines.kfm`.
- Geometry: 798 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.4547, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `892b5b1028abb27222f46a5d4ca6ec6448671fb382db0acce793a2ed226145fc`.
- idle: `art/units/unique/american navy seal/marines_md_idle.kf`.
- run: `art/units/unique/american navy seal/marines_md_run.kf`.
- fortify: `art/units/unique/american navy seal/marines_md_rangedfortify.kf`.
- attack: `art/units/unique/american navy seal/marines_md_rangedstrike.kf`.
- hurt: `art/units/unique/american navy seal/marines_md_hurta.kf`.
- die: `art/units/unique/american navy seal/marines_md_rangeddiea.kf`.

### military:anglo_saxon_huscarl

- Model: `src/Mods/Military/art/models/anglo_saxon_huscarl.glb` (298,096 bytes).
- Art: `ART_DEF_UNIT_ANGLO_SAXON_HUSCARL`. Source scale: 0.44.
- NIF: `art/anglosaxons/huskarl/huscarl.nif`.
- KFM: `art/anglosaxons/huskarl/huscarl.kfm`.
- Geometry: 689 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3407, feet Y=0.
- Clips: idle (72 channels), run (72 channels), fortify (72 channels), attack (72 channels), hurt (72 channels), die (72 channels).
- SHA-256: `f4b5574d943fa193a12cf1a10186f79f72650fa3e9ead4d08530ce58611f90c1`.
- idle: `art/anglosaxons/huskarl/huscarl_md_idle.kf`.
- run: `art/anglosaxons/huskarl/huscarl_md_run.kf`.
- fortify: `art/anglosaxons/huskarl/huscarl_md_fortifya.kf`.
- attack: `art/anglosaxons/huskarl/huscarl_md_strikea.kf`.
- hurt: `art/anglosaxons/huskarl/huscarl_md_hurta.kf`.
- die: `art/anglosaxons/huskarl/huscarl_md_diea.kf`.

### military:arabia_camelarcher

- Model: `src/Mods/Military/art/models/arabia_camelarcher.glb` (669,828 bytes).
- Art: `ART_DEF_UNIT_ARABIA_CAMELARCHER`. Source scale: 0.39.
- NIF: `art/units/unique/arabia/camelarcher/arabiacamelarcher.nif`.
- KFM: `art/units/unique/arabia/camelarcher/arabiacamelarcher.kfm`.
- Geometry: 1250 triangles, 4 skins, 42 joint bindings, 3 textures.
- Bounds: height 0.3779, radius 0.4800, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `2b70a740c8eeee45c057703014afaf23202d3f9d3c0e717bda57b0ba03843c56`.
- idle: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_idle.kf`.
- run: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_run.kf`.
- fortify: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_fortifya.kf`.
- attack: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_strikea.kf`.
- hurt: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_hurta.kf`.
- die: `art/units/unique/arabia/camelarcher/arabiacamelarcher_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:archer

- Model: `src/Mods/Military/art/models/archer.glb` (341,680 bytes).
- Art: `ART_DEF_UNIT_ARCHER`. Source scale: 0.44.
- NIF: `art/units/archer/archer.nif`.
- KFM: `art/units/archer/archer.kfm`.
- Geometry: 756 triangles, 3 skins, 20 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2395, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `7e899b9b104a5f9af3eea873b7020097bbb2b6e31a3fb8edae4be553d8f41364`.
- idle: `art/units/archer/archer_md_idle.kf`.
- run: `art/units/archer/archer_md_run.kf`.
- fortify: `art/units/archer/archer_md_fortifya.kf`.
- attack: `art/units/archer/archer_md_strikea.kf`.
- hurt: `art/units/archer/archer_md_hurta.kf`.
- die: `art/units/archer/archer_md_diea.kf`.
- Note: Missing texture: Archer_Arrow.dds

### military:armor_m60

- Model: `src/Mods/Military/art/models/armor_m60.glb` (217,480 bytes).
- Art: `ART_DEF_UNIT_ARMOR_M60`. Source scale: 0.4.
- NIF: `art/units/main battle tank/generic/m60.nif`.
- KFM: `art/units/main battle tank/generic/m60.kfm`.
- Geometry: 956 triangles, 11 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.2922, radius 0.4800, feet Y=0.
- Clips: idle (12 channels), run (12 channels), fortify (12 channels), attack (12 channels), hurt (12 channels).
- SHA-256: `0d69e62d13c15aa05502a5ff554b6040f33443302102ff41e86d3cc5da7a469e`.
- idle: `art/units/main battle tank/generic/m60_md_idle.kf`.
- run: `art/units/main battle tank/generic/m60_md_run.kf`.
- fortify: `art/units/main battle tank/generic/m60_md_rangedfortify.kf`.
- attack: `art/units/main battle tank/generic/m60_md_rangedstrike.kf`.
- hurt: `art/units/main battle tank/generic/m60_md_hurta.kf`.

### military:armored_car

- Model: `src/Mods/Military/art/models/armored_car.glb` (163,180 bytes).
- Art: `ART_DEF_UNIT_ARMORED_CAR`. Source scale: 0.40.
- NIF: `art/units/armored car/rollsroyce.nif`.
- KFM: `art/units/armored car/rollsroyce.kfm`.
- Geometry: 852 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.3478, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `c12abe8d52c1c474a4f3e2e44befcc7ffa4f9f352087dd7170069271fac2c169`.
- idle: `art/units/armored car/rollsroyce_md_idle.kf`.
- run: `art/units/armored car/rollsroyce_md_run.kf`.
- fortify: `art/units/armored car/rollsroyce_md_rangedfortify.kf`.
- attack: `art/units/armored car/rollsroyce_md_rangedstrike.kf`.
- hurt: `art/units/armored car/rollsroyce_md_hurta.kf`.

### military:arquebusier

- Model: `src/Mods/Military/art/models/arquebusier.glb` (319,744 bytes).
- Art: `ART_DEF_UNIT_ARQUEBUSIER`. Source scale: 0.44.
- NIF: `art/units/musketman/musketman.nif`.
- KFM: `art/units/musketman/musketman.kfm`.
- Geometry: 715 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2782, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `dfdc5db9288a57d9107f3b4590a9d509ea7ffd5c596762ef537becbcb622cf4b`.
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
- SHA-256: `866a2e04be6e2194a56a91bd3f014ec4537c036ccccdd1c97e3d9171229112ad`.
- idle: `art/units/artillery/artillery_md_idle.kf`.
- run: `art/units/artillery/artillery_md_run.kf`.
- fortify: `art/units/artillery/artillery_md_rangedfortify.kf`.
- attack: `art/units/artillery/artillery_md_rangedstrike.kf`.
- hurt: `art/units/artillery/artillery_md_hurta.kf`.

### military:artist

- Model: `src/Mods/Military/art/models/artist.glb` (204,764 bytes).
- Art: `ART_DEF_UNIT_ARTIST`. Source scale: 0.46.
- NIF: `art/units/greatpeople/artist/greatartist.nif`.
- KFM: `art/units/greatpeople/artist/greatartist.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2844, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels).
- SHA-256: `889c1ad4950053dd97865f5449dfb5918319dd51c5659bfdd76bc22a921c67e3`.
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
- SHA-256: `e6f278c613aa71ca594be6d1355a7fdc757025bbaea3bb2b883848cd6d1699db`.
- idle: `art/units/gothi/missionary_general_md_idle.kf`.
- run: `art/units/gothi/missionary_general_md_run.kf`.
- fortify: `art/units/gothi/missionary_general_md_fortifya.kf`.
- die: `art/units/gothi/missionary_general_md_idle_die.kf`.

### military:assault_droid

- Model: `src/Mods/Military/art/models/assault_droid.glb` (458,280 bytes).
- Art: `ART_DEF_UNIT_ASSAULT_DROID`. Source scale: 0.8.
- NIF: `art/units/assault droid/mech.nif`.
- KFM: `art/units/assault droid/mech.kfm`.
- Geometry: 1700 triangles, 1 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.5752, radius 0.4800, feet Y=0.
- Clips: idle (93 channels), run (93 channels), fortify (93 channels), attack (93 channels), hurt (93 channels).
- SHA-256: `7741b986fabe91f93a6ad6085172e958c1e9d8a9283a0a875a863111ed97cdc9`.
- idle: `art/units/assault droid/mech_md_idle.kf`.
- run: `art/units/assault droid/mech_md_run.kf`.
- fortify: `art/units/assault droid/mech_md_rangedfortify.kf`.
- attack: `art/units/assault droid/mech_md_rangedstrike.kf`.
- hurt: `art/units/assault droid/mech_md_rangedhurta.kf`.

### military:assault_mech

- Model: `src/Mods/Military/art/models/assault_mech.glb` (372,212 bytes).
- Art: `ART_DEF_UNIT_ASSAULT_MECH`. Source scale: 0.35.
- NIF: `art/units/assault_mech/nextwar_assaultmech.nif`.
- KFM: `art/units/assault_mech/nextwar_assaultmech.kfm`.
- Geometry: 833 triangles, 3 skins, 20 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4350, feet Y=0.
- Clips: idle (63 channels), run (63 channels), fortify (63 channels), attack (63 channels), die (63 channels).
- SHA-256: `19e6a558de504d3af5e914c6ea2eda5d639f6a64b0763266055b0db367491e66`.
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

- Model: `src/Mods/Military/art/models/assyrian_redu.glb` (311,400 bytes).
- Art: `ART_DEF_UNIT_ASSYRIAN_REDU`. Source scale: 0.44.
- NIF: `art/units/redu/spearman.nif`.
- KFM: `art/units/redu/spearman.kfm`.
- Geometry: 752 triangles, 1 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2904, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels), die (48 channels).
- SHA-256: `70e85d434e9bc42f60e44ec1c519e266e2316e08ab4f39eea2f31e0c91e3c5de`.
- idle: `art/units/redu/spearman_md_idle.kf`.
- run: `art/units/redu/spearman_md_run.kf`.
- fortify: `art/units/redu/spearman_md_fortifya.kf`.
- attack: `art/units/redu/spearman_md_strikea.kf`.
- hurt: `art/units/redu/spearman_md_hurta.kf`.
- die: `art/units/redu/spearman_md_diea.kf`.

### military:at_infantry

- Model: `src/Mods/Military/art/models/at_infantry.glb` (433,192 bytes).
- Art: `ART_DEF_UNIT_AT_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/antitankinfantry/wwantitankinfantry.nif`.
- KFM: `art/units/antitankinfantry/wwantitankinfantry.kfm`.
- Geometry: 800 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3512, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), die (44 channels).
- SHA-256: `cb3de54e8c0f5a1da4cd172e947c23003c8e5badaec171022b86f8dcac34b095`.
- idle: `art/units/antitankinfantry/wwantitankinfantry_md_idle.kf`.
- run: `art/units/antitankinfantry/wwantitankinfantry_md_run.kf`.
- fortify: `art/units/antitankinfantry/wwantitankinfantry_md_rangedfortify.kf`.
- attack: `art/units/antitankinfantry/wwantitankinfantry_md_rangedstrike.kf`.
- die: `art/units/antitankinfantry/wwantitankinfantry_md_rangeddiea.kf`.

### military:atomic

- Model: `src/Mods/Military/art/models/atomic.glb` (192,864 bytes).
- Art: `ART_DEF_UNIT_ATOMIC`. Source scale: 0.5.
- NIF: `art/units/a bomb/bomber.nif`.
- KFM: `art/units/a bomb/bomber.kfm`.
- Geometry: 1006 triangles, 4 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.1372, radius 0.4800, feet Y=0.
- Clips: idle (33 channels), run (33 channels), hurt (33 channels), die (33 channels).
- SHA-256: `c47d7868ab1b478280ec475f7f40dae2bd475a1ca9d8469ba1c94d89901d8c38`.
- idle: `art/units/a bomb/bomber_md_idle.kf`.
- run: `art/units/a bomb/bomber_md_run.kf`.
- hurt: `art/units/a bomb/bomber_md_hurt.kf`.
- die: `art/units/a bomb/bomber_md_die.kf`.

### military:attack_submarine

- Model: `src/Mods/Military/art/models/attack_submarine.glb` (180,000 bytes).
- Art: `ART_DEF_UNIT_ATTACK_SUBMARINE`. Source scale: 0.35.
- NIF: `art/units/nuclear_sub/nuclear_sub.nif`.
- KFM: `art/units/nuclear_sub/nuclear_sub.kfm`.
- Geometry: 305 triangles, 2 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.2504, radius 0.4800, feet Y=0.
- Clips: idle (29 channels), run (29 channels), fortify (29 channels), attack (29 channels), hurt (29 channels), die (29 channels).
- SHA-256: `7dfa951d1e316923b42cdb1aaefec84b2fd4ad4e9eba077a8e501e3d0464b45c`.
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
- SHA-256: `cebaeb467475b11df943fe283bc744102d8d16eac80665906d2b30dd5f32c6de`.
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
- Bounds: height 0.6000, radius 0.3950, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `5c4b4eb6f6e210a9f11aabe6c0b8bf2ea4929410c77be055507c85536631b602`.
- idle: `art/units/axeman/axeman_md_idle.kf`.
- run: `art/units/axeman/axeman_md_run.kf`.
- fortify: `art/units/axeman/axeman_md_fortifya.kf`.
- attack: `art/units/axeman/axeman_md_strikea.kf`.
- hurt: `art/units/axeman/axeman_md_hurta.kf`.
- die: `art/units/axeman/axeman_md_diea.kf`.

### military:axeman_latvia

- Model: `src/Mods/Military/art/models/axeman_latvia.glb` (414,372 bytes).
- Art: `ART_DEF_UNIT_AXEMAN_LATVIA`. Source scale: 0.44.
- NIF: `art/latvia/axemanlatvia/axeman.nif`.
- KFM: `art/latvia/axemanlatvia/axeman.kfm`.
- Geometry: 663 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3231, feet Y=0.
- Clips: idle (92 channels), run (92 channels), fortify (92 channels), attack (92 channels), die (92 channels).
- SHA-256: `c31bee411632be9a0d8c9e938cb94832c08176ffec109ded6088e17f70b3fdb8`.
- idle: `art/latvia/axemanlatvia/axeman_md_idle.kf`.
- run: `art/latvia/axemanlatvia/axeman_md_run.kf`.
- fortify: `art/latvia/axemanlatvia/axeman_md_fortifya.kf`.
- attack: `art/latvia/axemanlatvia/axeman_md_strikea.kf`.
- die: `art/latvia/axemanlatvia/axeman_md_dieb.kf`.
- Note: Hidden geometry omitted: Lightswordsman_sword
- Note: Hidden geometry omitted: Warrior_Body

### military:aztec_jaguar

- Model: `src/Mods/Military/art/models/aztec_jaguar.glb` (359,068 bytes).
- Art: `ART_DEF_UNIT_AZTEC_JAGUAR`. Source scale: 0.44.
- NIF: `art/units/unique/aztec/jaguar/aztec_jaguar.nif`.
- KFM: `art/units/unique/aztec/jaguar/aztec_jaguar.kfm`.
- Geometry: 702 triangles, 2 skins, 29 joint bindings, 4 textures.
- Bounds: height 0.5842, radius 0.4800, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `d1f1b42a2b1fa9d5df64ffa3fb6edad4c25b438f33ab6f84038f2d7008fdd7a4`.
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

- Model: `src/Mods/Military/art/models/babylon_bowman.glb` (270,944 bytes).
- Art: `ART_DEF_UNIT_BABYLON_BOWMAN`. Source scale: 0.44.
- NIF: `art/units/unique_babylon_bowman/unique_babylon_bowman.nif`.
- KFM: `art/units/unique_babylon_bowman/unique_babylon_bowman.kfm`.
- Geometry: 712 triangles, 2 skins, 21 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3937, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), die (53 channels).
- SHA-256: `f857ad5310a74ce77af0d463733577ab24809da1af67cb2eb3bfb040a5558b7f`.
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

- Model: `src/Mods/Military/art/models/battleship.glb` (353,904 bytes).
- Art: `ART_DEF_UNIT_BATTLESHIP`. Source scale: 0.8.
- NIF: `art/units/battleship/battleship.nif`.
- KFM: `art/units/battleship/battleship.kfm`.
- Geometry: 641 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.1595, radius 0.4800, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `62874de8c19e999cb8c6cb1b6366bec13354bc43b12d4d238806b26a90e837b6`.
- idle: `art/units/battleship/battleship_md_idle.kf`.
- run: `art/units/battleship/battleship_md_run.kf`.
- fortify: `art/units/battleship/battleship_md_fortifya.kf`.
- attack: `art/units/battleship/battleship_md_strikea.kf`.
- hurt: `art/units/battleship/battleship_md_hurta.kf`.
- die: `art/units/battleship/battleship_md_diea.kf`.

### military:bazooka

- Model: `src/Mods/Military/art/models/bazooka.glb` (383,880 bytes).
- Art: `ART_DEF_UNIT_BAZOOKA`. Source scale: 0.44.
- NIF: `art/units/bazooka/european/saminfantry.nif`.
- KFM: `art/units/bazooka/european/saminfantry.kfm`.
- Geometry: 754 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3926, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `6974397cc24fcc8b29719051ad309c6d033b2c2e6fc02de11de09fc8d11990a8`.
- idle: `art/units/bazooka/european/saminfantry_md_idle.kf`.
- run: `art/units/bazooka/european/saminfantry_md_run.kf`.
- fortify: `art/units/bazooka/european/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/bazooka/european/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/bazooka/european/saminfantry_md_rangedhurta.kf`.
- die: `art/units/bazooka/european/saminfantry_md_rangeddiea.kf`.

### military:bear

- Model: `src/Mods/Military/art/models/bear.glb` (277,996 bytes).
- Art: `ART_DEF_UNIT_BEAR`. Source scale: 0.6.
- NIF: `art/units/bear/bear.nif`.
- KFM: `art/units/bear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5093, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b6a904bca3c9741190c77f0f86427696329614e54543186b357059ff420c9e93`.
- idle: `art/units/bear/bear_md_idle.kf`.
- run: `art/units/bear/bear_md_run.kf`.
- fortify: `art/units/bear/bear_md_fortifya.kf`.
- attack: `art/units/bear/bear_md_strikea.kf`.
- hurt: `art/units/bear/bear_md_hurta.kf`.
- die: `art/units/bear/bear_md_diea.kf`.

### military:bengaltiger

- Model: `src/Mods/Military/art/models/bengaltiger.glb` (350,536 bytes).
- Art: `ART_DEF_UNIT_BENGALTIGER`. Source scale: 0.6.
- NIF: `art/units/animals/bengaltiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `a5fc02e21e931c47dfca6c9ea1a6a2eff14262fc51f19c861ac7a5259fc1839c`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:bgmaster

- Model: `src/Mods/Military/art/models/bgmaster.glb` (228,328 bytes).
- Art: `ART_DEF_UNIT_BGMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `3aef654c784d2a4054eac10a5e02441fd7a37cb1975f2e764714030fe7ed2ee2`.
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
- SHA-256: `15faef6b53998cb34a0624bab27e2f92308be2802dff4428196bb19e4e190748`.
- idle: `art/units/bioweapon_missile/bioweapon_missile_md_idle.kf`.
- run: `art/units/bioweapon_missile/bioweapon_missile_md_run.kf`.
- attack: `art/units/bioweapon_missile/bioweapon_missile_md_strikea.kf`.

### military:bleeder

- Model: `src/Mods/Military/art/models/bleeder.glb` (401,224 bytes).
- Art: `ART_DEF_UNIT_CYBORG1`. Source scale: 0.44.
- NIF: `art/units/genetic soldier/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 1036 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4191, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `6f744d53aca5c42edd6d164740f2e8a1b834adcde4071e58b03336239fc98790`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.
- Note: Missing texture: //cca.dds

### military:boar

- Model: `src/Mods/Military/art/models/boar.glb` (656,140 bytes).
- Art: `ART_DEF_UNIT_BOAR`. Source scale: 0.38.
- NIF: `art/units/boar/companion_cavalry.nif`.
- KFM: `art/units/knight_south_america/companion_cavalry.kfm`.
- Geometry: 948 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.5970, radius 0.4800, feet Y=0.
- Clips: idle (114 channels), run (114 channels), fortify (114 channels), attack (114 channels), hurt (114 channels), die (114 channels).
- SHA-256: `be3b5c00924bee805a1a402ef7e923a37d42e9a82ffe3276a90bb30b436469a7`.
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

- Model: `src/Mods/Military/art/models/bombard.glb` (226,536 bytes).
- Art: `ART_DEF_UNIT_BOMBARD`. Source scale: 0.53.
- NIF: `art/units/bombard/bombardb.nif`.
- KFM: `art/units/bombard/bombardb.kfm`.
- Geometry: 992 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4294, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `e9f935ae6ea795745b89bfa35ad25aaea8d610d8d90d21f158fd61d5939860fe`.
- idle: `art/units/bombard/bombardb_md_idle.kf`.
- run: `art/units/bombard/bombardb_md_run.kf`.
- fortify: `art/units/bombard/bombardb_md_rangedfortify.kf`.
- attack: `art/units/bombard/bombardb_md_rangedstrike.kf`.
- hurt: `art/units/bombard/bombardb_md_hurta.kf`.

### military:bomber

- Model: `src/Mods/Military/art/models/bomber.glb` (241,920 bytes).
- Art: `ART_DEF_UNIT_BOMBER`. Source scale: 0.5.
- NIF: `art/units/bomber/bomber.nif`.
- KFM: `art/units/bomber/bomber.kfm`.
- Geometry: 1006 triangles, 4 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.1372, radius 0.4800, feet Y=0.
- Clips: idle (33 channels), run (33 channels), hurt (33 channels), die (33 channels).
- SHA-256: `9366cad097b68ebae0b44f791e8d58e3b740a275d7bdf4908a18a58bb936a875`.
- idle: `art/units/bomber/bomber_md_idle.kf`.
- run: `art/units/bomber/bomber_md_run.kf`.
- hurt: `art/units/bomber/bomber_md_hurt.kf`.
- die: `art/units/bomber/bomber_md_die.kf`.

### military:brigantine

- Model: `src/Mods/Military/art/models/brigantine.glb` (216,604 bytes).
- Art: `ART_DEF_UNIT_CARAVEL_SQUARERIG`. Source scale: 0.22.
- NIF: `art/units/brigantine/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 758 triangles, 3 skins, 13 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4300, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels), die (20 channels).
- SHA-256: `80acd64139a10ae51444a49e6f50ac9d0f9f8248f196193f805db9c4a439827e`.
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
- SHA-256: `c4eb6155561c608120f696e3c82e3729b7c8384eefeceee5f620845287332a30`.
- idle: `art/units/missionary/buddhist/buddhist_missionary_md_idle.kf`.
- run: `art/units/missionary/buddhist/buddhist_missionary_md_run.kf`.
- fortify: `art/units/missionary/buddhist/buddhist_missionary_md_fortify.kf`.
- die: `art/units/missionary/buddhist/buddhist_missionary_md_idle_die.kf`.

### military:bulgarian_konnik

- Model: `src/Mods/Military/art/models/bulgarian_konnik.glb` (597,272 bytes).
- Art: `ART_DEF_UNIT_KONNIK`. Source scale: 0.42.
- NIF: `art/bulgaria/konnik/horseman_eurasian.nif`.
- KFM: `art/bulgaria/konnik/horseman_eurasian.kfm`.
- Geometry: 1183 triangles, 2 skins, 32 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3254, feet Y=0.
- Clips: idle (103 channels), run (103 channels), fortify (103 channels), attack (103 channels), hurt (103 channels), die (103 channels).
- SHA-256: `79e05ab4aa8348199fb6efd0825f19884cab0bf57fac6e721053e9b6cada0c41`.
- idle: `art/bulgaria/konnik/horseman_eurasian_md_idle.kf`.
- run: `art/bulgaria/konnik/horseman_eurasian_md_run.kf`.
- fortify: `art/bulgaria/konnik/horseman_eurasian_md_fortifya.kf`.
- attack: `art/bulgaria/konnik/horseman_eurasian_md_strikea.kf`.
- hurt: `art/bulgaria/konnik/horseman_eurasian_md_hurta.kf`.
- die: `art/bulgaria/konnik/horseman_eurasian_md_diea.kf`.

### military:bull

- Model: `src/Mods/Military/art/models/bull.glb` (88,232 bytes).
- Art: `ART_DEF_UNIT_BULL`. Source scale: 0.5.
- NIF: `art/units/bull/cow.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1182 triangles, 3 skins, 42 joint bindings, 0 textures.
- Bounds: height 0.6000, radius 0.2163, feet Y=0.
- Clips: idle (6 channels), run (6 channels), fortify (6 channels), attack (6 channels), hurt (6 channels), die (6 channels).
- SHA-256: `d3abc251844f68e55215ed34314eaeddd8d701edd9ee97c30b9bf7f09f4c8f7a`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.
- Note: Missing texture: bizon.dds
- Note: No diffuse texture resolved

### military:byzantine_cataphract

- Model: `src/Mods/Military/art/models/byzantine_cataphract.glb` (763,708 bytes).
- Art: `ART_DEF_UNIT_BYZANTINE_CATAPHRACT`. Source scale: 0.42.
- NIF: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract.nif`.
- KFM: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract.kfm`.
- Geometry: 1330 triangles, 3 skins, 47 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.4000, feet Y=0.
- Clips: idle (127 channels), run (127 channels), fortify (127 channels), attack (127 channels), hurt (127 channels), die (127 channels).
- SHA-256: `ebfea743adba094c1a7b02f168a8f8259442c109796c07edbae24b7c4208e874`.
- idle: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_idle.kf`.
- run: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_run.kf`.
- fortify: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_fortifya.kf`.
- attack: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_strikea.kf`.
- hurt: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_hurta.kf`.
- die: `art/units/unique_byzantine_cataphract/unique_byzantine_cataphract_md_diea.kf`.

### military:cannon

- Model: `src/Mods/Military/art/models/cannon.glb` (363,324 bytes).
- Art: `ART_DEF_UNIT_CANNON`. Source scale: 0.53.
- NIF: `art/units/cannon/cannon.nif`.
- KFM: `art/units/cannon/cannon.kfm`.
- Geometry: 1168 triangles, 3 skins, 23 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4568, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels).
- SHA-256: `2564bb48a678197c2ae81e7f0b6a336480fb67a1d77dd39d94d12ee5f36229fa`.
- idle: `art/units/cannon/cannon_md_idle.kf`.
- run: `art/units/cannon/cannon_md_run.kf`.
- fortify: `art/units/cannon/cannon_md_rangedfortify.kf`.
- attack: `art/units/cannon/cannon_md_rangedstrike.kf`.
- hurt: `art/units/cannon/cannon_md_hurta.kf`.

### military:captured_bear

- Model: `src/Mods/Military/art/models/captured_bear.glb` (277,996 bytes).
- Art: `ART_DEF_UNIT_BEAR`. Source scale: 0.6.
- NIF: `art/units/bear/bear.nif`.
- KFM: `art/units/bear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5093, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b6a904bca3c9741190c77f0f86427696329614e54543186b357059ff420c9e93`.
- idle: `art/units/bear/bear_md_idle.kf`.
- run: `art/units/bear/bear_md_run.kf`.
- fortify: `art/units/bear/bear_md_fortifya.kf`.
- attack: `art/units/bear/bear_md_strikea.kf`.
- hurt: `art/units/bear/bear_md_hurta.kf`.
- die: `art/units/bear/bear_md_diea.kf`.

### military:captured_bengaltiger

- Model: `src/Mods/Military/art/models/captured_bengaltiger.glb` (350,536 bytes).
- Art: `ART_DEF_UNIT_BENGALTIGER`. Source scale: 0.6.
- NIF: `art/units/animals/bengaltiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `a5fc02e21e931c47dfca6c9ea1a6a2eff14262fc51f19c861ac7a5259fc1839c`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_boar

- Model: `src/Mods/Military/art/models/captured_boar.glb` (656,140 bytes).
- Art: `ART_DEF_UNIT_BOAR`. Source scale: 0.38.
- NIF: `art/units/boar/companion_cavalry.nif`.
- KFM: `art/units/knight_south_america/companion_cavalry.kfm`.
- Geometry: 948 triangles, 1 skins, 31 joint bindings, 1 textures.
- Bounds: height 0.5970, radius 0.4800, feet Y=0.
- Clips: idle (114 channels), run (114 channels), fortify (114 channels), attack (114 channels), hurt (114 channels), die (114 channels).
- SHA-256: `be3b5c00924bee805a1a402ef7e923a37d42e9a82ffe3276a90bb30b436469a7`.
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

- Model: `src/Mods/Military/art/models/captured_bull.glb` (88,232 bytes).
- Art: `ART_DEF_UNIT_BULL`. Source scale: 0.5.
- NIF: `art/units/bull/cow.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1182 triangles, 3 skins, 42 joint bindings, 0 textures.
- Bounds: height 0.6000, radius 0.2163, feet Y=0.
- Clips: idle (6 channels), run (6 channels), fortify (6 channels), attack (6 channels), hurt (6 channels), die (6 channels).
- SHA-256: `d3abc251844f68e55215ed34314eaeddd8d701edd9ee97c30b9bf7f09f4c8f7a`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.
- Note: Missing texture: bizon.dds
- Note: No diffuse texture resolved

### military:captured_cave_lion

- Model: `src/Mods/Military/art/models/captured_cave_lion.glb` (320,016 bytes).
- Art: `ART_DEF_UNIT_CAVELION`. Source scale: 0.75.
- NIF: `art/units/animals/cavelion/lion.nif`.
- KFM: `art/units/animals/cavelion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5546, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `bb83fcc6eaeac0e44a57d85d0133fa63037cd4f2af481b3911677a6acedf4ba5`.
- idle: `art/units/animals/cavelion/lion_md_idle.kf`.
- run: `art/units/animals/cavelion/lion_md_run.kf`.
- fortify: `art/units/animals/cavelion/lion_md_fortifya.kf`.
- attack: `art/units/animals/cavelion/lion_md_strikea.kf`.
- hurt: `art/units/animals/cavelion/lion_md_hurta.kf`.
- die: `art/units/animals/cavelion/lion_md_diea.kf`.

### military:captured_cavebear

- Model: `src/Mods/Military/art/models/captured_cavebear.glb` (276,420 bytes).
- Art: `ART_DEF_UNIT_CAVEBEAR`. Source scale: 0.8.
- NIF: `art/units/animals/cavebear/bear.nif`.
- KFM: `art/units/animals/cavebear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5093, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b733fdc6d09e5f74c17352a5e73fded29e2357705d93d8ddcccb313a7bf9e55d`.
- idle: `art/units/animals/cavebear/bear_md_idle.kf`.
- run: `art/units/animals/cavebear/bear_md_run.kf`.
- fortify: `art/units/animals/cavebear/bear_md_fortifya.kf`.
- attack: `art/units/animals/cavebear/bear_md_strikea.kf`.
- hurt: `art/units/animals/cavebear/bear_md_hurta.kf`.
- die: `art/units/animals/cavebear/bear_md_diea.kf`.

### military:captured_cheetah

- Model: `src/Mods/Military/art/models/captured_cheetah.glb` (272,160 bytes).
- Art: `ART_DEF_UNIT_CHEETAH`. Source scale: 0.45.
- NIF: `art/units/animals/cheetah/panther.nif`.
- KFM: `art/units/animals/cheetah/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `b1c9a0a95d6b7107b042b5c8e9816a86ac5dcd08e05b0925232e18ef25a6f0f6`.
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
- Bounds: height 0.5066, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `6ffad3ba3664216ceac979cb4bc90d10a0198f49e5557efbf7071dd0dcdeae78`.
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
- SHA-256: `daa27b680f400c0e5bb549bbe7c48b6e015de73cbc8a10df92e0e07327bc952d`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: Bone

### military:captured_lion

- Model: `src/Mods/Military/art/models/captured_lion.glb` (321,212 bytes).
- Art: `ART_DEF_UNIT_LION`. Source scale: 0.61.
- NIF: `art/units/lion/lion.nif`.
- KFM: `art/units/lion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5546, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `a60802039cf91d239cf61f25869114ae09a584400083a079be033b81e86c402a`.
- idle: `art/units/lion/lion_md_idle.kf`.
- run: `art/units/lion/lion_md_run.kf`.
- fortify: `art/units/lion/lion_md_fortifya.kf`.
- attack: `art/units/lion/lion_md_strikea.kf`.
- hurt: `art/units/lion/lion_md_hurta.kf`.
- die: `art/units/lion/lion_md_diea.kf`.

### military:captured_panther

- Model: `src/Mods/Military/art/models/captured_panther.glb` (269,812 bytes).
- Art: `ART_DEF_UNIT_PANTHER`. Source scale: 0.5.
- NIF: `art/units/panther/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `d86f3bb8f199c763adbc875e2cbc900dd002da61a962d131e29c576a0ad8ed51`.
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
- Bounds: height 0.5093, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `4ce3237b35a01590f5cc240f7e6a39b82f84fd513e71eb1caa194f6aef2f6911`.
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
- SHA-256: `dfb0ac310a774138a9ee62f8a4fc2fd9d1f79b78d820731f6f7d7a68dae937b7`.
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

- Model: `src/Mods/Military/art/models/captured_siberiantiger.glb` (349,516 bytes).
- Art: `ART_DEF_UNIT_SIBERIANTIGER`. Source scale: 0.67.
- NIF: `art/units/animals/siberiantiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `a8c7f8301d393e74dca03b384c9029fdfc5ea6d68ec0fd1cd3d1a6d7120f2056`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:captured_stag

- Model: `src/Mods/Military/art/models/captured_stag.glb` (503,744 bytes).
- Art: `ART_DEF_UNIT_STAG`. Source scale: 0.38.
- NIF: `art/units/stag/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 392 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4636, feet Y=0.
- Clips: idle (106 channels), run (106 channels), fortify (106 channels), attack (106 channels), hurt (106 channels), die (106 channels).
- SHA-256: `117d97c1d615414aff3a2a2c73ad325d3327ef772d11e05b539582de84942682`.
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

- Model: `src/Mods/Military/art/models/captured_stallion.glb` (337,652 bytes).
- Art: `ART_DEF_UNIT_STALLION`. Source scale: 0.3.
- NIF: `art/units/horse/horsedark.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 432 triangles, 1 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3462, feet Y=0.
- Clips: idle (64 channels), run (64 channels), fortify (64 channels), attack (64 channels), hurt (64 channels), die (64 channels).
- SHA-256: `d4cb0c420aae235f0652537b5e7d8b04a4efe1e893bcc2d84fcece331a5f9c70`.
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
- Bounds: height 0.5066, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `330054d6a0012be5d05b0eb977ff677587be37ed14288ee963aae0c058ff02f4`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:caravel

- Model: `src/Mods/Military/art/models/caravel.glb` (192,176 bytes).
- Art: `ART_DEF_UNIT_CARAVEL`. Source scale: 0.24.
- NIF: `art/units/caravel/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 427 triangles, 3 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4335, feet Y=0.
- Clips: idle (23 channels), run (23 channels), fortify (23 channels), attack (23 channels), hurt (23 channels), die (23 channels).
- SHA-256: `6514af6e35e07d0909d94a8dd9fa4ec723d5d7d191ae9f498e73a24be81a1311`.
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

- Model: `src/Mods/Military/art/models/carthage_numidian_cavalry.glb` (585,276 bytes).
- Art: `ART_DEF_UNIT_CARTHAGE_NUMIDIAN_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/numidian_cavalry/numidian_cavalry.nif`.
- KFM: `art/units/numidian_cavalry/numidian_cavalry.kfm`.
- Geometry: 1027 triangles, 2 skins, 35 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3964, feet Y=0.
- Clips: idle (105 channels), run (105 channels), fortify (105 channels), attack (105 channels), hurt (105 channels), die (105 channels).
- SHA-256: `54ce1b8e111c3f2fa48a7a3ef760de2e378ef4395f08557f5a915391a0cde79c`.
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
- SHA-256: `58816b98970e4a4c372b2ac4ed3ed232d7da12cc420c4b5011bdf2c26067753a`.
- idle: `art/units/catapult/catapult_md_idle.kf`.
- run: `art/units/catapult/catapult_md_run.kf`.
- fortify: `art/units/catapult/catapult_md_rangedfortify.kf`.
- attack: `art/units/catapult/catapult_md_rangedstrike.kf`.
- hurt: `art/units/catapult/catapult_md_hurta.kf`.

### military:cavalry

- Model: `src/Mods/Military/art/models/cavalry.glb` (554,628 bytes).
- Art: `ART_DEF_UNIT_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/cavalry/cavalry.nif`.
- KFM: `art/units/cavalry/cavalry.kfm`.
- Geometry: 1142 triangles, 4 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3559, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (86 channels), attack (86 channels), hurt (86 channels), die (86 channels).
- SHA-256: `95e011c8e8aaab45d38dba069da00283447bc93072c271b0ea1d67a182746f45`.
- idle: `art/units/cavalry/cavalry_md_idle.kf`.
- run: `art/units/cavalry/cavalry_md_run.kf`.
- fortify: `art/units/cavalry/cavalry_md_fortifya.kf`.
- attack: `art/units/cavalry/cavalry_md_strikea.kf`.
- hurt: `art/units/cavalry/cavalry_md_hurta.kf`.
- die: `art/units/cavalry/cavalry_md_diea.kf`.

### military:cavalry_light

- Model: `src/Mods/Military/art/models/cavalry_light.glb` (566,016 bytes).
- Art: `ART_DEF_UNIT_CAVALRY_LIGHT`. Source scale: 0.42.
- NIF: `art/units/cavalry light/european/cavalry pistol.nif`.
- KFM: `art/units/cavalry light/european/cavalry.kfm`.
- Geometry: 1206 triangles, 3 skins, 36 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3704, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (123 channels), attack (123 channels), hurt (86 channels), die (86 channels).
- SHA-256: `092e39b13977315c8755fa440872f8a2f68ee6b8b7d88ea1f7ea3b846d5ce67e`.
- idle: `art/units/cavalry light/european/cavalry_md_idle.kf`.
- run: `art/units/cavalry light/european/cavalry_md_run.kf`.
- fortify: `art/units/cavalry light/european/cavalry_md_fortifya.kf`.
- attack: `art/units/cavalry light/european/cavalry_md_strikea.kf`.
- hurt: `art/units/cavalry light/european/cavalry_md_hurta.kf`.
- die: `art/units/cavalry light/european/cavalry_md_diea.kf`.

### military:cave_lion

- Model: `src/Mods/Military/art/models/cave_lion.glb` (320,016 bytes).
- Art: `ART_DEF_UNIT_CAVELION`. Source scale: 0.75.
- NIF: `art/units/animals/cavelion/lion.nif`.
- KFM: `art/units/animals/cavelion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5546, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `bb83fcc6eaeac0e44a57d85d0133fa63037cd4f2af481b3911677a6acedf4ba5`.
- idle: `art/units/animals/cavelion/lion_md_idle.kf`.
- run: `art/units/animals/cavelion/lion_md_run.kf`.
- fortify: `art/units/animals/cavelion/lion_md_fortifya.kf`.
- attack: `art/units/animals/cavelion/lion_md_strikea.kf`.
- hurt: `art/units/animals/cavelion/lion_md_hurta.kf`.
- die: `art/units/animals/cavelion/lion_md_diea.kf`.

### military:cavebear

- Model: `src/Mods/Military/art/models/cavebear.glb` (276,420 bytes).
- Art: `ART_DEF_UNIT_CAVEBEAR`. Source scale: 0.8.
- NIF: `art/units/animals/cavebear/bear.nif`.
- KFM: `art/units/animals/cavebear/bear.kfm`.
- Geometry: 586 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.5093, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `b733fdc6d09e5f74c17352a5e73fded29e2357705d93d8ddcccb313a7bf9e55d`.
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
- Bounds: height 0.6000, radius 0.3513, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `910516b5a7970a17f6b945404f3c592cb769a7a979ef771967c2bd7d92d5b2e1`.
- idle: `art/units/celtic_warrior/celtic_warrior_md_idle.kf`.
- run: `art/units/celtic_warrior/celtic_warrior_md_run.kf`.
- fortify: `art/units/celtic_warrior/celtic_warrior_md_fortifya.kf`.
- attack: `art/units/celtic_warrior/celtic_warrior_md_strikea.kf`.
- hurt: `art/units/celtic_warrior/celtic_warrior_md_hurta.kf`.
- die: `art/units/celtic_warrior/celtic_warrior_md_diea.kf`.

### military:chariot

- Model: `src/Mods/Military/art/models/chariot.glb` (588,744 bytes).
- Art: `ART_DEF_UNIT_CHARIOT`. Source scale: 0.35.
- NIF: `art/units/chariot/chariot.nif`.
- KFM: `art/units/chariot/chariot.kfm`.
- Geometry: 1488 triangles, 5 skins, 48 joint bindings, 5 textures.
- Bounds: height 0.5786, radius 0.4800, feet Y=0.
- Clips: idle (105 channels), run (105 channels), fortify (105 channels), attack (105 channels), hurt (105 channels), die (105 channels).
- SHA-256: `e07e7537dfcb33a85be01e6cbc03199d2aa569a0545adc698ae8598521a49884`.
- idle: `art/units/chariot/chariot_md_idle.kf`.
- run: `art/units/chariot/chariot_md_run.kf`.
- fortify: `art/units/chariot/chariot_md_fortifya.kf`.
- attack: `art/units/chariot/chariot_md_strikea.kf`.
- hurt: `art/units/chariot/chariot_md_hurta.kf`.
- die: `art/units/chariot/chariot_md_diea.kf`.

### military:cheetah

- Model: `src/Mods/Military/art/models/cheetah.glb` (272,160 bytes).
- Art: `ART_DEF_UNIT_CHEETAH`. Source scale: 0.45.
- NIF: `art/units/animals/cheetah/panther.nif`.
- KFM: `art/units/animals/cheetah/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `b1c9a0a95d6b7107b042b5c8e9816a86ac5dcd08e05b0925232e18ef25a6f0f6`.
- idle: `art/units/animals/cheetah/panther_md_idle.kf`.
- run: `art/units/animals/cheetah/panther_md_run.kf`.
- fortify: `art/units/animals/cheetah/panther_md_fortifya.kf`.
- attack: `art/units/animals/cheetah/panther_md_strikea.kf`.
- hurt: `art/units/animals/cheetah/panther_md_hurta.kf`.
- die: `art/units/animals/cheetah/panther_md_diea.kf`.

### military:china_chokonu

- Model: `src/Mods/Military/art/models/china_chokonu.glb` (341,476 bytes).
- Art: `ART_DEF_UNIT_CHINA_CHOKONU`. Source scale: 0.44.
- NIF: `art/units/unique/china/chokonu/chokunu.nif`.
- KFM: `art/units/unique/china/chokonu/chokunu.kfm`.
- Geometry: 812 triangles, 2 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.5150, radius 0.4800, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels), die (58 channels).
- SHA-256: `6e3fdfd3b8a1c818e9374ca5b54c80646fc6f4e16f0bd139f8c0eb47c8fb759b`.
- idle: `art/units/unique/china/chokonu/chokunu_md_idle.kf`.
- run: `art/units/unique/china/chokonu/chokunu_md_run.kf`.
- fortify: `art/units/unique/china/chokonu/chokunu_md_rangedfortify.kf`.
- attack: `art/units/unique/china/chokonu/chokunu_md_rangedstrike.kf`.
- hurt: `art/units/unique/china/chokonu/chokunu_md_hurta.kf`.
- die: `art/units/unique/china/chokonu/chokunu_md_rangeddiea.kf`.

### military:chivalry

- Model: `src/Mods/Military/art/models/chivalry.glb` (635,104 bytes).
- Art: `ART_DEF_UNIT_CHIVALRY`. Source scale: 0.42.
- NIF: `art/units/mailed knight/european/mtemplar_heavy.nif`.
- KFM: `art/units/knight/knight.kfm`.
- Geometry: 1090 triangles, 3 skins, 40 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4085, feet Y=0.
- Clips: idle (115 channels), run (115 channels), fortify (115 channels), attack (115 channels), hurt (115 channels), die (115 channels).
- SHA-256: `fb67c83c2b5b6b4e44714e47146776323fef85fc480dfbce92ebee22f5f10d84`.
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
- Bounds: height 0.6000, radius 0.1874, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), die (53 channels).
- SHA-256: `edd1a224993e4c801f2b41d1810fb511d54a44bed83b5c0fc64f42f479b3fa45`.
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
- Bounds: height 0.6000, radius 0.2451, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels).
- SHA-256: `d117b0f3c8d3488adc5ee9813a0b2adb0750fb0ea91628d6dfb30a47443cc5ef`.
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
- Bounds: height 0.6000, radius 0.2657, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `cb175684bee478d06077b653f52cffe43e4302c7516ba0694dbda1950cb6e73e`.
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
- SHA-256: `788bd4d3b43aaa04897541f801b1b4363ef96d1bb7bdf117070499430cbd39c2`.
- idle: `art/units/missionary/confucian/confucian_missionary_md_idle.kf`.
- run: `art/units/missionary/confucian/confucian_missionary_md_run.kf`.
- fortify: `art/units/missionary/confucian/confucian_missionary_md_fortifya.kf`.
- die: `art/units/missionary/confucian/confucian_missionary_md_idle_die.kf`.

### military:construct_ship

- Model: `src/Mods/Military/art/models/construct_ship.glb` (238,960 bytes).
- Art: `ART_DEF_UNIT_CONSTRUCT_SHIP`. Source scale: 0.5.
- NIF: `art/units/finalfrontier_constructionship/constructionship.nif`.
- KFM: `art/units/finalfrontier_constructionship/constructionship.kfm`.
- Geometry: 853 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2697, radius 0.4800, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels).
- SHA-256: `821362c2f671619eba1739ddf6165f5e5c76665d99ca656988413dcb94ef8caf`.
- idle: `art/units/finalfrontier_constructionship/constructionship_md_idle.kf`.
- run: `art/units/finalfrontier_constructionship/constructionship_md_run.kf`.
- fortify: `art/units/finalfrontier_constructionship/constructionship_md_fortifya.kf`.

### military:crossbowman

- Model: `src/Mods/Military/art/models/crossbowman.glb` (345,924 bytes).
- Art: `ART_DEF_UNIT_CROSSBOWMAN`. Source scale: 0.44.
- NIF: `art/units/crossbowman/crossbowman.nif`.
- KFM: `art/units/crossbowman/crossbowman.kfm`.
- Geometry: 832 triangles, 3 skins, 22 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2955, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels), hurt (61 channels), die (61 channels).
- SHA-256: `91cf5514e372776f02197ce21603002c8570042cb607c0508cab48961c692fb8`.
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
- SHA-256: `c56861475f9168d2ef1fe794499edab6c0c0a9f7ff5f5dd2148fe368cda387f1`.
- idle: `art/units/cruiser/brooklyn_md_idle.kf`.
- run: `art/units/cruiser/brooklyn_md_run.kf`.
- fortify: `art/units/cruiser/brooklyn_md_rangedfortify.kf`.
- attack: `art/units/cruiser/brooklyn_md_rangedstrike.kf`.
- hurt: `art/units/cruiser/brooklyn_md_hurta.kf`.

### military:cuirassier

- Model: `src/Mods/Military/art/models/cuirassier.glb` (646,368 bytes).
- Art: `ART_DEF_UNIT_CUIRASSIER`. Source scale: 0.42.
- NIF: `art/units/cuirassier/cuirassier.nif`.
- KFM: `art/units/cuirassier/cuirassier.kfm`.
- Geometry: 1344 triangles, 2 skins, 46 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4069, feet Y=0.
- Clips: idle (143 channels), run (143 channels), fortify (143 channels), attack (143 channels), die (143 channels).
- SHA-256: `7a56bb19a01f559720dd727b2b1a9d542ebe4c4415f43dcd7fdecc9c633b1568`.
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
- SHA-256: `fdabb62c5e6ddf27a99d14dec2404fc1527eeb28818bffd58a44fda100ee1e7d`.
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
- Bounds: height 0.6000, radius 0.2776, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `fed0c48945938d4558bfdc3a90b25119ae9326018a8d6d09e4c3d2a6cc7e38bc`.
- idle: `art/units/musketman/musketman_md_idle.kf`.
- run: `art/units/musketman/musketman_md_run.kf`.
- fortify: `art/units/musketman/musketman_md_rangedfortify.kf`.
- attack: `art/units/musketman/musketman_md_rangedstrike.kf`.
- hurt: `art/units/musketman/musketman_md_hurta.kf`.
- die: `art/units/musketman/musketman_md_rangeddiea.kf`.
- Note: Hidden geometry omitted: Editable Mesh

### military:danish_workboat

- Model: `src/Mods/Military/art/models/danish_workboat.glb` (209,256 bytes).
- Art: `ART_DEF_UNIT_DANISH_WORKBOAT`. Source scale: 0.19.
- NIF: `art/units/workboat_ancient/workboat_ancient.nif`.
- KFM: `art/units/workboat_ancient/workboat_ancient.kfm`.
- Geometry: 417 triangles, 3 skins, 10 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2547, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), die (15 channels).
- SHA-256: `e23e8dff7983f153f95014c98f7d510f6c99400a27e046823d3b1f7894556dae`.
- idle: `art/units/workboat_ancient/workboat_ancient_md_idle.kf`.
- run: `art/units/workboat_ancient/workboat_ancient_md_run.kf`.
- fortify: `art/units/workboat_ancient/workboat_ancient_md_fortifya.kf`.
- die: `art/units/workboat_ancient/workboat_ancient_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:destroyer

- Model: `src/Mods/Military/art/models/destroyer.glb` (354,572 bytes).
- Art: `ART_DEF_UNIT_DESTROYER`. Source scale: 0.7.
- NIF: `art/units/destroyer/destroyer.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 517 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.2892, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `cd5c3d79e6ba66785c2704dea0570129904261b2889e3c6328346b58e8704c91`.
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
- Bounds: height 0.5066, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `6ffad3ba3664216ceac979cb4bc90d10a0198f49e5557efbf7071dd0dcdeae78`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:dragoon

- Model: `src/Mods/Military/art/models/dragoon.glb` (595,156 bytes).
- Art: `ART_DEF_UNIT_DRAGOON`. Source scale: 0.42.
- NIF: `art/units/dragoon/british/light_dragoon.nif`.
- KFM: `art/units/dragoon/british/light_dragoon.kfm`.
- Geometry: 1200 triangles, 5 skins, 40 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3138, feet Y=0.
- Clips: idle (102 channels), run (104 channels), fortify (104 channels), attack (104 channels), hurt (104 channels), die (104 channels).
- SHA-256: `4a6ffb1e05042f22c425f35d1490496c2e6e8040095d2647f79fe9e87ead03aa`.
- idle: `art/units/dragoon/british/mounted_swordsman_mongol_md_idle.kf`.
- run: `art/units/dragoon/british/mounted_swordsman_mongol_md_run.kf`.
- fortify: `art/units/dragoon/british/mounted_swordsman_mongol_md_fortifya.kf`.
- attack: `art/units/dragoon/british/mounted_swordsman_mongol_md_strikea.kf`.
- hurt: `art/units/dragoon/british/mounted_swordsman_mongol_md_hurta.kf`.
- die: `art/units/dragoon/british/mounted_swordsman_mongol_md_diea.kf`.

### military:dreadnought

- Model: `src/Mods/Military/art/models/dreadnought.glb` (225,804 bytes).
- Art: `ART_DEF_UNIT_DREADNOUGHT`. Source scale: 0.75.
- NIF: `art/units/dreadnought/dreadnought.nif`.
- KFM: `art/units/dreadnought/dreadnought.kfm`.
- Geometry: 397 triangles, 2 skins, 13 joint bindings, 1 textures.
- Bounds: height 0.2627, radius 0.4800, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels).
- SHA-256: `022355c59382dee1f25b1009ba0cd87a2f6d4e8b9d16441d181c45e58fcde917`.
- idle: `art/units/dreadnought/dreadnought_md_idle.kf`.
- run: `art/units/dreadnought/dreadnought_md_run.kf`.
- fortify: `art/units/dreadnought/dreadnought_md_rangedfortify.kf`.
- attack: `art/units/dreadnought/dreadnought_md_rangedstrikea.kf`.

### military:dreadnought_helgoland

- Model: `src/Mods/Military/art/models/dreadnought_helgoland.glb` (500,796 bytes).
- Art: `ART_DEF_UNIT_DREADNOUGHT_SHIP`. Source scale: 0.6.
- NIF: `art/units/dreadnought/european/helgoland.nif`.
- KFM: `art/units/dreadnought/european/helgoland.kfm`.
- Geometry: 1996 triangles, 3 skins, 7 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2952, feet Y=0.
- Clips: idle (27 channels), run (27 channels), fortify (27 channels), attack (27 channels), hurt (27 channels).
- SHA-256: `28f1e2641aaef4081102d149a1cf5bb1d84969e3498ebfb2adfbf246ae7ce0ff`.
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

- Model: `src/Mods/Military/art/models/dropship.glb` (219,300 bytes).
- Art: `ART_DEF_UNIT_DROPSHIP`. Source scale: 0.36.
- NIF: `art/units/dropship/isscv.nif`.
- KFM: `art/units/dropship/gunship.kfm`.
- Geometry: 1168 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2084, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels).
- SHA-256: `f15079749388cd45957756c0aae878287580d11bf82b5420eba03c5fbdf8c1c5`.
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
- Bounds: height 0.2990, radius 0.4800, feet Y=0.
- Clips: idle (10 channels), run (10 channels), fortify (10 channels), attack (10 channels), hurt (10 channels).
- SHA-256: `ff9116b5419e5ef933fcee70bc87a16c5429fbb73761e36db469c52eac85c426`.
- idle: `art/units/scenario/mark_v_tank/markvtank_md_idle.kf`.
- run: `art/units/scenario/mark_v_tank/markvtank_md_run.kf`.
- fortify: `art/units/scenario/mark_v_tank/markvtank_md_rangedfortify.kf`.
- attack: `art/units/scenario/mark_v_tank/markvtank_md_rangedstrike.kf`.
- hurt: `art/units/scenario/mark_v_tank/markvtank_md_hurta.kf`.

### military:egypt_myth_missionary

- Model: `src/Mods/Military/art/models/egypt_myth_missionary.glb` (260,456 bytes).
- Art: `ART_DEF_UNIT_RA_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/scribe/hindu_missionary.nif`.
- KFM: `art/units/missionary/scribe/hindu_missionary.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3371, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `5b15ff1bc3ce727f69e63d584cc88a9a8c1c44cb124178c344e13b05ad1039d1`.
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
- Bounds: height 0.4972, radius 0.4800, feet Y=0.
- Clips: idle (214 channels), run (214 channels), fortify (214 channels), attack (214 channels), hurt (214 channels), die (214 channels).
- SHA-256: `950addd87fc46deac61fcafbc3c045d47ee8d494478a401b645bcd3b3fb92a0d`.
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
- SHA-256: `daa27b680f400c0e5bb549bbe7c48b6e015de73cbc8a10df92e0e07327bc952d`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: Bone

### military:elephant_bombard

- Model: `src/Mods/Military/art/models/elephant_bombard.glb` (1,087,024 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_BOMBARD`. Source scale: 0.32.
- NIF: `art/units/elephant bombard/siegeelephant.nif`.
- KFM: `art/units/elephant bombard/siegeelephant.kfm`.
- Geometry: 1973 triangles, 5 skins, 47 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3074, feet Y=0.
- Clips: idle (163 channels), run (163 channels), fortify (163 channels), attack (163 channels), hurt (163 channels), die (163 channels).
- SHA-256: `6d43f5a02714113e2dabce597d0cd21e091c67de6b70c5194e05e55fa60ccac9`.
- idle: `art/units/elephant bombard/warelephant_md_idle.kf`.
- run: `art/units/elephant bombard/warelephant_md_run.kf`.
- fortify: `art/units/elephant bombard/warelephant_md_fortifya.kf`.
- attack: `art/units/elephant bombard/warelephant_md_strikea.kf`.
- hurt: `art/units/elephant bombard/warelephant_md_hurta.kf`.
- die: `art/units/elephant bombard/warelephant_md_diea.kf`.
- Note: Hidden geometry omitted: HeadPeice:0
- Note: Hidden geometry omitted: WarElephant-Carriage:1

### military:elephant_cavalry

- Model: `src/Mods/Military/art/models/elephant_cavalry.glb` (1,016,724 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_CAVALRY`. Source scale: 0.32.
- NIF: `art/units/elephant cavalry/siegeelephant.nif`.
- KFM: `art/units/elephant cavalry/siegeelephant.kfm`.
- Geometry: 1718 triangles, 3 skins, 45 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3074, feet Y=0.
- Clips: idle (163 channels), run (163 channels), fortify (163 channels), attack (163 channels), hurt (163 channels), die (163 channels).
- SHA-256: `75a3546cbe8a7f7dd824f4c453faaad3e06740b96ea602c925b3dad89a8318b7`.
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

- Model: `src/Mods/Military/art/models/elephant_rider.glb` (760,304 bytes).
- Art: `ART_DEF_UNIT_ELEPHANT_RIDER`. Source scale: 0.32.
- NIF: `art/units/warelephant/warelephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 1454 triangles, 3 skins, 47 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3278, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `dc571b26b3dd04386a69fc2aa6d4b990d6673097514cc13e50a3c5f84c6254eb`.
- idle: `art/units/warelephant/warelephant_md_idle.kf`.
- run: `art/units/warelephant/warelephant_md_run.kf`.
- fortify: `art/units/warelephant/warelephant_md_fortifya.kf`.
- attack: `art/units/warelephant/warelephant_md_strikea.kf`.
- hurt: `art/units/warelephant/warelephant_md_hurta.kf`.
- die: `art/units/warelephant/warelephant_md_diea.kf`.

### military:emp_infantry

- Model: `src/Mods/Military/art/models/emp_infantry.glb` (528,692 bytes).
- Art: `ART_DEF_UNIT_EMP_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/emp_infantry2/marines.nif`.
- KFM: `art/units/emp_infantry2/animations/marines.kfm`.
- Geometry: 2262 triangles, 1 skins, 16 joint bindings, 4 textures.
- Bounds: height 0.5656, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `0501d9f2f6f075a8761ecdfcba089548afe2c446242f25dbcbd50a65dc465503`.
- idle: `art/units/emp_infantry2/animations/marines_md_idle.kf`.
- run: `art/units/emp_infantry2/animations/marines_md_run.kf`.
- fortify: `art/units/emp_infantry2/animations/marines_md_rangedfortify.kf`.
- attack: `art/units/emp_infantry2/animations/marines_md_rangedstrike.kf`.
- hurt: `art/units/emp_infantry2/animations/marines_md_hurta.kf`.
- die: `art/units/emp_infantry2/animations/marines_md_rangeddiea.kf`.

### military:engineer

- Model: `src/Mods/Military/art/models/engineer.glb` (305,876 bytes).
- Art: `ART_DEF_UNIT_ENGINEER`. Source scale: 0.46.
- NIF: `art/units/greatpeople/engineer/engineer.nif`.
- KFM: `art/units/greatpeople/engineer/engineer.kfm`.
- Geometry: 706 triangles, 1 skins, 18 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3785, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels).
- SHA-256: `cad10092851ac41f7ae19bff1e3f3964157d3c9661002a5dbba0357fadebaa31`.
- idle: `art/units/greatpeople/engineer/engineer_md_idle.kf`.
- run: `art/units/greatpeople/engineer/engineer_md_run.kf`.
- fortify: `art/units/greatpeople/engineer/engineer_md_fortify.kf`.

### military:english_redcoat

- Model: `src/Mods/Military/art/models/english_redcoat.glb` (365,492 bytes).
- Art: `ART_DEF_UNIT_ENGLISH_REDCOAT`. Source scale: 0.44.
- NIF: `art/units/unique/england/redcoat/redcoat.nif`.
- KFM: `art/units/unique/england/redcoat/redcoat.kfm`.
- Geometry: 772 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.1563, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `204f93c3f6285604b21d5449a45638e8133b0c9dbfa2f1e67e80e2c2c63f51bb`.
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
- SHA-256: `b1b43d149e249d90d508b0bb5ff6132807deff9c63f1f293948ad1a999c822be`.
- idle: `art/units/envoy/european/master_tobacconist_md_idle.kf`.
- run: `art/units/envoy/european/master_tobacconist_md_run.kf`.
- fortify: `art/units/envoy/european/master_tobacconist_md_fortifya.kf`.
- attack: `art/units/envoy/european/master_tobacconist_md_strikea.kf`.
- hurt: `art/units/envoy/european/master_tobacconist_md_hurta.kf`.
- die: `art/units/envoy/european/master_tobacconist_md_diea.kf`.

### military:ethiopian_oromo_warrior

- Model: `src/Mods/Military/art/models/ethiopian_oromo_warrior.glb` (330,960 bytes).
- Art: `ART_DEF_UNIT_ETHIOPIAN_OROMO_WARRIOR_ORANGE`. Source scale: 0.44.
- NIF: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_orange.nif`.
- KFM: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior.kfm`.
- Geometry: 834 triangles, 2 skins, 22 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2540, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), die (53 channels).
- SHA-256: `cc0d2e07c185791fd4df133d5ffa52767f97e7fe14747519440373c09bf61bbd`.
- idle: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_idle.kf`.
- run: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_run.kf`.
- fortify: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangedfortify.kf`.
- attack: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangedstrike.kf`.
- die: `art/units/unique_ethiopia_oromowarrior/ethiopian_oromowarrior_md_rangeddiea.kf`.
- Note: First of 3 formation art groups exported

### military:executive_1

- Model: `src/Mods/Military/art/models/executive_1.glb` (259,236 bytes).
- Art: `ART_DEF_UNIT_CEO_1`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_cereal.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `32051f0adff6a2ac77f9bda3be7a4eab12f3c328337bd262464e58b29669fbec`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_2

- Model: `src/Mods/Military/art/models/executive_2.glb` (260,024 bytes).
- Art: `ART_DEF_UNIT_CEO_2`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_sushi.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `6bb1f408bbdf49637983128612d3620d047bb2a357f5c7bd988314f03de6f9d7`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_3

- Model: `src/Mods/Military/art/models/executive_3.glb` (259,104 bytes).
- Art: `ART_DEF_UNIT_CEO_3`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_ethanol.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `7d010b5648314a595f19bbdd354dcf25b628632e81621e87da1ff9c00d0b11a0`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_4

- Model: `src/Mods/Military/art/models/executive_4.glb` (259,132 bytes).
- Art: `ART_DEF_UNIT_CEO_4`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_construction.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `f79ea98f128f9933a2a5225f42d9fd20c049dd19900dc018d42cd92b39e768a8`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_5

- Model: `src/Mods/Military/art/models/executive_5.glb` (258,912 bytes).
- Art: `ART_DEF_UNIT_CEO_5`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_mining.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `8892cf53ba21f74a3259e692b785ea3253ebc140f2c2df5ca3f8deb4d747fa4a`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_6

- Model: `src/Mods/Military/art/models/executive_6.glb` (258,940 bytes).
- Art: `ART_DEF_UNIT_CEO_6`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_aluminum.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `9c89c353dec22394f3cc2ab88eb524fe4961ef420ecf2c26d8c68220f6dc5ed3`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_7

- Model: `src/Mods/Military/art/models/executive_7.glb` (259,332 bytes).
- Art: `ART_DEF_UNIT_CEO_7`. Source scale: 0.46.
- NIF: `art/units/ceo/logos/ceo_jewelry.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `a2fd4cbf8429ea99fa05721288ba794a6dc12f9fa44f2fff7a32050787248349`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_adventure

- Model: `src/Mods/Military/art/models/executive_adventure.glb` (259,428 bytes).
- Art: `ART_DEF_UNIT_CEO_ADVENTURE`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_adventure.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `5cfb018ab2600b9046d2a51d072d5b62c6422853088ca47d090f8e1fb79869cc`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_apotheguild

- Model: `src/Mods/Military/art/models/executive_apotheguild.glb` (230,312 bytes).
- Art: `ART_DEF_UNIT_APOTHMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `2b3cd8da26def786961401c19909d60401b7f7520ff6d327f0ea7967f8741c42`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_artistguild

- Model: `src/Mods/Military/art/models/executive_artistguild.glb` (230,312 bytes).
- Art: `ART_DEF_UNIT_ARTISTMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `2b3cd8da26def786961401c19909d60401b7f7520ff6d327f0ea7967f8741c42`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_bakerguild

- Model: `src/Mods/Military/art/models/executive_bakerguild.glb` (229,348 bytes).
- Art: `ART_DEF_UNIT_BAKERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `402e19028d4a660edcdc807fc04bd6dd884607dad8a323316d57febed2ba8197`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_blacksmguild

- Model: `src/Mods/Military/art/models/executive_blacksmguild.glb` (228,328 bytes).
- Art: `ART_DEF_UNIT_BLACKSMITHMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `3aef654c784d2a4054eac10a5e02441fd7a37cb1975f2e764714030fe7ed2ee2`.
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
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `dbb319c07a7fa5515dd36833578372104cfd32dc4cc6521f4908ce1a1b38a1cd`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_butcherguild

- Model: `src/Mods/Military/art/models/executive_butcherguild.glb` (229,348 bytes).
- Art: `ART_DEF_UNIT_BUTCHERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `402e19028d4a660edcdc807fc04bd6dd884607dad8a323316d57febed2ba8197`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_carpenterguild

- Model: `src/Mods/Military/art/models/executive_carpenterguild.glb` (228,328 bytes).
- Art: `ART_DEF_UNIT_CARPENTERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `3aef654c784d2a4054eac10a5e02441fd7a37cb1975f2e764714030fe7ed2ee2`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_clerksguild

- Model: `src/Mods/Military/art/models/executive_clerksguild.glb` (230,312 bytes).
- Art: `ART_DEF_UNIT_CLERKSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `2b3cd8da26def786961401c19909d60401b7f7520ff6d327f0ea7967f8741c42`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_drapersguild

- Model: `src/Mods/Military/art/models/executive_drapersguild.glb` (230,312 bytes).
- Art: `ART_DEF_UNIT_DRAPERSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `2b3cd8da26def786961401c19909d60401b7f7520ff6d327f0ea7967f8741c42`.
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
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `ed731c18b8acf686e523515c47530c5ee9297cb274f899c8c5280fc61b9157e7`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_equestguild

- Model: `src/Mods/Military/art/models/executive_equestguild.glb` (230,312 bytes).
- Art: `ART_DEF_UNIT_EQUESTMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `2b3cd8da26def786961401c19909d60401b7f7520ff6d327f0ea7967f8741c42`.
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
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `e94f556841777585d770d565e8145cb8e48b58386a34c653128c1e00b91245a6`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_fashion

- Model: `src/Mods/Military/art/models/executive_fashion.glb` (257,608 bytes).
- Art: `ART_DEF_UNIT_CEO_FASHION`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_fashion.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `917abb74e318d1503f1bebd74e3a0f9e7cc3d1940b7b4b3408e8b85f0f39b813`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_fishmonguild

- Model: `src/Mods/Military/art/models/executive_fishmonguild.glb` (229,348 bytes).
- Art: `ART_DEF_UNIT_FISHMONGERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `402e19028d4a660edcdc807fc04bd6dd884607dad8a323316d57febed2ba8197`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_glassguild

- Model: `src/Mods/Military/art/models/executive_glassguild.glb` (228,328 bytes).
- Art: `ART_DEF_UNIT_GLASSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `3aef654c784d2a4054eac10a5e02441fd7a37cb1975f2e764714030fe7ed2ee2`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_goldsmguild

- Model: `src/Mods/Military/art/models/executive_goldsmguild.glb` (228,328 bytes).
- Art: `ART_DEF_UNIT_GOLDSMITHMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `3aef654c784d2a4054eac10a5e02441fd7a37cb1975f2e764714030fe7ed2ee2`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_grocerguild

- Model: `src/Mods/Military/art/models/executive_grocerguild.glb` (229,348 bytes).
- Art: `ART_DEF_UNIT_GROCERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `402e19028d4a660edcdc807fc04bd6dd884607dad8a323316d57febed2ba8197`.
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
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `907710295621723b61ad0a1d688ff8340bac56f9f04bf6e8bb128214680955e6`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_mapster

- Model: `src/Mods/Military/art/models/executive_mapster.glb` (257,580 bytes).
- Art: `ART_DEF_UNIT_CEO_MAPSTER`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_marnzmotor.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `973bbe96613fe641a41ddc38052cace6873a0f0ccd6399924660de2dd1a0a9bb`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_masonguild

- Model: `src/Mods/Military/art/models/executive_masonguild.glb` (228,328 bytes).
- Art: `ART_DEF_UNIT_MASONSMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/brownguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `3aef654c784d2a4054eac10a5e02441fd7a37cb1975f2e764714030fe7ed2ee2`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:executive_mercerguild

- Model: `src/Mods/Military/art/models/executive_mercerguild.glb` (229,348 bytes).
- Art: `ART_DEF_UNIT_MERCERMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `402e19028d4a660edcdc807fc04bd6dd884607dad8a323316d57febed2ba8197`.
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
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `8cbcb559e8a617330b67c562351dc9cdedb4a84cac5c1568d1d2d35a2d956d68`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_mobby

- Model: `src/Mods/Military/art/models/executive_mobby.glb` (260,300 bytes).
- Art: `ART_DEF_UNIT_CEO_MOBBY`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_mobby.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `dab1ce049947a17d6fb66e610cdf4b94e2f6b10b1db449c9525c3eddd9c92512`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_red

- Model: `src/Mods/Military/art/models/executive_red.glb` (258,560 bytes).
- Art: `ART_DEF_UNIT_CEO_RED`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_rc.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `a5e3f67dbdc65af5b9a730dbec8946c169ccfb606d41cbe4209a0e9cbfdc670a`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_safari

- Model: `src/Mods/Military/art/models/executive_safari.glb` (259,532 bytes).
- Art: `ART_DEF_UNIT_CEO_SAFARI`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_safari.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `83410f6a3883b87147f14096dfd6998f4229224727ce98cd58bc550a5e6a15f3`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_ultsoldier

- Model: `src/Mods/Military/art/models/executive_ultsoldier.glb` (259,448 bytes).
- Art: `ART_DEF_UNIT_CEO_ULTSOLDIER`. Source scale: 0.35.
- NIF: `art/units/executive/ceo_soldier.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `ddeab8d528dd45ee12f09ce12b13a09d768028b7b51edf49ec5ff7fa4b076f3d`.
- idle: `art/units/ceo/ceo_md_idle.kf`.
- run: `art/units/ceo/ceo_md_run.kf`.
- fortify: `art/units/ceo/ceo_md_fortify.kf`.

### military:executive_wonka

- Model: `src/Mods/Military/art/models/executive_wonka.glb` (259,220 bytes).
- Art: `ART_DEF_UNIT_CEO_WONKA`. Source scale: 0.46.
- NIF: `art/units/executive/ceo_wonka.nif`.
- KFM: `art/units/ceo/ceo.kfm`.
- Geometry: 630 triangles, 2 skins, 17 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `62bae396ea4147274d01efefac2944b0de845884de7fbda74d9fa7960c289059`.
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
- SHA-256: `84535bc04d3602ad703493785e5776ea85e16bd2c16db9fcd1bf91ef48a133d2`.
- idle: `art/units/nukes/peacemaker/icbm_md_idle.kf`.
- run: `art/units/nukes/peacemaker/icbm_md_run.kf`.
- attack: `art/units/nukes/peacemaker/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:explorer

- Model: `src/Mods/Military/art/models/explorer.glb` (303,684 bytes).
- Art: `ART_DEF_UNIT_EXPLORER`. Source scale: 0.44.
- NIF: `art/units/explorer/explorer.nif`.
- KFM: `art/units/explorer/explorer.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2772, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `ba7e85428a49be59b5c84a1c72f15a2f3494d0bed9c6b43c394d4eb312f46f83`.
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

- Model: `src/Mods/Military/art/models/feral.glb` (518,024 bytes).
- Art: `ART_DEF_UNIT_CYBORG2`. Source scale: 0.44.
- NIF: `art/units/super soldier/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 1499 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3029, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `c5498efd6fcbd7e7259343f73e8c94cab3cfe84bc94f13ba5cc5839609343792`.
- idle: `art/units/saminfantry/saminfantry_md_idle.kf`.
- run: `art/units/saminfantry/saminfantry_md_run.kf`.
- fortify: `art/units/saminfantry/saminfantry_md_rangedfortify.kf`.
- attack: `art/units/saminfantry/saminfantry_md_rangedstrike.kf`.
- hurt: `art/units/saminfantry/saminfantry_md_rangedhurta.kf`.
- die: `art/units/saminfantry/saminfantry_md_rangeddiea.kf`.
- Note: Missing texture: //cca.dds

### military:ff_destroyer_i

- Model: `src/Mods/Military/art/models/ff_destroyer_i.glb` (236,852 bytes).
- Art: `ART_DEF_UNIT_FF_DESTROYER_I`. Source scale: 0.45.
- NIF: `art/units/finalfrontier_destroyer/destroyer.nif`.
- KFM: `art/units/finalfrontier_destroyer/destroyer.kfm`.
- Geometry: 983 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2620, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `1825c6a9b3f2193126d83634669b640494892e91277fd1208af805e15e470599`.
- idle: `art/units/finalfrontier_destroyer/destroyer_md_idle.kf`.
- run: `art/units/finalfrontier_destroyer/destroyer_md_run.kf`.
- fortify: `art/units/finalfrontier_destroyer/destroyer_md_fortifya.kf`.
- attack: `art/units/finalfrontier_destroyer/destroyer_md_strikea.kf`.
- hurt: `art/units/finalfrontier_destroyer/destroyer_md_hurta.kf`.
- die: `art/units/finalfrontier_destroyer/destroyer_md_diea.kf`.

### military:fighter

- Model: `src/Mods/Military/art/models/fighter.glb` (196,248 bytes).
- Art: `ART_DEF_UNIT_FIGHTER`. Source scale: 0.5.
- NIF: `art/units/fighter/fighter.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 532 triangles, 4 skins, 8 joint bindings, 1 textures.
- Bounds: height 0.3284, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `8697ff4e093cb0afb7afcea607836076d15ace09311101005a102d538565c87e`.
- idle: `art/units/fighter/fighter_md_idle.kf`.
- run: `art/units/fighter/fighter_md_run.kf`.
- hurt: `art/units/fighter/fighter_md_hurt.kf`.
- die: `art/units/fighter/fighter_md_die.kf`.

### military:fluyt

- Model: `src/Mods/Military/art/models/fluyt.glb` (293,980 bytes).
- Art: `ART_DEF_UNIT_FLUYT`. Source scale: 0.12.
- NIF: `art/units/galleon/galleon.nif`.
- KFM: `art/units/galleon/galleon.kfm`.
- Geometry: 1023 triangles, 3 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1892, feet Y=0.
- Clips: idle (39 channels), run (39 channels), fortify (39 channels), attack (39 channels), hurt (39 channels), die (39 channels).
- SHA-256: `9d9e5034acd55c6b6c2339130348586e0f6864e6e7cf202afee1b0365ef66074`.
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

- Model: `src/Mods/Military/art/models/french_musketeer.glb` (413,924 bytes).
- Art: `ART_DEF_UNIT_FRENCH_MUSKETEER`. Source scale: 0.44.
- NIF: `art/units/unique/french/musketeer/musketeer.nif`.
- KFM: `art/units/unique/french/musketeer/musketeer.kfm`.
- Geometry: 718 triangles, 2 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3163, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels), die (58 channels).
- SHA-256: `0258103d741a0376a7a88c10e966602d6c091572b5f5a246160b1f7aede4821e`.
- idle: `art/units/unique/french/musketeer/musketeer_md_idle.kf`.
- run: `art/units/unique/french/musketeer/musketeer_md_run.kf`.
- fortify: `art/units/unique/french/musketeer/musketeer_md_rangedfortify.kf`.
- attack: `art/units/unique/french/musketeer/musketeer_md_rangedstrike.kf`.
- hurt: `art/units/unique/french/musketeer/musketeer_md_hurta.kf`.
- die: `art/units/unique/french/musketeer/musketeer_md_rangeddiea.kf`.

### military:frigate

- Model: `src/Mods/Military/art/models/frigate.glb` (304,788 bytes).
- Art: `ART_DEF_UNIT_FRIGATE`. Source scale: 0.24.
- NIF: `art/units/frigate/frigate.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1174 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3195, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `4ceade68fcf56d4597d4197b364dcb83bc170a45ad875dbb52614d0ea6752a77`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:fusion_battleship

- Model: `src/Mods/Military/art/models/fusion_battleship.glb` (292,532 bytes).
- Art: `ART_DEF_UNIT_FF_BATTLESHIP_I`. Source scale: 0.21.
- NIF: `art/units/finalfrontier_battleship/battleship.nif`.
- KFM: `art/units/finalfrontier_battleship/battleship.kfm`.
- Geometry: 1456 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2224, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `7d86a84cbf73118a1b9b06c4768514634cfa74ce5d3e0a59850b061142f3ff18`.
- idle: `art/units/finalfrontier_battleship/battleship_md_idle.kf`.
- run: `art/units/finalfrontier_battleship/battleship_md_run.kf`.
- fortify: `art/units/finalfrontier_battleship/battleship_md_fortifya.kf`.
- attack: `art/units/finalfrontier_battleship/battleship_md_strikea.kf`.
- hurt: `art/units/finalfrontier_battleship/battleship_md_hurta.kf`.
- die: `art/units/finalfrontier_battleship/battleship_md_diea.kf`.

### military:fusion_carrier

- Model: `src/Mods/Military/art/models/fusion_carrier.glb` (377,760 bytes).
- Art: `ART_DEF_UNIT_FF_CARRIER_I`. Source scale: 0.21.
- NIF: `art/units/finalfrontier_carrier/carrier.nif`.
- KFM: `art/units/finalfrontier_carrier/carrier.kfm`.
- Geometry: 1874 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.4454, radius 0.4800, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), attack (15 channels), hurt (15 channels), die (15 channels).
- SHA-256: `727ccf89b26e402643a7ffe093034bc822bd46c446994b911e73bbc9f7f15a3d`.
- idle: `art/units/finalfrontier_carrier/carrier_md_idle.kf`.
- run: `art/units/finalfrontier_carrier/carrier_md_run.kf`.
- fortify: `art/units/finalfrontier_carrier/carrier_md_fortifya.kf`.
- attack: `art/units/finalfrontier_carrier/carrier_md_strikea.kf`.
- hurt: `art/units/finalfrontier_carrier/carrier_md_hurta.kf`.
- die: `art/units/finalfrontier_carrier/carrier_md_diea.kf`.

### military:fusion_cruiser

- Model: `src/Mods/Military/art/models/fusion_cruiser.glb` (270,852 bytes).
- Art: `ART_DEF_UNIT_FF_CRUISER_I`. Source scale: 0.25.
- NIF: `art/units/finalfrontier_cruiser/cruiser.nif`.
- KFM: `art/units/finalfrontier_cruiser/cruiser.kfm`.
- Geometry: 1046 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2745, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `4aed69e9cab760d6ddd48f35736672267038104365d1b984903120804b001912`.
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
- SHA-256: `8ec2df6dbf3dc6f2d2dc726d0a8191a6e91efc82f95d4311a93f5326a781a7e7`.
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
- SHA-256: `8ec2df6dbf3dc6f2d2dc726d0a8191a6e91efc82f95d4311a93f5326a781a7e7`.
- idle: `art/units/nukes/fusionbombs/icbm_md_idle.kf`.
- run: `art/units/nukes/fusionbombs/icbm_md_run.kf`.
- attack: `art/units/nukes/fusionbombs/icbm_md_strikea.kf`.
- Note: Missing texture: ICBM Glow.dds

### military:fusion_submarine

- Model: `src/Mods/Military/art/models/fusion_submarine.glb` (210,448 bytes).
- Art: `ART_DEF_UNIT_FUSION_SUBMARINE`. Source scale: 0.60.
- NIF: `art/units/fusion submarine/submarine.nif`.
- KFM: `art/units/fusion submarine/submarine.kfm`.
- Geometry: 568 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.1884, radius 0.4800, feet Y=0.
- Clips: idle (31 channels), run (31 channels), fortify (31 channels), attack (31 channels), hurt (31 channels), die (31 channels).
- SHA-256: `fc38ec2010cce558d43025780f4e7d8ce36fe885fae363d18bda96dd6eea32b4`.
- idle: `art/units/fusion submarine/submarine_md_idle.kf`.
- run: `art/units/fusion submarine/submarine_md_run.kf`.
- fortify: `art/units/fusion submarine/submarine_md_fortifya.kf`.
- attack: `art/units/fusion submarine/submarine_md_strikea.kf`.
- hurt: `art/units/fusion submarine/submarine_md_hurta.kf`.
- die: `art/units/fusion submarine/submarine_md_diea.kf`.

### military:fusion_transport

- Model: `src/Mods/Military/art/models/fusion_transport.glb` (193,660 bytes).
- Art: `ART_DEF_UNIT_FF_INVASION_SHIP_I`. Source scale: 0.5.
- NIF: `art/units/finalfrontier_invasionship/invasionship.nif`.
- KFM: `art/units/finalfrontier_invasionship/invasionship.kfm`.
- Geometry: 641 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.2336, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), fortify (9 channels), attack (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `f591a9a105a926e1d3a168a8d82f94668455972b3f5775863aeaaa953a89056b`.
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

- Model: `src/Mods/Military/art/models/galleas.glb` (328,320 bytes).
- Art: `ART_DEF_UNIT_GALLEAS`. Source scale: 0.10.
- NIF: `art/venice/galleas/galley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 1444 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3583, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `da81fba048c6257e1313adef08db61812a0a2f915752ab3083bf586ac631f1a7`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.
- Note: Missing texture: //frigate.dds

### military:galleass

- Model: `src/Mods/Military/art/models/galleass.glb` (313,796 bytes).
- Art: `ART_DEF_UNIT_GALLEASS`. Source scale: 0.14.
- NIF: `art/units/galleas/galley.nif`.
- KFM: `art/units/galleas/galley.kfm`.
- Geometry: 1444 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3583, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (41 channels), hurt (47 channels), die (47 channels).
- SHA-256: `a6aead9fd2b0eeb556c0359679f1438ca2f6a9648056ef2db53e32972eaa0778`.
- idle: `art/units/galleas/galley_md_idle.kf`.
- run: `art/units/galleas/galley_md_run.kf`.
- fortify: `art/units/galleas/galley_md_fortifya.kf`.
- attack: `art/units/galleas/galley_md_strikea.kf`.
- hurt: `art/units/galleas/galley_md_hurta.kf`.
- die: `art/units/galleas/galley_md_diea.kf`.
- Note: Missing texture: \\frigate.dds

### military:galleon

- Model: `src/Mods/Military/art/models/galleon.glb` (293,980 bytes).
- Art: `ART_DEF_UNIT_GALLEON`. Source scale: 0.2.
- NIF: `art/units/galleon/galleon.nif`.
- KFM: `art/units/galleon/galleon.kfm`.
- Geometry: 1023 triangles, 3 skins, 26 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.1892, feet Y=0.
- Clips: idle (39 channels), run (39 channels), fortify (39 channels), attack (39 channels), hurt (39 channels), die (39 channels).
- SHA-256: `9d9e5034acd55c6b6c2339130348586e0f6864e6e7cf202afee1b0365ef66074`.
- idle: `art/units/galleon/galleon_md_idle.kf`.
- run: `art/units/galleon/galleon_md_run.kf`.
- fortify: `art/units/galleon/galleon_md_fortifya.kf`.
- attack: `art/units/galleon/galleon_md_strikea.kf`.
- hurt: `art/units/galleon/galleon_md_hurta.kf`.
- die: `art/units/galleon/galleon_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:galley

- Model: `src/Mods/Military/art/models/galley.glb` (270,748 bytes).
- Art: `ART_DEF_UNIT_GALLEY`. Source scale: 0.18.
- NIF: `art/units/galley/galley.nif`.
- KFM: `art/units/galley/galley.kfm`.
- Geometry: 897 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4320, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `e4505ef64207e6f970cf6ea0160516444ea0330641632b14916a2b4713693b65`.
- idle: `art/units/galley/galley_md_idle.kf`.
- run: `art/units/galley/galley_md_run.kf`.
- fortify: `art/units/galley/galley_md_fortifya.kf`.
- attack: `art/units/galley/galley_md_strikea.kf`.
- hurt: `art/units/galley/galley_md_hurta.kf`.
- die: `art/units/galley/galley_md_diea.kf`.

### military:gatling_gun

- Model: `src/Mods/Military/art/models/gatling_gun.glb` (398,176 bytes).
- Art: `ART_DEF_UNIT_GATLING_GUN`. Source scale: 0.44.
- NIF: `art/units/gatlinggun/european/machinegunner.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 1282 triangles, 7 skins, 36 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4362, feet Y=0.
- Clips: idle (76 channels), run (76 channels), fortify (76 channels), attack (76 channels), hurt (76 channels).
- SHA-256: `b7339675ce1f33f3dafa31854e0a790c292262ef11f98122b09342a341af6943`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:german_panzer

- Model: `src/Mods/Military/art/models/german_panzer.glb` (293,440 bytes).
- Art: `ART_DEF_UNIT_GERMAN_PANZER`. Source scale: 0.47.
- NIF: `art/units/unique/german/panzer/panzer.nif`.
- KFM: `art/units/unique/german/panzer/panzer.kfm`.
- Geometry: 526 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.4921, radius 0.4800, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels).
- SHA-256: `2410a11b621a01620b218997075b6b8dd9b2f2614bc2e04e804b34cb73960375`.
- idle: `art/units/unique/german/panzer/panzer_md_idle.kf`.
- run: `art/units/unique/german/panzer/panzer_md_run.kf`.
- fortify: `art/units/unique/german/panzer/panzer_md_rangedfortify.kf`.
- attack: `art/units/unique/german/panzer/panzer_md_rangedstrike.kf`.
- hurt: `art/units/unique/german/panzer/panzer_md_hurta.kf`.

### military:ggmaster

- Model: `src/Mods/Military/art/models/ggmaster.glb` (229,348 bytes).
- Art: `ART_DEF_UNIT_GGMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/greenguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `402e19028d4a660edcdc807fc04bd6dd884607dad8a323316d57febed2ba8197`.
- idle: `art/units/missionary/christian/christianmissionary_md_idle.kf`.
- run: `art/units/missionary/christian/christianmissionary_md_run.kf`.
- fortify: `art/units/missionary/christian/christianmissionary_md_fortifya.kf`.
- die: `art/units/missionary/christian/christianmissionary_md_idle_die.kf`.

### military:gothic_cavalry

- Model: `src/Mods/Military/art/models/gothic_cavalry.glb` (724,008 bytes).
- Art: `ART_DEF_UNIT_GOTHIC_CAVALRY`. Source scale: 0.43.
- NIF: `art/goths/gothiccavalry/mongelkeshik.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1505 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3108, feet Y=0.
- Clips: idle (119 channels), run (119 channels), fortify (119 channels), attack (119 channels), hurt (119 channels), die (119 channels).
- SHA-256: `d397f7b496ddfd05059fe1b183c019b9e9ee2bdceeb93fe77c4a01cc11001c12`.
- idle: `art/units/unique/mongol/keshik/mongelkeshik_md_idle.kf`.
- run: `art/units/unique/mongol/keshik/mongelkeshik_md_run.kf`.
- fortify: `art/units/unique/mongol/keshik/mongelkeshik_md_fortifya.kf`.
- attack: `art/units/unique/mongol/keshik/mongelkeshik_md_strikea.kf`.
- hurt: `art/units/unique/mongol/keshik/mongelkeshik_md_hurta.kf`.
- die: `art/units/unique/mongol/keshik/mongelkeshik_md_diea.kf`.
- Note: Missing texture: //bb2.dds

### military:great_diplomat

- Model: `src/Mods/Military/art/models/great_diplomat.glb` (287,452 bytes).
- Art: `ART_DEF_UNIT_GREAT_DIPLOMAT`. Source scale: 0.46.
- NIF: `art/units/great diplomat/engineer.nif`.
- KFM: `art/units/great diplomat/engineer.kfm`.
- Geometry: 658 triangles, 1 skins, 16 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4016, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels).
- SHA-256: `f1d040ca5718481c08bbe35ade9eb8cb9a9ec139ba05c3e991e7d4fa79ed745b`.
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
- SHA-256: `ff232e6254230ffa2adb73af48a8190b0475e90ec09bf9582399f4584e9e7d0d`.
- idle: `art/units/warlord_ancient/warlord_ancient_md_idle.kf`.
- run: `art/units/warlord_ancient/warlord_ancient_md_run.kf`.
- fortify: `art/units/warlord_ancient/warlord_ancient_md_fortify.kf`.

### military:great_spy

- Model: `src/Mods/Military/art/models/great_spy.glb` (445,452 bytes).
- Art: `ART_DEF_UNIT_ANCIENT_GREAT_SPY`. Source scale: 0.46.
- NIF: `art/units/great_spy_ancient/great_spy_ancient.nif`.
- KFM: `art/units/great_spy_ancient/great_spy_ancient.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3774, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels).
- SHA-256: `3535c75b7ccb261f4119c803f67e2836a74fe69186b49f47789be6b0da106fa1`.
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
- Bounds: height 0.6000, radius 0.2528, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `ce37eeaf2094cc14b0bb5a7641326feaa79c3a7acfa10942b58a2755d0e04590`.
- idle: `art/units/spearman/spearman_md_idle.kf`.
- run: `art/units/spearman/spearman_md_run.kf`.
- fortify: `art/units/spearman/spearman_md_fortifya.kf`.
- attack: `art/units/spearman/spearman_md_strikea.kf`.
- hurt: `art/units/spearman/spearman_md_hurta.kf`.
- die: `art/units/spearman/spearman_md_diea.kf`.

### military:grenadier

- Model: `src/Mods/Military/art/models/grenadier.glb` (420,384 bytes).
- Art: `ART_DEF_UNIT_GRENADIER`. Source scale: 0.44.
- NIF: `art/units/grenadier/grenadier.nif`.
- KFM: `art/units/grenadier/grenadier.kfm`.
- Geometry: 749 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2608, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `3f9251ca3d31a9b23a7a178f2a4cfd5ec1ab8977f8e1f77f82ece49079138509`.
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
- SHA-256: `ca68bdf9aa679e75a74b43bbc92c856b3e0a5815333342158d07898e67f042b0`.
- idle: `art/units/guided_missile/guided_missile_md_idle.kf`.
- run: `art/units/guided_missile/guided_missile_md_run.kf`.
- attack: `art/units/guided_missile/guided_missile_md_strikea.kf`.

### military:gunship

- Model: `src/Mods/Military/art/models/gunship.glb` (256,244 bytes).
- Art: `ART_DEF_UNIT_GUNSHIP`. Source scale: 0.36.
- NIF: `art/units/gunship/gunship.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 1174 triangles, 2 skins, 7 joint bindings, 2 textures.
- Bounds: height 0.2906, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), fortify (28 channels), attack (28 channels), hurt (28 channels).
- SHA-256: `61dd1fe5afb2099eb9e65e8af5594dc333c381424a227dabb12ca7f227de4945`.
- idle: `art/units/gunship/gunship_md_idle.kf`.
- run: `art/units/gunship/gunship_md_run.kf`.
- fortify: `art/units/gunship/gunship_md_rangedfortify.kf`.
- attack: `art/units/gunship/gunship_md_rangedstrike.kf`.
- hurt: `art/units/gunship/gunship_md_hurta.kf`.

### military:hacker

- Model: `src/Mods/Military/art/models/hacker.glb` (177,856 bytes).
- Art: `ART_DEF_UNIT_HACKER`. Source scale: 0.46.
- NIF: `art/units/hacker/modernscientist.nif`.
- KFM: `art/units/hacker/modernscientist.kfm`.
- Geometry: 568 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.1744, radius 0.4800, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels).
- SHA-256: `352ab3dbbea3978877785b9b68d954cdb327f01d6df3c745637c45d3ad8bf015`.
- idle: `art/units/hacker/modernscientist_md_idle.kf`.
- run: `art/units/greatpeople/modernscientist/modernscientist_md_run.kf`.
- fortify: `art/units/hacker/modernscientist_md_fortify.kf`.

### military:halftrack

- Model: `src/Mods/Military/art/models/halftrack.glb` (392,564 bytes).
- Art: `ART_DEF_UNIT_HALFTRACK`. Source scale: 0.6.
- NIF: `art/units/antiair halftrack/ma_ht.nif`.
- KFM: `art/units/antiair halftrack/modernarmor.kfm`.
- Geometry: 1152 triangles, 2 skins, 6 joint bindings, 2 textures.
- Bounds: height 0.3214, radius 0.4800, feet Y=0.
- Clips: idle (43 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels).
- SHA-256: `ff48fe3b93cc71f92eaba5bd6853d62e013667ef402da483b55940fc2bd6d541`.
- idle: `art/units/antiair halftrack/modernarmor_md_idle.kf`.
- run: `art/units/antiair halftrack/modernarmor_md_run.kf`.
- fortify: `art/units/antiair halftrack/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/antiair halftrack/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/antiair halftrack/modernarmor_md_hurta.kf`.

### military:heavy_cavalry

- Model: `src/Mods/Military/art/models/heavy_cavalry.glb` (644,196 bytes).
- Art: `ART_DEF_UNIT_HEAVY_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavycavalry/heavycavalry.nif`.
- KFM: `art/units/charlemagne_heavycavalry/heavycavalry.kfm`.
- Geometry: 1227 triangles, 3 skins, 57 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2835, feet Y=0.
- Clips: idle (122 channels), run (122 channels), fortify (122 channels), attack (122 channels), hurt (122 channels), die (122 channels).
- SHA-256: `bfa7db407918879c046fb879ca3b9d7d602432184876d28739bd990da9051c40`.
- idle: `art/units/charlemagne_heavycavalry/heavycavalry_md_idle.kf`.
- run: `art/units/charlemagne_heavycavalry/heavycavalry_md_run.kf`.
- fortify: `art/units/charlemagne_heavycavalry/heavycavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavycavalry/heavycavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavycavalry/heavycavalry_md_hurta.kf`.
- die: `art/units/charlemagne_heavycavalry/heavycavalry_md_diea.kf`.

### military:heavy_pikeman

- Model: `src/Mods/Military/art/models/heavy_pikeman.glb` (273,624 bytes).
- Art: `ART_DEF_UNIT_HEAVY_SPEARMAN`. Source scale: 0.42.
- NIF: `art/units/charlemagne_heavyspearman/heavyspearman.nif`.
- KFM: `art/units/charlemagne_heavyspearman/heavyspearman.kfm`.
- Geometry: 608 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.5510, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `9b27eb51f7bf17c51f4fcdd16274a3ca9f7c23eca2d9d8e9d4f37e71e57922b9`.
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
- Bounds: height 0.6000, radius 0.3253, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `fdf793c73281371d84ec12891994c53eeeddffa04ae38a2655a68b056e791866`.
- idle: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_idle.kf`.
- run: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_run.kf`.
- fortify: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_fortifya.kf`.
- attack: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_strikea.kf`.
- hurt: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_hurta.kf`.
- die: `art/units/charlemagne_heavyswordsman/heavyswordsman_md_diea.kf`.

### military:heavy_tank_m26

- Model: `src/Mods/Military/art/models/heavy_tank_m26.glb` (177,704 bytes).
- Art: `ART_DEF_UNIT_HEAVY_TANK`. Source scale: 0.38.
- NIF: `art/units/heavy tank/european/m26.nif`.
- KFM: `art/units/heavy tank/european/m26.kfm`.
- Geometry: 822 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.3788, radius 0.4800, feet Y=0.
- Clips: idle (12 channels), run (12 channels), fortify (12 channels), attack (12 channels), hurt (12 channels).
- SHA-256: `f86de207729d0ad39a07f7532687a1bec364f051f10a1c9809a98409c74f5a5d`.
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
- Bounds: height 0.6000, radius 0.2923, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), die (48 channels).
- SHA-256: `92e609c8824f56df23166bb85178125246d343d5dbd5367bd075e13fc54c6bd4`.
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
- Bounds: height 0.6000, radius 0.3263, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `64d218a2e74f71dfb78774c581e71c9ca85cc8c46970a3d0bca6520ec4a76e29`.
- idle: `art/scotland/highlander/swordsman_md_idle.kf`.
- run: `art/scotland/highlander/swordsman_md_run.kf`.
- fortify: `art/scotland/highlander/swordsman_md_fortifya.kf`.
- attack: `art/scotland/highlander/swordsman_md_strikea.kf`.
- hurt: `art/scotland/highlander/swordsman_md_hurta.kf`.
- die: `art/scotland/highlander/swordsman_md_diea.kf`.

### military:hindu_missionary

- Model: `src/Mods/Military/art/models/hindu_missionary.glb` (259,836 bytes).
- Art: `ART_DEF_UNIT_HINDU_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/hindu/hindu_missionary.nif`.
- KFM: `art/units/missionary/hindu/hindu_missionary.kfm`.
- Geometry: 584 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3371, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `e8ad6921e363734b081453ef3197713dc31dbe7f2e5b20c1a83ae5a42ed91a99`.
- idle: `art/units/missionary/hindu/hindu_missionary_md_idle.kf`.
- run: `art/units/missionary/hindu/hindu_missionary_md_run.kf`.
- fortify: `art/units/missionary/hindu/hindu_missionary_md_fortify.kf`.
- die: `art/units/missionary/hindu/hindu_missionary_md_idle_die.kf`.

### military:hitech_robot

- Model: `src/Mods/Military/art/models/hitech_robot.glb` (1,073,676 bytes).
- Art: `ART_DEF_UNIT_JAL`. Source scale: 0.62.
- NIF: `art/units/afterworld_gravebringer/jal/gravebringer.nif`.
- KFM: `art/units/afterworld_gravebringer/jal/gravebringer.kfm`.
- Geometry: 3438 triangles, 8 skins, 35 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.1416, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels), hurt (55 channels), die (55 channels).
- SHA-256: `1ca2db88c6fbf9bcef706bfc7aacf24842a34837e2e421ff2cf741497745eb87`.
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
- Bounds: height 0.4972, radius 0.4800, feet Y=0.
- Clips: idle (214 channels), run (214 channels), fortify (214 channels), attack (214 channels), hurt (214 channels), die (214 channels).
- SHA-256: `66f35a2291fdc0d306baa38260b7399d68219fe47e4b914af8c5a608b6d75689`.
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
- Bounds: height 0.5433, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `8b3449882ccc13e3e481060cd54026cf7c36081711cebdb0e8e6ecb7a82e9e85`.
- idle: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_idle.kf`.
- run: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_run.kf`.
- fortify: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_fortifya.kf`.
- attack: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_strikea.kf`.
- hurt: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_hurta.kf`.
- die: `art/units/unique_holyromanempire_landsknecht/unique_holyroman_landsknecht_md_diea.kf`.

### military:horse_archer

- Model: `src/Mods/Military/art/models/horse_archer.glb` (596,304 bytes).
- Art: `ART_DEF_UNIT_HORSE_ARCHER`. Source scale: 0.42.
- NIF: `art/units/horsearcher/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 1116 triangles, 3 skins, 37 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2656, feet Y=0.
- Clips: idle (107 channels), run (107 channels), fortify (107 channels), attack (107 channels), hurt (107 channels), die (107 channels).
- SHA-256: `0f8b3ff1774cc73d890ffaa98e3a1aef50958c1fedd95fa031e85114cbf8c101`.
- idle: `art/units/horsearcher/horsearcher_md_idle.kf`.
- run: `art/units/horsearcher/horsearcher_md_run.kf`.
- fortify: `art/units/horsearcher/horsearcher_md_fortifya.kf`.
- attack: `art/units/horsearcher/horsearcher_md_strikea.kf`.
- hurt: `art/units/horsearcher/horsearcher_md_hurta.kf`.
- die: `art/units/horsearcher/horsearcher_md_diea.kf`.
- Note: Missing texture: HorseArcher_weapons_64.dds

### military:horseman

- Model: `src/Mods/Military/art/models/horseman.glb` (606,484 bytes).
- Art: `ART_DEF_UNIT_HORSEMAN_EURASIAN`. Source scale: 0.42.
- NIF: `art/units/horseman_eurasian/horseman_eurasian.nif`.
- KFM: `art/units/horseman_eurasian/horseman_eurasian.kfm`.
- Geometry: 1183 triangles, 2 skins, 32 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3254, feet Y=0.
- Clips: idle (103 channels), run (103 channels), fortify (103 channels), attack (103 channels), hurt (103 channels), die (103 channels).
- SHA-256: `d715f2913dec4d079d42170acbeaac33cf78f8d27fa5eebbe1fdd8a7e59408cd`.
- idle: `art/units/horseman_eurasian/horseman_eurasian_md_idle.kf`.
- run: `art/units/horseman_eurasian/horseman_eurasian_md_run.kf`.
- fortify: `art/units/horseman_eurasian/horseman_eurasian_md_fortifya.kf`.
- attack: `art/units/horseman_eurasian/horseman_eurasian_md_strikea.kf`.
- hurt: `art/units/horseman_eurasian/horseman_eurasian_md_hurta.kf`.
- die: `art/units/horseman_eurasian/horseman_eurasian_md_diea.kf`.

### military:hungarian_huszar_cavalry

- Model: `src/Mods/Military/art/models/hungarian_huszar_cavalry.glb` (585,700 bytes).
- Art: `ART_DEF_UNIT_HUSZAR_CAVALRY`. Source scale: 0.42.
- NIF: `art/hungary/huszar cavalry/russiancossack.nif`.
- KFM: `art/hungary/huszar cavalry/russiancossack.kfm`.
- Geometry: 1076 triangles, 3 skins, 40 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3094, feet Y=0.
- Clips: idle (95 channels), run (95 channels), fortify (95 channels), attack (95 channels), hurt (95 channels), die (95 channels).
- SHA-256: `8cd29a42e3e95d16b81f79ca64f93af2e0a28e70a2187050043df989c061571f`.
- idle: `art/hungary/huszar cavalry/russiancossack_md_idle.kf`.
- run: `art/hungary/huszar cavalry/russiancossack_md_run.kf`.
- fortify: `art/hungary/huszar cavalry/russiancossack_md_fortifya.kf`.
- attack: `art/hungary/huszar cavalry/russiancossack_md_strikea.kf`.
- hurt: `art/hungary/huszar cavalry/russiancossack_md_hurta.kf`.
- die: `art/hungary/huszar cavalry/russiancossack_md_diea.kf`.

### military:huns_tarkan

- Model: `src/Mods/Military/art/models/huns_tarkan.glb` (1,149,340 bytes).
- Art: `ART_DEF_UNIT_HUNS_TARKAN`. Source scale: 0.42.
- NIF: `art/huns/tarkan/horsearcher.nif`.
- KFM: `art/huns/tarkan/horsearcher.kfm`.
- Geometry: 1132 triangles, 3 skins, 37 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2587, feet Y=0.
- Clips: idle (207 channels), run (207 channels), fortify (207 channels), attack (207 channels), hurt (207 channels), die (207 channels).
- SHA-256: `8904761cbace4b09ec5cabaa5074a51e5c59329a1b19a21c6f40b5a1b221f804`.
- idle: `art/huns/tarkan/horsearcher_md_idle.kf`.
- run: `art/huns/tarkan/horsearcher_md_run.kf`.
- fortify: `art/huns/tarkan/horsearcher_md_fortifya.kf`.
- attack: `art/huns/tarkan/horsearcher_md_strikea.kf`.
- hurt: `art/huns/tarkan/horsearcher_md_hurta.kf`.
- die: `art/huns/tarkan/horsearcher_md_diea.kf`.
- Note: Missing texture: HorseArcher_weapons_64.dds

### military:hybrid_gunship

- Model: `src/Mods/Military/art/models/hybrid_gunship.glb` (247,660 bytes).
- Art: `ART_DEF_UNIT_HYBRID_GUNSHIP`. Source scale: 0.30.
- NIF: `art/units/hybrid gunship/heavymiss.nif`.
- KFM: `art/units/gunship/gunship.kfm`.
- Geometry: 1859 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3749, feet Y=0.
- Clips: idle (26 channels), run (26 channels), fortify (26 channels), attack (26 channels), hurt (26 channels).
- SHA-256: `86f51edf4634ee4217b10e128b08da62b76f055c97cd0f957966276fd65f35cb`.
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
- SHA-256: `70b815e0bd3c7ac98698829da740a26fcc84b86e3207d737ee6d71b4dc36f953`.
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
- SHA-256: `6f84a6b4b866de2831b830afd49ded30fd850195419d7ca861d551bee72a4685`.
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

- Model: `src/Mods/Military/art/models/incan_quechua.glb` (280,516 bytes).
- Art: `ART_DEF_UNIT_INCAN_QUECHUA`. Source scale: 0.44.
- NIF: `art/units/unique/inca/quechua/incanquechua.nif`.
- KFM: `art/units/unique/inca/quechua/incanquechua.kfm`.
- Geometry: 648 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3599, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `1cdb0deac73f3a76a9e4ee080cf17a7f88aa9fe07ffc6e6a58eb7bd57a1d8e9c`.
- idle: `art/units/unique/inca/quechua/incanquechua_md_idle.kf`.
- run: `art/units/unique/inca/quechua/incanquechua_md_run.kf`.
- fortify: `art/units/unique/inca/quechua/incanquechua_md_fortifya.kf`.
- attack: `art/units/unique/inca/quechua/incanquechua_md_strikea.kf`.
- hurt: `art/units/unique/inca/quechua/incanquechua_md_hurta.kf`.
- die: `art/units/unique/inca/quechua/incanquechua_md_diea.kf`.

### military:indian_fast_worker

- Model: `src/Mods/Military/art/models/indian_fast_worker.glb` (237,852 bytes).
- Art: `ART_DEF_UNIT_INDIAN_FAST_WORKER`. Source scale: 0.44.
- NIF: `art/units/unique/india/worker/indianfastworker.nif`.
- KFM: `art/units/unique/india/worker/indianfastworker.kfm`.
- Geometry: 929 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3164, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels).
- SHA-256: `1de16faa358fd06ef6f3986eef4267c3682a4830b69155ad503900594bb29168`.
- idle: `art/units/unique/india/worker/indianfastworker_md_idle.kf`.
- run: `art/units/unique/india/worker/indianfastworker_md_run.kf`.
- fortify: `art/units/unique/india/worker/indianfastworker_md_fortify.kf`.
- Note: Missing texture: Worker_tools.dds

### military:infantry

- Model: `src/Mods/Military/art/models/infantry.glb` (352,324 bytes).
- Art: `ART_DEF_UNIT_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/infantry/infantry.nif`.
- KFM: `art/units/infantry/infantry.kfm`.
- Geometry: 658 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2792, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `d57575dd07fcb82c7bee2d11cb9b832da5e81bb50a0f73875288aa9a9962cdcf`.
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
- Bounds: height 0.6000, radius 0.4309, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels).
- SHA-256: `0d63843243493c6cc62f9eb81c8129595d0d8e8359743d42a92a036c0ae61aa2`.
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
- SHA-256: `1a392b17ee78eb92c3a6467ea6a3e565a7cd072bfe139a1297f934bb532412b3`.
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
- Bounds: height 0.6000, radius 0.3263, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `83d9be98fc0f4cf7b0bcd6fb5a1d57c7e5e57a27a4a1b6f184be483e1d967df5`.
- idle: `art/ireland/fianna/swordsman_md_idle.kf`.
- run: `art/ireland/fianna/swordsman_md_run.kf`.
- fortify: `art/ireland/fianna/swordsman_md_fortifya.kf`.
- attack: `art/ireland/fianna/swordsman_md_strikea.kf`.
- hurt: `art/ireland/fianna/swordsman_md_hurta.kf`.
- die: `art/ireland/fianna/swordsman_md_diea.kf`.

### military:iron_frigate

- Model: `src/Mods/Military/art/models/iron_frigate.glb` (307,724 bytes).
- Art: `ART_DEF_UNIT_IRON_FRIGATE`. Source scale: 0.19.
- NIF: `art/units/iron frigate/hms warrior.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1393 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.4769, radius 0.4800, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `2c18ea078fd66139d91778112b83b131e7103965724e88e40f093652e7f75c8d`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.
- Note: Missing texture: //hmswarrior256.dds

### military:ironclad

- Model: `src/Mods/Military/art/models/ironclad.glb` (301,408 bytes).
- Art: `ART_DEF_UNIT_IRONCLAD`. Source scale: 0.52.
- NIF: `art/units/ironclad/ironclad.nif`.
- KFM: `art/units/ironclad/ironclad.kfm`.
- Geometry: 445 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.2585, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `008aa491d4786f324290fb4171e5f13a817e75d2169f14a04144ddf9e48f7607`.
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
- Bounds: height 0.3537, radius 0.4800, feet Y=0.
- Clips: idle (86 channels), run (86 channels), fortify (86 channels), attack (86 channels), hurt (86 channels), die (86 channels).
- SHA-256: `4e2a08e7507a72abdfdc63ba70e111f1ec54ead1819ebfc3d60c85dc3c148cf4`.
- idle: `art/units/ironclad cruiser/ww1_cr_rus_md_idle.kf`.
- run: `art/units/ironclad cruiser/ww1_cr_rus_md_run.kf`.
- fortify: `art/units/ironclad cruiser/ww1_cr_rus_md_fortify.kf`.
- attack: `art/units/ironclad cruiser/ww1_cr_rus_md_strike.kf`.
- hurt: `art/units/ironclad cruiser/ww1_cr_rus_md_hurt.kf`.
- die: `art/units/ironclad cruiser/ww1_cr_rus_md_die.kf`.

### military:iroquois_mohawkarcher

- Model: `src/Mods/Military/art/models/iroquois_mohawkarcher.glb` (365,332 bytes).
- Art: `ART_DEF_UNIT_IROQUOIS_MOHAWKARCHER`. Source scale: 0.44.
- NIF: `art/units/unique/iroquois/mohawk longbowman/archer.nif`.
- KFM: `art/units/archer/archer.kfm`.
- Geometry: 848 triangles, 3 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2395, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `a177e05f439851ab98c4e6ddc100795240223d9701ce22156a1a98b2a8b00615`.
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
- Bounds: height 0.1707, radius 0.4800, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), die (46 channels).
- SHA-256: `3f3473e47fe337c0f261f510e0e163c39f67a9bffb36d6c8cd6c5a9f4f6cf849`.
- idle: `art/units/missionary/islamic/islamic_missionary_md_idle.kf`.
- run: `art/units/missionary/islamic/islamic_missionary_md_run.kf`.
- fortify: `art/units/missionary/islamic/islamic_missionary_md_fortify.kf`.
- die: `art/units/missionary/islamic/islamic_missionary_md_idle_die.kf`.

### military:japan_samurai

- Model: `src/Mods/Military/art/models/japan_samurai.glb` (302,376 bytes).
- Art: `ART_DEF_UNIT_JAPAN_SAMURAI`. Source scale: 0.44.
- NIF: `art/units/unique/japan/samurai/japanesesamurai.nif`.
- KFM: `art/units/unique/japan/samurai/japanesesamurai.kfm`.
- Geometry: 801 triangles, 2 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2748, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `19954ddb23d0c59f054207833a9b93ae7f3256113a3136b3d3ff0684db6709a5`.
- idle: `art/units/unique/japan/samurai/japanesesamurai_md_idle.kf`.
- run: `art/units/unique/japan/samurai/japanesesamurai_md_run.kf`.
- fortify: `art/units/unique/japan/samurai/japanesesamurai_md_fortifya.kf`.
- attack: `art/units/unique/japan/samurai/japanesesamurai_md_strikea.kf`.
- hurt: `art/units/unique/japan/samurai/japanesesamurai_md_hurta.kf`.
- die: `art/units/unique/japan/samurai/japanesesamurai_md_diea.kf`.

### military:javelineer

- Model: `src/Mods/Military/art/models/javelineer.glb` (317,044 bytes).
- Art: `ART_DEF_UNIT_JAVELINEER`. Source scale: 0.44.
- NIF: `art/units/javelineer/european/javelineer.nif`.
- KFM: `art/units/javelineer/animations/javelineer_2.kfm`.
- Geometry: 620 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3723, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `89986aa42af8f02f3180548c893e012feb6d1f94ba4e6ffc835086e0485c9005`.
- idle: `art/units/javelineer/animations/maliskirmisher_md_idle.kf`.
- run: `art/units/javelineer/animations/maliskirmisher_md_run.kf`.
- fortify: `art/units/javelineer/animations/maliskirmisher_md_fortifya.kf`.
- attack: `art/units/javelineer/animations/maliskirmisher_md_strikea.kf`.
- hurt: `art/units/javelineer/animations/maliskirmisher_md_hurta.kf`.
- die: `art/units/javelineer/animations/maliskirmisher_md_diea.kf`.

### military:jeep

- Model: `src/Mods/Military/art/models/jeep.glb` (458,796 bytes).
- Art: `ART_DEF_UNIT_JEEP`. Source scale: 0.45.
- NIF: `art/units/jeep/willys.nif`.
- KFM: `art/units/jeep/willys.kfm`.
- Geometry: 2443 triangles, 3 skins, 22 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2576, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels).
- SHA-256: `cb1853d931d8741c05cec8dd81f54172e9dcf62889ac6ffd1fecc28a2fac1a16`.
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
- Bounds: height 0.2751, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), hurt (23 channels).
- SHA-256: `dbccda2372613d3165981e70d3ff4798a4f23f4fe539fcb73fd5f1b84329ac86`.
- idle: `art/units/jetfighter/jetfighter_md_idle.kf`.
- run: `art/units/jetfighter/jetfighter_md_run.kf`.
- hurt: `art/units/jetfighter/jetfighter_md_hurt.kf`.

### military:jewish_missionary

- Model: `src/Mods/Military/art/models/jewish_missionary.glb` (170,792 bytes).
- Art: `ART_DEF_UNIT_JEWISH_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/jewish/jewishmissionary.nif`.
- KFM: `art/units/missionary/jewish/jewishmissionary.kfm`.
- Geometry: 592 triangles, 2 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.2988, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), die (45 channels).
- SHA-256: `c4a7e57dd3fce4b9cfe8f780eb1a6e7e44543821482856b3ec680bba3e791af7`.
- idle: `art/units/missionary/jewish/jewishmissionary_md_idle.kf`.
- run: `art/units/missionary/jewish/jewishmissionary_md_run.kf`.
- fortify: `art/units/missionary/jewish/jewishmissionary_md_fortifya.kf`.
- die: `art/units/missionary/jewish/jewishmissionary_md_idle_die.kf`.

### military:khmer_ballista_elephant

- Model: `src/Mods/Military/art/models/khmer_ballista_elephant.glb` (619,744 bytes).
- Art: `ART_DEF_UNIT_KHMER_BALLISTA_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/unique_khmer_elephantballista/khmer_elephantballista.nif`.
- KFM: `art/units/unique_khmer_elephantballista/khmer_elephantballista.kfm`.
- Geometry: 1414 triangles, 3 skins, 46 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2757, feet Y=0.
- Clips: idle (117 channels), run (117 channels), attack (117 channels), die (117 channels).
- SHA-256: `1c56e17b1d4513b0dbdefe9ad4bca44f6276c00a7a19393aa564db146a0ec53b`.
- idle: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_idle.kf`.
- run: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_run.kf`.
- attack: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_rangedstrikea.kf`.
- die: `art/units/unique_khmer_elephantballista/khmer_elephantballista_md_rangeddiea.kf`.

### military:knight

- Model: `src/Mods/Military/art/models/knight.glb` (615,624 bytes).
- Art: `ART_DEF_UNIT_KNIGHT`. Source scale: 0.42.
- NIF: `art/units/knight/knight.nif`.
- KFM: `art/units/knight/knight.kfm`.
- Geometry: 1143 triangles, 3 skins, 37 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4800, feet Y=0.
- Clips: idle (109 channels), run (109 channels), fortify (109 channels), attack (109 channels), hurt (109 channels), die (109 channels).
- SHA-256: `3a708dbd27a242dbb847e5a13846e3b09b0937d839f1e440b3f01baceb8c8b93`.
- idle: `art/units/knight/knight_md_idle.kf`.
- run: `art/units/knight/knight_md_run.kf`.
- fortify: `art/units/knight/knight_md_fortifya.kf`.
- attack: `art/units/knight/knight_md_strikea.kf`.
- hurt: `art/units/knight/knight_md_hurta.kf`.
- die: `art/units/knight/knight_md_diea.kf`.
- Note: Missing texture: Knight_horse_128.dds

### military:korean_hwacha

- Model: `src/Mods/Military/art/models/korean_hwacha.glb` (406,976 bytes).
- Art: `ART_DEF_UNIT_KOREAN_HWACHA`. Source scale: 0.50.
- NIF: `art/units/hwacha_korean/hwacha.nif`.
- KFM: `art/units/hwacha_korean/hwacha.kfm`.
- Geometry: 1298 triangles, 2 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2501, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels).
- SHA-256: `3632c5b62c95ef7eb2a59bb270c0141e017224d900e7edb31bb8cb85d01f09fa`.
- idle: `art/units/hwacha_korean/hwacha_md_idle.kf`.
- run: `art/units/hwacha_korean/hwacha_md_run.kf`.
- fortify: `art/units/hwacha_korean/hwacha_md_rangedfortify.kf`.
- attack: `art/units/hwacha_korean/hwacha_md_rangedstrike.kf`.
- hurt: `art/units/hwacha_korean/hwacha_md_hurta.kf`.

### military:lancer

- Model: `src/Mods/Military/art/models/lancer.glb` (695,528 bytes).
- Art: `ART_DEF_UNIT_LANCER`. Source scale: 0.42.
- NIF: `art/units/lancer/european/lancer.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1228 triangles, 3 skins, 37 joint bindings, 5 textures.
- Bounds: height 0.6000, radius 0.3006, feet Y=0.
- Clips: idle (119 channels), run (119 channels), fortify (119 channels), attack (119 channels), hurt (119 channels), die (119 channels).
- SHA-256: `8822f62041df108fc8e90904746125cb80a8e8dae416633f911d0da2f05190ff`.
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
- Bounds: height 0.3461, radius 0.4800, feet Y=0.
- Clips: idle (8 channels), run (8 channels), fortify (8 channels), attack (8 channels), hurt (8 channels).
- SHA-256: `723cd10813c1456f0b9c1eef353fbe50ace07d7a66f78c385d7fef5b6280ec1d`.
- idle: `art/units/light antiair gun/lightaa_md_idle.kf`.
- run: `art/units/light antiair gun/lightaa_md_run.kf`.
- fortify: `art/units/light antiair gun/lightaa_md_rangedfortify.kf`.
- attack: `art/units/light antiair gun/lightaa_md_rangedstrike.kf`.
- hurt: `art/units/light antiair gun/lightaa_md_hurta.kf`.

### military:light_artillery

- Model: `src/Mods/Military/art/models/light_artillery.glb` (168,416 bytes).
- Art: `ART_DEF_UNIT_LIGHT_ARTILLERY`. Source scale: 0.7.
- NIF: `art/units/light artillery/artillery.nif`.
- KFM: `art/units/artillery/artillery.kfm`.
- Geometry: 442 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.2290, radius 0.4800, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels).
- SHA-256: `c215005d5e7e85e3a65b23929f1649f710d97bbc38802e0004622723bc446f77`.
- idle: `art/units/artillery/artillery_md_idle.kf`.
- run: `art/units/artillery/artillery_md_run.kf`.
- fortify: `art/units/artillery/artillery_md_rangedfortify.kf`.
- attack: `art/units/artillery/artillery_md_rangedstrike.kf`.
- hurt: `art/units/artillery/artillery_md_hurta.kf`.

### military:light_cavalry

- Model: `src/Mods/Military/art/models/light_cavalry.glb` (635,488 bytes).
- Art: `ART_DEF_UNIT_LIGHT_CAVALRY`. Source scale: 0.42.
- NIF: `art/units/charlemagne_lightcavalry/lightcavalry.nif`.
- KFM: `art/units/charlemagne_lightcavalry/lightcavalry.kfm`.
- Geometry: 1306 triangles, 2 skins, 43 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2766, feet Y=0.
- Clips: idle (108 channels), run (108 channels), fortify (108 channels), attack (108 channels), hurt (108 channels), die (108 channels).
- SHA-256: `39e0e6968925fe4c75fcc79e09b4a8a41cbdf23612f5c413ae5290232ae2f8ed`.
- idle: `art/units/charlemagne_lightcavalry/lightcavalry_md_idle.kf`.
- run: `art/units/charlemagne_lightcavalry/lightcavalry_md_run.kf`.
- fortify: `art/units/charlemagne_lightcavalry/lightcavalry_md_fortifya.kf`.
- attack: `art/units/charlemagne_lightcavalry/lightcavalry_md_strikea.kf`.
- hurt: `art/units/charlemagne_lightcavalry/lightcavalry_md_hurta.kf`.
- die: `art/units/charlemagne_lightcavalry/lightcavalry_md_diea.kf`.

### military:light_swordsman

- Model: `src/Mods/Military/art/models/light_swordsman.glb` (292,460 bytes).
- Art: `ART_DEF_UNIT_SWORDSMAN`. Source scale: 0.44.
- NIF: `art/units/swordsman/swordsman.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3263, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `67ded6aaa48a5b4b9c921b3a91e2e8c65ee6ab7cd054df6ada24e7b48ff25f43`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:light_tank

- Model: `src/Mods/Military/art/models/light_tank.glb` (239,332 bytes).
- Art: `ART_DEF_UNIT_VICKERS_E`. Source scale: 0.5.
- NIF: `art/units/light tank/european/vickers_e.nif`.
- KFM: `art/units/light tank/european/vickers_e.kfm`.
- Geometry: 712 triangles, 2 skins, 4 joint bindings, 2 textures.
- Bounds: height 0.4659, radius 0.4800, feet Y=0.
- Clips: idle (10 channels), run (10 channels), fortify (10 channels), attack (10 channels), hurt (10 channels).
- SHA-256: `8a6dd171205e67186f85f088c5040dd2458a59373b5b6a0e7c3d0f2973878003`.
- idle: `art/units/light tank/european/vickers_e_md_idle.kf`.
- run: `art/units/light tank/european/vickers_e_md_run.kf`.
- fortify: `art/units/light tank/european/vickers_e_md_rangedfortify.kf`.
- attack: `art/units/light tank/european/vickers_e_md_rangedstrike.kf`.
- hurt: `art/units/light tank/european/vickers_e_md_hurta.kf`.

### military:lion

- Model: `src/Mods/Military/art/models/lion.glb` (321,212 bytes).
- Art: `ART_DEF_UNIT_LION`. Source scale: 0.61.
- NIF: `art/units/lion/lion.nif`.
- KFM: `art/units/lion/lion.kfm`.
- Geometry: 472 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.5546, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `a60802039cf91d239cf61f25869114ae09a584400083a079be033b81e86c402a`.
- idle: `art/units/lion/lion_md_idle.kf`.
- run: `art/units/lion/lion_md_run.kf`.
- fortify: `art/units/lion/lion_md_fortifya.kf`.
- attack: `art/units/lion/lion_md_strikea.kf`.
- hurt: `art/units/lion/lion_md_hurta.kf`.
- die: `art/units/lion/lion_md_diea.kf`.

### military:lithuanian_vytis

- Model: `src/Mods/Military/art/models/lithuanian_vytis.glb` (633,492 bytes).
- Art: `ART_DEF_UNIT_VYTIS`. Source scale: 0.42.
- NIF: `art/lithuania/vytis/heavycavalry.nif`.
- KFM: `art/lithuania/vytis/heavycavalry.kfm`.
- Geometry: 1138 triangles, 2 skins, 39 joint bindings, 4 textures.
- Bounds: height 0.5659, radius 0.4800, feet Y=0.
- Clips: idle (122 channels), run (122 channels), fortify (122 channels), attack (122 channels), hurt (122 channels), die (122 channels).
- SHA-256: `0daea43efad00b79a06b428f0df737943ad86e6bddf51b792e6e5e2eea3cab8d`.
- idle: `art/lithuania/vytis/heavycavalry_md_idle.kf`.
- run: `art/lithuania/vytis/heavycavalry_md_run.kf`.
- fortify: `art/lithuania/vytis/heavycavalry_md_fortifya.kf`.
- attack: `art/lithuania/vytis/heavycavalry_md_strikea.kf`.
- hurt: `art/lithuania/vytis/heavycavalry_md_hurta.kf`.
- die: `art/lithuania/vytis/heavycavalry_md_diea.kf`.

### military:littoral_combat_ship

- Model: `src/Mods/Military/art/models/littoral_combat_ship.glb` (368,868 bytes).
- Art: `ART_DEF_UNIT_LITTORAL_COMBAT_SHIP`. Source scale: 0.7.
- NIF: `art/units/littoral combat ship/dd_sens.nif`.
- KFM: `art/units/destroyer/destroyer.kfm`.
- Geometry: 955 triangles, 2 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.1830, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `7e496198ade82baec959f67abacd221b25fb5b9e9869be40fefa9022120557ca`.
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
- Bounds: height 0.6000, radius 0.4593, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `adce02e5ed20fe7bbd9cb84a9d33692465a0a4614f4765c56d2ac5f0dcbf6f3a`.
- idle: `art/lombards/gasindii/greekphalanx_md_idle.kf`.
- run: `art/lombards/gasindii/greekphalanx_md_run.kf`.
- fortify: `art/lombards/gasindii/greekphalanx_md_fortifya.kf`.
- attack: `art/lombards/gasindii/greekphalanx_md_strikea.kf`.
- hurt: `art/lombards/gasindii/greekphalanx_md_hurta.kf`.
- die: `art/lombards/gasindii/greekphalanx_md_diea.kf`.

### military:longbowman

- Model: `src/Mods/Military/art/models/longbowman.glb` (387,596 bytes).
- Art: `ART_DEF_UNIT_LONGBOWMAN`. Source scale: 0.44.
- NIF: `art/units/longbowman/longbowman.nif`.
- KFM: `art/units/longbowman/longbowman.kfm`.
- Geometry: 718 triangles, 2 skins, 20 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3624, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels), attack (59 channels), hurt (59 channels), die (59 channels).
- SHA-256: `2413eb710aac15aa198af764a6bdca06360a7c75156f89c06b101819361bebe6`.
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
- Bounds: height 0.5804, radius 0.4800, feet Y=0.
- Clips: idle (58 channels), run (58 channels), fortify (58 channels), attack (58 channels), hurt (58 channels), die (58 channels).
- SHA-256: `42a6322424f54c37242c87adad2783945aeaf1f7189e60036a3295474a4ef9fd`.
- idle: `art/units/maceman/maceman_md_idle.kf`.
- run: `art/units/maceman/maceman_md_run.kf`.
- fortify: `art/units/maceman/maceman_md_fortifya.kf`.
- attack: `art/units/maceman/maceman_md_strikea.kf`.
- hurt: `art/units/maceman/maceman_md_hurta.kf`.
- die: `art/units/maceman/maceman_md_diea.kf`.

### military:machine_gun

- Model: `src/Mods/Military/art/models/machine_gun.glb` (276,272 bytes).
- Art: `ART_DEF_UNIT_MACHINE_GUN`. Source scale: 0.44.
- NIF: `art/units/machinegun/machinegunner.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 774 triangles, 3 skins, 23 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2660, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels), hurt (61 channels).
- SHA-256: `25be327c11e346419833fb3b5cb6a9431dc927cc917fcf831cf8392977537b69`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:mali_skirmisher

- Model: `src/Mods/Military/art/models/mali_skirmisher.glb` (330,200 bytes).
- Art: `ART_DEF_UNIT_MALI_SKIRMISHER`. Source scale: 0.44.
- NIF: `art/units/unique/mali/skirmisher/maliskirmisher.nif`.
- KFM: `art/units/unique/mali/skirmisher/maliskirmisher.kfm`.
- Geometry: 710 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2907, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `8c3ed15f13b45f11634dd65c3eab785ea8c103399a75dae1aa6c94ac496ce919`.
- idle: `art/units/unique/mali/skirmisher/maliskirmisher_md_idle.kf`.
- run: `art/units/unique/mali/skirmisher/maliskirmisher_md_run.kf`.
- fortify: `art/units/unique/mali/skirmisher/maliskirmisher_md_fortifya.kf`.
- attack: `art/units/unique/mali/skirmisher/maliskirmisher_md_strikea.kf`.
- hurt: `art/units/unique/mali/skirmisher/maliskirmisher_md_hurta.kf`.
- die: `art/units/unique/mali/skirmisher/maliskirmisher_md_diea.kf`.

### military:man_at_arms

- Model: `src/Mods/Military/art/models/man_at_arms.glb` (652,864 bytes).
- Art: `ART_DEF_UNIT_MAN_AT_ARMS`. Source scale: 0.42.
- NIF: `art/units/man at arms/european/mmaceman_light.nif`.
- KFM: `art/units/horse archer/greek/companion_cavalry.kfm`.
- Geometry: 1145 triangles, 3 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2235, feet Y=0.
- Clips: idle (116 channels), run (116 channels), fortify (116 channels), attack (116 channels), hurt (116 channels), die (116 channels).
- SHA-256: `455adaa25a20c0cc4208474bc459aba9672c7724db4883ebfa58c76d0b85d706`.
- idle: `art/units/horse archer/greek/companion_cavalry_md_idle.kf`.
- run: `art/units/horse archer/greek/companion_cavalry_md_run.kf`.
- fortify: `art/units/horse archer/greek/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/horse archer/greek/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/horse archer/greek/companion_cavalry_md_hurta.kf`.
- die: `art/units/horse archer/greek/companion_cavalry_md_diea.kf`.

### military:manowar

- Model: `src/Mods/Military/art/models/manowar.glb` (288,568 bytes).
- Art: `ART_DEF_UNIT_MANOWAR`. Source scale: 0.20.
- NIF: `art/units/man o war/frigate.nif`.
- KFM: `art/units/frigate/frigate.kfm`.
- Geometry: 1135 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2956, feet Y=0.
- Clips: idle (41 channels), run (41 channels), fortify (41 channels), attack (41 channels), hurt (41 channels), die (41 channels).
- SHA-256: `b03b7d9df96b144d9c7c1734e236b9eea42472d6a03597cace319dba3357d6bc`.
- idle: `art/units/frigate/frigate_md_idle.kf`.
- run: `art/units/frigate/frigate_md_run.kf`.
- fortify: `art/units/frigate/frigate_md_fortifya.kf`.
- attack: `art/units/frigate/frigate_md_strikea.kf`.
- hurt: `art/units/frigate/frigate_md_hurta.kf`.
- die: `art/units/frigate/frigate_md_diea.kf`.

### military:marine

- Model: `src/Mods/Military/art/models/marine.glb` (315,796 bytes).
- Art: `ART_DEF_UNIT_MARINE`. Source scale: 0.44.
- NIF: `art/units/marine/marines.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 728 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.4303, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `e433e72889bdbd5eb5edcc6cfd5846dcd6e9da17e6890e919bb164cd3d58261c`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:maya_holkan

- Model: `src/Mods/Military/art/models/maya_holkan.glb` (368,256 bytes).
- Art: `ART_DEF_UNIT_MAYA_HOLKAN`. Source scale: 0.44.
- NIF: `art/units/unique_maya_holkan/unique_mayan_holkan.nif`.
- KFM: `art/units/unique_maya_holkan/unique_mayan_holkan.kfm`.
- Geometry: 588 triangles, 2 skins, 25 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4025, feet Y=0.
- Clips: idle (62 channels), run (62 channels), fortify (62 channels), attack (62 channels), hurt (62 channels), die (62 channels).
- SHA-256: `380fcc895f714c6eb47b6c34ef711f580075671a333a11f3c0575e2cc6d640f6`.
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
- SHA-256: `b10231ff6f6410c80cd45fa5c52dfdab4e574e632d265e26ba3cfe3e12c72796`.
- idle: `art/units/mechanizedinfantry/mechanizedinfantry_md_idle.kf`.
- run: `art/units/mechanizedinfantry/mechanizedinfantry_md_run.kf`.
- fortify: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedfortify.kf`.
- attack: `art/units/mechanizedinfantry/mechanizedinfantry_md_rangedstrike.kf`.
- hurt: `art/units/mechanizedinfantry/mechanizedinfantry_md_hurta.kf`.

### military:med_caravan

- Model: `src/Mods/Military/art/models/med_caravan.glb` (648,328 bytes).
- Art: `ART_DEF_UNIT_MED_CARAVAN`. Source scale: 0.34.
- NIF: `art/units/charlemagne_supplytrain/supplytrain.nif`.
- KFM: `art/units/charlemagne_supplytrain/supplytrain.kfm`.
- Geometry: 2396 triangles, 4 skins, 81 joint bindings, 4 textures.
- Bounds: height 0.4196, radius 0.4800, feet Y=0.
- Clips: idle (191 channels), run (191 channels).
- SHA-256: `98984c6e4016231437bbef288df2a6223a83a49bf48bed9907d983fb00f794ab`.
- idle: `art/units/charlemagne_supplytrain/supplytrain_md_idle.kf`.
- run: `art/units/charlemagne_supplytrain/supplytrain_md_run.kf`.

### military:merchant

- Model: `src/Mods/Military/art/models/merchant.glb` (423,404 bytes).
- Art: `ART_DEF_UNIT_MERCHANT`. Source scale: 0.46.
- NIF: `art/units/greatpeople/merchant/greatmerchantancient.nif`.
- KFM: `art/units/greatpeople/merchant/greatmerchantancient.kfm`.
- Geometry: 1410 triangles, 3 skins, 42 joint bindings, 3 textures.
- Bounds: height 0.5248, radius 0.4800, feet Y=0.
- Clips: idle (100 channels), run (100 channels), fortify (100 channels).
- SHA-256: `2b7584a1e0fbb9c53bad61e1e92b7c88692fdd26e0498dbc9d21ce3610d4bcd6`.
- idle: `art/units/greatpeople/merchant/greatmerchantancient_md_idle.kf`.
- run: `art/units/greatpeople/merchant/greatmerchantancient_md_run.kf`.
- fortify: `art/units/greatpeople/merchant/greatmerchantancient_md_fortify.kf`.
- Note: Non-triangle effect geometry omitted: GoldThrow_front
- Note: Non-triangle effect geometry omitted: GoldDig_rightside
- Note: Non-triangle effect geometry omitted: GoldDig_leftside

### military:minigun

- Model: `src/Mods/Military/art/models/minigun.glb` (318,920 bytes).
- Art: `ART_DEF_UNIT_MINIGUN`. Source scale: 0.44.
- NIF: `art/units/minigun/european/fremg.nif`.
- KFM: `art/units/machinegun/machinegunner.kfm`.
- Geometry: 1012 triangles, 3 skins, 23 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.2660, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels), hurt (61 channels).
- SHA-256: `a588963b47a650c29a8a58a7f04b811d15c1cd52af165e845e771cdb611e6ec2`.
- idle: `art/units/machinegun/machinegunner_md_idle.kf`.
- run: `art/units/machinegun/machinegunner_md_run.kf`.
- fortify: `art/units/machinegun/machinegunner_md_rangedfortify.kf`.
- attack: `art/units/machinegun/machinegunner_md_rangedstrike.kf`.
- hurt: `art/units/machinegun/machinegunner_md_hurta.kf`.

### military:missile_cruiser

- Model: `src/Mods/Military/art/models/missile_cruiser.glb` (262,344 bytes).
- Art: `ART_DEF_UNIT_MISSILE_CRUISER`. Source scale: 0.6.
- NIF: `art/units/missile_cruiser/missile_cruiser.nif`.
- KFM: `art/units/missile_cruiser/missile_cruiser.kfm`.
- Geometry: 526 triangles, 1 skins, 5 joint bindings, 1 textures.
- Bounds: height 0.3535, radius 0.4800, feet Y=0.
- Clips: idle (42 channels), run (42 channels), fortify (42 channels), attack (42 channels), hurt (42 channels), die (42 channels).
- SHA-256: `afec4e30d7084f7b5c0413cee9c45b3e83f554357685a252fb66f89dc4b0e2c0`.
- idle: `art/units/missile_cruiser/missile_cruiser_md_idle.kf`.
- run: `art/units/missile_cruiser/missile_cruiser_md_run.kf`.
- fortify: `art/units/missile_cruiser/missile_cruiser_md_fortifya.kf`.
- attack: `art/units/missile_cruiser/missile_cruiser_md_strikea.kf`.
- hurt: `art/units/missile_cruiser/missile_cruiser_md_hurta.kf`.
- die: `art/units/missile_cruiser/missile_cruiser_md_diea.kf`.

### military:mobile_artillery

- Model: `src/Mods/Military/art/models/mobile_artillery.glb` (217,436 bytes).
- Art: `ART_DEF_UNIT_MOBILE_ARTILLERY`. Source scale: 0.57.
- NIF: `art/units/mobileartillery/mobileartillery.nif`.
- KFM: `art/units/mobileartillery/mobileartillery.kfm`.
- Geometry: 708 triangles, 1 skins, 7 joint bindings, 1 textures.
- Bounds: height 0.3153, radius 0.4800, feet Y=0.
- Clips: idle (19 channels), run (19 channels), fortify (19 channels), attack (19 channels), hurt (19 channels).
- SHA-256: `ea07d8f55f26fb475996d89f13a6003faa597af196ba7c22c419b81aea8f4314`.
- idle: `art/units/mobileartillery/mobileartillery_md_idle.kf`.
- run: `art/units/mobileartillery/mobileartillery_md_run.kf`.
- fortify: `art/units/mobileartillery/mobileartillery_md_rangedfortify.kf`.
- attack: `art/units/mobileartillery/mobileartillery_md_rangedstrike.kf`.
- hurt: `art/units/mobileartillery/mobileartillery_md_hurta.kf`.

### military:mobile_sam

- Model: `src/Mods/Military/art/models/mobile_sam.glb` (348,248 bytes).
- Art: `ART_DEF_UNIT_MOBILE_SAM`. Source scale: 0.57.
- NIF: `art/units/mobilesam/mobilesam.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 802 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.3824, radius 0.4800, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels).
- SHA-256: `c080bd4089338f24ce7bef1e4f5d13cff04510883808c6541c5f80baf3acc297`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:modern_armor

- Model: `src/Mods/Military/art/models/modern_armor.glb` (238,836 bytes).
- Art: `ART_DEF_UNIT_MODERN_ARMOR`. Source scale: 0.57.
- NIF: `art/units/modernarmor/modernarmor.nif`.
- KFM: `art/units/modernarmor/modernarmor.kfm`.
- Geometry: 546 triangles, 1 skins, 20 joint bindings, 1 textures.
- Bounds: height 0.2434, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels).
- SHA-256: `5b3438a3d48ec241dcc4dc2cdf0bb1c08a07bd2a172a7c6958532d0877335a37`.
- idle: `art/units/modernarmor/modernarmor_md_idle.kf`.
- run: `art/units/modernarmor/modernarmor_md_run.kf`.
- fortify: `art/units/modernarmor/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/modernarmor/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/modernarmor/modernarmor_md_hurta.kf`.

### military:modern_grenadier

- Model: `src/Mods/Military/art/models/modern_grenadier.glb` (634,284 bytes).
- Art: `ART_DEF_UNIT_MODERN_GRENADIER`. Source scale: 0.44.
- NIF: `art/units/modern grenadier/grenadier.nif`.
- KFM: `art/units/grenadier/grenadier.kfm`.
- Geometry: 848 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.5407, radius 0.4800, feet Y=0.
- Clips: idle (102 channels), run (102 channels), fortify (102 channels), attack (102 channels), hurt (102 channels), die (102 channels).
- SHA-256: `557fd32f5044ce26297a33ffa6d838d979bd124eae25ffac8fca221cc991e992`.
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

- Model: `src/Mods/Military/art/models/modern_infantry.glb` (353,032 bytes).
- Art: `ART_DEF_UNIT_MODERNINFANTRY`. Source scale: 0.44.
- NIF: `art/units/modern infantry/european/moderninfantry.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 783 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.4073, radius 0.4800, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `b841c49acc2a19045721ba7b0d079246424a467c2ba17189c3268c9d4d0d0c0d`.
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
- Bounds: height 0.6000, radius 0.3697, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `89929d13f29a8d69736921a09c61343f6c1eb3d1374c648382e766fd86a13a61`.
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
- Bounds: height 0.6000, radius 0.1922, feet Y=0.
- Clips: idle (132 channels), run (132 channels), fortify (132 channels), attack (132 channels), die (132 channels).
- SHA-256: `cc3080d81d178881c9bd0555a8579f7b42fe2a5e1b1d83d766a571e9a8c019ad`.
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

- Model: `src/Mods/Military/art/models/modern_workboat.glb` (261,584 bytes).
- Art: `ART_DEF_UNIT_WORKBOAT_MODERN`. Source scale: 0.28.
- NIF: `art/units/workboat_modern/workboat_modern.nif`.
- KFM: `art/units/workboat_modern/workboat_modern.kfm`.
- Geometry: 489 triangles, 3 skins, 7 joint bindings, 4 textures.
- Bounds: height 0.5181, radius 0.4800, feet Y=0.
- Clips: idle (13 channels), run (13 channels), fortify (13 channels), die (13 channels).
- SHA-256: `7d65243838c73333cc670b64b823eb5e3095ccad6d7d29090bf15205d6d750c7`.
- idle: `art/units/workboat_modern/workboat_modern_md_idle.kf`.
- run: `art/units/workboat_modern/workboat_modern_md_run.kf`.
- fortify: `art/units/workboat_modern/workboat_modern_md_fortifya.kf`.
- die: `art/units/workboat_modern/workboat_modern_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:mongol_keshik

- Model: `src/Mods/Military/art/models/mongol_keshik.glb` (687,084 bytes).
- Art: `ART_DEF_UNIT_MONGOL_KESHIK`. Source scale: 0.42.
- NIF: `art/units/unique/mongol/keshik/mongelkeshik.nif`.
- KFM: `art/units/unique/mongol/keshik/mongelkeshik.kfm`.
- Geometry: 1148 triangles, 3 skins, 37 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3015, feet Y=0.
- Clips: idle (119 channels), run (119 channels), fortify (119 channels), attack (119 channels), hurt (119 channels), die (119 channels).
- SHA-256: `24b66a9834007b058be4ac4841658e23167a8c1d931e7ab09ca218af07b6dec2`.
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
- Bounds: height 0.6000, radius 0.3822, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels).
- SHA-256: `123a10481024546bc5549fb5712a6644b756236cf4df44c9628d2c940c565d0b`.
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
- SHA-256: `fb2f755433629905ce4a37f186dc860db999e2bc9e58ca1158979dcdb62ff813`.
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

- Model: `src/Mods/Military/art/models/mounted_infantry.glb` (666,452 bytes).
- Art: `ART_DEF_UNIT_MOUNTED_INFANTRY`. Source scale: 0.42.
- NIF: `art/units/mounted infantry/european/mswordsman_light.nif`.
- KFM: `art/units/horse archer/greek/companion_cavalry.kfm`.
- Geometry: 1114 triangles, 3 skins, 38 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2310, feet Y=0.
- Clips: idle (116 channels), run (116 channels), fortify (116 channels), attack (116 channels), hurt (116 channels), die (116 channels).
- SHA-256: `a5fc9291c8dbb8bc602eac9deea74dd8b8ff44daff10cf53b5b6a6513f29fa23`.
- idle: `art/units/horse archer/greek/companion_cavalry_md_idle.kf`.
- run: `art/units/horse archer/greek/companion_cavalry_md_run.kf`.
- fortify: `art/units/horse archer/greek/companion_cavalry_md_fortifya.kf`.
- attack: `art/units/horse archer/greek/companion_cavalry_md_strikea.kf`.
- hurt: `art/units/horse archer/greek/companion_cavalry_md_hurta.kf`.
- die: `art/units/horse archer/greek/companion_cavalry_md_diea.kf`.

### military:mounted_rifleman

- Model: `src/Mods/Military/art/models/mounted_rifleman.glb` (574,144 bytes).
- Art: `ART_DEF_UNIT_MOUNTED_RIFLEMAN`. Source scale: 0.42.
- NIF: `art/units/mounted rifleman/european/cavalry_fx.nif`.
- KFM: `art/units/cavalry/cavalry.kfm`.
- Geometry: 1156 triangles, 4 skins, 38 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2177, feet Y=0.
- Clips: idle (92 channels), run (92 channels), fortify (92 channels), attack (92 channels), hurt (92 channels), die (92 channels).
- SHA-256: `2f464481e4fb83b7ce78bc2606fce90b43a8f0f35456e101cb5ec3ee178ce64d`.
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
- SHA-256: `ceb0b44c6d6589709d045b79669923f301c56d77ed3f79eded7b430d7a90f771`.
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
- Bounds: height 0.6000, radius 0.1715, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `6bd387607f2a6c3df70b1090296b030cf8f3f19c0548be280447e7f87647414d`.
- idle: `art/units/unique/england/redcoat/redcoat_md_idle.kf`.
- run: `art/units/unique/england/redcoat/redcoat_md_run.kf`.
- fortify: `art/units/unique/england/redcoat/redcoat_md_rangedfortify.kf`.
- attack: `art/units/unique/england/redcoat/redcoat_md_rangedstrike.kf`.
- hurt: `art/units/unique/england/redcoat/redcoat_md_hurta.kf`.
- die: `art/units/unique/england/redcoat/redcoat_md_rangeddiea.kf`.

### military:nagualism_missionary

- Model: `src/Mods/Military/art/models/nagualism_missionary.glb` (410,156 bytes).
- Art: `ART_DEF_UNIT_TOLTEC_MISSIONARY`. Source scale: 0.46.
- NIF: `art/units/missionary/tlenamacac/greatprophet.nif`.
- KFM: `art/units/missionary/tlenamacac/greatprophet.kfm`.
- Geometry: 1540 triangles, 2 skins, 34 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3106, feet Y=0.
- Clips: idle (91 channels), run (91 channels), fortify (91 channels).
- SHA-256: `6a7311b7f3344f103f23005a9703262267417ff785c8c999de9615937dd799eb`.
- idle: `art/units/missionary/tlenamacac/greatprophet_md_idle.kf`.
- run: `art/units/missionary/tlenamacac/greatprophet_md_run.kf`.
- fortify: `art/units/missionary/tlenamacac/greatprophet_md_fortifya.kf`.
- Note: Missing texture: \\shamstaff.dds
- Note: Missing texture: \\skull.dds

### military:nanite_cloud

- Model: `src/Mods/Military/art/models/nanite_cloud.glb` (586,188 bytes).
- Art: `ART_DEF_UNIT_ENTITY`. Source scale: 0.6.
- NIF: `art/units/afterworld_boss/afterworld_boss.nif`.
- KFM: `art/units/afterworld_boss/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.2457, radius 0.4800, feet Y=0.
- Clips: idle (37 channels), attack (37 channels), die (37 channels).
- SHA-256: `83b94ba3319718bbe52c9157316aca4b4f713c90326c775047a9ce4d0de44f96`.
- idle: `art/units/afterworld_boss/afterworld_boss_md_idle.kf`.
- attack: `art/units/afterworld_boss/afterworld_boss_md_strikea.kf`.
- die: `art/units/afterworld_boss/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:nanite_spy

- Model: `src/Mods/Military/art/models/nanite_spy.glb` (568,428 bytes).
- Art: `ART_DEF_UNIT_NANITE_SPY`. Source scale: 0.1.
- NIF: `art/units/nanite spy/afterworld_boss.nif`.
- KFM: `art/units/nanite spy/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.2457, radius 0.4800, feet Y=0.
- Clips: idle (37 channels), attack (37 channels), die (37 channels).
- SHA-256: `0623687f15a2ebb27d96308c7a004345436d9224730c5823d0a29d5cdeb5bf4d`.
- idle: `art/units/nanite spy/afterworld_boss_md_idle.kf`.
- attack: `art/units/nanite spy/afterworld_boss_md_strikea.kf`.
- die: `art/units/nanite spy/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:nanite_swarm

- Model: `src/Mods/Military/art/models/nanite_swarm.glb` (569,220 bytes).
- Art: `ART_DEF_UNIT_NANITE_SWARM`. Source scale: 0.5.
- NIF: `art/units/nanite swarm/afterworld_boss.nif`.
- KFM: `art/units/nanite swarm/afterworld_boss.kfm`.
- Geometry: 476 triangles, 6 skins, 30 joint bindings, 5 textures.
- Bounds: height 0.2457, radius 0.4800, feet Y=0.
- Clips: idle (37 channels), attack (37 channels), die (37 channels).
- SHA-256: `e62eb357fe32114c0b8546deb25c759db8edb69c36af0e0c751437d86a4e3dfd`.
- idle: `art/units/nanite swarm/afterworld_boss_md_idle.kf`.
- attack: `art/units/nanite swarm/afterworld_boss_md_strikea.kf`.
- die: `art/units/nanite swarm/afterworld_boss_md_diea.kf`.
- Note: Hidden geometry omitted: Editable Mesh
- Note: Non-triangle effect geometry omitted: PC
- Note: Non-triangle effect geometry omitted: PC01

### military:native_america_dog_soldier

- Model: `src/Mods/Military/art/models/native_america_dog_soldier.glb` (352,352 bytes).
- Art: `ART_DEF_UNIT_NATIVE_AMERICA_DOG_SOLDIER`. Source scale: 0.44.
- NIF: `art/units/unique_nativeamerica_dogsoldier/dogsoldier.nif`.
- KFM: `art/units/unique_nativeamerica_dogsoldier/dogsoldier.kfm`.
- Geometry: 1014 triangles, 4 skins, 23 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2661, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels), hurt (55 channels), die (55 channels).
- SHA-256: `e8b0112b1c8ab2285b3c6d008ee2d48afc4ad703d321e8b4763ebc1e83524e08`.
- idle: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_idle.kf`.
- run: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_run.kf`.
- fortify: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_fortifya.kf`.
- attack: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_strikea.kf`.
- hurt: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_hurta.kf`.
- die: `art/units/unique_nativeamerica_dogsoldier/dogsoldier_md_diea.kf`.

### military:netherlands_oostindievaarder

- Model: `src/Mods/Military/art/models/netherlands_oostindievaarder.glb` (263,152 bytes).
- Art: `ART_DEF_UNIT_NETHERLANDS_OOSTINDIEVAARDER`. Source scale: 0.2.
- NIF: `art/units/dutch_east_india_man/dutch_east_india_man.nif`.
- KFM: `art/units/dutch_east_india_man/dutch_east_india_man.kfm`.
- Geometry: 844 triangles, 3 skins, 27 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3951, feet Y=0.
- Clips: idle (40 channels), run (40 channels), fortify (40 channels), attack (40 channels), hurt (40 channels), die (40 channels).
- SHA-256: `ce7c439cc5a66251b02f57ffafdcc3cdf6d949c9fdb0ad80bb99d8fffa3e1820`.
- idle: `art/units/dutch_east_india_man/dutch_east_india_man_md_idle.kf`.
- run: `art/units/dutch_east_india_man/dutch_east_india_man_md_run.kf`.
- fortify: `art/units/dutch_east_india_man/dutch_east_india_man_md_fortifya.kf`.
- attack: `art/units/dutch_east_india_man/dutch_east_india_man_md_strikea.kf`.
- hurt: `art/units/dutch_east_india_man/dutch_east_india_man_md_hurta.kf`.
- die: `art/units/dutch_east_india_man/dutch_east_india_man_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:nlos_cannon

- Model: `src/Mods/Military/art/models/nlos_cannon.glb` (345,296 bytes).
- Art: `ART_DEF_UNIT_NLOS_CANNON`. Source scale: 0.57.
- NIF: `art/units/nlos cannon/disari.nif`.
- KFM: `art/units/nlos cannon/mobileartillery.kfm`.
- Geometry: 1306 triangles, 1 skins, 3 joint bindings, 1 textures.
- Bounds: height 0.3817, radius 0.4800, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), attack (15 channels), hurt (15 channels).
- SHA-256: `30e937ed6c0e375e74fd1702d4b6b469695b652dd3993a4ade0bfe67e4c3d2bd`.
- idle: `art/units/nlos cannon/mobileartillery_md_idle.kf`.
- run: `art/units/nlos cannon/mobileartillery_md_run.kf`.
- fortify: `art/units/nlos cannon/mobileartillery_md_rangedfortify.kf`.
- attack: `art/units/nlos cannon/mobileartillery_md_rangedstrike.kf`.
- hurt: `art/units/nlos cannon/mobileartillery_md_hurta.kf`.

### military:nuclear_carrier

- Model: `src/Mods/Military/art/models/nuclear_carrier.glb` (287,304 bytes).
- Art: `ART_DEF_UNIT_NUCLEAR_CARRIER`. Source scale: 1.1.
- NIF: `art/units/carrier/carrier.nif`.
- KFM: `art/units/carrier/carrier.kfm`.
- Geometry: 603 triangles, 2 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.2029, radius 0.4800, feet Y=0.
- Clips: idle (36 channels), run (36 channels), fortify (36 channels), attack (36 channels), hurt (36 channels), die (36 channels).
- SHA-256: `fbd9c452a0e93cd2aaf9e524e46b191439c94037593bc3bb860ada1aac5b4cfa`.
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
- SHA-256: `a8627366b1dafa6a7b5cc1af2d0c81e0aaf19a4984dd8a2ff06e8f1d8c1154a0`.
- idle: `art/units/submarine/submarine_md_idle.kf`.
- run: `art/units/submarine/submarine_md_run.kf`.
- fortify: `art/units/submarine/submarine_md_fortifya.kf`.
- attack: `art/units/submarine/submarine_md_strikea.kf`.
- hurt: `art/units/submarine/submarine_md_hurta.kf`.
- die: `art/units/submarine/submarine_md_diea.kf`.

### military:orbital_bomber

- Model: `src/Mods/Military/art/models/orbital_bomber.glb` (127,500 bytes).
- Art: `ART_DEF_UNIT_ORBITAL_BOMBER`. Source scale: 0.5.
- NIF: `art/units/thunderbolt/jf_smart.nif`.
- KFM: `none`.
- Geometry: 676 triangles, 1 skins, 1 joint bindings, 1 textures.
- Bounds: height 0.1202, radius 0.4800, feet Y=0.
- Clips: none.
- SHA-256: `04d2976062f3630fb0f4fcf00f6847a3d3f2b9da328b2a50cd66c27ec829e9c7`.

### military:orbital_fighter

- Model: `src/Mods/Military/art/models/orbital_fighter.glb` (248,200 bytes).
- Art: `ART_DEF_UNIT_ORBITAL_FIGHTER`. Source scale: 0.45.
- NIF: `art/units/hammerheadrefar/jetfighter.nif`.
- KFM: `art/units/hammerheadrefar/jetfighter.kfm`.
- Geometry: 1001 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.1069, radius 0.4800, feet Y=0.
- Clips: idle (23 channels), run (23 channels), hurt (23 channels).
- SHA-256: `713e63e9955595902b8aed827ff264efbf9906de552433fd4c810a77a314b907`.
- idle: `art/units/hammerheadrefar/jetfighter_md_idle.kf`.
- run: `art/units/hammerheadrefar/jetfighter_md_run.kf`.
- hurt: `art/units/hammerheadrefar/jetfighter_md_hurt.kf`.

### military:ottoman_janissary

- Model: `src/Mods/Military/art/models/ottoman_janissary.glb` (306,580 bytes).
- Art: `ART_DEF_UNIT_OTTOMAN_JANISSARY`. Source scale: 0.44.
- NIF: `art/units/janissary/janissary.nif`.
- KFM: `art/units/janissary/janissary.kfm`.
- Geometry: 696 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2560, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `55cb74aa70da44ff833dc7808082a9e9f500a390ca4fbf6ef649c6fdf722f95a`.
- idle: `art/units/janissary/janissary_md_idle.kf`.
- run: `art/units/janissary/janissary_md_run.kf`.
- fortify: `art/units/janissary/janissary_md_rangedfortify.kf`.
- attack: `art/units/janissary/janissary_md_rangedstrike.kf`.
- hurt: `art/units/janissary/janissary_md_hurta.kf`.
- die: `art/units/janissary/janissary_md_rangeddiea.kf`.

### military:panther

- Model: `src/Mods/Military/art/models/panther.glb` (269,812 bytes).
- Art: `ART_DEF_UNIT_PANTHER`. Source scale: 0.5.
- NIF: `art/units/panther/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `d86f3bb8f199c763adbc875e2cbc900dd002da61a962d131e29c576a0ad8ed51`.
- idle: `art/units/panther/panther_md_idle.kf`.
- run: `art/units/panther/panther_md_run.kf`.
- fortify: `art/units/panther/panther_md_fortifya.kf`.
- attack: `art/units/panther/panther_md_strikea.kf`.
- hurt: `art/units/panther/panther_md_hurta.kf`.
- die: `art/units/panther/panther_md_diea.kf`.

### military:paratrooper

- Model: `src/Mods/Military/art/models/paratrooper.glb` (481,916 bytes).
- Art: `ART_DEF_UNIT_PARATROOPER`. Source scale: 0.44.
- NIF: `art/units/paratrooper/paratrooper.nif`.
- KFM: `art/units/paratrooper/paratrooper.kfm`.
- Geometry: 882 triangles, 2 skins, 26 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.1996, feet Y=0.
- Clips: idle (90 channels), run (90 channels), fortify (90 channels), attack (90 channels), die (90 channels).
- SHA-256: `c22d6cc3a586d196e2f3fb0128a5368323417672f558c9cc79dcd1613d7b06bb`.
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

- Model: `src/Mods/Military/art/models/persia_immortal.glb` (601,360 bytes).
- Art: `ART_DEF_UNIT_PERSIA_IMMORTAL`. Source scale: 0.42.
- NIF: `art/units/unique/persia/immortal/persianimmortal.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 971 triangles, 3 skins, 36 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3912, feet Y=0.
- Clips: idle (107 channels), run (107 channels), fortify (107 channels), attack (107 channels), hurt (107 channels), die (107 channels).
- SHA-256: `6de45e320e07fc0cf366478de19736df883e25d7204cabf34e2d75be2d86af7f`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:picts_cruithni

- Model: `src/Mods/Military/art/models/picts_cruithni.glb` (268,776 bytes).
- Art: `ART_DEF_UNIT_PICTS_CRUITHNI`. Source scale: 0.44.
- NIF: `art/picts/cruithni/swordsman_fx.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 660 triangles, 1 skins, 17 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3263, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `9b40369a477f3e72436f698a940ef22eb90c377957a9beb55420ecdfd4759bec`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:pikeman

- Model: `src/Mods/Military/art/models/pikeman.glb` (318,252 bytes).
- Art: `ART_DEF_UNIT_PIKEMAN`. Source scale: 0.44.
- NIF: `art/units/pikeman/pikeman.nif`.
- KFM: `art/units/pikeman/pikeman.kfm`.
- Geometry: 864 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.4461, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `e65dec8d76134244b16d5396351dcfb3e90e39a7c8c9eee0888dc921a4b98c8e`.
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
- Bounds: height 0.6000, radius 0.2657, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `cb175684bee478d06077b653f52cffe43e4302c7516ba0694dbda1950cb6e73e`.
- idle: `art/units/settler/settler_male_md_idle.kf`.
- run: `art/units/settler/settler_male_md_run.kf`.
- fortify: `art/units/settler/settler_male_md_fortify.kf`.
- die: `art/units/settler/settler_male_md_idle_die.kf`.
- Note: First of 7 formation art groups exported

### military:plasma_armor

- Model: `src/Mods/Military/art/models/plasma_armor.glb` (277,628 bytes).
- Art: `ART_DEF_UNIT_HELLHOUND_ARMOR`. Source scale: 0.3.
- NIF: `art/units/plasma armor/hellhound.nif`.
- KFM: `art/units/plasma armor/hellhound.kfm`.
- Geometry: 584 triangles, 4 skins, 4 joint bindings, 3 textures.
- Bounds: height 0.4254, radius 0.4800, feet Y=0.
- Clips: idle (25 channels), run (25 channels), fortify (25 channels), attack (25 channels), hurt (25 channels).
- SHA-256: `9b0b0b0576dbcb7e83dfeddcb2bb485bd9d32c50ddf4a93c82b97b7186d0b231`.
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
- SHA-256: `58c9ac490ee8e51cfaeb8ac521eb21959c8e16bdbb6138d61fc5a8a75c551eb3`.
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
- SHA-256: `5cce13d9e93a06f29907a5be57179c6d9894ad5e77b395abd677fe961cbdf0c2`.
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
- Bounds: height 0.5093, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels), die (50 channels).
- SHA-256: `4ce3237b35a01590f5cc240f7e6a39b82f84fd513e71eb1caa194f6aef2f6911`.
- idle: `art/units/animals/polarbear/bear_md_idle.kf`.
- run: `art/units/animals/polarbear/bear_md_run.kf`.
- fortify: `art/units/animals/polarbear/bear_md_fortifya.kf`.
- attack: `art/units/animals/polarbear/bear_md_strikea.kf`.
- hurt: `art/units/animals/polarbear/bear_md_hurta.kf`.
- die: `art/units/animals/polarbear/bear_md_diea.kf`.

### military:polish_heavy_hussar

- Model: `src/Mods/Military/art/models/polish_heavy_hussar.glb` (1,128,932 bytes).
- Art: `ART_DEF_UNIT_HEAVY_HUSSAR`. Source scale: 0.42.
- NIF: `art/poland/heavy hussar/heavycavalry.nif`.
- KFM: `art/poland/heavy hussar/heavycavalry.kfm`.
- Geometry: 1252 triangles, 2 skins, 35 joint bindings, 6 textures.
- Bounds: height 0.6000, radius 0.4654, feet Y=0.
- Clips: idle (228 channels), run (228 channels), fortify (228 channels), attack (228 channels), hurt (228 channels), die (228 channels).
- SHA-256: `0ff3b193b4670bcac72c5cf6fcb7a61d6c188424cce902b710cb940bb520178a`.
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

- Model: `src/Mods/Military/art/models/portugal_carrack.glb` (224,288 bytes).
- Art: `ART_DEF_UNIT_PORTUGAL_CARRACK`. Source scale: 0.24.
- NIF: `art/units/portugal_carrack/portugal_carrack.nif`.
- KFM: `art/units/portugal_carrack/portugal_carrack.kfm`.
- Geometry: 482 triangles, 3 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3379, feet Y=0.
- Clips: idle (38 channels), run (38 channels), fortify (38 channels), attack (38 channels), hurt (38 channels), die (38 channels).
- SHA-256: `98e01347a0fab8696df86afeb0ced2cb5c71998ef19c72c341787690caafc074`.
- idle: `art/units/portugal_carrack/portugal_carrack_md_idle.kf`.
- run: `art/units/portugal_carrack/portugal_carrack_md_run.kf`.
- fortify: `art/units/portugal_carrack/portugal_carrack_md_fortifya.kf`.
- attack: `art/units/portugal_carrack/portugal_carrack_md_strikea.kf`.
- hurt: `art/units/portugal_carrack/portugal_carrack_md_hurta.kf`.
- die: `art/units/portugal_carrack/portugal_carrack_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:power_armored_infantry

- Model: `src/Mods/Military/art/models/power_armored_infantry.glb` (301,636 bytes).
- Art: `ART_DEF_UNIT_POWER_ARMOR_INFANTRY`. Source scale: 0.75.
- NIF: `art/units/power armor infantry/warhammer.nif`.
- KFM: `art/units/power armor infantry/warhammer.kfm`.
- Geometry: 1568 triangles, 1 skins, 15 joint bindings, 1 textures.
- Bounds: height 0.4848, radius 0.4800, feet Y=0.
- Clips: idle (57 channels), run (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `3ad2dc7f81690fc224490cf1b34bce49c8f53290b89e1feaf1428f78be765cf6`.
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

- Model: `src/Mods/Military/art/models/precision_attack_missile.glb` (101,772 bytes).
- Art: `ART_DEF_UNIT_PRECISION_ATTACK_MISSILE`. Source scale: 0.3.
- NIF: `art/units/precision attack missile/tomahawk.nif`.
- KFM: `art/units/guided_missile/guided_missile.kfm`.
- Geometry: 286 triangles, 1 skins, 4 joint bindings, 1 textures.
- Bounds: height 0.2314, radius 0.4800, feet Y=0.
- Clips: idle (19 channels), run (19 channels), attack (19 channels).
- SHA-256: `4103283bd8facda883d0a0f7e4ce4931b1bd675a71c6fa549de24542ee46fec6`.
- idle: `art/units/guided_missile/guided_missile_md_idle.kf`.
- run: `art/units/guided_missile/guided_missile_md_run.kf`.
- attack: `art/units/guided_missile/guided_missile_md_strikea.kf`.

### military:privateer

- Model: `src/Mods/Military/art/models/privateer.glb` (263,792 bytes).
- Art: `ART_DEF_UNIT_PRIVATEER`. Source scale: 0.2.
- NIF: `art/units/privateer/privateer.nif`.
- KFM: `art/units/privateer/privateer.kfm`.
- Geometry: 923 triangles, 3 skins, 23 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2952, feet Y=0.
- Clips: idle (38 channels), run (38 channels), fortify (38 channels), attack (38 channels), hurt (38 channels), die (38 channels).
- SHA-256: `bfbd5322ea4bad172e521c0fd25bdbbbf8c65dc28c69185f5a4dc4e9963a792d`.
- idle: `art/units/privateer/privateer_md_idle.kf`.
- run: `art/units/privateer/privateer_md_run.kf`.
- fortify: `art/units/privateer/privateer_md_fortifya.kf`.
- attack: `art/units/privateer/privateer_md_strikea.kf`.
- hurt: `art/units/privateer/privateer_md_hurta.kf`.
- die: `art/units/privateer/privateer_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:prophet

- Model: `src/Mods/Military/art/models/prophet.glb` (171,592 bytes).
- Art: `ART_DEF_UNIT_PROPHET`. Source scale: 0.46.
- NIF: `art/units/greatpeople/prophet/greatprophet.nif`.
- KFM: `art/units/greatpeople/prophet/greatprophet.kfm`.
- Geometry: 552 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2845, feet Y=0.
- Clips: idle (43 channels), run (43 channels), fortify (43 channels).
- SHA-256: `9da3ed6348647564a1d16ac41dfbae765c3ebc5d0e0ba0ed9b4d7d9bc593c4a8`.
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

- Model: `src/Mods/Military/art/models/rifleman.glb` (280,212 bytes).
- Art: `ART_DEF_UNIT_RIFLEMAN`. Source scale: 0.44.
- NIF: `art/units/rifleman/rifleman.nif`.
- KFM: `art/units/rifleman/rifleman.kfm`.
- Geometry: 618 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.2574, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `4c100348f6b52d2d930f5ea315b593193b0d08239eb088584b8b7908dcc13040`.
- idle: `art/units/rifleman/rifleman_md_idle.kf`.
- run: `art/units/rifleman/rifleman_md_run.kf`.
- fortify: `art/units/rifleman/rifleman_md_rangedfortify.kf`.
- attack: `art/units/rifleman/rifleman_md_rangedstrike.kf`.
- hurt: `art/units/rifleman/rifleman_md_hurta.kf`.
- die: `art/units/rifleman/rifleman_md_rangeddiea.kf`.

### military:rocket_artillery

- Model: `src/Mods/Military/art/models/rocket_artillery.glb` (248,256 bytes).
- Art: `ART_DEF_UNIT_ROCKET_ARTILLERY`. Source scale: 0.7.
- NIF: `art/units/rocket artillery/m270.nif`.
- KFM: `art/units/mobilesam/mobilesam.kfm`.
- Geometry: 400 triangles, 1 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.3840, radius 0.4800, feet Y=0.
- Clips: idle (48 channels), run (48 channels), fortify (48 channels), attack (48 channels), hurt (48 channels).
- SHA-256: `0fcdaaec40a0c08296753c4f87a3b3ac33f6ec1276fe4936ff26fec22d9637d9`.
- idle: `art/units/mobilesam/mobilesam_md_idle.kf`.
- run: `art/units/mobilesam/mobilesam_md_run.kf`.
- fortify: `art/units/mobilesam/mobilesam_md_rangedfortify.kf`.
- attack: `art/units/mobilesam/mobilesam_md_rangedstrike.kf`.
- hurt: `art/units/mobilesam/mobilesam_md_hurta.kf`.

### military:rome_praetorian

- Model: `src/Mods/Military/art/models/rome_praetorian.glb` (280,460 bytes).
- Art: `ART_DEF_UNIT_ROME_PRAETORIAN`. Source scale: 0.44.
- NIF: `art/units/unique/roman/praetorian/praetorian.nif`.
- KFM: `art/units/unique/roman/praetorian/praetorian.kfm`.
- Geometry: 810 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3344, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `38b99a465ed01dc44c5f713a58c9549483a135286583ff44101af56eb34087de`.
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
- SHA-256: `75a0d4ac4db2210aa88d97ec3e8ec060cb548da3b43253079e1a7bc971112e4b`.
- idle: `art/units/rover/rover_md_idle.kf`.
- run: `art/units/rover/rover_md_run.kf`.
- fortify: `art/units/rover/rover_md_rangedfortify.kf`.
- attack: `art/units/rover/rover_md_rangedstrike.kf`.
- hurt: `art/units/rover/rover_md_rangedhurta.kf`.

### military:russia_cossack

- Model: `src/Mods/Military/art/models/russia_cossack.glb` (586,708 bytes).
- Art: `ART_DEF_UNIT_RUSSIA_COSSACK`. Source scale: 0.42.
- NIF: `art/units/unique/russia/cossack/russiancossack.nif`.
- KFM: `art/units/unique/russia/cossack/russiancossack.kfm`.
- Geometry: 1076 triangles, 3 skins, 40 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.3094, feet Y=0.
- Clips: idle (95 channels), run (95 channels), fortify (95 channels), attack (95 channels), hurt (95 channels), die (95 channels).
- SHA-256: `ca2164b7d64ee13fab75dfd28c26a0b84148b39b3786c0409afb28643d13963e`.
- idle: `art/units/unique/russia/cossack/russiancossack_md_idle.kf`.
- run: `art/units/unique/russia/cossack/russiancossack_md_run.kf`.
- fortify: `art/units/unique/russia/cossack/russiancossack_md_fortifya.kf`.
- attack: `art/units/unique/russia/cossack/russiancossack_md_strikea.kf`.
- hurt: `art/units/unique/russia/cossack/russiancossack_md_hurta.kf`.
- die: `art/units/unique/russia/cossack/russiancossack_md_diea.kf`.

### military:sam_infantry

- Model: `src/Mods/Military/art/models/sam_infantry.glb` (378,460 bytes).
- Art: `ART_DEF_UNIT_SAM_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/saminfantry/saminfantry.nif`.
- KFM: `art/units/saminfantry/saminfantry.kfm`.
- Geometry: 754 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3926, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), attack (51 channels), hurt (51 channels), die (51 channels).
- SHA-256: `a7eacbcdb0ffd74647d4ada2c5adf474482fb633fb21bd9d76a17876083b8d68`.
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
- SHA-256: `e2f3a2bea31d94bff5529dceaee17ac14f78a129a9c3231e939a03d3c37f3bdb`.
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
- SHA-256: `6695947fa2725f6ee8dbf0dc0b75de3be3067e1bf0d9df5039da68a5f8270ae1`.
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
- SHA-256: `8859abe289fbf1aeda15ab3a0da8b2d42e7a11cb4d1326b0acc0129c77df7b94`.
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

- Model: `src/Mods/Military/art/models/seaplane.glb` (205,260 bytes).
- Art: `ART_DEF_UNIT_SEAPLANE`. Source scale: 0.4.
- NIF: `art/units/seaplane/generic/e16a1.nif`.
- KFM: `art/units/fighter/fighter.kfm`.
- Geometry: 1129 triangles, 1 skins, 1 joint bindings, 2 textures.
- Bounds: height 0.3385, radius 0.4800, feet Y=0.
- Clips: idle (9 channels), run (9 channels), hurt (9 channels), die (9 channels).
- SHA-256: `cb93ff7a361306aded5432bc564a29eb5e391311672ace22d5497e85aa46170b`.
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
- Bounds: height 0.6000, radius 0.2657, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), die (52 channels).
- SHA-256: `cb175684bee478d06077b653f52cffe43e4302c7516ba0694dbda1950cb6e73e`.
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
- SHA-256: `e0905e29aa2b3d2b47e75f2547899887e26f48807c40362f576dcafc1651d92b`.
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
- SHA-256: `939423c860bd44f52f6fa04cd53b1a2249639046030f6ce196ea5624f1839b66`.
- idle: `art/units/kannushi/confucian_missionary_md_idle.kf`.
- run: `art/units/kannushi/confucian_missionary_md_run.kf`.
- fortify: `art/units/kannushi/confucian_missionary_md_fortifya.kf`.
- die: `art/units/kannushi/confucian_missionary_md_idle_die.kf`.

### military:ship_of_the_line

- Model: `src/Mods/Military/art/models/ship_of_the_line.glb` (178,092 bytes).
- Art: `ART_DEF_UNIT_SHIP_OF_THE_LINE`. Source scale: 0.22.
- NIF: `art/units/ship_0f_the_line/ship_0f_the_line.nif`.
- KFM: `art/units/ship_0f_the_line/ship_of_the_line.kfm`.
- Geometry: 1257 triangles, 2 skins, 2 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4193, feet Y=0.
- Clips: idle (1 channels), run (1 channels), fortify (1 channels), attack (1 channels), hurt (1 channels), die (1 channels).
- SHA-256: `8d19948e53fb38eaf6a2e7c2ac5bed1353bc2512a419fcb10add1eb8fa211fa3`.
- idle: `art/units/ship_0f_the_line/ship_of_the_line_md_idle.kf`.
- run: `art/units/ship_0f_the_line/ship_of_the_line_md_run.kf`.
- fortify: `art/units/ship_0f_the_line/ship_of_the_line_md_fortifya.kf`.
- attack: `art/units/ship_0f_the_line/ship_of_the_line_md_strikea.kf`.
- hurt: `art/units/ship_0f_the_line/ship_of_the_line_md_hurta.kf`.
- die: `art/units/ship_0f_the_line/ship_of_the_line_md_diea.kf`.
- Note: Missing texture: ship_rope02.dds

### military:siam_elephant

- Model: `src/Mods/Military/art/models/siam_elephant.glb` (1,574,820 bytes).
- Art: `ART_DEF_UNIT_SIAM_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/unique/siamese/battle elephant/warelephant_european_fx.nif`.
- KFM: `art/units/unique/siamese/battle elephant/warelephant_carthage.kfm`.
- Geometry: 2813 triangles, 4 skins, 56 joint bindings, 9 textures.
- Bounds: height 0.6000, radius 0.2374, feet Y=0.
- Clips: idle (234 channels), run (234 channels), fortify (234 channels), attack (234 channels), hurt (234 channels), die (234 channels).
- SHA-256: `3f4954e54760d211560163061a0544031bf4d75793fbcb6fb226412fc72cdee7`.
- idle: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_idle.kf`.
- run: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_run.kf`.
- fortify: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_fortifya.kf`.
- attack: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_strikea.kf`.
- hurt: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_hurta.kf`.
- die: `art/units/unique/siamese/battle elephant/warelephant_carthage_md_diea.kf`.

### military:siberiantiger

- Model: `src/Mods/Military/art/models/siberiantiger.glb` (349,516 bytes).
- Art: `ART_DEF_UNIT_SIBERIANTIGER`. Source scale: 0.67.
- NIF: `art/units/animals/siberiantiger/panther.nif`.
- KFM: `art/units/panther/panther.kfm`.
- Geometry: 480 triangles, 1 skins, 18 joint bindings, 1 textures.
- Bounds: height 0.4720, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `a8c7f8301d393e74dca03b384c9029fdfc5ea6d68ec0fd1cd3d1a6d7120f2056`.
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
- SHA-256: `dda59c92499259042b0592fca6467c86dc65790d99be5364ae200ff8af30ea86`.
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

- Model: `src/Mods/Military/art/models/slinger.glb` (440,360 bytes).
- Art: `ART_DEF_UNIT_SLINGER`. Source scale: 0.44.
- NIF: `art/units/peltast/peltast.nif`.
- KFM: `art/units/peltast/peltast.kfm`.
- Geometry: 792 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2653, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `e762cab0519a2c5b969e4b98d5875cf0ae06b889c9cf91ed417d1c5dbdce3d06`.
- idle: `art/units/peltast/peltast_md_idle.kf`.
- run: `art/units/peltast/peltast_md_run.kf`.
- fortify: `art/units/peltast/peltast_md_ranged_fortify.kf`.
- attack: `art/units/peltast/peltast_md_rangedstrike.kf`.
- hurt: `art/units/peltast/peltast_md_hurta.kf`.
- die: `art/units/peltast/peltast_md_rangeddiea.kf`.

### military:sloop

- Model: `src/Mods/Military/art/models/sloop.glb` (217,772 bytes).
- Art: `ART_DEF_UNIT_CARAVEL_SIMPLERIG`. Source scale: 0.18.
- NIF: `art/units/sloop/caravel.nif`.
- KFM: `art/units/caravel/caravel.kfm`.
- Geometry: 797 triangles, 3 skins, 12 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3753, feet Y=0.
- Clips: idle (20 channels), run (20 channels), fortify (20 channels), attack (20 channels), hurt (20 channels), die (20 channels).
- SHA-256: `fbe38803017900430e608bb240f22f6d1cf6a0ca355c605ca1e55a3c962c767e`.
- idle: `art/units/caravel/caravel_md_idle.kf`.
- run: `art/units/caravel/caravel_md_run.kf`.
- fortify: `art/units/caravel/caravel_md_fortifya.kf`.
- attack: `art/units/caravel/caravel_md_strikea.kf`.
- hurt: `art/units/caravel/caravel_md_hurta.kf`.
- die: `art/units/caravel/caravel_md_diea.kf`.

### military:spanish_conquistador

- Model: `src/Mods/Military/art/models/spanish_conquistador.glb` (603,000 bytes).
- Art: `ART_DEF_UNIT_SPANISH_CONQUISTADOR`. Source scale: 0.42.
- NIF: `art/units/unique/spanish/conquistador/conquistador.nif`.
- KFM: `art/units/unique/spanish/conquistador/conquistador.kfm`.
- Geometry: 1019 triangles, 3 skins, 50 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3192, feet Y=0.
- Clips: idle (106 channels), run (106 channels), fortify (106 channels), attack (106 channels), hurt (106 channels), die (106 channels).
- SHA-256: `7fcaa31ee9175e2a864bb18fff7efa1e858acda2f86f0747a969405ff64ca7d5`.
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
- Bounds: height 0.6000, radius 0.4287, feet Y=0.
- Clips: idle (49 channels), run (49 channels), fortify (49 channels), attack (49 channels), hurt (49 channels), die (49 channels).
- SHA-256: `08f299e0e7e8ec5b8d937c997fa55fc8de38c212d9749adae92670fbb6136d3f`.
- idle: `art/units/unique/greek/phalanx/greekphalanx_md_idle.kf`.
- run: `art/units/unique/greek/phalanx/greekphalanx_md_run.kf`.
- fortify: `art/units/unique/greek/phalanx/greekphalanx_md_fortifya.kf`.
- attack: `art/units/unique/greek/phalanx/greekphalanx_md_strikea.kf`.
- hurt: `art/units/unique/greek/phalanx/greekphalanx_md_hurta.kf`.
- die: `art/units/unique/greek/phalanx/greekphalanx_md_diea.kf`.

### military:special_agent

- Model: `src/Mods/Military/art/models/special_agent.glb` (484,704 bytes).
- Art: `ART_DEF_UNIT_SPECIAL_AGENT`. Source scale: 0.46.
- NIF: `art/units/specialagent/specialagent.nif`.
- KFM: `art/units/great_spy_modern/great_spy_modern.kfm`.
- Geometry: 811 triangles, 1 skins, 15 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.4055, feet Y=0.
- Clips: idle (61 channels), run (61 channels), fortify (61 channels), attack (61 channels).
- SHA-256: `fa65904360006e71b16bfce4442daacf61c1a84d6c8ec61cff2cb4002b12fd0b`.
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
- SHA-256: `4719051fa4227e1f00c76e5187ab67bbaab97df211c318828c0958a70a659723`.
- idle: `art/units/unique/america/navyseal/navyseal_md_idle.kf`.
- run: `art/units/unique/america/navyseal/navyseal_md_run.kf`.
- fortify: `art/units/unique/america/navyseal/navyseal_md_rangedfortify.kf`.
- attack: `art/units/unique/america/navyseal/navyseal_md_rangedstrike.kf`.
- hurt: `art/units/unique/america/navyseal/navyseal_md_hurta.kf`.
- die: `art/units/unique/america/navyseal/navyseal_md_rangeddiea.kf`.

### military:special_infantry

- Model: `src/Mods/Military/art/models/special_infantry.glb` (395,976 bytes).
- Art: `ART_DEF_UNIT_SPACE_INFANTRY`. Source scale: 0.44.
- NIF: `art/units/space infantry/marines.nif`.
- KFM: `art/units/marine/marines.kfm`.
- Geometry: 1190 triangles, 1 skins, 16 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.4440, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `b0c7ec8d554d2570012b40b60a0650c8dde80bc2b1eac633cbef10b1f85bf664`.
- idle: `art/units/marine/marines_md_idle.kf`.
- run: `art/units/marine/marines_md_run.kf`.
- fortify: `art/units/marine/marines_md_rangedfortify.kf`.
- attack: `art/units/marine/marines_md_rangedstrike.kf`.
- hurt: `art/units/marine/marines_md_hurta.kf`.
- die: `art/units/marine/marines_md_rangeddiea.kf`.

### military:spy

- Model: `src/Mods/Military/art/models/spy.glb` (408,152 bytes).
- Art: `ART_DEF_UNIT_ANCIENT_SPY`. Source scale: 0.46.
- NIF: `art/units/ancientspy/ancientspy.nif`.
- KFM: `art/units/ancientspy/ancientspy.kfm`.
- Geometry: 678 triangles, 1 skins, 15 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.4309, feet Y=0.
- Clips: idle (55 channels), run (55 channels), fortify (55 channels), attack (55 channels).
- SHA-256: `5d31f66a50c507c7fcc793abc57fe6a990e0b3a605f0f57df2a82ccfb8d4694b`.
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

- Model: `src/Mods/Military/art/models/stag.glb` (503,744 bytes).
- Art: `ART_DEF_UNIT_STAG`. Source scale: 0.38.
- NIF: `art/units/stag/horsearcher.nif`.
- KFM: `art/units/horsearcher/horsearcher.kfm`.
- Geometry: 392 triangles, 1 skins, 14 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.4636, feet Y=0.
- Clips: idle (106 channels), run (106 channels), fortify (106 channels), attack (106 channels), hurt (106 channels), die (106 channels).
- SHA-256: `117d97c1d615414aff3a2a2c73ad325d3327ef772d11e05b539582de84942682`.
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

- Model: `src/Mods/Military/art/models/stallion.glb` (337,652 bytes).
- Art: `ART_DEF_UNIT_STALLION`. Source scale: 0.3.
- NIF: `art/units/horse/horsedark.nif`.
- KFM: `art/units/unique/persia/immortal/persianimmortal.kfm`.
- Geometry: 432 triangles, 1 skins, 19 joint bindings, 1 textures.
- Bounds: height 0.6000, radius 0.3462, feet Y=0.
- Clips: idle (64 channels), run (64 channels), fortify (64 channels), attack (64 channels), hurt (64 channels), die (64 channels).
- SHA-256: `d4cb0c420aae235f0652537b5e7d8b04a4efe1e893bcc2d84fcece331a5f9c70`.
- idle: `art/units/unique/persia/immortal/persianimmortal_md_idle.kf`.
- run: `art/units/unique/persia/immortal/persianimmortal_md_run.kf`.
- fortify: `art/units/unique/persia/immortal/persianimmortal_md_fortifya.kf`.
- attack: `art/units/unique/persia/immortal/persianimmortal_md_strikea.kf`.
- hurt: `art/units/unique/persia/immortal/persianimmortal_md_hurta.kf`.
- die: `art/units/unique/persia/immortal/persianimmortal_md_diea.kf`.

### military:stealth_armor

- Model: `src/Mods/Military/art/models/stealth_armor.glb` (359,072 bytes).
- Art: `ART_DEF_UNIT_STEALTH_ARMOR`. Source scale: 0.40.
- NIF: `art/units/stealth armor/myrmidon.nif`.
- KFM: `art/units/stealth armor/modernarmor.kfm`.
- Geometry: 476 triangles, 1 skins, 2 joint bindings, 2 textures.
- Bounds: height 0.1060, radius 0.4800, feet Y=0.
- Clips: idle (45 channels), run (45 channels), fortify (45 channels), attack (45 channels), hurt (45 channels).
- SHA-256: `5388dd327471f0910392bbbb9806a40b263e93eeffd300adc0f1b00be4940fb8`.
- idle: `art/units/stealth armor/modernarmor_md_idle.kf`.
- run: `art/units/stealth armor/modernarmor_md_run.kf`.
- fortify: `art/units/stealth armor/modernarmor_md_rangedfortify.kf`.
- attack: `art/units/stealth armor/modernarmor_md_rangedstrike.kf`.
- hurt: `art/units/stealth armor/modernarmor_md_hurta.kf`.

### military:stealth_bomber

- Model: `src/Mods/Military/art/models/stealth_bomber.glb` (162,636 bytes).
- Art: `ART_DEF_UNIT_STEALTH_BOMBER`. Source scale: 0.5.
- NIF: `art/units/stealthbomber/stealthbomber.nif`.
- KFM: `art/units/stealthbomber/stealthbomber.kfm`.
- Geometry: 536 triangles, 2 skins, 6 joint bindings, 1 textures.
- Bounds: height 0.0627, radius 0.4800, feet Y=0.
- Clips: idle (25 channels), run (25 channels), hurt (25 channels), die (25 channels).
- SHA-256: `d296db124b09bc57bf642775cf4ba518af0514e9c8e6322a243d98c60a9b560d`.
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
- SHA-256: `26def9d18d1b036bac7a1daf6c16b8808ca48ab4727b4ac5db0fa18799ea5de4`.
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
- SHA-256: `b3ea7a0d9be447c41183f6ef6ae0c4710f90a96fba52b1f6c7b91321018b703d`.
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
- SHA-256: `045e5a7579e642744ea1035a6b32323b70ffee8e174e828d082760f35fcfbeb4`.
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
- SHA-256: `26def9d18d1b036bac7a1daf6c16b8808ca48ab4727b4ac5db0fa18799ea5de4`.
- idle: `art/units/stealth_destroyer/stealth_destroyer_md_idle.kf`.
- run: `art/units/stealth_destroyer/stealth_destroyer_md_run.kf`.
- fortify: `art/units/stealth_destroyer/stealth_destroyer_md_fortifya.kf`.
- attack: `art/units/stealth_destroyer/stealth_destroyer_md_strikea.kf`.
- hurt: `art/units/stealth_destroyer/stealth_destroyer_md_hurta.kf`.
- die: `art/units/stealth_destroyer/stealth_destroyer_md_diea.kf`.

### military:submarine

- Model: `src/Mods/Military/art/models/submarine.glb` (261,016 bytes).
- Art: `ART_DEF_UNIT_SUBMARINE`. Source scale: 0.55.
- NIF: `art/units/submarine/submarine.nif`.
- KFM: `art/units/submarine/submarine.kfm`.
- Geometry: 424 triangles, 2 skins, 13 joint bindings, 2 textures.
- Bounds: height 0.3012, radius 0.4800, feet Y=0.
- Clips: idle (28 channels), run (28 channels), fortify (28 channels), attack (28 channels), hurt (28 channels), die (28 channels).
- SHA-256: `e4d0cd2ea6a58b5e0987d43945f676b9dcf86bf469dacbc6e428748b2dad1382`.
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
- Bounds: height 0.6000, radius 0.3652, feet Y=0.
- Clips: idle (57 channels), run (57 channels), fortify (57 channels), attack (57 channels), hurt (57 channels), die (57 channels).
- SHA-256: `f612b5df95de64838b30266e3f5cb2db111686ce3ad00c5c9ce8ca5bba74e71a`.
- idle: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_idle.kf`.
- run: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_run.kf`.
- fortify: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_fortifya.kf`.
- attack: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_strikea.kf`.
- hurt: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_hurta.kf`.
- die: `art/units/unique_sumeria_vulture/unique_sumerian_vulture_md_diea.kf`.

### military:swiss_hellebardier

- Model: `src/Mods/Military/art/models/swiss_hellebardier.glb` (310,948 bytes).
- Art: `ART_DEF_UNIT_SWISS_HELLEBARDIER`. Source scale: 0.44.
- NIF: `art/switzerland/swisspikeman/pikeman.nif`.
- KFM: `art/switzerland/swisspikeman/pikeman.kfm`.
- Geometry: 864 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.4461, radius 0.4800, feet Y=0.
- Clips: idle (52 channels), run (52 channels), fortify (52 channels), attack (52 channels), hurt (52 channels), die (52 channels).
- SHA-256: `a17b1db09dff898fe571977aad6e302d233e88c08b238b3a2ed65263c184cb33`.
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
- Bounds: height 0.6000, radius 0.3160, feet Y=0.
- Clips: idle (46 channels), run (46 channels), fortify (46 channels), attack (46 channels), hurt (46 channels), die (46 channels).
- SHA-256: `9d206084b151476c3a6f1c01e5120374858485f3c47af729dc36c37840d236a0`.
- idle: `art/units/charlemagne_lightswordsman/lightswordsman_md_idle.kf`.
- run: `art/units/charlemagne_lightswordsman/lightswordsman_md_run.kf`.
- fortify: `art/units/charlemagne_lightswordsman/lightswordsman_md_fortifya.kf`.
- attack: `art/units/charlemagne_lightswordsman/lightswordsman_md_strikea.kf`.
- hurt: `art/units/charlemagne_lightswordsman/lightswordsman_md_hurta.kf`.
- die: `art/units/charlemagne_lightswordsman/lightswordsman_md_diea.kf`.

### military:t95

- Model: `src/Mods/Military/art/models/t95.glb` (353,508 bytes).
- Art: `ART_DEF_UNIT_T95`. Source scale: 0.4.
- NIF: `art/units/hybrid tank/t-95.nif`.
- KFM: `art/units/hybrid tank/tank.kfm`.
- Geometry: 2434 triangles, 1 skins, 5 joint bindings, 2 textures.
- Bounds: height 0.3313, radius 0.4800, feet Y=0.
- Clips: idle (14 channels), run (14 channels), fortify (14 channels), attack (14 channels), hurt (14 channels).
- SHA-256: `487bd232b03fb21cf5695dac9fbb7a83bddaa7b4b60f43dc45d0323d7b0d4ec5`.
- idle: `art/units/hybrid tank/tank_md_idle.kf`.
- run: `art/units/hybrid tank/tank_md_run.kf`.
- fortify: `art/units/hybrid tank/tank_md_rangedfortify.kf`.
- attack: `art/units/hybrid tank/tank_md_rangedstrike.kf`.
- hurt: `art/units/hybrid tank/tank_md_hurta.kf`.

### military:tactical_nuke

- Model: `src/Mods/Military/art/models/tactical_nuke.glb` (89,268 bytes).
- Art: `ART_DEF_UNIT_TACTICAL_NUKE`. Source scale: 0.5.
- NIF: `art/units/tactical_nuke/tactical_nuke.nif`.
- KFM: `art/units/tactical_nuke/tactical_nuke.kfm`.
- Geometry: 154 triangles, 1 skins, 3 joint bindings, 2 textures.
- Bounds: height 0.5418, radius 0.4800, feet Y=0.
- Clips: idle (18 channels), run (18 channels), attack (18 channels).
- SHA-256: `e83cc3e2b2913d969e5425c4a31438552240161d775ca9897e2e5ea82e8a9889`.
- idle: `art/units/tactical_nuke/tactical_nuke_md_idle.kf`.
- run: `art/units/tactical_nuke/tactical_nuke_md_run.kf`.
- attack: `art/units/tactical_nuke/tactical_nuke_md_strikea.kf`.

### military:tank

- Model: `src/Mods/Military/art/models/tank.glb` (263,600 bytes).
- Art: `ART_DEF_UNIT_TANK`. Source scale: 0.48.
- NIF: `art/units/tank/tank.nif`.
- KFM: `art/units/tank/tank.kfm`.
- Geometry: 369 triangles, 1 skins, 16 joint bindings, 1 textures.
- Bounds: height 0.4479, radius 0.4800, feet Y=0.
- Clips: idle (50 channels), run (50 channels), fortify (50 channels), attack (50 channels), hurt (50 channels).
- SHA-256: `219c8080923aade5784ca409ec4f4306a1f719b20ec79a61e68c74a45b167766`.
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
- Bounds: height 0.6000, radius 0.4594, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `fa70db78f1e2f5b11db6f2348689f60dc5bcf050801588719b262e5b697d4abe`.
- idle: `art/units/missionary/taoist/taoistmissionary_md_idle.kf`.
- run: `art/units/missionary/taoist/taoistmissionary_md_run.kf`.
- fortify: `art/units/missionary/taoist/taoistmissionary_md_fortify.kf`.
- die: `art/units/missionary/taoist/taoistmissionary_md_idle_die.kf`.

### military:trade_caravan

- Model: `src/Mods/Military/art/models/trade_caravan.glb` (825,004 bytes).
- Art: `ART_DEF_UNIT_TRADE_CARAVAN`. Source scale: 0.34.
- NIF: `art/units/caravan/caravan_art.nif`.
- KFM: `art/units/caravan/caravan_base.kfm`.
- Geometry: 2320 triangles, 4 skins, 74 joint bindings, 5 textures.
- Bounds: height 0.5836, radius 0.4800, feet Y=0.
- Clips: idle (239 channels), run (239 channels).
- SHA-256: `50d09cd3d91f4aa5b5830a4161cd49eed1129f4b8d749eac807f2ddee17a08ac`.
- idle: `art/units/caravan/caravan_base_md_idle.kf`.
- run: `art/units/caravan/caravan_base_md_run.kf`.

### military:transport

- Model: `src/Mods/Military/art/models/transport.glb` (304,916 bytes).
- Art: `ART_DEF_UNIT_TRANSPORT`. Source scale: 0.92.
- NIF: `art/units/transport/transport.nif`.
- KFM: `art/units/transport/transport.kfm`.
- Geometry: 490 triangles, 2 skins, 12 joint bindings, 1 textures.
- Bounds: height 0.2713, radius 0.4800, feet Y=0.
- Clips: idle (44 channels), run (44 channels), fortify (44 channels), attack (44 channels), hurt (44 channels), die (44 channels).
- SHA-256: `124c37e773b569274c5ad19cc7861df15de10ea48d504a0a53a25f986b128d5e`.
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
- Bounds: height 0.6000, radius 0.3162, feet Y=0.
- Clips: idle (55 channels), run (55 channels).
- SHA-256: `d96afa7a26b3a028708812ae2a9b7fe6befa17ccbeb0999a3630a879b3941061`.
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
- SHA-256: `e55645080681d1296d75a8a4af3707c9e7cf16aaafe34b3e0c6f682bef929942`.
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
- SHA-256: `be4b4b8443271e2da1ba40ff1892e438c8f140531a42432952a873f21d131296`.
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
- SHA-256: `a2d8fd874734f0e39e70059b380a8432ffe3348e8688cd70b606d356a3d845f6`.
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

- Model: `src/Mods/Military/art/models/unit_unit_crusader.glb` (296,864 bytes).
- Art: `ART_DEF_UNIT_KNIGHTS_TEMPLAR`. Source scale: 0.44.
- NIF: `art/units/scenario/knigts_templar/knightstemplar.nif`.
- KFM: `art/units/scenario/knigts_templar/knightstemplar.kfm`.
- Geometry: 556 triangles, 1 skins, 18 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2889, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `b797b1b996c6b13b2439f55e6e6671a1faf5fa274811a8d54cae503c0513fb8a`.
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

- Model: `src/Mods/Military/art/models/vandal_raider.glb` (287,908 bytes).
- Art: `ART_DEF_UNIT_VANDAL_RAIDER`. Source scale: 0.44.
- NIF: `art/units/raider/swordsman.nif`.
- KFM: `art/units/swordsman/swordsman.kfm`.
- Geometry: 710 triangles, 1 skins, 17 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3410, feet Y=0.
- Clips: idle (54 channels), run (54 channels), fortify (54 channels), attack (54 channels), hurt (54 channels), die (54 channels).
- SHA-256: `a319165dc217d65bf05202a7310f059ec29b14dcc837c5f493ee435c47287f20`.
- idle: `art/units/swordsman/swordsman_md_idle.kf`.
- run: `art/units/swordsman/swordsman_md_run.kf`.
- fortify: `art/units/swordsman/swordsman_md_fortifya.kf`.
- attack: `art/units/swordsman/swordsman_md_strikea.kf`.
- hurt: `art/units/swordsman/swordsman_md_hurta.kf`.
- die: `art/units/swordsman/swordsman_md_diea.kf`.

### military:viking_beserker

- Model: `src/Mods/Military/art/models/viking_beserker.glb` (449,192 bytes).
- Art: `ART_DEF_UNIT_VIKING_BESERKER`. Source scale: 0.44.
- NIF: `art/units/viking_berserker/berserker.nif`.
- KFM: `art/units/viking_berserker/berserker.kfm`.
- Geometry: 860 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3467, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels), attack (53 channels), hurt (53 channels), die (53 channels).
- SHA-256: `2fdfd60df70bf971dd3ee0aa038554e2191a305377100673be60a13681d6a615`.
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
- SHA-256: `f0765462d8ddc30ff4f7b76005af24bc682af4be9084386e8b92a2e2a2ef7bf5`.
- idle: `art/units/walker droid/mech_md_idle.kf`.
- run: `art/units/walker droid/mech_md_run.kf`.
- fortify: `art/units/walker droid/mech_md_rangedfortify.kf`.
- attack: `art/units/walker droid/mech_md_rangedstrike.kf`.
- hurt: `art/units/walker droid/mech_md_rangedhurta.kf`.

### military:war_elephant

- Model: `src/Mods/Military/art/models/war_elephant.glb` (760,304 bytes).
- Art: `ART_DEF_UNIT_WAR_ELEPHANT`. Source scale: 0.32.
- NIF: `art/units/warelephant/warelephant.nif`.
- KFM: `art/units/warelephant/warelephant.kfm`.
- Geometry: 1454 triangles, 3 skins, 47 joint bindings, 4 textures.
- Bounds: height 0.6000, radius 0.3278, feet Y=0.
- Clips: idle (117 channels), run (117 channels), fortify (117 channels), attack (117 channels), hurt (117 channels), die (117 channels).
- SHA-256: `dc571b26b3dd04386a69fc2aa6d4b990d6673097514cc13e50a3c5f84c6254eb`.
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

- Model: `src/Mods/Military/art/models/warrior.glb` (304,112 bytes).
- Art: `ART_DEF_UNIT_WARRIOR`. Source scale: 0.44.
- NIF: `art/units/warrior/warrior.nif`.
- KFM: `art/units/warrior/warrior.kfm`.
- Geometry: 598 triangles, 1 skins, 19 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3409, feet Y=0.
- Clips: idle (47 channels), run (47 channels), fortify (47 channels), attack (47 channels), hurt (47 channels), die (47 channels).
- SHA-256: `7aeec3460185db6191ae157af1c307a8b3023a76b7817c44d6f0400f8296d844`.
- idle: `art/units/warrior/warrior_md_idle.kf`.
- run: `art/units/warrior/warrior_md_run.kf`.
- fortify: `art/units/warrior/warrior_md_fortifya.kf`.
- attack: `art/units/warrior/warrior_md_strikea.kf`.
- hurt: `art/units/warrior/warrior_md_hurta.kf`.
- die: `art/units/warrior/warrior_md_diea.kf`.

### military:welsh_longbowman

- Model: `src/Mods/Military/art/models/welsh_longbowman.glb` (378,904 bytes).
- Art: `ART_DEF_UNIT_WELSH_LONGBOWMAN`. Source scale: 0.44.
- NIF: `art/wales/welsh longbowman/longbowman.nif`.
- KFM: `art/wales/welsh longbowman/longbowman.kfm`.
- Geometry: 652 triangles, 2 skins, 19 joint bindings, 3 textures.
- Bounds: height 0.6000, radius 0.2979, feet Y=0.
- Clips: idle (59 channels), run (59 channels), fortify (59 channels), attack (59 channels), hurt (59 channels), die (59 channels).
- SHA-256: `92393789e953291c37f689b9bc4dd887feb0eb197dc244e852c92686bed1b8ba`.
- idle: `art/wales/welsh longbowman/longbowman_md_idle.kf`.
- run: `art/wales/welsh longbowman/longbowman_md_run.kf`.
- fortify: `art/wales/welsh longbowman/longbowman_md_fortifya.kf`.
- attack: `art/wales/welsh longbowman/longbowman_md_strikea.kf`.
- hurt: `art/wales/welsh longbowman/longbowman_md_hurta.kf`.
- die: `art/wales/welsh longbowman/longbowman_md_diea.kf`.

### military:wgmaster

- Model: `src/Mods/Military/art/models/wgmaster.glb` (230,312 bytes).
- Art: `ART_DEF_UNIT_WGMASTER`. Source scale: 0.46.
- NIF: `art/units/guildmaster/whiteguildmaster_fx.nif`.
- KFM: `art/units/missionary/christian/christianmissionary.kfm`.
- Geometry: 632 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2047, feet Y=0.
- Clips: idle (51 channels), run (51 channels), fortify (51 channels), die (51 channels).
- SHA-256: `2b3cd8da26def786961401c19909d60401b7f7520ff6d327f0ea7967f8741c42`.
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
- SHA-256: `dfb0ac310a774138a9ee62f8a4fc2fd9d1f79b78d820731f6f7d7a68dae937b7`.
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
- Bounds: height 0.5066, radius 0.4800, feet Y=0.
- Clips: idle (60 channels), run (60 channels), fortify (60 channels), attack (60 channels), hurt (60 channels), die (60 channels).
- SHA-256: `330054d6a0012be5d05b0eb977ff677587be37ed14288ee963aae0c058ff02f4`.
- idle: `art/units/wolf/wolf_md_idle.kf`.
- run: `art/units/wolf/wolf_md_run.kf`.
- fortify: `art/units/wolf/wolf_md_fortifya.kf`.
- attack: `art/units/wolf/wolf_md_strikea.kf`.
- hurt: `art/units/wolf/wolf_md_hurta.kf`.
- die: `art/units/wolf/wolf_md_diea.kf`.

### military:workboat

- Model: `src/Mods/Military/art/models/workboat.glb` (209,256 bytes).
- Art: `ART_DEF_UNIT_WORKBOAT`. Source scale: 0.19.
- NIF: `art/units/workboat_ancient/workboat_ancient.nif`.
- KFM: `art/units/workboat_ancient/workboat_ancient.kfm`.
- Geometry: 417 triangles, 3 skins, 10 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.2547, feet Y=0.
- Clips: idle (15 channels), run (15 channels), fortify (15 channels), die (15 channels).
- SHA-256: `e23e8dff7983f153f95014c98f7d510f6c99400a27e046823d3b1f7894556dae`.
- idle: `art/units/workboat_ancient/workboat_ancient_md_idle.kf`.
- run: `art/units/workboat_ancient/workboat_ancient_md_run.kf`.
- fortify: `art/units/workboat_ancient/workboat_ancient_md_fortifya.kf`.
- die: `art/units/workboat_ancient/workboat_ancient_md_die.kf`.
- Note: Missing texture: ship_rope02.dds

### military:worker

- Model: `src/Mods/Military/art/models/worker.glb` (246,888 bytes).
- Art: `ART_DEF_UNIT_WORKER`. Source scale: 0.44.
- NIF: `art/units/worker/worker.nif`.
- KFM: `art/units/worker/worker.kfm`.
- Geometry: 884 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3281, feet Y=0.
- Clips: idle (56 channels), run (56 channels), fortify (56 channels).
- SHA-256: `79d239be2761c095426714680714a9e526dc12404f9eaae25fe4e1faf8f6c863`.
- idle: `art/units/worker/worker_md_idle.kf`.
- run: `art/units/worker/worker_md_run.kf`.
- fortify: `art/units/worker/worker_md_fortify.kf`.

### military:worker_android

- Model: `src/Mods/Military/art/models/worker_android.glb` (242,792 bytes).
- Art: `ART_DEF_UNIT_WORKER_ANDROID`. Source scale: 0.44.
- NIF: `art/units/android worker/modernworker.nif`.
- KFM: `art/units/android worker/modernworker.kfm`.
- Geometry: 854 triangles, 1 skins, 16 joint bindings, 2 textures.
- Bounds: height 0.6000, radius 0.3656, feet Y=0.
- Clips: idle (53 channels), run (53 channels), fortify (53 channels).
- SHA-256: `f0ece74c69219519fadde39b586c1d3ace15ad8fd769d560e090226e9bfc39a0`.
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
- Bounds: height 0.4587, radius 0.4800, feet Y=0.
- Clips: idle (21 channels), run (21 channels), fortify (21 channels), attack (21 channels), hurt (21 channels).
- SHA-256: `55b535dad33ca14adfb5ee80ba37ca2a6403f940a63442394ce856b7c363140d`.
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
- SHA-256: `dda78b1a617765a3a5d10c9b337200ce7b1de0ae4d46995f594ea621361913f3`.
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
- SHA-256: `efec0f76920fa42f0d01b92c037c006cdf5ad7405d182a8c0b015d6d6bb85df9`.
- idle: `art/units/zulu_impi/zulu_impi_md_idle.kf`.
- run: `art/units/zulu_impi/zulu_impi_md_run.kf`.
- fortify: `art/units/zulu_impi/zulu_impi_md_fortifya.kf`.
- attack: `art/units/zulu_impi/zulu_impi_md_strikea.kf`.
- hurt: `art/units/zulu_impi/zulu_impi_md_hurta.kf`.
- die: `art/units/zulu_impi/zulu_impi_md_diea.kf`.
