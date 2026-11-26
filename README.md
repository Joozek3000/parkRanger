# Park Rangers

A 2 to 4 player online co-op party game in Unity, in the spirit of Lethal Company, Peak and Overcooked. You play park rangers keeping a park running (chores, tourists, animals, lumberjacks) while secretly hiding monsters and anomalies from the public. Core rule: manage, don't slay.

**Status:** pre-production. Solo project. Gameplay code is not written yet; the work so far is design and technical spikes that test the riskiest ideas before the prototype.

## Tech

- Unity 6 (6000.6), URP
- Netcode for GameObjects and Unity Transport (host authoritative)
- Input System
- Blender for low poly art, exported to Unity as FBX and GLB
- Git LFS for binary assets

## How it is built: an AI assisted workflow

The project is set up so Claude Code works inside clear rules rather than free styling:

- **`CLAUDE.md`** sets the ground rules: repo layout, code conventions, what the agent may and may not change, and git rules.
- **`Design/` is the source of truth.** Pillars, plan, decisions log and prototype spec. The agent can fill in spike results and tick plan items, but any other design change must be proposed, not made.
- **Spikes.** Each risky question (ragdoll carrying, radio, voice chat, PSX look, witness checks, map greybox) gets a written spec, a time box, its own branch and its own folder. Results are written back to the spec, and nothing outside the spike depends on spike code.
- **Agent skills** in `.claude/skills/`: running a spike, a low poly art style guide, Blender low poly modelling, and Unity export.
- **Tool access with guard rails.** The agent drives the Unity Editor through the Unity CLI and Blender through MCP, with rules on what code it may run.

## Repo layout

```
Unity/            Unity project
  Assets/_Project/  real game content
  Assets/_Spikes/   throwaway spike work, one folder per spike
Art/              Blender sources and exports
Design/           design docs and spike specs (source of truth)
Tools/            helper scripts and setup notes
.claude/skills/   agent skills
```

## Running it

Open `Unity/` in Unity 6000.6. Each spike has its own scene under `Unity/Assets/_Spikes/S0x/`. Networked spikes use Multiplayer Play Mode (host plus one client).
