# Innate personality editor — stage 1.7A

Manual developer intervention, not personality learning. The existing thirteen-value ChaoPersonality and PersonalitySchemaVersion=1 remain unchanged. No new save schema, random personality factory, gameplay formula, sound, animation or navigation is added.

## Files and audit

New: `Scripts/ChaoPersonalityEditor.cs`, `Tools/chao_personality_editor.py`, `Tests/PersonalityEditorChecks.cs`, `Tests/test_chao_editor_personality.py`, `Tests/check_personality_editor_live.py`, `Scripts/GardenGame.PersonalityEditorChecks.cs`, this document.

Modified: `Scripts/ChaoEditorBridge.cs`, `Tools/chao_editor.py`, `Scripts/GardenGame.cs` (isolated check entry only), `Tests/Mori.Tests.csproj`, `Tests/Program.cs`.

Audited the existing JSON request/snapshot protocol before editing; PetState and its personality/current-state partials; model, decision, activity, expression, gesture and voice readers; garden adapters and resident binding; save integrity/store; personality and editor tests, including the existing `check_editor_live.py`; and the personality model/decision, expression, gesture and voice documentation. The new live checker complements the existing stats-preview-specific checker without relaxing its three-pet/profile assumptions.

## Interface

Personality is a separate **main tab**, placed after General and Stats so the existing Stats tab index is retained. The existing Tkinter application and JSON IPC are reused. Ctrl+Tab traversal is enabled. The existing 960×690 default and 710×540 minimum are retained.

The header identifies the selected name, stable ID and model version and states that this is manual innate-personality intervention. Each of thirteen independent rows has a name, confirmed original value, integer-step slider, numeric entry, explanation and Reset. Rows scroll vertically; focusing a lower entry reveals it. A scrollable read-only preview lists exact old→new differences, with changed count and visible Reset/Apply actions. Grid layout reserves space for actions at minimum size rather than hiding them below the scroll area.

Typed values must be whole signed integers in [-100,100]; invalid/decimal/out-of-range input remains visible with an error and disabled Apply, not silently rounded or clamped. Slider movements deliberately snap to step 1. Slider and entry synchronize without sending IPC. Returning to the confirmed value removes the change. Row/all Reset only changes the local draft and sends no request.

Drafts are separate per stable ID. Switching tabs does not rebuild them. Switching pets or explicitly reading from game with a dirty personality offers Apply-and-stay, discard, or cancel. Automatic snapshots refresh diagnostics and clean personality forms, but retain dirty originals, entered values and their original revision. A disappeared pet or unsupported/missing personality blocks Apply; no zero-filled or random replacement model is created.

Pending requests cannot be resubmitted. Server errors retain entered values. Successful acknowledgement rebases to the confirmed snapshot. Session changes invalidate old drafts/requests until explicitly refreshed. The top Apply supports mixed edits, including personality alone. The Personality Apply sends only personality; other unsaved ordinary/stat/relationship controls survive its successful acknowledgement rather than being discarded by the form rebuild.

Selected ID, form owner and snapshot target must agree before sending. Programmatic selection refresh avoids redundant selection events; callbacks from an old form cannot write a different pet's draft. A live test caught a mismatched target/revision during fast switching: the server safely rejected it, and the frontend now guards this condition independently (important when two pets have identical personality revisions). Snapshot reads retry only a transient Windows/OneDrive PermissionError, up to three 10ms pauses; persistent failure reports an error and keeps the draft rather than using guessed data.

## Thirteen traits: implemented effects

All numeric scales are unchanged, including **Naive: more negative means more naive**. These effects and coefficients are MoriGarden implementations, not claims of exact SA2B behavior.

| Trait | Confirmed current use |
| --- | --- |
| Curious | Garden Leisure/Wander scoring; existing exploration adapters; interest expression, Idle looks and question-voice pitch |
| Kindness | Animal assistance, expedition social intent weight and watering-can eligibility through the existing adapter |
| Energetic | Play/Leisure/Wander scoring, positive expression, Idle paw gestures, positive voice pitch; negative threshold backs Crybaby |
| Naive | Negative=naive axis retained; **Not yet connected to gameplay** |
| Solitude | Socialize scoring and small inward free-paw posture |
| Vitality | Small Play/Leisure scoring terms; no Energy-drain change |
| BigEater | Eat scoring and existing Glutton consumption/snore rules |
| Regain | **Not yet connected to gameplay** |
| Skillful | **Not yet connected to gameplay**; does not replace Dexterous |
| Charm | **Not yet connected to gameplay** |
| Chatty | Socialize scoring and ordinary vocal interval; does not replace Sociability |
| Carefree | Play/Leisure scoring and relaxed Idle sway |
| Fickle | Small random variation among close activity choices, under existing need guards |

Existing fixed favorites, acquired preferences/habits, memories, needs, sports, life stage, evolution, inventory, expedition and stadium progress are separate and are not edited by this tab.

## Protocol and revision

Snapshots add computed `personalityRevision` beside the existing `personalitySchemaVersion` and `personality`. This revision is **not saved in PetState**. It is uppercase SHA-256 of UTF-8 text `MoriGarden/personality-editor/v1:1` followed by `:` and each invariant-culture signed integer, in the exact order of the table above. It does not use runtime hashes, names, object IDs or list order.

Example request:

```json
{
  "session": "current-game-session",
  "id": "new-unique-request-id",
  "target": "main",
  "patch": {},
  "statistics": {},
  "personalityRevision": "revision-from-the-confirmed-snapshot",
  "personality": { "Curious": 80, "Energetic": -40 }
}
```

`personality_patch()` compares all entered values with the last confirmed baseline and returns only actual differences. No differences means `{}` and no unnecessary personality section/request. Nonempty canonical patches require an exact revision match. A change to **any** of thirteen canonical values makes an older revision stale; the whole mixed request then fails with a Read-from-game message. This is deliberate conflict rejection, not a silent merge.

The C# helper uses an explicit thirteen-name whitelist and switch, with strict JSON integer types and the original ChaoPersonality.Validate. Unknown keys, schema injection, null/string/fraction/bool values and out-of-range integers are rejected without clamping. Unknown request sections and malformed section structures are rejected. Existing optional `statistics:null` / `relationship:null` remain compatible with old clients; `personality:null` is invalid. Requests without personality retain the old protocol. Request IDs are remembered for the bridge session, including rejected requests, so nonconsecutive duplicates cannot replay; the set is runtime-only and resets with a new session.

## Atomicity and relationship protection

The bridge validates the session/target/availability and sections, clones the target's persisted data, applies ordinary/stat/personality changes to that candidate, prepares relationship changes in a **deep copied** root SocialLinks list, validates the candidate, and prepares runtime handoff. Only then does it commit.

For a resident-target relationship edit, existing live link objects are updated only at this final commit point, using the validated prepared values. Their identities are preserved because existing relationship views/executors may retain references. New links are appended only at commit. A root-target update uses the prepared list in the replacement root. There is no mutation of the live owner's list or link fields while validating. The transaction is synchronous on the game thread; there is no yielding between commit assignments.

An original smoke assertion exposed why merely replacing the resident's shared root list was insufficient: it retained a link reference and correctly detected that the editor had failed to update it. Production commit logic now preserves that link identity after independent preparation. The original assertion remains unchanged. Tests also force failure **after** relationship preparation (invalid SocialNeed) and require the entire original roster and live list reference to remain unchanged.

After success the existing game caller rebinds selected actors/executors, saves through the existing Save/SaveStore path and refreshes UI. No second saving mechanism is introduced. Errors before commit apply nothing, including other valid parts of the same request. The bridge's normal snapshot write retry does not replay an edit.

## Legacy fields

Curiosity, Kindness, Glutton and Crybaby remain the same source-compatible PetState adapters. Their old controls display read-only values and the frontend does not send them. The canonical Personality tab is the editing surface. A nonempty personality patch combined with **any** of those legacy fields is rejected atomically, preventing Crybaby/Glutton threshold setters from overwriting signed values unexpectedly.

For compatibility, old API clients may still submit a legacy-only ordinary patch under existing adapter semantics. Canonical JSON-import aliases (`SavedCuriosity`, `SavedKindness`, `SavedGlutton`, `SavedCrybaby`), Personality as an ordinary field and PersonalitySchemaVersion remain prohibited. A legacy-only edit changes the computed revision, so an already open canonical draft becomes stale safely.

## Runtime handoff

KeepCurrentRuntimeFrom retains the existing coordinator, mood/needs state, acquired-learning runtime, social selection and vocal mailbox. The existing coordinator compares a copied personality value snapshot and refreshes its normalized weights on the next evaluation without resetting its selected activity. ImportedChao binds the replacement PetState to the existing model. Gesture probabilities and facial multipliers read that state on subsequent updates; an already active gesture need not be restarted. VoiceProfile reads the updated canonical traits on the existing executor/channel; already planned delays and authored playback are not forcibly reset. Existing toy/music/ball rebinds remain in use.

The native test verifies updated Curious scores/visual readers, actual Energetic Happy expression, Chatty/Energetic voice coefficients, unchanged Energy, preserved visual/channel IDs, active flute grip/stream continuity, independent resident editing and exact all-thirteen save/load. No personality assignment is added to ordinary experience code.

## Verification

Build: `dotnet build Mori.csproj --no-restore -v quiet` passes with zero warnings/errors. Full C# suite: **745 checks pass**. Python: **34 tests pass** across the existing stats/editor suite and the new real-Tk personality suite. These cover the thirteen controls, synchronization, integer bounds/errors, sparse patches, Reset, confirmed baselines, dirty navigation/read guards, sessions, missing/future models, target binding, other unsaved controls, relationship/stat compatibility, minimum size, keyboard scroll-to-last-trait and transient snapshot locks.

All native modes exit 0: `--personality-editor-check`, `--smoke-test`, `--chao-vocal-check`, `--expression-integration-check`, `--idle-gesture-check`, `--daily-rhythm-check`, `--needs-check --needs-debug`, `--activity-habits-check`, `--activity-check`, `--learned-check`, `--place-preferences-check`. These retain existing Save Recovery, garden/expedition, race and Karate coverage. The original social-editor smoke assertion is unchanged and now passes with identity-preserving relationship commit.

The native `--personality-editor-check` requires an explicitly supplied isolated input copy; it never invents a replacement Mori if that input is absent. Optional `--personality-editor-live` holds the same native bridge for the frontend live checker at a bounded publication rate. The real frontend changes/restores Chatty on four actual stable IDs: **eight personality transactions**, then **three real Stats transactions** (Run E/C/S, Level 99, Points 1287/1881/2772), with exact acknowledgements and unchanged unedited personality values. It also checks the minimum-size UI. The native wrapper requires the frontend report itself to say passed; a completion marker alone is insufficient. Final live run exits 0 with `passed:true` and no Godot errors.

Desktop review on **2026-11-28**: launched ordinary MoriGarden on a fresh audited safe copy and launched the actual `ChaoEditor.cmd`. Viewed Personality for Mori, White Chao, Black Chao and Yellow Chao with their correct separate IDs. Confirmed original values, preview and Reset without changing the model. Through actual keyboard/mouse controls applied only `Curious: 0 → 1` to Mori, saw Applied and the new confirmed original, then restored `1 → 0`. The request contained only that trait, target main and an empty ordinary patch. All four pets end with their original thirteen values and sports statistics. Mori is left selected on Personality; the game and editor remain open on the separate profile.

All **269 backup files** verify against hashes and all **121 original save/slot files** remain unchanged. The real roster, fixed favorites, acquired preferences/habits, history, relationships, life/evolution and expedition progress are retained in the input and isolated round-trip checks. Tests, IPC snapshots and logs stay in ignored `artifacts/`; no personal saves are committed.

## Limits

Four traits are stored/editable but intentionally have no implemented effect. These are developer controls, not a progression system or original-game personality editor. There are no presets, randomization, inheritance changes or acquired-data editing. Dirty personality drafts are held for this editor process, not persisted as a new save feature. Revision conflicts require reread/reset; unsafe game transitions retain the existing rejection rules rather than interrupting current actions. Other editor tabs keep their existing behavior. No subsequent stage is started.
