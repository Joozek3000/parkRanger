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

### Connection findings (checked 2026-10-04)

| | Steam (Facepunch) | Unity Relay + Lobby |
|---|---|---|
| Packages, version | Facepunch.Steamworks 2.5.2 (2026-04-23, MIT) [C1]. NGO transport `com.community.netcode.transport.facepunch` 2.0.0, community repo, not archived; last change 2026-09-08 was an asmdef fix only. It bundles Facepunch.Steamworks **2.3.2 (2020)** [C2][C3]; swapping in 2.5.2 is untested. | `com.unity.services.multiplayer` 2.3.3 (2026-09-15; Sessions API, wraps Lobby + Relay) and `com.unity.transport` 2.7.4 (we already use UTP via NGO 2.13.3) [C4]. Official, maintained. |
| How it connects | Host opens a Steam relay socket; client connects by host SteamID through Valve's relay (SDR, NAT-safe, hides IPs) [C3][C5]. Invite/join via Steam lobby + friends overlay. | Host creates a session (Relay allocation), gets a join code; client enters the code. Traffic goes through Unity's relay servers. |
| Cost | Free networking. Dev with app 480 (Spacewar) is free. Own app ID: Steam Direct **$100** per app, recouped after $1,000 revenue [C6]; needs a Steamworks partner account (paperwork, tax, bank). | Relay free up to **50 avg monthly CCU** (then $0.16/CCU) and 3 GiB/CCU bandwidth up to 150 GiB/month; Lobby 10 GiB/month free per region group [C7]. A 2–4 player playtest is far inside this. Authentication free. |
| Accounts | Every player needs Steam running and logged in. | Jakub: link the Unity project to a Unity Cloud project (free, Unity ID he already has). Players: anonymous sign-in, invisible, no account. |
| Testing on one machine | **Not possible** with one Steam login: both builds get the same SteamID and can't connect to themselves. Needs 2 machines with 2 Steam accounts. | Works: two builds or MPPM. Each instance needs its own auth profile (`AuthenticationService.SwitchProfile`), otherwise both share one anonymous player ID. |
| Fit (criteria 3, 6) | Matches the shipping target (Steam friends invite flow, like Peak / Lethal Company). Maintenance risk: stale community transport, Steam must be running, harder testing. | Least effort now: no Steam dependency, UTP stays, same UGS sign-in reused by Vivox if chosen. No friends-list invite; join codes only. Extra hop through a Unity relay region. |

Recommendation: **top pick Unity Relay + Lobby (Sessions API)** for the prototype fun test; **fallback Steam (Facepunch)**, and revisit Steam lobbies when there is a real Steam app ID (release path, friend invites). Both keep NGO, so the switch later is a transport + lobby swap, not a rewrite.

## Voice candidates

| Option | What it is | Notes to check |
|---|---|---|
| Dissonance | Paid Unity asset; has an integration package for Netcode for GameObjects; voice goes over your own network connection | Price; per-player playback through Unity AudioSources so S02 filters apply; works over a Steam transport |
| Vivox | Unity Gaming Services voice; separate hosted service | Current pricing/free tier; Participant Tap components route a participant's audio to a Unity AudioSource (needed for S02); needs Unity account sign-in per player |
| Steam voice | Steam's voice capture/decode, exposed in Facepunch.Steamworks | Ties voice to Steam; you send the compressed data yourself over the game connection; playback through AudioSource is manual |

### Voice findings (checked 2026-10-04)

| | Dissonance | Vivox | Steam voice |
|---|---|---|---|
| Version | Dissonance Voice Chat 9.0.9 (2026-04-27), Unity 6000.0.23+ [V1]. NGO integration 9.0.8 (2026-03-12), free [V2]. | `com.unity.services.vivox` 16.12.1 (2026-09-17) [C4]. | Part of Steamworks SDK; exposed in Facepunch.Steamworks 2.5.2 [C1][V6]. |
| Cost | **€110.41 regular, €55.20 on sale** at time of check, one-time, Asset Store EULA (per-seat) [V1]. No per-player cost. | Free up to **5,000 PCU**; then $2,000 per 5,000 PCU [C7]. | Free. |
| Transport | Rides our own NGO connection (custom messages), so it works over Relay or a Steam transport [V2][V3]. Voice adds host bandwidth. | Separate Vivox servers; independent of the game transport. Needs UGS project + anonymous sign-in [V4]. | We send the compressed packets ourselves over NGO [V6]. |
| Proximity (1) | Built-in: positional playback per player, proximity rooms / position tracking [V3]. Occlusion through Unity audio. | Positional channels (Vivox does 3D distance). With Participant Taps, playback moves into Unity AudioSources, so Unity 3D + occlusion apply [V5]. | Manual: own AudioSource per player, own jitter buffer, decode via `DecompressVoice` into a streamed clip. |
| Radio routing (2) | One AudioSource per remote player via a playback prefab [V7]. **Audio Cloning** (`AudioCloneSource` → `AudioCloneSink`) exists for exactly the walkie-talkie case: same voice through extra AudioSources with their own filters, positional playback unchanged [V8]. | `VivoxParticipantTap` gives one participant's audio on an AudioSource [V5]. Playing the same voice through a second (radio) source is not documented; would need a second tap or copying samples. Known bug: taps go silent after scene loads, workaround is disable/enable the tap [V9]. | Full control (we own the samples), but all of it is our code. |
| PTT / VAD / mute (7) | Push-to-talk, voice activation, per-player mute, echo cancellation [V3]. | VAD settings, mute self/others, PTT via input mute [V4]. | Recording on/off only; VAD, mute UI ours. |
| Effort (6) | Low: drag-in prefabs, one integration package. | Medium: UGS setup, sign-in flow, tap lifecycle bugs. | High: a voice pipeline from scratch. |

Recommendation: **top pick Dissonance** (only option with built-in radio routing; works with either connection method; no service dependency). **Fallback Vivox** (free, official; radio routing needs to be proven). Steam voice is out: too much custom audio code for one developer.

Sources:
- [C1] Facepunch.Steamworks releases: https://github.com/Facepunch/Facepunch.Steamworks/releases
- [C2] Forum thread on the stale 2.3.2 dependency: https://discussions.unity.com/t/is-it-recommended-to-still-use-steamworks-facepunch-in-2026/1712429
- [C3] Facepunch transport (README, CHANGELOG, `FacepunchTransport.cs`): https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/main/Transports/com.community.netcode.transport.facepunch
- [C4] Unity package registry (`packages.unity.com/<package>`), queried 2026-10-04.
- [C5] Steam Networking Sockets: https://partner.steamgames.com/doc/api/ISteamNetworkingSockets
- [C6] Steam Direct fee: https://partner.steamgames.com/doc/gettingstarted/appfee
- [C7] UGS pricing (Relay, Lobby, Vivox, Authentication): https://unity.com/products/gaming-services/pricing
- [V1] Dissonance Voice Chat: https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078
- [V2] Dissonance for NGO: https://assetstore.unity.com/packages/tools/integration/dissonance-for-netcode-for-gameobjects-206514
- [V3] Dissonance docs (NGO quick start, proximity, push-to-talk, echo cancellation): https://placeholder-software.co.uk/dissonance/docs/
- [V4] Unity plugin `setup-vivox-voice-chat` skill notes (init order, anonymous sign-in, token minting).
- [V5] Vivox Audio Taps: https://docs.unity.com/en-us/vivox-unity/developer-guide/audio-taps/use-audio-taps
- [V6] Steam Voice: https://partner.steamgames.com/doc/features/voice
- [V7] Dissonance playback prefab: https://placeholder-software.co.uk/dissonance/docs/Tutorials/Playback-Prefab.html
- [V8] Dissonance audio cloning: https://placeholder-software.co.uk/dissonance/docs/Tutorials/Audio-Cloning.html
- [V9] Vivox tap silent after scene load: https://support.unity.com/hc/en-us/articles/42205921686548

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
- Day 2: connection: two builds on two machines (or one machine + a friend) join the same session through the chosen method. Add join-failure handling to the Shared baseline (OnClientStopped + DisconnectReason shown in the UI); a join to a missing host currently fails silently.
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
