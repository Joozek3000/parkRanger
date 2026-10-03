# S04 — PSX look

Status: undecided (1-day check now, full 3-day spike, or after the prototype)
Time box: 1–3 days
Related: D21 First person, D22 Body cam (optional view), D23 Art direction, D29 PSX shaders

## Goal

Confirm the PSX look works on Unity 6.6 URP, scales from afternoon to night, and keeps key objects readable.

## Questions to answer

1. Does VNTG Shaders (free) work on Unity 6.6 / URP 17.6 (Render Graph)? If not, does Retro Shaders Pro?
2. Which settings give "clean afternoon" vs "heavy night" (fog, dither, vertex jitter, resolution)?
3. Can you still spot a camera-holding tourist and Bigfoot at 30 m and 60 m, day and night?
4. What does it cost in frame time?

## What to build

- Scene `S04_Look`: forest patch with Kenney/Quaternius placeholder assets, a tourist with a camera, a Bigfoot block-out, a ranger station.
- Install VNTG Shaders. Apply: vertex snapping, affine texture mapping, low-res render target, dithering, fog.
- Two Volume profiles: Afternoon and Night. A slider that blends between them.
- Night vision test: simple green post-process on a key.
- Stretch: optional body-cam camera view (barrel distortion, slight chromatic aberration, timestamp + ranger ID overlay), toggled on a key. Normal first person stays the main view (D22).
- Screenshots saved to `Design/Spikes/S04/`.

## Done when

- [ ] PSX look running on 6.6 URP, or a decision to switch to Retro Shaders Pro (D29 updated).
- [ ] Afternoon and night presets saved; blend works.
- [ ] Readability check done at 30 m / 60 m, day and night; strong-silhouette notes for tourists and Bigfoot.
- [ ] GPU frame time noted for each preset.
- [ ] (Stretch) Body-cam view toggle tried; notes for Q02.

## Out of scope

Body-cam recording/highlight reel (Later), final art, terrain.
