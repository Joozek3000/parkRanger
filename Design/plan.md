# Park Rangers — Plan

Last updated: 2026-10-03

> Living plan. Steps and order can change. Changes to decisions go in `decisions.md`.

Status: **pre-production**. No Unity building until step 6 is done, apart from spikes (D31).

## Pre-production steps

| # | Step | Status |
|---|---|---|
| 1 | Idea dump | Done (keep adding to `ideas.md`) |
| 2 | Pitch and pillars | Done |
| 3 | Core loop | Done (draft level) |
| 4 | Sort ideas | Done |
| 5 | Tech setup and spikes (5a–5d below) | **Next: 5a** |
| 6 | Prototype spec: final in/out list, time box, fun test | Not started |
| 7 | Rough roadmap (provisional) | Not started |

## Step 5 — Tech setup and spikes

Changed 2026-10-03: setup first, then a prototype spec draft, then only the spikes the prototype needs.

| Step | What | Time | Status |
|---|---|---|---|
| 5a | Setup: Git + LFS, Claude Code setup, CLAUDE.md, Unity housekeeping. Prompt: `Tools/cc-setup-prompt.md` | ~1 day | **Next** |
| 5b | Prototype spec draft: in/out list from `ideas.md` → Prototype, list of unknowns | ~0.5 day | Not started |
| 5c | Spikes for the prototype (below) | ~14 days | Specs written |
| 5d | S04 PSX look: 1-day check now, full spike, or after the prototype | 0–3 days | **Undecided** |

Spike order and rules: `Spikes/README.md`.

| # | Spike | Spec | Time box | Status |
|---|---|---|---|---|
| S01 | Network baseline + object carrying (ragdoll = stretch) | `Spikes/S01-carrying-ragdoll.md` | 3 days | Spec written |
| S05 | Connection (Steam lobbies vs Unity Relay) + voice chat | `Spikes/S05-voice.md` | 4 days | Spec written |
| S02 | Radio range, filter and static | `Spikes/S02-radio.md` | 4 days | Spec written |
| S03 | Tourist line-of-sight witness check | `Spikes/S03-witness-check.md` | 3 days | Spec written |
| S04 | PSX look (+ optional body-cam view as stretch) | `Spikes/S04-psx-look.md` | 1–3 days | Undecided |

## Setup checklist

### Repo and tools
- [x] Repo folder structure (D34)
- [x] Git + LFS init, `.gitignore`, `.gitattributes` (Claude Code, 5a); first commit done
- [ ] Simple task board (where?)
- [x] Unity official Claude Code plugin: installed (unity@claude-plugins-official 0.1.6-beta, skills only, no MCP). Restart Claude Code to load its skills.
- [x] ~~unity-perf~~ dropped: no source found (D35)
- [x] Unity CLI connected via `com.unity.pipeline` (D35); Unity MCP is fallback only
- [x] Git identity: repo-local only (no remote for now)
- [x] CLAUDE.md + `spike` project skill saved (setup Phase 3)
- [x] Setup Phase 5 (first commit), after the Unity CLI connects
- [ ] Remove the leftover empty `C:\projects\parkRanger\parkRanger` folder after this Cowork session ends (its old folder connection recreates it)

### Unity project housekeeping
- [x] ~~Add Polybrush~~ dropped: deprecated from Unity 6.3 (D27)
- [x] Remove collab-proxy, Visual Scripting, AI Inference (D27, 5a)
- [x] Add Splines 2.9.1 to manifest.json directly (D27)
- [x] Remove TutorialInfo + Readme.asset; create `Assets/_Project/` and `Assets/_Spikes/` (5a)
- [ ] Remove SampleScene and Mobile renderer assets when the first real scene exists

### Blender
- [x] Blender Lab MCP add-on v1.0.3 installed, connection tested (localhost:9876)
- [ ] Backups in place before using the MCP (it runs AI-generated code unguarded)
- [x] Blender skills reviewed; LPM (trimmed), lowpoly-style, unity-export copied; blender-game-skills skipped (D35)

### Art pipeline
- [ ] Try VNTG Shaders (free); Retro Shaders Pro if needed (D29), covered by the PSX spike
- [ ] Pull placeholder packs: Kenney / Quaternius (D30)
