---
name: spike
description: Run a Park Rangers tech spike from its spec in Design/Spikes (e.g. "run spike S01", "/spike S02"). Builds it in Unity/Assets/_Spikes/S0x on branch spike/S0x, checks Done-when items with Jakub, writes Results, merges to main.
---

# Run a spike

Argument: spike ID, e.g. `S01`. If missing, ask.

## 1. Read

- `Design/Spikes/README.md` (rules, results template).
- `Design/Spikes/S0x-*.md` (the spec). Note the time box, dependencies, "What to build", "Done when", "Out of scope".
- If a dependency spike has no Results yet, say so and ask whether to continue.
- Summarise the plan in a few lines and wait for Jakub's OK.

## 2. Branch

- Ask Jakub to save open scenes in Unity before any `git switch`. Wait for confirmation.
- `git switch -c spike/S0x` from `main` (or `git switch spike/S0x` if it exists).

## 3. Set up

- Create `Unity/Assets/_Spikes/S0x/` with its own scene (name from the spec, else `S0x_Main`).
- Namespace `ParkRangers.Spikes.S0x`. Nothing outside `_Spikes/S0x/` may reference it.
- Install spike-only packages only if the spec calls for them, and ask first.

## 4. Build

- Work through "What to build" in order. Stretch items last.
- Commit small steps on the branch.
- Track time against the time box (focused days, ~6 h each). Ask Jakub for hours spent at each session start; report used vs. remaining.
- At ~75% of the time box: warn Jakub, say what's left, and suggest cuts if it won't fit.
- At the time box: stop. Write what's known and ask: extend once, or pick the safest option.

## 5. Check "Done when"

- Go through each item with Jakub. Jakub tests in Unity (Multiplayer Play Mode for networked spikes: 100 ms RTT + 1% loss, plus one run at 200 ms).
- Tick only the items Jakub confirms.

## 6. Results and merge

- Fill in **Results** in the spec using the README template. Allowed edit to `Design/`: the Results section and the spec's checkboxes.
- Decisions that need changing: list them under "Decisions updated" as proposals. Do not edit `decisions.md` or `ideas.md`; the coordinator does.
- Commit on `spike/S0x`.
- Results must end up on `main`, because the coordinator reads `Design/` from `main`. After Jakub has marked the Done-when items, ask Jakub to OK merging `spike/S0x` into `main`. It's safe: nothing outside `_Spikes/S0x/` depends on it. Ask Jakub to save open scenes first, then `git switch main` and `git merge --no-ff spike/S0x`.
- Report: answer, numbers, proposed decision changes, what's worth keeping.
