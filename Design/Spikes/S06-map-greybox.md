# S06 — Map greybox

Status: extended 2026-10-10 — size/layout OK (Jakub); now converting the ground to Unity Terrain (D36) before Results. Extension time box: ~1 day.
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
- Dense forest pass (2026-10-08): 1,370 trees (half low-branch spruces), ~3,300 walk-through bushes in 47 chunks.
- Landmarks (2026-10-08): fire tower on the hilltop, radio mast with red light at the station, diner sign, creek + bridge, blue/red/yellow marked trails.
- Lighting: afternoon / dusk / night presets with fog per phase, flashlight, glowing landmark lights.
- Later, optional: drop in S02 radio and S03 tourists on this map.

## Extension: Unity Terrain (D36)

- Convert the Blender ground into a Unity Terrain (same layout, heights, creek valley, hill; terrain size ~350 m, heightmap 513 or 1025).
- Trees onto the terrain as tree instances (two prefabs: tall pine, low spruce) with trunk colliders; bushes as walk-through prefab instances or detail meshes on a sight-blocking layer.
- Buildings, landmarks, road, trails, creek stay as Blender models; road/trails/creek sit on or are carved into the terrain.
- Jakub then reshapes with Terrain Tools: steeper hill, a ridge, rockier areas, deeper creek. Same layout.
- Keep lighting/day-night/fog and the overlay.

## Done when

- [ ] Crossing times measured (walk and sprint): station → diner, station → cave, corner to corner.
- [ ] Layout values agreed (sizes, distances) and written into Results.
- [ ] Top-down screenshot in `Design/Spikes/S06/`.
- [ ] Jakub's verdict on the feel, plus a list of changes for the prototype spec's World section.
- [ ] Ground converted to Unity Terrain; Jakub has reshaped it once and walked it.

## Out of scope

Real art, terrain texturing, props beyond blocks, vehicles, lake, mountains, full town hub.
