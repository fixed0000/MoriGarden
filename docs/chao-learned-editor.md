# Chao learned-preference editor — stage 1.7B

## Scope and audited integration

This extends the existing Chao Editor, IPC transaction and persisted models. It edits **effective Bias**, not experience. No new preference dictionaries, save schema, learning events, gameplay formulas, personalities or navigation are introduced. These editor controls and learning formulas are MoriGarden extensions, not restored SA2B rules.

Audit: `ChaoLearnedPreferences`, `ChaoActivityHabits`, `PetState.LearnedPreferences`, `PetState`/`CurrentState`, `ChaoEditorBridge`, `ChaoPersonalityEditor`, `ChaoActivityCoordinator`, `ChaoInterests`, leisure/social scorers, `SaveStore`/`SaveIntegrity`, garden activity/rebind/resident paths, both existing editor modules and their tests; read the item, habit, personality-editor and action-selection documents.

The existing consumers remain unchanged:

* `ChaoInterests.Choose`: existing available-toy and edible-fruit scores are multiplied by `1 + effective item Bias`. Fixed favorites, distance, Fun/Fullness, taste, reservation and reachability still apply. A fixed favorite's 2.15 multiplier can outweigh a learned preference; the editor does not promise absolute target selection.
* `ChaoActivityCoordinator.Evaluate`: Play/Socialize/Leisure receive the existing additive HabitAdjustment, alongside original needs, personality, rhythm and repeat penalty. Item Bias never changes category priority. Habits remain excluded from physiological interruption comparisons.
* Actual `ToyCar`/`FruitMeal` completions and existing leisure/social executors remain the only natural learning sources. No editor code calls `Credit`, `ObserveEvent` or memory recording.

## Files

New: `Scripts/ChaoLearnedEditor.cs`, `Tools/chao_learned_editor.py`, `Tests/LearnedEditorChecks.cs`, `Tests/test_chao_editor_learned.py`, `Tests/check_learned_editor_live.py`, `Scripts/GardenGame.LearnedEditorChecks.cs`, this document.

Modified: `Scripts/ChaoEditorBridge.cs` (optional patch/revision and read-only snapshot), `Tools/chao_editor.py` (Experience integration, joint dirty guards and section-aware acknowledgements), `Scripts/GardenGame.cs` (isolated test-mode dispatch), `Tests/Mori.Tests.csproj` and `Tests/Program.cs` (test wiring). `Tools/chao_personality_editor.py`, learning models, SaveStore and all selection formulas are unchanged.

## Controls

Experience → Item preferences has separate Toys and Fruits pages. Activity habits retains Real experience and adds Adjust Bias. All 17 rows have current effective Bias, synchronized slider/number input, local Reset/Neutral and read-only stored Bias/count/last real credit. A zero-count manually created record displays no real credit. Previews have scrolling; rows have independent scroll areas and keyboard focus reveals the selected field. The minimum size remains 710×540. A section selector preserves access to all previous Experience diagnostics when tab captions exceed the available width.

Items accept −0.300…+0.300; habits accept 0.000…0.100, at most three decimal places. Invalid input disables learned Apply. Returning to the displayed confirmed baseline produces no patch. Snapshot float rounding/ordinary decay do not manufacture unsaved changes. Explicit Neutral can still zero a tiny nonzero stored Bias even when the displayed effective value rounds to 0.000.

Reset selected/all only discards unsaved values; Neutral is also only a draft until Apply. After a confirmed save, reverting requires another explicit adjustment or a safe backup, not Reset. The editor does not provide an undo history for committed adjustments.

Drafts are per stable Chao ID, retained across tabs, automatic refresh, rejection and timeout. Switching Chao or Read with dirty drafts asks to apply/stay, discard, or cancel. A new session or missing target blocks an old draft. Real learned-data changes mark its revision stale and the game rejects it until reread. Dirty personality and learned drafts coexist: their scoped Apply buttons only send that section; the top Apply can send a mixed transaction. Scoped acknowledgements retain other unsaved personality/learned/general/sports/relationship controls. The ordinary existing relationship editor remains separate from learned partner preferences.

## Canonical keys and IPC

`toys`: `toy-car`, `rattle`, `beach-ball`, `flute`, `drums`, `electric-guitar`, `synthesizer`.

`fruits`: `Garden Fruit`, `Heart Fruit`, `Blue Square Fruit`, `Yellow Triangle Fruit`, `Red Round Fruit`, `Green Diamond Fruit`, `Moonblossom Fruit`.

`habits`: `Play`, `Socialize`, `Leisure`. Eat/Sleep/Wander are not editable habits.

```json
{
  "session": "confirmed-game-session",
  "id": "unique-request-id",
  "target": "main",
  "patch": {},
  "learnedRevision": "revision-from-the-confirmed-snapshot",
  "learnedAdjustments": {
    "toys": { "rattle": 0.200 },
    "fruits": { "Garden Fruit": -0.100 },
    "habits": { "Play": 0.075 }
  }
}
```

Only changed values are sent. Old clients without these sections continue working. Empty maps are no-ops. Nulls, arrays, strings, booleans, unknown keys/groups, schema/metadata/ledger injection, nonfinite/out-of-range/overprecision values are rejected, never clamped. The server inspects the JSON spelling before decimal conversion, rejecting tiny/long numbers that decimal parsing could otherwise silently round.

`learnedRevision` hashes canonical group/key order, both schema versions, presence and each stored Bias/count/update/credit using invariant round-trip numbers and SHA-256. It excludes `History.Seconds` and effective Bias: passive decay does not invalidate a draft, but real learning, record creation/removal or manual adjustment does. Presence distinguishes missing neutral from an existing neutral historical record.

## Effective rebase and real history

For server time `now = current.History.Seconds`:

```
stored Bias = requested effective Bias
LastUpdateSeconds = now
ExperienceCount = old count, or 0 for a newly created record
LastCreditSeconds = old real-credit time, or 0 for a new record
```

The requested value takes effect at apply time, even if the snapshot was older. Existing neutral records keep their real history. Missing neutral remains absent. An edited entry with update/credit ahead of the server's active clock is rejected. No system clock or client timestamp is trusted.

Manual Bias may intentionally disagree with the count or historical last credit. This is expected: those fields describe actual experience, while Bias also reflects this developer adjustment. No fictional meals, sessions, memory or mood are added.

Unchanged natural learning:

* Items: `effective + sign * .07 * max(0, 1 - abs(effective)/.30)`, clamped to ±.30; half-life 12000 active seconds, credit interval 45 seconds.
* Habits: `effective + .02 * max(0, 1 - effective/.10)`, clamped to 0… .10; half-life 24000 seconds, credit interval 120 seconds.
* Effective Bias = stored Bias × `pow(.5, elapsed / halfLife)`. Replay ledgers retain 128 IDs per model. Negative natural item learning remains wired only to already supported reliable events; this stage adds none.

At −.300 the existing saturation formula initially has no learning increment; passive decay gradually permits recovery. The UI warns about this instead of changing the model. Count-zero manual entries do not create a fake last completion/cooldown. Existing count-positive records retain their real cooldown.

Fixed `FavoriteToy`, `FavoriteFruit`, `FavoriteInstrument`, `LikesToyCars` remain independent, never reassigned by learned edits. Neither the thirteen innate parameters nor places, partner Bias, memory, athletic development or evolution are changed.

## Atomic commit and runtime

The bridge uses the existing session/target/availability checks, detached candidate, independent shared-relationship preparation and full validation. Personality and learned revisions are checked before committing. Any error rejects the whole mixed request, including valid ordinary/sports/personality/relationship fields. No live collection is edited while validation is pending.

`KeepCurrentRuntimeFrom` retains both replay ledgers and the existing coordinator/mood/needs/social/vocal runtime. Existing rebind paths update the active executor/model to the new PetState. No new coordinator, voice channel, scene or action is started. Already active play, meals, sleep and protected social activities continue under existing rules; selection changes apply on subsequent evaluation. Existing Save/SaveStore persists the final state, with no migration or new schema.

## Verification

Build: zero warnings/errors. Full pure C# suite: **809 checks passed**, including stage 1.7A and new learned-editor checks. New cases cover toy/fruit/habit bounds, strict types/precision/underflow, sparse/neutral/manual records, independent IDs, server-time rebase, revision conflict after real learning, no conflict from passive decay, future timestamps, mixed rollback, sessions/replay, learning cooldowns/ledgers after rebind, natural learning after edit, exact save/load and legacy neutral data.

Python: **59 tests passed** in the final combined run (25 learned-editor, existing 34 personality/stats). They exercise real Tk controls against temporary snapshots: synchronization, preview, local reset, sparse request/revision, dirty refresh/read/switch/session/target guards, error/ACK, independent drafts, scoped/mixed applies and minimum-size layout. The existing stage 1.7A assertions are unchanged.

Native `--learned-editor-check` requires `MORI_LEARNED_EDITOR_INPUT` to be an audited safe copy and `MORI_TEST_DIRECTORY` to be an isolated output directory. It loads actual saved Chao/models, then sets controlled needs and fixed favorites **only in that test copy** so equally favored toys can be compared. It tests actual ChaoInterests target ranking in both directions, availability despite high Bias, the same coordinator's Play HabitAdjustment/intention, real flute play during IPC rebind with unchanged model/audio-channel/stream identities, exactly one item/habit credit on natural finish, no cancelled credit, actual fruit ranking/consumption/taste rejection, independent residents, unchanged personalities/fixed favorites during editing, save/load and invalid-request atomicity.

Optional `--learned-editor-live`, with `Tests/check_learned_editor_live.py`, exercises real frontend Apply buttons through the running Godot bridge: four stable IDs, toy/fruit/habit sections, Reset, confirmed baselines, unchanged real counts/credit/personality and minimum-size visibility. Six transactions passed with `passed:true` and native exit 0. Initial live-harness failures were corrected: Experience must be found by its title, and widget paths must be refreshed after an acknowledged form rebuild. Those were harness assumptions, not target-selection or transaction failures.

### Final native regressions and desktop review

All eleven existing native modes exit 0: `--personality-editor-check`, `--smoke-test`, `--chao-vocal-check`, `--expression-integration-check`, `--idle-gesture-check`, `--daily-rhythm-check`, `--needs-check --needs-debug`, `--activity-habits-check`, `--activity-check`, `--learned-check`, `--place-preferences-check`. These cover introductions, shared ball, instruments, sleep/rising, garden transitions, all 20 save slots/recovery, expeditions, Race and Karate. The final new `--learned-editor-check` also passes on the existing garden toy instances (20 native assertions). No tests were disabled.

Before final desktop review, all **270 backup copies** verify against their hashes and all **121 original save/slot files** remain unchanged. Test profiles, IPC and logs remain in ignored artifacts.

Desktop review on **2026-11-28**: ordinary MoriGarden launched on a fresh audited `learned-1.7B-preview` copy, and the actual `ChaoEditor.cmd` opened. Verified Mori, White Chao, Black Chao and Yellow Chao; all four stable IDs, thirteen personality values, sports statistics and fixed favorites match the input. Viewed Toys, Fruits, the original Real experience habit table and all three Adjust Bias controls. Through real mouse controls previewed a rattle adjustment, reset it without an IPC request, then applied **0 → +0.300 → 0** to Mori. The game acknowledged both applications; real experience stayed zero and last real credit stayed zero. The resulting neutral manual record exists only in this separate preview profile.

The desktop automation connection briefly stalled while entering a number; it was reset, and the final manual apply was verified using the slider and actual Apply button, server snapshot and saved request. Numeric/slider synchronization also passes the real-Tk automated tests. A clipped Neutral button caption found during inspection was widened; the complete 59-test frontend suite passes, with a further minimum-size check after the final explanatory-label adjustment.

The final Chao Editor was reopened through `ChaoEditor.cmd` to load the finished UI; it and the ordinary safe-copy game are left open. Original user saves were not used for editing. No next stage was started.

## Limits

Only these 17 types are editable. Places, social partners and memory remain read-only; no presets, bulk randomization, new learning/evolution rules or subsequent stage. Drafts live only within this editor process. Stale revisions require explicit rereading rather than silent merging. Display precision is three decimals; manually applying a displayed value intentionally rebases that precision, while leaving it untouched sends no patch. IPC diagnostic changes and natural gameplay in safe test profiles are not changes to the user's original save.
