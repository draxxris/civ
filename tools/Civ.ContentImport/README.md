# Content importer

This .NET 8 tool builds a generic registry from the local source extraction, then compiles typed Core definitions.
It uses no external parsing packages. Generated sidecars are the runtime content. The extraction is not needed to build the game.

## Run

Supply the extraction directory as a CLI argument:

```sh
mise exec -- dotnet run --project tools/Civ.ContentImport -- --source <source-directory> --reports tools/Civ.ContentImport/reports --out-mods src/Mods
dotnet test Civ.slnx
```

Use `--dump tools/Civ.ContentImport/out` for the optional, git-ignored generic dump.
The default mod destination is `src/Mods`. Use `--out-mods` to select another directory.
Use `--max-era foundation:era_ancient` for a closed ancient subset. Without this filter, the importer emits all eras.
Reports contain no timestamps or absolute machine paths.
The tool rejects output directories inside the source directory.
Extraction-backed tests skip when the source is absent. `CIV_CONTENT_SOURCE` can specify its location for tests.

## Input and profile

The reader uses System.Text.Json for version-one tree and reference files. References resolve relative to the extraction's data directory.
Nodes retain strings, ordered children, repeated tags, mixed text, and attributes. Reference provenance remains separate from canonical tree provenance.

Core files use whole-file fallback from the source layer through the expansion layers to the original game.
Schema files, control files, and README examples do not contribute records.
The default MLF configuration selects module directories. Nested controls replace their parent directory with enabled descendant directories.
Disabled parents exclude all descendants. Files inside each selected directory use ordinal path order.
The native Windows directory order remains unknown. The profile-files report records the exact chosen order.

The registry includes every Type-bearing Info record in these trees, including art and fallback metadata.
Some metadata families use fixed native enums rather than the XML loader. This census does not claim runtime enum equivalence.

The civic recovery must contain 81 unique civic types and valid civic categories.
The tool checks the quarantined file's hash and strict comment error.
It repairs only double hyphens within existing comment boundaries, then compares the complete canonical data tree.
This proves comment-only recovery, not the installed DLL's parser behavior.

## Merge policy

The policy version is `profile-v1-merge-v1`. Collisions are measured before accumulation.
Every collision operation records its rule, source, and changed direct fields. Unknown collision classes fail instead of guessing.

Source references are relative to the supplied extraction:

- `docs/01-data-and-loader.md` describes the loading model.
- `reference/and-source/CvGameCoreDLL/CvXMLLoadUtilitySet.cpp` describes overwrite and discard controls.
- `reference/and-source/CvGameCoreDLL/CvInfos.cpp` contains the class-specific copy methods.

Implemented policies:

- Civilization, leaderhead, miscellaneous, and unit art: full replacement. Their source copy methods are empty.
- Force overwrite: full replacement, including explicit zeros and omitted fields.
- Force delete: discard the incoming contribution, not the previous record.
- A nonzero integer define named after a Type also discards its incoming contribution.
- Civilization scalars: empty strings, false, numeric zero, and missing-reference sentinels inherit previous values.
- Civilization keyed arrays: retain previous entries and inherit default slots. Leaders and free flags retain previous true values.
- Civilization unit/building overrides: a class-default value inherits a previous override. Explicit NONE disables that class.
- Civilization cities: incoming names precede previous names, without duplicate additions from the previous list.
- Initial civics: positional inheritance in the generic merge model. The measured profile only changes this field through force overwrite.
  The typed schema retains explicit declarations. A civic's category, not its list position, determines its option.
- Era scalars: default inheritance. Soundtracks append previous tracks after incoming tracks, including duplicates.
- Era soundscapes: positional default inheritance.
- Diplomacy: match response masks by intersecting active leader, attitude, and power sets. Merge matching text lists and flags.
- Diplomacy retains unmatched responses and moves updated responses to the end. A single previous response always matches.

The measured profile needs only art, civilization, era, and diplomacy policies. Tech, building, unit, and promotion gameplay records do not collide.
The tool preserves unresolved string references rather than native integer slots and late read passes.
Conditional dependency/replacement records fail explicitly if encountered.
Diplomacy deliberately avoids native loop-state bugs. Untested complex mask combinations remain approximate.
Class defaults for unit/building overrides come from the final class tables, not native registration-time indices.

## Defines

The tool loads the reference DLL's explicit core define sequence, then font defines, then extra core define files.
Module defines follow profile order. Later values replace earlier values, including zero.
The report records all overrides and file order.
The reference loader's optional extra define file is absent locally. Extra core files include interface and Python callback defines.
Their placement after explicit files is an approximation. The reference source is not proven to match the installed DLL.

## English and neutral markup

Fallback English trees load first. English slices load in ordinal path order. Enabled module English text loads last.
Later text keys replace earlier keys. The visible grammar form uses the English Text child.
The generic dump also retains grammar nodes for later typed parsing.

Neutral markup:

- `[ICON_GOLD]` becomes `{icon:gold}`.
- `[COLOR_WARNING_TEXT]text[COLOR_REVERT]` becomes `{color:warning_text}text{/color}`.
- `[LINK=TECH_EXAMPLE]text[\LINK]` becomes `{link:tech_example}text{/link}`.
- Typed emission resolves known record links through the ID map. Other links become `{topic:target}text{/topic}`.
- NEWLINE becomes one newline. PARAGRAPH becomes two newlines. SPACE and TAB become their corresponding characters.
- BULLET and BULLETS become `● `. Literal bullet characters become `●`.
- `%s1_city` becomes `{arg:1:text:city}`. `%d2` becomes `{arg:2:number}`. Double percent signs become one percent sign.
- Unknown tokens remain intact. The archival tree retains original markup. The resolved text view uses neutral markup.

The scanner reports source-title, parent-title, short-name, and standalone-acronym matches.
Title recognition also accepts underscore separators and case variants. Ordinary conjunctions and historical names remain unchanged.
Reports show neutral excerpts, not original branded prose. All serialized string values and dictionary keys pass through the scrubber.
Source-layer path segments and source-branded symbols also receive neutral names.

## IDs and ownership

The complete class-to-mod table is in `reports/id-map.md`. The complete record map is in `reports/id-map.json`.

Primary families remove these prefixes: TECH, BUILDING, UNIT, CIVILIZATION, CIVIC, TERRAIN, FEATURE, BONUS,
IMPROVEMENT, ROUTE, BUILD, RELIGION, CORPORATION, PROJECT, EVENT, and PROMOTION.
Secondary families retain their complete lowercase symbol. Thus leaders use `peoples:leader_x`, and traits use `peoples:trait_x`.
Class records retain their class prefix. Art records retain their art prefix.
If two classes produce the same ID, both names receive the class stem plus the complete source symbol.
The importer fails if disambiguated IDs still collide. Neutral substitutions occur before lowercase conversion.

Specialists belong to foundation, as the plan specifies. Great-person units remain military records.
Great-people and trade have no dedicated Info classes in this profile. Later rules consume units, specialists, routes, and constants.

## Census and era annotations

The census contains direct fields and nested field paths. Presence and non-default counts are per record, not per occurrence.
Repeated paths count once. Non-default means non-empty and not 0, NONE, -1, or false.
A branch counts as non-default if any descendant does. These census sentinels are not native getter defaults.
Every field status is `mapped` or `unsupported`. Mapped means that typed content retains the field, not that gameplay rules implement it.
Without typed emission, all statuses are unsupported. Examples contain up to three distinct values, truncated at 160 characters.

Tech eras come from their Era field. Other records use their prerequisite tech fields and TechTypes lists.
Multiple required techs use the latest era in source era order. Records without derivable prerequisites have an unknown era.
These annotations are lower bounds, not availability guarantees. They do not resolve class indirection or OR prerequisite graphs.
Era counts appear in Markdown. Per-record era annotations appear in census JSON, the ID map, and generic dumps.

Main-table count comparisons use the validation report's core sparse inventory only as a baseline.
The importer never reads sparse records as content. Enabled modules add 20 buildings, 20 units, 20 civilizations, and 49 leaders.

## Validation

Synthetic tests cover tree fidelity, reference errors, nested controls, merge defaults, art replacement, text precedence,
markup, branding, census counts, and ID conflicts.
Source-backed tests check profile counts, selected merged values, recovery evidence, deterministic typed output, and closed era filtering.
`Civ.Mods.Tests` loads the feature mod DLLs from build output. It checks counts, typed closure, golden records, and branding without the extraction.

## Typed emission

The declarative `SchemaMapping.json` maps source fields to clean Core properties. `RuleConstantMapping.json` selects typed rule constants.
The mapper preserves native positional pairs, partial vectors, explicit zeroes, negative costs and slots, and prerequisite scopes.
Building and unit classes resolve to their default definitions. Civilization overrides apply to starting grants too.
Every definition includes inline English text, an era, an icon, and sorted unsupported source paths.

Output arrays use ordinal id order. Object properties use ordinal order. Ordered source arrays retain their order.
Output uses indented camelCase JSON. The importer scans decoded keys and values and fails if forbidden branding remains.
It writes one file for each schema family, including empty arrays after an era filter.
It only replaces its own sidecars, not future manual hex overrides or unrelated data files.

The filter removes later-era records first. It then removes transitive dependents across all typed references and text links.
This conservative policy can remove early records with later obsolescence, upgrade, or effect references.
`dropped-content.json` records every removed id and the edge or era that caused removal.

`emitted-counts.json` separates shipped schema counts from the larger generic inventory in `counts.json`.
It also lists every direct content provider for each manifest's cyclic `references` declaration.
All v1 load-order `dependencies` are empty because these data-only mods have no overrides or ordering constraints.
The game checks provider presence first, then checks reference types after all mods register.

Religion emits 13 playable `ReligionDef` records with founding technologies, missionaries, commerce vectors, and related buildings.
The importer removes the obsolete generated Foundation religion identity sidecar. Religion references now use `religion:*` ids.
Foundation reserves corporation and victory identities for future rules. Their definitions have `Deferred = true` and no gameplay effects.
Their evaluators fail closed. Prerequisites retain these identities instead of silently disappearing.
Four native unit-AI roles absent from the XML inventory receive explicit military identities.
The native enum and `registerUnitAIs` in the reference DLL define these roles.
Four stale optional source references become null. Required unresolved references fail import.
Native unit outcomes retain chances, yields, grants, and mission actions. One missing delivery-policy define affects 34 units.
The native boolean getter returns false for each missing define. The emitted effects retain the policy keys too.
`data-issues.json` identifies these source errors.

No authoritative JSON number uses floating point. The food-wastage growth factor converts exactly to integer permille.
The raw define dictionary retains other values as strings. See [the schema contract](../../docs/content-schema.md) for units and scope parameters.

The runtime mods do not implement gameplay systems yet. Partial requirement evaluators only use existing state and otherwise fail closed.
