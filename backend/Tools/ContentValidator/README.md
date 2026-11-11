# Content-set validator (EP-CORE story009 / FND-010)

This BCL-only .NET10 CLI validates content without Unity, a running API, a
database or file writes. It does not load content into the game, select rewards
or approve production balance. The existing DEV_ONLY fixture and all its
authored values remain unchanged.

From the repository root:

```sh
dotnet run --project backend/Tools/ContentValidator/ContentValidator.csproj -- --file backend/Config/CurioDistrict.Config/profiles/dev-seed.v0.1.json
dotnet run --project backend/Tools/ContentValidator/ContentValidator.csproj -- products.json items.json pools.json fixtures.json
```

Use one or many positional JSON file paths, or repeat `--file <path>`. All
inputs comprise **one content set**. Select one profile version at a time;
passing two complete profiles intentionally produces duplicate declarations
and an incorrect pool count. The CLI does not infer or discover additional
files. `--help` prints usage and exits0. No arguments, an unknown option or a
missing `--file` argument exits2. A valid set exits0; any unreadable input,
malformed JSON, unsupported schema or semantic violation exits1. An unreadable
or malformed file does not prevent reporting defects from the remaining files.

`ContentSetValidator.Validate(IEnumerable<ContentSource>)` is the pure in-memory
entry point; it never reads the paths carried by `ContentSource`. The CLI alone
reads files. Results expose `PoolCount`, all `Violations`, `IsValid` and
`Format()`. Diagnostics have `Kind`, `FilePath`, `JsonPath` and `Message`.
They sort ordinally by kind, file, JSON path and message, and `Format()` groups
them by kind. Numeric semantic diagnostics use invariant formatting. Kind
names are `Input`, `Parse`, `Schema`, `Identifier`, `DuplicateId`, `PoolCount`,
`PoolWeight` and `Reference`.

## Recognized content format

Root JSON must be an object. It may be the complete imported story006 profile
or a partial document with any subset of the following members. Collections
may be empty in an individual partial document. Null collections/objects,
duplicate JSON members, incorrect types and unknown members at any recognized
object level are violations. This prevents unsupported reference fields from
silently disappearing through permissive typed deserialization. JSON property
names and identifiers are case-sensitive; identifier comparison is ordinal.
Every occurrence of a recognized duplicate member is inspected, including root
collections and nested reference arrays; the final occurrence never hides an
earlier discoverable reference or declaration. Repeated reward arrays are
checked separately, and ambiguous duplicate weight members cannot establish
a valid total. Duplicate-member schema violations remain in the result.

| Root member | Recognized fields |
| --- | --- |
| `configVersion` | Optional string, retained as context; this tool does not infer or select profile versions |
| `metadata` | `product`, `profileId`, `version`, `status`, `currency`, `source` strings; `weightScale` integer; `notes` string array |
| `economy` | `currency` string; `npc_item_resale` boolean; integer `starting_wallet`, `district_tick_seconds`, `district_shops_cap`, `district_tick_demand`, `staff_wage_cashier`, `staff_wage_restocker`, `staff_wage_purchasing`, `wallet_min_reserve`, `supplier_spend_max_per_tick`, `PERCENT_LOOT_SUM` |
| `products` | Array of product objects described below |
| `itemDefinitions` | Array of item objects described below |
| `lootPools` | Array of pool objects described below |
| `fixtures` | Array of fixture objects described below |
| `npcModels` | Array of NPC model objects described below |

Each declaration requires its nonblank string identifier. Optional contextual
fields, when present, must have the listed type. Integer values must fit Int64.

- **Product:** required `skuId` and `poolId`; optional strings `kind`, `seriesId`,
  `name`, `milestone`; optional integers `supplierCost`, `recommendedPrice`,
  `minPrice`, `maxPrice`; optional boolean `stackable`.
- **Item:** required `definitionId`; optional strings `kind`, `seriesId`,
  `displayName`, `rarity`, `prefabId`, `localizationKey`, `presentation`;
  optional `references` array of item-definition identifier strings.
- **Pool:** required `poolId`, `sourceSkuId` and `rewards` array; optional string
  `version`, optional integer `totalWeightExpected` (if present, must be10000).
  Every reward object requires `definitionId` and integer `weight`, with an
  optional `rarity` string. Zero weights are valid; negative weights are not.
- **Fixture:** required `fixtureId`; optional strings `displayName`,
  `compatibleType`, `firstStage`; optional integer `capacitySeed`; optional
  `grid` object recognizing integer `w` and `h` only.
- **NPC model:** required `modelId`; optional strings `type`, `archetype`,
  `description`, `milestone`.

Across all files, the five declaration kinds share a **global ID namespace**.
Duplicate diagnostics include the ID and both declaration locations, including
cross-type collisions. Referencing an existing declaration repeatedly does not
declare it again. `metadata.profileId`, `seriesId`, `prefabId`, localization
keys and compatibility strings are context, not new declarations.

References resolve against their expected kind across the complete input set:
`products[].poolId`→pool; `lootPools[].sourceSkuId`→product SKU;
`lootPools[].rewards[].definitionId`→item definition;
`itemDefinitions[].references[]`→item definition. An unresolved reference
reports the referring identifier, source file/field and missing target. The
`references` member is an explicit content-tool extension for story009 AC2;
it adds no recipe or game mechanic and does not alter the production fixture.

Exactly **four pools** must be present, and each must total exactly **10000**
basis points using checked Int64 arithmetic. These are the story's format
invariants, not newly invented probabilities. Diagnostics accumulate across
files and violation kinds; invalid or unparseable weights cannot yield PASS.
An unrecoverable syntax error necessarily prevents inspecting the malformed
document's remaining structure; other files continue to be checked.

This tool complements story006's seed-profile validation. It does not certify
every economy rule, asset existence, production balance or runtime safety.
Local CLI/test evidence does **not** fulfill story009 AC4's actual red/green
PR CI requirement. Story005 owns workflow integration; no remote CI, commit,
push, merge or deployment is performed by this CLI.
