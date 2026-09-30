# Content plan — 30 levels (rules v2, with Wait)

Bands follow the design document (*09 / 30 bölümün içerik planı*). The file prefix in
`Assets/_Project/Content/LevelSource` is the slot; the catalog is ordered by file name, so gaps are fine while
the plan fills up.

| Slot | Band | Level | Status | opt / par |
|---|---|---|---|---|
| 01 | Movement | İlk Adım (`01_first_step`) | shipped | 2 / 3 |
| 02 | Movement | Duvar (`02_walls`) | shipped | 6 / 8 |
| 03–05 | Movement, undo | 3 short mazes | **to do** (step 2) | |
| 06 | Gates | Kapı (`06_gate_tutorial`) | shipped | 4 / 5 |
| 07 | Gates | Kapı Ritmi (`07_gate_rhythm`) | **redesign** (step 2): the gate never costs a move | 4 / 5 |
| 08 | Gates + Wait | Wait tutorial, e.g. `P..xG` | **to do** (step 2) | |
| 09 | Gates + Wait | Aldatıcı Kapılar (`09_misleading_gates`) | redesigned for v2 | 8 / 10 |
| 10 | Gates + Wait | Gate chain capstone | **to do** (step 2) | |
| 11 | Echo | Yankı (`11_echo`) | shipped | 5 / 7 |
| 12 | Echo | Yankıyı Sürükle (`12_drag_the_echo`) | shipped | 8 / 10 |
| 13–15 | Echo | Blocked echo, collisions | **to do** (step 3) | |
| 16 | Gates + echo | Kapı ve Yankı (`16_gate_and_echo`) | par fixed for v2 | 7 / 9 |
| 17–20 | Gates + echo | One critical timing decision | **to do** (step 3) | |
| 21–25 | Long plans | Same mechanics, new layouts | **to do** (step 4) | |
| 26–30 | Mastery | Few moves, several plausible routes | **to do** (step 4) | |

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

## Workflow

```bash
cd Tools/LevelLab
dotnet run -- analyze                          # every seed level
dotnet run -- map "P..xG"                      # one ASCII map
dotnet run -- generate mixed --seed 3 --tries 300000
```

Columns: `opt` optimum · `par` current/suggested · `ways` number of distinct optimal solutions · `wait` / `eblk`
waits and echo blocks in the optimal solution · `noWait` / `noGates` / `noEcho` optimum without that mechanic
(`x` = impossible) · `dead` share of reachable states that can no longer win.

Then add the JSON seed, run `ProjectSetup.RunBatch -overwriteLevels`, and the catalog gate re-proves everything in
Unity.
