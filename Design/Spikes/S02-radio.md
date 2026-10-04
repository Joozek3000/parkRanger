# S02 — Radio range, filter and static

Status: not started (next)
Time box: 4 days
Changed 2026-10-04 after S05: uses real Vivox voice instead of mic loopback/clips.
Related: D19 Radios (settled concept), D20 Proximity voice, D28 (Vivox + tap mirroring)

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
- Voice source: real Vivox voice from the S05 setup (`_Spikes/S05`, Shared baseline). The radio replays the proximity tap's audio (S05 mirror workaround). Test tone (J) and distance keys (F5–F8) from S05 for measuring.
- Push-to-talk gates the radio: a player's voice goes out on the radio only while they hold PTT (state synced over NGO). Without PTT, only proximity voice.
- Incoming radio plays from a **3D** AudioSource on each radio item in the world, computed locally on every machine (every machine already has every voice from the group channel). That is what makes "overheard" work for other players and NPCs.
- Filter chain built on plain AudioSources (filters or a per-source mixer group), not tied to Vivox.
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
- [ ] Push-to-talk: voice reaches the radio only while PTT is held; proximity is unaffected.
- [ ] Radio keeps working when the talker's proximity source would stop: keep the proximity tap on an object that stays alive (not the body that can be despawned/disabled), and re-create Vivox taps after a scene load.
- [ ] Tested build to build (Windows + Ubuntu via SSH), not only Editor/MPPM.

## Out of scope

Code-word system, radio dropping/theft, UI polish. (Real voice is now in scope via Vivox.)
