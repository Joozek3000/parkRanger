# S03 — Tourist line-of-sight witness check

Status: not started
Time box: 3 days
Related: D11 Weirdness, Pillar 2 (Keep the secret), Pillar 4 (A park with its own life)

## Goal

Find a cheap, reliable way for many tourists to notice an anomaly, take a photo and flee to the diner, in a way players can read and play against.

## Questions to answer

1. Which check gives believable results without false positives through trees and rocks?
2. How many tourists can run the check before it costs too much on the host?
3. Should seeing be instant or build up over time (exposure meter)? Build-up gives players a window to react.
4. How should darkness and distance reduce detection?
5. Can a ranger block a tourist's view by standing in the way? (Test only; whether it becomes a mechanic is a design call.)

## What to build

- Scene `S03_Witness`: a forest patch (placeholder trees with colliders), rocks, a road, a "diner" trigger zone.
- NavMesh (AI Navigation). Tourist NPC wanders between waypoints.
- Witness check on the host only, every N frames, staggered across tourists:
  1. distance check
  2. view-cone angle check
  3. 2–3 raycasts to points on the target (head, centre, feet) against an occluder layer
- Exposure meter per tourist: rises while the anomaly is visible (faster when close, slower in the dark), decays when not.
- States: wander → notice → photo (short window, a ranger can interrupt) → flee to diner. Weirdness +1 when a fleeing tourist reaches the diner.
- State synced to clients with NetworkVariables.
- Debug gizmos: view cones, rays, meter value, state label.
- Stress test: 30 tourists + 3 anomalies. Measure host cost in the Profiler.

## Done when

- [ ] A tourist reliably notices Bigfoot in the open and does not notice it behind a rock or a thick group of trees.
- [ ] Photo → flee → diner → Weirdness works end to end, on host and client.
- [ ] A player can tell what a tourist is about to do (state is readable from animation/icon/sound).
- [ ] Host cost for 30 tourists measured; check frequency chosen.
- [ ] Day vs night detection values written into Results.

## Out of scope

Tourist animation, photo recovery, witness talk-down minigame, crowds above 30.
