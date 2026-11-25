# Learned leisure destinations — stage 1.5B1

Audit before edits, 2026-11-26. This extends MoriGarden, not SA2B navigation or original preference formulas.

Existing ChaoLeisure has four four-point Routes and four SkySpots. Reconsider filters supported terrain, Step executes the existing steering/motion, final waypoint pauses and sky watching/rising; Cancel is shared by both interruption and old successful cleanup. Success must therefore be split from Cancel. Route coordinates/order, movement, rising, 300s timeout and 2.5s stuck threshold remain intact.

ChaoMemory already persists bounded events (64), coalesces a key at >=120 active seconds, decays Influence, and exposes spatial/object/partner views. Only destination-specific Pleasant events are new. No place dictionary, save schema or toy/fruit preference field is added.

GardenActivityAdapter binds each actor's leisure context from the existing WorldTravel area, before using the old executor. Main and resident paths keep their existing Step calls. Coastal Cove has no leisure targets; expedition control is separate. ChaoWander's arbitrary goals are not catalog places and remain unchanged. Old quiet-rest memories remain spatial evidence, distinct from destination-completion records.

## Files and integration

New: `Scripts/LeisureDestinationCatalog.cs`, `Scripts/ChaoLeisurePreferenceScorer.cs`, `Scripts/GardenGame.PlaceChecks.cs`, `Tests/PlacePreferenceChecks.cs`, and this document.

Changed: `Scripts/ChaoLeisure.cs` (selection and distinct completion), `Scripts/GardenActivityAdapter.cs` (actual actor/area binding and interruption), `Scripts/GardenGame.Activities.cs` (WorldTravel area), `Scripts/GardenEgg.cs` (internal executor access for isolated checks), `Scripts/PetState.CurrentState.cs` (preserve runtime diagnostic snapshot during editor rebind), `Scripts/ChaoStateDiagnostics.cs`, `Scripts/ChaoEditorBridge.cs`, `Tools/chao_editor.py`, `Scripts/GardenGame.cs` (test-mode entry only), `Tests/Mori.Tests.csproj`, `Tests/Program.cs`.

Neither `ChaoActivityCoordinator` category formulas nor `ChaoMemory`, `ChaoLearnedPreferences`, SaveStore/SaveIntegrity, ChaoMotion, mountain/jump logic, expedition AI, toy/fruit learning, or personality values are changed.

## Registry

Canonical area is `garden`, obtained from `WorldTravel.AreaId(zone)`, never History.LastArea.

| Stable ID | Ordered points (X,Y,Z) |
|---|---|
| garden/leisure/route-0 | (12,0,12), (29,0,12), (46,0,12), (64,0,12) |
| garden/leisure/route-1 | (-15,0,14), (-25,0,14), (-35,0,14), (-45,0,14) |
| garden/leisure/route-2 | (36,0,5), (36,0,17), (36,0,29), (36,0,41) |
| garden/leisure/route-3 | (-10,0,25), (-22,0,30), (-34,0,35), (-46,0,40) |
| garden/leisure/sky-0 | (0,0,12) |
| garden/leisure/sky-1 | (11,0,10) |
| garden/leisure/sky-2 | (-2,0,4) |
| garden/leisure/sky-3 | (14,0,-1) |

LeisureDestinationCatalog is the single explicit ID/geometry mapping. Existing Godot arrays derive from it. Static validation checks completeness, unique IDs, indices and point counts; regression tests compare all 20 anchors against the pre-edit coordinates. No destination is spawned or marked in the world.

## Recording genuine completion

`ChaoLeisure.Complete` is called only when the final route waypoint's scheduled pause finishes, or when the 24-second sky observation AND 1.5-second ChaoRising sequence finish. It rechecks support beneath the destination and the unchanged garden context, calls `ChaoLeisurePreferenceScorer.RecordSuccess`, and clears activity state. Both main MoveFriend and GardenEgg.AdvanceChao use this executor.

Record: Kind=Pleasant, Source=stable destination ID, Area=garden, Valence=.55, Importance=.55, Place=route endpoint or selected sky anchor. Participants are empty. There is no credit for intermediate waypoints, arrival, selection or starting to rise.

`Cancel` only cleans up and publishes Interrupted/Failed diagnostics. Pickup/control/call, a shared action, a non-garden area, airborne/recovery state, critical-need interruption, blocked movement (existing 2.5 seconds), timeout (existing 300 seconds), and unsupported final destination cannot take the success path. The coordinator still protects its existing atomic and short-safe phases; no new interruption policy is imposed on category selection.

Repeated idle frames cannot replay a finished activity. ChaoMemory.Record may refuse a valid completion because of its existing 120-second coalescing interval or 64-event capacity; execution still succeeds and updates the runtime recent-place marker. No replacement ledger or forced eviction is introduced.

## Derived scores, not a new save model

For the matching positive Pleasant event at the exact Source and Area:

```
learned = clamp(.05 * min(5, Occurrences) * Influence(now) / Importance, 0, .25)
```

No event or nonpositive importance yields zero. Existing Influence uses `Importance * pow(.5, elapsed / (1800 + Importance*18000))`. Thus a .55-importance completion has a half-life of 11700 active Chao seconds. Reads never rewrite memory, favorites or personality.

Generic spatial evidence is calculated separately: matching area, Place within horizontal squared distance <9 of ANY actual point of this destination; every event contributes once, including memories near intermediate route points. All Sources beginning `garden/leisure/` are excluded, including other leisure destinations, preventing overlap from counting completion events twice.

```
spatial = clamp(sum(Valence * Influence(now) * .18), -.15, +.15)
total = clamp(learned + spatial, -.30, +.30)
weight = clamp(1 + total, .70, 1.30)
```

Successful recent route/sky is tracked independently in each ChaoLeisure instance. If the chosen type has more than one available candidate, only its last completed destination gets `weight *= .85`. Cancellation does not change this marker. Markers and selection snapshots are runtime-only and absent from save JSON.

When both types are available, sky retains its 32% probability. If only one type is available, use that type; if neither is available, fail safely. Within the type, choose proportionally to positive weights using a cumulative draw. This replaces the previous random-times-score maximum, not the activity category formula.

Each executor owns one System.Random. Its default stream is seeded once using the actor's stable ID and an explicit deterministic character hash (not string.GetHashCode); it is not recreated every frame. Tests can inject Random. Incidental movement/pause randomness remains the existing Godot behavior. Save/load does not promise to resume the identical RNG stream.

## Diagnostics and persistence

Existing needs/activity debug output now includes current ID/type, actual area, available candidates, learned/spatial/total bias, weight, last successful route and sky, and previous outcome. The existing Experience tab has a `Garden places — read-only` subsection with eight rows. No gameplay HUD with preference numbers or new editor write operation is introduced.

Only ordinary ChaoMemory.Events persist. PetState.LeisureSelection is JsonIgnore. Old saves require no migration/new defaults, no new pet IDs and no second place-preference schema. Editor patches continue to modify only requested fields; memories and learned toy/fruit preferences remain intact.

## Checks — 2026-11-26

* Build: 0 warnings, 0 errors.
* C#: **479 checks passed, 0 failed**, including 23 new pure place checks and the previous personality, needs, activity, item learning, saving, editor, expeditions and sports checks.
* Python: **5 editor checks passed**.
* Final isolated Godot place run: `PLACE_SCENE_OK`, no stderr errors. Actual main route-1 traversal completed all waypoints and final pause; actual GardenEgg sky-0 watching completed rising. Tests also cover repeated idle frames, cancellation, 301-second timeout, frozen-progress blocked route, interrupted rising, airborne interruption, critical fatigue during route approach, resident pickup, area change and save/load.
* Existing engine regressions: `ACTIVITY_SCENE_OK` (300 seconds, three live actors, real food/play/sleep/social, critical interruption, editor rebind, Coastal Cove rest); `LEARNED_SCENE_OK` (actual item completion, partial meal, refusal, cancellations, load and target selection); full `SMOKE_OK`, including stadium, 20 slots, travel, greenhouse, wildlife, camera, shoreline and expressions. Final engine logs have no stderr errors.
* Audited real-save copy: `RECOVERY_AUDIT_OK`, four existing Chao, canonical round-trip equality, original input unchanged, editor roster exported.

Pure route draws, 50000 independent matched seeds per pet, equal availability/position/history except the specified learning:

| History | route-0 | route-1 | route-2 | route-3 |
|---|---:|---:|---:|---:|
| A: five successes at route-0 | 14701 | 11767 | 11764 | 11768 |
| B: five successes at route-1 | 11762 | 14706 | 11764 | 11768 |
| C: neutral | 12497 | 12502 | 12499 | 12502 |

Expected preferred share is 1.25/4.25 = 29.41%; neutral share is 25%. Assertions use absolute tolerance .006 and separately verify retained 32% sky probability. All alternatives remain selectable.

Godot target draws: 1000 independently seeded/reset selections per actor; A learned sky-0, B route-1, C neutral. Actual supported terrain filtered route-3 out in this fixture (no geometry changed):

| History | route-0 | route-1 | route-2 | route-3 | sky-0 | sky-1 | sky-2 | sky-3 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| A | 227 | 226 | 227 | 0 | 96 | 76 | 72 | 76 |
| B | 207 | 261 | 212 | 0 | 80 | 80 | 80 | 80 |
| C | 227 | 226 | 227 | 0 | 80 | 80 | 80 | 80 |

These controlled histories are seeded test inputs; the separate actual main/resident completion checks above prove that real executors produce the underlying memories. Synthetic history creation is not presented as a real outing.

## Limitations and boundaries

Availability retains existing local surface support probes, not a new global pathfinder. Supported anchors do not guarantee a collision-free complete route: the old steering/stuck/timeout guards still apply and failed outings give no positive memory. Route-3's fixture exclusion was not repaired by changing terrain/navigation in this task.

The memory cap can evict weak memories, so preferences can be forgotten. Old rest and other spatial memories can affect several nearby destinations, but each event is counted only once per destination. Inactive/off-screen garden residents have no live availability probe; the editor still shows their memory-derived scores, with no claimed available targets until their actor is evaluated.

This stage does not add partner preferences, habits, routines, arbitrary Wander destination learning, expedition place learning, locations, routes or social actions. The formulas and seeded choice are MoriGarden extensions, **not** reconstructions of original SA2B preference or navigation formulas.

## Real-save and final editor review

Before tests, 274 files were copied with SHA-256 and nanosecond mtime manifests; every backup verifies unchanged. 121 source save/slot files also verify unchanged. Two files belonging to the already-running older **review copy** (pet.json and its backup) were autosaved by that existing game during this session; they were not test destinations and were not restored/overwritten. The personal save directory is unchanged. Tests and final preview use separate artifacts directories only.

The audited real garden was copied to `places-1.5B1-preview`; normal MoriGarden and **ChaoEditor.cmd** were launched there. Actual GUI review confirmed the correct Chao Editor title, Mori/White/Black/Yellow roster, actual Stats, separate innate/fixed/item sections, and all eight read-only Garden places rows with availability and weights. Both programs remain open.

Run Progress 60 -> 61 was sent through the actual editor's Apply changes button and acknowledged Applied. It was restored to 60 through the same editor bridge protocol. A fresh controlled 60 -> 61 -> 60 protocol round-trip then compared the live saved records: all four stable IDs, 13 innate traits, fixed favorites, learned item entries, memory events, relationships and final statistics remained identical. Active-time clocks may advance normally; they are not mistaken for lost data. A new real flute completion occurred during the earlier preview interval and legitimately increased Mori's item experience, so a stale pre-autosave file was not used as the baseline for the final exact comparison.

The user was using the desktop concurrently; further keyboard/mouse changes were avoided after user-input detection. No personal save or older profile was used for editor edits. No partner-preference or habit work was started.
