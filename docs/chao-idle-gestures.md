# Individual Idle gestures — stage 1.6B

These visual coefficients are MoriGarden design interpretations, not established effects/formulas from Sonic Adventure 2 Battle. This stage adds only decorative Idle variation; no activities, emotions, AI, navigation, assets, sounds, sports rules or save migration.

## Audit and architecture

Audited current ImportedChao/Presentation, ChaoExpression/Mood/Personality, PetState.Personality, GardenGame/ChaoMemory, GardenEgg, EmotionOrb, ChaoDistress/Motion, diagnostics, editor bridge/UI, the 1.6A tests and `chao-expression-integration.md`. Call-site search covers Pose, MovePose, Attend, Express and BindPresentation; its local inventory is `artifacts/idle-gesture-callsite-audit.txt`.

The existing presentation already owns PoseKind, poseSerial/lastPresentationPose, base head attention, limb blending, emotional microgestures, softBody and per-model blinking. New code does not replace those systems.

```
existing stable ID -> one separate gesture RNG/controller per ImportedChao
presentationClock + current pose/ownership + current personality/mood
  -> ChaoIdleGestureController: eligibility, scheduled attempt, type, duration, bounded offsets
  -> existing UpdateHead: additive free-look variation after its base interpolation
  -> final visual composition after the old sole alignment: paws and upper body
  -> existing real pose/executor immediately removes the decorative layer
```

Pure controller references no Godot, PetState, game event, learned preference, activity executor or world clock. It receives read-only personality/mood inputs and the model's current presentation time. The runtime controller/seed/gesture are never saved.

## Files

* `Scripts/ChaoIdleGestureController.cs`: pure scheduling, probabilities, curves, eligibility, diagnostics, private random state.
* `Scripts/ImportedChao.IdleGestures.cs`: reversible local transform composition, feet anchoring, actual-model diagnostics and ownership guards.
* `Scripts/ImportedChao.Presentation.cs`: first-ID initialization, Pose cancellation, controller update and same UpdateHead integration.
* `Scripts/ImportedChao.cs`: restore before existing physics work; apply final decorative layer after existing sole alignment; sport bypass clears it.
* `Scripts/ImportedChao.SportMotion.cs`: restore the decorative layer before capturing/using the existing sports rig. No clip value, timing, collision, movement or result formula changes.
* `Scripts/GardenGame.cs`, `Scripts/GardenGame.ChaoMemory.cs`: isolated test entry and explicit pause propagation through the existing update; no new Process node.
* `Scripts/ChaoExpression.cs`, `Scripts/ChaoStateDiagnostics.cs`, `Scripts/ChaoEditorBridge.cs`: optional nested actual-model gesture diagnostics, no PetState fields.
* `Tools/chao_editor.py`: read-only Idle gestures subsection beside Expression.
* `Tests/ChaoIdleGestureChecks.cs`, `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `Tests/test_chao_editor_stats.py`: pure/statistical/editor checks.
* `Scripts/GardenGame.IdleGestureChecks.cs`: actual-model/transform/gameplay/FPS tests and viewport evidence.
* This document and evidence images listed below.

## Gestures and personality

For each named trait `T=clamp(value/100,-1,1)`, `P=max(0,T)`.

| Component | Attempt probability | Maximum angular parameter |
| --- | --- | --- |
| LookAround / Curious | .10 + .25P | additional yaw .12P, pitch .04P rad |
| PawFidget / Energetic | .08 + .22P | .04 + .06P rad, at most .10 |
| GentleSway / Carefree | .10 + .20P | .025 + .020P rad, at most .045 |
| Static free posture / Solitude | no extra gesture or chance | inward paw adjustment .04P rad |

Each attempt makes one categorical draw using these probability masses; the remainder is None. Ineligible LookAround (real attention target) or PawFidget (Sleepy) has zero mass without redistributing its chance. Alternatives remain possible. If a third identical started gesture would occur and alternatives exist, weighted alternatives replace it. Interrupted starts also count in this temporary two-entry history.

Only one primary gesture runs. First eligible attempt is after 2–4 seconds of uninterrupted Idle; a None attempt waits 6–10 seconds. Gesture duration is 1–2.5 seconds, with at least 6–10 seconds after a natural finish before the next attempt. Timing is scheduled at exact presentation timestamps rather than incrementing angles per frame. No action is held to wait for a gesture.

`envelope = sin(pi * clamp(elapsed/duration,0,1))²`, explicitly zero at both endpoints. LookAround adds a signed smooth yaw/pitch variation to the existing free-look target. Neutral/negative Curious has zero added yaw/pitch, preserving the original ordinary head-look system during its nominal LookAround interval. PawFidget gently alternates the paws; the second paw uses .65 of the peak. GentleSway uses a 3.5–5-second sinusoid multiplied by the finite gesture envelope, so a short fragment also returns completely to zero. There is no second bounce.

Solitude is a static component, not a fourth gesture or social behavior. It eases towards .04P after the Idle warm-up; the first eligible partial frame is integrated only for its actual time past the two-second boundary, avoiding frame-rate-dependent onset. It never changes Sociability, SocialNeed, relationships or another actor. Negative values add no styling. The other nine innate parameters add no gesture formula; 1.6A expression and existing activity influences remain intact.

## Eligibility and ownership

New offsets require Idle, a bound model/personality, no held state, action/physical/sport ownership, post-action transition or actor movement. All other poses (Walk, Crawl, Water, Air, Rest, Sleep, Rise, Thought, Interaction, Grip, Distress) cancel the layer. Existing action/forced expression owners and active Eat/Play/Sleep/Socialize also block it even if an executor temporarily labels its pose Idle. Sleep's real mood flag and the first 1.2 seconds of the existing Delighted gesture are protected.

Afraid and Frustrated suppress it. Sad >= .60 is marked Strong sadness; weaker Sad also keeps only the original pose/expression, following the task's explicit weak-sadness rule. PawFidget is unavailable during awake Sleepy; actual sleep is completely protected. An actual Attend target has sole control of the head and cancels a running LookAround. No fake target, detection-radius change or research memory is created.

Priority stays physical/sports → action → authored interaction → 1.6A emotion → new decorative offset. Real actions immediately remove offsets; return to Idle starts a new warm-up, never resumes an interrupted gesture. Static posture eases while eligible but yields immediately to a protected author, rather than leaving a residual hand angle in a grip.

## Composition without accumulation

At Pose entry and before the existing model physics work, the previous layer is removed. Each touched node stores its current **base Transform, applied Transform and authored Scale**. Restore happens only while the node still matches the layer's applied transform; a newer real author's write is preserved. New offsets compose with the frame's true base, never last frame's decorated rotation. Head variation is applied inside UpdateHead after the original interpolation; its base `previousHead` state remains free of the new offset. Limb blending caches the original base before final composition.

Paw offsets apply to current local baselines. GentleSway rotates softBody only; both feet's exact world transforms are retained and compensated after this visual parent rotation. This runs **after** existing sole alignment, so new sway cannot cause sole correction to move the actor or slide its feet. The CharacterBody3D and its velocity/collision are never written.

A long test exposed small scale round-off when restoring a raw matrix through Godot's lazy decomposition. Explicit restoration of the captured authored Scale prevents that from building up. The native counterfactual compares a normal layer with a zero-weight layer at the same location over 10000 frames without any new Pose write, including limbs, head and body. The old blink stream is compared too. Sports rig preparation restores this same layer before taking its home transforms; existing clips cannot capture a transient paw angle.

`IdleGestureWeight=0` is an internal native-test comparison switch, not an editor/user/save setting; it skips all new transform writes. The baseline therefore remains the prior presentation path.

## Randomness, time, pauses, rebinds

A separate private Random is created once from the stable ID, using FNV-1a uint32 over explicit UTF-16 low/high bytes of `MoriGarden/idle/v1/` + ID. It never consumes the old `microRandom` used by blinking and ordinary free looks. No display name, system time, GD.Randf, Random.Shared or per-frame Random is used. Same ID, seed and inputs reproduce the sequence; different actors own separate state.

Only existing presentationClock is used. The existing game update explicitly signals its pause/team-selection/transition/stadium-blocking state to current models. The new controller freezes offsets and elapsed time during pauses, then shifts its local scheduled timestamps by the paused interval, preventing catch-up jumps. A higher-priority physical state still clears the layer while paused. This does not retime existing 1.6A presentation or game clocks.

BindPresentation retains the controller across a PetState replacement and reads the current object's traits on subsequent updates. Active gesture parameters remain stable until completion; current free posture responds smoothly to its target. A new evolved model gets a new first-ID controller. Save/load may restart plain Idle after the normal warm-up; unfinished gestures are deliberately not serialized.

## Statistical results

**1000 independent 90-second eligible Idle intervals per profile** with matching controlled seeds; no continuous lucky observation is substituted. All other traits/mood/conditions are equal. This includes an extra 1000 Solitary intervals, though only its posture is expected to differ. Pure checks additionally show its probabilities are exactly neutral. Every permitted type still occurs; no third identical start, cooldown violation or duration violation was accepted.

| Profile | LookAround | PawFidget | GentleSway | Minimum finish→start gap | Mean gap |
| --- | ---: | ---: | ---: | ---: | ---: |
| Curious | 3014 | 1065 | 1326 | 6.0010s | 13.83s |
| Energetic | 1224 | 2686 | 1245 | 6.0010s | 14.31s |
| Carefree | 1234 | 1033 | 2704 | 6.0052s | 14.71s |
| Solitary | 1066 | 853 | 1079 | 6.0050s | 20.32s |

Observed duration range across scenarios: about 1.00024…2.499995 seconds. Mean gap includes skipped None attempts; the authored minimum remains six seconds. These are controlled visual intervals, not claims that busy real garden pets must idle or perform a gesture this often.

## Diagnostics

The existing developer view and Experience → Idle gestures read actual controller data: type, elapsed/duration, time to next eligible attempt, current envelope-scaled angular amplitude, Curious/Energetic/Carefree probability bonuses, current Solitude adjustment, pose, suppression reason and counters. No active model/controller gives **Unavailable**. No sliders or saved diagnostic fields are added. Runtime completion counters are not gameplay experience or habit credit.

## Verification and evidence

Build: `dotnet build Mori.csproj --no-restore -v quiet` succeeds without warnings/errors. `dotnet run --project Tests/Mori.Tests.csproj`: **663 checks pass**. Python editor suite: **8 tests pass**.

The real Godot `--idle-gesture-check` passes: four independent models over 180 controlled seconds, followed by 180 seconds through the actual autonomous garden physics loop. It checks real Play interruption/grip, the existing introduction/handshake, natural ChaoRest approach → sleep → Rising, real attention overriding decorative scanning, actual game pause dispatch, editor PetState replacement, and save/load without gesture fields. At least three actors complete ordinary autonomous actions; all four continue selecting activities.

10,000 consecutive frames compared against the same original visual path with only the new layer disabled give maximum matrix drift **1.79e−7**. Original blink values remain identical. Maximum sole displacement during body sway is **3.93e−6 m**. Actual model comparisons at 30/60/120 FPS retain gesture sequences, with maximum sampled offset difference **0.000723 rad**. Sports-rig handover matches the zero-layer reference, including original authored scale. Pure checks additionally cover probability distributions, anti-repetition, emotion/pose/action guards and absence of gameplay-state mutation.

Existing native regression modes all pass: `--smoke-test`, `--expression-integration-check`, `--daily-rhythm-check`, `--needs-check --needs-debug`, `--activity-habits-check`, `--activity-check`, `--learned-check`, and `--place-preferences-check`. These include the existing sleep, social, item, garden, expedition, race and Karate checks. No existing check was disabled.

Real viewport evidence: [Neutral Idle](images/idle-gestures/Neutral-Idle.png), [Curious LookAround](images/idle-gestures/Curious-LookAround.png), [Energetic PawFidget](images/idle-gestures/Energetic-PawFidget.png), [Carefree GentleSway](images/idle-gestures/Carefree-GentleSway.png), [Solitude posture](images/idle-gestures/Solitude-Posture.png), [cancellation](images/idle-gestures/Gesture-Cancellation.png), [protected interaction](images/idle-gestures/Protected-Interaction.png). These are isolated real-model scenario captures, not illustrations or forced personality changes to the user's pets.

Final desktop review on 2026-11-27: launched an ordinary garden from a separately audited safe copy and launched the actual `ChaoEditor.cmd`. Mori, White Chao, Black Chao and Yellow Chao are present with their original stable IDs, all 13 personality values, sports data, fixed favorites, learned preferences/habits, existing memory entries and relationships intact. Normal active time still advances. The read-only Experience → Idle gestures pane displays live actual-model data; Mori's Thought, White/Yellow Water and Black's Sleep correctly suppress decoration. Game and editor are left open on this separate profile. All **269 backup files** verify against their hashes; all **121 original save/slot files** remain unchanged. Test saves and logs stay in ignored `artifacts/`.

## Limits

Only eligible Idle receives gestures. Busy, crawling, swimming, sleeping, distressed or explicitly attending pets can legitimately show None/Suppressed for a long time. Neutral personalities retain rare baseline gestures. Solitude eases at 6/s only after the Idle warm-up; it is posture, not a new activity. Still frames show a moment of subtle movement; they do not replace the timed sequence/continuity checks. Existing navigation and animations were reused, not redesigned. Stage 1.6C is not started.
