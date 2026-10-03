# S02 — Radio range, filter and static

Status: not started
Time box: 4 days
Related: D19 Radios (settled concept), D20 Proximity voice

## Goal

Check that the radio works as designed and reads clearly in play: push-to-talk, filtered audio, range, dead zones, repeater, battery, static near anomalies, and incoming audio overheard by people nearby.

## Questions to answer

1. What filter chain sounds like a cheap park radio without making speech hard to understand?
2. What signal-strength model is simple and readable? Distance only, or distance + terrain blocking + dead-zone volumes?
3. How should weak signal sound: more static, dropouts, or both?
4. Can incoming radio audio play from the radio item in the world (3D) so other players and NPCs nearby hear it?
5. How big should "overheard" range be for tourists to react?

## What to build

- Scene `S02_Radio`: terrain with a hill, one dead-zone volume (a valley), a repeater object on the hill, one placeholder "anomaly" object.
- Voice source: use the Unity `Microphone` class looped back (local test), plus pre-recorded voice clips sent over the network. This keeps the spike independent of the voice SDK choice (S05).
- Push-to-talk on a key. A radio item held by the player.
- Audio chain on an AudioMixer group "Radio": high-pass + low-pass (band around ~300–3400 Hz), distortion, compression, static noise loop mixed in by signal strength.
- Signal strength 0–1 from: distance between radios, line of sight via the repeater, dead-zone volumes. Plot the curve in a debug overlay.
- Anomaly proximity adds static to any radio within range (detector behaviour).
- Battery value that drains while transmitting.
- Incoming audio plays from a 3D AudioSource on the receiving radio. A test NPC within X m logs "overheard".

## Done when

- [ ] Two players talk over radios; audio is clearly "radio" and still understandable at full signal.
- [ ] Signal goes clean → static → cut out over distance along a defined curve.
- [ ] Dead zone cuts signal; repeater restores it.
- [ ] Static rises near the anomaly.
- [ ] Nearby player hears the other end of the conversation through the radio item; NPC "overheard" event fires.
- [ ] Ranges and curve values written into Results.
- [ ] Note for S05: what the voice SDK must support to feed this chain.

## Out of scope

Real voice SDK, code-word system, radio dropping/theft, UI.
