# Stat development audit and migration (stage 8)

## Dependency map before changes

| Owner | Current dependency | Required integration |
|---|---|---|
| PetState | Seven integers, capped at 990; animal +12, food +2; evolution chooses existing form | Canonical grade/level/points/progress; versioned migration; same forms |
| TrainingAnimal, AnimalCatalog, GardenGame gift handling | Species ID exists, gift passes only skill | Pass species; multiple signed progress effects; preserve capture/spawning |
| FruitMeal, FruitTree | Four bites; FeedBite on consumed bite | Stamina progress per bite, preserve fullness, rejection and development time |
| ChaoRace, RaceCourse, RaceTraversal | Raw skill slopes, thresholds, 990 divisors | A shared points-to-ability adapter; level never substitutes for points |
| ChaoKarate | Raw Power, Run etc.; 990 divisors in probabilities and timing | Bounded normalized points, positive durations, no animation edits |
| SportsRivals, StadiumSession | Opponent profiles use old ratings | Convert profiles once, preserve relative difficulty |
| ChaoMotion, GardenEgg, main garden behavior | Speeds, stamina drain, crawl/swim thresholds | Preserve proportional performance; use actual points at documented locomotion thresholds |
| Expedition team, discovery, wildlife, greenhouse, watering can | Intelligence ratios and thresholds | Hidden-stat points mapped independently; personality remains unchanged |
| PetState evolution/courtship | First and gradual second evolution; no automatic reincarnation/death | One grade promotion at first evolution; testable reincarnation transform only |
| SaveStore, nested GardenEggState.Chao | JSON + backup, snapshots, twenty slots | Additive stat schema; migrate every resident once, preserve identities and achievements |
| ChaoEditorBridge, Tools/chao_editor.py | Flat reflective scalar fields | Dedicated validated stat patch; numeric bounds and reproducible presets |
| Main card, team selection, StadiumUi | Fake level via points/10 | Real level, points and progress |
| Tests and in-engine checks | Many fixtures/assertions assume fixed +12/+2 | Update training expectations to progress; add seeded numerical and migration tests |

## Research and limits

Primary reference is SA2B, not Dreamcast. Sources:
- https://chao-island.com/info-center/basics/stats.html
- https://chao-island.com/wiki/Stats (search-index text; direct fetch returns 403)
- https://chao-island.com/wiki/Small_Animals
- https://chao-island.com/info-center/basics/feeding.html
- https://chao-island.com/info-center/life-cycle/death.html
- https://chao-island.com/guides/chao-hacking/hacking-pc/chao-offsets/chao-stats.html
- https://chao-island.com/guides/chao-hacking/hacking-pc/code-extracts/stat-level-up.html

The last code extract is explicitly SADX. It supports carrying the bar remainder but is not independent proof of every SA2B edge case. Large artificial editor grants will be processed safely with a loop; normal animal effects are below one full level. Main grades E–S use 13+3*grade+uniform(-2..2). Hidden grades use a separate byte 0–255, not E–S; the documented same arithmetic is evaluated with their own 4000 point cap. Level limit is 99; negative bar changes stop at zero without losing a level. 3266 is the console-only natural ceiling used as the requested safety bound, not the maximum of the first life. Retention after reincarnation is floor(points/10), grades unchanged, level 1.

Garden fruit gives 10 progress per consumed bite (40 per complete four-bite fruit). Existing custom colored fruits do not have confirmed SA2B equivalents; their stamina effects must be identified as MoriGarden adaptations. No new fruit types are added. Songbird has no unambiguous SA2B species identity; do not silently present an invented mapping as original data.

The fruit table also checks https://chao-island.com/wiki/Fruit: Heart Fruit gives 10 per bite and its final bite enables breeding. The existing colored square/triangle/round fruits follow the stamina amount of their shape equivalents, but remain MoriGarden variants. Diamond and Moonblossom have no confirmed original counterpart. Birth-grade distribution is a MoriGarden simplification: uniform E–S, hidden grades default to 0; exact genetic alleles/distributions are not reconstructed in this stage.

The installed executables were located only through Steam's library manifests: D:/SteamLibrary/steamapps/common/Sonic Adventure 2/sonic2app.exe and D:/SteamLibrary/steamapps/common/Sonic Adventure DX/Sonic Adventure DX.exe. Direct training experiments are not performed until save isolation is established; neither game's files or saves are modified.

Luck in races is disputed: the older info-center page assigns it race effects; the newer Stats research says SA2B Luck has no race effect and Run affects tripping. The user explicitly chose the SA2B research on 2026-11-17: tripping uses Run, not Luck.

## Migration decision

The canonical new data stores seven independent records. Legacy main points are mapped deterministically with round(old*3266/990); hidden points use round(old*4000/990). All migrated main grades are C, hidden grades 0: no random reassignment of existing pets. Levels preserve the former displayed old/10 value (minimum 1 for nonzero imported points), and progress preserves the former remainder as (old%10)*10. Historical random gains cannot be reconstructed. Imported point baselines are compatibility values and may not match the hypothetical history of a naturally raised C-grade Chao; they are not evidence of original-game reincarnations.

A shared adapter maps actual points back to the old bounded ability range for existing spatial/gameplay coefficients. This preserves relative legacy performance and prevents 3000 points from tripling old 990-based speeds or making probabilities negative. Actual points, never level or grade alone, feed this adapter. Locomotion milestones with documented SA2B point thresholds are treated explicitly. Personality fields retain their existing scales.

Existing public scalar names remain point aliases to the canonical records for source/JSON compatibility; they do not store a second independent current value. Legacy backing values are consumed only when the new stat record is absent. Saving creates a backup through the existing SaveStore mechanism. Migration itself is in-memory, idempotent, and does not write files on read.

## Implemented behavior and limits

- Only the first evolution promotes a grade, at most to S. Existing forms and the existing recent-animal/care/agility choice are reused; the full SA2B evolution-axis/genetics system is not reconstructed. An untouched neutral child defaults to the existing normal Bloom form.
- There is no new automatic death, deletion or reincarnation event. `ReincarnateStatistics` is an isolated transformation prepared for an eventual life-cycle event. Four optimal console-only lives are tested: 2970 → 3237 → 3263 → 3266 points.
- Current Energy is never replaced by Stamina. Existing gradual evolution food-time bonuses remain MoriGarden behavior, not measured SA2B constants.
- Main sports/AI coefficients consume actual points through the shared adapter, never Level or Grade. The adapter deliberately preserves MoriGarden course lengths and timing rather than asserting original SA2B formulas. First walking/swimming thresholds use actual 50/100 points. Other garden climbing/gliding thresholds remain calibrated adaptations.
- Run now controls trip probability. Luck continues to affect Karate recovery. Full original luck-dependent poses and the detrimental high-luck miss effect are outside this stat migration; no new animations are added.
- The editor rejects bounds, zero-level nonzero points, and clearly impossible level/point combinations using conservative bounds. Unchanged imported point baselines remain editable without erasing legacy strength. It does not fabricate a complete historical sequence of random gains.

## Validation

Build: zero errors and warnings. 263 C# checks, including the real editor bridge, independent migration of nested pets, no write on read, legacy backup, seeded grade windows, signed animal effects, food progress, point caps, first-evolution promotion and four reincarnations. Four Python editor checks cover presets, invalid input and unchanged imported baselines.

A seeded full Mushroom Forest race gives level-99 E (1280 points) 26.76 s, and level-99 S (2765 points) 14.46 s. These are MoriGarden simulation measurements, not claimed SA2B lap times. Existing swim/flight/climb timing checks retain the previous calibrated relative abilities.

The graphical SMOKE_OK run covers gardens, collisions/interactions, the editor protocol, sports, all 20 slots, expeditions, research, greenhouse, wildlife and memory on isolated saves. No original-game training experiments or user-save migration was run. The editor's final visual inspection uses an explicitly separate `stats-preview` garden with three test Chao.

The live Tk frontend integration check created an auxiliary test window, verified all seven stat rows and three pets, checked control bounds, and applied S → E → C presets through the running game (2772 → 1287 → 1881 points). The auxiliary test window closed itself; the Chao Editor launched through ChaoEditor.cmd remains open. The Windows screenshot helper timed out twice, so direct visual screenshot inspection is not claimed.

## Changed files

- README.md
- Scripts/AnimalCatalog.cs
- Scripts/ChaoBallGame.cs
- Scripts/ChaoEditorBridge.cs
- Scripts/ChaoInterests.cs
- Scripts/ChaoKarate.cs
- Scripts/ChaoMotion.cs
- Scripts/ChaoPillarVisit.cs
- Scripts/ChaoRace.cs
- Scripts/ChaoSettling.cs
- Scripts/ChaoStatistics.cs
- Scripts/CompactMissionState.cs
- Scripts/ExpeditionTeam.cs
- Scripts/ExpeditionTeamUi.cs
- Scripts/FruitMeal.cs
- Scripts/GardenEgg.cs
- Scripts/GardenGame.StatisticsPreview.cs
- Scripts/GardenGame.WildlifeChecks.cs
- Scripts/GardenGame.cs
- Scripts/ImportedChao.Racing.cs
- Scripts/ImportedChao.cs
- Scripts/PetState.Statistics.cs
- Scripts/PetState.cs
- Scripts/PuzzleElement.cs
- Scripts/RaceCourse.cs
- Scripts/RaceTraversal.cs
- Scripts/SaveStore.cs
- Scripts/SportsProgress.cs
- Scripts/StadiumUi.cs
- Scripts/WateringCan.cs
- Scripts/WildAnimal.cs
- Tests/KarateChecks.cs
- Tests/Mori.Tests.csproj
- Tests/Program.cs
- Tests/SportsChecks.cs
- Tests/StatDevelopmentChecks.cs
- Tests/WildlifeChecks.cs
- Tests/check_editor_live.py
- Tests/test_chao_editor_stats.py
- Tools/chao_editor.py
- docs/STAT_DEVELOPMENT.md
