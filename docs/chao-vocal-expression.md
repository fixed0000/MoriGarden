# Individual voices and emotional vocal reactions — 1.6C

These procedural intonations, personality coefficients and scheduling rules are **MoriGarden extensions**, not reconstructed SA2B mechanics. No imported recordings, speech, music, new social actions, emotions, animations or save migration are added.

## Audit: original calls and ownership

Originally `ChaoSounds.Play(owner,cue,start=0)` reused `owner/ChaoCue`, replaced its Stream and called Play. All streams were generated once as 22050 Hz, mono, signed 16-bit AudioStreamWav. The original ChaoVocal in ChaoSounds.cs had one random delay per garden/expedition caller, GD.Randf timing and generic Voice; Curiosity shortened its 19–39s awake delay. Sleep already used Breath/Snore and the Glutton-or-Aggressiveness rule.

The audit covers ChaoSounds/Mood/CurrentState/Expression/Personality, ImportedChao/Presentation, EmotionOrb, Distress, Introductions, BallGame, MusicGroup, ToyCar, GardenGame/Egg, editor bridge/UI and state diagnostics. It includes the needs, expression and idle-gesture documents. A full call-site inventory is retained locally in the isolated test case's `audio-callsite-audit.txt`.

| Existing source | Cue → original player | Start condition / frequency | Original interruption; current ownership |
| --- | --- | --- | --- |
| GardenGame, GardenEgg, ExpeditionCompanion → ChaoVocal.Step | voice → model/ChaoCue | Awake delay, initially 10–23s, thereafter 19–39s minus legacy Curiosity | Any next same-owner cue replaced it; now the same periodic executor uses model/ChaoVoice |
| Same sleeping executor | breath/snore → model/ChaoCue | Sleep entry .8s; breath every 5–8s, snore 3.5–5s | Now personal ChaoVoice; same sleep rules and quieter gain |
| ChaoIntroductions Talk | voice → each participant art/ChaoCue | Three alternating replies at .35s steps within Talk | Explicit animation priority on each ChaoVoice; reply count/order/animation unchanged |
| ChaoBallGame Offer/Answer/Refuse/Sad/pass | voice, angry, sniff → participant art/ChaoCue | Existing .8/.9s speech gates; sniff 1.4s; occasional play voice 3.1s | Same explicit calls, now personal ChaoVoice; no extra voice per pass |
| ChaoBallGame toss/hit/miss | ball → ball/ChaoCue | Existing contact/phase gates and 1.05s rolling cue | Remains the ball's ChaoCue |
| DiscoverySession/DiscoverySystem | voice → finder art/ChaoCue | Existing single call-player/invite-peer phases | Explicit ChaoVoice; underlying events are not generated again |
| ExpeditionTeam | voice → member art/ChaoCue | Existing call/rejoin responses | Explicit ChaoVoice |
| GreenhouseMission | voice → awake member art/ChaoCue | Existing changed-state reaction, 6s gate | Explicit ChaoVoice |
| WildAnimal interaction | voice → approached pet art/ChaoCue | Existing encounter interaction | Explicit ChaoVoice |
| FruitMeal.Advance | bite → art/ChaoCue | Real completed bite | Remains ChaoCue; FoodBite adds no emotional voice |
| EmotionOrb | mood → orb/ChaoCue | Existing shape change, excluding return to ball | Unchanged orb player and timing |
| ToyCar rattle | rattle → toy/ChaoCue | Shake index at 4/s | Unchanged item stream/player |
| ToyCar flute | flute → toy/ChaoCue | Phrase index every 1.24s | Unchanged item stream/player |
| ToyCar drums | drums → toy/ChaoCue | Hit index at 4/s | Unchanged item stream/player |
| ToyCar electric guitar/synthesizer | guitar/synth → toy/ChaoCue | Phrase index every 1.20s with existing start offset | Unchanged item stream/player |
| ChaoMusicGroup | No additional direct Play | Existing ToyCar performances and shared lifecycle | Music is protected by real action context; no new notes or parts |
| StadiumSession | mood/voice → participant art/ChaoCue | Existing event/celebration and .3s cheer gate | Mood stays ChaoCue; explicitly authored voice uses ChaoVoice |
| StadiumView | ball → art/ChaoCue | Existing impact counter | Unchanged event sound |
| PuzzleElement/WildlifeWorld | mood → element/effect/ChaoCue | Existing accepted mechanism/collection | Unchanged |
| ChaoDistress | No direct Play | Existing unpleasant/fright/wake ObserveEvent sites | Only accepted compatible runtime signals can sound; distress mechanics unchanged |

## Files and integration

New: `Scripts/ChaoVoicePolicy.cs` (pure profile, mailbox, scheduler and diagnostics), `Scripts/ChaoVocal.cs` (existing executor plus model-owned channel), `Scripts/GardenGame.VocalChecks.cs`, `Tests/ChaoVocalChecks.cs`, this document.

Modified: `Scripts/ChaoSounds.cs`, `PetState.CurrentState.cs`, `ImportedChao.Presentation.cs`, `ChaoExpression.cs`, `GardenGame.cs`, `GardenEgg.cs`, `ChaoEditorBridge.cs`, `ChaoStateDiagnostics.cs`, `Tools/chao_editor.py`, `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `Tests/test_chao_editor_stats.py`.

```
existing accepted ObserveEvent -> private bounded runtime mailbox
existing ChaoVocal.Step -> model's one ChaoVoicePolicy -> model/ChaoVoice
existing authored ChaoSounds.Play(model, vocal cue) -> same policy/channel, Animation priority
existing item/orb/event sounds -> original owner/ChaoCue
```

The old Play signature and Step call remain source-compatible. Active stable-ID ImportedChao models redirect only voice/angry/sniff/breath/snore. Unbound legacy objects retain their old fallback; no guessed permanent ID is invented. BindPresentation initializes one profile/executor per model, and rebind updates its current state without replacing the executor. A genuinely new/evolved model initializes a fresh runtime scheduler. No new Process/PhysicsProcess or global voice player exists.

GardenGame and GardenEgg execute their existing single Step before guarded returns, allowing the scheduler to age/drop signals during protected actions rather than replay them afterwards. Existing social reservations explicitly block ambient/reaction scheduling; authored dialogue still owns its calls. ExpeditionCompanion continues using the existing same Step. Gameplay updates, needs rates, action lifetimes and social choreography are unchanged.

## Profile, intonation and random stream

`BasePitchMultiplier = .94 + .12 * stableHash / uint.MaxValue`. The explicitly defined FNV-1a uint32 hash covers UTF-16 low then high bytes of `MoriGarden/voice/v1/` plus stable ID. Display name, list order, system time and string.GetHashCode are unused. One private System.Random per executor is initialized from that hash; it is independent of blink, Idle gestures and all AI streams. No new voice decision draws GD.Randf. The old unbound legacy Play fallback is retained for compatibility.

For T=clamp(trait/100,−1,1):

- ChattyMultiplier = 1−.20T. Next awake ambient attempt = max(15, random(19,39) × ChattyMultiplier).
- EnergeticAdjustment = 1+.08T, applied only to Happy/Excited pitch.
- CuriousAdjustment = 1+.06T, applied only to Curious pitch.
- Final pitch = clamp(base × (1+jitter) × applicable adjustment, .80, 1.35), with jitter uniformly within ±.02 (the pure profile also clamps external jitter to ±.025).

The canonical signed Personality.Curious supplies the question contour; old Curiosity is a compatibility adapter and no longer separately modifies frequency. Chatty alone adjusts ambient timing. Other ten traits receive no invented voice formulas; existing Glutton and Aggressiveness snore compatibility remains intact.

| Kind | Source / contour | Duration |
| --- | --- | ---: |
| Neutral | Existing Voice | .36s |
| Happy | Bright upward 450 Hz + 410t sweep | .36s |
| Excited | Stronger upward 460 Hz + 780t sweep | .36s |
| Curious | Soft rising 390 Hz + 600t sweep | .36s |
| Sad | Descending 430 Hz − 300t sweep, quieter gain | .36s |
| Frustrated | Existing Angry | .55s |
| Startled | Short falling 570 Hz − 560t contour | .24s |
| SleepBreath / Snore | Existing Breath / Snore | .82s |

The new phase is 2π(hz×t + sweep×t²/2), with fixed harmonic weights and soft noise. SmoothStep attack .035s and decay from .62×duration to the end provide smooth boundaries. Existing Render's clamp and 13000 PCM scale are retained. All variants are static cached AudioStreamWav instances; no PCM/WAV allocation occurs on playback. Existing authored sniff uses the original .32s Sniff, represented as Sad in diagnostics.

ChaoVoice is one AudioStreamPlayer3D attached to the actual model at stable-ID binding, including before its first sound: MaxDistance=15, ordinary gain −11 dB, Sad −14 dB, sleep −19 dB. Energetic never boosts volume. Distance attenuation remains spatial. Explicit animation replacement uses a bounded 8ms release on the **same** player before the new enveloped sample; at most one model-bound fade tween is active, with cancellation on sleep transitions/model removal. It adds no overlapping voice player or generated buffer. Lower-priority scheduled voices let the current short sample finish.

## Events, priorities and protection

Only `emotion == true` from the existing Mood.React result inside ObserveEvent forwards a signal. A physical contact accepted independently from an emotional cooldown does not manufacture a voice. No additional ObserveEvent is called for audio.

| Event | Voice |
| --- | --- |
| MealFinished, PlayerContact, PositiveSocial, DiscoveryFinished | Happy |
| PlayFinished | Excited |
| DiscoveryNoticed | Curious |
| UnpleasantContact, WakeDisturbed | Frustrated, only in compatible context |
| Frightened | Startled, only in compatible context |
| FoodBite, PlayStarted | None |

Pending capacity=8; maximum age=3s of existing Mood.Now active time; explicit logical contact IDs are deduplicated within 128 recent keys. The most important/latest usable signal is considered first; successful selection drains obsolete competing signals rather than creating a long queue. Protected signals can wait briefly, then expire. Editor KeepCurrentRuntimeFrom carries the same mailbox, with the existing copied Mood time. Mailbox contents and scheduler state are never serialized.

Priority order: Animation (5), Important unpleasant/fright (4), action-completion emotion (3), Ambient (2), Sleep (1). Explicit animation calls retain their number and ordering; ambient cannot interrupt them. Scheduled requests wait for the short current voice to finish, retaining only fresh signals. Minimum emotional interval=4s; same-kind cooldown=10s (also prevents an ambient echo of a recent emotional kind). Minimum ambient interval=15s. Authored animation replies are exempt from those new frequency limits to preserve their original .35/.8s choreography. Mood.React cooldown and all social/need consequences are unchanged.

Ambient/event voices require a safe free Idle/Walk and no held/sports/action owner. Music, toy grips, eating, rising, social reservations and protected poses suppress them. Important reactions can also use Distress if unheld and outside sports/social ownership. A reaction can legitimately be omitted while unsafe or under cooldown; it is not played late after expiry. Sleepy mood has no extra awake vocalization. Other ambient mood mappings are Neutral→Neutral, Happy→Happy, Excited→Excited, Curious→Curious, Sad/Lonely→Sad, Frustrated→Frustrated, Afraid→Startled.

Actual sleep clears pending awake signals and permits only Breath/Snore. Snore still means Glutton or Aggressiveness≥25. Existing sleep timings/energy/poses are unchanged. Wake cancels sleepy planning/playback and backlog, schedules a fresh ≥15s ambient interval and leaves natural/disturbed-wake Mood behavior intact. Existing local dt advances this runtime scheduler; paused gameplay supplies no Step. No offline simulation or new world clock is introduced.

## Diagnostics

The existing developer view and read-only **Voice and vocal reactions** editor tab receive the actual model executor diagnostic: stable ID, base pitch, Chatty/Energetic coefficients, current/last kind and source, priority, next ambient delay, emotional cooldown, channel name, actual player.Playing, suppression, pending count and runtime counts. Missing active executor is **Unavailable**, not synthesized state. The tab is beside the existing outer tabs so the seven Experience sub-tabs do not overflow. Editing other fields preserves personality, preferences, habits, relationships, memories and vocal runtime binding.

## Controlled statistical results

1000 independent 600s periods **per profile**, matching seeds/starting conditions, 1s sampling. The same nine-mood schedule is supplied to every profile; no event voices are injected. Sleepy windows are intentionally silent. Thus counts measure ambient behavior, not social/contact spam.

| Profile | Mean ambient count /600s | Observed minimum gap | Highest pitch | Sounding time fraction |
| --- | ---: | ---: | ---: | ---: |
| Quiet: Chatty −100, Energetic −50 | 14.839 | 23s | 1.0477 | .00894 |
| Neutral: all zero | 17.841 | 20s | 1.0477 | .01065 |
| Vocal: Chatty 100, Energetic 50, Curious 50 | 22.322 | 16s | 1.0896 | .01318 |

All seven allowed awake kinds occur; Sad includes Lonely. Frequencies remain ordered and sounding duty remains below 1.4%. Discrete observed gaps include sampling quantization; the scheduler's authored floor is 15s. Independent tests also check identical intervals when only Energetic/Curious change, reproducible decisions, all trait/pitch extremes and no shared timer.

## Verification, audio data and limits

Build succeeds with zero warnings/errors. The full pure suite passes **704 C# checks**; the Python editor suite passes **9 tests**. Existing native modes pass: `--smoke-test`, `--expression-integration-check`, `--idle-gesture-check`, `--daily-rhythm-check`, `--needs-check --needs-debug`, `--activity-habits-check`, `--activity-check`, `--learned-check`, `--place-preferences-check`. These include original personality/mood, sleep, introductions, ball, musical instruments, save recovery, expeditions, races and Karate. No existing assertion is removed or disabled, including the real instrument AudioStreamWav checks.

The new real-model scenario checks separate channels and pitch identities, actual completed rattle play, accepted discovery without extra game events, the original three alternating introduction replies with no ambient additions, a bound-pair beach-ball game, unchanged flute stream under background voice suppression, Breath and Snore, a natural bed approach → sleep → Rising → wake, short distress-compatible fright, actual audio-player WAV selection after the bounded release, editor rebind and save/load. Four models run for 180 controlled seconds and another 180 through the actual autonomous garden physics loop. Generated profiles belong only to isolated tests. Suppression samples, category counters, priorities, channel count and maximum pitch are exported with the report.

All kinds use 22050 Hz mono 16-bit buffers; repeated requests reuse the same resource. Measurements (normalized PCM):

| Kind | Duration | Peak | RMS |
| --- | ---: | ---: | ---: |
| Neutral | .360s | .24310 | .11213 |
| Happy | .360s | .22736 | .11449 |
| Excited | .360s | .22800 | .11450 |
| Curious | .360s | .22467 | .11446 |
| Sad | .360s | .22430 | .11446 |
| Frustrated | .550s | .17639 | .07601 |
| Startled | .240s | .22787 | .11197 |
| SleepBreath | .820s | .08621 | .02260 |
| Snore | .820s | .20203 | .05844 |

Absolute DC is below .000001 for every kind. Nonempty length, expected duration/rate, finite peak/RMS, signal limits and first/last samples are asserted. An added real-player test exposed a test-order issue: SceneTreeTimer can resume before that frame's fade tween. The check now also waits for FramePostDraw and still requires the exact expected cached WAV, rather than weakening the audio assertion.

The native `--chao-vocal-check` exports each cached kind and `voice-preview.wav` into its isolated test directory for audition, in enum order Neutral, Happy, Excited, Curious, Sad, Frustrated, Startled, SleepBreath, Snore. Generated exports are not runtime assets and are not committed. Numerical PCM checks do not establish subjective timbre quality. No external recording or original Sonic voice was used. No subjective listening claim is made without an audio observation facility.

Final `--chao-vocal-check` exits 0 with CHAO_VOCAL_SCENE_OK. Four bound models own exactly **four ChaoVoice nodes**, including before their first sound. During the initial identical 180s free interval they produce 6/4/5/7 ambient reactions. Across the whole controlled/event/sleep/autonomous scenario, per-model counters are:

| Actor | Ambient | Emotional | Authored animation | Sleep | Rejected direct priority requests |
| --- | ---: | ---: | ---: | ---: | ---: |
| main | 11 | 3 | 42 | 10 | 0 |
| voice-0 | 8 | 1 | 9 | 4 | 0 |
| voice-1 | 12 | 0 | 0 | 4 | 0 |
| voice-2 | 11 | 1 | 0 | 4 | 0 |

Maximum played pitch is 1.07114. No concurrent voice player is created. Scheduled requests are withheld while occupied, rather than submitted as conflicting direct requests. Sampled blocked/warm-up frames include 818 existing-voice, 3582 social/music, 2884 Rest, 180 Rise, 202 Thought, 121 Grip, 185 gameplay interaction, 13 Interaction, 11 transition warm-ups and one same-reaction cooldown. These are sampled actor-frames across phases, **not** counts of audible attempts or percentages of one 180s window. The obsolete suppression reason is cleared immediately when an actor becomes eligible; a separate pure check protects that diagnostic rule.

Desktop review on **2026-11-28**: launched ordinary MoriGarden using a separately audited copy and launched the actual `ChaoEditor.cmd`. Mori, White Chao, Black Chao and Yellow Chao are all present, with unchanged IDs, all 13 personality values, sports statistics, favorites, learned data, habits, relationships and existing memory records. The new read-only tab shows actual bound model data, including the distinct base pitches 1.0508139 / .9914459 / .9940132 / .9412657. Water, Crawl and Grip correctly suppress background voice without inventing playback data. Game and editor are left open on that safe profile; original user data is not the active testing profile. All **269 backup files** verify against their hashes and all **121 original save/slot files** remain unchanged. Logs, isolated JSON reports and the audition WAV remain in ignored `artifacts/`.

This stage deliberately does not implement dialogue, new social behavior, new emotions/animations, navigation, sports mechanics or a saved voice parameter. Protected actors may remain silent for a long time. Signals may be dropped under priority/cooldown/safe-pose rules; there is no backlog of late reactions. Pending state and unfinished sounds restart neutrally after save/load; the stable identity pitch remains the same. No subsequent stage is started.
