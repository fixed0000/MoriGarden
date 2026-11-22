# Chao personality model — stage 1.2

Implemented 2026-11-23. Scope: data, initialization, serialization and compatibility. No new behavior planner, needs system, inheritance or personality-driven face changes.

## Research and limits

No `docs/chao-personality-audit.md` existed at the start. The audit used the actual code and these Chao Island research pages:

- [Personality & Emotion](https://chao-island.com/wiki/Personality) / [indexed canonical article](https://chao-island.com/wiki/Personality_%26_Emotion): thirteen signed scales; the article explicitly marks some research as incomplete/speculative. Direct wiki access returned HTTP 403, but its indexed text was available.
- [Chao Personality Offsets](https://chao-island.com/guides/chao-hacking/hacking-pc/chao-offsets/chao-personality.html): accessible directly, explicit sign direction for five named scales.

These are community reverse-engineering sources, not official internal variable names. The wiki uses a magnitude above 54 for named personalities; the older offsets guide uses 46. This stage does not reproduce the doctor's personality display. The boolean compatibility threshold **55**, and true-to-signed-value mapping **75**, are explicit MoriGarden choices, not a claim to resolve that disagreement.

All scales are integers in **[-100,100]** with **0 neutral**. The `Naive` property keeps the original signed axis: more negative means more naive, despite its property name.

| MoriGarden property | Original label | Negative end | Positive end | Evidence / known influence |
|---|---|---|---|---|
| Curious | Normal–Curious | Normal side | Curious | Sign explicit in offsets; detailed original formulas not established here |
| Kindness | Kindness | Lower kindness | Higher kindness | Scale listed; no verified original formula used |
| Energetic | Cry Baby–Energetic | Crybaby | Energetic | Sign explicit; wiki reports longer crying on negative side |
| Naive | Naive–Normal | Naive | Normal side | Sign explicit; no effect added |
| Solitude | Solitude | Lower stored solitude | Higher stored solitude | Wiki associates scale with loneliness; exact signed behavioral direction not sufficiently verified |
| Vitality | Vitality | Lower stored value | Higher stored value | Listed scale; no verified formula used |
| BigEater | Normal–Big Eater | Normal side | Big eater | Sign explicit; wiki reports ravenous eating; existing MoriGarden fullness rules retained |
| Regain | Regain | Lower stored value | Higher stored value | Listed scale; signed effects not established here |
| Skillful | Skillful | Lower stored value | Higher stored value | Listed scale; not treated as the existing Dexterous boolean |
| Charm | Charm | Lower stored value | Higher stored value | Listed scale; effect unknown here |
| Chatty | Chatty | Lower stored value | Higher stored value | Listed scale; not treated as Sociability |
| Carefree | Normal–Carefree | Normal side | Carefree | Sign explicit; no effect added |
| Fickle | Fickle | Lower stored value | Higher stored value | Listed scale; exact original effect not established here |

“Lower/higher stored value” deliberately does not invent an opposite personality or an undocumented effect. Chao World Extended behavior is not evidence of vanilla SA2B behavior. Original game files/saves were not modified for research.

## Canonical representation

`ChaoPersonality` has exactly the thirteen public integer properties above. It supports centralized rejecting validation, independent `Copy()`, and value equality/hash. It has no references to a pet or random generator. No ordinary update mutates it.

`PetState.PersonalitySchemaVersion` and nullable `PetState.Personality` hold the canonical model. `EnsurePersonality()`:

- Version 0, missing/null personality: deterministic legacy mapping below; no random generation.
- Version 0 with a complete valid personality: retain it and mark version 1, rather than rerolling or overwriting it with aliases.
- Version 1: require a complete valid personality and retain it.
- Negative/future versions: reject. Future data never silently falls back to an older backup, including manual slots.
- Invalid/missing scale, null scale, wrong type, unknown member or out-of-range canonical value: fail validation, never remove a pet to continue.

All thirteen canonical properties are required in JSON when an object is present. Explicit null with version 1 is an error; null with version 0 is treated as absent legacy data. Omitted values must not silently erase part of a version-1 personality.

## Legacy audit and mapping

| Existing field | Existing default / validated range | Migration and ongoing compatibility |
|---|---|---|
| Curiosity | 0 / -100..100 | Exact signed copy to Curious; source adapter writes canonical value |
| Kindness | 0 / -100..100 | Exact signed copy; source adapter writes canonical value |
| Glutton | false / bool | true → BigEater=75; false → 0; read is BigEater>54 |
| Crybaby | false / bool | true → Energetic=-75; false → 0; read is Energetic<-54 |
| Sociability | 50 / 0..100 | Retained separately; no inferred Chatty, Charm or Solitude |
| Initiative | 0 / 0..990 | Retained separately |
| Aggressiveness | 0 / -100..100 | Retained separately |
| ActivityPreference | 3 / 1..5 | Retained separately |
| Clumsy | false / bool | Retained separately; no inferred Skillful |
| Dexterous | false / bool | Retained separately; no inferred Skillful |

Other canonical scales migrate to zero. Boolean migration cannot recover an original continuous value; ±75 encodes the prior true state, not recovered SA2B data. Setting an unchanged bool preserves its full signed scale. Changing true/false deliberately maps to ±75/0.

Reads were audited across garden actions, eating, toys, emotions, sleep, social activities, expedition intent/discovery, animal encounters, racing/karate and editor controls. Production writes to overlapping fields were in initialization, validation, editor patches and `ChaoMemory.Grow`. Curiosity/Kindness growth through events is now disabled as explicitly required for innate personality. Memories, their biases, relationships, Bond/Sociability growth and all activity formulas/thresholds remain in place. Existing legacy invalid numeric traits now fail safely rather than being silently clamped during migration.

## JSON and one source of truth

Source-compatible `Curiosity`, `Kindness`, `Glutton`, `Crybaby` are `[JsonIgnore]` adapters. Separate nullable JSON import properties consume old names into private staging fields; they always serialize as null and are omitted. Deserialization callback migrates **after all properties** have arrived. If canonical data exists it wins over legacy aliases, in either JSON order. Staging fields are cleared after migration.

New saves emit only `PersonalitySchemaVersion` and the canonical thirteen-value object, not overlapping aliases. Serialization callback ensures an ordinary programmatically constructed old-style `PetState` also migrates before serialization. `SaveStore.Snapshot`, validation, children inside `GardenEggs`, editor snapshots and ordinary JSON round trips all use the same model.

Editor requests apply allowed compatibility fields to a detached deserialized candidate via source adapters, validate it, then replace the selected pet. They do not inject old alias fields into a canonical JSON document. Schema version is not an editable primitive. Snapshots expose `personalitySchemaVersion` and `personality` per stable pet ID. Python retains these snapshots while continuing to send patches; no thirteen-slider UI is added. Stats and social relationship editing remain supported.

## Newborns

Explicit `InitializeFreshPersonality(Random?)` is called only by `PetState.NewEgg()` and `GardenEgg.MakeHatchling()` when it actually creates a new `PetState`. Existing hatchlings retain their personality. The breeding flow still creates ordinary eggs, so a newborn gets initialized through the same hatchling path; parent selection and inheritance are unchanged.

`CreateInitial(Random)` uses the sum of two independent discrete uniforms [-40,40] per scale. This is a reproducible, centered triangular **MoriGarden** distribution spanning [-80,80], with more moderate than extreme values. It is not claimed to be SA2B's original birth distribution. Future inheritance can supply a complete model instead of calling the factory. Calling fresh initialization twice is rejected.

## Safety and verification

Before tests, both known Godot save directories were copied with relative paths, SHA-256 and original mtimes to an ignored private artifact folder. Personal save data is never committed. A separate old-format copy of the actual four-pet Mori garden is the migration fixture; the active personal files are not migrated on disk.

Safety from stage 1.1R remains: an invalid resident fails the entire load; no replacement garden is created, and an unreadable existing save cannot be overwritten. Unsupported future personality versions stop even when an older readable backup exists. Other errors may offer an explicit backup-loaded warning through existing behavior, but saving over the damaged primary is still blocked.

Tests cover all scales/bounds, serialization order, exact signed migration, null/incomplete/unknown schema, three distinct pets, repeated migration, newborn seeds, source adapters, no event mutations, editor patches and original-file protection. The real Mori-copy audit verifies full canonical Load→Save→Load equality and the original roster. Existing stats, memory, discovery, expedition, social, wildlife, sports and editor regressions are run. The former experience-growth assertion was updated to the new explicit rule that baseline personality stays fixed.

Graphical regression runs can set `MORI_TEST_DIRECTORY` to a fresh absolute folder under project `artifacts`; this is accepted only in test mode. Real-garden inspection uses `MORI_SAVE_DIRECTORY` pointing to a copied save, as in stage 1.1R. Neither option changes the normal personal save destination.

Stage 1.3 and expanded behavior/needs/habits are intentionally not implemented.

### Results, 2026-11-23

- Build: zero errors and warnings.
- C# suite: **320 checks passed**; Python editor suite: **5 tests passed**.
- Real four-pet copied save: `RECOVERY_AUDIT_OK` and `RECOVERY_SCENE_OK`; all thirteen personality values match between the migrated copy and the saved real scene for each resident.
- Graphical full regression: `SMOKE_OK`, including stadium/all twenty slots, garden interactions, landscape/water, expedition team and discoveries, greenhouse, wildlife and expression/memory checks. No stderr errors.
- Archive: **264 files** verified against their hashes and mtimes; **119 pre-existing pet/slot save files** unchanged at the original paths.

Changed files: `Scripts/ChaoPersonality.cs`, `Scripts/PetState.Personality.cs`, `Scripts/PetState.cs`, `Scripts/GardenEgg.cs`, `Scripts/SaveStore.cs`, `Scripts/GardenGame.SaveSafety.cs`, `Scripts/ChaoEditorBridge.cs`, `Scripts/ChaoMemory.cs`, `Tools/chao_editor.py`, `Tests/PersonalityModelChecks.cs`, `Tests/MemoryChecks.cs`, `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `Tests/test_chao_editor_stats.py`, and this document.
