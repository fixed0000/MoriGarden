# Retained memory diagnostics — stage 1.7C

## Pre-implementation audit

Canonical memory is `PetState.History` (`ChaoMemory`), not `DiscoveryMemory`, item preferences, habits or root SocialLinks. The latter models respectively track object familiarity/reward gates, toy/fruit type Bias, category habits and directional relationships. No new journal or save schema is needed. `ChaoMemory.Validate` normalizes/rebuilds collections and MUST NOT be called by a viewer.

| Kind | Actual production writer/source | Existing readers |
|---|---|---|
| FirstVisit | History.Visit, called by GardenGame.AdvanceChaoMemories and StadiumSession; Source visit | retained-history UI; raw bias functions when their filters match |
| ReturnVisit | same Visit path after prior recorded/Visited area | same |
| AnimalSeen | DiscoverySystem, WildAnimal acquisition, GardenGame animal training; animal-species ID | KnowsAnimal; history UI; matching raw bias functions |
| IndependentStudy | ChaoExperience.Study after DiscoverySystem completion | Confidence, DiscoveryDecisions; ObjectBias; history UI |
| PlayerDiscovery | Study(requested) and DiscoverySystem player acknowledgement | ObjectBias; matching participant bias; history UI |
| SharedStudy | Study(guided); Together for nearby shared Discovery completion | ObjectBias, PartnerBias when IDs actually recorded; history UI |
| HelpReceived | DiscoverySystem finished guided discovery | matching ObjectBias/PartnerBias; history UI |
| HelpGiven | DiscoverySystem guide success | matching ObjectBias/PartnerBias; history UI |
| FailedAttempt | DiscoverySystem unreachable-interest cancellation | ObjectBias; history UI; not a separate trauma model |
| Pleasant | PetState player-care; FruitMeal finished food; GardenGame quiet-rest; leisure completion; ChaoSocialPartnerScorer completed ball/music; SportsProgress entry/win/cup | matching PartnerBias/PlaceBias/ObjectBias, specialized leisure scorer; history UI |
| Unpleasant | player-care; ball invitation scolding; difficult sports setback | matching raw bias functions; history UI |
| SharedRest | ExpeditionTeam → ChaoExperience.Together | PartnerBias; history UI |
| Introduction | ChaoIntroductions completion → Together | PartnerBias; history UI |

All three raw bias readers filter actual Source/Area/Participants/Place; Kind alone does not prove participation in any particular choice. `DiscoveryDecisions` consumes ObjectBias/Confidence. `ExpeditionTeam` consumes PartnerBias, PlaceBias and FavoritePlace. Garden social selection uses ChaoSocialPartnerScorer; garden leisure uses its specialized scorer. Existing CHAO MEMORIES remains a compact player-facing reader, not this technical table.

Some Study(guided) descriptions include a guide's name without Participants. The viewer must preserve the empty participant list, not infer an ID. Unknown Source and Area remain their saved strings.

## Existing limits and time

History stores at most **64 retained significant records**, never a complete reconstruction of a life. Equal `(Kind,Source,Area,Participants)` keys coalesce after the existing 120-active-second gate; Occurrences saturates at 20. A weaker record can be evicted at capacity. The viewer shows every retained record, including negative and neutral values, without manufacturing timestamps for merged repetitions. One record has one FirstAt/LastAt interval and one count.

`PastUnknown` is displayed verbatim. True means some earlier history may be unknown; false does not guarantee completeness after capacity/merging. Visited count/list are the existing saved set (bounded to 32), not inferred from today's location or friendships.

Event time is each Chao's subjective `History.Seconds`. FirstAt, LastAt, age and first-to-last span have exact active-second values and readable seconds/minutes/hours. GardenSeconds is shown separately from the **existing root garden clock**, shared by all residents. Their unused local GardenSeconds field must not be mistaken for another garden clock. No real calendar date is inferred.

The exact existing influence is:

```
halfLife = 1800 + Importance * 18000
Influence(now) = Importance * pow(.5, max(0, now - LastAt) / halfLife)
```

It is computed on demand, not saved, and reading never changes LastAt. Valence sign alone classifies Positive/Neutral/Negative. Kind remains the original enum name; FailedAttempt does not imply trauma, and Pleasant does not imply friendship or current happiness.

## Files and read-only architecture

New:

* `Scripts/ChaoMemoryDiagnostics.cs`: detached DTOs, defensive non-normalizing checks, summaries and model-backed breakdowns.
* `Scripts/ChaoObjectBiasScorer.cs`: shared pure success/failure calculation.
* `Tools/chao_memory_view.py`: local filters/sorts, timeline, details and three influence views.
* `Tests/ChaoMemoryDiagnosticChecks.cs`, `Tests/test_chao_editor_memory.py`, `Tests/check_memory_diagnostics_live.py`.
* `Scripts/GardenGame.MemoryDiagnosticChecks.cs`: isolated physical scenario and optional live frontend test.
* This document.

Modified: `Scripts/ChaoMemory.cs` only delegates ObjectBias to the identical shared arithmetic; `Scripts/ChaoEditorBridge.cs` publishes memoryDiagnostics and a runtime publication counter; `Tools/chao_editor.py` wires the new primary tab and refresh handling; `Scripts/GardenGame.cs` dispatches the isolated test flag; `Tests/Program.cs` and `Tests/Mori.Tests.csproj` wire the tests/helpers.

No change to Record, Visit, Grow, Influence, PartnerBias, PlaceBias, FavoritePlace, learned preferences, habits, personality, relationships, executors, navigation, saving or schema. The existing CHAO MEMORIES panel is untouched.

Capture reads only real saved records. It never calls Validate, Record, Visit, Grow, Credit, ObserveEvent, random selection, SaveStore or any repair. It copies participants, positions, visited areas and other DTO arrays; no mutable event/list references escape. Source sorting is always LINQ over copies. Invalid/nonfinite/inconsistent memory, unsupported Kind, oversized collections or duplicate row identities return `available:false` with a diagnostic error. They are not normalized, truncated, deduplicated or replaced. Empty valid history is distinguished from unavailable data.

`memoryDiagnostics` exists only in IPC output, never PetState JSON. It is not added to the request whitelist. An attempted memory edit/delete, even in a mixed request, is rejected atomically. No memory Apply, edit, delete, add or reset-history controls exist. The existing global Apply still operates only on its original editable sections.

## Interface and refresh

The primary **Memory** tab sits immediately after Personality (Stats and Personality retain their original positions). It contains Timeline, Influence, Places and Partners. The header identifies the Chao/ID, memory time, separate garden time, retained capacity, visited areas, signed counts and PastUnknown caveat.

Timeline shows every retained event, with vertical/horizontal scrolling and a full read-only detail pane. Default sorting is LastAt descending; FirstAt, Importance, Influence and Occurrences are selectable. Filters for Kind, Area, Source, participant **ID**, Valence and case-insensitive Description search run locally. Clear filters clears only UI criteria. Unknown Sources remain their saved IDs. Missing participants retain their IDs and a Not in current roster label; no Chao or relationship is created. Same display names remain distinguishable by IDs. Missing Place/Participants explicitly say Position not recorded / No recorded participants.

All text is passed as plain Tk widget values/Text inserts, never eval, Tcl substitution, HTML, BBCode, a command or path. Details preserve the complete existing ≤180-character Description, source IDs, exact timestamps, positions and merged-count caveat. Table descriptions may be visually shortened. Influence includes a readable summary of most significant/recent records, current recorded area's FavoritePlace and participants in positive records; these are retained evidence, not a mood/happiness ranking.

Per-ID browser state retains filters, sorting, selected event, scroll position and subsection through periodic refresh and ordinary form rebuilds. Stable runtime row identities are SHA-256 of JSON-encoded Kind/Source/Area/Participants; they are not new save IDs. Merged repetitions keep their row identity. Influence updates from each new game snapshot. Local filtering does not cause a Godot recalculation or IPC write.

The runtime-only publication counter lets the viewer notice that snapshots stopped advancing, even with unchanged memory time. After five seconds it says the game may be closed or paused and values belong to the last received snapshot. A temporarily missing snapshot preserves the last data and labels it unavailable/stale; no invented replacement is displayed. This timer only measures freshness, not event age or game time. It does not advance the garden, memory or decay.

## Partner diagnostics

For each roster peer and any absent recorded participant:

```
raw = sum(Valence * Influence(now)) for events containing peer ID
beforeClamp = raw * .07
PartnerBias = clamp(beforeClamp, -.30, +.40)
```

The final result calls the existing History.PartnerBias. Each contributing event lists its scaled **pre-clamp** contribution; their sum is not falsely labelled the post-clamp result. Counts, positive/negative counts and identity come from that Chao's history.

Directional relationship score and the separate relationship/memory contributions use `ChaoSocialPartnerScorer.Evaluate(root,seekerId,seeker,peerId, available:false)`. Reference PartnerWeight assumes distance zero and no recent penalty; FinalWeight remains zero until actual eligibility is established by gameplay. If available, the real last candidate snapshot is shown separately and explicitly labelled possibly older, not current eligibility.

Garden introductions, joining musicians and ball partner selection use this scorer; expedition social choices also consume PartnerBias. A sleeping/unavailable peer, protected action, lack of equipment, separation or refusal can override any positive memory. The UI says **possible contribution**, not that this record caused a specific decision. Root SocialLinks are never created or changed by the viewer.

## Places and leisure

Raw place rows are actual retained positions, grouped by Area and exact point. Relevant events require that Area and horizontal `distanceSquared < 9`; Y does not affect the radius. Each contributes `Valence * Influence * .18`. The final `History.PlaceBias(area,point)` clamps the total to [−.30,+.60]. Both pre-clamp total and true result are shown. FavoritePlace calls the original per-area function; absent favorite/coordinates remain absent. Expedition landmark exploration consumes raw PlaceBias/FavoritePlace. These are not garden route weights.

All eight canonical leisure destinations are shown using **ChaoLeisurePreferenceScorer.Evaluate**, with no recomputed alternative formula. Matching successful Pleasant route/sky completions contribute LearnedBias, while SpatialBias excludes **all** `garden/leisure/` sources. Thus the same completion is not counted twice. Reference weights explicitly assume available destinations and no repeat penalty; the real last candidate snapshot, when present, is separate. Neither diagnostic output nor an acquired preference bypasses terrain, reachability, reservation, need/category or action-protection gates.

## Objects

ObjectBias is distinct from ChaoLearnedPreferences toy/fruit Bias. Both gameplay and diagnostics now call the same extracted pure helper with the unchanged operation order:

```
success = sum(Influence * (1 + min(Occurrences,4) * .15)) for positive Source/Area records
failure = sum(Influence) for negative Source/Area records
ObjectBias = clamp(success*.10 - failure*.24/(1+success), -.25, +.25)
```

The magnitude of Valence is NOT multiplied into these branches; its sign selects success/failure. Neutral records contribute to neither. Matching positive/negative event lists, intermediates and final result are visible. DiscoveryDecisions combines the result with perception, object familiarity, curiosity, initiative, energy, confidence and current activity. The viewer does not imply that every source is an available discovery or that ObjectBias governs all garden items.

## Automated and native verification

Build passes with zero warnings/errors. Full pure C# suite: **835 checks passed**, including 26 new diagnostics checks and existing memory, discovery, personality, needs/mood, coordinator, learned preferences/habits, places/partners, editor protocol, recovery, expeditions and sports. Python: 29 new real-Tk memory checks plus the unchanged 59 stats/personality/learned-editor checks. The combined final run passes **88 tests**.

All **12** existing native regression modes exit 0: personality-editor-check, learned-editor-check, smoke-test, chao-vocal-check, expression-integration-check, idle-gesture-check, daily-rhythm-check, needs-check with needs-debug, activity-habits-check, activity-check, learned-check and place-preferences-check. These retain real introductions, ball/music, sleep/rising, garden transitions, all slots/recovery, Expedition/Discovery, Stadium Race and Karate coverage. No original tests were edited or disabled.

The pure checks cover empty/all-13/64-record history, merged counts, exact time/Influence/half-life, passive decay, Valence, known/absent/same-name participants, positions, all raw bias functions, nonlinear object weighting, clamp limits, leisure separation, repeated byte-identical PetState capture, unchanged list/event identity, detached arrays, no schema, malformed/duplicate memory errors, request whitelist and save/load.

The real-Tk checks cover sorting, all filters, source/description casing, arbitrary text safety, missing metadata, retained selection/filter/scroll, live Influence update, no memory edit controls or requests, per-pet browser state, minimum size, stale/absent snapshots, invalid-memory error and coexistence with Personality/Learned drafts. Existing stats/personality/learned tests remain unchanged.

`--memory-diagnostics-check` requires `MORI_MEMORY_INPUT` pointing to an audited safe copy and an isolated `MORI_TEST_DIRECTORY`. It loads real saved IDs/models. Only in that test copy, controlled needs/Run and discovery repeat eligibility are prepared; no retained event is inserted to fake completion. The actual expedition Discovery approach/completion produces a history event; actual garden Leisure route/final pause produces its real source/position; two genuine ToyCar ensemble completions separated by the original cooldown produce both participants' memories and one merged repeat. Advancing active test time reduces Influence without changing LastAt. Repeated Capture/Bridge reads leave the serialized garden unchanged. The original CHAO MEMORIES opens, and Save/Load preserves exact events for all Chao.

Optional `--memory-diagnostics-live` plus `Tests/check_memory_diagnostics_live.py` exercises actual frontend selection, full rows/details, filters, all subsections, draft protection and no outgoing memory request. The native wrapper verifies that editor browsing changed no events. The final live run reports passed for four real IDs with **14/4/13/7 retained records** in the controlled scenario. A first harness minimum-size check inspected the hidden Timeline while the remembered subsection was Partners; the harness now explicitly selects Timeline. The browser's remembered subsection was functioning as designed.

## Safety, desktop review and remaining limits

Before integration testing, the previous ordinary preview game was cleanly closed. **272 backup files** were copied and verified, including current personal save/slot data and the actual most recent safe garden. The recovery audit loaded four existing pets and round-tripped canonical data without source writes. All **121 original save/slot files** remain unchanged. Tests use new ignored artifact directories, never the original profile.

Desktop review on **2026-11-28**: ordinary game and the actual `ChaoEditor.cmd` opened on a safe copy. Inspected Mori, White Chao, Black Chao and Yellow Chao, with **11/3/13/7 retained records** in that ordinary profile. Black Chao's negative event remains visible. Verified full details, active times, LastAt/Influence sorting, Kind filtering/Clear, Influence summary, Places/leisure separation, Partners and the original Personality/Learned controls. The per-Chao histories do not mix. While the game was focus-paused, navigating these read-only editor pages left the entire safe-profile save **byte-identical**; no outgoing request was written.

Visual inspection caught the residents' unused zero GardenSeconds field. The DTO now uses the existing root clock for everyone; a dedicated test and the restarted real game's snapshot confirm equal global garden time alongside independent memory times. No game clock or persisted field changed. The identity and clock header lines were separated for readable long IDs; the minimum-size test includes a full 32-character resident ID.

The original **CHAO MEMORIES** panel was opened using its existing method during the native physical scenario, rendered to `native-final/memory-panel.png` and visually inspected: actual important/recent events and the picker remain intact. Automated Tab input in the ordinary desktop game did not expose the card, so no claim is made that that particular keyboard path was manually verified; no input or navigation logic was changed in this diagnostic stage.

During the ordinary foreground run, Black Chao naturally gained Stamina progress 40→80 in the safe copy; this is not a diagnostic edit, and all original files retain their original statistics. No editor request was written. The paused read-only browsing interval itself remained byte-identical.

Per the user's final instruction, the game is closed after inspection; Chao Editor remains open on Mori → Memory with the last-snapshot/stale indication. The input and backup copies are kept in ignored artifacts. No next stage was started.

Only retained records can be displayed. Evicted or never-recorded events, exact intermediate repetition times, missing participants/positions and unknown earlier history cannot be recovered. Current action causality/eligibility is not inferred from a Bias. Full record descriptions are bounded by the existing memory system. Diagnostic errors do not repair the garden. No new memory, relationship, learning, behavioral or subsequent-stage feature is introduced.
