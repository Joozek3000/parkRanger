# S01 — Network baseline, object carrying (ragdoll stretch)

Status: not started
Time box: 3 days
Changed 2026-10-03: ragdoll moved to stretch (not in prototype scope); 4 → 3 days.
Related: D26 Networking (provisional)

## Goal

Find an authority model for players and physics objects that feels responsive for the person carrying something and looks smooth for everyone else, with Netcode for GameObjects and a host (no dedicated server).

This spike also builds the network baseline that S02, S03 and S05 reuse: host/join, a first-person player, latency simulation.

## Questions to answer

1. Player movement: owner-authoritative (client moves itself, host accepts) or host-authoritative with prediction? For a co-op party game, owner-authoritative is the likely answer. Confirm it feels right.
2. Carried objects: which of these works best?
   - A. Host simulates everything; client sends "carry" input. Simple, but the carrier feels the lag.
   - B. Ownership moves to the carrier on pickup; returns to host on drop/rest.
   - C. While carried, the object is kinematic and attached to the hand; physics only resumes on drop/throw.
3. (Stretch) Ragdoll: host simulates and syncs a few bones, or sync only the root/pelvis and let each client simulate the limbs locally (cosmetic)?
4. (Stretch) Does getting back up from ragdoll land at the same spot on all screens?

## What to build

- Scene `S01_Carry` in `Unity/Assets/_Spikes/S01/`: flat ground, ramp, a few boxes.
- Network setup: NetworkManager, Unity Transport, simple host/join buttons. Multiplayer Play Mode with 2 players.
- First-person controller: CharacterController, Input System, camera on the head.
- Latency simulation via Unity Transport's debug simulator settings.
- Props: light crate (one hand), trash bag, heavy crate (try two players carrying it; stretch).
- Pick up / carry / drop / throw for each authority model A, B, C. Toggle between them at runtime.
- Stretch — ragdoll: capsule-based humanoid from placeholder art. Trigger on key press and on a fall from the ramp. Get up after 3 s.
- On-screen debug: owner ID, RTT, current authority mode.

## Done when

- [ ] Two players join, move and see each other at 100 ms and 200 ms RTT.
- [ ] Carry model chosen: the carrier sees no visible snap or rubber-banding at 100 ms; the other player sees smooth motion.
- [ ] A thrown crate lands in the same place on both screens (or the difference is too small to notice).
- [ ] (Stretch) Ragdoll triggers and recovers on both screens; final standing position matches.
- [ ] Bandwidth noted for 2 players + 10 props.
- [ ] D26 updated with the chosen authority models.

## Out of scope

Animation, IK hands, many objects, vehicles, Steam.
