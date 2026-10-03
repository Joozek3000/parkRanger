# Claude Code setup prompt — Park Rangers (plan step 5a)

Run Claude Code from `C:\projects\parkRanger` and paste everything below the line.

---

You are setting up the repo for my game project, Park Rangers. I'm Jakub, a solo developer. Work through the phases below in order. Stop at every **CHECKPOINT** and wait for my reply. Write in a plain, direct style.

## Context

- Repo root: `C:\projects\parkRanger` (Windows).
  - `Unity\`: Unity project, editor **6000.6.4f1** (Unity 6.6), URP. Do not change the editor version.
  - `Art\Source\` (.blend files), `Art\Export\` (FBX/GLB), `Design\` (design docs), `Design\Spikes\` (spike specs), `Tools\`.
- `Design\` is the source of truth for design. Read `Design\plan.md`, `Design\decisions.md` and `Design\Spikes\README.md` before starting. Do not edit `Design\` files in this task except to tick items in the `plan.md` setup checklist at the end.
- Pre-production. No gameplay code yet (decision D31). This task is setup only.

## Phase 0 — Check before changing anything

1. Check whether Unity is running (`Unity.exe` process). If it is, ask me to close it and wait.
2. Report versions: `git --version`, `git lfs version`, `git config user.name`, `git config user.email`.
3. Report whether `C:\projects\parkRanger` is already a git repo.
4. Report which Claude Code plugins, skills and MCP servers are installed (user and project level). Check specifically for: an official Unity plugin/integration, `unity-perf`, a Unity MCP server, a Blender MCP server.
5. Find `UnityYAMLMerge.exe` for editor 6000.6.4f1 (usually `<Unity install>\Editor\Data\Tools\UnityYAMLMerge.exe`; Unity Hub default is `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\`).

**CHECKPOINT 1:** report the results. If Git or Git LFS is missing, tell me the winget command to install it; do not install system software yourself.

## Phase 1 — Git + LFS

1. `git init` at `C:\projects\parkRanger` (default branch `main`), `git lfs install`.
2. `.gitignore` at the repo root, based on GitHub's Unity template with paths prefixed for the `Unity/` subfolder. Must cover at least:
   - `Unity/[Ll]ibrary/`, `Unity/[Tt]emp/`, `Unity/[Oo]bj/`, `Unity/[Bb]uild/`, `Unity/[Bb]uilds/`, `Unity/[Ll]ogs/`, `Unity/[Uu]ser[Ss]ettings/`, `Unity/[Mm]emoryCaptures/`, `Unity/[Rr]ecordings/`
   - `Unity/*.csproj`, `Unity/*.sln`, `Unity/*.slnx`, `Unity/*.user`, `Unity/.vs/`, `Unity/.vscode/`, `*.pidb`, `*.booproj`, `*.svd`, `*.pdb`, `*.mdb`, `*.opendb`, `*.VC.db`
   - crash/build reports, `sysinfo.txt`, `*.apk`, `*.aab`
   - Blender backups: `*.blend1`, `*.blend2`, `*.blend@`
   - OS files: `Thumbs.db`, `Desktop.ini`, `.DS_Store`
   - `/_to_delete/`
3. `.gitattributes`:
   - LFS: `*.blend *.fbx *.glb *.obj *.png *.jpg *.jpeg *.tga *.tif *.tiff *.psd *.exr *.hdr *.wav *.mp3 *.ogg *.aif *.mp4 *.mov *.ttf *.otf *.zip *.7z *.dll *.so *.dylib *.a *.unitypackage`
   - Unity YAML (`*.unity *.prefab *.asset *.mat *.anim *.controller *.overrideController *.physicMaterial *.mask *.playable *.meta *.lighting *.terrainlayer *.shadergraph *.shadersubgraph *.vfx *.inputactions`): `text merge=unityyamlmerge eol=lf`
   - `*.cs diff=csharp text eol=lf`, `*.md text eol=lf`, `* text=auto`
4. Configure the merge driver in the repo's local git config (not global), using the path from Phase 0:
   `merge.unityyamlmerge.name`, `merge.unityyamlmerge.driver = '<path>\UnityYAMLMerge.exe' merge -p %O %B %A %A`
5. Check Unity settings in `Unity/ProjectSettings/EditorSettings.asset`: asset serialization = Force Text, version control mode = Visible Meta Files. Report; fix only if wrong.
6. Do not commit yet.

## Phase 2 — Unity housekeeping (Unity closed)

1. Edit `Unity/Packages/manifest.json`: remove `com.unity.collab-proxy`, `com.unity.visualscripting`, `com.unity.ai.inference`. Keep everything else. Do not add Polybrush (deprecated from Unity 6.3, decision D27). Do not add spike packages (Dissonance, Vivox, Facepunch.Steamworks, PSX shaders): those are installed during their spikes.
2. Delete template content: `Unity/Assets/TutorialInfo/` (+ `.meta`), `Unity/Assets/Readme.asset` (+ `.meta`). Keep `SampleScene`, the renderer/pipeline assets, `InputSystem_Actions` and `DefaultNetworkPrefabs.asset`.
3. Create folders:
   ```
   Unity/Assets/_Project/Art/
   Unity/Assets/_Project/Audio/
   Unity/Assets/_Project/Materials/
   Unity/Assets/_Project/Prefabs/
   Unity/Assets/_Project/Scenes/
   Unity/Assets/_Project/Scripts/
   Unity/Assets/_Project/Settings/
   Unity/Assets/_Spikes/
   ```
4. Delete leftovers outside the repo content if they exist: the empty folder `C:\projects\parkRanger\parkRanger\` (only if empty) and `C:\projects\parkRanger\_to_delete\`.

**CHECKPOINT 2:** list every change. Ask me to open the project in Unity, let it resolve packages, and tell you if the Console shows errors. Wait. If there are errors, fix them before going on.

## Phase 3 — CLAUDE.md and project skill

1. Create `C:\projects\parkRanger\CLAUDE.md` (keep it under ~120 lines):
   - one-paragraph project summary (from `Design/pillars.md`)
   - repo layout and what goes where
   - Unity 6000.6.4f1, URP, Netcode for GameObjects; do not upgrade the editor or packages without asking
   - `Design/` is the source of truth and is maintained in a separate coordinator session. Claude Code may only: fill in the **Results** section of a spike spec, and tick checklist items in `Design/plan.md`. Any other design change: propose it, don't make it.
   - D31: no gameplay code outside `Unity/Assets/_Spikes/` until the prototype spec is done
   - spike rules (copy the short version from `Design/Spikes/README.md`)
   - code conventions: C#, namespaces `ParkRangers.*`, one class per file, Input System only, no singletons for networked state, server/host-authoritative unless a spike decided otherwise
   - git: small commits with clear messages; never commit `Library/`, `Temp/`, `UserSettings/`; LFS for binaries
   - Blender MCP warning: it runs AI-generated Python unguarded. Save/backup the `.blend` before running anything through it.
2. Create a project skill `.claude/skills/spike/SKILL.md` that runs a spike from its spec:
   - read `Design/Spikes/S0x-*.md` and `Design/Spikes/README.md`
   - create `Unity/Assets/_Spikes/S0x/` with its own scene
   - work through "What to build", then check each "Done when" item with me
   - track time against the time box and stop at it
   - write the Results section using the template; list decisions that need updating (do not edit `decisions.md`)
   - commit the spike work on a branch `spike/S0x`

**CHECKPOINT 3:** show me CLAUDE.md and the skill before saving them.

## Phase 4 — Plugins, skills, MCP (security review first)

For each item below: find the official source, then review it before installing. Review means: read every file that will run (scripts, hooks, MCP server code, install steps), list what it can do (file access outside the repo, network calls, shell commands, credentials), flag anything that downloads and runs code at install time or later. Then report and wait for my OK per item. Do not install anything I haven't approved.

1. Unity MCP via the installed AI Assistant package (`com.unity.ai.assistant` 2.20.0-pre.1): follow Unity's docs to connect Claude Code to the Unity editor. Verify by reading the Console or listing the open scene's objects.
2. Official Unity plugin for Claude Code (if one exists) and `unity-perf`: report what they are, where they come from, and whether they overlap with the Unity MCP.
3. Blender skills, review only (install only after my OK): `blender-LPM-skill`, arjun988 "Low Poly Style Specialist", `blender-game-skills`. My target is low-poly models with PSX-style rendering, exported as FBX/GLB to Unity.
4. Blender MCP: check whether the Blender Lab MCP add-on (v1.0.3, localhost:9876) is reachable from Claude Code. Report only; don't configure it unless I ask.

**CHECKPOINT 4:** review report, then install what I approve.

## Phase 5 — First commit and report

1. `git status`: confirm no `Library/`, `Temp/`, `UserSettings/` or large binaries outside LFS are staged. `git lfs ls-files` to confirm LFS tracking.
2. First commit: `chore: repo setup (git, lfs, unity housekeeping, CLAUDE.md)`.
3. Ask whether I want a remote (e.g. a private GitHub repo). Mention the LFS storage/bandwidth limits of the host. Don't create or push without my OK.
4. Tick the done items in `Design/plan.md` → Setup checklist.
5. Final report: what changed, what's installed, what's pending, anything that needs a decision. I'll pass it to my coordinator session.
