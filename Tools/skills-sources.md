# Third-party skills: sources

Copied as plain folders into `.claude/skills/` on 2026-10-03. No plugin install, no installer. To update: re-copy from the source at a new commit, re-review, re-apply the local changes.

| Skill | Source | Commit | Local changes |
|---|---|---|---|
| `blender-lpm-skill` | https://github.com/ozanzeng/blender-LPM-skill (`skills/blender-lpm-skill`) | `687d0a4` | Deleted `scripts/make_turnaround.py`, `scripts/multiview_to_mesh.py` (fal.ai, read the key from `~/.claude.json`) and `scripts/hy3d_local_mv.py` (local Hunyuan3D). Removed their references from `SKILL.md` and `references/reference-to-lowpoly.md` (turnaround views are supplied by hand; Route C / C-local dropped). |
| `lowpoly-style` | https://github.com/arjun988/blender-skills (`.claude/skills/lowpoly-style`) | `8f778d2` | Copied the shared `references/polycount-budgets.md` into the skill and fixed its path. |
| `unity-export` | https://github.com/arjun988/blender-skills (`.claude/skills/unity-export`) | `8f778d2` | None. |

Not installed: arjun988's plugin (its `.mcp.json` adds an unpinned `uvx blender-mcp` on port 9876), `blender-game-skills`.
