# Story 010: Create Unity 6 project and URP settings

> **Epic**: EP-CORE — Foundation & Repo Bootstrap
> **Status**: Blocked — BLOCKED: ADR-0022 is Proposed — accept it with `/architecture-decision accept ADR-0022` once decided
> **Layer**: Foundation
> **Type**: Config/Data
> **Estimate**: M
> **Manifest Version**: 2026-11-04
> **Tracker ticket**: FND-003 (stub: `production/epics/Foundation/story-fnd-003.md`)

## Context

**GDD**: `design/gdd/systems-index.md` (row #31 Rendering & VFX? no — row for Foundation/bootstrap; doc 24 §1 Phase 0; doc 17 engine reference)
**Requirement**: `TR-fnd-010`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0022: Pixel-art / 2.5D Art Direction
**ADR Decision Summary**: *(Proposed — not yet decided.)* The ADR governs the art direction the Unity project and its URP settings are set up for; while it stays Proposed, this story's Unity-side decisions are not final.
**ADR Version**: Version 0.1 — 16/10/2026

**Engine**: Unity 6.3 LTS (C# / URP) | **Risk**: HIGH
**Engine Notes**: Unity 6.3 LTS is rated HIGH in `docs/engine-reference/unity/VERSION.md` (first LTS in two years — Platform Toolkit, Nintendo Switch 2 support, Android XR, desktop screen reader support, Shader Build Settings). **No Unity 6.3 LTS editor is installed on this machine**: the only editor found is 6000.4.0f1 at `D:/Dev/Unity/6000.4.0f1/Editor/Unity.exe` — the 6.4 line, not 6.3. The pin stays 6.3 LTS; the editor must be installed before AC1/AC2 can be executed. `project.yaml` `engine.path` is still a TODO.

**Control Manifest Rules (Foundation layer)**:
- Required: use the **Input System package** (`InputAction`, `Keyboard.current`, `Mouse.current`) — source: `docs/engine-reference/unity/current-best-practices.md`
- Required: load assets through **Addressables**, releasing handles on destroy — source: `docs/engine-reference/unity/current-best-practices.md`
- Required: load scenes with **`SceneManager.LoadScene`** — source: `docs/engine-reference/unity/deprecated-apis.md`
- Forbidden: **never use the legacy `Input` class** — source: `docs/engine-reference/unity/deprecated-apis.md`
- Forbidden: **never call `Resources.Load()`** or load assets synchronously — source: `docs/engine-reference/unity/deprecated-apis.md`
- Forbidden: **never call `Application.LoadLevel()`** — source: `docs/engine-reference/unity/deprecated-apis.md`
- Guardrail: (none performance-related at Foundation layer)

---

## Acceptance Criteria

*From doc 17 and the FND-003 ticket:*

- [ ] AC1: The project opens in Unity 6.3 LTS with C# and URP enabled, and the exact editor patch version, URP package version and 2D package candidate are recorded in the story file and in `project.yaml`
- [ ] AC2: A Windows standalone build launches an empty scene
- [ ] AC3: No 3D art asset is imported and no engine switch is recorded — the project stays Unity 6.3 LTS, and the 2D art direction from ADR-0022 is not pre-empted

---

## Implementation Notes

*Derived from doc 17 and the FND-003 ticket:*

- **Blocked work**: AC1 and AC2 cannot be executed until ADR-0022 is Accepted — the ticket's own Source/Trace names ADR-022, and the art direction determines which 2D package candidates are recorded. The implementer executes them as written the moment ADR-0022 is Accepted.
- Editor install: install the pinned 6.3 LTS editor (the VERSION.md reference lists 6000.3.26f1 as the latest 6.3 editor) and set `project.yaml` `engine.path` to it. The machine's 6000.4.0f1 is the wrong line — opening the project with it would silently pin the wrong engine version.
- The project is created at the Unity project root established by story 001 (the repo's top-level Unity root, so the code root is the Unity root and the test root is `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/` — never `tests/unit/`, which Unity does not compile).
- Record the exact versions: editor patch, URP package, and the 2D package candidate (e.g. 2D Sprite package) — the ticket requires recording the candidates even when the art direction is still undecided.
- AC3's "no 3D art assumption" means: the default scene is empty, no 3D primitive is imported as a placeholder, and the project does not commit to a 3D pipeline. Any placeholder used to prove AC2 must be removed or be 2D.
- Once this story closes, the CI Unity job's skip (story 005, AC2) is removed and `project.yaml` `commands.smoke` gets its real Unity invocation.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001 (FND-001): the repo folders this project lands in
- Story 005 (FND-005): the CI Unity job, which stays skipped until this story closes
- Any actual gameplay or art asset: ADR-0022 governs the art direction and is not yet decided

---

## Files

- Unity project root (`ProjectSettings/`, `Packages/manifest.json`, `Assets/`)
- `project.yaml` (`engine.path`, `commands.build`, `commands.test`, `commands.smoke`)
- `[test root]` — none in this story (Config/Data); the Unity test roots are `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`

---

## QA Test Cases

*Written by qa-lead at story creation. Config/Data → advisory smoke-check form; every criterion requires a full Unity install and is therefore **DEFERRED** (not BLOCKED) under the QL-STORY-READY rule for criteria needing a full game build.*

- **AC1**: Project opens in Unity 6.3 LTS with C# and URP; versions recorded
  - Setup: install the pinned Unity 6.3 LTS editor (per `docs/engine-reference/unity/VERSION.md`, 6000.3.26f1 is the latest 6.3 editor — the machine's 6000.4.0f1 is the 6.4 line and must not be used), set `project.yaml` `engine.path`, open the project
  - Verify: the project opens with no compile errors; URP is the active render pipeline in Project Settings; the exact editor patch version, URP package version and 2D package candidate are recorded in this story file and in `project.yaml`
  - Pass condition: the project opens in the pinned 6.3 editor, URP is active, and all three version strings are recorded. DEFERRED — needs the Unity 6.3 LTS editor installed, which is not on this machine (only 6000.4.0f1)
- **AC2**: Windows standalone build launches an empty scene
  - Setup: with AC1 satisfied, build the Windows standalone from the project
  - Verify: the built executable launches and shows an empty scene with no console errors; the build completes with the URP asset pipeline configured
  - Pass condition: the executable launches and the scene is empty. DEFERRED — depends on AC1
- **AC3**: No 3D art asset imported; no engine switch recorded
  - Setup: with AC1 satisfied, inspect the committed project
  - Verify: no 3D mesh/model asset is committed under `Assets/`; `project.yaml` `engine.name` still reads Unity and `engine.version` still reads 6.3 LTS; no ADR or doc records an engine switch
  - Pass condition: zero 3D art assets, the engine pin unchanged, no engine-switch record. DEFERRED — depends on AC1

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- Config/Data: smoke check pass (`production/qa/smoke-*.md`)
- Status: [ ] Not yet created — blocked on ADR-0022 (Proposed) and on the Unity 6.3 LTS editor not being installed

---

## Dependencies

- Depends on: `production/epics/ep-core/story-001-monorepo-folder-architecture.md`
- Unlocks: `production/epics/ep-core/story-005-ci-pipeline.md` (removes the Unity job's skip)
