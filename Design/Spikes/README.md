# Spikes

Spikes answer one technical question each. They are throwaway: the goal is a decision, not reusable code.

## Rules

- One spec file per spike: `S0x-name.md`. Fill in **Results** when done.
- Unity location: `Unity/Assets/_Spikes/S0x/` with its own scene. Nothing outside that folder depends on it.
- Shared spike baseline (from S01): `Unity/Assets/_Spikes/Shared/`, namespace `ParkRangers.Spikes.Shared`. Network setup, first-person controller, latency simulation, debug overlay. Other spikes may reference `Shared/`; nothing outside `_Spikes/` may. Also throwaway.
- Code from a spike is not carried into the prototype as-is. Rewrite it, or mark in Results which parts are worth keeping.
- Stop at the time box. If it isn't answered, write down what's known and decide: extend once, or pick the safest option and move on.
- A spike that changes a decision lists the proposed change in Results. The coordinator session updates `decisions.md` and `ideas.md`; Claude Code does not edit them.
- Spike work happens on branch `spike/S0x`. Results must end up on `main` (merge after Jakub's OK) so the coordinator sees them.
- Time boxes are in **focused days** (~6 h of work). Convert to evenings as needed.
- Test networked spikes with Multiplayer Play Mode (host + 1 client) and simulated latency: 100 ms round trip, 1% packet loss, plus one run at 200 ms. Use the Shared baseline's F1/F2/F3 presets (Unity Transport's own debug simulator is obsolete in this NGO version). Host in the main Editor, Join in Player 2.
- A networked spike isn't done until it has run **build to build** (not only Editor/MPPM). S05 showed the Editor can hide bugs that break every real build.
- Every networked spike scene must be in Build Settings. With NGO scene management off, in-scene NetworkObjects only get their prefab hash recorded for scenes in Build Settings.
- Builds show a commit stamp; joins with a different commit are refused.

## Order (suggested)

Biggest risk first. Setup (plan 5a) and the prototype spec draft (5b) come before any spike.
S04 is undecided: 1-day check, full spike, or after the prototype. It has no dependencies.

| # | Spike | Time box | Depends on |
|---|---|---|---|
| S01 | Network baseline + object carrying (ragdoll = stretch) | 3 days | setup done |
| S05 | Connection (Steam lobbies vs Unity Relay) + voice chat | 4 days | S01 baseline |
| S02 | Radio range, filter and static | 4 days | S01 baseline; S05 for real voice |
| S03 | Tourist line-of-sight witness check | 3 days | S01 baseline |
| | **Total (prototype spikes)** | **14 days** | |
| S04 | PSX look (+ optional body-cam view, stretch) | 1–3 days | — |

## Results template

```
## Results (YYYY-MM-DD)
- Answer:
- Numbers measured:
- What surprised me:
- Keep (code/assets worth carrying over):
- Decisions updated: Dxx
- Follow-up ideas → ideas.md:
```
