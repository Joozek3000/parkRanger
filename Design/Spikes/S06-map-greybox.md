# S06 — Map greybox

Status: in progress (started 2026-10-08)
Time box: 3 days
Related: D31 (greybox runs as a spike, not a change to D31), D12 Map, prototype spec (World), D19/S02 radio range, S03 witness check

## Goal

Get the feel of the prototype map's scale and layout before the prototype spec is final. Walk it in first person at real speed.

## Questions to answer

1. How long does it take to cross on foot (walk / sprint)? Target from the spec: ~1–1.5 min.
2. Is the diner far enough from the forest that a fleeing tourist can be chased and caught, but not so far it's trivial?
3. Can tourists on the road see into the forest? Where are the good hiding spots for Bigfoot?
4. Do the chores (feeding stations, trash spots) pull players apart enough to need the radio?
5. Does the radio range (S02: clean to 120 m, cut ~230 m) plus the valley/hill make splitting up meaningful?
6. Is the forest density right: dense enough to hide, open enough to move and see?

## What to build

- Blender starter blockout (coordinator generated it via the Blender connection): `Art/Source/map_greybox_v01.blend`. Jakub edits it.
  - ~350 × 350 m ground with a hill, a valley and gentle noise; flat road corridor.
  - Ranger station, road, town edge with diner + parking + gas station, Bigfoot cave, 3 feeding stations, 4 trash spots, forest with clearings, a 1.8 m player-scale reference.
  - Flat colour materials per type; one collection per type.
- Export to `Art/Export/` (FBX), import into Unity as scene `_Spikes/S06/S06_Map` (in Build Settings), walkable with the Shared FP controller, 2 players in MPPM.
- Overlay: player position, distance to station / diner / cave, a stopwatch for crossing times.
- Later, optional: drop in S02 radio and S03 tourists on this map.

## Done when

- [ ] Crossing times measured (walk and sprint): station → diner, station → cave, corner to corner.
- [ ] Layout values agreed (sizes, distances) and written into Results.
- [ ] Top-down screenshot in `Design/Spikes/S06/`.
- [ ] Jakub's verdict on the feel, plus a list of changes for the prototype spec's World section.

## Out of scope

Real art, terrain texturing, props beyond blocks, vehicles, lake, mountains, full town hub.
