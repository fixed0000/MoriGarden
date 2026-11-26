# Learned activity habits — stage 1.5C1

2026-11-26. These are **MoriGarden-designed** habit mechanics, not recovered SA2B formulas. Stage 1.5C2/daily rhythms are excluded.

## Audit and implementation boundaries

ChaoActivityCoordinator already owns six categories, needs/personality scores, the .15 immediate-repeat penalty, .12 close-choice window, weighted random choice and interruption rules. GardenActivityAdapter connects both Mori and GardenEgg actors to the existing physical executors. Coordinator.Complete can follow executor cleanup and therefore is **not** evidence for learning. No habit credit is attached to it, selection, approach, failure, or cancellation.

Existing learned item preferences, place/partner memories, SocialLinks and innate personality remain separate. Only Play, Socialize and Leisure can learn; Eat/Sleep/Wander always have zero habit adjustment and cannot create habit entries. Island visits, cliff/pillar trips and watering do not acquire Leisure habits in this stage; only the verified ChaoLeisure route/sky boundary is connected.

## Files and methods

New:

* Scripts/ChaoActivityHabits.cs: ActivityHabitEntry, versioned per-pet model, Credit/EffectiveBias/Validate, runtime replay/diagnostics, canonical PetState.ActivityHabits.
* Scripts/GardenGame.HabitChecks.cs: actual solo/leisure/editor/save fixture, reusing verified social scenarios.
* Tests/ActivityHabitChecks.cs: focused A–Y checks, data safety and controlled statistics.
* docs/chao-activity-habits.md: this report.

Modified:

* Scripts/ChaoActivityCoordinator.cs: ActivityScore.HabitAdjustment, Evaluate addition and Describe output; ShouldInterrupt projects the original pre-habit scores to preserve its existing semantics. Choose and availability are unchanged.
* Scripts/PetState.cs: ValidateActivityHabits during validation.
* Scripts/PetState.CurrentState.cs: KeepCurrentRuntimeFrom preserves the bounded habit replay ledger and diagnostic state during editor replacement.
* Scripts/SaveIntegrity.cs: Prepare rejects unknown habit versions before deserializing root/residents.
* Scripts/SaveStore.cs: existing load/backup/slot schema-error filters also recognize UnsupportedActivityHabitsSchemaException. No new save format or file-writing path.
* Scripts/ToyCar.cs: session identity, CompleteSoloHabit, natural Advance/FinishEnsemble hooks, cleanup.
* Scripts/ChaoBallGame.cs: solo finish and existing ten-pass shared completion identity.
* Scripts/ChaoSocialPartnerScorer.cs: CompleteShared accepts the actual completion ID and credits each current participant once.
* Scripts/ChaoLeisure.cs: session identities at BeginRoute/BeginSky, credit in successful Complete, discard in Cleanup.
* Scripts/ChaoEditorBridge.cs, Scripts/ChaoStateDiagnostics.cs, Tools/chao_editor.py: read-only Activity habits and per-score diagnostics.
* Scripts/GardenGame.cs: --activity-habits-check entry through existing shared test harness.
* Scripts/GardenGame.PartnerChecks.cs: reuse real introductions/music/ball and add habit-only classification assertions; then run the dedicated habit fixture.
* Scripts/GardenGame.LearnedChecks.cs: assert real solo-ball Play habit while retaining all prior assertions.
* Scripts/GardenGame.ActivityChecks.cs: wait for the actual already-pending rattle play before the live-editor assertion, with a strict bound and failure/category guards.
* Tests/Mori.Tests.csproj, Tests/Program.cs: model/test wiring.
* Tests/SocialPartnerPreferenceChecks.cs: explicit shared completion IDs for the extended signature.
* Tests/test_chao_editor_stats.py: read-only habit diagnostics excluded from outgoing patches.

ChaoPersonalityActivityWeights, ChaoNeeds/ChaoMood, GardenActivityAdapter, ChaoMemory, ChaoLearnedPreferences, FruitMeal, relationship scores, sports, genetics/evolution, navigation and expedition decisions are not rewritten.

## Saved data and validation

One independent model per PetState:

```json
"ActivityHabits": {
  "SchemaVersion": 1,
  "Entries": {
    "Play": {
      "Bias": 0.02,
      "ExperienceCount": 1,
      "LastUpdateSeconds": 4.0,
      "LastCreditSeconds": 4.0
    }
  }
}
```

Only exact keys Play/Socialize/Leisure are accepted; missing keys are neutral. Bias must be finite in [0,.10], ExperienceCount in [0,1000], timestamps finite/nonnegative, LastCreditSeconds<=LastUpdateSeconds. Null/unknown/malformed fields are rejected, never replaced with random habits. Unknown schema versions reject the whole root/resident load, including when a readable older backup exists; the original is kept. Old saves lacking the entire field construct independent empty models.

Existing SaveIntegrity/SaveStore validate and persist the model through ordinary autosaves and the same twenty slots. Reading/decay does not modify entries or trigger saves. Save/load/repeated serialization preserve stored floats/times; other persisted pet fields remain canonical and unchanged.

## Learning and decay

Use only History.Seconds, the pet's existing active-time clock:

```
effective = storedBias * pow(.5, max(0, now - LastUpdateSeconds) / 24000)
factor = max(0, 1 - effective / .10)
newBias = clamp(effective + .02 * factor, 0, .10)
```

First credit is .02. Later credits diminish toward .10. With minimum-interval repeated experiences there is slight decay between updates; the steady value remains just below the cap. No negative learning exists here. Counts increment only on accepted credit, capped at 1000.

The first valid completion can credit immediately at active second zero. A subsequent completion of the same category requires >=120 active seconds. Different categories have independent credit intervals. LastCreditSeconds survives load.

Each logical executor session has a unique transient identity. Each pet remembers the last 128 processed completion IDs, including cooldown-rejected IDs: retrying an old rejection later cannot turn it into fresh experience while retained. The ledger is not saved and is carried across a live editor rebind. Loaded worlds do not restore ToyCar, ball, ensemble or leisure execution sessions or replay old completion callbacks; they may start only new physical sessions. Saving/loading itself cannot teach habits.

## Exact completion hooks and classification

| Existing boundary | Habit | Identity / safety |
|---|---|---|
| ToyCar.Advance crosses natural PlayDuration while solo | Play | Toy session GUID; actual valid/current player and PlayState, no ensemble |
| ChaoBallGame Solo reaches existing eight-second finish | Play | Same toy session; item learning remains independent |
| CompletePass reaches ten, guarded shared completion | Socialize | Existing ball contact-session identity, one credit per participant |
| ToyCar.FinishEnsemble(successful:true) | Socialize | Natural finishing instrument's session identity, actual ensemble members resolved through current PlayState |
| ChaoLeisure.Complete after final waypoint pause | Leisure | Outing GUID, unchanged valid garden/terrain checks |
| ChaoLeisure.Complete after sky watching AND rising | Leisure | Outing GUID; arrival or starting to rise gives no credit |

CompleteShared is the single shared habit boundary. It still records original partner memories and updates the runtime recent partner; it adds no SocialNeed or relationship reward. Introduction stays excluded. Each ensemble participant receives only Socialize, even when it also learns an instrument preference. Advance remembers whether the performance was shared before ensemble cleanup clears references, so cleanup cannot turn the same finish into solo Play. Other members are moved into the original placement phase at duration and cannot cross the natural boundary a second time.

CompleteLearnedPlay remains item learning only; it is not used as a general habit callback. Solo habit completion is separate and clears its session after the attempt. Forced cleanup clears the session without credit. Ball shared and solo phases are mutually exclusive and release ownership through the old paths. Cancelled ensembles give neither Play nor Socialize.

Leisure Cancel/Cleanup never teach; blocked routes, 300-second timeout, failed starts, intermediate waypoints, pickup, area change and need/player interruption retain old cancellation behavior. A real successful outing may independently update place memory and Leisure habit. A memory/habit recorder declining cooldown credit does not make the physical outing fail.

## Coordinator: before and after

Before: base needs score → bounded innate personality → repeat penalty → existing close-choice weighted draw.

Now:

```
habit = EffectiveBias(category, History.Seconds)  // zero for Eat/Sleep/Wander
beforeRepeat = clamp(BaseScore + PersonalityAdjustment + habit, 0, 1)
ChoiceScore = max(0, beforeRepeat - RepeatPenalty)
```

Physiological interruption comparisons use the original clamped base + personality − repeat score, excluding the new habit term. This preserves the existing live-action policy: a learned preference cannot newly cause or delay an interruption. A regression fixture ages Critical boredom, then crosses the Critical fatigue threshold with maximum Leisure personality/habit; it exposed the accidental delay and now verifies the original interruption. Atomic protection and urgency/commitment conditions are unchanged.

Base formulas and the ±.20 personality bound are unchanged. Habit adds at most .10, before the existing .15 repeat penalty. The .12 close-choice window, Fickle variation, per-actor Random, availability, category-local failure delay, commitments and atomic/short-safe phases remain unchanged. No active action is cancelled by the learning callback. Critical available hunger/fatigue continue to be selected deterministically by the original Choose branch.

## Diagnostics / Chao Editor

Existing debug output shows BaseScore, PersonalityAdjustment, HabitAdjustment, repeat penalty and final choice score, plus all three effective habits, last accepted category/time and last rejected duplicate/cooldown reason. Last accepted diagnostics can be derived from stored timestamps after load; transient rejection history is not persisted.

Existing Experience contains **Activity habits**, read-only: activity, effective bias, experience count, last credit and last update in active seconds. It is separate from personality/fixed favorites, item learning, garden places and social partners. No extra editor window or editable habit/personality fields are added. Unrelated patches preserve raw habit records exactly; the bounded ledger is rebound too.

## Automated results

Build: **0 warnings, 0 errors**. C#: **539 checks passed**, including 34 new habit checks and all previous suites. Python: **5 editor checks passed**.

Coverage includes empty/first/growth/saturation/half-life/interval/duplicates, category and pet independence, 1000 outcomes without innate mutation, invalid values/keys/schema/times, pure reading, canonical whole-garden save/load, legacy root/resident migration, future primary with readable backup, corruption overwrite prevention and editor patches. Coordinator checks verify neutral equivalence, exact .08 addition, isolated category effect, disabled categories, deterministic critical hunger/fatigue, strong boredom, unavailable favorite, protected atomic action, repeat penalty and seeded choices.

Controlled category statistics: identical neutral personality, needs/clock/availability/history, no repeat; only the tested habit differs. The three pre-habit scores are equal (~.165). A maximum habit raises its score to ~.265; alternatives remain within the unchanged .12 close window. **20000 independently seeded decisions per profile**, not one stateful activity stream:

| Profile | Play | Socialize | Leisure |
|---|---:|---:|---:|
| Neutral | 6670 (33.350%) | 6666 (33.330%) | 6664 (33.320%) |
| Play .10 | 8910 (44.550%) | 5546 (27.730%) | 5544 (27.720%) |
| Socialize .10 | 5548 (27.740%) | 8908 (44.540%) | 5544 (27.720%) |
| Leisure .10 | 5548 (27.740%) | 5547 (27.735%) | 8905 (44.525%) |

Synthetic maximum biases are controlled test inputs, not claimed naturally trained records. Every alternative remains possible. Physiological-priority tests separately use all available categories and maximum competing habits.

## Godot / regressions

`--activity-habits-check` reuses the existing real partner harness for introduction/music/ball and adds real play/leisure/rebind scenarios, rather than a second social simulator. `HABITS_SCENE_OK`, exit 0:

* Actual solo rattle gives one Play credit and its old item credit.
* Actual introduction gives no Socialize habit.
* Actual ensemble, including editor rebind during performance, gives one Socialize per pet and no Play; instrument learning/partner memories remain correct.
* Main and resident ten-pass games give one Socialize each, no Play, no per-pass habit and no additional SocialNeed reward.
* Cancelled ensemble, refused invitation, missing performer, ball timeout and cancelled acquisitions give no habit success.
* Three independent actual histories: main solo Play, a saved/restored real ten-pass Socialize learner, resident full sky/rise Leisure. No ID/model crossover.
* Full route through actual MoveFriend credits the replacement PetState after an unrelated editor patch; obsolete state receives no late credit. Place memory is retained.
* Cancelled route/toy do not change raw habit records. Save/load retains individual habits and their actual coordinator score contributions.

`ACTIVITY_SCENE_OK`, `LEARNED_SCENE_OK` (also explicitly checks actual solo-ball Play), `PLACE_SCENE_OK`, and full `SMOKE_OK`, exit 0, passed without stderr errors. Full regression includes gardens/movement/water, introductions/music/ball, personality/needs, all twenty slots and recovery, expeditions/discovery/greenhouse/wildlife, races and Karate. Existing assertions were retained; extra habit classification assertions were added. A first pure round-trip fixture needed full recursive canonical validation before its baseline, rather than comparing partially initialized child data; no production preservation assertion was weakened.

Regression investigation: after the interruption fix, GardenGame.CheckActivityScene's live-editor fixture once failed at its fixed eight-second checkpoint. Diagnostic capture showed Play selected, ChaoInterests busy in Thought, valid needs and no path/start failure. The test now observes the same pending rattle's actual start, bounded to 16 seconds and only while that Play remains busy/error-free; it still requires a real played prop, the exact replacement PlayState and increasing Fun. No assertion was disabled and production thought/animation timing was not changed. The final fresh run reported an actual start at 8.0 seconds and ACTIVITY_SCENE_OK.

The final dedicated run also explicitly sets a generated resident's Leisure bias to .10, begins an outing and picks it up using the existing Hold path: leisure stops and raw habit records do not change. This maximum fixture is not represented as naturally acquired experience.

Real four-pet recovery audit passed with canonical round-trip equality, frozen input unchanged, and editor roster exported. Before tests, **269 files** were copied with SHA-256/mtime manifests; every backup verifies. Before the final desktop review all 121 source save/slot files verified unchanged. At the last check, 119 still matched; pet.json and its backup in the older already-running partners-1.5B2-preview copy had been updated by that preview's normal autosave/shutdown. No test targeted that directory, and it was not restored/overwritten. The personal save directory remains unchanged. Tests used only isolated artifacts directories and temporary pure-test folders.

Final GUI review used the verified four-Chao copy `habits-1.5C1-preview`. Actual ChaoEditor.cmd showed Mori, White Chao, Black Chao and Yellow Chao; all thirteen innate traits, fixed favorites, earned item experience and outgoing relationship scores were compared with the audited reference and retained. Experience → Activity habits opened with all three read-only rows and the required columns. A fresh game launch after the final code build confirmed the normal garden and editor connection without errors. The user then explicitly requested the game be switched off: **the game was closed and Chao Editor left open**, superseding the earlier prompt's request to leave both open. Stage 1.5C2 was not started.

## Limits and explicit exclusions

Only positive category habits are implemented. Habit decay follows existing active pet time, not offline real-world time. The transient ledger is intentionally not a permanent archive of all completions. Last-credit diagnostics for identical persisted timestamps use a stable key tie-break after reload; behavior does not depend on that diagnostic tie.

Existing blocked paths/approach failures remain possible and teach nothing. No navigation, new waypoints, mountain climbing or jump changes were made. There are no daily schedules, time-of-day preferences, wake-up appointments, new toys/actions/animations, evolution/genetic changes, new emotions/relationships or expedition/stadium AI habits. **Stage 1.5C2 is not started.**
