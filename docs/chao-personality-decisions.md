# Personality and garden decisions — stage 1.4B

Research/audit before implementing weights, 2026-11-23. Sources: existing [personality model](chao-personality-model.md), [Chao Island research](https://chao-island.com/wiki/Personality_%26_Emotion), [personality offsets](https://chao-island.com/guides/chao-hacking/hacking-pc/chao-offsets/chao-personality.html). The research article labels its evidence incomplete and partly speculative. These are community reverse-engineering findings, not official formulas. Original games and saves were not modified.

## Evidence and proposed extension

Every stored scale is -100..100, zero neutral. Detailed selection coefficients below are **MoriGarden rules supplied in this task**, not recovered SA2B formulas. Unknown does not mean unused in the original.

| Property / original axis | Evidence for original behavior | Unknown / limit | This stage's extension |
|---|---|---|---|
| Curious / Normal–Curious | Signed curiosity; contributes to newborn face | Exact action-selection formula | Leisure and Wander preference |
| Kindness / Kindness | Contributes to newborn face | Exact behavioral formula | No category effect; future help priorities |
| Energetic / Crybaby–Energetic | Negative side associated with longer/more crying; contributes to face | Not a measured movement-speed multiplier | Active category preference only |
| Naive / Naive–Normal | Negative means naive | Original decision formula unverified | None; future unfamiliar-event response |
| Solitude / Solitude | Research associates it with Loneliness and seeking player attention | Signed direction/precise formula unverified; mod behavior is not vanilla evidence | Positive reduces Socialize preference, explicitly a MoriGarden design interpretation |
| Vitality / Vitality | Listed signed scale | No original category formula confirmed | Small active preference, no energy-recovery change |
| BigEater / Normal–Big Eater | Ravenous eating reported in original | Exact category priority unverified | Small Eat preference; existing fullness rules authoritative |
| Regain / Regain | Listed signed scale | Behavioral direction/formula unverified | None; future recovery execution context |
| Skillful / Skillful | Listed signed scale | Not assumed equivalent to Dexterous | None; future execution quality |
| Charm / Charm | Listed signed scale | Exact social effects unverified | None; future others' reactions |
| Chatty / Chatty | Listed signed scale | Original conversation frequency not confirmed here | Socialize preference; no conversation AI |
| Carefree / Normal–Carefree | Signed scale in offsets | No original safety/need immunity established | Play/Leisure preference only |
| Fickle / Fickle | Listed signed scale | Exact vanilla effect unverified | Bounded choice variation among close scores |

The research association of Solitude with seeking attention does **not** establish that a high value means avoiding company. The requested negative Socialize coefficient is deliberately not presented as an original effect. No experiment in the installed originals was performed.

## Scoring contract

`T = storedValue / 100`, in [-1,+1]. The pure immutable `ChaoPersonalityActivityWeights` snapshot normalizes all thirteen values. The coordinator refreshes that snapshot only when canonical values change (including editor replacement); it never migrates, initializes or edits personality. The evaluator needs no PetState reference: urgency and the pre-existing base score supply all necessary context.

| Trait | Category contribution |
|---|---|
| Curious | Leisure +.12T; Wander +.05T |
| Energetic | Play +.08T; Leisure +.04T; Wander +.03T |
| Solitude | Socialize -.10T |
| Chatty | Socialize +.08T |
| Carefree | Leisure +.06T; Play +.04T |
| BigEater | Eat +.06T |
| Vitality | Leisure +.04T; Play +.03T |
| Fickle | No additive preference; close-choice jitter bounded by .03 × max(0,T) |

Sum contributions, clamp to [-.20,+.20], then `FinalScore = clamp(BaseScore + Adjustment,0,1)`. For Eat/Sleep, a negative adjustment is removed at Critical urgency and bounded below by -.05 at High. Sleep currently has no trait contribution. The six stage-1.4A base formulas remain unchanged. Availability/cooldown excludes actions before personality scoring. The existing -.15 immediate-repeat penalty is applied **after** FinalScore, with the same critical-physiology/no-alternative exemptions. Diagnostics distinguish this effective choice score from the unpenalized FinalScore.

Choice is evaluated only at an existing decision point. Available critical Hunger/Fatigue resolves deterministically to the most urgent physiological category (need weight, then Hunger on exact tie). Otherwise only positive effective scores within .12 of the best remain; a single candidate wins, multiple candidates use score-proportional sampling with the coordinator's own Random. Fickle jitter is drawn only within this close set and only for candidates whose **unmodified base score** is within .10 of the best available base. It is symmetric, bounded, and affects sampling weights, not the pure FinalScore. Negative/zero Fickle leaves the general close-choice sampling intact. Randomness is never drawn by Evaluate or applied per frame to a running action.

Ownership, commitment (Leisure 8s/Wander 5s), Atomic protection, urgent optional cancellation, five-entry history, category-local retries and resource claims are retained. Changes in personality do not cancel an active executor. No speed, energy rate, ability, mood, memory, relationship, fullness or animation is written by the evaluator; no temporary weights are serialized.

## Existing compatibility boundaries

Legacy Initiative, Sociability, Aggressiveness, ActivityPreference, Clumsy and Dexterous have no added coordinator multiplier or migration. Canonical Curious/BigEater/Energetic still back their established compatibility aliases in other executors. Existing target preferences and eligibility remain authoritative:

- ChaoInterests retains favorite fruit/toy, food-finding/fullness/strength rules.
- GardenGame.Activities and GardenActivityAdapter retain the existing Sociability>50 shared-ball eligibility; ChaoBallGame retains its prior invitation/acceptance and agility preferences.
- ChaoIntroductions retains its existing contact/readiness/visibility constraints; introductions happen once per pair. Existing music joins still need a compatible performer/instrument and social availability.
- Other old action-quality/personality checks in executors are unchanged, not duplicated as general category bonuses.

Kindness, Naive, Regain, Skillful and Charm are normalized snapshot fields without category contributions. Future help, unfamiliar-event reactions, recovery execution, execution quality and others' responses can consume them in separate small tasks; none of those effects is implemented here. There are no new personality classes in gameplay; Explorer/Social/Playful/Quiet exist only as generated test inputs.

## Deterministic statistical results

2000 independent decisions per profile, the same seed set 4100..6099, no completion history. Every profile has Fullness=80, Energy=80, Fun=65, SocialNeed=35 and the same available categories (Sleep excluded because no rest need). Availability is synthetic for this isolated selection comparison. These frequencies do not promise exact proportions in a living garden where resources, needs and actions change.

| Profile | Eat | Play | Socialize | Leisure | Wander |
|---|---:|---:|---:|---:|---:|
| Explorer | 0 | 1019 | 0 | 981 | 0 |
| Social | 0 | 885 | 1115 | 0 | 0 |
| Playful | 0 | 1128 | 0 | 872 | 0 |
| Quiet | 722 | 777 | 501 | 0 | 0 |

Each profile selects at least two categories; no category reaches 90%. Explorer has more exploratory choices than Quiet, Social more social choices than Explorer, and Playful more Play than Social. Wander is outside the .12 window in this particular fixture, not globally disabled. At Critical Hunger and Critical Fatigue, each of 1000 seeds chose the corresponding available physiological action despite extreme active preferences. Existing activity safety checks remain enabled.

## Test modes and limits

`--personality-decisions-check --needs-debug` uses generated temporary data and three actual actors, identical initial needs, equal types/counts of nearby food/toys, and existing rest/social/route opportunities. It collects selections, time by category, failed attempts, cancellations, repeats and mean needs over 600 simulated seconds, checks innate values and save/load, captures a native screenshot, and exits. It is an accelerated engine simulation, not a controlled frequency experiment: actors move to different places, reserve shared resources and can exhaust one-time introductions. The independent pure test is the controlled personality comparison. Existing route blockage/retry and conservative target reachability from 1.4A remain limitations; this task does not replace navigation or create repeatable conversation AI.

## Files and verification scope

New: `Scripts/ChaoPersonalityActivityWeights.cs`, `Scripts/GardenGame.PersonalityDecisionChecks.cs`, `Tests/PersonalityDecisionChecks.cs`, this document.

Updated: `Scripts/ChaoActivityCoordinator.cs` (score details, cached trait contributions, bounded close sampling and runtime counters), `Scripts/GardenGame.cs` (two test-mode hooks only), `Tests/ActivitySelectionChecks.cs` (replace the superseded no-Curious-weight assertion with protection against duplicated legacy/deferred weights), `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `docs/chao-action-selection.md` (link the subsequent stage).

No executor, animation, movement, expedition AI, personality schema, save schema or sports-stat file was changed. Cached trait contributions are computed once per personality snapshot, not once per physics frame. Atomic actions are retained across repeated Choose calls even if needs/personality change.

## Recorded engine results — 2026-11-23

Build: zero warnings/errors. C# suite: **430 passed**. Python editor suite: **5 passed**. Full native regression: **SMOKE_OK**, including stadium, twenty slots, garden interactions, Coastal water, expedition team/discoveries/travel, greenhouse, wildlife and expressions. Existing activity regression: **ACTIVITY_SCENE_OK**, atomic feeding, local claims, blocked food, occupied bed/ground rest, urgent optional cancellation, social ownership and live editor executor rebind.

Three actors completed **600 simulated seconds** using existing executors with rendered frames between batches; native data and all thirteen traits survived saving unchanged. Per-actor measurements:

| Profile | Selections | Failed attempts | Interruptions | Completed repeats | Mean Hunger / Fatigue / Boredom / SocialNeed |
|---|---:|---:|---:|---:|---|
| Explorer | 14 | 1 | 0 | 1 | 30.5 / 28.7 / 20.1 / 39.0 |
| Social | 20 | 3 | 0 | 2 | 30.5 / 28.8 / 10.4 / 41.0 |
| Playful | 15 | 1 | 0 | 2 | 30.5 / 29.2 / 11.4 / 39.0 |

Time by category (seconds):

| Profile | Play | Socialize | Leisure | Wander | Sleep |
|---|---:|---:|---:|---:|---:|
| Explorer | 91.8 | 16.0 | 424.9 | 15.0 | 38.6 |
| Social | 34.4 | 0.0 | 473.3 | 40.0 | 38.6 |
| Playful | 33.6 | 16.0 | 483.1 | 15.0 | 38.6 |

Failed attempts were existing Leisure route blockage handled by the unchanged retry contract. Live results are strongly affected by resources/locations; Social did not initiate a social action in this particular unconstrained run because eligible partners were scarce and introductions are one-time. **This run alone does not establish higher live social frequency.** Controlled equal-availability tests establish the category-weight effect. A separate existing-music availability fixture checks social joining without adding conversation behavior.

Safety: 270 pre-existing files archived with hashes/timestamps; 103 source pet/slot files (including the previous safe preview) unchanged. The real four-pet garden passed the recovery audit with a full canonical round-trip and unchanged source. Temporary weights/history remain absent from saves.

Supplemental `--ensemble-fixture`: 180 simulated seconds, all three starting at SocialNeed=70 with neutral introduced relationships, an existing performer and spare flutes. After physics registration, Social independently selected Socialize and joined the musician for 60.4 seconds; Playful independently selected Play. Explorer/Social/Playful selected 3/4/3 activities, with zero failures, interruptions or completed repeats. This validates an available social path, **not** a guarantee of frequent conversation in an empty garden. The performer is test setup, not a forced autonomous decision. No gameplay executor was changed to obtain this result.

Manual Chao Editor check on the real four-pet copied garden: Mori, White Chao, Black Chao and Yellow Chao present with their original stable IDs. Run Progress 60 → 61 was Applied to the live game, then restored to 60 with Applied confirmation. All thirteen personality values for every pet exactly matched the original input before and after. The latest user instruction overrides the attached request to leave the game running: the game is closed after verification; the editor can remain open on the isolated profile.
