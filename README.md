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

## Game server (C#)

`Server/OneMoreMove.Server` is an ASP.NET Core (.NET 10) minimal API that compiles the game's own Core, Session and
Persistence sources, so it verifies runs with the same `RulesEngine`, merges progress with the same `ProgressMerger`
and speaks the same wire format (`CloudDtos`).

| Endpoint | Purpose |
|---|---|
| `POST /api/v1/accounts` | Anonymous device account (rate-limited per IP); returns id + secret (PBKDF2-hashed on the server) |
| `POST /api/v1/sessions` | Secret → JWT access token |
| `GET/PUT /api/v1/progress` | Cloud save: the device's progress is merged (commutative, never lowers a best) and returned |
| `POST /api/v1/levels/{id}/runs` | Submit a run's commands; the server replays them on that level revision and ranks the replayed length |
| `GET /api/v1/levels/{id}/leaderboard` | Top runs per level revision |
| `GET /api/v1/level-packs[/{v}]`, `POST /api/v1/level-packs` | Versioned level packs; publishing (X-Admin-Key) proves every level with the validator and exhaustive solver |

On an empty database the game's seed levels are published as pack 1. SQLite for development and tests, PostgreSQL in
production (`Server:Database:Provider=Postgres`). Secrets come from configuration or environment variables
(`Server__SigningKey`, `Server__AdminApiKey`).

```bash
dotnet run --project Server/OneMoreMove.Server          # http://localhost:5080 (Development settings)
dotnet test Server/OneMoreMove.Server.Tests              # in-memory server, real HTTP, game client included
docker build -f Server/Dockerfile -t onemoremove-server .
```

The game talks to it through `CloudSync` (offline-first: every call returns null without a connection) and
`HttpRemoteService`. Set `GameBootstrap.serverUrl` (HTTPS) to enable cloud save and world rankings; empty keeps the
game fully offline, which is the default.

## Localization (Turkish, English)

- Every UI text is written once in `Strings` as `L("Türkçe", "English")` (`Localization`), so a missing translation is a
  compile error. `UiTexts` writes them into App.uxml (whose Turkish texts are only a UI Builder preview).
- Level names and tips: Turkish in `name`/`tip` (source language), others in `translations`, e.g.
  `"translations": { "en": { "name": "Wall", "tip": "…" } }`. Missing text falls back to Turkish; the catalog gate
  warns when English is missing.
- The language follows the device (Turkish device → Turkish, otherwise English) until the player picks one in
  Settings; the choice is saved and applied to every screen at once.
- `LocalizationTests` fail if an English text contains Turkish letters, i.e. if anything escaped translation.

## Accessibility

- Contrast: UI text ≥ 4.5:1, focus ring and every board element ≥ 3:1 against the floor (WCAG 1.4.11); enforced by
  `AccessibilityPlayModeTests`, which read `Theme.uss` and `Palette`.
- State is never shown by colour alone (player circle, echo diamond, open/closed gate icons, "Kilitli" text, star
  tooltips); no information is carried by sound alone.
- Keyboard/gamepad: menus open with a visible focus; arrows/d-pad move it, Enter/South activates (focus order is tested
  with UI Toolkit navigation events; the key → navigation mapping is Unity's and needs a focused window, so check it
  by hand in the editor).
- Screen readers (VoiceOver, TalkBack): `ScreenReaderSupport` mirrors the visible controls into Unity's accessibility
  hierarchy (buttons, toggles, sliders with increment/decrement, dropdowns), reads the board with `BoardDescriber`
  and announces every move. Modal panels hide what is behind them.
- Reduced motion, text size (100–150 %), separate effect and music volumes, ≥ 44 pt touch targets in portrait.

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
