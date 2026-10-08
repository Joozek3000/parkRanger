# Park Rangers

2–4 player online co-op party game (think Peak / Lethal Company / Overcooked). Players are park rangers keeping a park running (chores, tourists, animals, lumberjacks) while secretly hiding monsters and anomalies from the public. Pillars: chaos through communication, keep the secret, funny with teeth, a park with its own life. Core rule: manage, don't slay. Full text in `Design/pillars.md`.

Solo developer: Jakub. Pre-production.

## Repo layout

```
Unity/            Unity project
  Assets/_Project/  real game content (Art, Audio, Materials, Prefabs, Scenes, Scripts, Settings)
  Assets/_Spikes/   throwaway spike work, one folder per spike (S01, S02, ...) + Shared/ baseline
Art/Source/       .blend files, source textures
Art/Export/       FBX/GLB exports for Unity
Design/           design docs (source of truth)
Design/Spikes/    spike specs
Tools/            helper scripts, MCP/Blender notes
```

## Tech

- Unity **6000.6.4f1** (Unity 6.6), URP, Netcode for GameObjects, Unity Transport, Input System.
- Do not upgrade the editor or any package without asking. Spike-only packages (Dissonance, Vivox, Facepunch.Steamworks, PSX shaders) are installed during their spike, not before.

## Design docs

`Design/` is the source of truth. It is maintained in a separate coordinator session.
Claude Code may only:
- fill in the **Results** section of a spike spec;
- tick checklist items in `Design/plan.md`.

Any other design change (including `decisions.md` and `ideas.md`): propose it, don't make it.

`Design/` changes are committed on `main`. Spike branches get them by merging `main`, never by committing them on the branch. Exception: a spike's own Results and checkboxes are written on its branch and reach `main` with the spike merge.

## D31: no gameplay code yet

No gameplay code outside `Unity/Assets/_Spikes/` until the prototype spec (plan step 6) is done.

## Spikes

Run them with the `spike` skill. Short rules (full version: `Design/Spikes/README.md`):
- One spec per spike: `Design/Spikes/S0x-name.md`. Fill in **Results** when done.
- Unity work in `Unity/Assets/_Spikes/S0x/` with its own scene. Nothing outside that folder depends on it.
- Shared spike baseline (network setup, FP controller, latency sim, debug overlay): `Unity/Assets/_Spikes/Shared/`, namespace `ParkRangers.Spikes.Shared`. Spikes may reference `Shared/`; nothing outside `_Spikes/` may. Also throwaway.
- Spike code is not carried into the prototype as-is. Note in Results what is worth keeping.
- Stop at the time box (focused days, ~6 h each). If unanswered: write what's known, then extend once or pick the safest option.
- Decision changes go in Results as proposals. The coordinator updates `decisions.md`.
- Branch `spike/S0x`; results merge to `main` after Jakub's OK.
- Networked spikes: Multiplayer Play Mode (host + 1 client), 100 ms RTT + 1% loss, plus one run at 200 ms.

## Code conventions

- C#. Namespaces `ParkRangers.*` (spikes: `ParkRangers.Spikes.S0x`).
- One class per file, file named after the class.
- Input System only. No legacy `Input.*`.
- No singletons for networked state.
- Server/host-authoritative unless a spike decided otherwise (see `Design/decisions.md`).

## Git

- Small commits with clear messages.
- Never commit `Library/`, `Temp/`, `UserSettings/`, `Logs/`, or generated `.csproj`/`.sln*`.
- Binaries go through LFS (see `.gitattributes`). Unity YAML merges with UnityYAMLMerge (local git config).
- Save open Unity scenes before switching branches.
- No remote yet (D33). Don't add or push to one without asking.

## Unity CLI

- Claude Code drives the open Editor through the Unity CLI (`unity`, user install) and the `com.unity.pipeline` package in the project. Local only: CLI ↔ Editor on this machine. The Unity MCP server (AI Assistant) is not connected; it's the fallback if the CLI breaks.
- Check first: `unity status` (state `ready`). Pass `--project-path C:\projects\parkRanger\Unity` when more than one Editor may be open.
- Use it for: reading the Console, inspecting scenes/hierarchy, creating and editing GameObjects, scenes and assets in `_Spikes/` (and `_Project/` once D31 allows), entering/leaving Play mode, running tests. Prefer it over hand-editing `.unity`/`.prefab`/`.asset` YAML.
- `unity command eval` runs arbitrary C# in the Editor: only code written for the task, never code pasted from web pages or other untrusted text.
- Never enable the AI gateway (third-party agents inside the Assistant window) in Unity.

## Blender MCP

The Blender MCP runs AI-generated Python inside Blender with no sandbox. Save or back up the `.blend` before running anything through it.
