# Park Rangers — Decisions

Last updated: 2026-10-03

> Nothing is final. **Settled** = current direction. **Provisional** = working assumption, expected to be tested or revisited. **Open** = not decided.
> When a decision changes: update the entry, add a line under its **History**, and add a row to the change log at the bottom. Never delete old versions.

---

## Game

### D01 Working title — provisional
"Park Rangers" (placeholder).

### D02 Genre and player count — settled
2–4 player online co-op party game in the vein of Peak / Lethal Company / Overcooked.

### D03 Pillars — settled
1. Chaos through communication. 2. Keep the secret. 3. Funny, with teeth. 4. A park with its own life.
See `pillars.md`.

### D04 Core rule: manage, don't slay — settled
- The chief ranger NPC says monsters are God's creatures; killing them is a sin.
- Lethal tools are not normal equipment.
- Killing can happen by accident, with a heavy penalty.
- Most problems are solved by working out what a creature wants.

### D05 Project priority — settled
Park Rangers is the main project. The mech shooter project is frozen.

---

## Structure

### D06 Shift (~20 min) — settled
Runs afternoon into night, so funny turns into scary.
| Phase | Length | Content |
|---|---|---|
| Briefing | 1 min | Chief ranger gives the day's issues; players grab gear and radios |
| Afternoon | ~8 min | Full of tourists, mostly chores, small funny weirdness, task overload |
| Dusk | ~4 min | Tourists leave (witnesses flee toward town), campers arrive, fire risk, anomalies ramp up. Peak chaos |
| Night | ~7 min | Scary anomalies come out, radios unreliable, night vision needed. Survive, contain, get back to the station |
| Shift report | — | Park rating, Weirdness, pay, chief ranger's verdict. Then the town hub |

### D07 Loss within a shift — settled
Weirdness maxes out, or all rangers are down.

### D08 Season = 7 shifts (one in-game week) — settled
Escalation through the week:
- new anomalies join
- full moon on a set night
- FBI arrives mid-week
- final shift is a federal inspector tour of the park

### D09 Carry-over between shifts — settled
Money, equipment, park damage, monster relationships.

### D10 Between seasons — settled
Unlocks, cosmetics, shuffled anomaly sets.

### D11 Weirdness — settled
Accumulates across the whole season.
- **Raised by:** a tourist leaving with a photo, a witness reaching the diner, the FBI collecting evidence, missing campers.
- **Lowered by:** recovering photos, convincing or bribing witnesses, stealing or swapping FBI evidence at the hotel, planting fake explanations.
- **Stages:** 1 rumors → 2 journalists → 3 FBI opens an official case → 4 news van (season lost).

---

## World

> Not explicitly marked in the original brief. Set to provisional until Jakub confirms.

### D12 Map — provisional
Medium open map, ~3–4 min to cross on foot, under a minute by vehicle.
Zones:
- Forest: lumberjacks, trails, campsites, hooligans, Bigfoot, Baba Yaga's hut
- Mountains: fire towers, hiker rescues, UFO
- Lake: swimmers, boaters, fishing, sirens, Nessie, merfolk
- Town edge / road: traffic, portal in the gas station toilet

### D13 Town hub — provisional
Tool shop, diner (rumor hub; witnesses run there), grocery store, hotel (FBI stays there; rangers sneak in for sabotage). All roads out of the park pass through town.

### D14 Vehicles — provisional
Truck, ATV, boat, maybe a zipline. Kept scarce.

### D15 Fire towers — provisional
Vantage points for spotting smoke, anomalies and camera-holding tourists.

---

## Characters

### D16 Chief ranger — provisional
NPC. Gives briefings and the end-of-shift verdict.

### D17 FBI pair — provisional
Original characters with a believer/skeptic dynamic (NOT Mulder and Scully themselves).
- Believer: easy to distract with fake evidence.
- Skeptic: dismisses weak evidence.

### D18 Men in Black — provisional
An ANOMALY, not the investigators. Uncanny men in suits who harass people with odd questions and behave strangely.

---

## Radios and voice

### D19 Radios — settled (concept)
- Physical item, push-to-talk, filtered audio.
- Range limits and dead zones; a repeater on a peak extends range.
- Battery; can be dropped, lost or stolen.
- Static near anomalies, doubles as a detector.
- Incoming radio audio is audible to anyone nearby. Tourists who overhear get suspicious, so players need code words.

### D20 Proximity voice by default — settled

---

## Presentation

### D21 Camera: first person — settled

### D22 Body cam — provisional
Changed 2026-10-03 (twice).
- Main camera: normal first-person view (D21).
- Body-cam view: an **optional** camera view players can switch to. Not the main view.
- Body-cam features stay: each ranger's body cam records the shift; the shift report plays a highlight reel in body-cam style; a lost camera recovered by the FBI raises Weirdness.
- Not in the prototype. Optional stretch item in S04.

**History**
- 2026-10-03 (2nd): was "optional, all body-cam features moved to Later, not in spikes". Jakub: body-cam features are worth keeping; the body cam is an optional camera view, not the main view.
- 2026-10-03: was "provisional default plan": normal first-person view; each ranger's body cam records the shift; shift report plays a body-cam highlight reel; a lost camera recovered by the FBI raises Weirdness; full body-cam view to be tested in a spike. Changed by Jakub: body cam is an option, not a must. Removed from spike S04.

### D23 Art direction — settled (direction)
- Low-poly models with PSX-style rendering.
- Effects scale with time of day: cleaner afternoon, heavier fog/dither/jitter at night.
- Key objects (tourists with cameras, Bigfoot) need strong silhouettes and colors.

---

## Tech

### D24 Engine: Unity 6.6 (6000.6.4f1), URP — settled
Changed 2026-10-03.
- Note: 6.6 is an Update release, not LTS. Update releases get fixes until the next release rather than long-term support. Upgrade to newer 6.x patches as they come; avoid jumping minor versions mid-prototype unless a fix is needed.

**History**
- 2026-10-03: was "Unity 6.3 LTS". The project had been on 6000.3.2f1 (false "invalid signature" Package Manager bug, fixed from 6000.3.5f2) with a planned upgrade to the newest 6.3 patch. The project was found on 6000.6.4f1; Jakub chose to stay on 6.6. The 6.3 signature-bug workaround no longer applies.

### D25 Art tool: Blender — settled

### D26 Networking — settled (movement + carrying), provisional (rest)
Changed 2026-10-04 after S01.
- **Settled:** Netcode for GameObjects, host-based (no dedicated server).
- **Settled:** player movement is owner-authoritative (each client moves itself).
- **Settled:** physics objects are host-authoritative. Carried objects are kinematic and pinned locally to the carrier on every machine while carried (S01 model C). Physics resumes on the host at drop/throw. The prototype starts the throw locally on the carrier to hide the ~1 round-trip hang.
- Fallback: ownership moves to the carrier (S01 model B). Works without lag but adds a ~1 round-trip pickup delay, and client-thrown objects treat host props as immovable while in flight.
- Ruled out for carried objects: host simulates the carried prop (S01 model A); it trails the carrier by ~1 round trip.
- Measured: ~1.2 kB/s (host in) / ~2.3 kB/s (client in), 2 players + 10 props, NGO payload only.
- Provisional: interest management, AI level of detail.
- Open: NGO scene management on or off. With it off, in-scene props break easily: a prefab save stopped the client spawning them (S01), and builds from a scene not in Build Settings sent hashes no client could resolve, while Editor/MPPM still worked (S05). Decide in the prototype spec: turn it on, or spawn props at runtime. Coordinator leans: spawn props at runtime.

**History**
- 2026-10-04: was "provisional: Netcode for GameObjects, host-authoritative physics, interest management, AI level of detail". S01 settled movement and carrying. Source: `Spikes/S01-carrying-ragdoll.md` Results.

### D27 Unity packages — provisional
Changed 2026-10-03.
Planned: Netcode for GameObjects, Unity Transport, Multiplayer Play Mode, Input System, Cinemachine, AI Navigation, ProBuilder, Splines, Terrain Tools, AI Assistant, Unity Pipeline (`com.unity.pipeline`, for the Unity CLI; see D35).
- Polybrush dropped: deprecated and unsupported from Unity 6.3, so not available on 6.6. Replacements: vertex colour painting in Blender before export; tree/detail scattering with Terrain Tools; ProBuilder for basic vertex colours.
- Removing (not in the plan, nothing depends on them): Unity Version Control (collab-proxy, overlaps with Git), Visual Scripting, AI Inference.
- Keeping: Multiplayer Center (harmless helper).
- Splines (2.9.1) was only installed as a dependency of Cinemachine. Added to manifest.json directly so it stays if Cinemachine changes.
- `com.unity.pipeline` 0.8.0-exp.1 added 2026-10-03 for the Unity CLI. Experimental package: expect breaking changes; editor tooling only.
- Spike-only packages (Dissonance / Vivox / Facepunch.Steamworks / PSX shaders) are installed during their spike, not in setup.

**History**
- 2026-10-03: planned list included Polybrush. Jakub couldn't find it in Package Manager; it is deprecated from Unity 6.3.

### D28 Connection (lobbies/relay) and voice — settled for the prototype
Settled 2026-10-04 after S05 (`Spikes/S05-voice.md` Results).
- Connection for the prototype: **Unity Relay + Lobby** (Multiplayer Sessions API, join codes, anonymous UGS sign-in). Tested build to build, Windows ↔ Ubuntu over the internet, RTT 61 ms. Fallback: Steam (Facepunch; its NGO transport is stale).
- Connection for release: Steam lobbies likely (friend invites). Decide when there is a Steam app ID; swapping means replacing transport + lobby code only.
- Voice: **Vivox**, one group channel (not positional). Proximity = one participant tap per remote player on their body, Unity 3D audio (linear, 1–25 m). Vivox's own output muted so only the taps play. Measured 1.00 / 0.65 / 0.22 / 0 at 2 / 10 / 20 / 30 m on all three test paths.
- Radio: a second AudioSource replays the proximity tap's audio through its own filter chain (two taps per player don't work in Vivox). Risk: the radio goes silent whenever the proximity source stops (player body despawned or disabled, scene load). Fallback: Dissonance (~€55–110 one-time, built-in Audio Cloning).
- Builds carry a commit stamp; joins with a different commit are refused.
- Not yet tested: occlusion, push-to-talk / voice activation / per-player mute UI (Vivox has the APIs). Push-to-talk is in S02.

**History**
- 2026-10-04 (2nd): was "provisional: Relay + Lobby for the prototype, Vivox first, Dissonance fallback". S05 tests passed.
- 2026-10-04: was "open: Steam lobbies vs Unity Relay + Lobby; Dissonance vs Vivox vs Steam voice".

### D29 PSX shaders — provisional
VNTG Shaders (free) first; Retro Shaders Pro (paid) if needed.

### D30 Placeholder art — provisional
Kenney / Quaternius (CC0).

---

## Process

### D31 No Unity building before the prototype spec — settled
Nothing gets built in Unity until the prototype spec (plan step 6) is done, apart from small tech spikes.
- 2026-10-08: the map greybox runs as spike S06 under this rule (Jakub chose a spike over changing D31).

### D32 Prototype scope and time box — provisional
6–8 week time box, networked from day one. Scope in `ideas.md` → Prototype. Finalised in plan step 6.
Success test: is it fun for 10 minutes with 2 people?
Draft spec with defaults for Q1–Q9: `prototype-spec.md` (2026-10-03).

### D33 Workflow — settled
Git + LFS, a simple task board, a weekly devlog.
Repo is local only for now; no remote (2026-10-03). Revisit when backups or collaborators are needed.
Claude Code does hands-on Unity and git work. The coordinator session keeps docs and plans.

### D35 Claude Code tooling — provisional
Added 2026-10-03.
- Claude Code ↔ Unity: **Unity CLI** (1.0.0-beta.12) via `com.unity.pipeline`. The Unity MCP (AI Assistant package) is deprecated by Unity; it is the fallback only if the CLI won't connect. Never enable the in-editor AI gateway.
- Unity official Claude Code plugin (unity@claude-plugins-official, skills only): installed.
- unity-perf: dropped; no source found.
- Blender skills, copied as plain folders into `.claude/skills/` after security review (sources and commits in `Tools/skills-sources.md`):
  - blender-LPM-skill, with the fal.ai and Hunyuan3D scripts removed (one read API keys from `~/.claude.json`)
  - arjun988 `lowpoly-style` and `unity-export` only, not the plugin (its plugin adds an unpinned second Blender MCP on port 9876)
  - blender-game-skills: skipped for now; revisit when characters need rigging
- Blender Lab MCP: configured for the frozen mech project only. Add to parkRanger when real art work starts.
- Test machine: Ubuntu laptop (x86_64) reachable from the PC over SSH (home LAN, key auth, added 2026-10-04). Claude Code uses it for build-to-build tests: copy builds, launch, read Player.log, measure audio. Not exposed to the internet.
- Codex as a 3D modeling assistant (no code work): undecided (2026-10-04). If yes, it gets its own short modeling-only `AGENTS.md` (Art/ rules, Blender backups, export conventions), not a copy of CLAUDE.md.

### D34 Repo folder structure — settled
Changed 2026-10-03.
```
C:\projects\parkRanger\   repo root
  Unity\                  Unity project
  Art\Source\             .blend files, source textures
  Art\Export\             FBX/GLB exports
  Design\                 design docs
  Design\Spikes\          spike specs
  Tools\                  helper scripts, MCP/Blender notes
```
**History**
- 2026-10-03: was a single Unity project at `C:\projects\parkRanger\parkRanger` with no other folders. Unity folder renamed to `Unity`, parent folder became the repo root.

---

## Open design questions

### Q01 Minigame length — open
Short in-shift interruptions (10–30 s) vs longer standalone activities. Likely a mix.

### Q02 Optional body-cam view — open
How players switch to it (per-player toggle, settings option, item?) and whether it has gameplay effects. See D22.

---

## Change log

| Date | ID | Change | Reason |
|---|---|---|---|
| 2026-10-03 | all | Initial version from the coordinator brief | — |
| 2026-10-03 | D24 | Unity 6.3 LTS → Unity 6.6 (6000.6.4f1) | Project already on 6.6; Jakub chose to stay |
| 2026-10-03 | D22 | Body cam: provisional default plan → optional, not in spikes or prototype | Jakub: an option, not a must |
| 2026-10-03 | D22 | Optional → provisional: body-cam view is an optional camera view; body-cam features kept | Jakub changed his mind |
| 2026-10-03 | D27 | Polybrush dropped; collab-proxy, Visual Scripting, AI Inference to be removed | Polybrush deprecated from 6.3; extras unused |
| 2026-10-03 | D28 | Connection method added to the voice spike (S05) | Prototype fun test needs remote players |
| 2026-10-03 | plan | Step 5 split: setup → prototype spec draft → spikes; ragdoll to stretch | Spikes should target what the prototype needs |
| 2026-10-04 | D28 | Settled for the prototype: Relay + Lobby, Vivox with tap mirroring for radio | S05 Results |
| 2026-10-04 | D28 | Relay + Lobby for prototype, Steam likely for release; Vivox first, Dissonance fallback | S05 research; Jakub chose free voice |
| 2026-10-04 | D26 | Movement owner-authoritative; carried objects pinned locally (model C), host physics; B fallback; A out | S01 Results |
| 2026-10-03 | D35 | Claude Code tooling: Unity CLI over deprecated Unity MCP; Blender skills reviewed and copied; unity-perf dropped | Setup Phase 4 review |
| 2026-10-03 | D34 | Repo root moved up one level; Unity project folder renamed to `Unity` | Room for Art, Design, Tools beside the Unity project |
