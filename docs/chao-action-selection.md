# Garden activity selection — stage 1.4A

## Audit before implementation — 2026-11-23

The actual main update is `GardenGame.MoveFriend`, not a method named UpdateMori. Residents use the separate `GardenEgg.AdvanceChao`. They do not share an identical action order.

World scope: the current catalog has two permanent gardens, Mori Garden and Coastal Cove. Residents in GardenEggs are spawned in Mori Garden only; a carried main pet can reside in Coastal Cove. The plaza/stadium are not gardens. ExpeditionCompanion and ExpeditionDecisions own expedition pets and are outside this change.

### Actual old order

- Physics frame: advance toys → music-group acquisition/update → ball acquisition/update → watering can → player petting/pickup → residents → player offering → MoveFriend → expedition update → introductions acquisition/update → courtship → fruit growth/contact/memory.
- Mori: protected flight/offer/rise/touch/carry/distress → existing toy pose → recovery → sleep → action pause/courtship → cliff → watering → pillar → mixed food/toy interests → fruit request → island nap → pillar acquisition → requested bed rest → leisure → call/swim/cliff/wander movement. A periodic fallback counter also requests sleep (Energy<45, or every fifth fallback with Energy<62) and swimming (every fourth fallback).
- Residents: reservation/current needs update → distress/rise/touch/toy/recovery → sleep → action pause/courtship → cliff/watering/pillar → mixed food/toy interests → fruit request/call → island/pillar → requested bed rest → Energy<58 rest request after sleepDelay → leisure → wander movement. Residents have independent components; social-used residents receive Tick without running their normal mover.
- Coastal Cove: main pet only. Old MoveFriend still provides basic wandering/calls and protected reactions, but Garden-only food/toys/island/leisure/bed acquisition do not exist there. No resident spawning or new garden is implied by this task.

| Category / existing executor | Start and availability before change | Completion / cancellation | Animation ownership / need effect | Old check interval |
|---|---|---|---|---|
| Eat / ChaoInterests → FruitMeal | Loose usable food in 8m (12m favorite), fullness<45 unless existing FindsFood allows <76; usable mature tree within 7m, PowerAbility>=100 and fullness<72. Feeder stock excluded unless single-use. Mixed against toy scores. No explicit route probe. | Approach timeout 24s (54 low Run); meal until consumed/rejected; Cancel releases food and clears thought/orb. Bite is atomic. | Interest/Meal drive approach, thought, shaking, eating; existing FeedBite raises Fullness. | Idle delay 5s initially, 6s scans, 5/11s after finish |
| Play / ChaoInterests → ToyCar, ChaoBallGame | ToyCar.Available, free compatible toy, Fun<100; chosen in the same mixed food/toy loop. Ball partner selection is owned by ball sequence. | Toy/ball duration; CancelPlay/ball Cancel restore prop and poses. Active grip/play is protected. | Existing toy hands, music, ball poses; Fun relief. | Interest scans; music scan 3s; ball begins when a ball is already being played |
| Sleep / ChaoRest | A free slot among four bed positions; old triggers above differ between main and residents. Request reserves a slot. | 35s approach timeout, existing hop/settle then minimum 35s sleep and Energy>=100; Cancel releases slot. Settling/sleep/rise protected. | ChaoRest/ChaoSettling/SleepPose/Rising; Tick restores Energy. | Main fallback counter; residents delay then Energy<58 |
| Socialize / ChaoIntroductions | Two ready, visible, not-introduced pets within 1.95m; floor/height/LOS/player-action restrictions. No relationship is created by availability. | Look/think/alert/approach/talk/raise/shake/lower/wait; Cancel resets both poses; successful handshake marks introduction. | Existing handshake/voices/eyes; positive social event. | .45s scan when idle, 2.5s after cancellation |
| Socialize / ChaoMusicGroup | Existing performing leader, compatible idle partner and available instrument; existing energy/fullness/Fun/relation/LOS rules. | Existing think/approach/carry/join; Cancel frees held instrument; ensemble owns performance. | Existing instrument executor; positive contact/Fun. | 3s scan with old random chance |
| Leisure / ChaoLeisure, IslandNap, ChaoPillarVisit, ChaoMotion cliff route, WateringCan | Main-garden destinations/ability/surface guards; independent delays. Leisure uses fixed existing route/sky-spot candidates. | Route/sky completion, 300s max, 2.5s stuck recovery; Cancel restores pose. Lying/rising or airborne pieces must finish safely. | Existing walking, cloud/rest, island hops, climb/glide and watering sequences. | Leisure 4–9s initial delay, 7s after cancel; other components keep their old delays |
| Wander / ChaoWander + ChaoMotion | Fallback local destination; obstacle steering/neighbor avoidance/water/ledge handling. | Destination refresh; no interaction reservation to release. | Existing gait, swimming, gravity, collision, calls. | Main 4.5–6.5s destination, resident 4.6–6.6s |

`Busy`/`CanPauseForSocial` do not mean every phase is cancellable: interests allow social pausing only Idle/Approach; leisure excludes lying/rising; physical fall recovery, grips, meals, coordinated social phases and settling require protection. The existing globals can acquire partners before local food/sleep decisions, which the new ownership contract must resolve.

## Implementation and verification

### New order and responsibilities

The original physics loop is retained. There is no second moving/updating AI loop. Each actor's existing update first observes its actual execution state through `GardenActivityAdapter.Begin`, then, after the existing player/flight/recovery/sleep/pose guards, calls `Select`. The adapter feeds `ChaoNeeds` and pure availability records to its own `ChaoActivityCoordinator`.

Current feeding, toy grips, actual sleep, coordinated social phases, rising, physical recovery and short pauses remain authoritative. On an open decision point: probe available categories → score → retain or safely cancel the current optional action → select → ask the old component to begin → run its existing Step. Mixed ChaoInterests scanning no longer independently acquires categories in the production garden loop; category-restricted BeginSelected still chooses the actual target using its previous preferences/ability rules. Standalone executor entry points remain available to existing tests and explicit interactions.

Main and resident adapters are the same class. Their existing movement/animation paths remain separate. Coastal Cove uses that same selector with food/toys/social/main-garden routes unavailable, neutral wandering and safe dry ground rest available when needed. The existing ChaoRest settlement and SleepPose are reused without a bed hop; waking ground rest does not launch the bed-leaving jump.

Legacy automatic sleep counters have been removed from autonomous selection. Terrain excursions, island visits, pillars and watering remain existing Leisure executors with their previous capability checks/delays; calls, player gifts, courtship and physical safety remain external higher-priority choreography. No new animation, personality-weight formula or activity is introduced.

### Scores

| Category | Base | Final score before repeat penalty |
|---|---:|---|
| Eat | .10 | .10 + Hunger.Weight × .75 |
| Sleep | .10 | .10 + Fatigue.Weight × .75 |
| Play | .12 | .12 + Boredom.Weight × .75 |
| Socialize | .10 | .10 + SocialNeed.Weight × .75 |
| Leisure | .15 | .15 + Boredom.Weight × .25 |
| Wander | .15 | .15 |

Unavailable categories score zero and never enter selection. Existing Need.Weight includes the stage-1.3 essential-need bias/hysteresis; it is not recomputed as a competing need system. No random score bonus is used. Exact score ties use the coordinator's Random, seeded from the stable actor ID. Tests can supply a fixed Random directly.

No category score reads the thirteen personality values, Kindness, Initiative or Sociability. Existing target compatibility (including favorite objects, established food-finding ability and the ball executor's pre-existing sociability requirement) remains in its old executors; those are not new category-weight multipliers.

### Availability, claims and failure

Availability records include category, reason, estimated duration, interruption class and target count. They are observations, not reservations. Probes run at most once per active second while an activity is held, immediately when need urgency changes or a new selection is required.

- Food/toy probes reuse their real executor eligibility, honor current physical occupancy and another actor's target claim, reject water-crossing approaches, excessive height differences and blocked direct physics segments. Loose fruit cannot be considered accessible through its tree trunk. This is intentionally conservative visibility/local reachability, **not a new global navigation system**. Moving obstacles can still invalidate a previously usable target; the executor timeout/failure path releases it.
- A bed needs a free existing ChaoRest slot and must be within a practical local approach (10m). The old resident Energy<58 rest-need guard is used consistently, avoiding unnecessary sleep when already energetic. If the bed is full/too distant or absent in Cove, the same executor can settle on a free dry patch supported at its center and four surrounding samples, normal Y>=.7 and height agreement within .13m. Bed-top ground fallback is excluded. Existing bed slot reservations and temporary ground-rest claims prevent overlap.
- Play requires a free suitable toy under the existing Fun thresholds. The adapter claims the actual chosen target before another actor may approach it.
- Socialize requires an actual possible introduction, ensemble join, or existing shared-ball opportunity. It does not invent a generic conversation action. Raw physical readiness is retained, and partners must be free or safely interruptible after their commitment. Introduction acquisition reserves both participants; an existing ball sequence acquires its recipient through the same ownership gate. Music performers retain their existing instrument ownership.
- Leisure counts supported existing route/sky destinations; its existing steering and stuck timeout remain responsible for execution. Wander is the neutral fallback.

The controller owns no prop transforms, movement, animation, food consumption, memories or relationships. Claims are temporary resource coordination around the old executors. A target becoming invalid or a failed start produces a category-local retry at +4s; three attempts with the same error use +12s. Other categories remain selectable. A successful completion clears that category's failure counter. Cancel releases a claim/bed slot through the existing component and does not teleport the actor or drop an atomically held prop merely because scores changed.

### Commitment and interruption

Eat/Play/Socialize remain committed for actual execution, with Atomic protecting feeding/grips/coordinated social phases. Interest approach is LongOptional; thought/brief observations are ShortSafe. Sleep uses the existing minimum **35 seconds of actual sleep**, in addition to approach/settling; a filling Energy meter does not end it early. Leisure holds at least 8 seconds, Wander at least 5. Physical rise/recovery/airborne and existing action pauses are protected from coordinator interruption.

Critical Hunger/Fatigue can cancel LongOptional immediately when an available physiological alternative outranks it. High Hunger/Fatigue can do so after commitment. Atomic/ShortSafe do not get cancelled by need rescoring. Comparing the alternative's score prevents cancelling and restarting a higher-scoring food approach every frame. A broken/unavailable executor is allowed to fail before minimum commitment.

The last five **completed** intentions/times are runtime-only. An immediate repeat receives exactly -.15 when an alternative exists. After intervening activities the immediate-repeat penalty is gone; it is never a prohibition. Critical Eat/Sleep and absence of alternatives waive it. Interruptions/failures are separately diagnosed, not fabricated as successful completions.

### Completed social contacts

Physical relief is independent of `ChaoMood.React` acceptance. `ObserveEvent` accepts an optional completed-contact ID; each pet's bounded runtime ledger applies PlayerContact -6 or PositiveSocial -8 once per ID even if the same-source emotion is on cooldown. Player-care calls use monotonic local completion IDs; ball passes use session/pass IDs, music joins distinct completion IDs, and recorded shared completions use their stable source/time identity. Repeated frame processing of one completion gives no extra relief.

Legacy event callers without an ID use frame-level source/time idempotency. The ledger retains the latest 256 completion IDs, is not a persistent event archive and is copied/rebound with editor runtime state. No events are replayed from saves. The shared-state bug fix does not add any expedition decision code or change Follow/Wait/Call; expedition companion/decision/discovery/navigation/mission files are untouched.

### Diagnostics and persistence

The existing `--needs-debug` panel now appends current category, executor, remaining commitment, all scores/availability reasons, last completion/failure/cancellation and selection/interruption counts. Editor IPC adds the same activity diagnostics. No new debug window or ordinary HUD element is created.

`PetState.GardenActivity` and contact history are JsonIgnore/private runtime data. Save JSON retains all permanent needs, personality, IDs, relationships, memories and statistics. Chao Editor changes keep the actor's coordinator while rebinding meter/mood views to the replacement PetState. Travel suspends the garden lease and clears world-specific claims; no garden coordinator moves expedition actors.

### Verification

Pure tests cover prescribed A–L/N cases: need winners, absent food/partners, protected atomic feeding and observation, urgent optional interruption, no pointless higher-score restart, local retry escalation/reset, three independent coordinators, two completed social contacts during one emotion cooldown, repeat penalty/critical exemption, five-entry history, seeded ties, personality-independent scores and JSON exclusion. A five-minute fixed-condition unit simulation collects completed selections and interruption counts.

`--activity-check --needs-debug` runs the actual existing garden updates on isolated generated data: three different actors feed/play/sleep simultaneously, an actual meal survives a fatigue spike, five simulated minutes collect per-actor selection/failure statistics, blocked food is unavailable, full bed slots use safe free rest, one toy has one claimant, critical fatigue cancels an existing route, a real introduction owns/completes both participants, and Cove reuses ground rest. It writes statistics and a native diagnostic screenshot and exits. The existing full graphical smoke suite remains enabled for gardens, social/food/animation/editor behavior, all twenty slots, expeditions, wildlife, missions and stadium.

Remaining limitations: conservative local reachability can exclude a target accessible by a more complex detour; existing routes can fail and are retried with cooldown rather than being replaced by a new planner. Social categories expose existing interactions only. Completion-ID replay protection is a bounded active-session window. Category integration for expeditions and personality weighting belong to later stages, not 1.4A.

### Recorded results — 2026-11-23

- Build: zero warnings/errors. C# suite: **406 checks passed**; Python editor suite: **5 passed**.
- Native activity run: **ACTIVITY_SCENE_OK**, three actual actors, 300 simulated seconds. Hungry selected 5 times, Playful 7, Sleepy 9; each had zero need-driven interruptions under stable conditions. One existing Leisure route reported blockage and used the failure/cooldown path. A separate forced critical-fatigue case interrupted a real optional route successfully.
- Native editor replacement while a real rattle interaction was active retained the executor and updated Fun on the replacement PetState. Ball/music/toy captured participant references are rebound through the existing editor apply hook.
- Real-save recovery audit: **four existing Chao**, canonical serialization round-trip and unchanged input. Archive: 269 original files, all backup hashes and timestamps verified. Default user saves and slots were unchanged; a previously open stage-1.3 preview had newer simulation progress and was preserved as the source of the final isolated preview rather than overwritten.

### Changed files

New: `Scripts/ChaoActivityCoordinator.cs`, `Scripts/GardenActivityAdapter.cs`, `Scripts/GardenGame.Activities.cs`, `Scripts/GardenGame.ActivityChecks.cs`, `Tests/ActivitySelectionChecks.cs`, this document.

Integration: `Scripts/GardenGame.cs`, `Scripts/GardenEgg.cs`, `Scripts/ChaoInterests.cs`, `Scripts/ChaoLeisure.cs`, `Scripts/ChaoRest.cs`, `Scripts/ChaoIntroductions.cs`, `Scripts/ChaoMusicGroup.cs`, `Scripts/ToyCar.cs`, `Scripts/ChaoBallGame.cs`.

Contact idempotency/runtime preservation: `Scripts/PetState.CurrentState.cs`, `Scripts/PetState.cs`, `Scripts/ChaoExperience.cs`. Diagnostics/tests/documentation: `Scripts/ChaoStateDiagnostics.cs`, `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `docs/chao-needs-mood.md`.

Final full native regression: **SMOKE_OK**, including stadium, interactions, landscape, Coastal water, expedition team/discovery/travel, greenhouse, wildlife and personality scenarios. No stderr errors.
