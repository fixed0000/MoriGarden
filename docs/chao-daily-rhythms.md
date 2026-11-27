# Flexible daily rhythms — stage 1.5C2

These coefficients and learned habits are MoriGarden extensions, not verified formulas from Sonic Adventure 2 Battle. There is no required schedule, new AI, calendar, offline simulation or new personal time counter.

## Architecture and audit

Before this change, GardenGame advanced the root PetState.GardenSeconds only in Mori Garden and Coastal Cove, after pause/team-selection/transition guards. GardenSky used the existing +145 offset and 600-second unequal solar cycle. Mori and GardenEgg already shared GardenActivityAdapter/ChaoActivityCoordinator and their existing executors. ChaoRest requires at least 35 seconds of sleep and full Energy before the existing Rising animation; IslandNap has its own existing guarded lifecycle. None of those durations, animations or need rates were changed.

Audited GardenSky, GardenGame/Activities, GardenEgg, GardenActivityAdapter, ChaoActivityCoordinator, ChaoActivityHabits, ChaoNeeds, ChaoRest, IslandNap, PetState/CurrentState, SaveStore, SaveIntegrity, ChaoStateDiagnostics, ChaoEditorBridge, Python editor and existing needs/action/habit documentation and tests. Interaction completion hooks and habit learning are untouched.

```
root PetState.GardenSeconds
  -> GardenDayCycle.Evaluate
     -> GardenSky: identical angle, altitude, light and twilight
     -> GardenActivityContext: root time + area + existing actor ID
        -> cached personal offset -> ChaoDailyRhythmScorer
        -> coordinator: existing scores + small rhythm term
        -> existing interests / rest / social / leisure / wander executors
```

The root time is explicitly passed by GardenGame.ActivityContext for both Mori and resident adapters. Child PetState.GardenSeconds is not used as a second clock. No singleton lookup, Process node, timer or additional physics loop was added.

## Changed files

* `Scripts/GardenDayCycle.cs` — pure shared solar calculation, phase weights and constants.
* `Scripts/ChaoDailyRhythmScorer.cs` — immutable context, stable ID offset, coefficients, physiological night gate and read-only capture.
* `Scripts/GardenSky.cs` — consumes shared calculation; existing colors, lighting strengths, sun/moon direction, clouds and gull behavior remain.
* `Scripts/ChaoActivityCoordinator.cs` — optional context and RhythmAdjustment before the old repeat penalty; diagnostics.
* `Scripts/GardenActivityAdapter.cs` — cached actor offset, explicit world context and narrowly expanded Sleep availability.
* `Scripts/GardenGame.Activities.cs`, `Scripts/GardenGame.cs` — root clock context/editor integration and isolated test flag.
* `Scripts/PetState.CurrentState.cs` — JsonIgnore runtime diagnostics, retained across editor rebind; no saved schema changes.
* `Scripts/ChaoStateDiagnostics.cs`, `Scripts/ChaoEditorBridge.cs` — existing debug panel and per-ID editor snapshots.
* `Tools/chao_editor.py` — live read-only Experience → Daily rhythm; neighboring tab labels shortened to fit, content stays read-only.
* `Scripts/GardenGame.DailyRhythmChecks.cs` — generated isolated native scene test.
* `Tests/DailyRhythmChecks.cs`, `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `Tests/test_chao_editor_stats.py` — pure/integration/statistical/editor regression coverage.
* This document.

GardenEgg, ChaoRest, IslandNap, SaveStore and SaveIntegrity production code need no changes. Their existing paths are exercised rather than duplicated. Save compatibility needs no migration.

## Shared mathematical model

```
cycle = positive_mod(GardenSeconds + 145, 600)
fraction = cycle < 410 ? 0.5*cycle/410 : 0.5 + 0.5*(cycle-410)/190
angle = 2*pi*fraction
altitude = sin(angle)
daylight = smoothstep(-0.07, 0.09, altitude)
night = 1 - daylight
twilight = 1 - smoothstep(0.012, 0.13, abs(altitude))
```

Sky now receives a double GardenSeconds and reduces the cycle before conversion to float. The original float solar fraction/angle evaluation is retained. The sky's 410/190 timing and all rendering constants remain unchanged. Pure and native Godot numerical sweeps compare against the original formula at 60000 samples.

Nominal phase boundaries in **cycle position**, not GardenSeconds, are 20 (Dawn→Day), 390 (Day→Dusk), 430 (Dusk→Night), 580 (Night→Dawn). Dawn centers on 0/600; Day on 205; Dusk on 410; Night on 505. Short twilight phases preserve the existing unequal arcs. Each boundary blends over ±20 seconds with `u=clamp((cycle-(boundary-20))/40,0,1)`, `s=u*u*(3-2*u)`; adjacent weights are `1-s` and `s`. The wrap uses Night→Dawn at 560…600 and Dawn→Day at 0…40, both reaching pure Dawn at the seam. Weights sum to one and are continuous with zero derivative at endpoints.

GardenSeconds 60 is Day, 265 Dusk, 360 Night, 455 Dawn. CurrentPhase is the nominal label; blended weights determine scores.

## Individual rhythm

FNV-1a (uint32 overflow, initial 2166136261, multiplier 16777619) consumes the low byte then high byte of each UTF-16 code unit in the **existing stable Chao ID**. Offset is `-20 + 40*hash/uint.MaxValue`. No runtime string hash, display name, list position or random generation is involved. The adapter caches it at construction; editor caches per ID. There is no serialized offset or version to migrate.

Behavior uses weights evaluated at `root seconds + personal offset`. Actual sky/light and night-rest eligibility always use unshifted root time. An offset cannot invent night during visual day.

## Coefficients and selection

| Activity | Dawn | Day | Dusk | Night |
| --- | ---: | ---: | ---: | ---: |
| Eat | 0 | 0 | 0 | 0 |
| Sleep | .025 | 0 | .035 | .080 |
| Play | .020 | .050 | .015 | -.025 |
| Socialize | .010 | .030 | .045 | -.015 |
| Leisure | .050 | .035 | .025 | -.025 |
| Wander | 0 | 0 | 0 | 0 |

`RhythmAdjustment = sum(phase weight * coefficient)`, clamped to [-.05,+.08]. The pure scorer also exposes individual phase contributions and reason.

```
BeforeRepeat = clamp(BaseScore + PersonalityAdjustment + HabitAdjustment + RhythmAdjustment, 0, 1)
ChoiceScore = max(0, BeforeRepeat - RepeatPenalty)
```

Base formulas, trait terms, habit decay/learning, repeat .15, close-choice .12, Fickle variation, cooldowns and minimum commitment are unchanged. Missing context is neutral. The context additionally checks area IDs `garden` and `coastal-cove`; Plaza, Stadium and expedition spaces receive zero even if accidentally supplied a context.

Rhythm changes do not interrupt Atomic, ShortSafe or LongOptional activities. ShouldInterrupt keeps the existing physiological-only comparison using base+personality minus repeat; **both habits and rhythm are excluded**. Critical available hunger/rest still win deterministically. No phase transition writes memories or changes habits, needs, fixed favorites, personality, evolution or sports stats.

## Sleep availability and waking

The adapter keeps its existing dry surface, SafeRestFloor, bed distance/slot and rest-claim checks. Only the Energy gate expands:

```
Energy < 58
OR (Energy <= 75 AND actual night intensity >= .60 AND active garden context)
```

Moderate fatigue makes Sleep an available candidate, not a command. Energy 100 blocks immediate re-entry after full rest. An occupied bed cannot be used; ground rest remains possible only through the same safe free-place rules. Day sleep at ordinary fatigue remains possible. Phase changes never end play or sleep. The old minimum duration, Energy=100 normal wake criterion, Rising and bed hop are unchanged.

Coastal Cove retains its real availability constraints; the rhythm does not fabricate food, toys or social executors there. GardenSeconds remains frozen during pause and retains the old garden/travel semantics. Save/load restores phase from existing time; editor patches cannot edit GardenSeconds.

## Diagnostics

Existing debug panel shows root seconds, cycle, phase, daylight/night, cached offset, Energy, physiological rest reason and last safe-place probe. Existing coordinator rows now include base/personality/habit/rhythm/repeat/final choice. Runtime snapshots are JsonIgnore.

Chao Editor adds read-only Daily rhythm inside Experience, refreshed from the shared game snapshot once per second without rebuilding editable controls. It shows actual phase, personal offset, coefficients, Energy, current habits, physiological night eligibility and last physical safe-place probe. The latter is explicitly a last probe, not a claim that a currently occupied place can be used. No clock/trait sliders were added. Editor diagnostics use the current root clock even for inactive roster entries; unavailable actors are not granted actions.

## Statistical checks

Twelve scenarios × **20000 independent choices = 240000**. Each trial uses a new coordinator and matching seed; it does not carry a previous action, location, reservation or repeat history forward. All profiles share the same canonical needs and personality; only one learned habit differs. Personal offset is zero for this controlled coefficient comparison; actual ID-derived offsets are tested separately.

Balanced fixture: Energy=50 (ordinary Sleep available in every phase), Fullness=80, Fun=42.105263, SocialNeed=61.40351, Joy=70; Curious=100, Energetic=-100, Carefree=100, Vitality=100, remaining innate traits=0. Same four valid actions Sleep/Play/Socialize/Leisure; Eat/Wander intentionally unavailable. The latter never win. Chosen fixture avoids mistaking a close-choice-window exclusion for a monopoly. Energy=70 night/day availability is separately tested through the actual adapter.

| Profile | Phase | Sleep | Play | Socialize | Leisure |
| --- | --- | ---: | ---: | ---: | ---: |
| Neutral | Day | 23.54% | 25.93% | 25.39% | 25.14% |
| Neutral | Dusk | 25.33% | 24.01% | 26.12% | 24.54% |
| Neutral | Night | 29.32% | 23.18% | 24.31% | 23.18% |
| Play | Day | 0.00% | 38.19% | 31.07% | 30.75% |
| Play | Dusk | 24.05% | 27.82% | 24.81% | 23.32% |
| Play | Night | 27.77% | 27.25% | 23.02% | 21.96% |
| Socialize | Day | 0.00% | 31.71% | 37.54% | 30.75% |
| Socialize | Dusk | 44.65% | 0.00% | 55.35% | 0.00% |
| Socialize | Night | 27.77% | 21.97% | 28.30% | 21.96% |
| Leisure | Day | 0.00% | 31.71% | 31.07% | 37.22% |
| Leisure | Dusk | 24.05% | 22.80% | 24.82% | 28.32% |
| Leisure | Night | 27.77% | 21.97% | 23.02% | 27.25% |

Neutral Play increases Day versus Night; neutral Sleep increases Night versus Day. Socialize receives its exact +.045 dusk term. Each learned habit increases its own category relative to the corresponding neutral profile in all three phases. No optional action becomes the only choice. Existing close-window filtering can still exclude lower-score alternatives in some trained scenarios; this behavior was intentionally not rewritten.

## Verification

* Build: .NET 8 / Godot 4.5.1 mono, zero warnings/errors.
* 579 C# checks passed, including all prior personality/needs/coordinator/preferences/habits/save/sports checks and 40 new daily-rhythm checks. Read-only calculation tests begin with canonically initialized statistics, avoiding the existing lazy stat migration in a raw new-pet fixture.
* Six Python editor checks passed, including live Daily rhythm formatting/read-only behavior and patch exclusions.
* `--daily-rhythm-check`: generated isolated three-pet scenario in real Godot. Native sky comparison, day/dusk/night, Energy70 safe night bed and day rejection, occupied slots, natural approach/hop/settling, full minimum sleep/recovery/wake/Rising, no immediate repeat, independent habits/offsets/shared time, Coastal Cove transition, actual paused physics, actual editor rebind and night save/load.
* Full `--smoke-test`: exit 0 / SMOKE_OK, with Stadium races/Karate/contact/all20slots, ordinary interactions/landscape/water, garden transitions, expeditions/discovery/greenhouse/wildlife/personality and existing sleep/IslandNap/ChaoRest tests.

Dedicated native regressions also passed with exit 0: HABITS_SCENE_OK/PARTNER_SCENE_OK, ACTIVITY_SCENE_OK, NEEDS_SCENE_OK (with its required `--needs-debug` panel flag), LEARNED_SCENE_OK, PLACE_SCENE_OK, PERSONALITY_GARDEN_OK (three real actors, 600 simulated seconds). An initial needs invocation omitted the required debug flag; rerunning with the panel enabled passed the unchanged assertion. The generated daily fixture explicitly clears default egg entries before adding its own three actors; no real user roster is replaced.

Native captures `daily-Day.png`, `daily-Dusk.png`, `daily-Night.png`, `daily-Sleep.png`, `daily-Rising.png` were produced by the actual viewport in the isolated daily run. Final desktop review is recorded below.

## Safety and remaining limits

Before integration, 269 source files were copied with SHA-256/mtime manifests, including the real four-pet recovery profile and personal Godot data. All 121 source save/slot files verified unchanged after the native daily and full smoke runs; only engine logs rotated/updated in Godot's default log location. No test uses the personal save path. Four-pet recovery audit retained Mori, White Chao, Black Chao, Yellow Chao and stable IDs with canonical round-trip equality. Final desktop review uses a new copy of that audited profile, never generated test pets.

This is a bounded preference layer, not a scheduler. Short twilight boundaries are authored MoriGarden tuning; behavior need not visibly change on every cycle because needs, traits, habits and actual targets continue to dominate. No new resting places, navigation, sleep controller or expedition rhythm was introduced. No guarantees of identical choice frequencies in an uncontrolled live garden are implied by the controlled statistical fixture.

The existing long-personality regression still reports bounded `Leisure route blocked` starter failures (3 for one actor, 1 for another) while continuing through the old cooldown/fallback and passing. These existing route/navigation limitations are outside 1.5C2 and were not hidden by disabling tests or changing movement.

## Final desktop review — 27 November 2026

Launched normal MoriGarden with `MORI_SAVE_DIRECTORY` pointing to `artifacts/rhythms-1.5C2-c92da15a/rhythms-1.5C2-preview`, copied from the canonical four-pet recovery audit. Launched **the actual ChaoEditor.cmd**, not a substitute diagnostic window. Visually verified the four-pet roster, current sports-stat display, original personality/item data and Experience → Daily rhythm. The new pane fits, shows live advancing garden time, Day phase, per-pet offset, exact current coefficients, Energy/rest reason and unchanged habits. Read-only auto-refresh updates without sending editor patches.

Compared this copy's saved data against the frozen audited source: all four stable IDs/names, all thirteen innate traits, learned item preferences, activity habits, fixed favorites and directed SocialLinks preserved. Native and pure editor rebind tests separately confirm an ordinary field edit retains clock and learned/innate state. All 121 original source save/slot files still match their pre-test SHA-256 hashes; all 269 backup copies match their manifest. Engine logs alone rotate in the old default log folder.

Both the ordinary safe-copy game and its Chao Editor are left open for inspection, with the editor on Daily rhythm for Mori. Generated regression games exited normally. No later stage was started. User approval of the result is still pending.
