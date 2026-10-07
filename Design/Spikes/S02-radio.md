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

## Results (in progress, 2026-10-07: fix round after the first automated run)

Builds: e714549 (Windows + Linux). Measured Windows ↔ Windows on one PC over Relay (two builds, WASAPI loopback, 440 Hz tone injected); the Ubuntu laptop was offline, so no Linux or cross-OS run in this round. "H" = host, "C" = client; the listener stands 60 m away (radio only, no proximity).

- Answer (so far):
  - Chop (radio mirror): **mostly fixed, small residual.** Causes found and fixed: (1) the mirror source didn't loop, so it stopped at the end of the ~3 s tap clip and restarted (a gap every ~3 s); (2) it sat a few hundred samples behind the tap, in audio Vivox had already silenced (10 ms gaps every ~40 ms); now it stays 256 samples ahead; (3) the proximity tap went virtual beyond 25 m, its position then slipped a DSP buffer (1024 samples) against the mirror and Vivox silenced audio the mirror hadn't played yet; the tap rolloff now floors at −60 dB so it stays a real voice (`tapvirtual False` in 1,656 diag lines). Mirroring moved to LateUpdate (the Vivox tap coroutine runs after Update and can jump its read pointer). Residual: isolated ~15 ms gaps, a one-buffer slip (drift −768 / 1280 seen) whose cause isn't found yet. Not the old regular stutter.
  - Weak signal: signal gain now applied **after** the compressor (new S02Gain at the end of the chain); before, the compressor made the drop back up (old build: 170 m only −4 % vs 120 m). Now the voice really drops (table below). Static rises with distance as designed, but its level is low; whether it reads clearly is for the ear test.
  - PTT end: the radio now keeps playing after PTT-off until the talker's tap has been quiet 150 ms, max 1 s. Measured: no cut-off (old build cut up to 459 ms).
  - Windows garble ~3 s after scene reload: not reproduced in 4 runs (8 reloads, both directions): no gaps 0–8 s after the reload. Probably cause (1) above: after a reload the mirror ran off the end of the new 3 s tap clip.
- Numbers measured:
  - Chop, gaps > 5 ms in the radio output (1 ms envelope of the 440 Hz tone, 2 s presses):

    | Build | H→C | C→H | Notes |
    |---|---|---|---|
    | 362f2a7 (before) | — | 33 gaps in one 5 s window (120 m) | regular, every ~40 ms |
    | 9453275 (loop + lead) | 0 / 32 presses | 12 gaps / 32 | |
    | 08e287e (+ LateUpdate) | 0 / 40, then 11 / 32 | 1 / 40, then 5 / 32 | |
    | c79eae8 (+ tap never virtual) | 0 / 32 | 1 / 32 (start transient) | |
    | a5e25a5 (mirror always running) | 12 / 40 | 2 / 40 | reverted |
    | **e714549 (final = c79eae8 code)** | **12 gaps in 8 / 32 presses** | **0 / 32** | each gap 13–15 ms |
    | Control: proximity only, 2 m, no radio | 0 / 32 (×3 runs) | 0 / 32 (×3 runs) | upstream stream is clean |

  - Weak signal, final build, listener's speakers (440 Hz tone RMS / rest RMS in 300–3400 Hz). Gain = signal-strength gain on the voice. H→C "rest" has unrelated desktop audio under it (0.06 with nothing playing); C→H is clean.

    | Distance | Signal | Voice gain | Static vol | H→C tone / rest | C→H tone / rest | Static alone, C→H (PTT, no voice) |
    |---|---|---|---|---|---|---|
    | 60 m | 1.00 | 1.00 | 0.03 | 0.236 / 0.111 | 0.250 / 0.041 | 0.0012 |
    | 120 m | 1.00 | 1.00 | 0.03 | 0.241 / 0.100 | 0.253 / 0.002 | 0.0012 |
    | 170 m | 0.50 | 0.75 | 0.21 | 0.189 / 0.071 | 0.191 / 0.009 | 0.0084 |
    | 200 m | 0.20 | 0.60 + dropouts | 0.33 | 0.132 / 0.084 | 0.138 / 0.021 | 0.0134 |

    Old build (362f2a7, Win → Ubuntu): 120 m 0.417, 170 m 0.402 (−4 %). Now 170 m −25 %, 200 m −45 % (incl. dropouts).
  - PTT end, both directions, 10 presses where the voice and PTT stop together. Times are ms after the listener sees PTT-off:

    | | Tap goes quiet | Radio voice ends | Radio − tap (negative = cut) |
    |---|---|---|---|
    | H→C | +290 (267..335) | +285 (267..334) | 0 (−21..+4) |
    | C→H | +323 (275..357) | +317 (269..338) | −2 (−19..+2) |
    | Old build, Win → Ubuntu | +293..773 | fixed ~+300 | −18..−459 |
    | Old build, Ubuntu → Win | +292..314 | fixed ~+300 | 0..−13 |

    The −20 ms worst case is the 1024-sample level meter, not a cut. Still talking at PTT-off: radio stops at +998 / +1001 ms (1 s cap). Voice stopped 0.5 s before PTT-off: the tap was already quiet, so by the rule the radio stops within 150 ms of PTT-off (the edges can't show it; voice had ended).
- What surprised me:
  - A Unity AudioSource beyond its max distance (linear rolloff to 0) goes virtual, and a virtual source's timeSamples advances outside the mixer. Vivox uses that position as its read pointer and silences behind it, so anything mirroring the tap needs the tap to stay real.
  - Vivox's tap writes the stream from a coroutine (after Update) and jumps its read pointer on overrun.
  - AudioSource.volume applies before the filter chain, so a compressor in the chain undoes any volume-based signal drop.
  - Gating the mirror by gain instead of Stop/Play (so it never restarts) made things worse (12 + 2 gaps), reverted.
- Keep: S02Gain-style gain at the end of the radio chain; PTT-end rule (tap quiet 150 ms, max 1 s); Mirror rules (loop, lead 256, LateUpdate, tap never virtual).
- Open: residual ~15 ms one-buffer slips (0–12 per 32 presses, varies run to run and by direction); Linux and cross-OS not re-measured this round; ear test with a friend. If the residual is audible in the ear test, options: accept it, try another approach, or Dissonance (D28 fallback). Jakub decides.
