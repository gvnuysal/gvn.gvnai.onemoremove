# Bir Hamle Daha (One More Move)

A turn-based logic puzzle on a small board. Unity **6000.3.25f1**, C#, UI Toolkit, Input System.
Design source: *Bir Hamle Daha — Teknik ve Oyun Analizi v1.0*.

## Architecture

The dependency direction points inward only: `Presentation → Session → Core`. The core does not know Unity.

| Assembly | Responsibility | Unity dependency |
|---|---|---|
| `OneMoreMove.Core` | `RulesEngine.TryMove`, `BoardState`, `LevelDefinition`, `LevelValidator`, `BfsSolver`, `LevelAnalyzer`, `AsciiLevelParser` | none (`noEngineReferences`) |
| `OneMoreMove.Session` | `GameSession` (undo, hint flag), `GameCoordinator`, `ProgressService`, `StarRating`, `HintService`, `ISaveStore` | none |
| `OneMoreMove.Persistence` | Versioned + checksummed JSON save, migrations, atomic write + backup, ordered background queue, `LevelJson` | none |
| `OneMoreMove.Content` | `LevelAsset` / `LevelCatalog` ScriptableObjects | yes |
| `OneMoreMove.Presentation` | `GameBootstrap` (composition root), `BoardView`, `GameplayController`, `AppPresenter`, UI Toolkit screens | yes |
| `OneMoreMove.EditorTools` | Level editor inspector, `CatalogValidator` (release gate), `ProjectSetup` | Editor |

The game, preview, hint, solver and level editor all go through the same `RulesEngine`.

## Commands

```bash
U="/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity"
# Seed levels (JSON) → solved/verified SO assets, catalog, PanelSettings, Main scene
"$U" -batchmode -nographics -projectPath . -executeMethod OneMoreMove.EditorTools.ProjectSetup.RunBatch -logFile -
# Tests
"$U" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults editmode-results.xml
"$U" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode-results.xml
# Player builds (start Unity on the matching -buildTarget to avoid a platform switch); output goes to Builds/
"$U" -batchmode -nographics -projectPath . -buildTarget iOS -executeMethod OneMoreMove.EditorTools.BuildTools.BuildIosSimulator
"$U" -batchmode -nographics -projectPath . -buildTarget iOS -executeMethod OneMoreMove.EditorTools.BuildTools.BuildIos
"$U" -batchmode -nographics -projectPath . -buildTarget Android -executeMethod OneMoreMove.EditorTools.BuildTools.BuildAndroid
"$U" -batchmode -nographics -projectPath . -buildTarget OSXUniversal -executeMethod OneMoreMove.EditorTools.BuildTools.BuildMac
"$U" -batchmode -nographics -projectPath . -buildTarget Win64 -executeMethod OneMoreMove.EditorTools.BuildTools.BuildWindows
# iOS Simulator: build the generated Xcode project, then install it on a booted simulator
xcodebuild -project Builds/iOS-Simulator/Unity-iPhone.xcodeproj -scheme Unity-iPhone -destination "generic/platform=iOS Simulator" -derivedDataPath Builds/iOS-Simulator/dd CODE_SIGNING_ALLOWED=NO build
# Content release gate only
"$U" -batchmode -nographics -projectPath . -executeMethod OneMoreMove.EditorTools.CatalogValidator.ValidateBatch -logFile -
```

In the editor, use the **One More Move** menu for the same actions. Selecting a `LevelAsset` opens the level editor (paint, BFS solve, replay, step-by-step test).

## Controls

Arrows/WASD move, Space/. wait (the d-pad centre button), Z/Backspace undo, R restart, H hint, Esc menu/back.
Touch: swipe on the board to move, tap your own piece to wait, or use the on-screen d-pad. Phones play in portrait;
`ScreenLayout` keeps the UI inside the safe area and fits the board between the HUD rows.

## Content workflow

Level design tool (plain .NET, runs the game's own Core code without Unity): `Tools/LevelLab`.
`dotnet run --project Tools/LevelLab -- analyze` reports every seed level; see [docs/content-plan.md](docs/content-plan.md)
for the 30-level plan, the metrics and the generator.

Seed files are `Content/LevelSource/*.json`. Each one uses either explicit coordinates or a `map` field (`P` player, `E` echo, `G` goal, `#` wall, `o/x` open/closed gate, `O/X` gate+goal).
Setup does not overwrite existing assets. To overwrite, use `-overwriteLevels` or the "Reimport Seed Levels" menu.
If a published level's board changes, increment its `Revision`. The old half-finished session is then discarded and the player is told about it.

## Design note: gate parity and Wait

Every accepted move toggles all gates. With directional moves only, the player always moves exactly one square on a bipartite grid,
so without the echo a gate's passability, seen from next to it, would never change during a level: every gate would be either
always or never passable. **Wait** (rules version 2) fixes this: the player stays put, the turn still passes (gates toggle, the
move counts, the echo mirrors the zero step and stays). One wait shifts every gate's phase by one turn.
`GateParityTests` pins both facts: directional-only play keeps the parity, and a wait flips it.
