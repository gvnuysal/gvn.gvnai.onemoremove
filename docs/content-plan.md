# Content plan — 30 levels (rules v2, with Wait)

Bands follow the design document (*09 / 30 bölümün içerik planı*). The file prefix in
`Assets/_Project/Content/LevelSource` is the slot; the catalog is ordered by file name, so gaps are fine while
the plan fills up.

| Slot | Band | Level | Status | opt / par |
|---|---|---|---|---|
| 01 | Movement | İlk Adım (`01_first_step`) · tip | shipped | 2 / 3 |
| 02 | Movement, undo | Duvar (`02_walls`) · tip | shipped | 6 / 8 |
| 03 | Movement | Dolambaç (`03_detour`): goal 3 away, path 5 | shipped | 5 / 7 |
| 04 | Movement | Engel (`04_barrier`): same row, wall between | shipped | 8 / 10 |
| 05 | Movement | Sarmal (`05_spiral`): 12-move spiral to a goal 4 away | shipped | 12 / 15 |
| 06 | Gates | Kapı (`06_gate_tutorial`) · tip | shipped | 4 / 5 |
| 07 | Gates | Kapı Ritmi (`07_gate_rhythm`) · tip: predict the gate on arrival. The open-looking gates are always closed when reached, the closed-looking one is open; Wait gives nothing | shipped (rev 2) | 7 / 9 |
| 08 | Gates + Wait | Bekle (`08_wait`, `P..xG`) · tip: the Wait control | shipped | 5 / 7 |
| 09 | Gates + Wait | Aldatıcı Kapılar (`09_misleading_gates`): wait through the chain (8) vs detour (11) | shipped | 8 / 10 |
| 10 | Gates + Wait | Kapı Ustalığı (`10_gate_mastery`): three timed waits across four gates (12) vs the natural detour (13) | shipped | 12 / 15 |
| 11 | Echo | Yankı (`11_echo`) · tip: the echo mirrors you | shipped | 5 / 7 |
| 12 | Echo | Yankıyı Sürükle (`12_drag_the_echo`) · tip: a blocked echo stays put | shipped | 8 / 10 |
| 13 | Echo | Hedefi Koru (`13_guard_the_goal`) · tip: the echo sits next to the goal; one wrong route puts it on the goal (dead 0.77, undo) | shipped | 6 / 8 |
| 14 | Echo | Yankıyı Kaydır (`14_shift_the_echo`): a left-right wiggle against a wall repositions the echo (echo costs 2) | shipped | 7 / 9 |
| 15 | Echo | Gel-Git (`15_back_and_forth`): the same idea vertically and longer (echo costs 4) | shipped | 10 / 13 |
| 16 | Gates + echo | Kapı ve Yankı (`16_gate_and_echo`) · tip: closed gates stop the echo too | shipped | 7 / 9 |
| 17 | Gates + echo | Zamanlama (`17_timing`): two waits plus an echo wiggle; gates cost 2, echo costs 4 | shipped | 8 / 10 |
| 18 | Gates + echo | Yankı Tuzağı (`18_echo_trap`): the echo costs nothing but punishes a wrong order | shipped | 9 / 12 |
| 19 | Gates + echo | Yankı Kapısı (`19_echo_gate`): unsolvable if gates were floor; a closed gate must hold the echo | shipped | 11 / 14 |
| 20 | Gates + echo | Bekçi (`20_the_warden`): the echo guards the goal; 3 moves without it, 14 with it | shipped | 14 / 18 |
| 21–25 | Long plans | Same mechanics, new layouts | **to do** (phase 1c) | |
| 26–30 | Mastery | Few moves, several plausible routes | **to do** (phase 1c) | |

## Design rules learned from LevelLab

- **Gate-only levels have no dead ends** once Wait exists. A wait always fixes the gate phase, so the challenge is
  efficiency (stars), not solvability: waiting versus detouring (`09`: waiting through the gate chain is 8, the
  detour is 11). Real traps (`dead > 0`) come from the echo.
- A chain of equally-phased gates is crossed as *wait, step, wait, step…*. Use it once as a teaching beat, not as
  the main idea of many levels.
- **Par** = `opt + ceil(opt / 4)`. Every level shipped before v2 already followed this; LevelLab prints it as the
  suggested par.
- Check that each mechanic in a level costs moves: `noGates`, `noEcho` and `noWait` should differ from `opt`,
  otherwise that mechanic is decoration (the old `04/07` gate).
- Levels before 08 must not reward waiting (`noWait == opt`): Wait is visible from level 1 but only taught at 08.
  LevelLab's `gate-rhythm` profile enforces this.
- `tip` (optional, JSON) is a one-line teaching text shown in the HUD while the level is played. Use it where a
  control or rule is introduced, not as a hint.

## Workflow

```bash
cd Tools/LevelLab
dotnet run -- analyze                          # every seed level
dotnet run -- map "P..xG"                      # one ASCII map
dotnet run -- generate mixed --seed 3 --tries 300000
dotnet run -- generate moves --min 8 --max 8     # override the optimum range of a profile
```

Columns: `opt` optimum · `par` current/suggested · `ways` number of distinct optimal solutions · `wait` / `eblk`
waits and echo blocks in the optimal solution · `noWait` / `noGates` / `noEcho` optimum without that mechanic
(`x` = impossible) · `dead` share of reachable states that can no longer win.

Then add the JSON seed, run `ProjectSetup.RunBatch -overwriteLevels`, and the catalog gate re-proves everything in
Unity.
