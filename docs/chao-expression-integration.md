# Emotional expression integration — 1.6A

MoriGarden visual extension, not reconstructed Sonic Adventure 2 Battle formulas. No new emotion, activity, AI, animation skeleton, asset, sound, save field or schema migration. The existing ChaoMood remains the sole emotional state; ImportedChao owns temporary visual interpolation only.

## Audit and ownership map

Audited ChaoExpression, ChaoMood, ImportedChao/Presentation, EmotionOrb, ChaoDistress, ChaoPersonality, PetState.Personality/CurrentState, GardenGame/ChaoMemory, GardenEgg, diagnostics, editor bridge/Python UI, and the existing expression/animation tests. Read the personality 1.2, needs/mood 1.3, personality-selection 1.4B and daily-rhythm 1.5C2 documentation.

| Source | ChaoMood | ChaoExpression | PoseKind / owner | Eyes, head, body, orb |
| --- | --- | --- | --- | --- |
| Existing meters Joy/Energy/Fullness/SocialNeed | Snapshot background state | Existing nine-emotion mapping | Idle/Walk | New bounded face/head/body intensity; no orb write |
| FoodBite/MealFinished, PlayerContact, pleasant shared play, discovery, wake/frighten events | Existing ObserveEvent/React priorities/durations | Current mood after action guards | Existing action executor first | No new meter/reaction writes |
| Sleep / IslandNap / lying / Rising | Existing sleeping flag and wake reactions | Sleepy or action face | Sleep/Rest/Rise, ChaoRest/ChaoSettling/ChaoRising | Physical closure remains full, no ordinary blink; pose and wake timing untouched |
| ChaoTouch | Existing PlayerContact reaction | Explicit initial Neutral, then action smile | Existing petting lead-in/gesture | Same two-second lead-in, unchanged hands and heart event |
| ToyCar/FruitMeal/animal/music/ball | Existing start/completion mood events | Smile/upset flags and action face | Grip/Interaction/Thought; same executors | Same hand targets; no automatic mood head gestures on protected actions |
| Introductions | Existing contact reaction | Thought/idea/happy flags | Look/Think/Approach/Grip/Wait | Existing look, hand raise/shake/lower and event glyphs retain ownership |
| ChaoDistress, thrown/fallen, ball scolding/crying | Existing upset/fear/wake reactions | Protected squint/Annoyed/closed crying | Distress or physical transition | Physical eye shapes outrank background joy, original distress choreography retained |
| DiscoveryPresentation / temporary Express | Existing mood remains independent | Leased explicit expression | Free request or same-action-owned face | Lease ends into current mood, no stored stale pre-action emotion |
| Racing/Karate | Existing result reactions | Existing sports expression/flags | Existing sportMotionDriven/pose owners | Sports clips and early bypass unchanged |
| EmotionOrb.SetIdea/SetDistressed/SetUpset/meal flags | Independent event rules | Existing SmileEyes/HalfEyes flags | Event/action priority | Existing glyph geometry, duration, sound and inertia untouched |

Call-site search covered all Express, CloseEyes, SmileEyes, HalfEyes, MovePose, CryPose and orb SetIdea/SetDistressed/SetUpset uses. Main owners are ImportedChao's sleep/cloud/rise/crawl/water/air/think/receive/eat/instrument/cry/animal/sport methods; ChaoTouch, ChaoInterests, FruitReaction, ToyCar, ChaoRest/IslandNap, ChaoDistress, introductions/music/ball/leisure, discovery/expedition and GardenGame/GardenEgg. Exact generated search output is in the isolated local `artifacts/expression-callsite-audit.txt`; no caller or existing expression was deleted.

## Changed files

* `Scripts/ChaoExpression.cs`: pure central expression arbitration, visual-only coefficient helpers and diagnostic record. Existing mapping and legacy Resolve/Intensity/Response API retained for compatibility.
* `Scripts/ImportedChao.Presentation.cs`: Idle/Walk mood integration, lease ownership, intensity-driven rendered weights, smooth face/head/body parameters, protected visual masks, safe blinking and live model diagnostics.
* `Scripts/GardenGame.ChaoMemory.cs`: read-only ID→active-model diagnostic collection, alongside the existing BindPresentation loop.
* `Scripts/GardenGame.cs`: optional model snapshot passed into the editor and isolated test flag/entry point only.
* `Scripts/ChaoStateDiagnostics.cs`, `Scripts/GardenGame.NeedsDebug.cs`: existing developer panel displays real visual state.
* `Scripts/ChaoEditorBridge.cs`, `Tools/chao_editor.py`: optional actual-model diagnostics and live read-only Expression subsection.
* `Scripts/GardenGame.ExpressionChecks.cs`: isolated actual-model/gameplay test and viewport evidence.
* `Tests/ChaoExpressionIntegrationChecks.cs`, `Tests/Program.cs`, `Tests/test_chao_editor_stats.py`: pure and read-only editor checks.
* This document and generated evidence images.

No changes to ChaoMood/needs, Personality, daily rhythms, habit/preference learning, save format, movement, sports rules, navigation, jumping or game assets.

## Central priority

`ChaoExpressionLayer.Evaluate` is pure; ImportedChao.UpdatePresentation uses its decision each visual frame.

1. Physical closure/protective squint. Sleep/rest closure selects Sleepy; closed crying retains Sad with fully closed physical eyes. Water struggle squint selects Afraid; scolding squint selects Annoyed.
2. Existing action eyelid/smile flags and protected pose. Action-owned Express is permitted for the same protected pose, preserving crying and authored action faces.
3. Free-pose temporary Express lease.
4. Current ChaoMood.Snapshot for Idle and Walk.
5. Neutral.

Protected automatic-mood poses: Crawl, Water, Air, Rest, Sleep, Rise, Thought, Interaction, Grip, Distress; held models are also protected. Free-pose leases cannot override these. A request authored by a protected pose does not leak into free movement after that pose ends. Existing action smile/half-eye flags keep priority. Thought defaults to Curious, Grip/Interaction to Focused, other protected poses retain their action-controlled neutral/closed face.

No background Happy→heart conversion exists. The layer never calls EmotionOrb. Its event setters still author smile/upset flags through the old path, and automatic mood never changes a glyph.

## Visual intensity and personality

```
Happy / Delighted: multiplier = 1 + .12 * clamp(Energetic,-100,100)/100
Curious:           multiplier = 1 + .10 * clamp(Curious,-100,100)/100
other expressions: multiplier = 1
VisualIntensity = clamp(MoodIntensity * multiplier, 0, 1)
```

Ranges are .88…1.12 and .90…1.10 respectively. They apply to allowed free expressions (including free temporary requests), not to weakening necessary physical closure/action flags. Other eleven innate traits have no new visual formula. Legacy Response still supplies the prior facial interpolation speed from Initiative; this stage does not replace its timing. Legacy Intensity is retained for old callers/tests, but the presentation amplitude now uses the required VisualIntensity rather than the old Sociability/Initiative amplitude.

Mood intensity, valence, arousal, reactions, priorities/cooldowns, needs and sports stats are never written by this layer. The native test measures Happy .7 with Energetic +100/-100: rendered smile targets .784/.616, while both exact mood snapshots remain identical.

## Smoothness and protected geometry

Existing Ease uses `lerp(current,target,1-exp(-speed*dt))`. Happy, sadness, curiosity, fear, surprise and delight surface/microgesture weights ease towards their intensity-scaled targets. Lid coverage, sad slant, curious asymmetric lid opening and wide-eye amount use these weights. Happy→Sad crossfades rather than enabling two full shapes; Idle↔Walk reads the same current snapshot and does not reset face or blink timers.

The existing partial-lid shader and alpha-capable line materials provide graded transitions; no renderer or eye mesh is replaced. Original eye anchors remain unchanged. Physical eyesClosed forces full closure even immediately in RefreshEyes; protected squint is full. Residual free-mood smile/curiosity/sadness weights are masked when an incompatible protected action owns the face.

Head look targets retain existing Attend priorities, neck bounds and quaternion interpolation. No mood bias changes the target's yaw/pitch. Free curiosity contributes bounded roll, free sadness a small downward pitch, and existing idle looks have modest happy amplitude. Protected grips/actions do not inherit free head gestures. Only existing idle soft-body/paw/delight gestures are intensity-scaled; walking gait, CharacterBody3D position/velocity/collisions, jumps, swimming and sports are untouched.

## Blinking and state replacement

Each ImportedChao keeps its existing private Random, seeded on first binding from its stable ID, and independent timer/phase. No Random is created per frame. First binding clears only a pre-bind blink; ordinary PetState replacement does not reseed or reset clocks. Physical closure/squint suppresses ordinary blinking; release schedules a fresh private interval once. Emotional changes never reset the timer.

A native long run exposed `sin(pi)` yielding a tiny negative final-frame blink amount. Clamp to [0,1] fixes the cause; the strict bounded-value check remains. BlinkDelay and its existing Energy effect are unchanged.

The existing AdvanceChaoMemories→BindPresentation loop reads replacement PetState objects. The editor rebind test changes an unrelated Fullness value through the actual bridge, verifies the model references the replacement, exact presentation/blink deadlines persist, and the current runtime mood is retained. Evolution's new ImportedChao initializes through the same first-bind path.

## Diagnostics

Existing developer view and read-only Experience → Expression show DominantEmotion, EmotionIntensity, Valence/Arousal, actual CurrentExpression/Pose, Forced/Action/Mood/Neutral source, personality multiplier, target and smoothed visual intensity, BlinkAmount, protected flag and suppression reason. Game passes an optional ID-keyed snapshot from actual models; neither the bridge nor editor derives a pretend visual pose from saved data. Inactive models show unavailable. There is no diagnostic field added to PetState or its save schema, and no emotion sliders.

## Verification and evidence

* Build: .NET 8 / Godot 4.5.1 mono; zero errors/warnings.
* **613 C# checks passed** (579 previous + 34 expression checks), including nine mood mappings, both free poses, protected poses, explicit/action ownership, exact coefficient ranges and saturation, unchanged whole saved state/mood after 1000 reads and unchanged eleven other trait effects.
* **7 Python editor checks passed**, including live read-only Expression formatting, missing active models and patch exclusions.
* `--expression-integration-check`: EXPRESSION_INTEGRATION_OK, exit 0. Three actual ImportedChao; rendered .784/.616 smiles for identical .7 Happy; independent Sad; actual Wander movement with Happy on every walking frame; ObserveEvent changes to Annoyed without stopping movement; walking Sad; eased explicit Neutral and return to current Happy; independent blink histories; real rattle, actual full introduction/handshake, natural ChaoRest approach/sleep/normal wake/Rising; actual bridge rebind without resetting presentation/blink clocks; three actual garden actors with nonzero selections for 180 simulated seconds; save/load and no new schema.
* Full `--smoke-test`: SMOKE_OK, exit 0, including all ten existing face variants, original eye anchors, attention bounds/release, grips, standing/lying/crawl/water/sleep/throw/distress/evolution, garden water/movement, 20 save slots, expeditions/discovery/greenhouse/wildlife, races, Karate and strike contact. No old assertion was removed or weakened.
* Dedicated native DAILY_RHYTHM_SCENE_OK, NEEDS_SCENE_OK (`--needs-debug` enabled), HABITS_SCENE_OK/PARTNER_SCENE_OK, ACTIVITY_SCENE_OK, LEARNED_SCENE_OK, PLACE_SCENE_OK all passed with exit 0 and no stderr errors.

Fixture corrections: applying the new generated state's existing ApplyDevelopment is necessary before a manual visual-only loop; otherwise an old crawling pose is correctly protected. Canonical recursive stat initialization is required before full JSON invariance baselines because existing legacy getters initialize statistics. These fixes affected test preparation, not movement or personality production rules. The real negative blink endpoint was fixed by clamping; its strict range assertion remains.

### Actual viewport evidence

These are frames from the real generated test garden, not illustrations or replacement assets. Camera changes are test-only; the sleep image uses an overhead view so physical closure is visible without rotating the actor or changing its sleeping pose. Controlled visual fixtures hold the garden clock fixed; the autonomous stability part advances existing executors/needs with explicit test steps.

| State | Image |
| --- | --- |
| Neutral | [Neutral](images/expression-integration/Neutral.png) |
| Happy | [Happy](images/expression-integration/Happy.png) |
| Sad | [Sad](images/expression-integration/Sad.png) |
| Curious | [Curious](images/expression-integration/Curious.png) |
| Sleepy | [Sleepy](images/expression-integration/Sleepy.png) |
| Walking Happy | [Walking Happy](images/expression-integration/Walking-Happy.png) |
| Walking Sad | [Walking Sad](images/expression-integration/Walking-Sad.png) |
| Protected Sleep | [Protected Sleep](images/expression-integration/Protected-Sleep.png) |

## Safety and remaining limits

270 original files were copied and verified by SHA-256 before integration; tests used separate project artifacts directories. The real four-pet canonical recovery audit passed without modifying its input. Before desktop launch, all 122 original save/slot files and all 270 backup copies verified unchanged. Engine logs alone rotate in the old default log directory.

No facial morph targets or mouth rig were added. Weak happiness blends existing ^ lines with the original eye surface; sadness uses existing lid/slant/head mechanisms. Automatic mood is intentionally absent in Crawl and other protected poses; low-Run pets may therefore show the crawling action's face until they stand. This is the requested ownership constraint, not a new reason to alter Run or movement. Learned preferences, activity habits and daily rhythm remain separate and unchanged. No later stage was started.

## Final desktop review — 27 November 2026

Launched ordinary MoriGarden on `artifacts/expression-1.6A-08f7c424/expression-1.6A-preview`, copied from the verified four-pet audit. Launched the actual **ChaoEditor.cmd**. Visually confirmed Mori, White Chao, Black Chao and Yellow Chao; current sports values, thirteen innate traits and item preferences; and the live Experience → Expression pane. The live copied Mori was actually in Walk with Sad from Mood (intensity about .56), while water/crawl models correctly reported protected action owners. No sports values were increased to make a pet walk for this review.

The new pane fits the existing window and updates actual expression/pose/source/weights rather than inventing a pose from saved data. Normal game rendered without errors. Both game and editor remain open, with the editor on Expression for Mori.

Compared the copied saved roster with the frozen canonical audit: all four IDs/names, Statistics, thirteen Personality values, ActivityHabits, LearnedPreferences, fixed favorites and directed SocialLinks retained. All 122 original save/slot files still match pre-test hashes; all 270 backup copies verify. Ordinary play may naturally advance meters/history inside the preview copy only. No original save was overwritten, no replacement Mori was generated, and no migration was needed. Result awaits user approval; no later stage was begun.
