# Quaternius Ultimate Stylized Nature conversion report

Source archive: `Ultimate Stylized Nature - May 2022-20261014T154712Z-1-001.zip`.
Source FBX and texture files are read from the extracted pack directory; the archive is not shipped.
Licence: Quaternius pack `License.txt` states CC0 1.0 Universal (public domain dedication).
The previous 138,824,120-byte export repeated high-entropy 512 × 512 bark albedo and normal PNGs in each tree GLB; inspected GLBs had no unused FBX images.

Only referenced albedo textures are embedded; bark and rock textures are at most 256 × 256, while alpha-cutout PNGs are at most 512 × 512.
Bark normal maps are omitted because sub-pixel props do not benefit from their extra data and decode cost.

Forest snow variants tint pine leaf RGB values toward blue-white and preserve the source alpha.
GLB JSON is checked for embedded albedo-only images; cutout materials are written as `MASK` with cutoff 0.5.
## forest

22 models; 39,189 total triangles.

- `PineTree_1` — `FBX/PineTree_1.fbx`; 2,128 → 1,500 triangles; Y-up bounds 0.278 × 0.420 × 0.283; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/PineTree_1.glb` (435,440 bytes).
- `PineTree_2` — `FBX/PineTree_2.fbx`; 2,716 → 1,499 triangles; Y-up bounds 0.199 × 0.420 × 0.181; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/PineTree_2.glb` (440,368 bytes).
- `PineTree_3` — `FBX/PineTree_3.fbx`; 1,998 → 1,500 triangles; Y-up bounds 0.316 × 0.420 × 0.313; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/PineTree_3.glb` (434,224 bytes).
- `PineTree_4` — `FBX/PineTree_4.fbx`; 2,686 → 1,499 triangles; Y-up bounds 0.453 × 0.420 × 0.304; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/PineTree_4.glb` (438,892 bytes).
- `PineTree_5` — `FBX/PineTree_5.fbx`; 838 → 838 triangles; Y-up bounds 0.323 × 0.420 × 0.321; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/PineTree_5.glb` (418,504 bytes).
- `NormalTree_1` — `FBX/NormalTree_1.fbx`; 8,520 → 3,735 triangles; Y-up bounds 0.236 × 0.400 × 0.160; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/NormalTree_1.glb` (740,024 bytes).
- `NormalTree_2` — `FBX/NormalTree_2.fbx`; 8,084 → 2,749 triangles; Y-up bounds 0.278 × 0.400 × 0.170; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/NormalTree_2.glb` (639,356 bytes).
- `NormalTree_3` — `FBX/NormalTree_3.fbx`; 7,096 → 2,175 triangles; Y-up bounds 0.382 × 0.400 × 0.218; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/NormalTree_3.glb` (580,904 bytes).
- `NormalTree_4` — `FBX/NormalTree_4.fbx`; 6,338 → 2,816 triangles; Y-up bounds 0.445 × 0.400 × 0.188; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/NormalTree_4.glb` (646,232 bytes).
- `NormalTree_5` — `FBX/NormalTree_5.fbx`; 2,182 → 1,500 triangles; Y-up bounds 0.416 × 0.400 × 0.344; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/NormalTree_5.glb` (468,420 bytes).
- `BirchTree_1` — `FBX/BirchTree_1.fbx`; 4,598 → 1,919 triangles; Y-up bounds 0.198 × 0.400 × 0.142; textures: BirchTree_Bark.png 256×256, BirchTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/BirchTree_1.glb` (607,144 bytes).
- `BirchTree_2` — `FBX/BirchTree_2.fbx`; 7,660 → 2,226 triangles; Y-up bounds 0.177 × 0.400 × 0.149; textures: BirchTree_Bark.png 256×256, BirchTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/BirchTree_2.glb` (638,392 bytes).
- `BirchTree_3` — `FBX/BirchTree_3.fbx`; 6,818 → 4,147 triangles; Y-up bounds 0.238 × 0.400 × 0.157; textures: BirchTree_Bark.png 256×256, BirchTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/BirchTree_3.glb` (900,828 bytes).
- `BirchTree_4` — `FBX/BirchTree_4.fbx`; 3,562 → 1,500 triangles; Y-up bounds 0.263 × 0.400 × 0.174; textures: BirchTree_Bark.png 256×256, BirchTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/BirchTree_4.glb` (543,228 bytes).
- `BirchTree_5` — `FBX/BirchTree_5.fbx`; 4,520 → 1,500 triangles; Y-up bounds 0.259 × 0.400 × 0.254; textures: BirchTree_Bark.png 256×256, BirchTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/BirchTree_5.glb` (564,624 bytes).
- `MapleTree_1` — `FBX/MapleTree_1.fbx`; 3,848 → 1,499 triangles; Y-up bounds 0.375 × 0.400 × 0.295; textures: MapleTree_Bark.png 256×256, MapleTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/MapleTree_1.glb` (541,276 bytes).
- `MapleTree_2` — `FBX/MapleTree_2.fbx`; 4,800 → 1,500 triangles; Y-up bounds 0.399 × 0.400 × 0.372; textures: MapleTree_Bark.png 256×256, MapleTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/MapleTree_2.glb` (569,600 bytes).
- `MapleTree_3` — `FBX/MapleTree_3.fbx`; 6,678 → 1,741 triangles; Y-up bounds 0.514 × 0.400 × 0.379; textures: MapleTree_Bark.png 256×256, MapleTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/MapleTree_3.glb` (602,820 bytes).
- `MapleTree_4` — `FBX/MapleTree_4.fbx`; 1,256 → 1,256 triangles; Y-up bounds 0.308 × 0.400 × 0.271; textures: MapleTree_Bark.png 256×256, MapleTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/MapleTree_4.glb` (498,628 bytes).
- `MapleTree_5` — `FBX/MapleTree_5.fbx`; 4,504 → 1,500 triangles; Y-up bounds 0.385 × 0.400 × 0.279; textures: MapleTree_Bark.png 256×256, MapleTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/MapleTree_5.glb` (570,276 bytes).
- `Bush` — `FBX/Bush.fbx`; 400 → 400 triangles; Y-up bounds 0.150 × 0.095 × 0.144; textures: Bush_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/Bush.glb` (78,444 bytes).
- `Bush_Small` — `FBX/Bush_Small.fbx`; 190 → 190 triangles; Y-up bounds 0.150 × 0.061 × 0.057; textures: Bush_Leaves.png 512×512; output `src/Mods/World/art/scatter/forest/Bush_Small.glb` (57,036 bytes).

## forest_snow

8 models; 11,334 total triangles.

- `PineTree_1_Snow` — `FBX/PineTree_1.fbx`; 2,128 → 1,500 triangles; Y-up bounds 0.291 × 0.440 × 0.296; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png (snow tint) 512×512; output `src/Mods/World/art/scatter/forest_snow/PineTree_1_Snow.glb` (409,760 bytes).
- `PineTree_2_Snow` — `FBX/PineTree_2.fbx`; 2,716 → 1,499 triangles; Y-up bounds 0.209 × 0.440 × 0.190; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png (snow tint) 512×512; output `src/Mods/World/art/scatter/forest_snow/PineTree_2_Snow.glb` (414,688 bytes).
- `PineTree_3_Snow` — `FBX/PineTree_3.fbx`; 1,998 → 1,500 triangles; Y-up bounds 0.331 × 0.440 × 0.328; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png (snow tint) 512×512; output `src/Mods/World/art/scatter/forest_snow/PineTree_3_Snow.glb` (408,544 bytes).
- `PineTree_4_Snow` — `FBX/PineTree_4.fbx`; 2,686 → 1,499 triangles; Y-up bounds 0.475 × 0.440 × 0.319; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png (snow tint) 512×512; output `src/Mods/World/art/scatter/forest_snow/PineTree_4_Snow.glb` (413,216 bytes).
- `PineTree_5_Snow` — `FBX/PineTree_5.fbx`; 838 → 838 triangles; Y-up bounds 0.338 × 0.440 × 0.336; textures: PineTree_Bark.png 256×256, PineTree_Leaves.png (snow tint) 512×512; output `src/Mods/World/art/scatter/forest_snow/PineTree_5_Snow.glb` (392,828 bytes).
- `DeadTree_1` — `FBX/DeadTree_1.fbx`; 4,792 → 1,498 triangles; Y-up bounds 0.199 × 0.400 × 0.083; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/forest_snow/DeadTree_1.glb` (382,624 bytes).
- `DeadTree_2` — `FBX/DeadTree_2.fbx`; 5,344 → 1,500 triangles; Y-up bounds 0.220 × 0.400 × 0.093; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/forest_snow/DeadTree_2.glb` (384,076 bytes).
- `DeadTree_3` — `FBX/DeadTree_3.fbx`; 4,928 → 1,500 triangles; Y-up bounds 0.339 × 0.400 × 0.137; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/forest_snow/DeadTree_3.glb` (382,248 bytes).

## jungle

13 models; 15,751 total triangles.

- `PalmTree_1` — `FBX/PalmTree_1.fbx`; 472 → 472 triangles; Y-up bounds 0.479 × 0.480 × 0.465; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/jungle/PalmTree_1.glb` (445,648 bytes).
- `PalmTree_2` — `FBX/PalmTree_2.fbx`; 368 → 368 triangles; Y-up bounds 0.508 × 0.480 × 0.434; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/jungle/PalmTree_2.glb` (441,032 bytes).
- `PalmTree_3` — `FBX/PalmTree_3.fbx`; 344 → 344 triangles; Y-up bounds 0.460 × 0.480 × 0.434; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/jungle/PalmTree_3.glb` (439,784 bytes).
- `PalmTree_4` — `FBX/PalmTree_4.fbx`; 472 → 472 triangles; Y-up bounds 0.624 × 0.480 × 0.651; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/jungle/PalmTree_4.glb` (445,652 bytes).
- `PalmTree_5` — `FBX/PalmTree_5.fbx`; 264 → 264 triangles; Y-up bounds 0.934 × 0.480 × 0.968; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/jungle/PalmTree_5.glb` (437,140 bytes).
- `NormalTree_1_Large` — `FBX/NormalTree_1.fbx`; 8,520 → 3,735 triangles; Y-up bounds 0.295 × 0.500 × 0.200; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/jungle/NormalTree_1_Large.glb` (740,024 bytes).
- `NormalTree_2_Large` — `FBX/NormalTree_2.fbx`; 8,084 → 2,749 triangles; Y-up bounds 0.347 × 0.500 × 0.212; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/jungle/NormalTree_2_Large.glb` (639,352 bytes).
- `NormalTree_3_Large` — `FBX/NormalTree_3.fbx`; 7,096 → 2,175 triangles; Y-up bounds 0.477 × 0.500 × 0.273; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/jungle/NormalTree_3_Large.glb` (580,904 bytes).
- `NormalTree_4_Large` — `FBX/NormalTree_4.fbx`; 6,338 → 2,816 triangles; Y-up bounds 0.556 × 0.500 × 0.235; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/jungle/NormalTree_4_Large.glb` (646,228 bytes).
- `NormalTree_5_Large` — `FBX/NormalTree_5.fbx`; 2,182 → 1,500 triangles; Y-up bounds 0.520 × 0.500 × 0.431; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/jungle/NormalTree_5_Large.glb` (468,420 bytes).
- `Plant_1` — `FBX/Plant_1.fbx`; 420 → 420 triangles; Y-up bounds 0.150 × 0.083 × 0.145; textures: Flowers.png 512×512; output `src/Mods/World/art/scatter/jungle/Plant_1.glb` (158,248 bytes).
- `Plant_2` — `FBX/Plant_2.fbx`; 160 → 160 triangles; Y-up bounds 0.143 × 0.056 × 0.150; textures: Flowers.png 512×512; output `src/Mods/World/art/scatter/jungle/Plant_2.glb` (145,836 bytes).
- `Bush_Large` — `FBX/Bush_Large.fbx`; 276 → 276 triangles; Y-up bounds 0.170 × 0.109 × 0.065; textures: Bush_Leaves.png 512×512, Flowers.png 512×512; output `src/Mods/World/art/scatter/jungle/Bush_Large.glb` (65,808 bytes).

## swamp

13 models; 15,226 total triangles.

- `DeadTree_1` — `FBX/DeadTree_1.fbx`; 4,792 → 1,498 triangles; Y-up bounds 0.199 × 0.400 × 0.083; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_1.glb` (382,624 bytes).
- `DeadTree_2` — `FBX/DeadTree_2.fbx`; 5,344 → 1,500 triangles; Y-up bounds 0.220 × 0.400 × 0.093; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_2.glb` (384,076 bytes).
- `DeadTree_3` — `FBX/DeadTree_3.fbx`; 4,928 → 1,500 triangles; Y-up bounds 0.339 × 0.400 × 0.137; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_3.glb` (382,248 bytes).
- `DeadTree_4` — `FBX/DeadTree_4.fbx`; 3,528 → 1,500 triangles; Y-up bounds 0.419 × 0.400 × 0.145; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_4.glb` (379,608 bytes).
- `DeadTree_5` — `FBX/DeadTree_5.fbx`; 1,360 → 1,360 triangles; Y-up bounds 0.221 × 0.400 × 0.236; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_5.glb` (369,352 bytes).
- `DeadTree_6` — `FBX/DeadTree_6.fbx`; 2,984 → 1,500 triangles; Y-up bounds 0.337 × 0.400 × 0.206; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_6.glb` (377,680 bytes).
- `DeadTree_7` — `FBX/DeadTree_7.fbx`; 3,504 → 1,500 triangles; Y-up bounds 0.389 × 0.400 × 0.319; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_7.glb` (378,348 bytes).
- `DeadTree_8` — `FBX/DeadTree_8.fbx`; 4,944 → 1,500 triangles; Y-up bounds 0.520 × 0.400 × 0.306; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_8.glb` (382,632 bytes).
- `DeadTree_9` — `FBX/DeadTree_9.fbx`; 824 → 824 triangles; Y-up bounds 0.249 × 0.400 × 0.087; textures: NormalTree_Bark.png 256×256, NormalTree_Leaves.png 512×512; output `src/Mods/World/art/scatter/swamp/DeadTree_9.glb` (351,064 bytes).
- `DeadTree_10` — `FBX/DeadTree_10.fbx`; 3,208 → 1,500 triangles; Y-up bounds 0.307 × 0.400 × 0.182; textures: NormalTree_Bark.png 256×256; output `src/Mods/World/art/scatter/swamp/DeadTree_10.glb` (378,308 bytes).
- `Grass_Large` — `FBX/Grass_Large.fbx`; 108 → 108 triangles; Y-up bounds 0.150 × 0.103 × 0.105; textures: Grass.png 512×512; output `src/Mods/World/art/scatter/swamp/Grass_Large.glb` (203,040 bytes).
- `Grass_Large_Extruded` — `FBX/Grass_Large_Extruded.fbx`; 516 → 516 triangles; Y-up bounds 0.150 × 0.102 × 0.100; textures: Grass.png 512×512; output `src/Mods/World/art/scatter/swamp/Grass_Large_Extruded.glb` (237,308 bytes).
- `Plant_1` — `FBX/Plant_1.fbx`; 420 → 420 triangles; Y-up bounds 0.150 × 0.083 × 0.145; textures: Flowers.png 512×512; output `src/Mods/World/art/scatter/swamp/Plant_1.glb` (158,248 bytes).

## oasis

6 models; 2,196 total triangles.

- `PalmTree_1` — `FBX/PalmTree_1.fbx`; 472 → 472 triangles; Y-up bounds 0.479 × 0.480 × 0.465; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/oasis/PalmTree_1.glb` (445,648 bytes).
- `PalmTree_2` — `FBX/PalmTree_2.fbx`; 368 → 368 triangles; Y-up bounds 0.508 × 0.480 × 0.434; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/oasis/PalmTree_2.glb` (441,032 bytes).
- `PalmTree_3` — `FBX/PalmTree_3.fbx`; 344 → 344 triangles; Y-up bounds 0.460 × 0.480 × 0.434; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/oasis/PalmTree_3.glb` (439,784 bytes).
- `PalmTree_4` — `FBX/PalmTree_4.fbx`; 472 → 472 triangles; Y-up bounds 0.624 × 0.480 × 0.651; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/oasis/PalmTree_4.glb` (445,652 bytes).
- `PalmTree_5` — `FBX/PalmTree_5.fbx`; 264 → 264 triangles; Y-up bounds 0.934 × 0.480 × 0.968; textures: PalmTree_Leaves.png 512×512, PalmTree_Trunk.png 256×256; output `src/Mods/World/art/scatter/oasis/PalmTree_5.glb` (437,140 bytes).
- `Bush_Large` — `FBX/Bush_Large.fbx`; 276 → 276 triangles; Y-up bounds 0.170 × 0.109 × 0.065; textures: Bush_Leaves.png 512×512, Flowers.png 512×512; output `src/Mods/World/art/scatter/oasis/Bush_Large.glb` (65,808 bytes).

## rocks

5 models; 1,330 total triangles.

- `Rock_1` — `FBX/Rock_1.fbx`; 228 → 228 triangles; Y-up bounds 0.105 × 0.160 × 0.131; textures: Rocks.png 256×256; output `src/Mods/World/art/scatter/rocks/Rock_1.glb` (106,308 bytes).
- `Rock_2` — `FBX/Rock_2.fbx`; 202 → 202 triangles; Y-up bounds 0.150 × 0.160 × 0.153; textures: Rocks.png 256×256; output `src/Mods/World/art/scatter/rocks/Rock_2.glb` (105,288 bytes).
- `Rock_3` — `FBX/Rock_3.fbx`; 712 → 300 triangles; Y-up bounds 0.103 × 0.160 × 0.107; textures: Rocks.png 256×256; output `src/Mods/World/art/scatter/rocks/Rock_3.glb` (108,560 bytes).
- `Rock_4` — `FBX/Rock_4.fbx`; 456 → 300 triangles; Y-up bounds 0.064 × 0.160 × 0.060; textures: Rocks.png 256×256; output `src/Mods/World/art/scatter/rocks/Rock_4.glb` (109,400 bytes).
- `Rock_5` — `FBX/Rock_5.fbx`; 632 → 300 triangles; Y-up bounds 0.123 × 0.160 × 0.126; textures: Rocks.png 256×256; output `src/Mods/World/art/scatter/rocks/Rock_5.glb` (114,948 bytes).

## Triangle-budget misses

- `forest/NormalTree_1`: 3,735 triangles exceeds 1,500; Leaf cards were not decimated.
- `forest/NormalTree_2`: 2,749 triangles exceeds 1,500; Leaf cards were not decimated.
- `forest/NormalTree_3`: 2,175 triangles exceeds 1,500; Leaf cards were not decimated.
- `forest/NormalTree_4`: 2,816 triangles exceeds 1,500; Leaf cards were not decimated.
- `forest/BirchTree_1`: 1,919 triangles exceeds 1,500; Leaf cards were not decimated.
- `forest/BirchTree_2`: 2,226 triangles exceeds 1,500; Leaf cards were not decimated.
- `forest/BirchTree_3`: 4,147 triangles exceeds 1,500; Leaf cards were not decimated.
- `forest/MapleTree_3`: 1,741 triangles exceeds 1,500; Leaf cards were not decimated.
- `jungle/NormalTree_1_Large`: 3,735 triangles exceeds 1,500; Leaf cards were not decimated.
- `jungle/NormalTree_2_Large`: 2,749 triangles exceeds 1,500; Leaf cards were not decimated.
- `jungle/NormalTree_3_Large`: 2,175 triangles exceeds 1,500; Leaf cards were not decimated.
- `jungle/NormalTree_4_Large`: 2,816 triangles exceeds 1,500; Leaf cards were not decimated.
- `jungle/Plant_1`: 420 triangles exceeds 300; Plant and grass geometry was kept intact.
- `swamp/Grass_Large_Extruded`: 516 triangles exceeds 300; Plant and grass geometry was kept intact.
- `swamp/Plant_1`: 420 triangles exceeds 300; Plant and grass geometry was kept intact.

Total GLB output: 67 files, 27,440,824 bytes.
Pack root used for this conversion: `/tmp/civ-nature-src/Ultimate Stylized Nature - May 2022` (ignored work directory only).
