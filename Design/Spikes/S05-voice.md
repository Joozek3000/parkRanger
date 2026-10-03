# S05 — Connection + voice chat

Status: not started
Time box: 4 days (1 day research, 1 day connection test, 2 days voice test)
Changed 2026-10-03: connection method added (was voice only, 3 days).
Related: D20 Proximity voice, D28 Steam lobbies and voice (open), S02 Radio

## Goal

Pick how players connect over the internet, and the voice chat solution. The prototype fun test needs 2 players on different machines.

Proximity voice is the default and the radio filter chain (S02) has to be able to process the voice audio.

## Connection candidates

| Option | Notes to check |
|---|---|
| Steam lobbies + Steam networking (Facepunch.Steamworks + a Facepunch transport for Netcode for GameObjects) | Needs a Steam app ID (or test app 480 for development); friends invite flow; works behind NAT via Steam relay |
| Unity Relay + Lobby (Unity Gaming Services) | Join codes; works without Steam; current free tier/pricing; needs Unity sign-in per player |

## Voice candidates

| Option | What it is | Notes to check |
|---|---|---|
| Dissonance | Paid Unity asset; has an integration package for Netcode for GameObjects; voice goes over your own network connection | Price; per-player playback through Unity AudioSources so S02 filters apply; works over a Steam transport |
| Vivox | Unity Gaming Services voice; separate hosted service | Current pricing/free tier; Participant Tap components route a participant's audio to a Unity AudioSource (needed for S02); needs Unity account sign-in per player |
| Steam voice | Steam's voice capture/decode, exposed in Facepunch.Steamworks | Ties voice to Steam; you send the compressed data yourself over the game connection; playback through AudioSource is manual |

## Criteria

1. Proximity voice (3D, distance falloff, occlusion).
2. Radio routing: voice can be played through a radio AudioSource with the S02 filter chain, separate from the proximity voice.
3. Works with a host-based game, no dedicated server, players joining via Steam lobbies.
4. Latency and quality at 100 ms RTT.
5. Cost: upfront and per player.
6. Setup and maintenance effort for one developer.
7. Push-to-talk and voice-activity options; mute per player.

## What to build

- Day 1: read docs, check current prices and versions, fill in both tables. Pick top choice + fallback for each.
- Day 2: connection: two builds on two machines (or one machine + a friend) join the same session through the chosen method.
- Days 3–4: integrate the top choice into the S01 baseline:
  - proximity voice between 2 players in Play Mode, then between 2 builds on different machines if possible
  - route one player's voice through a test radio AudioSource with the S02 filter chain

## Done when

- [ ] Connection method chosen; two separate machines join and play in the S01 scene.
- [ ] Comparison tables filled in with sources and current prices.
- [ ] Top choice tested: proximity voice works between two clients.
- [ ] Voice can be routed through the radio filter chain.
- [ ] D28 updated with the connection and voice decisions.

## Out of scope

Lobby UI polish, matchmaking, moderation, text chat.
