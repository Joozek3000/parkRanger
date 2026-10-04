# Park Rangers — Devlog

One entry per week, newest at the top. Copy the template below.

---

## Template

```
## Week YYYY-Www (Mon DD – Sun DD Month)

### Done
-

### Decisions changed
- (link to decisions.md entry, what it was → what it is now)

### New ideas added
- (idea → bucket in ideas.md)

### Problems / blockers
-

### Hours (rough)
- S01: ~4 h total (time box 18 h). One data point; spike boxes may be generous when Claude Code builds and Jakub tests. Re-check after S05.

### Next week
-
```

---

## Week 2026-W40 (Mon 28 Sep – Sun 4 Oct)

### Done
- Idea dump, pitch and pillars, core loop (draft), idea sorting.
- Unity project (6000.6.4f1) created with planned packages, template content only.
- Repo root set up at `C:\projects\parkRanger`: `Unity/`, `Art/Source`, `Art/Export`, `Design/`, `Design/Spikes`, `Tools/`.
- Design docs created: pillars, decisions, ideas, plan, devlog.
- Spike specs S01–S05 written in `Design/Spikes/`.
- 5a setup done via Claude Code: git + LFS (local only), CLAUDE.md, spike skill, Unity CLI, Blender skills. First commit 12f174b.
- 5b prototype spec draft written (`prototype-spec.md`); Q1–Q9 set to defaults.
- S01 done: owner-authoritative movement, carry model C (B fallback, A out). Shared spike baseline in `_Spikes/Shared/`.

### Decisions changed
- D24 Engine version: Unity 6.3 LTS → Unity 6.6 (6000.6.4f1).
- D22 Body cam: provisional → optional → provisional again: body-cam view is an optional camera view; body-cam features kept.
- D27 Polybrush dropped (deprecated from Unity 6.3); collab-proxy, Visual Scripting, AI Inference to be removed.
- D28 Connection method added to S05.
- D26 settled for movement and carrying (S01).
- Plan step 5 split into setup → prototype spec draft → spikes.

### New ideas added
- Two-player heavy carry → Inbox

### Problems / blockers
- none

### Hours (rough)
-

### Next week
- S05: connection (Steam lobbies vs Unity Relay) + voice.
- Then S02 radio.
