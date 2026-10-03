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
-

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

### Decisions changed
- D24 Engine version: Unity 6.3 LTS → Unity 6.6 (6000.6.4f1).
- D22 Body cam: provisional → optional → provisional again: body-cam view is an optional camera view; body-cam features kept.
- D27 Polybrush dropped (deprecated from Unity 6.3); collab-proxy, Visual Scripting, AI Inference to be removed.
- D28 Connection method added to S05.
- Plan step 5 split into setup → prototype spec draft → spikes.

### New ideas added
- none

### Problems / blockers
- none

### Hours (rough)
-

### Next week
- 5a setup via Claude Code (`Tools/cc-setup-prompt.md`).
- 5b prototype spec draft.
- Start S01 (network baseline + carrying).
