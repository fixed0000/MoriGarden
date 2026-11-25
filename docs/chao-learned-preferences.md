# Learned toy and fruit preferences — stage 1.5A

Audit before implementation, 2026-11-25. This is a MoriGarden extension, not an original SA2B learning formula.

## Existing sources and lifecycle map

| Source | Existing role | Boundary |
|---|---|---|
| FavoriteToy / PreferredToy | Fixed toy bonus, LikesToyCars fallback | Retain, never replace with learned bias |
| LikesToyCars / EnjoysToyCars | Existing car compatibility | Retain availability gate |
| FavoriteFruit | Fixed special-fruit taste and target bonus | Incompatible special fruit still rejected |
| FavoriteInstrument | Fixed instrument target bonus | Retain |
| ChaoMemory.ObjectBias | Salient discovery event context | No new use or duplication of discovery evaluations |
| ChaoMemory.PlaceBias | Place experience | Untouched |
| ChaoActivityCoordinator | Six category scores and commitments | Untouched; learned scores only rank targets |

| Executor | Start | Reliable normal completion | Interruption / not credited |
|---|---|---|---|
| ToyCar | BeginPlay after eligibility, including gifts | Advance crosses PlayDuration; once per session | CancelPlay, TakeFromPlay, invalid/far actor, deletion, moving before end; EndPlay is shared cleanup, not proof of success |
| Music group | Existing ToyCar.BeginPlay + StartWith | Each performer reaches duration, or shared normal end from conductor | Cancellation may shorten all group members; must not confuse this with normal group completion |
| Beach ball | ToyCar.BeginPlay; ChaoBallGame obtains owner/partner | Existing Solo 8s or 10 completed shared passes | Cancel is shared cleanup for refusal, fatigue, timeout, bad path, disappearance; never credit Cancel/FinishBall unconditionally |
| FruitMeal | Begin after receiving/reaching fruit | Advance reaches natural end with at least one actual TakeBite, including saturation after partial consumption | Taste refusal, no bite, missing/path-failed target, forced interruption; arrival/holding/inventory/bites alone are not completion |
| ChaoInterests | Choose → approach → thought → old executor | Delegates actual play/eating to above | No learning at choice, approach, thought or category completion |

FruitMeal is reused by Mori's gift flow and residents' ChaoInterests, including ContinueGivenMeal. A completion belongs to the real PetState supplied by the existing executor. Editor replacement already rebinds toy/ball/music actors; the new runtime anti-replay ledger must survive that replacement. Scene reload reconstructs executors, so old in-progress completions are not replayed from saves.

## Canonical data and identifiers

`PetState.LearnedPreferences` is the only persisted model. Its internal `SchemaVersion` is 1. It owns two independent dictionaries, `ToyPreferences` and `FruitPreferences`; missing keys read as neutral without insertion. Each entry stores `Bias`, `ExperienceCount`, `LastUpdateSeconds`, and `LastCreditSeconds`. Bias is [-.30,+.30], count [0,1000], timestamps finite/nonnegative with last credit no later than update. Known keys cap each dictionary at seven records. Static ID lists are read-only; pets never share mutable records.

Toy IDs: `toy-car`, `rattle`, `beach-ball`, `flute`, `drums`, `electric-guitar`, `synthesizer`. `ToyCar.PreferenceId` delegates the existing type flags to the centralized `LearnedPreferenceIds.Toy` mapping, with the same precedence as ItemName. UI labels are not keys. `Toy Car`/`toy car` are not accepted as extra learned IDs.

Fruit keys are the exact FruitKinds constants: `Garden Fruit`, `Heart Fruit`, `Blue Square Fruit`, `Yellow Triangle Fruit`, `Red Round Fruit`, `Green Diamond Fruit`, `Moonblossom Fruit`. Unknown IDs cannot create entries; unknown saved keys fail validation rather than silently expanding the catalog. No place/partner group exists.

## Learning, decay and anti-replay

All calculations use the individual pet's existing `History.Seconds`, advanced by `PetState.Tick`. No wall clock or new time source is introduced.

`effective = storedBias * pow(.5, max(0, now - LastUpdateSeconds) / 12000)`

`factor = max(0, 1 - abs(effective) / .30)`

`newBias = clamp(effective + outcomeSign * .07 * factor, -.30, .30)`

Reading computes effective bias without mutating stored data. A credited update stores the decayed result and current timestamps. No per-frame rewriting or decay-only saving occurs. Credit interval is **45 active seconds per item type**, separate from toy availability/cooldown. First credit at time zero is valid. Saturating experience counts do not grow past 1000.

Every play/meal starts with a new GUID completion token. Each pet retains a runtime FIFO/hash set of the latest **128** processed tokens, including tokens suppressed by the credit interval, so an ignored old completion cannot become a fresh credit later. Normal toy completion consumes its token; FruitMeal also retains a per-meal completion flag. Editor replacement copies this bounded ledger into the detached replacement model. The ledger and active executor/session tokens are not serialized. Scene loading reconstructs executors and cannot replay an old in-progress completion; the persisted LastCreditSeconds additionally blocks immediate repeat-credit after loading. The bounded ledger is not an unlimited historical archive.

`Credit(..., outcomeSign:-1)` is prepared and unit-tested, but **no production negative source is wired**. A failed path, tired pet, occupied item, player cancellation and a fixed-taste refusal are not evidence of disliking the item.

## Exact executor hooks

- `ToyCar.BeginPlay` creates the session token only after the old start checks succeed. `Advance` crossing the existing PlayDuration calls `CompleteLearnedPlay` once before cleanup. The helper also rejects invalid/deleted actors and invalid non-ball play distance. EndPlay/CancelPlay/TakeFromPlay never award credit.
- `FinishEnsemble(successful:true)` is used only by natural conductor completion. It completes each live member's session once before the old shared placement animation. Forced group cleanup uses the default false; artificially setting a member's timer to its end does not produce a completion credit on its next Advance.
- `ChaoBallGame`: the existing Solo eight-second end completes the owner's token; ten actually completed shared passes complete the owner's token plus one partner completion keyed by the shared session. Refusal, timeout, energy/path failure and Cancel/FinishBall do not credit. No per-pass learning is added.
- `FruitMeal.Begin` resets consumption count/token. `Advance` increments consumed count only with actual `TakeBite`. At natural meal end it credits once if at least one bite happened and the fruit was not taste-rejected. Partial consumption ending through saturation counts; simply beginning/holding/chewing without a bite does not. Forced interruption before that normal end does not credit. The same hook serves root/resident/given/autonomous meals without a second feeding implementation.

## Target selection and fixed favorites

The original ChaoInterests scoring remains: missing Fun/fullness, fixed favorite factor, loose-vs-tree factor for fruit, and distance. After eligibility/reachability/ownership checks, multiply that target score by `1 + effectiveBias`, in [.70,1.30]. This does **not** affect Play/Eat or any other category score in ChaoActivityCoordinator. A higher learned preference is deterministic for equal candidates in the old max-score target chooser; it is not a new probabilistic target planner. Distance, collision, target reservation, Fun/Fullness, car compatibility, fruit ripeness/Power and special-fruit taste still apply first.

FavoriteToy, LikesToyCars, FavoriteFruit and FavoriteInstrument are never written by learning/migration. A fixed favorite keeps its original 2.15 target factor; acquired bias is a smaller independent modifier. Existing special-fruit refusal is unchanged even at +.30 learned bias. ChaoMemory discovery/place/partner biases are not read or written by this model. No need rate, fullness reward, sports training, evolution, movement, animation, jumping or expedition-AI formula changes.

## Persistence and editor

Old JSON missing the entire LearnedPreferences field creates an empty model. Explicit null, incomplete records, unknown members/types and invalid numbers reject validation; filled valid records retain their values exactly. Unsupported schema versions fail in SaveStore.Load/LoadSlot without silently falling back to an older schema backup. Existing SaveIntegrity invokes PetState validation; SaveStore retains its all-or-nothing roster and refusal to overwrite an unreadable source. Existing serialization callbacks validate the new field without modifying the thirteen innate values. No separate save file or preference-specific autosave is introduced.

Editor snapshots expose a read-only view of fourteen supported types: group, stable ID, effective bias, experience count, last credit time and fixed-favorite flag. A single Experience tab separates **Innate personality**, **Fixed favorites**, and **Learned preferences**. Read from game refreshes it. Other editable controls retain their patch flow; the bridge excludes this complex model from editable fields and preserves its persistent data/runtime ledger when applying unrelated changes. The existing game debug panel shows the same three distinctions; no ordinary HUD numbers or second game diagnostic window are added.

## Verification results

- Build: zero warnings/errors. C# suite: **456 passed**, including A–M/Q–S model/persistence/editor scenarios and all former suites. Python editor suite: **5 passed**, including that unrelated patches carry neither innate nor learned models.
- Native `--learned-check --needs-debug`: **LEARNED_SCENE_OK**, no stderr errors. Three actual actors had identical needs/personality/fixed favorites for the controlled comparison. A earned six real completed rattle sessions, B six real flute sessions, C no item experience. After clock/need alignment, 200 matched seeds per actor varied candidate order and small symmetric distances. A chose rattle **200/200**, B chose flute **200/200**, C split **98 rattle / 102 flute**. Comparing goals did not itself change preferences.
- Native executor checks: cancelled play, disappeared actor, natural/cancelled ensemble, normal solo ball, complete fruit meal with repeated Advance, partial saturation after one actual bite, taste-refused fruit with injected +.30 bias, unconsumed/cancelled approach, physical wall, saving/loading and real target selection after loading. These cover N–P and the physical part of L. A high incompatible-fruit fixture is test data, not a gameplay way to acquire forbidden taste.
- Full graphical regression: **SMOKE_OK**, including stadium/twenty slots, garden interactions, landscape/water, expedition team/discovery/travel, greenhouse, wildlife, expressions/memory and existing editor grips/animations. Original state audits recovered all four real Chao with unchanged IDs and canonical round-trip data.
- Before tests: **270 original files** backed up with SHA-256 and timestamps; source saves are checked again after the final UI run. Generated tests and screenshots stay in ignored artifacts, never Git.

Limits: no new negative-experience source; no place/partner learning/habits; no change to the existing max-score target chooser or conservative reachability. Strong fixed favorites can outweigh learned inclinations by design. In a live garden opportunity/location still affects choices; the controlled trials, not a single wandering session, isolate acquired experience. Tokens cannot guarantee arbitrary external replay beyond 128 IDs; production executors never replay old serialized sessions because no sessions are saved.

## Changed files

New: `Scripts/ChaoLearnedPreferences.cs`, `Scripts/PetState.LearnedPreferences.cs`, `Scripts/GardenGame.LearnedChecks.cs`, `Tests/LearnedPreferenceChecks.cs`, this document.

Updated: `Scripts/ToyCar.cs`, `Scripts/ChaoBallGame.cs`, `Scripts/FruitMeal.cs`, `Scripts/ChaoInterests.cs`, `Scripts/PetState.cs`, `Scripts/PetState.Personality.cs` (serialization hook only), `Scripts/PetState.CurrentState.cs` (editor runtime ledger), `Scripts/SaveStore.cs`, `Scripts/ChaoEditorBridge.cs`, `Scripts/ChaoStateDiagnostics.cs`, `Scripts/GardenGame.cs` (test-mode hooks only), `Tools/chao_editor.py`, `Tests/Mori.Tests.csproj`, `Tests/Program.cs`, `Tests/test_chao_editor_stats.py`.

### Final copied-garden UI check

The original four-pet garden was loaded into `learned-1.5A-preview`, never the personal save path. Only this temporary copy started with Mori Fun=5 to observe a real autonomous session promptly; its original value and reason are recorded in ignored test setup data. Mori independently completed a flute performance and earned one flute experience (+.07 stored bias); Black Chao independently consumed Garden Fruit and earned its own fruit experience. These entries survived closing/reloading the game. No preference was injected into Mori for this UI test.

Chao Editor's Experience tab displayed all fourteen types with the three distinct sections and no editable learned fields. Mori plus White/Black/Yellow Chao were present. A real Run Progress 60 → 61 → 60 edit was Applied twice while already-earned records stayed byte-for-byte unchanged for every pet; all thirteen innate values/IDs matched the input garden.

A Windows snapshot-replacement race was found during the first UI opening (a reader can temporarily block File.Move). ChaoEditorBridge now retries **diagnostic publication** on the next Poll for transient IO/access conflicts, without replaying an applied command or writing the garden save. A deterministic held-reader test verifies retained preferences and eventual Applied publication; the restarted real game/editor check had no stderr errors. This is limited to the existing IPC snapshot, not a second persistence mechanism.

The latest direct user instruction overrides the generic leave-game-open step: close the game after checks, leave Chao Editor open on the safe copy. Stage 1.5B/1.5C are not started.

Final verification: all **270 archived files** matched their original hashes/timestamps; **103 source save/slot files** were unchanged. The copied garden saved successfully on exit with its four IDs, thirteen traits and earned preferences intact. Game processes were closed; the read-only Experience tab remains open in Chao Editor.
