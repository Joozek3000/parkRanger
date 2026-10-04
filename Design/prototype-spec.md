# Park Rangers — Prototype spec

Status: **DRAFT (plan step 5b)**. Finalised in step 6 after the spikes.
Q1–Q9 set to defaults on 2026-10-03 (Jakub: "carry on"). Marked **[default]**; change any of them at any time.
Last updated: 2026-10-03

> Nothing is final. **[default]** = coordinator's default, not yet confirmed by Jakub. **[S0x]** = waits on a spike result.

## Purpose

Answer one question: **is it fun for 10 minutes with 2 people?**
Everything in the prototype exists to test pillars 1 and 2 (chaos through communication, keep the secret). Pillars 3 and 4 only as far as they come for free.

## Constraints

- Time box: 6–8 weeks, starting after the 5c spikes. (D32)
- Networked from day one. 2 players, on two machines in different places. (D32, S05)
- Placeholder art (Kenney / Quaternius). Look: PSX only if S04 has been done; otherwise plain URP.
- Code goes in `Unity/Assets/_Project/`. Spike code is rewritten, not copied. (D31)

## Session shape

The full shift is ~20 min (D06). The prototype uses a shorter shift so a test round fits the 10-minute success test.

| Phase | Full game | Prototype (draft) |
|---|---|---|
| Briefing | 1 min | 30 s, text on screen (no chief ranger NPC) |
| Afternoon | ~8 min | ~5 min |
| Dusk | ~4 min | ~2 min (tourists head out, witnesses flee) |
| Night | ~7 min | ~3 min (only if the stretch night threat is built; otherwise end at dusk) |
| Report | — | end screen with numbers |

**[Q1 default]** Yes: ~10-minute prototype shift.

## IN — minimum version of each item

### World
- One forest area, a road, and the town edge with the **diner as a trigger zone**.
- A ranger station: spawn point, where radios and items are picked up. (Not in the original list; needed as a start/return point.)
- **[Q2 default]** Size: ~1–1.5 min to cross on foot. (Full map: 3–4 min.)

### Players [S01, S05]
- 2 players, first person, walk/run/jump.
- Pick up, carry, drop, throw objects.
- Proximity voice.

### Radios [S02, S05]
- Push-to-talk, filtered audio, range falloff with static, one dead zone.
- Static near Bigfoot (detector).
- Incoming audio audible to nearby tourists → they get suspicious.
- Not in the prototype: battery, repeater, dropping/stealing. **[Q3 default]** Repeater stays out.

### Tourists [S03]
- 6–10 tourists that wander between points of interest.
- Witness check: notice → take photo → flee to the diner.
- Overhearing radio talk makes them suspicious (easier to trigger a witness).
- **[Q4 default]** Players can stop a fleeing tourist before the diner: catch up, hold a key for ~2 s ("it was a bear" line). The photo is gone and the tourist calms down. No minigame.

### Bigfoot
- One Bigfoot that wanders the forest and drifts toward tourists/food.
- Herding: **food** lures it (it follows dropped food); the **inflatable decoy** holds it in place while it's nearby.
- **[Q5 default]** Herding goal: move it to a hidden spot (a cave). It stays calm there for a few minutes, then wanders out again. Gives a clear goal and repeats the pressure.

### Chores
- Feeding: carry feed to 2–3 feeding stations.
- Trash: pick up bags around the area, carry to a bin.
- **[Q6 default: D = A + B]** Ignored trash/feed draws Bigfoot toward that spot (B), which tends to be where tourists are; chores done/missed also shows as a park rating on the end screen (A). No pay in the prototype.
- Original question: what happens if chores are ignored? Chores are the pressure that pulls players away from the secret (pillar 2), so ignoring them must cost something. Options:
  - A. a "park rating" number on the end screen only
  - B. ignored trash/feed attracts Bigfoot or animals toward tourists
  - C. the chief ranger nags over the radio (text/voice line) and docks pay
  - D. a mix

### Weirdness
- One number. Goes up when a tourist with a photo reaches the diner.
- Loss: Weirdness hits max.
- **[Q7 default]** Only goes up. Stopping a fleeing tourist (Q4) prevents the increase; nothing lowers it.

### Time of day
- Afternoon → dusk → night lighting shift over the shift.

### End of shift
- Win: shift ends with Weirdness below max.
- End screen: Weirdness, tourists escaped/stopped, chores done.

### Stretch
- One simple night threat. **[Q8 default]** Shadow people: dark silhouettes need almost no art and work with radio static and darkness.

## OUT (for the prototype)

Vehicles, 3–4 players, FBI, hotel, town buildings except the diner, chief ranger NPC, money and shop, seasons, Weirdness stages, other anomalies, minigames, fire, campers, radio battery/repeater/theft, body cam, PSX look (unless S04 done), cosmetics, lobby UI polish.

## Unknowns → spike

| Unknown | Spike |
|---|---|
| Carry/throw authority model, network feel | S01 — **done**: model C (D26) |
| NGO scene management on/off (in-scene props fragile with it off; broke all S01 builds in S05) | Prototype spec decision (from S01/S05). Lean: spawn props at runtime |
| How two remote players connect; voice solution | S05 — **done**: Relay + Lobby, Vivox (D28) |
| Radio filter chain, range curve, overheard range | S02 |
| Witness check cost and feel; day vs night detection | S03 |
| Prototype look | S04 (undecided) |
| Bigfoot herding feel (food/decoy) | **[Q9 default]** No spike. Built in weeks 1–2 of the prototype; if it isn't fun by end of week 2, stop and rethink before adding more. |

## Fun test

- 2 players on separate machines, voice on. 3 sessions minimum, with at least one player who isn't Jakub.
- Record each session (screen + voice).
- After each round, ask:
  1. Did you laugh? When?
  2. Did the radio matter, or did you just stay together?
  3. Did you split up? Why?
  4. What did you want to do that you couldn't?
  5. Do you want another round right now?
- Pass: players want another round, there's at least one "story" moment per session, and they split up and used the radio to coordinate.
- Fail → look at which pillar didn't show up before adding features.

## Rough milestones (provisional)

| Weeks | Focus |
|---|---|
| 1–2 | Greybox map, players, carrying, radios, Bigfoot + herding |
| 3–4 | Tourists + witness, Weirdness, diner, chores |
| 5 | Shift timer, lighting shift, end screen, full loop |
| 6 | Playtests, fixes |
| 7–8 | Buffer / stretch night threat / second playtest round |
