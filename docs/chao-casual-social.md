# Casual social encounters — stage 1.8A

## Audit before implementation (2026-11-29)

`Socialize selection → GardenGame.SocialActivityTargets → eligible/leaseable candidates → SocialPartnerSelection → synchronous pair reservation → physical executor → completion/cancel → adapter returns to autonomy`.

Existing priority: first introductions, then joining music, then adapter beach-ball fallback. New familiar greeting goes between music and the ball fallback, without changing any category scores. ChaoGreeting is player-facing fast orientation/rising, not a social planner. ChaoIntroductions owns first Look/Think/Alert/Approach/Talk/handshake; its Met/Introduced/initial relationship and memory path must remain exclusive.

Ownership audit: GardenSocialUses, SocialCandidates, SocialLeaseable/CanJoinSocial, MoveFriend's early return, UpdateEggs' shared-executor branch, GardenEgg.PrepareActivityFrame/Advance, ChaoVocal actionBlocked, player pickup/drop/call, petting/gifts, UsingItem, world ClearZone and editor RebindGardenExecutors. Updating only one Uses check would leave overlaps. Existing ChaoContacts remains the general separation layer; approach uses existing collision bodies and short direct-surface checks, never a new route network.

GardenActivityAdapter chooses Socialize with CurrentExecutor=`pending start`; its initiator is already Atomic while the synchronous starter runs. Recipients must pass CanJoinSocial, while that specific pending initiator must be allowed to complete its own start. ReserveSocial adopts the existing shared Socialize action; after physical ownership ends, the adapter completes that action normally. Ordinary Coordinator.Complete does not train habits, so no new learning hook is needed.

Completion uses only Needs.PositiveContact(3), an optional low-priority Mood.React and the existing LastCompletedPartner runtime marker. ObserveEvent(PositiveSocial), ChaoExperience.Together, memory Record and ActivityHabits.Credit would add unwanted effects and are excluded. Authored voice cues go through the model's existing ChaoVoice channel; shared ownership suppresses ambient voices. Fixed phase timings need no extra RNG.

Implementation and verified results follow below. This is a MoriGarden addition, not a reconstructed SA2B mechanic.

## Files

| File | Responsibility |
| --- | --- |
| `Scripts/ChaoCasualSocialRuntime.cs` (new) | Pure eligibility, phase timing, bounded cooldowns, completion and read-only DTO |
| `Scripts/ChaoCasualSocial.cs` (new) | Physical pair, existing poses/rising/voices, direct surface checks and cleanup |
| `Scripts/GardenGame.Activities.cs` | Existing Socialize acquisition order, pair leases, state rebind and actual counters |
| `Scripts/GardenGame.cs` | Shared ownership guards, physics update, cancellation hooks, bridge and isolated check entry |
| `Scripts/GardenEgg.cs` | Cancel before replacing an involved visual model |
| `Scripts/ChaoEditorBridge.cs` | Optional read-only `casualSocial` snapshot, no new edit section |
| `Tools/chao_editor.py` | Experience → Casual social encounters, read-only actual executor data |
| `Scripts/GardenGame.CasualSocialChecks.cs` (new) | Actual-model/physics tests and full normal four-Chao autonomy |
| `Tests/ChaoCasualSocialChecks.cs` (new) | Pure policy, timing, invariants and independent partner sampling |
| `Tests/test_chao_editor_casual.py` (new) | Actual Tk diagnostics, pet isolation, missing snapshot and request safety |
| `Tests/Mori.Tests.csproj`, `Tests/Program.cs` | Include and run pure checks |
| `Scripts/GardenGame.MemoryDiagnosticChecks.cs` | Isolate an existing route test from inherited courtship in its input copy; richer failure details |
| `Scripts/GardenGame.PartnerChecks.cs` | Positive check for newly repeatable greetings; retain negative availability test with satisfied SocialNeed |
| `docs/chao-casual-social.md` (new) | Audit, implementation, results and limits |

No PetState, SaveStore, personality, needs formula, relationship, navigation, expression, gesture or vocal algorithms were changed.

## Eligibility and selection

Only Mori Garden offers this executor. Both actors must be real active Chao on a safe dry supported surface, physically free or in an interruptible free activity, with a leaseable recipient. Eggs/cocoons, sleepers, held Chao, gift receivers, protected transitions, swimming, instruments, ongoing social activities and other existing protected actions remain excluded.

Both IDs must be distinct and valid. The existing link must already have `Met && Introduced`, and **both directional scores must be nonnegative**. Eligibility never creates a link or introduction. Energy must be at least 35 for both; SocialNeed at least 25 for the initiator and 15 for the recipient. Critical hunger/fatigue blocks a start. Existing category scores, physiology interruptions, rhythm, habits and personality adjustments are unchanged.

Nearby range is 0.45–2.2 horizontal units, height difference at most 0.16. A torso ray, nine dry/support samples and clearance from other Chao check the direct approach. No path search, waypoint, jump or teleport was added. Eligibility is rechecked immediately before reservation. Acquisition is synchronous: one runtime pair is owned, then both existing adapters reserve Socialize; an exception releases both leases and cancels. There is one active casual pair, not a new scheduler.

Eligible partners use the existing `SocialPartnerSelection.Select(..., "CasualGreeting")` and `ChaoSocialPartnerScorer`, including directional relationship/memory/distance weights and existing recent-partner multiplier 0.85. Success updates `LastCompletedPartner`; no new preference model or learning is introduced. Existing introduction and music acquisition precede casual greeting; the adapter's existing shared-ball fallback follows it.

## Execution and cancellation

| Phase | Rule |
| --- | --- |
| Notice | 0.60s, existing attention and smooth facing |
| Approach | Only the initiator moves with MoveAndCollide, at most 0.80 units/s, toward 0.80 separation; at most 3s |
| Face | 0.45s; existing ChaoRising first raises a crawling/lying model safely |
| Exchange | 2s; one authored model `voice` cue per actor, separated by 1s |
| React | 1s, small existing-pose nod |
| Finish | One completion, release ownership, normal adapter continuation |

Typical upright duration is 4.05s plus approach. Existing crawler rising can add about 1.5s; it pauses the Face phase timer but **not** the hard total 10s timeout. No new animations or model proportions were created. Attention/facing use existing ImportedChao presentation. The recipient is not translated toward the initiator. Both are protected from unrelated activity selection while owned.

Every step verifies models, current availability, direct visibility/support, distance (cancel beyond 3 or below 0.45), height and participant state. Energy below 25 or Fullness at/below 25 cancels. New obstacles, collisions, missing/queued-for-deletion models, player pickup/call/gift, transition or incompatible visual replacement release the pair without rewards. Cleanup stops its own rising/pose ownership. State-only editor replacement rebinds the same pair to current PetState by stable identity, retaining phase/timers and applying completion to the updated state once. Navigation and higher-priority executors retain their existing rules.

Authored sounds use each model's existing ChaoVoice channel/profile and priority policy. `GardenSocialUses` blocks ambient vocalization and competing solo execution; no new voice timer or extra ObserveEvent was added. Instruments still use their existing channels. A low-priority `Mood.React(Happy, 0.25, 3s, unique runtime source, 25)` is optional expression, not a permanent Joy reward.

## Effects and rate limits

Successful Finish calls **Needs.PositiveContact(3) exactly once per participant**. There is no direct Joy write, relationship growth, new introduction, ChaoExperience.Together, memory Record, learned partner credit or ActivityHabits credit. Generic coordinator completion is not a learning hook. Cancellation gives no SocialNeed relief.

Cooldowns use the existing root `GardenSeconds`: pair 180s, actor 60s after success; pair and both actors 15s after cancellation. Unordered stable ID pairs are JSON-encoded sorted arrays, avoiding ambiguous concatenation. Expired entries are pruned; storage is bounded to 256 pairs/64 actor entries. Restoring the garden clock cancels/clears stale runtime timing. All timing/counters/pair references are runtime-only: no schema or migration. Restarting resets greeting cooldowns by design; this action gives no persistent rewards.

## Editor

Experience → **Casual social encounters** shows actual pair ownership/IDs, phase, elapsed, distance, last availability check, last completed pair, cooldowns, cancellation and counts. Counts and last completed pair are explicitly labelled **executor** totals; actor cooldown is specific to the selected ID. An inactive actor has no invented partner or phase. Missing runtime snapshots show Unavailable. The section has no Apply, force-start or relationship controls. Existing request whitelist is unchanged and cannot accept `casualSocial` edits. All four pets have independent rows; changing unrelated parameters does not recreate the executor.

## Verified results — 2026-11-29

Build: `dotnet build Mori.csproj --no-restore -v quiet`, zero warnings/errors. Pure suite: **869 checks passed** (835 previous + 34 new). Existing 88 Python editor tests passed; **4 new actual-Tk checks passed** (92 total across the runs). No existing tests were disabled.

Independent partner sampling resets selection state and uses 10,000 controlled seeds per case:

| Case | First partner choices | Expected probability |
| --- | ---: | ---: |
| Equal eligible partners | 5,001 / 10,000 | 0.5000 |
| Positive prior relationship/memory | 5,330 / 10,000 | 0.5330 |
| Preferred partner unavailable | 0 / 10,000 | 0 |

The normal repeat multiplier is separately checked. These test a weighted preference, not guaranteed friendship or selection.

Native `--casual-social-check` requires an explicit isolated audited `MORI_CASUAL_INPUT` and separate `MORI_TEST_DIRECTORY`. **20 scene assertions passed**: original first introduction, real coordinator acquisition, all six phases, bounded collision motion, no recipient displacement, exact relief/no persistent mutations, exactly two authored voice signals, ten real completed greetings, protected toy/gift/sleep, real wall and dynamically inserted obstacle, disappearing visual, pickup/zone cancellation, real editor IPC rebind, existing crawler rising and full autonomy. A rendered real Exchange frame was captured and visually inspected; both models face one another upright at safe separation. No teleport or new pose asset is used.

Four actual saved Chao then run the full normal `_PhysicsProcess`, adapters and executors for **300 simulated seconds**, without invoking the greeting starter directly. At 20Hz: **1 autonomous greeting**, **0 cancellations**, 93 greeting frames / 5,907 ordinary frames (1.55% of the run in a greeting). Social starts: 1 casual, 0 introductions/music/ball in that specific fixture; coordinator selections by the four actors: 5/2/1/1. The earlier 60Hz run also completed one greeting (278/18,000 frames). This is controlled scenario evidence, not a promised universal encounter rate; other actions, distance, courtship and needs naturally dominate much of the time.

The ordinary final safe garden also completed a spontaneous **Mori–Black Chao** greeting. The real launched `ChaoEditor.cmd` showed one successful encounter, no cancellations and the remaining pair/actor cooldowns. Mori, White Chao, Black Chao and Yellow Chao were present with their original IDs and thirteen innate parameters. No relationship patch or force-greeting button was used.

All thirteen regression native modes passed: personality editor, learned editor, memory diagnostics, smoke (including Stadium Race/Karate, expedition/discovery/greenhouse/wildlife), expression, idle gestures, daily rhythm, needs, activity coordinator, learned item preferences, place preferences, activity habits/social partners, and vocal reactions (including 180s actual autonomy and PCM/channel checks). The corrected activity-habits fixture was rerun to HABITS_SCENE_OK/PARTNER_SCENE_OK; its earlier obsolete-assumption failure is retained in the artifact logs, not hidden.

Two fixture corrections preserve prior assertions: the memory route fixture's input had CourtshipReadiness=100/BloomRemaining≈75.5, so existing courtship correctly cancelled its forced route; only that test copy now clears courtship before testing leisure. The former no-toy social test must now acknowledge eligible familiar greetings; it adds a positive assertion, then satisfies SocialNeed before retaining the original no-eligible-social fallback assertion. No production route, movement speed, energy cost or learning formula was changed to pass tests.

## Save protection and limits

Before launching/testing, 273 files were backed up and verified. All 273 backup hashes and **121 original save/slot hashes** match at final verification. Integration tests use separate directories; original files are not migrated or edited. Recovery audit confirmed all four existing Chao and canonical save round-trip. The ordinary preview is intentionally playable and its own clock/needs/normal systems can advance; it is not overwritten with an old input when restarting. Existing courtship in that preview changed Mori–Black Chao to Partner/55 through the unchanged PairChao path; this is distinct from greetings, whose ten-completion invariant tests prove no relationship changes. No original save is affected.

Limitations: nearby dry direct approaches only, one active casual pair, Mori Garden only; no dialogue, new relationships, memories, habit learning or long-range searching. A meeting is not guaranteed when a preferred partner is unavailable. Runtime cooldowns do not survive restart. Visual review included actual rendered models and the ordinary live editor; it is not a claim of a broad subjective audio-quality evaluation. Full per-run logs, native report and screenshot remain in ignored `artifacts/casual-social-1.8A-5fbc4c9d`; no personal save is committed.
