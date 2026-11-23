# Chao needs and mood — stage 1.3

Implemented 2026-11-23. This is a current-state model and event integration, **not a new action planner**. The thirteen innate personality scales are neither read for new behavior formulas nor changed by this system. These rules are MoriGarden design, not reconstructed SA2B formulas.

## Audit of the existing meters

All rates below are per active-play second, not wall time. GardenSky has a 600-second cycle (410-second day arc, 190-second night arc). Existing minimum bed sleep is 35 seconds; instruments play for 60 seconds, ordinary toys usually four seconds, and social encounters take several seconds of actual choreography.

| Canonical field | Range / initial | Existing continuous rule | Actual writers, besides initialization/editor/validation | Important readers and effect |
|---|---|---|---|---|
| Fullness | 0..100 / 72 | Tick: -0.035/s, including sleep | Feed +24; FeedBite +6; both capped at 100 | FruitMeal chew/pause/rejection, FruitReaction begging, ChaoInterests food/tree scores, music/expedition eligibility, UI; determines hunger and feeding |
| Energy | 0..100 / 90 | Tick awake: -0.045/(1+StaminaAbility*0.002)/s; asleep: +5/s | CompleteRace -10; SportsProgress Karate -2 for withdrawal or -8 otherwise; ExpeditionCompanion lying rest +0.15/s | Garden/Island rest, expedition rest/sleep/Call/decisions, discovery/perception, social/game eligibility, voice/blink, sports efficiency, UI |
| Fun | 0..100 / 65 | Tick awake: -0.07/s; no sleep drain | ToyCar.BeginPlay +0.5; Advance +0.65/s for instruments or +9/s other toys, limited to actual play duration; solo ball +4/s, shared pass +3 for each pet | ChaoInterests scores, ToyCar availability, ball/music invitations; determines desire for entertainment |
| Joy | 0..100 / 65 | Tick: -0.018/s, including sleep | Pet +12; TrainWithSpecies +4; CompleteRace +2*score; favorite fruit gift +4; accepted player toy +6; declines -3/-5/-12; interrupted food petting/withdrawn offers/throws -4; hard landings -5; rock bumps/rejected interactions -8 | FruitMeal displeasure, ball refusal, discovery requests, vocal reactions, UI; existing broad pleasantness meter |

Audit sources: `PetState.cs`, `PetState.Statistics.cs`, `ChaoRest`, `IslandNap`, `ChaoLeisure`, `ChaoDistress`, `ChaoInterests`, `ChaoMotion`, `FruitMeal`, `FruitReaction`, `ChaoRising/ChaoTouch`, `ToyCar`, `ChaoBallGame`, `ChaoMusicGroup`, `ChaoIntroductions`, `ChaoMemory/ChaoExperience`, `DiscoverySystem/DiscoveryDecisions`, `ExpeditionCompanion/ExpeditionTeam/ExpeditionRoster`, `ChaoRace`, `ChaoKarate`, `SportsProgress`, `GardenEgg`, `GardenGame`, `ChaoExpression`, `EmotionOrb`, `ImportedChao.Presentation` and `ChaoEditorBridge`. Production meter writes and their callers were inspected; check/review fixtures are not gameplay rules.

**These four meters retain their names, JSON meaning and numerical formulas.** An emotional event never applies the feeding, sleep, toy or Joy effect a second time. The existing algorithms still read the original meters and use the original eligibility thresholds, movement and activity durations.

No current loneliness/social-need meter existed. Sociability is a tendency, SocialContact is a choreography timer, SocialLinks/CharacterRelationships are relationships, and memory confidence is experience; none is repurposed as a current social need.

## Model and persistence

- `PetState.Needs` is one cached, runtime-only `ChaoNeeds` view bound to that pet. Hunger = 100 - Fullness; Fatigue = 100 - Energy; Boredom = 100 - Fun. Views clamp to 0..100 and reject nonfinite meters. There is no independently editable stored hunger/fatigue/boredom.
- `PetState.SocialNeed` is the only new persisted scalar, default **20**, finite 0..100. Invalid values fail the entire save validation; no resident is removed to continue loading.
- `PetState.Mood` is one cached, runtime-only `ChaoMood`, bound to that pet. Snapshot: Valence [-1,1], Arousal [0,1], DominantEmotion, EmotionIntensity [0,1].
- Needs/Mood, reaction queues, emotion clocks, solitude timers, urgency ages and hysteresis caches are `[JsonIgnore]`/private. They do not grow saved JSON. Loading rebuilds baseline emotion from current meters without fabricating a past reaction or wake event.
- Each pet, including each `GardenEggs[].Chao`, owns separate runtime state. IDs, roster, sports progress, memory, relationships and personality are unchanged by migration.
- Editor replacement keeps copies of the selected pet's runtime classification/reactions, **rebound to the edited PetState**. It does not retain views pointing at obsolete meter values. Save snapshots intentionally do not persist those transient caches.

## Need urgency and hysteresis

Fresh classification: Low [0,25), Moderate [25,50), High [50,75), Critical [75,100]. Fractional meters use the same continuous boundaries.

Entry boundaries are 25, 50 and 75. Exit requires strictly less than 20, 45 and 70 respectively. Large decreases can cross several levels. Actual values are never changed to satisfy hysteresis. For example, 74→75→72→69 is High→Critical→Critical→High. Runtime classifications are rebuilt on load, so a loaded value of 72 initially classifies as High even if the previous session held Critical.

`ChaoNeeds.EvaluateNeeds(pet, context)` returns four `NeedPriority` records: identity, actual value, stable level, weight, interruption permission, time in the same level and English debug reason. Read-only evaluation does not age states. Advancing current state increments active duration; a level change resets it.

Exact normalized weight:

```
essential = Hunger or Fatigue
weight = clamp(value/100 * (essential ? 0.92 : 0.76)
             + (level == Critical ? 0.06 : 0)
             + min(0.06, secondsAtSameLevel/6000), 0, 1)
```

The age bonus caps after six minutes. A very high optional need can outweigh a modest essential need, so there is no fixed global ordering. High/Critical Hunger/Fatigue can recommend interruption **only when the caller explicitly supplies LongOptional context**. ShortSafe/None context never grants immediate interruption. Boredom/SocialNeed do not cancel actions. **No planner consumes these scores in stage 1.3.** Stage 1.4A subsequently connects a thin garden-only category coordinator; the original 1.3 behavior above is recorded here for context.

## SocialNeed

Awake: +0.02/s, or 12 points per ten-minute garden day. Sleeping: +0.001/s, or 0.6 points per day. After 1,200 awake seconds without a positive contact, the awake rate gains +0.015/s. The extra rate is integrated only for the portion of a tick beyond that threshold; small and large timesteps agree. Sleep does not advance the awake-solitude clock. All results clamp to 0..100.

Accepted positive player contact subtracts **6**. A successful social event subtracts **8** for each actual participant. Those contacts reset the awake-solitude clock. Since stage 1.4A these physical effects are deduplicated by completed-contact identity, independently of emotion cooldown; a new actual contact still relieves the need even if its emotional overlay is suppressed. Legacy unstamped callers receive frame-level deduplication. See `chao-action-selection.md`. Sociability does not modify the rate or change meaning.

The starting value reaches Moderate in about 4m10s without contact. Prolonged solitude adds pressure only after two garden days; the meter reaches 65 after three awake days. Several seconds of real positive social contact can substantially relieve it. This intentionally creates a slow current need rather than urgency every few seconds. This stage does not make pets seek company according to the new meter.

## Emotional event rules

| Existing event / reliable hook | Emotion | Intensity | Duration | Priority | Social relief |
|---|---|---:|---:|---:|---:|
| FeedBite, nonfinal | Happy | 0.35 | 3s | 30 | 0 |
| Feed or final FeedBite | Happy | 0.60 | 8s | 40 | 0 |
| ToyCar.BeginPlay | Happy | 0.45 | 6s | 35 | 0 |
| Natural toy completion; tenth shared ball pass | Excited | 0.65 | 8s | 45 | 0 |
| Positive ChangeRelationship with player | Happy | 0.55 | 6s | 40 | 6 |
| Successful introduction/shared rest/study, shared ball pass, ensemble join, actual expedition social walk | Happy | 0.60 | 8s | 42 | 8 per participant |
| Negative ChangeRelationship; hard landing or rock bump | Frustrated | 0.65 | 8s | 75 | 0 |
| DiscoverySystem starts a visible/available research session | Curious | 0.55 | 6s | 35 | 0 |
| ChaoExperience.Study completes | Happy | 0.60 | 8s | 40 | 0 |
| ChaoDistress.PickUp interrupts actual sleep | Frustrated | 0.70 | 60s | 80 | 0 |
| First physical throw-impact bounce | Afraid | 0.80 | 3s | 95 | 0 |
| Actual sleep→awake transition | Happy | 0.25 | 4s | 20 | 0 |
| Ball invitation declined | Sad | 0.75 if scolded, otherwise 0.35 | 8s / 4s | 70 | 0 |

Player feeding, petting, animal gifts and unpleasant acts already call the shared relationship method; they use that reliable contact event. Self-fed food emits eating happiness without inventing a player interaction. Rejected taste/fullness attempts never emit FoodBite because no bite was consumed. Joining music and shared play do not add Fun again. Interrupted activities do not receive a fabricated successful-completion event.

An event has type, normalized intensity, duration, stable source, priority and active-clock expiration. Maximum eight active reactions and 32 cooldown sources per pet. Same-source events cannot extend an active expiration and cannot retrigger until **expiration + 8s**. At capacity, only an event at least as strong in priority/intensity replaces a weakest active event. Expired entries are removed; lower events are not held in a backlog waiting to start.

Winner: highest priority, then highest intensity, then earliest expiration. Lower active reactions remain background. After an overlay ends, an unexpired background reaction can resume; after all expire the **current** meter-derived state is shown. Actual sleep always produces Sleepy. Awake Energy<=10 gives baseline Sleepy priority 85, suppressing weak positive reactions; brief fear priority95 can still overlay it. Fullness<=10 while Energy>10 gives baseline Frustrated priority65. Other baselines: Energy<=25 → Sleepy priority25; SocialNeed>=75 → Lonely priority15; Joy<25 → Sad priority10; Joy>=80 → Happy priority5; otherwise Neutral. Critical hunger takes precedence over ordinary drowsiness; critical fatigue takes precedence over hunger through the Energy>10 condition.

Baseline intensities: Happy=(Joy-65)/100; Sad=0.35+(25-Joy)/100; Lonely=0.35+(SocialNeed-75)/100; Sleepy=0.4+(25-Energy)/100; critical-hunger Frustrated=0.5; actual sleep=1; Neutral=0. Final intensity is clamped to [0,1].

Valence starts at `clamp((Joy-50)/50,-1,1)` and blends with the dominant emotion by its intensity. Emotion valence: Happy +0.6, Excited +0.85, Curious +0.25, Sad -0.65, Lonely -0.45, Frustrated -0.7, Afraid -0.9, Sleepy -0.1. Arousal: Excited .9, Afraid .95, Frustrated .75, Curious .6, Sleepy .1; otherwise `clamp(.15+.55*Energy/100+.3*Joy/100,0,1)`. These values describe mood; they do not change game speed or action probabilities.

## Timing and presentation

Ordinary `PetState.Tick` retains its four old meter calculations and advances the new runtime state once. Existing sleep flags drive sleep/wake transitions. Where existing resident logic intentionally skips Tick (held, in flight, reserved for an offer or pairing), only the new runtime state advances: the old Fullness/Energy/Fun/Joy timing is preserved. Pausing game simulation pauses emotion time; offline time does not age reactions.

Current mood supplies a facial **fallback only for free idle poses with no active explicit expression request**. Existing sleep, distress, happy-orb and squint flags, thought/interaction/grip poses and sport animations continue to arbitrate through the existing renderer. Internal Excited maps to Delighted; Lonely to Sad; Frustrated to Annoyed. An explicit Neutral request also remains authoritative; the original two-second neutral lead-in to petting is preserved and tested. Existing Surprised/Focused and original model geometry are retained. EmotionOrb's existing shape/sound choreography is not replaced or triggered by every mood update.

## Developer view and editor

Explicit `--needs-debug` enables a selectable, scrollable developer panel showing the four original meters, derived needs, SocialNeed, dominant emotion/intensity, valence/arousal, levels, weights, duration, long-optional interruption recommendations and active timed reactions. No diagnostic panel or extra notifications appear in ordinary play.

Chao Editor retains its UI; SocialNeed is an ordinary current-state field under Care. The IPC snapshot adds `currentState` per stable pet ID with the full diagnostic view. Changing Fullness/Energy/Joy does not change any personality parameter. The view is also available programmatically through `ChaoStateDiagnostics.Capture/Describe`.

## Verification and save safety

Tests use private temporary data. Both known Godot data directories were backed up before tests, preserving relative names, hashes and original file mtimes. The personal root save is copied before migration; no development migration or test save writes to the active personal path.

`NeedsMoodChecks` covers requested A–L scenarios, all twelve hysteresis boundary/direction combinations, exact old rates, social timing/integration, contextual interruptions, event overlays/expiry/bounds, real-value restoration, 1,000 personality-invariance cycles, three-pet independence, legacy/nested persistence, editor runtime rebinding and invalid-data protection. Existing suites remain enabled.

`--needs-check --needs-debug` is a separate generated Godot scene check, run with a fresh `MORI_TEST_DIRECTORY` under ignored project artifacts. It exercises actual toy, eating-method, social/research and disturbed-sleep hooks, original animation presentation, a three-pet diagnostic panel and save/load; it captures `needs-diagnostic.png` and exits. The full existing graphical smoke run exercises the physical feeding/sleep/play/social/garden/expedition/animal/expression/save/editor flows.

## Deferred integration points for stage 1.4

- Read `NeedEvaluation` when scoring existing activities, using actual action interruptibility.
- Use SocialNeed to influence social appeal without conflating it with Sociability.
- Connect explicit proximity/contact context if long physical solitude must differ from time without positive contact.
- Consider continuous relief during sustained successful interactions instead of bounded discrete events.
- Consider personality-based expression modulation separately, with evidence/design labels; no new Energetic/Solitude formulas are active now.
- Decide whether urgency history must persist across sessions; currently only actual meters persist.

No new games, missions, habits, actions, animals, personality changes or stage-1.4 planner are implemented.

## Results and changed files — 2026-11-23

- Build: zero warnings/errors. C# suite: **379 checks passed**. Python editor suite: **5 passed**.
- Graphical checks: `NEEDS_SCENE_OK` (including the preserved neutral petting lead-in), followed by a complete successful `SMOKE_OK` on the final gameplay code. No stderr errors. The developer panel screenshot was visually inspected; longer reaction lists are intentionally scrollable.
- Actual personal Mori-copy audit and scene: `RECOVERY_AUDIT_OK` and `RECOVERY_SCENE_OK`, four original identities present, all thirteen innate values unchanged. The most recent previous preview also passed an independent complete round-trip, so final inspection can continue that progress in a new copy.
- **267 archived files** verified with SHA-256 and original mtimes; **121 pre-existing pet/slot files** (including the previous preview) unchanged at their original paths. Personal files and test snapshots are excluded from Git.
- Remaining limits: new priority scores and SocialNeed do not choose actions yet; solitude currently means time without positive contact, not a proximity simulation; urgency history and emotional overlays reset on loading while persistent meters remain. Existing action-specific expression/orb choreography remains authoritative.

Created: `Scripts/ChaoNeeds.cs`, `Scripts/ChaoMood.cs`, `Scripts/PetState.CurrentState.cs`, `Scripts/ChaoStateDiagnostics.cs`, `Scripts/GardenGame.NeedsDebug.cs`, `Scripts/GardenGame.NeedsChecks.cs`, `Tests/NeedsMoodChecks.cs`, this document.

Updated: `Scripts/PetState.cs`, `Scripts/ChaoEditorBridge.cs`, `Scripts/ChaoExperience.cs`, `Scripts/ChaoExpression.cs`, `Scripts/ChaoDistress.cs`, `Scripts/ChaoRising.cs`, `Scripts/ChaoBallGame.cs`, `Scripts/DiscoverySystem.cs`, `Scripts/ExpeditionTeam.cs`, `Scripts/ToyCar.cs`, `Scripts/GardenEgg.cs`, `Scripts/GardenGame.cs`, `Scripts/ImportedChao.Presentation.cs`, `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `Tools/chao_editor.py`.
