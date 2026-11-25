# Learned social partner preferences — stage 1.5B2

Audit and implementation: 2026-11-26. This is an extended **MoriGarden** social preference system, not a recovered SA2B formula.

## Existing architecture and lifecycle audit

Canonical long-term attitudes remain root PetState.SocialLinks. Each initiator reads ScoreFrom(its own stable ID), never the other direction or a pair average. Personal, decaying experience remains ChaoMemory.PartnerBias(peer ID). SocialNeed still determines the desire for social activity through the unchanged category coordinator.

| Existing action | Acquisition / reservation | Actual successful completion | Interruption |
|---|---|---|---|
| Introduction | GardenGame.StartSocialActivity, existing SocialCandidates/SocialLeaseable, ChaoIntroductions.CanStart/TryStart; PauseForSocial reserves both actors | Existing Shake phase: mark Met/Introduced, existing relationship change and single ChaoExperience.Together call | Existing Cancel resets poses; no new completion record |
| Ensemble | ChaoMusicGroup.TryJoin selects an existing performer and free instrument; holds the prop and reserves the visitor | ToyCar.Advance crosses natural PlayDuration; FinishEnsemble(successful:true) ends the original group | Music approach Cancel, lost performer/roster identity, removed/moved actor, CancelPlay/FinishBall/EndPlay or forced group cleanup |
| Shared ball | Existing ball play enters Consider; filters nearby free candidates, chooses and reserves recipient; receiver independently answers/refuses | ChaoBallGame.CompletePass reaches ten passes, then existing Finish/placement | Refusal, timeouts, missed approach/separation/fetch/return, missing actor/prop, or Cancel do not take this completion path |

Introductions retain Look → Think → Alert → Approach → Talk → Raise → Shake → Lower → Wait, original poses/sounds and completion timing. Music retains thinking, instrument approach, carrying, playing and placing. Ball retains invitation, acceptance/refusal, separation, throws, misses, fetch, return and physical hit animations.

## Files

New:

* Scripts/ChaoSocialPartnerScorer.cs — pure scores, weighted draw, actor-owned runtime RNG/diagnostics, memory-only shared completion primitive.
* Scripts/GardenGame.PartnerChecks.cs — isolated actual-executor integration mode.
* Tests/SocialPartnerPreferenceChecks.cs — pure deterministic and persistence checks.
* docs/chao-social-partner-preferences.md — this report.

Changed:

* Scripts/GardenGame.Activities.cs — introduction candidate weighting, then revalidation and original reservation.
* Scripts/ChaoIntroductions.cs — remember successful partner in runtime state only; no second introduction memory.
* Scripts/ChaoMusicGroup.cs — valid performer/instrument combinations, weighted performer choice, revalidation, stable-ID handoff to the existing ensemble.
* Scripts/ToyCar.cs — short-lived performer identity in the existing ensemble, completion validation/recording, discard on cleanup; current PlayState resolves editor replacement.
* Scripts/ChaoBallGame.cs — eligible weighted choice, live-roster guards, completed ten-pass memory, refusal-memory safety.
* Scripts/PetState.CurrentState.cs — preserve per-actor runtime RNG/recent selection across editor rebind.
* Scripts/ChaoEditorBridge.cs, Scripts/ChaoStateDiagnostics.cs, Tools/chao_editor.py — separate read-only partner diagnostics.
* Scripts/GardenGame.cs — isolated test-mode entry only.
* Tests/Mori.Tests.csproj, Tests/Program.cs, Tests/test_chao_editor_stats.py — test wiring and read-only editor regression.

No changes to ChaoActivityCoordinator formulas, sports, evolution, inherited personality, navigation, mountain climbing, jumps, expedition decisions, ChaoMemory format, SaveStore/SaveIntegrity or learned toy/fruit/place formulas.

## Directional scores

ChaoSocialPartnerScorer.Evaluate is read-only, including missing-relationship lookup with create:false:

```
relationshipContribution = .20 * clamp(ScoreFrom(seekerId) / 100, -1, 1)
memory = seeker.History.PartnerBias(peerId)       // existing [-.30, +.40]
memoryContribution = .25 * memory
partnerBias = clamp(relationshipContribution + memoryContribution, -.30, +.30)
partnerWeight = clamp(1 + partnerBias, .70, 1.30)
distanceWeight = 1 / (1 + .12 * distance)
finalWeight = partnerWeight * distanceWeight * recentMultiplier
```

Unknown/no experience gives 1.00; +50/no experience gives 1.10; +100/+0.40 memory gives 1.30; -50/no memory gives .90; -100/-0.30 memory gives .725. Eligibility remains a separate gate: a negative score is not globally forbidden, but the existing music/ball nonnegative thresholds remain authoritative.

Each currently eligible partner appears once, regardless of how many instrument combinations might refer to it. A cumulative proportional draw uses the sum of positive finite weights. No max-score selection, random-times-weight maximum, Random.Shared or fresh generator per production choice is used. Each actor owns one System.Random stream, initialized once using a deterministic character hash of its stable ID. Tests inject seeded generators. The stream is shared across that actor's existing social starters, not shared across pets.

The most recently completed partner gets ×.85 only for repeatable music/ball when at least two eligible choices exist. Unknown/unavailable alternatives do not trigger the penalty. Introductions do not receive it. Successful handshake or shared completion updates the runtime marker; failed invitations/cancellations do not. A later successful partner replaces it. For a multi-performer ensemble one record can list all other performers; the runtime marker uses the first other participant from the existing ensemble membership, without adding a group/leadership model.

## Eligibility before preference

* Introductions enumerate the existing physical source, filter coordinator availability and CanStart (not already introduced, CanAct, <=1.95m, height/LOS), weight, then recheck the selected peer and TryStart. Original reservations remain in PauseForSocial. The legacy standalone auto-scan entry used by older tests is retained; production acquisition goes through GardenGame.StartSocialActivity.
* Music first constructs actual eligible performer/free-instrument combinations. Candidate Energy>=35, Fullness>=28, Fun<100, nonnegative outgoing relation, <=5m performer distance, <=.15m height, original performer LOS, free/non-water instrument within 8m and clear instrument approach are required. Invalid/removed actors cannot be selected. Performer and prop are revalidated before holding; a failed option can fall back to another eligible option. The nearest valid instrument is selected once; performer distance is weighted once, not twice.
* Ball keeps CanAct, current availability/roster presence, Fun<100, Met, nonnegative outgoing relation, <5m, <.16m height and clear path. The selected receiver is revalidated before reservation. Acceptance still uses the receiver's own outgoing relation, mood, energy and manners. Preference does not imply willingness.

Sleeping, held, rising, atomic, reserved, wrong-area and physically blocked actors remain excluded by the original SocialCandidates/SocialLeaseable/CanJoinSocial and executor gates. A high preference never interrupts a protected partner.

## Shared memory hooks and cancellation

New completed memories use existing ChaoMemory.Record only:

| Natural completion | Kind / Source / Area | Values / Participants |
|---|---|---|
| CompletePass number ten | Pleasant / shared-ball / garden | Valence .65, Importance .55, the other stable ID |
| Original ensemble natural finish | Pleasant / music-ensemble / garden | Valence .65, Importance .55, actual other performer IDs |

Each participant receives one own event. In ensembles with more than two performers there is still one event per pet, with the other IDs in Participants, not one reward per frame or per pair. Stable sources contain no runtime GUIDs. Existing 64-event bounds, key coalescing and 120-second cooldown apply. A refused memory credit does not undo physical success.

Ball has a per-session completion guard and leaves the completion phase after ten passes. ToyCar clears ensemble references on finish, preventing a later member's placement/EndPlay from recording again. Natural ensemble validation requires live, in-tree, non-deleting actors, current CanAct states, stable identities and valid play positions. StartWith gets IDs from the actual joining/performing candidates; no new persistent membership dictionary is introduced. Old standalone StartWith callers without IDs retain toy learning but cannot fabricate partner memories.

Current ToyCar.PlayState is updated by the existing RebindPlayer hook, so a live editor replacement receives the eventual memory. Forced cleanup discards context without recording success. Music approach references continue to be rebound by ChaoMusicGroup's existing hook.

No new ObserveEvent(PositiveSocial) or ChaoExperience.Together call is added at music/ball completion. Existing join relief and per-pass relief remain untouched; completion adds **zero** SocialNeed or relationship reward. Intro retains its one original Together call. The previous ball refusal memory is now recorded only for actual scolding; tired/low-mood/polite refusal alone does not add a negative partner-memory event. Refusal poses, mood/joy response and acceptance logic remain unchanged.

## Saves and editor

No schema migration or new saved affinity fields are needed. Persistent data remains SocialLinks and ordinary History.Events; SocialSelection has JsonIgnore. Runtime recent partner, RNG, candidate snapshots and ensemble context never enter save JSON. Missing/absent roster members may retain memories but do not generate actors or become eligible.

Existing Experience now includes `Social partners — read-only`, separate from personality, fixed favorites, item preferences and garden places. It shows stable ID/name, outgoing relation/status, existing PartnerBias, both contributions, partner weight and last actual selection weight/eligibility. The table scrolls horizontally at small window sizes. Last-scan eligibility is explicitly an observation, not a current reservation; without a scan it says Unknown. Existing relationship editing is unchanged.

The normal debug panel includes interaction type, last choice/completed partner and candidate weights decomposed into preference, distance and repetition. Other editor patches preserve runtime identity and persisted memories/items/traits.

## Verification

Build: zero warnings/errors. C# suite: **505 checks passed**, including 26 new partner checks and all previous personality/needs/activity/item/place/recovery/travel/sports checks. Python: **5 editor checks passed**, including omission of read-only social diagnostics from outgoing edits.

Pure cases cover A–R: neutral/positive/negative/directional attitudes, personal memory reuse/decay, no relationship creation, bounds, unavailable candidates, deterministic seeds, recent/single/intro behavior, invariant personality/needs/serialized input, completion deduplication, no duplicate relief, runtime rebind and persistence. For equal distance, **20000 independent seeds** gave **11302 preferred selections (56.51%)**, expected 1.30/2.30=56.52%, tolerance .02; neutral gave **9999 (49.995%)**. Every alternative remains possible.

Actual Godot acquisition trials reset candidate positions/leases and inject an independent seed for each choice, rather than treating one ongoing session as a statistical experiment. Three actual actors share the same display name but retain main/partner-b/partner-c identities. Each prefers the next actor through its own directional relation/history:

| Initiator | Introduction choices (main, B, C), 1000 trials | Ball choices (main, B, C), 500 trials |
|---|---|---|
| main | 0, 563, 437 | 0, 282, 218 |
| B | 446, 0, 554 | 223, 0, 277 |
| C | 577, 423, 0 | 288, 212, 0 |

Two equally distant performing musicians: the real TryJoin chose preferred B **564/1000**, C **436/1000**. These controlled prior histories are test inputs, not claimed real outings. Separate tests execute the actual completions:

* Real GardenGame introduction, all original phases, one original Introduction record per participant, repeat prohibited.
* Real music thought/approach/hold/play handoff; natural 60-second ensemble completion gives reciprocal memories, no extra need relief or relationship change. Actual editor bridge replacement during playback receives the record instead of the obsolete PetState.
* Main and resident inviters each complete a physical ten-pass game using real Game/egg updates and existing ball animations/physics. Each pet gets one correct partner event; the final pass gives exactly the existing clamped -8 relief, with no completion bonus. Innate traits/favorites remain intact.
* Cancelled acquisitions, forced ensemble cleanup, missing performer, tired refusal, ball timeout, protected favored partner, and no-repeatable-action fallback.
* Save/load restores individual memories/IDs and affects subsequent outgoing scores. The isolated report collects actual choices, weights, events, before/after relationship snapshots, need deltas and cancellation checks.

Final isolated engine runs: **PARTNER_SCENE_OK**, **ACTIVITY_SCENE_OK** (three actors/300 seconds), **LEARNED_SCENE_OK**, **PLACE_SCENE_OK**, and complete **SMOKE_OK**, exit code **0**, no stderr errors. Full regression includes introduction/music/ball animations, twenty save slots, selected-pet races/Karate, gardens/shoreline, expedition teams/discovery/travel, greenhouse, wildlife and personality. The first parallel smoke attempt ended without a final marker; it is not counted as a pass. The subsequent standalone tracked run completed successfully.

Real-save recovery audit: four original Chao, canonical round-trip equality, frozen input unchanged, editor roster exported. Before testing, 274 original files were copied; every backup hash/mtime verifies. Of 123 source save/slot files, 121 remain unchanged; the two autosaved files in the already-running earlier places-1.5B1-preview copy changed through that older simulation. The personal save directory is unchanged, and no source was restored/overwritten. New tests use only separate MORI_TEST_DIRECTORY locations.

Final GUI review: normal MoriGarden and **ChaoEditor.cmd** launched on a separate `partners-1.5B2-preview` copy of the audited real garden. The actual editor showed Mori, White Chao, Black Chao and Yellow Chao, original Stats, 13 innate parameters, fixed favorites and earned item experiences. Experience → Social partners opened successfully; both sides of the horizontally scrolling table were inspected, including directional score/status, PartnerBias, contributions, weights and Unknown last-scan availability before a social choice. A live snapshot comparison verified all four stable IDs, exact personality/statistics/fixed favorites, preserved learned experience counts and three partner rows per Chao. Both game and Chao Editor are left open. No real-save test edit or synthetic replacement Mori was needed.

## Limitations / intentionally deferred

Introductions are once per pair. Without a performing musician/free instrument or eligible beach-ball opportunity, no repeatable social action exists and Socialize may be unavailable. No invisible conversation or numerical-only interaction was added to conceal this limitation.

Physical eligibility is the existing conservative local geometry/LOS system, not a new global navigation graph. A previously available approach may still fail; existing lease/timeout/cleanup paths cancel without success memory. Personality/need state and receiver willingness continue to vary independently of preference. RNG progress and recent-partner history restart on reload; persisted memories/relationships do not.

No daily habits, routines, expedition preferences, extra relationship model, new social actions, new animations, new toys or mountain/navigation fixes were started.
