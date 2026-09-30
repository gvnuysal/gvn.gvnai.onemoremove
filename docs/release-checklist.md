# Release checklist — Bir Hamle Daha 1.0.0

The design document's release gate (*Yayın kapısı*), made concrete. Items marked **(you)** need an account, a key,
a device or a human decision and cannot be automated here.

## 1. Automated gates (must all pass)

```bash
U="/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity"
"$U" -batchmode -nographics -projectPath . -executeMethod OneMoreMove.EditorTools.ProjectSetup.RunBatch -logFile -   # 30/30 levels proven, no design warnings
"$U" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults editmode-results.xml
"$U" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode-results.xml
dotnet test Server/OneMoreMove.Server.Tests
```

- [ ] Catalog gate: every level structurally valid, known solution replays, optimum proven by a completed search.
- [ ] No critical save loss or rule inconsistency (the document's stop condition).

## 2. Builds

| Platform | Command (`-buildTarget` + `-executeMethod OneMoreMove.EditorTools.BuildTools.…`) | Output |
|---|---|---|
| macOS | `OSXUniversal` + `BuildMac` | `Builds/macOS/BirHamleDaha.app` |
| Windows | `Win64` + `BuildWindows` | `Builds/Windows/BirHamleDaha.exe` |
| Android (Play) | `Android` + `BuildAndroidBundle` | `Builds/Android/BirHamleDaha.aab` |
| iOS | `iOS` + `BuildIos` | Xcode project in `Builds/iOS` |

- Version `1.0.0` lives in `BuildTools.Version`; pass `-buildNumber N` (or `OMM_BUILD_NUMBER`) and increase it for every
  store upload.
- **(you)** Android release signing: create an upload keystore once (keep it and its passwords outside the repo) and
  export `OMM_KEYSTORE`, `OMM_KEYSTORE_PASS`, `OMM_KEY_ALIAS`, `OMM_KEY_PASS` before `BuildAndroidBundle`. Without them
  the bundle is debug-signed and Play rejects it. Enrol in Play App Signing.
- **(you)** iOS signing: open the Xcode project, pick your team (Signing & Capabilities), Product → Archive, upload.
  `ITSAppUsesNonExemptEncryption = NO` is already set by `IosPostBuild`.
- **(you)** Windows: code-sign the exe (or accept SmartScreen warnings for itch.io); macOS: sign and notarize outside
  the App Store (`codesign`, `notarytool`), or ship through the Mac App Store.

## 3. Manual checks per platform (you)

On each target (macOS, Windows, an iPhone, an iPad, an Android phone, a small Android phone):

- [ ] First launch opens the menu; Continue starts level 1.
- [ ] Play a level with keyboard (desktop) or swipes, tap-to-wait and the d-pad (touch). Undo, Restart, Hint work.
- [ ] Quit mid-level, reopen: the half-finished level resumes exactly (save, reopen).
- [ ] Rotate / resize: the board stays between the HUD rows; nothing hides behind a notch or the home indicator.
- [ ] Keyboard only: arrows move the visible focus through every menu, Enter activates, Esc goes back.
- [ ] VoiceOver (iOS) / TalkBack (Android): menus read in order, buttons activate, the board is described, moves are
      announced.
- [ ] Settings: reduced motion, text size 150 %, effect/music volume 0 and 100.
- [ ] Sound on the device speaker is pleasant at 100 % and never the only feedback.

## 4. Player testing and balance (you)

The document asks for independent players before locking revisions. Suggested session: 5–8 players who have not seen
the game, 30–45 minutes each, silent observation.

- [ ] Note where each player stops, restarts or asks for a hint (per level).
- [ ] Watch in particular: 07 (predicting gates), 08 (discovering Wait), 13–15 (echo dead ends, 72–77 % of states),
      26–30 (mastery).
- [ ] Adjust par or order, or redesign; every board change increases that level's `revision`.
- [ ] Re-run the catalog gate and LevelLab `analyze` after changes.

## 5. Server (only if cloud save and world rankings ship in 1.0)

- [ ] **(you)** Choose hosting (Docker on Azure Container Apps, Fly.io or a VPS) and a PostgreSQL database.
- [ ] Set `Server__Database__Provider=Postgres`, `Server__Database__ConnectionString`, a random 64-character
      `Server__SigningKey` and `Server__AdminApiKey` as secrets, never in the repo.
- [ ] HTTPS only (iOS blocks plain HTTP). Put the URL into `GameBootstrap.serverUrl` and rebuild.
- [ ] Back up the database; the leaderboard and cloud saves live there.
- [ ] Leaving `serverUrl` empty ships a fully offline game (no privacy policy entries about the server needed).

## 6. Store listings (you)

- [ ] Texts: `docs/store-listing.md` (TR/EN). Screenshots: `docs/screenshots/` (iPhone 6.9"; take Android/desktop ones
      the same way).
- [ ] Privacy policy published at a public URL: `docs/privacy-policy.md` (review it with whoever is legally responsible;
      KVKK/GDPR). App Store privacy labels / Play Data safety must match it.
- [ ] Age rating questionnaires: no violence, no ads, no purchases, no user-generated content, no chat.
- [ ] Category: Puzzle (Games). Price: decided by you (the document suggests one-time purchase or free intro levels).
